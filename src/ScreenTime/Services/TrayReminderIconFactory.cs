using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ScreenTime.Core;

namespace ScreenTime.Services;

public static class TrayReminderIconFactory
{
    private static readonly object FrameCacheLock = new();
    private static readonly Dictionary<string, Bitmap[]> BitmapFrameCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Icon[]> IconFrameCache = new(StringComparer.OrdinalIgnoreCase);

    public static Icon[] CreateFrames(string reminderCharacter, CpuLoadLevel loadLevel, string? assetDirectory = null)
    {
        return CreateFrames(reminderCharacter, loadLevel, false, assetDirectory);
    }

    public static Icon[] CreateFrames(string reminderCharacter, CpuLoadLevel loadLevel, bool useDarkMode, string? assetDirectory = null)
    {
        var cacheKey = BuildCacheKey(reminderCharacter, loadLevel, useDarkMode, 32, assetDirectory);
        lock (FrameCacheLock)
        {
            if (IconFrameCache.TryGetValue(cacheKey, out var cachedFrames))
            {
                return CloneIcons(cachedFrames);
            }
        }

        var bitmaps = CreateBitmapFrames(reminderCharacter, loadLevel, useDarkMode, 32, assetDirectory);
        try
        {
            var frames = bitmaps.Select(CreatePngIcon).ToArray();
            lock (FrameCacheLock)
            {
                if (!IconFrameCache.ContainsKey(cacheKey))
                {
                    IconFrameCache[cacheKey] = CloneIcons(frames);
                }
            }

            return frames;
        }
        finally
        {
            foreach (var bitmap in bitmaps)
            {
                bitmap.Dispose();
            }
        }
    }

    public static Bitmap[] CreateBitmapFrames(
        string reminderCharacter,
        CpuLoadLevel loadLevel,
        bool useDarkMode,
        int frameSize,
        string? assetDirectory = null)
    {
        var cacheKey = BuildCacheKey(reminderCharacter, loadLevel, useDarkMode, frameSize, assetDirectory);
        lock (FrameCacheLock)
        {
            if (BitmapFrameCache.TryGetValue(cacheKey, out var cachedFrames))
            {
                return CloneFrames(cachedFrames);
            }
        }

        var assetFrames = TryLoadAssetBitmapFrames(reminderCharacter, loadLevel, useDarkMode, frameSize, assetDirectory);
        var frames = assetFrames.Length > 0
            ? assetFrames
            : CreateGeneratedBitmapFrames(reminderCharacter, loadLevel, useDarkMode, frameSize);

        lock (FrameCacheLock)
        {
            if (!BitmapFrameCache.ContainsKey(cacheKey))
            {
                BitmapFrameCache[cacheKey] = CloneFrames(frames);
            }
        }

        return frames;
    }

    private static Icon[] CloneIcons(IEnumerable<Icon> frames)
    {
        return frames.Select(frame => (Icon)frame.Clone()).ToArray();
    }

    private static Bitmap[] CloneFrames(IEnumerable<Bitmap> frames)
    {
        return frames.Select(frame => (Bitmap)frame.Clone()).ToArray();
    }

    private static string BuildCacheKey(
        string reminderCharacter,
        CpuLoadLevel loadLevel,
        bool useDarkMode,
        int frameSize,
        string? assetDirectory)
    {
        var explicitSignature = BuildDirectorySignature(assetDirectory);
        var bundledDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "TrayRunners", reminderCharacter);
        var bundledSignature = BuildDirectorySignature(bundledDirectory);
        return string.Join(
            "|",
            reminderCharacter.ToLowerInvariant(),
            loadLevel,
            useDarkMode,
            frameSize,
            explicitSignature,
            bundledSignature);
    }

    private static string BuildDirectorySignature(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return string.Empty;
        }

        return string.Join(
            ";",
            Directory.GetFiles(directory, "*.*")
                .Where(path => Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase)
                    || Path.GetExtension(path).Equals(".ico", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path =>
                {
                    var info = new FileInfo(path);
                    return $"{info.FullName}:{info.Length}:{info.LastWriteTimeUtc.Ticks}";
                }));
    }

    private static Bitmap[] CreateGeneratedBitmapFrames(string reminderCharacter, CpuLoadLevel loadLevel, bool useDarkMode, int frameSize)
    {
        return Enumerable.Range(0, 5)
            .Select(frame => CreateGeneratedBitmapFrame(reminderCharacter, loadLevel, useDarkMode, frame, frameSize))
            .ToArray();
    }

    private static Bitmap[] TryLoadAssetBitmapFrames(string reminderCharacter, CpuLoadLevel loadLevel, bool useDarkMode, int frameSize, string? assetDirectory)
    {
        var explicitFrames = TryLoadAssetBitmapFramesFromDirectory(reminderCharacter, loadLevel, useDarkMode, frameSize, assetDirectory);
        if (explicitFrames.Length > 0)
        {
            return explicitFrames;
        }

        var bundledDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "TrayRunners", reminderCharacter);
        return TryLoadAssetBitmapFramesFromDirectory(reminderCharacter, loadLevel, useDarkMode, frameSize, bundledDirectory);
    }

    private static Bitmap[] TryLoadAssetBitmapFramesFromDirectory(string reminderCharacter, CpuLoadLevel loadLevel, bool useDarkMode, int frameSize, string? assetDirectory)
    {
        if (string.IsNullOrWhiteSpace(assetDirectory) || !Directory.Exists(assetDirectory))
        {
            return [];
        }

        var loadName = loadLevel.ToString().ToLowerInvariant();
        var iconPatterns = new[]
        {
            $"{reminderCharacter}_tray_{loadName}_*.ico",
            $"{reminderCharacter}_tray_*.ico"
        };

        foreach (var pattern in iconPatterns)
        {
            var files = Directory.GetFiles(assetDirectory, pattern)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (files.Length == 0)
            {
                continue;
            }

            try
            {
                return files.Select(path => CreateBitmapFromAsset(path, useDarkMode, frameSize)).ToArray();
            }
            catch
            {
                // Invalid asset frames should not break the tray; generated frames remain the fallback.
                return [];
            }
        }

        var pngFiles = GetPngAssetFiles(assetDirectory, reminderCharacter, frameSize);
        if (pngFiles.Length == 0)
        {
            return [];
        }

        try
        {
            return pngFiles.Select(path => CreateBitmapFromAsset(
                path,
                useDarkMode,
                frameSize,
                IsOverlayPngFrame(path, reminderCharacter))).ToArray();
        }
        catch
        {
            // Invalid asset frames should not break the tray; generated frames remain the fallback.
            return [];
        }
    }

    private static string[] GetPngAssetFiles(string assetDirectory, string reminderCharacter, int frameSize)
    {
        var files = Directory.GetFiles(assetDirectory, $"{reminderCharacter}_*.png")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var overlayPrefix = $"{reminderCharacter}_overlay_";
        var overlayFiles = files
            .Where(path => Path.GetFileName(path).StartsWith(overlayPrefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var trayFiles = files
            .Where(path => IsTrayPngFrame(path, reminderCharacter, overlayPrefix))
            .ToArray();

        return frameSize >= 128 && overlayFiles.Length > 0
            ? overlayFiles
            : trayFiles;
    }

    private static bool IsOverlayPngFrame(string path, string reminderCharacter)
    {
        return Path.GetFileName(path).StartsWith(
            $"{reminderCharacter}_overlay_",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTrayPngFrame(string path, string reminderCharacter, string overlayPrefix)
    {
        var fileName = Path.GetFileName(path);
        if (fileName.StartsWith(overlayPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var suffixStart = reminderCharacter.Length + 1;
        if (stem.Length <= suffixStart)
        {
            return false;
        }

        var suffix = stem[suffixStart..];
        return suffix.Length <= 2 && suffix.All(char.IsDigit);
    }

    private static Bitmap CreateBitmapFromAsset(string path, bool useDarkMode, int frameSize, bool preserveLayout = false)
    {
        if (Path.GetExtension(path).Equals(".ico", StringComparison.OrdinalIgnoreCase))
        {
            using var icon = new Icon(path);
            using var iconBitmap = icon.ToBitmap();
            return NormalizeAssetBitmap(iconBitmap, useDarkMode, frameSize, preserveLayout);
        }

        using var source = new Bitmap(path);
        return NormalizeAssetBitmap(source, useDarkMode, frameSize, preserveLayout);
    }

    private static Bitmap NormalizeAssetBitmap(Bitmap source, bool useDarkMode, int frameSize, bool preserveLayout = false)
    {
        var removeLightBackground = HasLightOpaqueCorner(source);
        var tint = useDarkMode && ShouldTintForDarkMode(source, removeLightBackground) ? Color.White : (Color?)null;
        var bounds = preserveLayout
            ? new Rectangle(0, 0, source.Width, source.Height)
            : FindVisibleBounds(source, removeLightBackground);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            bounds = new Rectangle(0, 0, source.Width, source.Height);
        }

        using var cleaned = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var pixel = source.GetPixel(x, y);
                if (IsBackgroundPixel(pixel, removeLightBackground))
                {
                    continue;
                }

                cleaned.SetPixel(x, y, tint.HasValue ? Color.FromArgb(pixel.A, tint.Value) : pixel);
            }
        }

        return RenderToFrame(cleaned, bounds, frameSize);
    }

    private static Bitmap RenderToFrame(Bitmap source, Rectangle sourceBounds, int frameSize)
    {
        var target = new Bitmap(frameSize, frameSize, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(target);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = frameSize > 48 ? InterpolationMode.HighQualityBicubic : InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var padding = frameSize <= 32 ? 0 : Math.Max(1, frameSize / 24);
        var maxWidth = frameSize - (padding * 2);
        var maxHeight = frameSize - (padding * 2);
        var scale = Math.Min(maxWidth / (double)sourceBounds.Width, maxHeight / (double)sourceBounds.Height);
        var width = Math.Max(1, (int)Math.Round(sourceBounds.Width * scale));
        var height = Math.Max(1, (int)Math.Round(sourceBounds.Height * scale));
        var destination = new Rectangle((frameSize - width) / 2, (frameSize - height) / 2, width, height);
        graphics.DrawImage(source, destination, sourceBounds, GraphicsUnit.Pixel);
        return target;
    }

    private static Rectangle FindVisibleBounds(Bitmap source, bool removeLightBackground)
    {
        var left = source.Width;
        var top = source.Height;
        var right = -1;
        var bottom = -1;

        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                if (IsBackgroundPixel(source.GetPixel(x, y), removeLightBackground))
                {
                    continue;
                }

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        return right < left || bottom < top
            ? Rectangle.Empty
            : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    private static bool HasLightOpaqueCorner(Bitmap source)
    {
        var corners = new[]
        {
            source.GetPixel(0, 0),
            source.GetPixel(source.Width - 1, 0),
            source.GetPixel(0, source.Height - 1),
            source.GetPixel(source.Width - 1, source.Height - 1)
        };

        return corners.Count(pixel => pixel.A > 200 && IsLightNeutral(pixel)) >= 2;
    }

    private static bool ShouldTintForDarkMode(Bitmap source, bool removeLightBackground)
    {
        var visiblePixels = 0;
        var totalBrightness = 0;
        var totalChroma = 0;

        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var pixel = source.GetPixel(x, y);
                if (IsBackgroundPixel(pixel, removeLightBackground))
                {
                    continue;
                }

                var max = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
                var min = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
                totalBrightness += (pixel.R + pixel.G + pixel.B) / 3;
                totalChroma += max - min;
                visiblePixels++;
            }
        }

        if (visiblePixels == 0)
        {
            return false;
        }

        var averageBrightness = totalBrightness / visiblePixels;
        var averageChroma = totalChroma / visiblePixels;
        return averageBrightness < 140 && averageChroma < 28;
    }

    private static bool IsBackgroundPixel(Color pixel, bool removeLightBackground)
    {
        return pixel.A < 16 || (removeLightBackground && IsLightNeutral(pixel));
    }

    private static bool IsLightNeutral(Color pixel)
    {
        var max = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
        var min = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
        return min >= 220 && max - min <= 24;
    }

    private static Icon CreatePngIcon(Bitmap bitmap)
    {
        using var pngStream = new MemoryStream();
        bitmap.Save(pngStream, ImageFormat.Png);
        var pngData = pngStream.ToArray();

        using var icoStream = new MemoryStream();
        using var writer = new BinaryWriter(icoStream);

        writer.Write((short)0);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write((byte)(bitmap.Width >= 256 ? 0 : bitmap.Width));
        writer.Write((byte)(bitmap.Height >= 256 ? 0 : bitmap.Height));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((short)1);
        writer.Write((short)32);
        writer.Write(pngData.Length);
        writer.Write(22);
        writer.Write(pngData);

        icoStream.Position = 0;
        return new Icon(icoStream);
    }

    [Obsolete("Use CreateFrames(string, CpuLoadLevel, string?) so asset frames can be loaded first.")]
    public static Icon[] CreateFrames(string reminderCharacter, CpuLoadLevel loadLevel)
    {
        return CreateFrames(reminderCharacter, loadLevel, false, null);
    }

    private static Bitmap CreateGeneratedBitmapFrame(string reminderCharacter, CpuLoadLevel loadLevel, bool useDarkMode, int frame, int frameSize)
    {
        using var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.Clear(Color.Transparent);

        var isDog = reminderCharacter.Equals("dog", StringComparison.OrdinalIgnoreCase);
        var iconColor = useDarkMode ? Color.White : Color.Black;

        if (isDog)
        {
            DrawPixelDog(graphics, iconColor, frame);
        }
        else
        {
            DrawPixelCat(graphics, iconColor, frame);
        }

        return frameSize == 32
            ? (Bitmap)bitmap.Clone()
            : RenderToFrame(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height), frameSize);
    }

    private static void DrawPixelCat(Graphics graphics, Color color, int frame)
    {
        var bob = frame is 1 or 3 ? -2 : 0;
        var frontLeg = frame switch { 0 => 0, 1 => -2, 2 => -4, 3 => -2, _ => 0 };
        var backLeg = frame switch { 0 => -4, 1 => -2, 2 => 0, 3 => 2, _ => 0 };
        var tailLift = frame switch { 0 => 0, 1 => -2, 2 => -4, 3 => -2, _ => 0 };

        FillPixels(graphics, color,
            (4, 15 + bob, 2, 2),
            (2, 13 + bob + tailLift, 2, 2),
            (4, 13 + bob + tailLift, 2, 2),
            (6, 15 + bob, 2, 2),
            (8, 15 + bob, 2, 2),
            (10, 15 + bob, 2, 2),
            (12, 15 + bob, 2, 2),
            (6, 17 + bob, 2, 2),
            (8, 17 + bob, 2, 2),
            (10, 17 + bob, 2, 2),
            (12, 17 + bob, 2, 2),
            (14, 13 + bob, 2, 2),
            (16, 11 + bob, 2, 2),
            (18, 11 + bob, 2, 2),
            (16, 9 + bob, 2, 2),
            (20, 9 + bob, 2, 2),
            (20, 13 + bob, 2, 2),
            (18, 15 + bob, 2, 2),
            (8 + backLeg, 19 + bob, 2, 4),
            (14 + frontLeg, 19 + bob, 2, 4),
            (20, 15 + bob, 2, 2));
    }

    private static void DrawPixelDog(Graphics graphics, Color color, int frame)
    {
        var bob = frame is 1 or 3 ? -2 : 0;
        var frontLeg = frame switch { 0 => 0, 1 => -2, 2 => -4, 3 => -2, _ => 0 };
        var backLeg = frame switch { 0 => -4, 1 => -2, 2 => 0, 3 => 2, _ => 0 };
        var earDrop = frame is 1 or 2 ? 2 : 0;

        FillPixels(graphics, color,
            (3, 13 + bob, 2, 2),
            (5, 15 + bob, 2, 2),
            (7, 15 + bob, 2, 2),
            (9, 15 + bob, 2, 2),
            (11, 15 + bob, 2, 2),
            (7, 17 + bob, 2, 2),
            (9, 17 + bob, 2, 2),
            (11, 17 + bob, 2, 2),
            (13, 13 + bob, 2, 2),
            (15, 11 + bob, 2, 2),
            (17, 11 + bob, 2, 2),
            (19, 13 + bob, 2, 2),
            (21, 13 + bob, 2, 2),
            (17, 13 + bob + earDrop, 2, 4),
            (21, 15 + bob, 2, 2),
            (9 + backLeg, 19 + bob, 2, 4),
            (15 + frontLeg, 19 + bob, 2, 4),
            (23, 15 + bob, 2, 2));
    }

    private static void FillPixels(Graphics graphics, Color color, params (int X, int Y, int Width, int Height)[] pixels)
    {
        using var brush = new SolidBrush(color);
        foreach (var (x, y, width, height) in pixels)
        {
            graphics.FillRectangle(brush, x, y, width, height);
        }
    }

    private static void DrawCat(Graphics graphics, Pen pen, Pen thinPen, Brush brush, int bob, int legShift)
    {
        graphics.DrawEllipse(pen, 6, 15 + bob, 15, 8);
        graphics.DrawEllipse(pen, 18, 9 + bob, 9, 8);

        PointF[] leftEar = [new(19.5f, 10.5f + bob), new(21.5f, 5.5f + bob), new(23.5f, 10.5f + bob)];
        PointF[] rightEar = [new(23.0f, 10.5f + bob), new(27.0f, 6.0f + bob), new(27.0f, 12.5f + bob)];
        graphics.DrawPolygon(pen, leftEar);
        graphics.DrawPolygon(pen, rightEar);

        graphics.FillEllipse(brush, 22, 12 + bob, 2, 2);
        graphics.FillEllipse(brush, 25, 12 + bob, 2, 2);
        graphics.DrawLine(thinPen, 24, 15 + bob, 23, 16 + bob);
        graphics.DrawLine(thinPen, 24, 15 + bob, 26, 16 + bob);

        graphics.DrawLine(pen, 9, 22 + bob, 8 + legShift, 27);
        graphics.DrawLine(pen, 15, 22 + bob, 15 - legShift, 27);
        graphics.DrawLine(pen, 21, 18 + bob, 23 + legShift, 26);
        graphics.DrawArc(pen, 1, 9 + bob, 13, 11, 185, 130);
    }

    private static void DrawDog(Graphics graphics, Pen pen, Pen thinPen, Brush brush, int bob, int legShift)
    {
        graphics.DrawEllipse(pen, 6, 15 + bob, 15, 8);
        graphics.DrawEllipse(pen, 17, 9 + bob, 10, 9);
        graphics.DrawBezier(pen, 18, 10 + bob, 15, 7 + bob, 17, 16 + bob, 20, 16 + bob);
        graphics.DrawBezier(pen, 26, 10 + bob, 30, 8 + bob, 28, 17 + bob, 25, 16 + bob);

        graphics.FillEllipse(brush, 21, 13 + bob, 2, 2);
        graphics.FillEllipse(brush, 25, 13 + bob, 2, 2);
        graphics.FillEllipse(brush, 26, 16 + bob, 2, 2);
        graphics.DrawArc(thinPen, 23, 16 + bob, 4, 4, 25, 130);

        graphics.DrawLine(pen, 9, 22 + bob, 8 + legShift, 27);
        graphics.DrawLine(pen, 15, 22 + bob, 15 - legShift, 27);
        graphics.DrawLine(pen, 21, 18 + bob, 23 + legShift, 26);
        graphics.DrawArc(pen, 1, 9 + bob, 13, 11, 185, 130);
    }

    private static void DrawLoadMarks(Graphics graphics, Pen pen, CpuLoadLevel loadLevel)
    {
        var markCount = loadLevel switch
        {
            CpuLoadLevel.Low => 1,
            CpuLoadLevel.Normal => 2,
            CpuLoadLevel.High => 3,
            _ => 4
        };

        for (var i = 0; i < markCount; i++)
        {
            var x = 4 + (i * 3);
            graphics.DrawLine(pen, x, 5, x + 1, 3);
        }
    }
}

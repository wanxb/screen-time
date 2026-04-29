using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using ScreenTime.Core;

namespace ScreenTime.Services;

public static class TrayReminderIconFactory
{
    public static Icon[] CreateFrames(string reminderCharacter, CpuLoadLevel loadLevel, string? assetDirectory = null)
    {
        return CreateFrames(reminderCharacter, loadLevel, false, assetDirectory);
    }

    public static Icon[] CreateFrames(string reminderCharacter, CpuLoadLevel loadLevel, bool useDarkMode, string? assetDirectory = null)
    {
        var assetFrames = TryLoadAssetFrames(reminderCharacter, loadLevel, useDarkMode, assetDirectory);
        return assetFrames.Length > 0
            ? assetFrames
            : CreateGeneratedFrames(reminderCharacter, loadLevel, useDarkMode);
    }

    private static Icon[] CreateGeneratedFrames(string reminderCharacter, CpuLoadLevel loadLevel, bool useDarkMode)
    {
        return Enumerable.Range(0, 5)
            .Select(frame => CreateFrame(reminderCharacter, loadLevel, useDarkMode, frame))
            .ToArray();
    }

    private static Icon[] TryLoadAssetFrames(string reminderCharacter, CpuLoadLevel loadLevel, bool useDarkMode, string? assetDirectory)
    {
        var explicitFrames = TryLoadAssetFramesFromDirectory(reminderCharacter, loadLevel, useDarkMode, assetDirectory);
        if (explicitFrames.Length > 0)
        {
            return explicitFrames;
        }

        var bundledDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "TrayRunners", reminderCharacter);
        return TryLoadAssetFramesFromDirectory(reminderCharacter, loadLevel, useDarkMode, bundledDirectory);
    }

    private static Icon[] TryLoadAssetFramesFromDirectory(string reminderCharacter, CpuLoadLevel loadLevel, bool useDarkMode, string? assetDirectory)
    {
        if (string.IsNullOrWhiteSpace(assetDirectory) || !Directory.Exists(assetDirectory))
        {
            return [];
        }

        var loadName = loadLevel.ToString().ToLowerInvariant();
        var patterns = new[]
        {
            $"{reminderCharacter}_tray_{loadName}_*.ico",
            $"{reminderCharacter}_tray_*.ico",
            $"{reminderCharacter}_*.png"
        };

        foreach (var pattern in patterns)
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
                return files.Select(path => CreateIconFromAsset(path, useDarkMode)).ToArray();
            }
            catch
            {
                // Invalid asset frames should not break the tray; generated frames remain the fallback.
                return [];
            }
        }

        return [];
    }

    private static Icon CreateIconFromAsset(string path, bool useDarkMode)
    {
        if (Path.GetExtension(path).Equals(".ico", StringComparison.OrdinalIgnoreCase))
        {
            return new Icon(path);
        }

        using var source = new Bitmap(path);
        if (!useDarkMode)
        {
            return CreatePngIcon(source);
        }

        using var recolored = Recolor(source, Color.White);
        return CreatePngIcon(recolored);
    }

    private static Bitmap Recolor(Bitmap source, Color color)
    {
        var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height));
        }

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                if (pixel.A == 0)
                {
                    continue;
                }

                bitmap.SetPixel(x, y, Color.FromArgb(pixel.A, color));
            }
        }

        return bitmap;
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

    private static Icon CreateFrame(string reminderCharacter, CpuLoadLevel loadLevel, bool useDarkMode, int frame)
    {
        using var bitmap = new Bitmap(32, 32);
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

        var hIcon = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(hIcon);
            return (Icon)icon.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
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

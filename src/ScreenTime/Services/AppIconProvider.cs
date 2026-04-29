using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenTime.Models;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;
using DrawingPointF = System.Drawing.PointF;

namespace ScreenTime.Services;

public sealed class AppIconProvider
{
    private readonly Dictionary<string, ImageSource> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ImageSource GetIcon(AppUsage app)
    {
        var key = !string.IsNullOrWhiteSpace(app.ExecutablePath)
            ? app.ExecutablePath
            : !string.IsNullOrWhiteSpace(app.ProcessName)
                ? app.ProcessName
                : app.Name;

        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var icon = TryExtractIcon(app.ExecutablePath) ?? CreateFallbackIcon(app.Name, app.ProcessName);
        _cache[key] = icon;
        return icon;
    }

    private static ImageSource? TryExtractIcon(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return null;
        }

        try
        {
            using var icon = Icon.ExtractAssociatedIcon(executablePath);
            if (icon is null)
            {
                return null;
            }

            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(20, 20));
            source.Freeze();
            return source;
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource CreateFallbackIcon(string appName, string processName)
    {
        const int size = 24;
        using var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(DrawingColor.Transparent);

        var color = PickColor(string.IsNullOrWhiteSpace(processName) ? appName : processName);
        using var background = new SolidBrush(color);
        using var path = RoundedRectangle(1, 1, size - 2, size - 2, 7);
        graphics.FillPath(background, path);

        var initial = GetInitial(appName, processName);
        using var font = new DrawingFont("Segoe UI", 10, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(DrawingColor.White);
        var textSize = graphics.MeasureString(initial, font);
        graphics.DrawString(
            initial,
            font,
            textBrush,
            new DrawingPointF((size - textSize.Width) / 2f, (size - textSize.Height) / 2f - 0.5f));

        return ToImageSource(bitmap);
    }

    private static ImageSource ToImageSource(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static GraphicsPath RoundedRectangle(float x, float y, float width, float height, float radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(x, y, diameter, diameter, 180, 90);
        path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
        path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
        path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static string GetInitial(string appName, string processName)
    {
        var value = string.IsNullOrWhiteSpace(appName) ? processName : appName;
        var first = value.Trim().FirstOrDefault(char.IsLetterOrDigit);
        return first == default ? "?" : char.ToUpperInvariant(first).ToString();
    }

    private static DrawingColor PickColor(string seed)
    {
        var palette = new[]
        {
            DrawingColor.FromArgb(47, 125, 82),
            DrawingColor.FromArgb(47, 128, 237),
            DrawingColor.FromArgb(242, 153, 74),
            DrawingColor.FromArgb(155, 81, 224),
            DrawingColor.FromArgb(32, 180, 134)
        };
        var hash = string.IsNullOrWhiteSpace(seed) ? 0 : seed.Aggregate(0, (acc, c) => (acc * 31) + c);
        return palette[(hash & int.MaxValue) % palette.Length];
    }
}

using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ScreenTime.Core;
using WpfImage = System.Windows.Controls.Image;
using Point = System.Windows.Point;

namespace ScreenTime.Services;

public sealed class LiquidGlassBackdropService : IDisposable
{
    private const uint WdaNone = 0x00000000;
    private const uint WdaExcludeFromCapture = 0x00000011;

    private readonly Window _window;
    private readonly WpfImage _target;
    private readonly DispatcherTimer _timer;
    private nint _handle;
    private bool _isRunning;

    public LiquidGlassBackdropService(Window window, WpfImage target)
    {
        _window = window;
        _target = target;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(90)
        };
        _timer.Tick += (_, _) => CaptureBackdrop();
    }

    public void Start()
    {
        _handle = new WindowInteropHelper(_window).Handle;
        if (_handle == 0 || _isRunning)
        {
            return;
        }

        NativeMethods.SetWindowDisplayAffinity(_handle, WdaExcludeFromCapture);
        _target.Visibility = Visibility.Visible;
        _isRunning = true;
        CaptureBackdrop();
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        if (_handle != 0)
        {
            NativeMethods.SetWindowDisplayAffinity(_handle, WdaNone);
        }

        _target.Source = null;
        _target.Visibility = Visibility.Collapsed;
        _isRunning = false;
    }

    public void Dispose()
    {
        Stop();
    }

    private void CaptureBackdrop()
    {
        if (!_isRunning || _window.ActualWidth <= 1 || _window.ActualHeight <= 1)
        {
            return;
        }

        var source = PresentationSource.FromVisual(_window);
        var transform = source?.CompositionTarget?.TransformToDevice ?? System.Windows.Media.Matrix.Identity;
        var topLeft = _window.PointToScreen(new Point(0, 0));
        var width = Math.Max(1, (int)Math.Round(_window.ActualWidth * transform.M11));
        var height = Math.Max(1, (int)Math.Round(_window.ActualHeight * transform.M22));

        using var bitmap = new Bitmap(width, height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen((int)Math.Round(topLeft.X), (int)Math.Round(topLeft.Y), 0, 0, bitmap.Size);
        }

        var hBitmap = bitmap.GetHbitmap();
        try
        {
            var image = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                nint.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            _target.Source = image;
        }
        finally
        {
            NativeMethods.DeleteObject(hBitmap);
        }
    }
}

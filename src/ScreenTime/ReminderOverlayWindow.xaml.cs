using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ScreenTime.Models;

namespace ScreenTime;

public partial class ReminderOverlayWindow : Window
{
    private readonly UserSettings _settings;
    private readonly DispatcherTimer _timer;
    private TimeSpan _remaining;
    private bool _canClose;

    public ReminderOverlayWindow(UserSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        _remaining = TimeSpan.FromMinutes(Math.Max(1, settings.BreakDurationMinutes));
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTick;
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    public string Action { get; private set; } = "completed";

    public void ForceClose()
    {
        Action = "app_exit";
        _canClose = true;
        Close();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var alpha = (byte)Math.Clamp(_settings.OverlayOpacity * 255, 0, 230);
        RootOverlay.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 0, 0, 0));
        ActionPanel.Visibility = _settings.AllowCloseFullscreenReminder ? Visibility.Visible : Visibility.Collapsed;
        RenderCountdown();
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _remaining -= TimeSpan.FromSeconds(1);
        if (_remaining <= TimeSpan.Zero)
        {
            Action = "completed";
            _canClose = true;
            Close();
            return;
        }

        RenderCountdown();
    }

    private void RenderCountdown()
    {
        CountdownText.Text = $"{(int)_remaining.TotalMinutes:00}:{_remaining.Seconds:00}";
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Action = "closed";
        _canClose = true;
        Close();
    }

    private void OnSnoozeClick(object sender, RoutedEventArgs e)
    {
        Action = "snoozed";
        _canClose = true;
        Close();
    }

    private void OnBreakClick(object sender, RoutedEventArgs e)
    {
        Action = "confirmed";
        _canClose = true;
        Close();
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _settings.AllowCloseFullscreenReminder)
        {
            Action = "closed";
            _canClose = true;
            Close();
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_settings.AllowCloseFullscreenReminder && !_canClose)
        {
            e.Cancel = true;
            return;
        }

        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}

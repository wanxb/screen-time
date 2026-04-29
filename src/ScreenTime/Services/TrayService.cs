using ScreenTime.Core;
using ScreenTime.Models;
using Microsoft.Win32;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace ScreenTime.Services;

public sealed class TrayService : IDisposable
{
    private readonly WinForms.NotifyIcon _notifyIcon;
    private readonly TrayReminderAnimator _animator;
    private readonly SystemMetricsMonitor _metricsMonitor = new();
    private readonly System.Windows.Threading.Dispatcher _dispatcher;
    private string? _themeMode;
    private bool _isPaused;
    private bool _isDisposed;

    public TrayService(UserSettings settings, string? assetDirectory = null)
    {
        _dispatcher = System.Windows.Application.Current.Dispatcher;
        _themeMode = settings.ThemeMode;
        _notifyIcon = new WinForms.NotifyIcon
        {
            Text = "Screen Time",
            Visible = true,
            ContextMenuStrip = new WinForms.ContextMenuStrip()
        };

        _notifyIcon.ContextMenuStrip.Items.Add("打开看板", null, (_, _) => OpenBoardRequested?.Invoke(this, EventArgs.Empty));
        _notifyIcon.ContextMenuStrip.Items.Add("设置", null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        _notifyIcon.ContextMenuStrip.Items.Add("退出", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));
        _notifyIcon.MouseClick += OnMouseClick;

        _animator = new TrayReminderAnimator(_notifyIcon, settings.ReminderCharacter, ThemeService.IsDarkMode(_themeMode), assetDirectory);
        ApplyTheme();
        _metricsMonitor.CpuUsageUpdated += OnCpuUsageUpdated;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public event EventHandler? OpenBoardRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;

    public CpuUsageSnapshot CpuUsage => _metricsMonitor.Current;

    public void Start()
    {
        _animator.Start();
        _metricsMonitor.Start();
    }

    public void SetPaused(bool isPaused)
    {
        _isPaused = isPaused;
        _animator.SetPaused(isPaused);
    }

    public void ApplySettings(UserSettings settings)
    {
        _themeMode = settings.ThemeMode;
        _animator.SetReminderCharacter(settings.ReminderCharacter);
        _animator.SetEnabled(settings.TrayReminderAnimationEnabled);
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        if (_notifyIcon.ContextMenuStrip is null)
        {
            return;
        }

        var useDark = ThemeService.IsDarkMode(_themeMode);
        var background = useDark ? Drawing.Color.FromArgb(27, 30, 24) : Drawing.Color.FromArgb(255, 255, 255);
        var foreground = useDark ? Drawing.Color.FromArgb(245, 241, 232) : Drawing.Color.FromArgb(29, 27, 24);
        var selection = useDark ? Drawing.Color.FromArgb(36, 40, 31) : Drawing.Color.FromArgb(241, 237, 229);

        _notifyIcon.ContextMenuStrip.BackColor = background;
        _notifyIcon.ContextMenuStrip.ForeColor = foreground;
        _notifyIcon.ContextMenuStrip.RenderMode = WinForms.ToolStripRenderMode.Professional;
        _notifyIcon.ContextMenuStrip.Renderer = new ThemedToolStripRenderer(background, foreground, selection);

        foreach (WinForms.ToolStripItem item in _notifyIcon.ContextMenuStrip.Items)
        {
            item.BackColor = background;
            item.ForeColor = foreground;
        }

        _animator.SetTheme(useDark);
    }

    public void UpdateSnapshot(UsageSnapshot snapshot)
    {
        var state = _isPaused ? "已暂停" : snapshot.IsActive ? "活跃" : "空闲";
        var cpuUsage = CpuUsage.UsagePercent;
        var text = $"屏幕时间\n{state} · 今日 {FormatDuration(snapshot.TodayUsage.TotalActiveSeconds)}\nCPU: {cpuUsage:0.0}%";
        _notifyIcon.Text = text.Length > 63 ? text[..63] : text;
    }

    private void OnMouseClick(object? sender, WinForms.MouseEventArgs e)
    {
        if (e.Button == WinForms.MouseButtons.Left)
        {
            OpenBoardRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnCpuUsageUpdated(object? sender, CpuUsageSnapshot snapshot)
    {
        _animator.SetCpuUsage(snapshot);
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_themeMode)
            && !_themeMode.Equals("system", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (e.Category is not UserPreferenceCategory.Color and not UserPreferenceCategory.General)
        {
            return;
        }

        _dispatcher.BeginInvoke(() =>
        {
            if (!_isDisposed)
            {
                ApplyTheme();
            }
        });
    }

    private static string FormatDuration(int totalSeconds)
    {
        if (totalSeconds < 60)
        {
            return $"{totalSeconds}秒";
        }

        var hours = totalSeconds / 3600;
        var minutes = totalSeconds % 3600 / 60;
        return hours > 0 ? $"{hours}小时{minutes}分" : $"{minutes}分钟";
    }

    public void Dispose()
    {
        _isDisposed = true;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _notifyIcon.MouseClick -= OnMouseClick;
        _metricsMonitor.CpuUsageUpdated -= OnCpuUsageUpdated;
        _metricsMonitor.Dispose();
        _animator.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private sealed class ThemedToolStripRenderer : WinForms.ToolStripProfessionalRenderer
    {
        private readonly Drawing.Color _background;
        private readonly Drawing.Color _foreground;
        private readonly Drawing.Color _selection;

        public ThemedToolStripRenderer(Drawing.Color background, Drawing.Color foreground, Drawing.Color selection)
        {
            _background = background;
            _foreground = foreground;
            _selection = selection;
        }

        protected override void OnRenderToolStripBackground(WinForms.ToolStripRenderEventArgs e)
        {
            using var brush = new Drawing.SolidBrush(_background);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderMenuItemBackground(WinForms.ToolStripItemRenderEventArgs e)
        {
            using var brush = new Drawing.SolidBrush(e.Item.Selected ? _selection : _background);
            e.Graphics.FillRectangle(brush, new Drawing.Rectangle(Drawing.Point.Empty, e.Item.Size));
            e.Item.ForeColor = _foreground;
        }
    }
}


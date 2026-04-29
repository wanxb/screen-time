using System.Windows.Threading;
using ScreenTime.Core;
using WinForms = System.Windows.Forms;
using System.Drawing;

namespace ScreenTime.Services;

public sealed class TrayReminderAnimator : IDisposable
{
    private readonly WinForms.NotifyIcon _notifyIcon;
    private readonly string? _assetDirectory;
    private readonly DispatcherTimer _timer;
    private Icon[] _frames = [];
    private int _frameIndex;
    private string _reminderCharacter;
    private CpuLoadLevel _loadLevel = CpuLoadLevel.Low;
    private double? _cpuUsagePercent;
    private bool _useDarkMode;
    private bool _isPaused;
    private bool _isEnabled = true;

    public TrayReminderAnimator(WinForms.NotifyIcon notifyIcon, string reminderCharacter, bool useDarkMode, string? assetDirectory = null)
    {
        _notifyIcon = notifyIcon;
        _reminderCharacter = reminderCharacter;
        _useDarkMode = useDarkMode;
        _assetDirectory = assetDirectory;
        _timer = new DispatcherTimer();
        _timer.Tick += OnTick;
        RebuildFrames();
        WarmFrameCache();
        ApplyInterval();
    }

    public void Start()
    {
        _timer.Start();
        ApplyFrame();
    }

    public void SetReminderCharacter(string reminderCharacter)
    {
        if (_reminderCharacter.Equals(reminderCharacter, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _reminderCharacter = reminderCharacter;
        RebuildFrames();
    }

    public void SetTheme(bool useDarkMode)
    {
        if (_useDarkMode == useDarkMode)
        {
            return;
        }

        _useDarkMode = useDarkMode;
        RebuildFrames();
    }

    public void SetPaused(bool isPaused)
    {
        _isPaused = isPaused;
        ApplyInterval();
        ApplyFrame();
    }

    public void SetEnabled(bool isEnabled)
    {
        _isEnabled = isEnabled;
        if (isEnabled)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
            _frameIndex = 0;
        }

        ApplyFrame();
    }

    public void SetCpuLoad(CpuLoadLevel loadLevel)
    {
        if (_loadLevel == loadLevel)
        {
            return;
        }

        _loadLevel = loadLevel;
        RebuildFrames();
        ApplyInterval();
    }

    public void SetCpuUsage(CpuUsageSnapshot snapshot)
    {
        _cpuUsagePercent = snapshot.UsagePercent;
        SetCpuLoad(snapshot.LoadLevel);
        ApplyInterval();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_frames.Length == 0)
        {
            return;
        }

        _frameIndex = (_frameIndex + 1) % _frames.Length;
        ApplyFrame();
    }

    private void RebuildFrames()
    {
        foreach (var frame in _frames)
        {
            frame.Dispose();
        }

        _frames = TrayReminderIconFactory.CreateFrames(_reminderCharacter, _loadLevel, _useDarkMode, _assetDirectory);
        _frameIndex = 0;
        ApplyFrame();
    }

    private void WarmFrameCache()
    {
        var currentCharacter = _reminderCharacter;
        var currentLoadLevel = _loadLevel;
        var currentUseDarkMode = _useDarkMode;
        var assetDirectory = _assetDirectory;

        _ = Task.Run(() =>
        {
            WarmCharacterFrames(currentCharacter, currentLoadLevel, currentUseDarkMode, assetDirectory);

            foreach (var reminderCharacter in new[] { "cat", "dog" })
            {
                foreach (var useDarkMode in new[] { false, true })
                {
                    foreach (var loadLevel in Enum.GetValues<CpuLoadLevel>())
                    {
                        WarmCharacterFrames(reminderCharacter, loadLevel, useDarkMode, assetDirectory);
                    }
                }
            }
        });
    }

    private static void WarmCharacterFrames(
        string reminderCharacter,
        CpuLoadLevel loadLevel,
        bool useDarkMode,
        string? assetDirectory)
    {
        try
        {
            var icons = TrayReminderIconFactory.CreateFrames(
                reminderCharacter,
                loadLevel,
                useDarkMode,
                assetDirectory);
            foreach (var icon in icons)
            {
                icon.Dispose();
            }
        }
        catch
        {
            // Cache warm-up is opportunistic; normal frame creation remains the fallback.
        }
    }

    private void ApplyFrame()
    {
        if (_frames.Length == 0)
        {
            return;
        }

        _notifyIcon.Icon = _isPaused || !_isEnabled ? _frames[0] : _frames[_frameIndex];
    }

    private void ApplyInterval()
    {
        _timer.Interval = _cpuUsagePercent.HasValue
            ? TrayAnimationSpeed.CalculateInterval(_cpuUsagePercent.Value, _isPaused)
            : TrayAnimationSpeed.InitialInterval;
    }

    public void Dispose()
    {
        _timer.Tick -= OnTick;
        _timer.Stop();

        foreach (var frame in _frames)
        {
            frame.Dispose();
        }
    }
}

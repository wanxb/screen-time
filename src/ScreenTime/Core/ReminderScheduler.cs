using ScreenTime.Models;

namespace ScreenTime.Core;

public sealed class ReminderScheduler
{
    private readonly UserSettings _settings;
    private bool _isReminderOpen;

    public ReminderScheduler(UserSettings settings)
    {
        _settings = settings;
    }

    public event EventHandler<ReminderDueEventArgs>? ReminderDue;

    public void Observe(UsageSnapshot snapshot)
    {
        if (_isReminderOpen
            || !_settings.ReminderEnabled
            || snapshot.IsPaused
            || snapshot.TodayUsage.ContinuousActiveSeconds <= 0)
        {
            return;
        }

        var thresholdSeconds = Math.Max(1, _settings.ReminderIntervalMinutes * 60);
        if (snapshot.TodayUsage.ContinuousActiveSeconds >= thresholdSeconds)
        {
            _isReminderOpen = true;
            ReminderDue?.Invoke(this, new ReminderDueEventArgs(snapshot.TodayUsage, thresholdSeconds));
        }
    }

    public void MarkReminderClosed()
    {
        _isReminderOpen = false;
    }
}

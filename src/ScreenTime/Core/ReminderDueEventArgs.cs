using ScreenTime.Models;

namespace ScreenTime.Core;

public sealed class ReminderDueEventArgs : EventArgs
{
    public ReminderDueEventArgs(DailyUsage usage, int thresholdSeconds, ForegroundAppInfo? currentApp)
    {
        Usage = usage;
        ThresholdSeconds = thresholdSeconds;
        CurrentApp = currentApp;
    }

    public DailyUsage Usage { get; }
    public int ThresholdSeconds { get; }
    public ForegroundAppInfo? CurrentApp { get; }
}

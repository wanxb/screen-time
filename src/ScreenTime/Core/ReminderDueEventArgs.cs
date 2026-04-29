using ScreenTime.Models;

namespace ScreenTime.Core;

public sealed class ReminderDueEventArgs : EventArgs
{
    public ReminderDueEventArgs(DailyUsage usage, int thresholdSeconds)
    {
        Usage = usage;
        ThresholdSeconds = thresholdSeconds;
    }

    public DailyUsage Usage { get; }
    public int ThresholdSeconds { get; }
}

using ScreenTime.Models;

namespace ScreenTime.Core;

public sealed class UsageSnapshot
{
    public required DailyUsage TodayUsage { get; init; }
    public ForegroundAppInfo? CurrentApp { get; init; }
    public bool IsActive { get; init; }
    public bool IsPaused { get; init; }
    public TimeSpan IdleTime { get; init; }
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;
}

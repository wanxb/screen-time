namespace ScreenTime.Models;

public sealed class AppUsage
{
    public string Name { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public AppCategory Category { get; set; } = AppCategory.Other;
    public int ActiveSeconds { get; set; }
    public DateTimeOffset FirstUsedAt { get; set; }
    public DateTimeOffset LastUsedAt { get; set; }
    public string IconCacheKey { get; set; } = string.Empty;
}

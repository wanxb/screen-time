namespace ScreenTime.Core;

public sealed class CpuUsageSnapshot
{
    public double UsagePercent { get; init; }
    public CpuLoadLevel LoadLevel { get; init; }
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;
}

public enum CpuLoadLevel
{
    Low,
    Normal,
    High,
    VeryHigh
}

namespace ScreenTime.Core;

public static class CpuLoadClassifier
{
    public static CpuLoadLevel Classify(double usagePercent)
    {
        return usagePercent switch
        {
            < 20 => CpuLoadLevel.Low,
            < 60 => CpuLoadLevel.Normal,
            < 85 => CpuLoadLevel.High,
            _ => CpuLoadLevel.VeryHigh
        };
    }
}

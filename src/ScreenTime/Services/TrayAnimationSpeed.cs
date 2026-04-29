namespace ScreenTime.Services;

public static class TrayAnimationSpeed
{
    public static readonly TimeSpan InitialInterval = TimeSpan.FromMilliseconds(200);

    private const double ReferenceIntervalMilliseconds = 500;
    private const double PauseIntervalMilliseconds = 900;

    public static TimeSpan CalculateInterval(double usagePercent, bool isPaused = false)
    {
        if (isPaused)
        {
            return TimeSpan.FromMilliseconds(PauseIntervalMilliseconds);
        }

        var speed = CalculateSpeedMultiplier(usagePercent);
        return TimeSpan.FromMilliseconds(ReferenceIntervalMilliseconds / speed);
    }

    public static double CalculateSpeedMultiplier(double usagePercent)
    {
        var load = Math.Clamp(usagePercent, 0, 100);
        return Math.Max(1, load / 5);
    }
}

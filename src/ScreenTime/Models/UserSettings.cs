namespace ScreenTime.Models;

public sealed class UserSettings
{
    public int Version { get; set; } = 1;
    public bool ReminderEnabled { get; set; } = true;
    public int ReminderIntervalMinutes { get; set; } = 45;
    public int BreakDurationMinutes { get; set; } = 3;
    public bool AllowCloseFullscreenReminder { get; set; } = false;
    public string ReminderCharacter { get; set; } = "cat";
    public int IdleThresholdSeconds { get; set; } = 60;
    public double OverlayOpacity { get; set; } = 0.1;
    public bool LaunchAtStartup { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool TrayReminderAnimationEnabled { get; set; } = true;
    public string TrayReminderCpuDriver { get; set; } = "cpu_usage";
    public string ThemeMode { get; set; } = "system";
    public int AppCategoryRulesVersion { get; set; } = 1;
    public bool OpenBoardOnTrayClick { get; set; } = true;
}

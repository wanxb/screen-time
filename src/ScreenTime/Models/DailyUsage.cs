namespace ScreenTime.Models;

public sealed class DailyUsage
{
    public int Version { get; set; } = 1;
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Now);
    public int TotalActiveSeconds { get; set; }
    public int ContinuousActiveSeconds { get; set; }
    public Dictionary<string, AppUsage> Apps { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<int, Dictionary<AppCategory, int>> HourlyCategorySeconds { get; set; } = [];
    public List<ReminderEvent> Reminders { get; set; } = [];
}

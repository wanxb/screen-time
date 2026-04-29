namespace ScreenTime.Models;

public sealed class ReminderEvent
{
    public string Type { get; set; } = "break";
    public DateTimeOffset TriggeredAt { get; set; } = DateTimeOffset.Now;
    public int ContinuousActiveSeconds { get; set; }
    public string Action { get; set; } = string.Empty;
}

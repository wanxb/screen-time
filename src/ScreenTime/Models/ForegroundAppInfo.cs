namespace ScreenTime.Models;

public sealed class ForegroundAppInfo
{
    public int ProcessId { get; init; }
    public string Name { get; init; } = "Unknown";
    public string ProcessName { get; init; } = "unknown";
    public string ExecutablePath { get; init; } = string.Empty;
    public string WindowTitle { get; init; } = string.Empty;
    public bool IsFullScreen { get; init; }
    public string AppId => string.IsNullOrWhiteSpace(ExecutablePath) ? ProcessName : ExecutablePath;
}

using ScreenTime.Models;

namespace ScreenTime.Core;

public static class ReminderSuppressionDetector
{
    private static readonly string[] PresentationProcesses =
    [
        "powerpnt",
        "wpp",
        "wpsoffice"
    ];

    private static readonly string[] MediaProcesses =
    [
        "vlc",
        "mpv",
        "potplayer",
        "potplayermini64",
        "bilibili",
        "douyin",
        "iina",
        "wmplayer",
        "quicktimeplayer",
        "mpc-hc64",
        "mpc-be64"
    ];

    private static readonly string[] BrowserProcesses =
    [
        "chrome",
        "msedge",
        "firefox",
        "brave",
        "opera",
        "vivaldi",
        "arc"
    ];

    private static readonly string[] MediaTitleKeywords =
    [
        "youtube",
        "bilibili",
        "哔哩哔哩",
        "netflix",
        "twitch",
        "iqiyi",
        "爱奇艺",
        "youku",
        "优酷",
        "腾讯视频",
        "douyin",
        "抖音",
        "西瓜视频",
        "芒果TV",
        "视频",
        "video"
    ];

    private static readonly string[] PresentationTitleKeywords =
    [
        "slide show",
        "presenter view",
        "powerpoint",
        "幻灯片放映",
        "演示者视图"
    ];

    public static bool TryGetSuppressionReason(ForegroundAppInfo? app, out string reason)
    {
        reason = string.Empty;
        if (app is null)
        {
            return false;
        }

        if (Matches(PresentationProcesses, app.ProcessName)
            && (app.IsFullScreen || ContainsAny(app.WindowTitle, PresentationTitleKeywords)))
        {
            reason = "演示中";
            return true;
        }

        if (!app.IsFullScreen)
        {
            return false;
        }

        if (IsMediaPlaybackApp(app))
        {
            reason = "全屏视频中";
            return true;
        }

        return false;
    }

    public static bool IsMediaPlaybackApp(ForegroundAppInfo? app)
    {
        if (app is null)
        {
            return false;
        }

        return Matches(MediaProcesses, app.ProcessName)
            || (Matches(BrowserProcesses, app.ProcessName) && ContainsAny(app.WindowTitle, MediaTitleKeywords));
    }

    private static bool Matches(IEnumerable<string> needles, string value)
    {
        return needles.Any(needle => string.Equals(needle, value, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsAny(string value, IEnumerable<string> needles)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }
}

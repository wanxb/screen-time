using System.Text.Json;
using ScreenTime.Models;

namespace ScreenTime.Storage;

public sealed class AppCategoryRuleStore
{
    private readonly AppDataPaths _paths;

    public AppCategoryRuleStore(AppDataPaths paths)
    {
        _paths = paths;
    }

    public async Task<AppCategoryRuleSet> LoadAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();

        if (!File.Exists(_paths.AppCategoriesFile))
        {
            var defaults = CreateDefaults();
            await SaveAsync(defaults, cancellationToken);
            return defaults;
        }

        try
        {
            AppCategoryRuleSet loaded;
            await using (var stream = File.OpenRead(_paths.AppCategoriesFile))
            {
                loaded = await JsonSerializer.DeserializeAsync<AppCategoryRuleSet>(stream, JsonOptions.Default, cancellationToken)
                    ?? CreateDefaults();
            }

            var defaults = CreateDefaults();
            if (loaded.Version < defaults.Version)
            {
                loaded = UpgradeRules(loaded, defaults);
                await SaveAsync(loaded, cancellationToken);
            }

            return loaded;
        }
        catch (JsonException)
        {
            File.Copy(_paths.AppCategoriesFile, $"{_paths.AppCategoriesFile}.corrupt", overwrite: true);
            var defaults = CreateDefaults();
            await SaveAsync(defaults, cancellationToken);
            return defaults;
        }
    }

    private Task SaveAsync(AppCategoryRuleSet ruleSet, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(ruleSet, JsonOptions.Default);
        return AtomicFileWriter.WriteTextAsync(_paths.AppCategoriesFile, json, cancellationToken);
    }

    private static AppCategoryRuleSet UpgradeRules(AppCategoryRuleSet loaded, AppCategoryRuleSet defaults)
    {
        foreach (var defaultRule in defaults.Rules)
        {
            var existing = loaded.Rules.FirstOrDefault(rule => rule.Category == defaultRule.Category);
            if (existing is null)
            {
                loaded.Rules.Add(defaultRule);
                continue;
            }

            Merge(existing.ProcessNames, defaultRule.ProcessNames);
            Merge(existing.PathKeywords, defaultRule.PathKeywords);
            Merge(existing.NameKeywords, defaultRule.NameKeywords);
            Merge(existing.WindowTitleKeywords, defaultRule.WindowTitleKeywords);
        }

        loaded.Version = defaults.Version;
        return loaded;
    }

    private static void Merge(List<string> target, IEnumerable<string> source)
    {
        foreach (var value in source)
        {
            if (!target.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                target.Add(value);
            }
        }
    }

    private static AppCategoryRuleSet CreateDefaults()
    {
        return new AppCategoryRuleSet
        {
            Version = 2,
            Rules =
            [
                new AppCategoryRule
                {
                    Category = AppCategory.Work,
                    ProcessNames = ["devenv", "code", "rider", "webstorm", "pycharm", "idea", "cursor", "notepad++", "figma", "excel", "winword", "powerpnt", "outlook", "onenote", "wps", "wpp", "wpsoffice", "xmind", "postman", "datagrip", "datagrip64", "navicat", "dbeaver", "tableau", "powerbi", "powerpnt"],
                    PathKeywords = ["microsoft vs code", "jetbrains", "microsoft office", "wps office", "postman", "navicat", "dbeaver"],
                    NameKeywords = ["visual studio", "code", "figma", "excel", "word", "powerpoint", "outlook", "postman", "power bi", "tableau"],
                    WindowTitleKeywords = ["github", "gitlab", "jira", "confluence", "notion", "docs", "sheet", "slides", "office", "powerpoint", "excel", "word"]
                },
                new AppCategoryRule
                {
                    Category = AppCategory.Social,
                    ProcessNames = ["wechat", "weixin", "qq", "tim", "teams", "slack", "discord", "telegram", "lark", "feishu", "dingtalk", "wxwork", "zoom", "voovmeeting"],
                    PathKeywords = ["wechat", "weixin", "tencent", "slack", "discord", "telegram", "lark", "feishu"],
                    NameKeywords = ["wechat", "weixin", "qq", "teams", "slack", "discord", "telegram", "lark", "feishu", "dingtalk", "zoom"],
                    WindowTitleKeywords = ["wechat", "weixin", "qq", "teams", "slack", "discord", "telegram", "feishu", "飞书", "钉钉", "zoom"]
                },
                new AppCategoryRule
                {
                    Category = AppCategory.Entertainment,
                    ProcessNames = ["steam", "epicgameslauncher", "neteaseuu", "cloudmusic", "spotify", "potplayermini64", "potplayer", "vlc", "mpv", "bilibili", "douyin", "obs64", "iina", "qqmusic", "kuwo", "kugou"],
                    PathKeywords = ["steam", "epic games", "spotify", "cloudmusic", "bilibili", "douyin", "potplayer", "vlc", "qqmusic", "kugou", "kuwo"],
                    NameKeywords = ["steam", "epic", "spotify", "music", "video", "bilibili", "douyin", "potplayer", "vlc"],
                    WindowTitleKeywords = ["youtube", "netflix", "bilibili", "哔哩哔哩", "douyin", "抖音", "twitch", "iqiyi", "爱奇艺", "youku", "优酷", "腾讯视频", "hulu", "disney+", "spotify", "music", "video"]
                },
                new AppCategoryRule
                {
                    Category = AppCategory.Learning,
                    ProcessNames = ["obsidian", "notion", "kindle", "zotero", "typora", "anki", "calibre", "logseq"],
                    PathKeywords = ["obsidian", "notion", "kindle", "zotero", "typora", "anki", "calibre", "logseq"],
                    NameKeywords = ["obsidian", "notion", "kindle", "zotero", "typora", "anki", "calibre", "logseq"],
                    WindowTitleKeywords = ["coursera", "udemy", "edx", "khan academy", "duolingo", "中国大学mooc", "mooc", "leetcode", "力扣", "wikipedia", "维基百科"]
                },
                new AppCategoryRule
                {
                    Category = AppCategory.System,
                    ProcessNames = ["explorer", "taskmgr", "systemsettings", "applicationframehost", "shellexperiencehost", "searchhost", "startmenuexperiencehost", "cmd", "powershell", "pwsh", "windowsterminal", "conhost", "regedit", "control"],
                    PathKeywords = ["windows\\explorer.exe", "windows\\system32"],
                    NameKeywords = ["explorer", "task manager", "settings", "terminal", "powershell", "windows security"],
                    WindowTitleKeywords = ["task manager", "settings", "control panel", "windows security"]
                }
            ]
        };
    }
}

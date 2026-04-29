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
            await using var stream = File.OpenRead(_paths.AppCategoriesFile);
            return await JsonSerializer.DeserializeAsync<AppCategoryRuleSet>(stream, JsonOptions.Default, cancellationToken)
                ?? CreateDefaults();
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

    private static AppCategoryRuleSet CreateDefaults()
    {
        return new AppCategoryRuleSet
        {
            Version = 1,
            Rules =
            [
                new AppCategoryRule
                {
                    Category = AppCategory.Work,
                    ProcessNames = ["devenv", "code", "rider", "webstorm", "pycharm", "idea", "cursor", "notepad++", "figma", "excel", "winword", "powerpnt"],
                    PathKeywords = ["microsoft vs code", "jetbrains", "microsoft office"],
                    NameKeywords = ["visual studio", "code", "figma", "excel", "word", "powerpoint"]
                },
                new AppCategoryRule
                {
                    Category = AppCategory.Social,
                    ProcessNames = ["wechat", "weixin", "qq", "tim", "teams", "slack", "discord", "telegram", "lark", "feishu"],
                    PathKeywords = ["wechat", "weixin", "tencent", "slack", "discord", "telegram", "lark", "feishu"],
                    NameKeywords = ["wechat", "weixin", "qq", "teams", "slack", "discord", "telegram", "lark", "feishu"]
                },
                new AppCategoryRule
                {
                    Category = AppCategory.Entertainment,
                    ProcessNames = ["steam", "epicgameslauncher", "neteaseuu", "cloudmusic", "spotify", "potplayermini64", "vlc", "bilibili", "douyin"],
                    PathKeywords = ["steam", "epic games", "spotify", "cloudmusic", "bilibili", "douyin"],
                    NameKeywords = ["steam", "epic", "spotify", "music", "video", "bilibili", "douyin"]
                },
                new AppCategoryRule
                {
                    Category = AppCategory.Learning,
                    ProcessNames = ["obsidian", "notion", "onenote", "kindle", "zotero", "typora"],
                    PathKeywords = ["obsidian", "notion", "onenote", "kindle", "zotero", "typora"],
                    NameKeywords = ["obsidian", "notion", "onenote", "kindle", "zotero", "typora"]
                },
                new AppCategoryRule
                {
                    Category = AppCategory.System,
                    ProcessNames = ["explorer", "taskmgr", "systemsettings", "applicationframehost", "shellexperiencehost", "searchhost", "startmenuexperiencehost", "cmd", "powershell", "windowsterminal"],
                    PathKeywords = ["windows\\explorer.exe", "windows\\system32"],
                    NameKeywords = ["explorer", "task manager", "settings", "terminal", "powershell"]
                }
            ]
        };
    }
}

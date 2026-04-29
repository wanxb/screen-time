using ScreenTime.Models;
using ScreenTime.Core;
using ScreenTime.Storage;

namespace ScreenTime.Services;

public sealed class AppBootstrapper
{
    private readonly SettingsStore _settingsStore;
    private readonly AppCategoryRuleStore _appCategoryRuleStore;

    public AppBootstrapper()
    {
        var paths = new AppDataPaths();
        Paths = paths;
        _settingsStore = new SettingsStore(paths);
        UsageStore = new UsageStore(paths);
        _appCategoryRuleStore = new AppCategoryRuleStore(paths);
    }

    public UserSettings Settings { get; private set; } = new();
    public DailyUsage TodayUsage { get; private set; } = new();
    public AppCategoryClassifier AppCategoryClassifier { get; private set; } = new(new AppCategoryRuleSet());
    public AppDataPaths Paths { get; }
    public UsageStore UsageStore { get; }
    public SettingsStore SettingsStore => _settingsStore;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Settings = await _settingsStore.LoadAsync(cancellationToken);
        TodayUsage = await UsageStore.LoadTodayAsync(cancellationToken);
        var ruleSet = await _appCategoryRuleStore.LoadAsync(cancellationToken);
        AppCategoryClassifier = new AppCategoryClassifier(ruleSet);
    }
}

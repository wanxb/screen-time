namespace ScreenTime.Storage;

public sealed class AppDataPaths
{
    public AppDataPaths(string? baseDirectory = null)
    {
        BaseDirectory = baseDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenTime");
    }

    public string BaseDirectory { get; }
    public string UsageDirectory => Path.Combine(BaseDirectory, "usage");
    public string SessionsDirectory => Path.Combine(BaseDirectory, "sessions");
    public string AssetsDirectory => Path.Combine(BaseDirectory, "assets");
    public string IconCacheDirectory => Path.Combine(BaseDirectory, "icon-cache");
    public string SettingsFile => Path.Combine(BaseDirectory, "settings.json");
    public string AppCategoriesFile => Path.Combine(BaseDirectory, "app_categories.json");

    public string GetUsageFile(DateOnly date) => Path.Combine(UsageDirectory, $"{date:yyyy-MM-dd}.json");

    public void EnsureCreated()
    {
        Directory.CreateDirectory(BaseDirectory);
        Directory.CreateDirectory(UsageDirectory);
        Directory.CreateDirectory(SessionsDirectory);
        Directory.CreateDirectory(AssetsDirectory);
        Directory.CreateDirectory(IconCacheDirectory);
    }
}

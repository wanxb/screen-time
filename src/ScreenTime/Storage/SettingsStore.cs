using System.Text.Json;
using ScreenTime.Models;

namespace ScreenTime.Storage;

public sealed class SettingsStore
{
    private readonly AppDataPaths _paths;

    public SettingsStore(AppDataPaths paths)
    {
        _paths = paths;
    }

    public async Task<UserSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();

        if (!File.Exists(_paths.SettingsFile))
        {
            var defaults = new UserSettings();
            await SaveAsync(defaults, cancellationToken);
            return defaults;
        }

        try
        {
            await using var stream = File.OpenRead(_paths.SettingsFile);
            return await JsonSerializer.DeserializeAsync<UserSettings>(stream, JsonOptions.Default, cancellationToken)
                ?? new UserSettings();
        }
        catch (JsonException)
        {
            File.Copy(_paths.SettingsFile, $"{_paths.SettingsFile}.corrupt", overwrite: true);
            var defaults = new UserSettings();
            await SaveAsync(defaults, cancellationToken);
            return defaults;
        }
    }

    public Task SaveAsync(UserSettings settings, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions.Default);
        return AtomicFileWriter.WriteTextAsync(_paths.SettingsFile, json, cancellationToken);
    }
}

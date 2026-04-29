using System.Text.Json;
using ScreenTime.Models;

namespace ScreenTime.Storage;

public sealed class UsageStore
{
    private readonly AppDataPaths _paths;

    public UsageStore(AppDataPaths paths)
    {
        _paths = paths;
    }

    public Task<DailyUsage> LoadTodayAsync(CancellationToken cancellationToken = default)
    {
        return LoadAsync(DateOnly.FromDateTime(DateTime.Now), cancellationToken);
    }

    public async Task<DailyUsage> LoadAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        var file = _paths.GetUsageFile(date);

        if (!File.Exists(file))
        {
            var usage = new DailyUsage { Date = date };
            await SaveAsync(usage, cancellationToken);
            return usage;
        }

        try
        {
            await using var stream = File.OpenRead(file);
            return await JsonSerializer.DeserializeAsync<DailyUsage>(stream, JsonOptions.Default, cancellationToken)
                ?? new DailyUsage { Date = date };
        }
        catch (JsonException)
        {
            File.Copy(file, $"{file}.corrupt", overwrite: true);
            var usage = new DailyUsage { Date = date };
            await SaveAsync(usage, cancellationToken);
            return usage;
        }
    }

    public async Task<DailyUsage> LoadExistingOrEmptyAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        var file = _paths.GetUsageFile(date);

        if (!File.Exists(file))
        {
            return new DailyUsage { Date = date };
        }

        try
        {
            await using var stream = File.OpenRead(file);
            return await JsonSerializer.DeserializeAsync<DailyUsage>(stream, JsonOptions.Default, cancellationToken)
                ?? new DailyUsage { Date = date };
        }
        catch (JsonException)
        {
            return new DailyUsage { Date = date };
        }
    }

    public Task SaveAsync(DailyUsage usage, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(usage, JsonOptions.Default);
        return AtomicFileWriter.WriteTextAsync(_paths.GetUsageFile(usage.Date), json, cancellationToken);
    }
}

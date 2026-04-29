using System.Windows.Threading;
using ScreenTime.Models;
using ScreenTime.Storage;

namespace ScreenTime.Core;

public sealed class UsageTimer : IDisposable
{
    private readonly UserSettings _settings;
    private readonly DailyUsage _todayUsage;
    private readonly UsageStore _usageStore;
    private readonly AppCategoryClassifier _appCategoryClassifier;
    private readonly IdleDetector _idleDetector = new();
    private readonly ForegroundAppTracker _foregroundAppTracker = new();
    private readonly DispatcherTimer _timer;
    private bool _isSaving;
    private bool _wasActive;
    private DateTimeOffset _lastSavedAt = DateTimeOffset.MinValue;
    private string _lastAppId = string.Empty;
    private string _lastActiveAppId = string.Empty;
    private AppCategory _lastActiveCategory = AppCategory.Other;
    private bool _isPaused;

    public UsageTimer(
        UserSettings settings,
        DailyUsage todayUsage,
        UsageStore usageStore,
        AppCategoryClassifier appCategoryClassifier)
    {
        _settings = settings;
        _todayUsage = todayUsage;
        _usageStore = usageStore;
        _appCategoryClassifier = appCategoryClassifier;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTick;
    }

    public event EventHandler<UsageSnapshot>? SnapshotUpdated;

    public bool IsPaused => _isPaused;

    public void Start()
    {
        _timer.Start();
    }

    public async Task StopAsync()
    {
        _timer.Stop();
        await SaveAsync();
    }

    public void Pause()
    {
        _isPaused = true;
        _lastAppId = string.Empty;
    }

    public void Resume()
    {
        _isPaused = false;
    }

    public async Task ResetContinuousAsync(string reminderAction)
    {
        _todayUsage.Reminders.Add(new ReminderEvent
        {
            Type = "break",
            TriggeredAt = DateTimeOffset.Now,
            ContinuousActiveSeconds = _todayUsage.ContinuousActiveSeconds,
            Action = reminderAction
        });
        _todayUsage.ContinuousActiveSeconds = 0;
        await SaveAsync();
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        var idleTime = _idleDetector.GetIdleTime();
        var isActive = idleTime < TimeSpan.FromSeconds(_settings.IdleThresholdSeconds);
        var currentApp = isActive ? _foregroundAppTracker.GetCurrent() : null;
        var shouldSave = false;

        if (_isPaused)
        {
            isActive = false;
            currentApp = null;
        }
        else if (isActive && currentApp is not null)
        {
            var now = DateTimeOffset.Now;
            var appId = currentApp.AppId;

            if (!_todayUsage.Apps.TryGetValue(appId, out var usage))
            {
                usage = new AppUsage
                {
                    Name = currentApp.Name,
                    ProcessName = currentApp.ProcessName,
                    ExecutablePath = currentApp.ExecutablePath,
                    Category = _appCategoryClassifier.Classify(currentApp),
                    FirstUsedAt = now,
                    IconCacheKey = currentApp.ProcessName
                };
                _todayUsage.Apps[appId] = usage;
            }

            usage.Name = currentApp.Name;
            usage.ProcessName = currentApp.ProcessName;
            usage.ExecutablePath = currentApp.ExecutablePath;
            usage.Category = _appCategoryClassifier.Classify(currentApp);
            usage.ActiveSeconds++;
            usage.LastUsedAt = now;
            _todayUsage.TotalActiveSeconds++;
            _todayUsage.ContinuousActiveSeconds++;
            AddHourlyCategorySecond(now.Hour, usage.Category);

            shouldSave = !string.Equals(_lastAppId, appId, StringComparison.OrdinalIgnoreCase);
            _lastAppId = appId;
            _lastActiveAppId = appId;
            _lastActiveCategory = usage.Category;
        }
        else if (_wasActive)
        {
            RewindIdleTail();
            shouldSave = true;
            _lastAppId = string.Empty;
            _lastActiveAppId = string.Empty;
        }

        _wasActive = isActive;
        shouldSave = shouldSave || DateTimeOffset.Now - _lastSavedAt >= TimeSpan.FromSeconds(30);

        SnapshotUpdated?.Invoke(this, new UsageSnapshot
        {
            TodayUsage = _todayUsage,
            CurrentApp = currentApp,
            IsActive = isActive,
            IsPaused = _isPaused,
            IdleTime = idleTime
        });

        if (shouldSave)
        {
            await SaveAsync();
        }
    }

    private void AddHourlyCategorySecond(int hour, AppCategory category)
    {
        if (!_todayUsage.HourlyCategorySeconds.TryGetValue(hour, out var categorySeconds))
        {
            categorySeconds = [];
            _todayUsage.HourlyCategorySeconds[hour] = categorySeconds;
        }

        categorySeconds.TryGetValue(category, out var seconds);
        categorySeconds[category] = seconds + 1;
    }

    private void RewindIdleTail()
    {
        if (string.IsNullOrWhiteSpace(_lastActiveAppId)
            || !_todayUsage.Apps.TryGetValue(_lastActiveAppId, out var usage))
        {
            return;
        }

        var rewindSeconds = Math.Min(_settings.IdleThresholdSeconds, usage.ActiveSeconds);
        rewindSeconds = Math.Min(rewindSeconds, _todayUsage.TotalActiveSeconds);
        rewindSeconds = Math.Min(rewindSeconds, _todayUsage.ContinuousActiveSeconds);
        if (rewindSeconds <= 0)
        {
            return;
        }

        usage.ActiveSeconds -= rewindSeconds;
        var correctedLastUsedAt = usage.LastUsedAt.AddSeconds(-rewindSeconds);
        usage.LastUsedAt = correctedLastUsedAt < usage.FirstUsedAt ? usage.FirstUsedAt : correctedLastUsedAt;
        _todayUsage.TotalActiveSeconds -= rewindSeconds;
        _todayUsage.ContinuousActiveSeconds -= rewindSeconds;
        RemoveHourlyCategorySeconds(DateTimeOffset.Now, rewindSeconds, _lastActiveCategory);
    }

    private void RemoveHourlyCategorySeconds(DateTimeOffset endAt, int seconds, AppCategory category)
    {
        var remaining = seconds;
        var segmentEnd = endAt.LocalDateTime;

        while (remaining > 0)
        {
            var hourStart = new DateTime(
                segmentEnd.Year,
                segmentEnd.Month,
                segmentEnd.Day,
                segmentEnd.Hour,
                0,
                0);
            var secondsInHour = (int)Math.Ceiling((segmentEnd - hourStart).TotalSeconds);
            if (secondsInHour <= 0)
            {
                segmentEnd = hourStart.AddTicks(-1);
                continue;
            }

            var take = Math.Min(remaining, secondsInHour);
            if (_todayUsage.HourlyCategorySeconds.TryGetValue(segmentEnd.Hour, out var categorySeconds)
                && categorySeconds.TryGetValue(category, out var existing))
            {
                var updated = Math.Max(0, existing - take);
                if (updated == 0)
                {
                    categorySeconds.Remove(category);
                }
                else
                {
                    categorySeconds[category] = updated;
                }
            }

            remaining -= take;
            segmentEnd = hourStart;
        }
    }

    private async Task SaveAsync()
    {
        if (_isSaving)
        {
            return;
        }

        try
        {
            _isSaving = true;
            await _usageStore.SaveAsync(_todayUsage);
            _lastSavedAt = DateTimeOffset.Now;
        }
        finally
        {
            _isSaving = false;
        }
    }

    public void Dispose()
    {
        _timer.Tick -= OnTick;
        _timer.Stop();
    }
}

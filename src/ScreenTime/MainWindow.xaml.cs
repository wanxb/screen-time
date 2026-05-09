using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using ScreenTime.Core;
using ScreenTime.Models;
using ScreenTime.Services;
using MediaBrush = System.Windows.Media.Brush;

namespace ScreenTime;

public partial class MainWindow : Window
{
    private readonly AppBootstrapper _bootstrapper = new();
    private readonly AppIconProvider _appIconProvider = new();
    private UsageTimer? _usageTimer;
    private ReminderScheduler? _reminderScheduler;
    private SettingsWindow? _settingsWindow;
    private ReminderOverlayWindow? _reminderOverlayWindow;
    private TrayService? _trayService;
    private UsageSnapshot? _lastSnapshot;
    private ChartData? _currentChartData;
    private readonly DispatcherTimer _barValueTimer;
    private readonly LiquidGlassBackdropService _liquidGlassBackdrop;
    private DateTimeOffset _barValueVisibleUntil = DateTimeOffset.MinValue;
    private string _currentPeriodTotalText = "0 秒";
    private List<AppUsage> _currentApps = [];
    private int _currentAppsTotalSeconds;
    private Dictionary<string, int> _weeklyAverageSecondsByAppKey = new(StringComparer.OrdinalIgnoreCase);
    private AppCategory? _selectedCategory;
    private DashboardMode _dashboardMode = DashboardMode.Daily;
    private DateOnly _selectedDate = DateOnly.FromDateTime(DateTime.Now);
    private readonly bool _startHidden;
    private bool _isExitRequested;
    private bool _isClosingForExit;
    private bool _isDashboardRendering;
    private bool _isDashboardRenderPending;

    public MainWindow() : this(false)
    {
    }

    public MainWindow(bool startHidden)
    {
        _startHidden = startHidden;
        InitializeComponent();
        _liquidGlassBackdrop = new LiquidGlassBackdropService(this, LiquidGlassBackdrop);
        _barValueTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _barValueTimer.Tick += OnBarValueTimerTick;
        SourceInitialized += (_, _) =>
        {
            ThemeService.ApplyWindowTitleBar(this, _bootstrapper.Settings.ThemeMode);
            ApplyLiquidGlassBackdrop();
        };
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _bootstrapper.InitializeAsync();
            ThemeService.Apply(_bootstrapper.Settings.ThemeMode);
            ThemeService.ApplyWindowTitleBar(this, _bootstrapper.Settings.ThemeMode);
            ApplyLiquidGlassBackdrop();
            StartupService.Apply(_bootstrapper.Settings);
            _usageTimer = new UsageTimer(
                _bootstrapper.Settings,
                _bootstrapper.TodayUsage,
                _bootstrapper.UsageStore,
                _bootstrapper.AppCategoryClassifier);
            _usageTimer.SnapshotUpdated += OnSnapshotUpdated;
            _usageTimer.Start();
            _reminderScheduler = new ReminderScheduler(_bootstrapper.Settings);
            _reminderScheduler.ReminderDue += OnReminderDue;

            _trayService = new TrayService(_bootstrapper.Settings, _bootstrapper.Paths.AssetsDirectory);
            _trayService.OpenBoardRequested += (_, _) => Dispatcher.Invoke(ShowMainWindow);
            _trayService.SettingsRequested += (_, _) => Dispatcher.Invoke(OpenSettings);
            _trayService.ExitRequested += (_, _) => Dispatcher.Invoke(ExitApplication);
            _trayService.Start();

            RenderSnapshot(new UsageSnapshot
            {
                TodayUsage = _bootstrapper.TodayUsage,
                IsActive = false,
                IdleTime = TimeSpan.Zero
            });

            if (_startHidden)
            {
                Hide();
                Opacity = 1;
                ShowInTaskbar = true;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log(ex, "Main window initialization failed");
            StatusText.Text = $"初始化失败：{ex.Message}";
            StateText.Text = "错误";
        }
    }

    private void OnSnapshotUpdated(object? sender, UsageSnapshot snapshot)
    {
        RenderSnapshot(snapshot);
    }

    private void RenderSnapshot(UsageSnapshot snapshot)
    {
        _lastSnapshot = snapshot;
        var state = snapshot.IsPaused ? "已暂停" : snapshot.IsActive ? "活跃" : "空闲";
        var remainingSeconds = Math.Max(
            0,
            (_bootstrapper.Settings.ReminderIntervalMinutes * 60) - snapshot.TodayUsage.ContinuousActiveSeconds);

        StateText.Text = state;
        TodayTimeText.Text = FormatDuration(snapshot.TodayUsage.TotalActiveSeconds);
        ContinuousTimeText.Text = FormatDuration(snapshot.TodayUsage.ContinuousActiveSeconds);
        NextBreakText.Text = FormatDuration(remainingSeconds);

        var reminderStatus = ReminderSuppressionDetector.TryGetSuppressionReason(snapshot.CurrentApp, out var suppressionReason)
            ? $" · 提醒延后：{suppressionReason}"
            : string.Empty;
        StatusText.Text = $"{FormatDuration((int)snapshot.IdleTime.TotalSeconds)}空闲 · {snapshot.TodayUsage.Apps.Count} 个软件 · {snapshot.UpdatedAt:HH:mm:ss}{reminderStatus}";
        _ = RenderDashboardSafelyAsync();

        _trayService?.UpdateSnapshot(snapshot);
        _reminderScheduler?.Observe(snapshot);
    }

    private async void OnDailyModeClick(object sender, RoutedEventArgs e)
    {
        ClearBarValueDisplay();
        ClearAppDetail();
        _selectedCategory = null;
        _dashboardMode = DashboardMode.Daily;
        _selectedDate = DateOnly.FromDateTime(DateTime.Now);
        await RenderDashboardSafelyAsync();
    }

    private async void OnWeeklyModeClick(object sender, RoutedEventArgs e)
    {
        ClearBarValueDisplay();
        ClearAppDetail();
        _selectedCategory = null;
        _dashboardMode = DashboardMode.Weekly;
        _selectedDate = DateOnly.FromDateTime(DateTime.Now);
        await RenderDashboardSafelyAsync();
    }

    private async void OnPreviousPeriodClick(object sender, RoutedEventArgs e)
    {
        ClearBarValueDisplay();
        ClearAppDetail();
        _selectedDate = _dashboardMode == DashboardMode.Daily ? _selectedDate.AddDays(-1) : _selectedDate.AddDays(-7);
        await RenderDashboardSafelyAsync();
    }

    private async void OnNextPeriodClick(object sender, RoutedEventArgs e)
    {
        if (!CanMoveNextPeriod())
        {
            return;
        }

        ClearBarValueDisplay();
        ClearAppDetail();
        _selectedDate = _dashboardMode == DashboardMode.Daily ? _selectedDate.AddDays(1) : _selectedDate.AddDays(7);
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (_selectedDate > today)
        {
            _selectedDate = today;
        }

        await RenderDashboardSafelyAsync();
    }

    private void OpenSettings()
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_bootstrapper.Settings);
        _settingsWindow.Closed += async (_, _) =>
        {
            var saved = _settingsWindow.SettingsSaved;
            _settingsWindow = null;

            if (!saved)
            {
                return;
            }

            try
            {
                await _bootstrapper.SettingsStore.SaveAsync(_bootstrapper.Settings);
                ThemeService.Apply(_bootstrapper.Settings.ThemeMode);
                ThemeService.ApplyWindowTitleBar(this, _bootstrapper.Settings.ThemeMode);
                ApplyLiquidGlassBackdrop();
                StartupService.Apply(_bootstrapper.Settings);
                _trayService?.ApplySettings(_bootstrapper.Settings);
                StatusText.Text = "设置已保存。";
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex, "Saving settings failed");
                StatusText.Text = $"设置保存失败：{ex.Message}";
            }
        };
        _settingsWindow.Show();
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleWindowMaximize();
            return;
        }

        DragMove();
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeClick(object sender, RoutedEventArgs e)
    {
        ToggleWindowMaximize();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleWindowMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnReminderDue(object? sender, ReminderDueEventArgs e)
    {
        if (_usageTimer is null || _reminderOverlayWindow is { IsVisible: true })
        {
            return;
        }

        if (ReminderSuppressionDetector.IsMediaPlaybackApp(e.CurrentApp))
        {
            try
            {
                MediaPlaybackService.SendPlayPause();
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex, "Pausing media playback before reminder failed");
            }
        }

        _reminderOverlayWindow = new ReminderOverlayWindow(_bootstrapper.Settings);
        _reminderOverlayWindow.Closed += async (_, _) =>
        {
            var action = _reminderOverlayWindow.Action;
            _reminderOverlayWindow = null;
            _reminderScheduler?.MarkReminderClosed();
            try
            {
                await _usageTimer.ResetContinuousAsync(action);
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex, "Resetting continuous usage after reminder failed");
                StatusText.Text = $"休息记录保存失败：{ex.Message}";
            }
        };
        _reminderOverlayWindow.Show();
    }

    private void ShowMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void TogglePause()
    {
        if (_usageTimer is null)
        {
            return;
        }

        if (_usageTimer.IsPaused)
        {
            _usageTimer.Resume();
            _trayService?.SetPaused(false);
            StateText.Text = "活跃";
            return;
        }

        _usageTimer.Pause();
        _trayService?.SetPaused(true);
        StateText.Text = "已暂停";
    }

    private void ExitApplication()
    {
        _isExitRequested = true;
        Close();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);

        if (WindowState == WindowState.Minimized && _bootstrapper.Settings.MinimizeToTray)
        {
            Hide();
        }
    }

    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isExitRequested && _bootstrapper.Settings.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        if (_isClosingForExit)
        {
            return;
        }

        e.Cancel = true;
        _isClosingForExit = true;

        try
        {
            if (_usageTimer is not null)
            {
                await _usageTimer.StopAsync();
                _usageTimer.Dispose();
            }

            _reminderOverlayWindow?.ForceClose();
            _settingsWindow?.Close();
            _trayService?.Dispose();
            _liquidGlassBackdrop.Dispose();
        }
        catch (Exception ex)
        {
            AppLogger.Log(ex, "Application shutdown cleanup failed");
        }
        finally
        {
            _ = Dispatcher.BeginInvoke(new Action(Close));
        }
    }

    private void ApplyLiquidGlassBackdrop()
    {
        if (ThemeService.IsLiquidGlassMode(_bootstrapper.Settings.ThemeMode))
        {
            _liquidGlassBackdrop.Start();
            return;
        }

        _liquidGlassBackdrop.Stop();
    }

    private static string FormatDuration(int totalSeconds)
    {
        if (totalSeconds < 60)
        {
            return $"{totalSeconds} 秒";
        }

        var hours = totalSeconds / 3600;
        var minutes = totalSeconds % 3600 / 60;

        return hours > 0 ? $"{hours} 小时 {minutes} 分钟" : $"{minutes} 分钟";
    }

    private static string FormatCategory(AppCategory category)
    {
        return category switch
        {
            AppCategory.Work => "工作",
            AppCategory.Social => "社交",
            AppCategory.Entertainment => "娱乐",
            AppCategory.Learning => "学习",
            AppCategory.System => "系统",
            _ => "其他"
        };
    }

    private static string FormatReminderCharacter(string reminderCharacter)
    {
        return reminderCharacter.Equals("dog", StringComparison.OrdinalIgnoreCase) ? "小狗" : "小猫";
    }

    private async Task RenderDashboardSafelyAsync()
    {
        if (_isDashboardRendering)
        {
            _isDashboardRenderPending = true;
            return;
        }

        _isDashboardRendering = true;
        try
        {
            do
            {
                _isDashboardRenderPending = false;
                try
                {
                    await RenderDashboardAsync();
                }
                catch (Exception ex)
                {
                    AppLogger.Log(ex, "Rendering dashboard failed");
                    StatusText.Text = $"看板刷新失败：{ex.Message}";
                    return;
                }
            }
            while (_isDashboardRenderPending);
        }
        finally
        {
            _isDashboardRendering = false;
        }
    }

    private async Task RenderDashboardAsync()
    {
        if (_bootstrapper.UsageStore is null)
        {
            return;
        }

        _weeklyAverageSecondsByAppKey = await LoadWeeklyAverageSecondsByAppKeyAsync(_selectedDate);

        if (_dashboardMode == DashboardMode.Daily)
        {
            var usage = IsToday(_selectedDate)
                ? _bootstrapper.TodayUsage
                : await _bootstrapper.UsageStore.LoadExistingOrEmptyAsync(_selectedDate);
            RenderDailyDashboard(usage);
            return;
        }

        var weekStart = StartOfWeek(_selectedDate);
        var days = new List<DailyUsage>();
        for (var i = 0; i < 7; i++)
        {
            var date = weekStart.AddDays(i);
            days.Add(IsToday(date)
                ? _bootstrapper.TodayUsage
                : await _bootstrapper.UsageStore.LoadExistingOrEmptyAsync(date));
        }

        RenderWeeklyDashboard(weekStart, days);
    }

    private void RenderDailyDashboard(DailyUsage usage)
    {
        UpdateModeButtons();
        PeriodLabelText.Text = $"{usage.Date:yyyy 年 M 月 d 日}";
        _currentPeriodTotalText = FormatDuration(usage.TotalActiveSeconds);
        ShowPeriodTotalUnlessBarValueActive();
        var hourlyBars = BuildHourlyBars(usage);
        _currentChartData = hourlyBars;
        UsageBarsList.ItemsSource = hourlyBars.Bars;
        ChartMaxText.Text = hourlyBars.AxisMaxText;
        ChartMidText.Text = FormatAxisDuration(hourlyBars.AxisMaxSeconds / 2, DashboardMode.Daily);
        XAxisLabelsList.ItemsSource = hourlyBars.Bars.Select(bar => bar.Label).ToList();
        AverageLine.Visibility = Visibility.Collapsed;
        AverageLineText.Visibility = Visibility.Collapsed;
        _currentApps = usage.Apps.Values.ToList();
        _currentAppsTotalSeconds = usage.TotalActiveSeconds;
        RefreshAppLists();
    }

    private void RenderWeeklyDashboard(DateOnly weekStart, IReadOnlyList<DailyUsage> days)
    {
        UpdateModeButtons();
        var weekEnd = weekStart.AddDays(6);
        var totalSeconds = days.Sum(day => day.TotalActiveSeconds);
        PeriodLabelText.Text = $"{weekStart:yyyy 年 M 月 d 日} - {weekEnd:M 月 d 日}";
        _currentPeriodTotalText = FormatDuration(totalSeconds);
        ShowPeriodTotalUnlessBarValueActive();

        var weeklyBars = BuildWeeklyBars(days);
        _currentChartData = weeklyBars;
        UsageBarsList.ItemsSource = weeklyBars.Bars;
        ChartMaxText.Text = weeklyBars.AxisMaxText;
        ChartMidText.Text = FormatAxisDuration(weeklyBars.AxisMaxSeconds / 2, DashboardMode.Weekly);
        XAxisLabelsList.ItemsSource = new[] { "一", "二", "三", "四", "五", "六", "日" };
        AverageLineText.Text = $"平均 {FormatDurationCompact(weeklyBars.AverageSeconds)}";
        AverageLine.Visibility = weeklyBars.AverageSeconds > 0 ? Visibility.Visible : Visibility.Collapsed;
        AverageLineText.Visibility = weeklyBars.AverageSeconds > 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateAverageLinePosition();

        var apps = MergeApps(days);
        _currentApps = apps;
        _currentAppsTotalSeconds = totalSeconds;
        RefreshAppLists();
    }

    private void RefreshAppLists()
    {
        if (_selectedCategory is not null && _currentApps.All(app => app.Category != _selectedCategory.Value))
        {
            _selectedCategory = null;
        }

        var apps = _selectedCategory is null
            ? _currentApps
            : _currentApps.Where(app => app.Category == _selectedCategory.Value).ToList();

        CategoryStatsList.ItemsSource = BuildCategoryStats(_currentApps, _currentAppsTotalSeconds, _selectedCategory);
        TopAppsList.ItemsSource = BuildAppRows(apps, _currentAppsTotalSeconds);
    }

    private void OnCategoryStatClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CategoryStatRow row })
        {
            return;
        }

        ClearAppDetail();
        if (row.Category is null)
        {
            _selectedCategory = null;
        }
        else
        {
            _selectedCategory = _selectedCategory == row.Category.Value ? null : row.Category.Value;
        }

        RefreshAppLists();
    }

    private async Task<Dictionary<string, int>> LoadWeeklyAverageSecondsByAppKeyAsync(DateOnly selectedDate)
    {
        var weekStart = StartOfWeek(selectedDate);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var weekEnd = weekStart.AddDays(6);
        var elapsedEnd = weekEnd < today ? weekEnd : today;
        var elapsedDays = Math.Max(1, elapsedEnd.DayNumber - weekStart.DayNumber + 1);
        var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < elapsedDays; i++)
        {
            var date = weekStart.AddDays(i);
            var usage = IsToday(date)
                ? _bootstrapper.TodayUsage
                : await _bootstrapper.UsageStore.LoadExistingOrEmptyAsync(date);
            foreach (var app in usage.Apps.Values)
            {
                var key = AppKey(app);
                totals.TryGetValue(key, out var seconds);
                totals[key] = seconds + app.ActiveSeconds;
            }
        }

        return totals.ToDictionary(
            pair => pair.Key,
            pair => pair.Value / elapsedDays,
            StringComparer.OrdinalIgnoreCase);
    }

    private void ClearAppDetail()
    {
        TopAppsList.SelectedItem = null;
    }

    private void OnUsageBarClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ChartBar bar })
        {
            if (bar.TotalSeconds <= 0)
            {
                return;
            }

            _barValueVisibleUntil = DateTimeOffset.Now.AddSeconds(5);
            _barValueTimer.Stop();
            _barValueTimer.Start();
            PeriodTotalText.Text = bar.TotalText;
        }
    }

    private void OnBarValueTimerTick(object? sender, EventArgs e)
    {
        if (DateTimeOffset.Now < _barValueVisibleUntil)
        {
            return;
        }

        ClearBarValueDisplay();
        PeriodTotalText.Text = _currentPeriodTotalText;
    }

    private void ClearBarValueDisplay()
    {
        _barValueVisibleUntil = DateTimeOffset.MinValue;
        _barValueTimer.Stop();
    }

    private void ShowPeriodTotalUnlessBarValueActive()
    {
        if (DateTimeOffset.Now >= _barValueVisibleUntil)
        {
            PeriodTotalText.Text = _currentPeriodTotalText;
        }
    }

    private void OnChartPlotAreaSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateAverageLinePosition();
    }

    private void UpdateAverageLinePosition()
    {
        if (_currentChartData is not { AverageSeconds: > 0 } chartData || ChartPlotArea.ActualHeight <= 0)
        {
            return;
        }

        var averageLineTop = ChartPlotArea.ActualHeight * (1 - chartData.AverageRatio);
        AverageLine.Margin = new Thickness(0, averageLineTop, 0, 0);
        AverageLineText.Margin = new Thickness(0, Math.Max(0, averageLineTop - 16), 0, 0);
    }

    private void UpdateModeButtons()
    {
        DailyModeButton.SetResourceReference(BackgroundProperty, _dashboardMode == DashboardMode.Daily ? "AccentSoftBrush" : "SurfaceBrush");
        WeeklyModeButton.SetResourceReference(BackgroundProperty, _dashboardMode == DashboardMode.Weekly ? "AccentSoftBrush" : "SurfaceBrush");
        DailyModeButton.SetResourceReference(BorderBrushProperty, _dashboardMode == DashboardMode.Daily ? "AccentBorderBrush" : "InputBorderBrush");
        WeeklyModeButton.SetResourceReference(BorderBrushProperty, _dashboardMode == DashboardMode.Weekly ? "AccentBorderBrush" : "InputBorderBrush");
        NextPeriodButton.IsEnabled = CanMoveNextPeriod();
    }

    private bool CanMoveNextPeriod()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        return _dashboardMode == DashboardMode.Daily
            ? _selectedDate < today
            : StartOfWeek(_selectedDate) < StartOfWeek(today);
    }

    private static List<AppUsage> MergeApps(IEnumerable<DailyUsage> days)
    {
        var merged = new Dictionary<string, AppUsage>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in days.SelectMany(day => day.Apps.Values))
        {
            var key = string.IsNullOrWhiteSpace(app.ExecutablePath) ? app.ProcessName : app.ExecutablePath;
            if (!merged.TryGetValue(key, out var target))
            {
                target = new AppUsage
                {
                    Name = app.Name,
                    ProcessName = app.ProcessName,
                    ExecutablePath = app.ExecutablePath,
                    Category = app.Category,
                    FirstUsedAt = app.FirstUsedAt,
                    LastUsedAt = app.LastUsedAt
                };
                merged[key] = target;
            }

            target.ActiveSeconds += app.ActiveSeconds;
            if (app.LastUsedAt > target.LastUsedAt)
            {
                target.LastUsedAt = app.LastUsedAt;
                target.Name = app.Name;
                target.Category = app.Category;
            }
        }

        return merged.Values.ToList();
    }

    private static ChartData BuildHourlyBars(DailyUsage usage)
    {
        var allHours = Enumerable.Range(0, 24)
            .Select(hour =>
            {
                usage.HourlyCategorySeconds.TryGetValue(hour, out var categories);
                return new TimeBucket(hour, CopyCategorySeconds(categories));
            })
            .ToList();
        PopulateLegacyHourlyBuckets(usage, allHours);

        var firstRecordedHour = allHours.FirstOrDefault(hour => hour.TotalSeconds > 0)?.Hour ?? 0;
        var hours = firstRecordedHour <= 0
            ? allHours
            :
            [
                new TimeBucket(0, new Dictionary<AppCategory, int>()),
                new TimeBucket(-1, new Dictionary<AppCategory, int>()),
                .. allHours.Where(hour => hour.Hour >= firstRecordedHour)
            ];
        const int axisMaxSeconds = 3600;

        return new ChartData(
            hours.Select(hour => BuildStackedBar(
                hour.Hour < 0 ? "..." : hour.Hour.ToString(),
                hour.TotalSeconds > 0 ? FormatDurationCompact(hour.TotalSeconds) : string.Empty,
                FormatDuration(hour.TotalSeconds),
                hour.CategorySeconds,
                axisMaxSeconds,
                14)).ToList(),
            FormatAxisDuration(axisMaxSeconds, DashboardMode.Daily),
            axisMaxSeconds,
            0,
            0);
    }

    private static Dictionary<AppCategory, int> CopyCategorySeconds(IReadOnlyDictionary<AppCategory, int>? categories)
    {
        return categories is null
            ? []
            : categories.ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private static void PopulateLegacyHourlyBuckets(DailyUsage usage, List<TimeBucket> hours)
    {
        if (hours.Sum(hour => hour.TotalSeconds) > 0 || usage.TotalActiveSeconds <= 0)
        {
            return;
        }

        foreach (var app in usage.Apps.Values.Where(app => app.ActiveSeconds > 0))
        {
            var usedAt = app.LastUsedAt != default ? app.LastUsedAt : app.FirstUsedAt;
            var hour = usedAt != default && DateOnly.FromDateTime(usedAt.LocalDateTime) == usage.Date
                ? usedAt.LocalDateTime.Hour
                : 0;
            var bucket = hours[Math.Clamp(hour, 0, 23)];
            bucket.CategorySeconds.TryGetValue(app.Category, out var seconds);
            bucket.CategorySeconds[app.Category] = seconds + app.ActiveSeconds;
        }
    }

    private static ChartData BuildWeeklyBars(IReadOnlyList<DailyUsage> days)
    {
        var buckets = days.Select(day =>
        {
            var categorySeconds = CategoryOrder.ToDictionary(
                category => category,
                category => day.Apps.Values.Where(app => app.Category == category).Sum(app => app.ActiveSeconds));
            return new
            {
                day.Date,
                CategorySeconds = categorySeconds,
                day.TotalActiveSeconds
            };
        }).ToList();

        var averageSeconds = buckets.Count == 0 ? 0 : (int)buckets.Average(bucket => bucket.TotalActiveSeconds);
        var maxBucketSeconds = Math.Max(averageSeconds, buckets.Max(bucket => bucket.TotalActiveSeconds));
        var axisMaxSeconds = RoundUp(maxBucketSeconds, 3600, 3600);
        var averageRatio = axisMaxSeconds == 0 ? 0 : averageSeconds / (double)axisMaxSeconds;

        return new ChartData(
            buckets.Select(bucket => BuildStackedBar(
                string.Empty,
                bucket.TotalActiveSeconds > 0 ? FormatDurationCompact(bucket.TotalActiveSeconds) : string.Empty,
                FormatDuration(bucket.TotalActiveSeconds),
                bucket.CategorySeconds,
                axisMaxSeconds,
                30)).ToList(),
            FormatAxisDuration(axisMaxSeconds, DashboardMode.Weekly),
            axisMaxSeconds,
            averageSeconds,
            averageRatio);
    }

    private static ChartBar BuildStackedBar(
        string label,
        string valueText,
        string totalText,
        IReadOnlyDictionary<AppCategory, int> categorySeconds,
        int axisMaxSeconds,
        double width)
    {
        var segments = CategoryOrder
            .Select(category =>
            {
                categorySeconds.TryGetValue(category, out var seconds);
                return new ChartSegment(
                    seconds,
                    axisMaxSeconds,
                    width,
                    CategoryBrush(category));
            })
            .Where(segment => segment.Seconds > 0)
            .Reverse()
            .ToList();

        return new ChartBar(label, valueText, totalText, categorySeconds.Values.Sum(), segments);
    }

    private static List<CategoryStatRow> BuildCategoryStats(IEnumerable<AppUsage> apps, int totalSeconds, AppCategory? selectedCategory)
    {
        return CategoryOrder
            .Select(category =>
            {
                var seconds = apps.Where(app => app.Category == category).Sum(app => app.ActiveSeconds);
                return new CategoryStatRow(
                    category,
                    FormatCategory(category),
                    FormatDurationCompact(seconds),
                    seconds,
                    CategoryBrush(category),
                    selectedCategory == category);
            })
            .Where(row => row.Seconds > 0)
            .ToList();
    }

    private List<TopAppRow> BuildAppRows(IEnumerable<AppUsage> apps, int totalSeconds)
    {
        return apps
            .OrderByDescending(app => app.ActiveSeconds)
            .Select(app =>
            {
                var key = AppKey(app);
                var sharePercent = totalSeconds == 0 ? 0 : app.ActiveSeconds * 100.0 / totalSeconds;
                _weeklyAverageSecondsByAppKey.TryGetValue(key, out var averageSeconds);
                return new TopAppRow(
                    key,
                    _appIconProvider.GetIcon(app),
                    app.Name,
                    FormatCategory(app.Category),
                    FormatDurationShort(app.ActiveSeconds),
                    FormatDuration(averageSeconds),
                    sharePercent <= 0 ? 0 : Math.Max(8, Math.Min(148, sharePercent * 1.48)),
                    $"{sharePercent:0}%");
            })
            .ToList();
    }

    private static string FormatDurationShort(int totalSeconds)
    {
        if (totalSeconds < 60)
        {
            return $"{totalSeconds}秒";
        }

        var hours = totalSeconds / 3600;
        var minutes = totalSeconds % 3600 / 60;
        if (hours <= 0)
        {
            return $"{minutes}分钟";
        }

        return minutes == 0 ? $"{hours}小时" : $"{hours}时{minutes}分";
    }

    private static string AppKey(AppUsage app)
    {
        if (!string.IsNullOrWhiteSpace(app.ExecutablePath))
        {
            return app.ExecutablePath;
        }

        if (!string.IsNullOrWhiteSpace(app.ProcessName))
        {
            return app.ProcessName;
        }

        return app.Name;
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }

    private static bool IsToday(DateOnly date)
    {
        return date == DateOnly.FromDateTime(DateTime.Now);
    }

    private static string FormatWeekday(DateOnly date)
    {
        return date.DayOfWeek switch
        {
            DayOfWeek.Monday => "一",
            DayOfWeek.Tuesday => "二",
            DayOfWeek.Wednesday => "三",
            DayOfWeek.Thursday => "四",
            DayOfWeek.Friday => "五",
            DayOfWeek.Saturday => "六",
            _ => "日"
        };
    }

    private static string FormatDurationCompact(int totalSeconds)
    {
        if (totalSeconds < 60)
        {
            return $"{totalSeconds}秒";
        }

        var hours = totalSeconds / 3600;
        var minutes = totalSeconds % 3600 / 60;
        return hours > 0 ? $"{hours}小时{minutes}分" : $"{minutes}分";
    }

    private static int RoundUp(int value, int step, int minimum)
    {
        if (value <= 0)
        {
            return minimum;
        }

        return Math.Max(minimum, (int)Math.Ceiling(value / (double)step) * step);
    }

    private static string FormatAxisDuration(int seconds, DashboardMode mode)
    {
        if (mode == DashboardMode.Daily)
        {
            return $"{Math.Max(1, seconds / 60)}分钟";
        }

        return $"{Math.Max(1, seconds / 3600)}小时";
    }

    private static string FormatCategoryShort(AppCategory category)
    {
        return category switch
        {
            AppCategory.Work => "工作",
            AppCategory.Social => "社交",
            AppCategory.Entertainment => "娱乐",
            AppCategory.Learning => "学习",
            AppCategory.System => "系统",
            _ => "其他"
        };
    }

    private static MediaBrush CategoryBrush(AppCategory category)
    {
        var color = category switch
        {
            AppCategory.Work => "#2F80ED",
            AppCategory.Social => "#20B486",
            AppCategory.Entertainment => "#F2994A",
            AppCategory.Learning => "#9B51E0",
            AppCategory.System => "#8A8F98",
            _ => "#B8B8C2"
        };
        var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    private static readonly AppCategory[] CategoryOrder =
    [
        AppCategory.Work,
        AppCategory.Social,
        AppCategory.Entertainment,
        AppCategory.Learning,
        AppCategory.System,
        AppCategory.Other
    ];

    private enum DashboardMode
    {
        Weekly,
        Daily
    }

    private sealed record TopAppRow(
        string Key,
        ImageSource Icon,
        string Name,
        string Category,
        string Time,
        string AverageTime,
        double ProgressWidth,
        string Share);
    private sealed record TimeBucket(int Hour, Dictionary<AppCategory, int> CategorySeconds)
    {
        public int TotalSeconds => CategorySeconds.Values.Sum();
    }

    private sealed record ChartData(List<ChartBar> Bars, string AxisMaxText, int AxisMaxSeconds, int AverageSeconds, double AverageRatio);
    private sealed record ChartBar(string Label, string ValueText, string TotalText, int TotalSeconds, List<ChartSegment> Segments);
    private sealed record ChartSegment(int Seconds, int AxisMaxSeconds, double Width, MediaBrush Fill);
    private sealed record CategoryStatRow(AppCategory? Category, string Name, string Time, int Seconds, MediaBrush Fill, bool IsSelected);
}

public sealed class ChartSegmentHeightConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 3 ||
            values[0] is not int seconds ||
            values[1] is not int axisMaxSeconds ||
            values[2] is not double plotHeight ||
            seconds <= 0 ||
            axisMaxSeconds <= 0 ||
            plotHeight <= 0)
        {
            return 0d;
        }

        return Math.Max(3d, seconds * plotHeight / axisMaxSeconds);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

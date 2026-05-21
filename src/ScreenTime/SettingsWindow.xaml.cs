using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.Windows.Input;
using ScreenTime.Models;
using ScreenTime.Services;
using WpfDataFormats = System.Windows.DataFormats;
using WpfDataObject = System.Windows.DataObject;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace ScreenTime;

public partial class SettingsWindow : Window
{
    private readonly UserSettings _settings;
    private readonly LiquidGlassBackdropService _liquidGlassBackdrop;
    private readonly UpdateCheckerService _updateChecker = new();
    private string? _latestUpdateUrl;

    public SettingsWindow(UserSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        _liquidGlassBackdrop = new LiquidGlassBackdropService(this, LiquidGlassBackdrop);
        SourceInitialized += (_, _) =>
        {
            ThemeService.ApplyWindowTitleBar(this, _settings.ThemeMode);
            ApplyLiquidGlassBackdrop();
        };
        Closed += (_, _) => _liquidGlassBackdrop.Dispose();
        LoadSettings();
        ConfigureIntegerInput(ReminderIntervalBox);
        ConfigureIntegerInput(BreakDurationBox);
        ConfigureIntegerInput(IdleThresholdBox);
    }

    public bool SettingsSaved { get; private set; }

    private void LoadSettings()
    {
        CurrentVersionText.Text = $"当前版本 v{UpdateCheckerService.CurrentVersion}";
        UpdateStatusText.Text = string.Empty;
        UpdateStatusText.Visibility = Visibility.Collapsed;
        ReminderEnabledBox.IsChecked = _settings.ReminderEnabled;
        AllowCloseBox.IsChecked = _settings.AllowCloseFullscreenReminder;

        TrayAnimationBox.IsChecked = _settings.TrayReminderAnimationEnabled;
        LaunchAtStartupBox.IsChecked = _settings.LaunchAtStartup;
        // Reminder character selection is temporarily hidden in SettingsWindow.xaml.
        ThemeModeBox.SelectedIndex = _settings.ThemeMode switch
        {
            "light" => 1,
            "dark" => 2,
            "liquid_glass" => 3,
            _ => 0
        };
        ReminderIntervalBox.Text = _settings.ReminderIntervalMinutes.ToString();
        BreakDurationBox.Text = _settings.BreakDurationMinutes.ToString();
        IdleThresholdBox.Text = _settings.IdleThresholdSeconds.ToString();
        OverlayOpacitySlider.Value = _settings.OverlayOpacity;
        UpdateOverlayOpacityText();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnOverlayOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateOverlayOpacityText();
    }

    private void UpdateOverlayOpacityText()
    {
        if (OverlayOpacityValueText is null)
        {
            return;
        }

        OverlayOpacityValueText.Text = $"{Math.Round(OverlayOpacitySlider.Value * 100):0}%";
    }

    private async void OnCheckUpdateClick(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        DownloadUpdateButton.Visibility = Visibility.Collapsed;
        _latestUpdateUrl = null;
        UpdateStatusText.Text = "正在检查更新...";
        UpdateStatusText.Visibility = Visibility.Visible;

        try
        {
            var result = await _updateChecker.CheckLatestAsync();
            if (result.HasUpdate)
            {
                _latestUpdateUrl = result.UpdateUrl;
                UpdateStatusText.Text = $"发现新版本 {result.LatestTagName}，当前版本 v{result.CurrentVersion}。";
                DownloadUpdateButton.Visibility = string.IsNullOrWhiteSpace(_latestUpdateUrl)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                return;
            }

            UpdateStatusText.Text = $"当前已是最新版本 v{result.CurrentVersion}。";
        }
        catch (Exception ex)
        {
            AppLogger.Log(ex, "Failed to check for updates");
            UpdateStatusText.Text = "检查更新失败，请稍后重试。";
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private void OnDownloadUpdateClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_latestUpdateUrl))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(_latestUpdateUrl)
        {
            UseShellExecute = true
        });
    }

    private void ApplyLiquidGlassBackdrop()
    {
        if (ThemeService.IsLiquidGlassMode(_settings.ThemeMode))
        {
            _liquidGlassBackdrop.Start();
            return;
        }

        _liquidGlassBackdrop.Stop();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;

        if (!TryParseInt(ReminderIntervalBox.Text, 1, 240, out var reminderInterval))
        {
            ErrorText.Text = "提醒间隔必须是 1 到 240 分钟之间的整数。";
            return;
        }

        if (!TryParseInt(BreakDurationBox.Text, 1, 60, out var breakDuration))
        {
            ErrorText.Text = "休息时长必须是 1 到 60 分钟之间的整数。";
            return;
        }

        if (!TryParseInt(IdleThresholdBox.Text, 10, 3600, out var idleThreshold))
        {
            ErrorText.Text = "空闲阈值必须是 10 到 3600 秒之间的整数。";
            return;
        }

        _settings.ReminderEnabled = ReminderEnabledBox.IsChecked == true;
        _settings.AllowCloseFullscreenReminder = AllowCloseBox.IsChecked == true;

        _settings.TrayReminderAnimationEnabled = TrayAnimationBox.IsChecked == true;
        _settings.LaunchAtStartup = LaunchAtStartupBox.IsChecked == true;
        // Keep the existing reminder character while the setting is temporarily hidden.
        _settings.ThemeMode = ((ComboBoxItem)ThemeModeBox.SelectedItem).Tag?.ToString() ?? "system";
        _settings.ReminderIntervalMinutes = reminderInterval;
        _settings.BreakDurationMinutes = breakDuration;
        _settings.IdleThresholdSeconds = idleThreshold;
        _settings.OverlayOpacity = OverlayOpacitySlider.Value;

        SettingsSaved = true;
        Close();
    }

    private static bool TryParseInt(string value, int min, int max, out int parsed)
    {
        return int.TryParse(value, out parsed) && parsed >= min && parsed <= max;
    }

    private static void ConfigureIntegerInput(WpfTextBox textBox)
    {
        textBox.PreviewTextInput += OnIntegerPreviewTextInput;
        textBox.PreviewKeyDown += OnIntegerPreviewKeyDown;
        WpfDataObject.AddPastingHandler(textBox, OnIntegerPaste);
    }

    private static void OnIntegerPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not WpfTextBox textBox)
        {
            e.Handled = true;
            return;
        }

        e.Handled = !WouldRemainInteger(textBox, e.Text);
    }

    private static void OnIntegerPreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            e.Handled = true;
        }
    }

    private static void OnIntegerPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not WpfTextBox textBox
            || !e.DataObject.GetDataPresent(WpfDataFormats.Text)
            || e.DataObject.GetData(WpfDataFormats.Text) is not string pastedText
            || !WouldRemainInteger(textBox, pastedText))
        {
            e.CancelCommand();
        }
    }

    private static bool WouldRemainInteger(WpfTextBox textBox, string input)
    {
        var proposed = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength)
            .Insert(textBox.SelectionStart, input);

        return proposed.All(char.IsAsciiDigit);
    }
}

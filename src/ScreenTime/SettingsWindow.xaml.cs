using System.Windows;
using System.Windows.Controls;
using ScreenTime.Models;
using ScreenTime.Services;

namespace ScreenTime;

public partial class SettingsWindow : Window
{
    private readonly UserSettings _settings;

    public SettingsWindow(UserSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        SourceInitialized += (_, _) => ThemeService.ApplyWindowTitleBar(this, _settings.ThemeMode);
        LoadSettings();
    }

    public bool SettingsSaved { get; private set; }

    private void LoadSettings()
    {
        ReminderEnabledBox.IsChecked = _settings.ReminderEnabled;
        AllowCloseBox.IsChecked = _settings.AllowCloseFullscreenReminder;
        MinimizeToTrayBox.IsChecked = _settings.MinimizeToTray;
        TrayAnimationBox.IsChecked = _settings.TrayReminderAnimationEnabled;
        LaunchAtStartupBox.IsChecked = _settings.LaunchAtStartup;
        // Reminder character selection is temporarily hidden in SettingsWindow.xaml.
        ThemeModeBox.SelectedIndex = _settings.ThemeMode switch
        {
            "light" => 1,
            "dark" => 2,
            _ => 0
        };
        ReminderIntervalBox.Text = _settings.ReminderIntervalMinutes.ToString();
        BreakDurationBox.Text = _settings.BreakDurationMinutes.ToString();
        IdleThresholdBox.Text = _settings.IdleThresholdSeconds.ToString();
        OverlayOpacitySlider.Value = _settings.OverlayOpacity;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Close();
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
        _settings.MinimizeToTray = MinimizeToTrayBox.IsChecked == true;
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
}

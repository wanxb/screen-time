using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using ScreenTime.Core;

namespace ScreenTime.Services;

public static class ThemeService
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;

    public static void Apply(string? themeMode)
    {
        var useDark = IsDarkMode(themeMode);

        var resources = System.Windows.Application.Current.Resources;
        resources["PageBrush"] = Brush(useDark ? "#11130F" : "#F7F5F0");
        resources["SurfaceBrush"] = Brush(useDark ? "#1B1E18" : "#FFFFFF");
        resources["SurfaceAltBrush"] = Brush(useDark ? "#24281F" : "#F1EDE5");
        resources["BorderBrush"] = Brush(useDark ? "#3A4034" : "#E3DED5");
        resources["TextPrimaryBrush"] = Brush(useDark ? "#F5F1E8" : "#1D1B18");
        resources["TextSecondaryBrush"] = Brush(useDark ? "#C7C0B2" : "#5F5A52");
        resources["TextMutedBrush"] = Brush(useDark ? "#9F9788" : "#777067");
        resources["AccentBrush"] = Brush(useDark ? "#8FD3A8" : "#2F7D52");
        resources["AccentSoftBrush"] = Brush(useDark ? "#20392A" : "#E9F4EF");
        resources["AccentBorderBrush"] = Brush(useDark ? "#386247" : "#B9D9C9");
        resources["DangerBrush"] = Brush(useDark ? "#FFB4A9" : "#B42318");
        resources["InputBrush"] = Brush(useDark ? "#12140F" : "#FFFFFF");
        resources["InputBorderBrush"] = Brush(useDark ? "#4B5244" : "#D7D0C5");
        resources["OverlayCardBrush"] = Brush(useDark ? "#181A15" : "#F7F5F0");
        resources["ReminderCharacterBrush"] = Brush(useDark ? "#F2F2ED" : "#222320");
    }

    public static void ApplyWindowTitleBar(Window window, string? themeMode)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == 0)
        {
            return;
        }

        var enabled = IsDarkMode(themeMode) ? 1 : 0;
        var size = sizeof(int);

        if (NativeMethods.DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref enabled, size) != 0)
        {
            NativeMethods.DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkModeBefore20H1, ref enabled, size);
        }
    }

    public static bool IsDarkMode(string? themeMode)
    {
        var mode = string.IsNullOrWhiteSpace(themeMode) ? "system" : themeMode;
        return mode.Equals("dark", StringComparison.OrdinalIgnoreCase)
            || (mode.Equals("system", StringComparison.OrdinalIgnoreCase) && IsSystemDarkMode());
    }

    public static bool IsTrayDarkMode(string? themeMode)
    {
        return IsSystemShellDarkMode();
    }

    private static bool IsSystemDarkMode()
    {
        return IsRegistryDarkMode("AppsUseLightTheme", false);
    }

    private static bool IsSystemShellDarkMode()
    {
        return IsRegistryDarkMode("SystemUsesLightTheme", IsSystemDarkMode());
    }

    private static bool IsRegistryDarkMode(string valueName, bool fallback)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue(valueName);
            return value is int intValue ? intValue == 0 : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}

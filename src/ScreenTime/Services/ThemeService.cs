using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using ScreenTime.Core;

namespace ScreenTime.Services;

public static class ThemeService
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmSystemBackdropNone = 1;
    private const int DwmSystemBackdropTransientWindow = 3;
    private const int WcaAccentPolicy = 19;
    private const int AccentDisabled = 0;
    private const int AccentEnableBlurBehind = 3;

    public static void Apply(string? themeMode)
    {
        if (IsLiquidGlassMode(themeMode))
        {
            ApplyLiquidGlassResources();
            return;
        }

        var useDark = IsDarkMode(themeMode);

        var resources = System.Windows.Application.Current.Resources;
        resources["PageBrush"] = Brush(useDark ? "#11130F" : "#F7F5F0");
        resources["SurfaceBrush"] = Brush(useDark ? "#1B1E18" : "#FFFFFF");
        resources["SurfaceAltBrush"] = Brush(useDark ? "#24281F" : "#F1EDE5");
        resources["BorderBrush"] = Brush(useDark ? "#3A4034" : "#E3DED5");
        resources["TextPrimaryBrush"] = Brush(useDark ? "#F5F1E8" : "#1D1B18");
        resources["TextSecondaryBrush"] = Brush(useDark ? "#C7C0B2" : "#5F5A52");
        resources["TextMutedBrush"] = Brush(useDark ? "#9F9788" : "#777067");
        resources["UsageProgressBrush"] = Brush(useDark ? "#A8A39A" : "#8D8A84");
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

        if (HwndSource.FromHwnd(handle) is { CompositionTarget: not null } source)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }

        var enabled = IsDarkMode(themeMode) || IsLiquidGlassMode(themeMode) ? 1 : 0;
        var size = sizeof(int);

        if (NativeMethods.DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref enabled, size) != 0)
        {
            NativeMethods.DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkModeBefore20H1, ref enabled, size);
        }

        var isLiquidGlass = IsLiquidGlassMode(themeMode);
        ApplyAcrylicComposition(handle, isLiquidGlass);

        var backdropType = isLiquidGlass ? DwmSystemBackdropTransientWindow : DwmSystemBackdropNone;
        NativeMethods.DwmSetWindowAttribute(handle, DwmwaSystemBackdropType, ref backdropType, size);
    }

    public static bool IsDarkMode(string? themeMode)
    {
        var mode = string.IsNullOrWhiteSpace(themeMode) ? "system" : themeMode;
        return mode.Equals("dark", StringComparison.OrdinalIgnoreCase)
            || (mode.Equals("system", StringComparison.OrdinalIgnoreCase) && IsSystemDarkMode());
    }

    public static bool IsLiquidGlassMode(string? themeMode)
    {
        return themeMode?.Equals("liquid_glass", StringComparison.OrdinalIgnoreCase) == true;
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

    private static void ApplyLiquidGlassResources()
    {
        var resources = System.Windows.Application.Current.Resources;
        resources["PageBrush"] = Brush("#00000000");
        resources["SurfaceBrush"] = Brush("#14FFFFFF");
        resources["SurfaceAltBrush"] = Brush("#22FFFFFF");
        resources["BorderBrush"] = Brush("#A8FFFFFF");
        resources["TextPrimaryBrush"] = Brush("#F7FBFF");
        resources["TextSecondaryBrush"] = Brush("#D8E7F7");
        resources["TextMutedBrush"] = Brush("#BED5E8");
        resources["UsageProgressBrush"] = Brush("#D8F3FF");
        resources["AccentBrush"] = Brush("#7BE7FF");
        resources["AccentSoftBrush"] = Brush("#367BE7FF");
        resources["AccentBorderBrush"] = Brush("#B8D8FAFF");
        resources["DangerBrush"] = Brush("#FFB8C1");
        resources["InputBrush"] = Brush("#10FFFFFF");
        resources["InputBorderBrush"] = Brush("#B8FFFFFF");
        resources["OverlayCardBrush"] = Brush("#18FFFFFF");
        resources["ReminderCharacterBrush"] = Brush("#F7FBFF");
    }

    private static void ApplyAcrylicComposition(nint handle, bool enabled)
    {
        var accent = new NativeMethods.AccentPolicy
        {
            AccentState = enabled ? AccentEnableBlurBehind : AccentDisabled,
            AccentFlags = enabled ? 2 : 0,
            GradientColor = 0,
            AnimationId = 0
        };

        var size = Marshal.SizeOf<NativeMethods.AccentPolicy>();
        var accentPtr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, accentPtr, false);
            var data = new NativeMethods.WindowCompositionAttributeData
            {
                Attribute = WcaAccentPolicy,
                Data = accentPtr,
                SizeOfData = size
            };
            NativeMethods.SetWindowCompositionAttribute(handle, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(accentPtr);
        }
    }

    private static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}

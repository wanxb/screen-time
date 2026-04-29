using Microsoft.Win32;
using ScreenTime.Models;

namespace ScreenTime.Services;

public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "ScreenTime";

    public static void Apply(UserSettings settings)
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (settings.LaunchAtStartup)
        {
            var executablePath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(executablePath))
            {
                runKey.SetValue(AppName, $"\"{executablePath}\" --startup");
            }

            return;
        }

        if (runKey.GetValue(AppName) is not null)
        {
            runKey.DeleteValue(AppName, throwOnMissingValue: false);
        }
    }
}

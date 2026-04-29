using System.Diagnostics;
using ScreenTime.Models;

namespace ScreenTime.Core;

public sealed class ForegroundAppTracker
{
    public ForegroundAppInfo? GetCurrent()
    {
        var handle = NativeMethods.GetForegroundWindow();
        if (handle == 0)
        {
            return null;
        }

        NativeMethods.GetWindowThreadProcessId(handle, out var processId);
        if (processId == 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            var processName = process.ProcessName;
            var executablePath = TryGetExecutablePath(process);
            var displayName = TryGetDisplayName(process, executablePath, processName);

            return new ForegroundAppInfo
            {
                ProcessId = (int)processId,
                Name = displayName,
                ProcessName = processName,
                ExecutablePath = executablePath
            };
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string TryGetExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string TryGetDisplayName(Process process, string executablePath, string processName)
    {
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(executablePath);
                if (!string.IsNullOrWhiteSpace(versionInfo.FileDescription))
                {
                    return versionInfo.FileDescription;
                }

                if (!string.IsNullOrWhiteSpace(versionInfo.ProductName))
                {
                    return versionInfo.ProductName;
                }
            }
            catch
            {
                // Process metadata is best-effort; process name is a reliable fallback.
            }
        }

        return string.IsNullOrWhiteSpace(process.MainWindowTitle) ? processName : process.MainWindowTitle;
    }
}

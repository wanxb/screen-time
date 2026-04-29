using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
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
            var windowTitle = TryGetWindowTitle(handle);

            return new ForegroundAppInfo
            {
                ProcessId = (int)processId,
                Name = displayName,
                ProcessName = processName,
                ExecutablePath = executablePath,
                WindowTitle = windowTitle,
                IsFullScreen = IsWindowFullScreen(handle)
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

    private static string TryGetWindowTitle(nint handle)
    {
        try
        {
            var length = NativeMethods.GetWindowTextLength(handle);
            if (length <= 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(length + 1);
            return NativeMethods.GetWindowText(handle, builder, builder.Capacity) > 0
                ? builder.ToString()
                : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsWindowFullScreen(nint handle)
    {
        const uint monitorDefaultToNearest = 2;
        const int tolerance = 8;

        if (!NativeMethods.GetWindowRect(handle, out var windowRect)
            || windowRect.Width <= 0
            || windowRect.Height <= 0)
        {
            return false;
        }

        var monitor = NativeMethods.MonitorFromWindow(handle, monitorDefaultToNearest);
        if (monitor == 0)
        {
            return false;
        }

        var monitorInfo = new NativeMethods.MonitorInfo
        {
            CbSize = (uint)Marshal.SizeOf<NativeMethods.MonitorInfo>()
        };
        if (!NativeMethods.GetMonitorInfo(monitor, ref monitorInfo))
        {
            return false;
        }

        var monitorRect = monitorInfo.RcMonitor;
        return windowRect.Left <= monitorRect.Left + tolerance
            && windowRect.Top <= monitorRect.Top + tolerance
            && windowRect.Right >= monitorRect.Right - tolerance
            && windowRect.Bottom >= monitorRect.Bottom - tolerance;
    }
}

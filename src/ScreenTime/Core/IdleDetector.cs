namespace ScreenTime.Core;

public sealed class IdleDetector
{
    public TimeSpan GetIdleTime()
    {
        var info = new NativeMethods.LastInputInfo
        {
            CbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.LastInputInfo>()
        };

        if (!NativeMethods.GetLastInputInfo(ref info))
        {
            return TimeSpan.Zero;
        }

        var elapsedMilliseconds = NativeMethods.GetTickCount() - info.DwTime;
        return TimeSpan.FromMilliseconds(elapsedMilliseconds);
    }

    public bool IsActive(int idleThresholdSeconds)
    {
        return GetIdleTime() < TimeSpan.FromSeconds(idleThresholdSeconds);
    }
}

using System.Runtime.InteropServices;

namespace ScreenTime.Services;

public static class MediaPlaybackService
{
    private const byte VkMediaPlayPause = 0xB3;
    private const uint KeyEventKeyUp = 0x0002;

    public static void SendPlayPause()
    {
        keybd_event(VkMediaPlayPause, 0, 0, UIntPtr.Zero);
        keybd_event(VkMediaPlayPause, 0, KeyEventKeyUp, UIntPtr.Zero);
    }

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}

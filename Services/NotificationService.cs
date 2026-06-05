using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Controls;

namespace ReceiptPrinterEmulator.Services;

/// <summary>
/// Default notification service. On Windows it flashes the taskbar button to draw attention
/// (mirroring the original WPF <c>user32!FlashWindow</c> behavior); on macOS/Linux it is a no-op,
/// since there is no cross-platform taskbar-flash API in Avalonia.
/// </summary>
public class NotificationService : INotificationService
{
    private Window? _window;

    public void AttachWindow(Window window) => _window = window;

    public void NotifyActivity()
    {
        if (OperatingSystem.IsWindows())
            FlashOnWindows();
    }

    [SupportedOSPlatform("windows")]
    private void FlashOnWindows()
    {
        var handle = _window?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle != IntPtr.Zero)
            FlashWindow(handle, true);
    }

    [SupportedOSPlatform("windows")]
    [DllImport("user32")]
    private static extern int FlashWindow(IntPtr hwnd, bool bInvert);
}

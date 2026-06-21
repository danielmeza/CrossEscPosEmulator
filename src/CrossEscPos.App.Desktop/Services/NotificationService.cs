using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia.Controls;
using CrossEscPos.Logging;

namespace CrossEscPos.Controls.Services;

/// <summary>
/// Default notification service. Plays a best-effort sound for buzzer / cash-drawer events
/// (afplay on macOS, Console.Beep on Windows, paplay/aplay/terminal-bell on Linux) and flashes the
/// taskbar on Windows. The primary, always-visible feedback is the on-screen toast shown by the view
/// model — sound here is supplementary since the machine may be muted.
/// </summary>
public class NotificationService : INotificationService
{
    private Window? _window;

    public void AttachWindow(Window window) => _window = window;

    public void NotifyActivity() => Flash();

    public void Beep()
    {
        Logger.Info("Buzzer");
        PlaySound(isDrawer: false);
        Flash();
    }

    public void OpenCashDrawer()
    {
        Logger.Info("Cash drawer kick");
        PlaySound(isDrawer: true);
        Flash();
    }

    private static void PlaySound(bool isDrawer)
    {
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                // Built-in macOS system sounds.
                var sound = isDrawer ? "/System/Library/Sounds/Funk.aiff"
                                     : "/System/Library/Sounds/Glass.aiff";
                if (!TryStart("/usr/bin/afplay", sound))
                    TryStart("/usr/bin/osascript", "-e", "beep");
            }
            else if (OperatingSystem.IsWindows())
            {
                // Console.Beep is synchronous — run it off the UI thread. The platform guard is
                // repeated inside the lambda so the analyzer sees it at the call site (CA1416).
                Task.Run(() =>
                {
                    if (!OperatingSystem.IsWindows()) return;
                    try
                    {
                        Console.Beep(isDrawer ? 600 : 1000, 180);
                        if (isDrawer) Console.Beep(900, 180);
                    }
                    catch { /* ignore */ }
                });
            }
            else // Linux / other Unix
            {
                if (!TryStart("canberra-gtk-play", "-i", isDrawer ? "message" : "bell") &&
                    !TryStart("paplay", "/usr/share/sounds/freedesktop/stereo/complete.oga") &&
                    !TryStart("aplay", "-q", "/usr/share/sounds/alsa/Front_Center.wav"))
                {
                    Console.Out.Write('\a');
                    Console.Out.Flush();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "Failed to play notification sound");
        }
    }

    private static bool TryStart(string fileName, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var a in args)
                psi.ArgumentList.Add(a);

            return Process.Start(psi) is not null;
        }
        catch
        {
            return false; // tool not installed — caller falls back
        }
    }

    private void Flash()
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

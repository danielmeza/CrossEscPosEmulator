using CrossEscPos.Controls.Services;

namespace CrossEscPos.App.Browser;

/// <summary>
/// Browser notifications — the on-screen toast (shown by the shared MainViewModel) is the feedback;
/// sound/flash have no clean browser equivalent here, so they are no-ops.
/// </summary>
public sealed class BrowserNotificationService : INotificationService
{
    public void NotifyActivity() { }
    public void Beep() { }
    public void OpenCashDrawer() { }
}

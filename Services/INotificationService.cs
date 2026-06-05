namespace ReceiptPrinterEmulator.Services;

/// <summary>
/// Surfaces user-facing "the printer just received data" notifications. Cross-platform by design;
/// platform-specific affordances (e.g. flashing the taskbar) are best-effort.
/// </summary>
public interface INotificationService
{
    void NotifyActivity();
}

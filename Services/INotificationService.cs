namespace ReceiptPrinterEmulator.Services;

/// <summary>
/// Surfaces user-facing printer events. Cross-platform by design; platform-specific affordances
/// (e.g. flashing the taskbar, playing a sound) are best-effort.
/// </summary>
public interface INotificationService
{
    /// <summary>The printer received and processed data.</summary>
    void NotifyActivity();

    /// <summary>The printer buzzer/beeper fired (ESC/POS BEL or buzzer command).</summary>
    void Beep();

    /// <summary>A cash-drawer kick pulse was issued (ESC p / DLE DC4).</summary>
    void OpenCashDrawer();
}

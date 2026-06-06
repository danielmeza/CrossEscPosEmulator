namespace ReceiptPrinterEmulator.Emulator.Enums;

/// <summary>Roll-paper level reported by the paper sensors.</summary>
public enum PaperLevel
{
    Adequate = 0,
    NearEnd = 1,
    Out = 2
}

/// <summary>Printer error condition reported by the error-status commands.</summary>
public enum PrinterErrorState
{
    None = 0,
    Recoverable = 1,
    Unrecoverable = 2
}

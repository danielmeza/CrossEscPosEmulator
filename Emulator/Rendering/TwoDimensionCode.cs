namespace ReceiptPrinterEmulator.Emulator.Rendering;

/// <summary>
/// ESC/POS 2D symbol family codes (the <c>cn</c> parameter of the GS ( k command).
/// </summary>
public static class TwoDimensionCode
{
    public const int Pdf417 = 48;
    public const int QrCode = 49;
    public const int DataMatrix = 54;
    public const int Aztec = 55;
}

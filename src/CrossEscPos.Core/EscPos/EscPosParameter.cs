namespace CrossEscPos.EscPos;

/// <summary>
/// Helpers for ESC/POS command parameters, which often accept a value either as a raw number (e.g. 0)
/// or as its ASCII digit ('0' = 48).
/// </summary>
internal static class EscPosParameter
{
    /// <summary>Folds an ASCII digit ('0'..'9') to its numeric value; leaves other bytes unchanged.</summary>
    public static int Digit(int n) => n is >= '0' and <= '9' ? n - '0' : n;
}

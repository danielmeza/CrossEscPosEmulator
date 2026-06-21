using Ardalis.SmartEnum;
using QRCoder;

namespace CrossEscPos.EscPos;

/// <summary>GS ( k fn 69 QR error-correction codes mapped to QRCoder's <see cref="QRCodeGenerator.ECCLevel"/>.</summary>
public sealed class QrErrorCorrectionLevel : SmartEnum<QrErrorCorrectionLevel>
{
    public static readonly QrErrorCorrectionLevel Low      = new(nameof(Low), 0, QRCodeGenerator.ECCLevel.L);
    public static readonly QrErrorCorrectionLevel Medium   = new(nameof(Medium), 1, QRCodeGenerator.ECCLevel.M);
    public static readonly QrErrorCorrectionLevel Quartile = new(nameof(Quartile), 2, QRCodeGenerator.ECCLevel.Q);
    public static readonly QrErrorCorrectionLevel High     = new(nameof(High), 3, QRCodeGenerator.ECCLevel.H);

    public QRCodeGenerator.ECCLevel Level { get; }

    private QrErrorCorrectionLevel(string name, int value, QRCodeGenerator.ECCLevel level) : base(name, value)
        => Level = level;

    /// <summary>Resolves a QR EC parameter (ASCII '0'..'3' or numeric), defaulting to Medium.</summary>
    public static QRCodeGenerator.ECCLevel FromParameter(int n)
        => (TryFromValue(EscPosParameter.Digit(n), out var level) ? level : Medium).Level;
}

using Ardalis.SmartEnum;
using CrossEscPos.Emulator.Enums;

namespace CrossEscPos.EscPos;

/// <summary>ESC a justification codes mapped to <see cref="TextJustification"/> (numeric or ASCII form).</summary>
public sealed class JustificationMode : SmartEnum<JustificationMode>
{
    public static readonly JustificationMode Left   = new(nameof(Left), 0, TextJustification.Left);
    public static readonly JustificationMode Center = new(nameof(Center), 1, TextJustification.Center);
    public static readonly JustificationMode Right  = new(nameof(Right), 2, TextJustification.Right);

    public TextJustification Justification { get; }

    private JustificationMode(string name, int value, TextJustification justification) : base(name, value)
        => Justification = justification;

    /// <summary>Resolves an ESC a parameter (numeric or ASCII digit); null if unsupported.</summary>
    public static JustificationMode? FromParameter(int n)
        => TryFromValue(EscPosParameter.Digit(n), out var mode) ? mode : null;
}

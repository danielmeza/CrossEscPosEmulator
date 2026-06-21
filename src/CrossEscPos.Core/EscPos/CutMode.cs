using Ardalis.SmartEnum;
using CrossEscPos.Emulator.Enums;

namespace CrossEscPos.EscPos;

/// <summary>
/// GS V cut modes mapped to a (<see cref="CutFunction"/>, <see cref="CutShape"/>) pair. The numeric
/// full/partial codes accept both numeric and ASCII form; the feed/position variants use letter codes.
/// </summary>
public sealed class CutMode : SmartEnum<CutMode>
{
    public static readonly CutMode FullCut            = new(nameof(FullCut), 0, CutFunction.Cut, CutShape.Full);
    public static readonly CutMode PartialCut         = new(nameof(PartialCut), 1, CutFunction.Cut, CutShape.Partial);
    public static readonly CutMode FeedFull           = new(nameof(FeedFull), 'A', CutFunction.FeedAndCut, CutShape.Full);
    public static readonly CutMode FeedPartial        = new(nameof(FeedPartial), 'B', CutFunction.FeedAndCut, CutShape.Partial);
    public static readonly CutMode SetPositionFull    = new(nameof(SetPositionFull), 'a', CutFunction.SetCutPos, CutShape.Full);
    public static readonly CutMode SetPositionPartial = new(nameof(SetPositionPartial), 'b', CutFunction.SetCutPos, CutShape.Partial);
    public static readonly CutMode FeedReverseFull    = new(nameof(FeedReverseFull), 'g', CutFunction.FeedAndCutAndReverse, CutShape.Full);
    public static readonly CutMode FeedReversePartial = new(nameof(FeedReversePartial), 'h', CutFunction.FeedAndCutAndReverse, CutShape.Partial);

    public CutFunction Function { get; }
    public CutShape Shape { get; }

    private CutMode(string name, int value, CutFunction function, CutShape shape) : base(name, value)
    {
        Function = function;
        Shape = shape;
    }

    /// <summary>Resolves a GS V parameter, defaulting to a full cut for unknown codes (prior behaviour).</summary>
    public static CutMode FromParameter(int n) => TryFromValue(EscPosParameter.Digit(n), out var mode) ? mode : FullCut;
}

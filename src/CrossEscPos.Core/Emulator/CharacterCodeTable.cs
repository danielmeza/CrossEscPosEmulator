using Ardalis.SmartEnum;

namespace CrossEscPos.Emulator;

/// <summary>
/// ESC/POS character code tables (ESC t n) and the .NET code page each maps to. The SmartEnum value is
/// the ESC/POS table id, so selecting a table is a lookup instead of a magic-number switch.
/// </summary>
public sealed class CharacterCodeTable : SmartEnum<CharacterCodeTable>
{
    public static readonly CharacterCodeTable Pc437       = new(nameof(Pc437), 0, 437);    // USA / standard Europe
    public static readonly CharacterCodeTable Katakana    = new(nameof(Katakana), 1, 932);  // approx via Shift-JIS
    public static readonly CharacterCodeTable Pc850       = new(nameof(Pc850), 2, 850);    // multilingual
    public static readonly CharacterCodeTable Pc860       = new(nameof(Pc860), 3, 860);    // Portuguese
    public static readonly CharacterCodeTable Pc863       = new(nameof(Pc863), 4, 863);    // Canadian-French
    public static readonly CharacterCodeTable Pc865       = new(nameof(Pc865), 5, 865);    // Nordic
    public static readonly CharacterCodeTable Windows1252 = new(nameof(Windows1252), 16, 1252);
    public static readonly CharacterCodeTable Pc866       = new(nameof(Pc866), 17, 866);   // Cyrillic
    public static readonly CharacterCodeTable Pc852       = new(nameof(Pc852), 18, 852);   // Latin-2
    public static readonly CharacterCodeTable Pc858       = new(nameof(Pc858), 19, 858);   // Euro

    /// <summary>The .NET code page id this table maps to.</summary>
    public int CodePage { get; }

    private CharacterCodeTable(string name, int value, int codePage) : base(name, value) => CodePage = codePage;

    /// <summary>Resolves an ESC t table id, defaulting to PC437 for unknown ids (prior behaviour).</summary>
    public static CharacterCodeTable FromTableId(int table) => TryFromValue(table, out var t) ? t : Pc437;
}

using System;
using System.Collections.Concurrent;
using System.IO;
using SkiaSharp;

namespace ReceiptPrinterEmulator.Emulator.Rendering;

/// <summary>
/// Resolves and caches <see cref="SKTypeface"/> instances used to render receipt text.
///
/// The original WPF app rendered with GDI+ using Windows-only font families (e.g. "MS Gothic"),
/// which are absent on macOS/Linux. To keep receipt output identical across platforms we bundle a
/// monospace font (JetBrains Mono, OFL) under <c>Assets/Fonts/</c> and load it directly. If the
/// embedded font is missing we fall back to a monospace family resolved from the OS.
/// </summary>
public static class FontProvider
{
    private const string FontDirectory = "Assets/Fonts";

    // Embedded JetBrains Mono weight files (copied next to the executable). Real bold/italic glyphs
    // are preferred over Skia's synthesized emphasis.
    private const string RegularFile = "receipt-mono.ttf";
    private const string BoldFile = "receipt-mono-bold.ttf";
    private const string ItalicFile = "receipt-mono-italic.ttf";
    private const string BoldItalicFile = "receipt-mono-bolditalic.ttf";

    // Fallback families tried in order when no embedded font is present.
    private static readonly string[] MonospaceFallbacks =
    {
        "Consolas", "Menlo", "DejaVu Sans Mono", "Liberation Mono", "Courier New", "monospace"
    };

    private static readonly ConcurrentDictionary<(string family, bool bold, bool italic), SKTypeface> Cache = new();

    public static SKTypeface Get(string family, bool bold, bool italic)
        => Cache.GetOrAdd((family, bold, italic), key => Resolve(key.family, key.bold, key.italic));

    private static SKTypeface Resolve(string family, bool bold, bool italic)
    {
        // 1. Embedded font weight — guarantees identical output everywhere.
        var embedded = LoadEmbedded(bold, italic);
        if (embedded is not null)
            return embedded;

        // 2. Requested family, then the monospace fallback chain.
        var style = new SKFontStyle(
            bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

        foreach (var candidate in Prepend(family, MonospaceFallbacks))
        {
            var typeface = SKTypeface.FromFamilyName(candidate, style);
            if (typeface is not null && MatchesFamily(typeface, candidate))
                return typeface;
        }

        // 3. Platform default (never null).
        return SKTypeface.FromFamilyName(family, style) ?? SKTypeface.Default;
    }

    private static SKTypeface? LoadEmbedded(bool bold, bool italic)
    {
        var file = (bold, italic) switch
        {
            (true, true) => BoldItalicFile,
            (true, false) => BoldFile,
            (false, true) => ItalicFile,
            _ => RegularFile
        };

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, FontDirectory, file);
            if (File.Exists(path))
                return SKTypeface.FromFile(path);

            // Fall back to the regular weight if a specific style file is absent.
            var regular = Path.Combine(AppContext.BaseDirectory, FontDirectory, RegularFile);
            if (File.Exists(regular))
                return SKTypeface.FromFile(regular);
        }
        catch
        {
            // Fall through to system fonts.
        }

        return null;
    }

    private static bool MatchesFamily(SKTypeface typeface, string requested)
    {
        // "monospace" is a fontconfig alias, not a real family name — accept whatever it resolves to.
        if (string.Equals(requested, "monospace", StringComparison.OrdinalIgnoreCase))
            return true;
        return string.Equals(typeface.FamilyName, requested, StringComparison.OrdinalIgnoreCase);
    }

    private static System.Collections.Generic.IEnumerable<string> Prepend(string first, string[] rest)
    {
        yield return first;
        foreach (var item in rest)
            if (!string.Equals(item, first, StringComparison.OrdinalIgnoreCase))
                yield return item;
    }
}

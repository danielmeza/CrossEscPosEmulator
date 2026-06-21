using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CrossEscPos.Graphics;
using SkiaSharp;

namespace CrossEscPos.Rendering.Skia;

/// <summary>
/// SkiaSharp <see cref="ITypefaceProvider"/>. Resolves and caches <see cref="SKTypeface"/> instances.
///
/// To keep receipt output identical across platforms (the original WPF app rendered with Windows-only
/// families like "MS Gothic"), a monospace font (JetBrains Mono, OFL) is bundled as an embedded
/// resource and loaded via <see cref="SKTypeface.FromStream(Stream, int)"/> — no file IO, so this also
/// works inside the browser sandbox. If the embedded font is unavailable, a monospace family is
/// resolved from the OS.
/// </summary>
public sealed class SkiaTypefaceProvider : ITypefaceProvider
{
    // Embedded JetBrains Mono weight files (logical resource names set in the csproj). Real bold/italic
    // glyphs are preferred over Skia's synthesized emphasis.
    private const string RegularFile = "receipt-mono.ttf";
    private const string BoldFile = "receipt-mono-bold.ttf";
    private const string ItalicFile = "receipt-mono-italic.ttf";
    private const string BoldItalicFile = "receipt-mono-bolditalic.ttf";

    // Fallback families tried in order when no embedded font is present.
    private static readonly string[] MonospaceFallbacks =
    {
        "Consolas", "Menlo", "DejaVu Sans Mono", "Liberation Mono", "Courier New", "monospace"
    };

    private static readonly Assembly ResourceAssembly = typeof(SkiaTypefaceProvider).Assembly;

    private readonly ConcurrentDictionary<(string family, bool bold, bool italic), SKTypeface> _cache = new();

    public IReceiptFont GetFont(string family, bool bold, bool italic, float sizePx)
    {
        var typeface = _cache.GetOrAdd((family, bold, italic), key => Resolve(key.family, key.bold, key.italic));
        return new SkiaReceiptFont(new SKFont(typeface, sizePx));
    }

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
            return LoadResource(file) ?? LoadResource(RegularFile); // fall back to regular weight
        }
        catch
        {
            return null; // fall through to system fonts
        }
    }

    private static SKTypeface? LoadResource(string logicalName)
    {
        using var stream = ResourceAssembly.GetManifestResourceStream(logicalName);
        return stream is null ? null : SKTypeface.FromStream(stream);
    }

    private static bool MatchesFamily(SKTypeface typeface, string requested)
    {
        // "monospace" is a fontconfig alias, not a real family name — accept whatever it resolves to.
        if (string.Equals(requested, "monospace", StringComparison.OrdinalIgnoreCase))
            return true;
        return string.Equals(typeface.FamilyName, requested, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> Prepend(string first, string[] rest)
    {
        yield return first;
        foreach (var item in rest)
            if (!string.Equals(item, first, StringComparison.OrdinalIgnoreCase))
                yield return item;
    }
}

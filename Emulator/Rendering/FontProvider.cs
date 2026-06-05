using System;
using System.Collections.Concurrent;
using System.IO;
using SkiaSharp;

namespace ReceiptPrinterEmulator.Emulator.Rendering;

/// <summary>
/// Resolves and caches <see cref="SKTypeface"/> instances used to render receipt text.
///
/// The original WPF app rendered with GDI+ using Windows-only font families (e.g. "MS Gothic"),
/// which are absent on macOS/Linux. To keep receipt output reasonably consistent across platforms
/// we resolve a monospace typeface through a fallback chain, and optionally an embedded font.
///
/// To make rendering byte-for-byte deterministic on every OS, drop a permissively-licensed
/// monospace TTF into <c>Assets/Fonts/</c> and set <see cref="EmbeddedFontResourcePath"/> /
/// load it in <see cref="LoadEmbeddedTypeface"/>; it will then take priority over system fonts.
/// </summary>
public static class FontProvider
{
    // Fallback families tried in order when a requested family cannot be matched.
    // Covers Windows / macOS / Linux monospace fonts.
    private static readonly string[] MonospaceFallbacks =
    {
        "Consolas", "Menlo", "DejaVu Sans Mono", "Liberation Mono", "Courier New", "monospace"
    };

    private static readonly ConcurrentDictionary<(string family, int weight, int slant), SKTypeface> Cache = new();

    private static readonly Lazy<SKTypeface?> EmbeddedTypeface = new(LoadEmbeddedTypeface);

    /// <summary>
    /// Returns a cached typeface for the given family and style. Falls back to an embedded font,
    /// then to a known monospace family, then to the platform default.
    /// </summary>
    public static SKTypeface Get(string family, bool bold, bool italic)
    {
        var weight = bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
        var slant = italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
        var key = (family, (int)weight, (int)slant);

        return Cache.GetOrAdd(key, _ => Resolve(family, weight, slant));
    }

    private static SKTypeface Resolve(string family, SKFontStyleWeight weight, SKFontStyleSlant slant)
    {
        var style = new SKFontStyle(weight, SKFontStyleWidth.Normal, slant);

        // 1. Embedded font (if provided) — guarantees identical output everywhere.
        // (Bold/italic of the same family are synthesized by Skia at draw time.)
        var embedded = EmbeddedTypeface.Value;
        if (embedded is not null)
            return embedded;

        // 2. Requested family, then the monospace fallback chain.
        foreach (var candidate in Prepend(family, MonospaceFallbacks))
        {
            var typeface = SKTypeface.FromFamilyName(candidate, style);
            if (typeface is not null && MatchesFamily(typeface, candidate))
                return typeface;
        }

        // 3. Platform default (never null).
        return SKTypeface.FromFamilyName(family, style) ?? SKTypeface.Default;
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

    /// <summary>
    /// Loads an embedded monospace font if one is bundled with the app. Returns null when none is
    /// present (the default today). Place a TTF next to the executable under Assets/Fonts/ and
    /// adjust this method to enable deterministic cross-platform rendering.
    /// </summary>
    private static SKTypeface? LoadEmbeddedTypeface()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "receipt-mono.ttf");
            if (File.Exists(path))
                return SKTypeface.FromFile(path);
        }
        catch
        {
            // Fall through to system fonts.
        }

        return null;
    }
}

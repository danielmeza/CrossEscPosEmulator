using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using CrossEscPos.Graphics;
using SixLabors.Fonts;

namespace CrossEscPos.Rendering.ImageSharp;

/// <summary>
/// ImageSharp/SixLabors <see cref="ITypefaceProvider"/>. Resolves and caches <see cref="Font"/> instances.
///
/// To keep receipt output identical across platforms, a monospace font (JetBrains Mono, OFL) is bundled
/// as an embedded resource and loaded into a shared <see cref="FontCollection"/> from the assembly
/// manifest — 100% managed (SixLabors.Fonts), so it also works inside the browser sandbox. If the
/// embedded font is unavailable, a monospace family is resolved from the OS with synthesized emphasis.
/// </summary>
public sealed class ImageSharpTypefaceProvider : ITypefaceProvider
{
    // Embedded JetBrains Mono weight files. Real bold/italic glyphs are preferred over synthesized emphasis.
    private const string RegularFile = "receipt-mono.ttf";
    private const string BoldFile = "receipt-mono-bold.ttf";
    private const string ItalicFile = "receipt-mono-italic.ttf";
    private const string BoldItalicFile = "receipt-mono-bolditalic.ttf";

    // Fallback families tried in order when no embedded font is present.
    private static readonly string[] MonospaceFallbacks =
    {
        "Consolas", "Menlo", "DejaVu Sans Mono", "Liberation Mono", "Courier New", "monospace"
    };

    private static readonly Assembly ResourceAssembly = typeof(ImageSharpTypefaceProvider).Assembly;

    // One shared collection holds the embedded weights.
    private static readonly FontCollection Collection = new();

    // The embedded families (loaded lazily on first use), keyed by the weight file we added.
    private static readonly ConcurrentDictionary<string, FontFamily> EmbeddedFamilies = new();
    private static readonly object EmbeddedLock = new();
    private static bool _embeddedLoaded;

    private readonly ConcurrentDictionary<(string family, bool bold, bool italic, float size), Font> _cache = new();

    public IReceiptFont GetFont(string family, bool bold, bool italic, float sizePx)
    {
        var font = _cache.GetOrAdd((family, bold, italic, sizePx),
            key => Resolve(key.family, key.bold, key.italic, key.size));
        return new ImageSharpReceiptFont(font);
    }

    private static Font Resolve(string family, bool bold, bool italic, float sizePx)
    {
        // 1. Embedded font weight — guarantees identical output everywhere.
        if (LoadEmbedded(bold, italic) is FontFamily embedded)
            return embedded.CreateFont(sizePx, FontStyle.Regular);

        // 2. Requested family, then the monospace fallback chain (synthesizing bold/italic).
        var style = (bold, italic) switch
        {
            (true, true) => FontStyle.BoldItalic,
            (true, false) => FontStyle.Bold,
            (false, true) => FontStyle.Italic,
            _ => FontStyle.Regular
        };

        foreach (var candidate in Prepend(family, MonospaceFallbacks))
        {
            if (SystemFonts.Collection.TryGet(candidate, out var sysFamily))
                return sysFamily.CreateFont(sizePx, style);
        }

        // 3. Any available system family (never returns null on a real OS).
        foreach (var any in SystemFonts.Families)
            return any.CreateFont(sizePx, style);

        throw new InvalidOperationException(
            "No fonts available: embedded resource missing and no system fonts found.");
    }

    private static FontFamily? LoadEmbedded(bool bold, bool italic)
    {
        EnsureEmbeddedLoaded();

        var file = (bold, italic) switch
        {
            (true, true) => BoldItalicFile,
            (true, false) => BoldFile,
            (false, true) => ItalicFile,
            _ => RegularFile
        };

        // Try the exact weight, then fall back to the regular weight.
        if (EmbeddedFamilies.TryGetValue(file, out var exact))
            return exact;
        if (EmbeddedFamilies.TryGetValue(RegularFile, out var regular))
            return regular;
        return null;
    }

    private static void EnsureEmbeddedLoaded()
    {
        if (_embeddedLoaded)
            return;

        lock (EmbeddedLock)
        {
            if (_embeddedLoaded)
                return;

            foreach (var file in new[] { RegularFile, BoldFile, ItalicFile, BoldItalicFile })
            {
                if (AddEmbeddedResource(file) is FontFamily family)
                    EmbeddedFamilies[file] = family;
            }

            _embeddedLoaded = true;
        }
    }

    private static FontFamily? AddEmbeddedResource(string fileName)
    {
        try
        {
            var resourceName = Array.Find(
                ResourceAssembly.GetManifestResourceNames(),
                n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
            if (resourceName is null)
                return null;

            using var stream = ResourceAssembly.GetManifestResourceStream(resourceName);
            if (stream is null)
                return null;

            return Collection.Add(stream);
        }
        catch
        {
            // Fall through to system fonts.
            return null;
        }
    }

    private static IEnumerable<string> Prepend(string first, string[] rest)
    {
        yield return first;
        foreach (var item in rest)
            if (!string.Equals(item, first, StringComparison.OrdinalIgnoreCase))
                yield return item;
    }
}

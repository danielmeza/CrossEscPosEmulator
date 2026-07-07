using System;
using CrossEscPos.Graphics;
using CrossEscPos.Rendering.ImageSharp;
using CrossEscPos.Rendering.Skia;

namespace CrossEscPos.App;

/// <summary>
/// Selects the render backend at startup so the two rendering libraries can be A/B tested visually.
///
/// Choose with a command-line argument — <c>--backend imagesharp</c> (or <c>--backend=imagesharp</c>,
/// or <c>-b imagesharp</c>) — or the <c>ESCPOS_RENDER_BACKEND</c> environment variable. Accepted values
/// are <c>skia</c> (default) and <c>imagesharp</c>. Anything unrecognised falls back to Skia.
/// </summary>
public sealed record RenderBackend(
    string Name,
    IReceiptImageFactory ImageFactory,
    ITypefaceProvider Typefaces,
    IImageEncoder Encoder)
{
    public static RenderBackend Select(string[] args) => Resolve(args) switch
    {
        "imagesharp" => new RenderBackend(
            "ImageSharp (managed)",
            new ImageSharpImageFactory(),
            new ImageSharpTypefaceProvider(),
            new ImageSharpImageEncoder()),
        _ => new RenderBackend(
            "SkiaSharp",
            new SkiaImageFactory(),
            new SkiaTypefaceProvider(),
            new SkiaImageEncoder()),
    };

    // CLI wins over the environment variable; both are normalised the same way.
    private static string Resolve(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if ((a.Equals("--backend", StringComparison.OrdinalIgnoreCase) || a.Equals("-b", StringComparison.OrdinalIgnoreCase))
                && i + 1 < args.Length)
                return Normalize(args[i + 1]);
            if (a.StartsWith("--backend=", StringComparison.OrdinalIgnoreCase))
                return Normalize(a["--backend=".Length..]);
        }

        return Normalize(Environment.GetEnvironmentVariable("ESCPOS_RENDER_BACKEND"));
    }

    private static string Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "imagesharp" or "managed" or "is" => "imagesharp",
        _ => "skia",
    };
}

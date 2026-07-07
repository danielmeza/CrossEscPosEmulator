using System;
using System.Collections.Generic;
using System.Linq;
using CrossEscPos.Graphics;
using CrossEscPos.Rendering.ImageSharp;
using CrossEscPos.Rendering.Skia;

namespace CrossEscPos.Web.Rendering;

/// <summary>
/// A named render backend — the small registry that lets the app resolve a rendering engine by id and
/// swap it at runtime. Each backend bundles the three abstractions the emulator needs
/// (<see cref="IReceiptImageFactory"/>, <see cref="ITypefaceProvider"/>, <see cref="IImageEncoder"/>)
/// behind lazy factories, so nothing is constructed until an engine is actually selected.
/// </summary>
public sealed record RenderBackend(
    string Id,
    string Name,
    string Blurb,
    Func<IReceiptImageFactory> CreateImageFactory,
    Func<ITypefaceProvider> CreateTypefaces,
    Func<IImageEncoder> CreateEncoder)
{
    /// <summary>100% managed — loads like any assembly, no native relink. The default in the browser.</summary>
    public static readonly RenderBackend ImageSharp = new(
        "imagesharp",
        "ImageSharp (managed)",
        "100% managed — no native dependency, no wasm relink. The lightweight browser default.",
        () => new ImageSharpImageFactory(),
        () => new ImageSharpTypefaceProvider(),
        () => new ImageSharpImageEncoder());

    /// <summary>Native SkiaSharp linked into the wasm runtime — heavier download, the desktop default engine.</summary>
    public static readonly RenderBackend Skia = new(
        "skia",
        "SkiaSharp (native)",
        "Native libSkiaSharp linked into the wasm runtime (larger download). The desktop default engine.",
        () => new SkiaImageFactory(),
        () => new SkiaTypefaceProvider(),
        () => new SkiaImageEncoder());

    public static readonly IReadOnlyList<RenderBackend> All = new[] { ImageSharp, Skia };

    public static readonly RenderBackend Default = ImageSharp;

    public static RenderBackend ById(string? id) =>
        All.FirstOrDefault(b => string.Equals(b.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Default;
}

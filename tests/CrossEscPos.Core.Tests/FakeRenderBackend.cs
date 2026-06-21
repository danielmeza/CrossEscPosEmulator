using System;
using System.Collections.Generic;
using CrossEscPos.Graphics;

namespace CrossEscPos.Core.Tests;

/// <summary>
/// A pure-managed render backend with no graphics library behind it. Its existence is the point: the
/// emulator core renders against the abstraction only, so it runs with a completely synthetic backend
/// — proof that the render layer is swappable and that Core is truly headless (no SkiaSharp at all).
/// It also records primitive draw calls so tests can assert that drawing actually happened.
/// </summary>
public sealed class FakeImageFactory : IReceiptImageFactory
{
    public List<FakeCanvas> Canvases { get; } = new();

    public IReceiptImage Create(int width, int height, ReceiptColor fill) => new FakeImage(width, height);

    public IReceiptImage FromPixels(int width, int height, ReceiptColor[] rowMajorPixels) => new FakeImage(width, height);

    public IReceiptCanvas CreateCanvas(IReceiptImage image)
    {
        var canvas = new FakeCanvas((FakeImage)image);
        Canvases.Add(canvas);
        return canvas;
    }
}

public sealed class FakeImage : IReceiptImage
{
    public FakeImage(int width, int height) { Width = width; Height = height; }
    public int Width { get; }
    public int Height { get; }
    public IReceiptImage Copy() => new FakeImage(Width, Height);
    public void Dispose() { }
}

public sealed class FakeCanvas : IReceiptCanvas
{
    public FakeCanvas(FakeImage image) => Image = image;
    public FakeImage Image { get; }
    public int TextDraws => DrawnText.Count;
    public int RectDraws { get; private set; }
    public int ImageDraws { get; private set; }

    /// <summary>The text strings drawn, in order.</summary>
    public List<string> DrawnText { get; } = new();

    /// <summary>The (sx, sy) of every Scale applied (character magnification shows up here).</summary>
    public List<(float sx, float sy)> Scales { get; } = new();

    /// <summary>Every Translate applied — text lines translate to their justified x before drawing.</summary>
    public List<(float dx, float dy)> Translates { get; } = new();

    private int _saveDepth;

    public void Clear(ReceiptColor color) { }

    public void DrawText(string text, float x, float baselineY, IReceiptFont font, ReceiptColor color)
        => DrawnText.Add(text);

    public void DrawRect(ReceiptRect rect, ReceiptColor color) => RectDraws++;
    public void DrawLine(float x0, float y0, float x1, float y1, ReceiptColor color, float strokeWidth) { }
    public void DrawImage(IReceiptImage image, float x, float y) => ImageDraws++;
    public void DrawImage(IReceiptImage image, ReceiptRect dest) => ImageDraws++;
    public int Save() => ++_saveDepth;
    public void Translate(float dx, float dy) => Translates.Add((dx, dy));
    public void Scale(float sx, float sy) => Scales.Add((sx, sy));
    public void RestoreToCount(int count) => _saveDepth = count;
    public void Flush() { }
    public void Dispose() { }
}

/// <summary>
/// A typeface provider with fixed, deterministic metrics — no font files, no SkiaSharp. Records every
/// font request so tests can assert that bold/italic styles were applied.
/// </summary>
public sealed class FakeTypefaceProvider : ITypefaceProvider
{
    public List<(string family, bool bold, bool italic, float size)> Requests { get; } = new();

    public IReceiptFont GetFont(string family, bool bold, bool italic, float sizePx)
    {
        Requests.Add((family, bold, italic, sizePx));
        return new FakeFont(sizePx);
    }
}

public sealed class FakeFont : IReceiptFont
{
    public FakeFont(float size) => Size = size;
    public float Size { get; }
    public FontMetrics Metrics => new(-Size * 0.8f, Size * 0.2f);
    public float MeasureText(string text) => text.Length * (Size * 0.5f);
    public void Dispose() { }
}

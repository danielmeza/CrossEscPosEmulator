# Adding a Render Backend

A **render backend** is what turns the receipt document model into pixels. `CrossEscPos.Core` only ever
talks to the five interfaces in `CrossEscPos.Graphics`, so you can render with anything —
System.Drawing, a GPU canvas, an HTML5 `<canvas>`, a headless measuring stub — by implementing them and
injecting your types. No change to `Core`.

> ### 📌 The worked sample: `CrossEscPos.Rendering.ImageSharp`
> The managed **ImageSharp backend** was contributed as a complete, class-for-class mirror of the Skia
> one and is the reference example for this guide. Read it alongside these steps:
> - **[PR #11 — Add a managed ImageSharp render backend](https://github.com/danielmeza/CrossEscPosEmulator/pull/11)** by [@yhonc9](https://github.com/yhonc9)
> - Sources: [`src/CrossEscPos.Rendering.ImageSharp/`](https://github.com/danielmeza/CrossEscPosEmulator/tree/main/src/CrossEscPos.Rendering.ImageSharp)
> - The existing Skia backend to diff against: [`src/CrossEscPos.Rendering.Skia/`](https://github.com/danielmeza/CrossEscPosEmulator/tree/main/src/CrossEscPos.Rendering.Skia)

## What you implement

| Interface | You build | Responsibility |
| --- | --- | --- |
| `IReceiptImageFactory` | `MyImageFactory` | create blank images, build images from raw pixels, create a canvas over an image |
| `IReceiptImage` | `MyReceiptImage` | a rasterized image — `Width`, `Height`, `Copy()`, `Dispose()` |
| `IReceiptCanvas` | `MyReceiptCanvas` | draw text / rects / lines / images; the transform stack |
| `ITypefaceProvider` + `IReceiptFont` | `MyTypefaceProvider`, `MyReceiptFont` | resolve & cache fonts; measure text + metrics |
| `IImageEncoder` | `MyImageEncoder` | encode an `IReceiptImage` to PNG |

Then inject the triple:

```csharp
var printer = new ReceiptPrinter(
    PaperConfiguration.Default,
    new MyImageFactory(),       // IReceiptImageFactory
    new MyTypefaceProvider());  // ITypefaceProvider
var encoder = new MyImageEncoder();
```

## Step 1 — the image (`IReceiptImage`)

Wrap your library's bitmap type. `Copy()` must return an **independent** copy (the caller owns its
lifetime); `Dispose()` frees the underlying bitmap.

```csharp
public sealed class MyReceiptImage : IReceiptImage
{
    internal Image<Rgba32> Image { get; }
    public MyReceiptImage(Image<Rgba32> image) => Image = image;

    public int Width  => Image.Width;
    public int Height => Image.Height;
    public IReceiptImage Copy() => new MyReceiptImage(Image.Clone());
    public void Dispose() => Image.Dispose();
}
```

## Step 2 — the factory (`IReceiptImageFactory`)

Three jobs: a blank filled image, an image built from **row-major** pixels (used by the raster /
bit-image ESC/POS commands), and a canvas over an image.

```csharp
public sealed class MyImageFactory : IReceiptImageFactory
{
    public IReceiptImage Create(int width, int height, ReceiptColor fill) => /* new filled bitmap */;

    public IReceiptImage FromPixels(int width, int height, ReceiptColor[] rowMajorPixels)
    {
        // pixel i sits at (i % width, i / width)
    }

    public IReceiptCanvas CreateCanvas(IReceiptImage image) => new MyReceiptCanvas((MyReceiptImage)image);
}
```

> Guard tiny sizes (`Math.Max(1, width)`) — some paths create 0-height images.

## Step 3 — the canvas (`IReceiptCanvas`)

The workhorse. It draws primitives **and** maintains a transform stack.

```csharp
public interface IReceiptCanvas : IDisposable
{
    void Clear(ReceiptColor color);
    void DrawText(string text, float x, float baselineY, IReceiptFont font, ReceiptColor color);
    void DrawRect(ReceiptRect rect, ReceiptColor color);
    void DrawLine(float x0, float y0, float x1, float y1, ReceiptColor color, float strokeWidth);
    void DrawImage(IReceiptImage image, float x, float y);
    void DrawImage(IReceiptImage image, ReceiptRect dest);
    int  Save();
    void Translate(float dx, float dy);
    void Scale(float sx, float sy);
    void RestoreToCount(int count);
    void Flush();
}
```

Three contracts to get right — they are where a naïve port diverges from Skia:

### (a) Anti-aliasing is per primitive

- **text & lines** → anti-aliased (smooth glyphs / underlines)
- **filled rects** → **not** anti-aliased (crisp, scannable barcode / QR modules)
- **scaled image draws** → sampled

Skia does this with `SKPaint.IsAntialias`; the ImageSharp backend sets `GraphicsOptions.Antialias =
false` for `DrawRect`. Get this wrong and barcodes render fuzzy and unscannable.

### (b) The transform stack

`DrawText`/`DrawLine` for scaled (double-width/height) text run **inside** a `Save → Translate → Scale →
draw → RestoreToCount` block. If your library has no canvas save/restore (ImageSharp doesn't), keep a
`Matrix3x2` stack yourself and apply it per draw op — **translate outermost, scale inner**, matching
Skia:

```csharp
private Matrix3x2 _current = Matrix3x2.Identity;
private readonly List<Matrix3x2> _stack = new();

public int Save() { _stack.Add(_current); return _stack.Count; }
public void Translate(float dx, float dy) => _current = Matrix3x2.CreateTranslation(dx, dy) * _current;
public void Scale(float sx, float sy)     => _current = Matrix3x2.CreateScale(sx, sy) * _current;
public void RestoreToCount(int count)
{
    while (_stack.Count >= count && _stack.Count > 0)
    {
        _current = _stack[^1];
        _stack.RemoveAt(_stack.Count - 1);
    }
}
```

`Save()` returns a token; the same value passed to `RestoreToCount` unwinds to that depth. Draw ops must
apply `_current` (e.g. ImageSharp's `SetDrawingTransform` / `DrawingOptions.Transform`).

### (c) Baseline vs top-left text

`DrawText` positions the **baseline** at `(x, baselineY)` (Skia's convention). If your text API draws
from the top-left, convert: `top = baselineY - ascenderPx`, where `ascenderPx = -font.Metrics.Ascent`.

## Step 4 — fonts (`ITypefaceProvider` + `IReceiptFont`)

Resolve a font by family/bold/italic/size and **cache** it. To keep output identical across platforms,
embed a monospace font (JetBrains Mono, OFL) as a manifest resource and load it from memory — this also
works in the browser sandbox (no file IO). Fall back to an OS monospace family only if the embedded
resource is missing.

Two measurement contracts are easy to get wrong:

```csharp
public FontMetrics Metrics { get; }          // Skia sign convention: Ascent NEGATIVE, Descent POSITIVE
public float Size { get; }
public float MeasureText(string text);        // ADVANCE width — not glyph bounds
```

- **`MeasureText` must return the advance width** (like `SKFont.MeasureText`): the horizontal space the
  text occupies for layout, **including side bearings and trailing whitespace**. The receipt layout uses
  it for justification, multi-run advance, and underline length. ImageSharp's `TextMeasurer.MeasureSize`
  returns glyph *bounds* (too small — drops side bearings and trailing spaces); `MeasureAdvance` returns
  the advance but still trims trailing whitespace, so the backend re-adds it with a sentinel character.
  *(This was the one bug found reviewing PR #11 — see the fix in
  [`ImageSharpReceiptFont.cs`](https://github.com/danielmeza/CrossEscPosEmulator/blob/main/src/CrossEscPos.Rendering.ImageSharp/ImageSharpReceiptFont.cs).)*
- **`FontMetrics.Ascent` is negative, `Descent` positive** (distance above / below the baseline).

## Step 5 — the encoder (`IImageEncoder`)

```csharp
public sealed class MyImageEncoder : IImageEncoder
{
    public void EncodePng(IReceiptImage image, Stream destination) => /* SaveAsPng */;
    public byte[] EncodePng(IReceiptImage image)
    {
        using var ms = new MemoryStream();
        EncodePng(image, ms);
        return ms.ToArray();
    }
}
```

## Step 6 — verify against Skia

Because both backends implement the same contract, you can assert **parity**: feed the same ESC/POS
through a Skia printer and your printer and compare. Key checks (all confirmed for the ImageSharp
backend):

- Same paper width and **same rendered height** for a multi-style ticket (⇒ metrics match).
- `MeasureText` returns the **same advance** as Skia across samples (including trailing whitespace).
- Double-size text renders at ~2× (⇒ the transform stack works).
- Barcode modules stay crisp (⇒ `DrawRect` AA is off).

Add an end-to-end test mirroring
[`SkiaRenderTests`](https://github.com/danielmeza/CrossEscPosEmulator/blob/main/tests/CrossEscPos.Core.Tests)
(the ImageSharp backend added `ImageSharpRenderTests` the same way).

## Packaging

- Target the repo's shared settings (`Directory.Build.props`): `net10.0`, nullable on,
  `TreatWarningsAsErrors`, `NuGetAuditMode=all` — so pin any transitive package that trips a security
  advisory (the ImageSharp backend pins `SixLabors.ImageSharp` to a patched version for exactly this).
- Embed fonts with an explicit `LogicalName` so the manifest resource names stay stable.
- Set `IsPackable=true` and add it to the `Release` workflow's pack list if you want it on NuGet.

That's the whole contract. If Skia and your backend produce the same receipts, you're done.

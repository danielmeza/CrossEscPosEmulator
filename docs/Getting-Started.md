# Getting Started

The smallest useful setup: turn a stream of ESC/POS bytes into a PNG, with no UI.

## 1. Reference the packages

```sh
dotnet add package CrossEscPos.Core
dotnet add package CrossEscPos.Rendering.Skia
```

Prefer a **fully managed** backend (no native dependency — needed for the browser (WASM) without a native
relink)? Swap the render package for `CrossEscPos.Rendering.ImageSharp`; everything below is identical
apart from the `Skia*` type names becoming `ImageSharp*`.

## 2. Compose a printer and render

```csharp
using CrossEscPos.Emulator;       // ReceiptPrinter, PaperConfiguration
using CrossEscPos.Rendering.Skia; // SkiaImageFactory, SkiaTypefaceProvider, SkiaImageEncoder

// The composition root: pick a render backend and inject it.
var imageFactory = new SkiaImageFactory();
var typefaces    = new SkiaTypefaceProvider();
var encoder      = new SkiaImageEncoder();

var printer = new ReceiptPrinter(PaperConfiguration.Default, imageFactory, typefaces);

// ESC/POS is binary — feed the raw bytes.
byte[] escpos = File.ReadAllBytes("ticket.escpos");
printer.FeedEscPos(escpos);

// Each "cut" produces a Receipt; render the current one to an image and encode it.
using var image = printer.CurrentReceipt.Render();   // IReceiptImage
using var output = File.Create("ticket.png");
encoder.EncodePng(image, output);
```

That's the entire headless pipeline. No Avalonia, no window, no UI thread — `CrossEscPos.Core` has no
dependency on a graphics framework; the render backend is what you plugged in.

### The same thing with the managed backend

```csharp
using CrossEscPos.Rendering.ImageSharp;

var printer = new ReceiptPrinter(
    PaperConfiguration.Default,
    new ImageSharpImageFactory(),
    new ImageSharpTypefaceProvider());

printer.FeedEscPos(escpos);
byte[] png = new ImageSharpImageEncoder().EncodePng(printer.CurrentReceipt.Render());
```

## 3. Multiple receipts (cuts)

A `GS V` / `ESC i` / `ESC m` cut starts a new receipt. They accumulate in `printer.ReceiptStack`:

```csharp
foreach (var receipt in printer.ReceiptStack)
{
    if (receipt.IsEmpty) continue;
    using var img = receipt.Render();
    // …encode each, or stack them into one image — see Rendering & Backends.
}
```

## Where to go next

- **[Core Emulator](Core-Emulator.md)** — printer state, status replies, events, code pages.
- **[Rendering & Backends](Rendering-and-Backends.md)** — exporting, stacking, and the two backends.
- **[Controls](Controls.md)** — show receipts live in an Avalonia app.
- **[Transports](Transports.md)** — feed the printer over TCP / serial / USB instead of a file.
- **[Browser App](Browser-App.md)** — the same pipeline, in the browser.

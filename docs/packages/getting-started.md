# Getting started

The smallest useful setup: turn a stream of ESC/POS bytes into a PNG, with no UI.

## 1. Reference the packages

```sh
dotnet add package CrossEscPos.Core
dotnet add package CrossEscPos.Rendering.Skia
```

(See the [package index](README.md#installing) for using local builds until these are on NuGet.)

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
dependency on a graphics framework; `CrossEscPos.Rendering.Skia` is the backend you plugged in.

## 3. Multiple receipts (cuts)

A `GS V` / `ESC i` / `ESC m` cut starts a new receipt. They accumulate in `printer.ReceiptStack`:

```csharp
foreach (var receipt in printer.ReceiptStack)
{
    if (receipt.IsEmpty) continue;
    using var img = receipt.Render();
    // …encode each, or stack them — see Rendering.
}
```

## Where to go next

- [Core](core.md) — printer state, status replies, events, code pages.
- [Rendering](rendering.md) — exporting, stacking, and writing a custom backend.
- [Controls](controls.md) — show receipts live in an Avalonia app.
- [Transports](transports.md) — feed the printer over TCP/serial/USB instead of a file.

# Architecture

How the code is split into packages, how the desktop and browser apps share one codebase, and the bundled receipt font.

## Packages

The emulator is split into layered, independently-publishable packages so the **rendering backend is
swappable** and the **core runs headless** (and in the browser). The namespace is unified under
`CrossEscPos.*` (organized by feature/directory, not by package), so types resolve across assemblies.

| Package | Namespace(s) | Role | Depends on |
| --- | --- | --- | --- |
| `CrossEscPos.Abstractions` | `CrossEscPos`, `CrossEscPos.Graphics` | Backend-agnostic rendering + printer contracts (`IReceiptCanvas`, `IReceiptImage`, `IReceiptImageFactory`, `ITypefaceProvider`, `IImageEncoder`, `IReceiptPrintable`, `IPrinterResponder`) | — |
| `CrossEscPos.Core` | `CrossEscPos.Emulator`, `CrossEscPos.EscPos`, … | Headless ESC/POS interpreter, printer state machine, receipt document model, barcode/QR generation. ESC/POS code maps (2D families, code tables, barcode systems, status requests) are behaviour-carrying `SmartEnum`s, not magic-number switches | Abstractions, QRCoder, ZXing.Net, Ardalis.SmartEnum |
| `CrossEscPos.Rendering.Skia` | `CrossEscPos.Rendering.Skia` | The default **render backend** (SkiaSharp). Swap it for another `IReceiptImageFactory`/`ITypefaceProvider`/`IImageEncoder` | Abstractions, SkiaSharp |
| `CrossEscPos.Rendering.ImageSharp` | `CrossEscPos.Rendering.ImageSharp` | A **100% managed render backend** (ImageSharp) — no native dependency, so it runs in the browser (WebAssembly) without a native relink. Same output as the Skia backend | Abstractions, SixLabors.ImageSharp.Drawing |
| `CrossEscPos.Transports` | `CrossEscPos.Transports` | TCP / serial / USB transports (desktop only) | Core, System.IO.Ports, LibUsbDotNet, ESC-POS-.NET |
| `CrossEscPos.Controls` | `CrossEscPos.Controls` | Reusable Avalonia controls (`ReceiptView`, `PrinterStatePanel`) — host apps consume these. **Backend-agnostic** (no SkiaSharp dependency) | Core, Avalonia |

`Core` carries **no UI and no graphics-backend dependency**, so the library works headless or in WASM.
The host (desktop, browser, or your own app) is the composition root: it picks a backend and injects it.

```csharp
var imageFactory = new SkiaImageFactory();
var typefaces    = new SkiaTypefaceProvider();
var printer      = new ReceiptPrinter(PaperConfiguration.Default, imageFactory, typefaces);
printer.FeedEscPos(escPosBytes);                         // byte[] — ESC/POS is binary
using var image  = printer.CurrentReceipt.Render();      // IReceiptImage
new SkiaImageEncoder().EncodePng(image, outputStream);
```

To use the packages in your own app, start with [Getting started](Getting-Started.md) and
[Packages](Packages.md). To write your own render backend, follow
[Adding a render backend](Adding-a-Render-Backend.md).

## One app, two heads

The desktop and browser apps are the **same** Avalonia application
([`src/CrossEscPos.App`](../src/CrossEscPos.App)) — the same views, view models, receipts, printer-state
panel, PNG export, and the Monitor test-client. Only the platform edges differ, injected via
`IPlatformServices`:

| | Desktop head | Browser head |
|---|---|---|
| Transports | TCP + serial | **Web Serial + WebUSB + SignalR TCP proxy** |
| Export | native save dialog | browser **download** (same Avalonia storage API) |
| Monitor | test-client **window** | in-page **overlay** (SignalR round-trip) |

A browser can't open a raw **TCP** listen socket, so the browser head connects over **SignalR** to
[`samples/CrossEscPos.Host`](../samples/CrossEscPos.Host) — one ASP.NET Core host that serves the WASM app
*and* runs the broker. The emulator asks the host to open a TCP listener on the address:port you choose;
POS software connects there and its jobs bridge to the in-page emulator, with status flowing back. The
same hub carries the browser Monitor's jobs, so it round-trips against the in-page emulator too.

## Fonts and license

Receipt text is rendered with **[JetBrains Mono](https://www.jetbrains.com/lp/mono/)**, embedded in the
`CrossEscPos.Rendering.Skia` package (under
[`src/CrossEscPos.Rendering.Skia/Assets/Fonts/`](../src/CrossEscPos.Rendering.Skia/Assets/Fonts)) so output
is identical across platforms and works in the browser sandbox (no file IO). JetBrains Mono is licensed
under the **SIL Open Font License 1.1**; the full license text is included at
[`src/CrossEscPos.Rendering.Skia/Assets/Fonts/OFL.txt`](../src/CrossEscPos.Rendering.Skia/Assets/Fonts/OFL.txt). Per the OFL, the font is redistributed here under its
original license and "JetBrains Mono" is a trademark of JetBrains s.r.o. To swap in a different
monospace font, replace the `receipt-mono*.ttf` files (and keep its license alongside).

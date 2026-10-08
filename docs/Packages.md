# Packages

CrossEscPos is split into layered, independently-usable packages so you can take only what you need —
the headless emulator, a render backend, the transports, and/or the Avalonia controls.

| Package | What it gives you | Depends on |
| --- | --- | --- |
| **CrossEscPos.Abstractions** | Backend-agnostic contracts: `IReceiptCanvas`, `IReceiptImage`, `IReceiptImageFactory`, `ITypefaceProvider`, `IImageEncoder`, `IReceiptPrintable`, `IPrinterResponder` | — |
| **CrossEscPos.Core** | The headless ESC/POS emulator: `ReceiptPrinter`, the interpreter, the receipt document model, barcode/QR | Abstractions |
| **CrossEscPos.Rendering.Skia** | The default render backend (SkiaSharp, native); fonts embedded | Abstractions |
| **CrossEscPos.Rendering.ImageSharp** | A 100% managed render backend (ImageSharp) — no native dependency, runs in the browser (WASM) with no native relink; byte-compatible output with Skia | Abstractions |
| **CrossEscPos.Transports** | TCP / serial / USB transports that feed a printer (desktop) | Core |
| **CrossEscPos.Controls** | Reusable Avalonia controls: `ReceiptView`, `PrinterStatePanel` | Core, Avalonia |

Everything lives under the single `CrossEscPos.*` namespace root (organized by feature, e.g.
`CrossEscPos.Emulator`, `CrossEscPos.Graphics`, `CrossEscPos.Controls`), so types resolve across the
packages regardless of which one they ship in.

## How the packages depend on each other

```mermaid
flowchart BT
    Abstractions["CrossEscPos.Abstractions"]
    Core["CrossEscPos.Core"] --> Abstractions
    Skia["CrossEscPos.Rendering.Skia"] --> Abstractions
    ImageSharp["CrossEscPos.Rendering.ImageSharp"] --> Abstractions
    Transports["CrossEscPos.Transports"] --> Core
    Controls["CrossEscPos.Controls"] --> Core
    Controls --> Abstractions

    Host(["your host app"]) -.-> Core
    Host -.->|"pick one backend"| Skia
    Host -.->|"…or"| ImageSharp
    Host -.-> Controls
    Host -.-> Transports
```

## Breaking changes

### `CrossEscPos.Transports` drops ESCPOS_NET

`UsbPrinter` no longer derives from `ESCPOS_NET.BasePrinter`. It derives from `StreamPrinter`, this
package's own host-side printer, whose one-method write surface is `IPrinterResponder.Send` — the
same interface `NetClient` and `SerialServer` implement, though note those two are the *printer*
answering a host, while `StreamPrinter` is the host driving a printer. The `ESCPOS_NET` dependency is
gone, and with it the `SixLabors.ImageSharp` reference that existed only to lift ESCPOS_NET's
transitive 2.1.3 — the package now depends on `CrossEscPos.Core`, `LibUsbDotNet` and
`System.IO.Ports`, and nothing else.

What this changes for a consumer holding a `UsbPrinter`:

| Was, on `BasePrinter` | Is now |
| --- | --- |
| `Write(byte[])`, `Write(byte[][])` | `Send(byte[])` — the `IPrinterResponder` method |
| `event EventHandler StatusChanged` with a parsed `PrinterStatusEventArgs` | `event Action<byte[]> StatusFrameReceived` with the raw 4-byte Automatic Status Back block; decode it with `CrossEscPos.Emulator.AutoStatusBackReader.Parse` |
| `PrinterName` | `Name` — same value, still `$"USB {vid:X4}:{pid:X4}"` |
| `Status`, `GetStatus()`, `Flush(…)`, `Connected` / `Disconnected` | not replaced — see below |
| writes queued on a background pump, failures swallowed | `Send` writes synchronously and throws on failure |
| `protected override void OverridableDispose()` | ordinary `IDisposable`. `UsbPrinter` is `sealed` (it already was), so this only concerns a `StreamPrinter` subclass, which overrides `Dispose(bool)` |

Three status flags ESCPOS_NET parsed are not surfaced: *paper currently feeding*, *waiting for
online recovery* and *feed button pushed*. Nothing in this repository read them, and the bits are
still in the block `StatusFrameReceived` hands over, so a consumer that needs them can read them.
`GetStatus()` threw `NotImplementedException` in ESCPOS_NET and has no replacement. `Connected` and
`Disconnected` never fired for USB, because `BasePrinter` only raised them from the network
printer's reconnect logic.

Two decode details were kept deliberately, because ESCPOS_NET had them and a real printer can
exercise them: a block whose first byte lacks the ASB fixed bits is rejected rather than decoded,
and the error flag covers all four byte-1 error bits (recoverable, unrecoverable, autocutter and
recoverable non-autocutter), not just the two the emulator itself emits.

Why: ESCPOS_NET 3.0.0 is its last release (2022-08-18) and upstream's last commit is 2024-09-18. It
pinned `SixLabors.ImageSharp` 2.x, which no longer has a release without published advisories, and
its `PrintImage` has been broken since ImageSharp 3.x. `UsbPrinter` already owned the parts that
touch the device — it built its own stream, reader and writer, and overrode disposal — so what the
dependency actually supplied was the generic stream pump above it: the write queue, the 15,000-byte
chunking, the flush policy, the read loop, the 4-byte framing and the bit decode. That is what
`StreamPrinter` and `AutoStatusBackReader` now do, in about 150 lines, without setting the whole
package's dependency floor.

## Choosing a render backend

| | `Rendering.Skia` | `Rendering.ImageSharp` |
| --- | --- | --- |
| Implementation | native `libSkiaSharp` | 100% managed (SixLabors.ImageSharp) |
| Browser (WASM) | needs `wasm-tools` + native relink (~23 MB runtime) | **just works** — no native relink |
| Speed | faster (native raster) | fine for receipt rendering |
| Output | reference | byte-compatible with Skia |
| Default in | desktop app, server | browser app |

Both embed JetBrains Mono and produce the same receipts. See **[Rendering & Backends](Rendering-and-Backends.md)**.

## Install

The packages are on NuGet, published by the `Release` workflow on each `v*` tag:

```sh
dotnet add package CrossEscPos.Core
dotnet add package CrossEscPos.Rendering.Skia
# or the managed backend:
dotnet add package CrossEscPos.Rendering.ImageSharp
# …and Controls / Transports as needed
```

Prefer local builds? `dotnet pack -c Release` emits the `.nupkg` files under each project's
`bin/Release`, or reference the projects directly with `dotnet add reference`.

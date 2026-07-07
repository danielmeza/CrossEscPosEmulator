# Using the CrossEscPos packages

CrossEscPos is split into layered, independently-usable packages so you can take only what you need —
the headless emulator, a render backend, the transports, and/or the Avalonia controls.

| Package | What it gives you | Depends on |
| --- | --- | --- |
| **CrossEscPos.Abstractions** | Backend-agnostic contracts: `IReceiptCanvas`, `IReceiptImage`, `IReceiptImageFactory`, `ITypefaceProvider`, `IImageEncoder`, `IReceiptPrintable`, `IPrinterResponder` | — |
| **CrossEscPos.Core** | The headless ESC/POS emulator: `ReceiptPrinter`, the interpreter, the receipt document model, barcode/QR | Abstractions |
| **CrossEscPos.Rendering.Skia** | The default render backend (SkiaSharp); fonts embedded | Abstractions |
| **CrossEscPos.Rendering.ImageSharp** | A 100% managed render backend (ImageSharp) — no native dependency, so it runs in Blazor WASM with no native relink. Byte-compatible output with Skia | Abstractions |
| **CrossEscPos.Transports** | TCP / serial / USB transports that feed a printer | Core |
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

    %% A host (your app) composes a backend + core + controls/transports
    Host(["your host app"]) -.-> Core
    Host -.->|"pick one backend"| Skia
    Host -.->|"…or"| ImageSharp
    Host -.-> Controls
    Host -.-> Transports
```

## The mental model

```mermaid
flowchart TD
    bytes["ESC/POS bytes"] --> printer["ReceiptPrinter<br/>(Core · headless)"]
    printer --> stack["ReceiptStack<br/>one Receipt per cut"]
    stack -->|"Render()"| image["IReceiptImage"]
    image --> encoder["IImageEncoder"]
    encoder --> png["PNG bytes<br/>(or an Avalonia Bitmap in a host)"]

    backend["render backend<br/>Rendering.Skia, Rendering.ImageSharp, or your own"]
    backend -. "IReceiptImageFactory + ITypefaceProvider" .-> printer
```

`Core` knows **nothing** about SkiaSharp or Avalonia. You — the host — pick a render backend and inject
it. That's the whole design: the emulation is portable (headless, server, WASM); rendering is swappable.

## Guides

- **[Getting started](getting-started.md)** — install and render your first ticket headless.
- **[Core](core.md)** — feeding ESC/POS, receipts, printer state, status responses, events.
- **[Rendering](rendering.md)** — the Skia and ImageSharp backends, exporting PNGs, and writing your own.
- **[Controls](controls.md)** — hosting `ReceiptView` and `PrinterStatePanel` in an Avalonia app.
- **[Transports](transports.md)** — driving the emulator over TCP, serial, or USB.
- **[Blazor web app](web.md)** — render ESC/POS in the browser with the managed backend (Blazor WASM).

> 📚 For the full reference — including a step-by-step **[Adding a render backend](https://github.com/danielmeza/CrossEscPosEmulator/wiki/Adding-a-Render-Backend)** guide — see the [project wiki](https://github.com/danielmeza/CrossEscPosEmulator/wiki).

## Installing

The packages are on NuGet (published by the [`Release`](../../.github/workflows/release.yml) workflow on each `v*` tag):

```sh
dotnet add package CrossEscPos.Core
dotnet add package CrossEscPos.Rendering.Skia       # native SkiaSharp backend (desktop default)
# or the managed, WASM-safe backend:
dotnet add package CrossEscPos.Rendering.ImageSharp
# …and Controls / Transports as needed
```

Prefer local builds? Pack them or reference the projects directly:

```sh
dotnet pack -c Release          # emits the .nupkg files under each project's bin/Release
# or
dotnet add reference ../CrossEscPosEmulator/src/CrossEscPos.Core/CrossEscPos.Core.csproj
```

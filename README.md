# CrossEscPos: ESC/POS receipt printer emulator

[![CI](https://github.com/danielmeza/CrossEscPosEmulator/actions/workflows/ci.yml/badge.svg)](https://github.com/danielmeza/CrossEscPosEmulator/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/danielmeza/CrossEscPosEmulator)](https://github.com/danielmeza/CrossEscPosEmulator/releases/latest)
[![NuGet](https://img.shields.io/nuget/v/CrossEscPos.Core?label=NuGet)](https://www.nuget.org/packages/CrossEscPos.Core)
[![License: MIT](https://img.shields.io/github/license/danielmeza/CrossEscPosEmulator)](LICENSE)

🖨️ A receipt printer on your screen, for testing the ESC/POS your point-of-sale software sends. Point
your app at it over TCP, serial or USB, and each receipt prints in the window. Status queries get real
answers, and you can simulate paper-out, an open cover, the cash drawer and error states. It runs on
Windows, macOS and Linux, and the same app runs in the browser.

**[Try it in your browser](https://danielmeza.github.io/CrossEscPosEmulator/app/)** ·
**[Download](#download)** · **[Documentation](docs/README.md)** ·
**[Project site](https://danielmeza.github.io/CrossEscPosEmulator/)**

![The emulator on Linux, rendering QR, PDF417, DataMatrix and Aztec codes](docs/After%20Linux.png)

> **Avalonia Port Challenge entry.** CrossEscPos is a cross-platform port of
> [roydejong/EscPosEmulator](https://github.com/roydejong/EscPosEmulator), a Windows-only WPF app. See
> [From WPF to Avalonia](#from-wpf-to-avalonia-what-the-migration-cost) for before/after screenshots and
> what the migration cost.

## Features

- Receives ESC/POS over TCP/IP and serial on the desktop, and over Web Serial, WebUSB and a TCP proxy in
  the browser.
- Renders text styles, 1D barcodes, 2D codes (QR, PDF417, DataMatrix, Aztec), bit images and page mode.
  See [Supported commands](docs/Supported-Commands.md).
- Answers status queries (`DLE EOT`, `GS r`, Automatic Status Back). The **Printer state** panel simulates
  paper-out, an open cover, the cash drawer, offline and error states, and like real hardware the
  printer refuses jobs while it isn't ready.
- Signals the buzzer and the cash-drawer kick with a sound and an on-screen toast.
- Includes a **Monitor** test client that prints samples and shows the status the emulator reports back.
- Exports receipts as PNG, all in one image or one file per cut.
- Embeds in your own code: a headless core and NuGet packages with swappable render backends (SkiaSharp,
  or ImageSharp with no native dependencies).

## Download

Self-contained builds from the [latest release](https://github.com/danielmeza/CrossEscPosEmulator/releases/latest).
No .NET install is needed.

| Platform | Download |
|----------|----------|
| Windows (x64) | [`CrossEscPos-win-x64.zip`](https://github.com/danielmeza/CrossEscPosEmulator/releases/latest/download/CrossEscPos-win-x64.zip) |
| macOS (Apple silicon) | [`CrossEscPos-osx-arm64.zip`](https://github.com/danielmeza/CrossEscPosEmulator/releases/latest/download/CrossEscPos-osx-arm64.zip) (`.app` bundle) |
| macOS (Intel) | [`CrossEscPos-osx-x64.zip`](https://github.com/danielmeza/CrossEscPosEmulator/releases/latest/download/CrossEscPos-osx-x64.zip) (`.app` bundle) |
| Linux (x64) | [`CrossEscPos-linux-x64.tar.gz`](https://github.com/danielmeza/CrossEscPosEmulator/releases/latest/download/CrossEscPos-linux-x64.tar.gz) |
| Browser | No download: [run it online](https://danielmeza.github.io/CrossEscPosEmulator/app/) |

The libraries are on NuGet as `CrossEscPos.*`; see [Packages](docs/Packages.md).

> **macOS first launch.** The `.app` is ad-hoc signed but not notarized, so macOS may say *"CrossEscPos
> is damaged and can't be opened"*. Clear the quarantine flag once, then open it:
>
> ```sh
> xattr -dr com.apple.quarantine /path/to/CrossEscPos.app
> open /path/to/CrossEscPos.app
> ```

## Quick start

1. Start the app. It listens on TCP port 9100 on all interfaces.
2. Send it a receipt from a terminal, or point your POS software at `localhost:9100` as a network
   printer:

   ```sh
   printf 'Hello from CrossEscPos\n\n\n\035V\000' | nc -w 1 localhost 9100
   ```

3. Click **Open monitor…** to print sample receipts, barcodes and QR codes, and flip the switches in the
   **Printer state** panel to see the status your software would receive.

To change the port, open a serial port, or test serial without hardware, see
[Connecting](docs/Connecting.md).

To render ESC/POS from your own .NET code, start with [Getting started](docs/Getting-Started.md):

```csharp
var printer = new ReceiptPrinter(PaperConfiguration.Default,
    new SkiaImageFactory(), new SkiaTypefaceProvider());
printer.FeedEscPos(escPosBytes);                     // ESC/POS is binary: feed the raw bytes
using var image = printer.CurrentReceipt.Render();   // IReceiptImage
new SkiaImageEncoder().EncodePng(image, outputStream);
```

## From WPF to Avalonia: what the migration cost

*Entered in the [Avalonia Port Challenge](https://avaloniaui.net/blog/avalonia-port-challenge). The same
write-up is on the [project site](https://danielmeza.github.io/CrossEscPosEmulator/), next to a
[live browser demo](https://danielmeza.github.io/CrossEscPosEmulator/app/).*

| Before: WPF, Windows only | After: Avalonia 12 on macOS |
|:---:|:---:|
| ![Original WPF app on Windows](docs/Before%20WPF.png) | ![Avalonia app on macOS](docs/Example.png) |
| **After: Linux** | **After: browser (WebAssembly)** |
| ![Avalonia app on Linux](docs/After%20Linux.png) | ![The same app in the browser](docs/After%20Browser.png) |

### Starting point

The upstream app ([roydejong/EscPosEmulator](https://github.com/roydejong/EscPosEmulator), last
updated July 2025) targeted `net9.0-windows7.0` with `UseWPF`: 48 C#/XAML files, about 2,400 lines.
Windows was wired in at four levels:

- **Rendering.** Every receipt line drew itself with GDI+ (`System.Drawing.Bitmap` and `Graphics`),
  which is Windows-only on .NET 6 and later. The GDI+ types were part of the core interface,
  `IReceiptPrintable.Render(Bitmap, Graphics, int, int)`. To show a receipt, the window saved each
  bitmap into a BMP `MemoryStream` and loaded it back as a WPF `BitmapImage`.
- **UI.** Code-behind only. `MainWindow.xaml.cs` created `Image` controls by hand, found them again
  by a GUID-derived `Name`, and added or removed them from a `StackPanel`.
- **OS calls.** A `user32!FlashWindow` P/Invoke and `System.Media.SystemSounds` signalled new jobs.
- **Assumptions.** TCP was the only transport, and the test receipt was read from the current
  working directory.

The ESC/POS interpreter (one command class per opcode, registered in `EscPosInterpreter`) had no UI
or Windows dependency, so its design carried over unchanged. It is now the headless
`CrossEscPos.Core` package.

### What changed, and what each part cost

| Area | WPF original | Avalonia port | What it took |
|---|---|---|---|
| Rendering | GDI+ `Bitmap` / `Graphics` | SkiaSharp, later behind a backend-neutral `IReceiptCanvas`, plus a fully managed ImageSharp backend | The biggest single job. The text-line renderer (styles, sizes, justification, underline) was rewritten. System fonts differ per OS, so the same receipt measured differently on each; embedding JetBrains Mono (OFL) made output identical everywhere. The ImageSharp backend later needed its advance widths matched to Skia's. |
| UI | XAML + code-behind | AXAML + MVVM (CommunityToolkit.Mvvm); receipts bound to a reusable `ReceiptView` control | The markup ported almost line for line: `Window`, `DockPanel`, `StackPanel` and `ScrollViewer` all exist in Avalonia. The work was moving the code-behind into view models and bindings. |
| Win32 calls | `FlashWindow`, `SystemSounds` | `INotificationService`: `afplay` on macOS, `Console.Beep` on Windows, `paplay`/`aplay` on Linux, plus an in-window toast | Avalonia has no cross-platform system-sound API, so each OS gets its own strategy. |
| Files | Relative to the working directory | Avalonia `StorageProvider` for PNG export; app-relative asset paths | The working-directory assumption broke in the packaged app and was fixed right after the first release. |
| Transports | TCP | TCP and serial (`System.IO.Ports`); the Monitor adds direct USB (libusb) | Port names differ per OS (`COM3`, `/dev/ttyUSB0`, `/dev/cu.*`). A Homebrew-installed libusb wasn't found until the app added the usual install paths to `NATIVE_DLL_SEARCH_DIRECTORIES` at startup. |
| Packaging | One Windows `.exe` | Self-contained win-x64, linux-x64, osx-x64 and osx-arm64 builds from one CI matrix; a script builds the macOS `.app` | Unsigned macOS bundles were reported as "damaged", so the bundle is now ad-hoc signed. Notarization needs a paid Apple ID, so the README documents clearing the quarantine flag instead. |
| Browser | n/a | The same app as an Avalonia WASM head (`net10.0-browser`) | Platform edges sit behind `IPlatformServices`. A browser can't listen on TCP, so an ASP.NET Core SignalR host opens the socket and relays jobs to the page. Serial and USB go through Web Serial and WebUSB via JS interop. The storage API's save picker failed in the browser, so export downloads a JS blob instead. |

### The numbers

- **Time:** 7 days with commits over 5 weeks, per the git history. The port and desktop parity landed
  on June 5–6, 2026 (PRs #1–#7), the layered-package refactor on June 20–21 (#8–#10), and the
  browser head on July 4–7 (#11–#16).
- **Size:** from 48 files and ~2.4k lines to 168 files and ~10k lines of C# and AXAML, across 11
  projects, 2 samples and 2 test projects. Most of the growth is new features (barcodes, 2D codes,
  status commands, printer-state simulation, the Monitor, serial/USB, the browser head), not port
  overhead. The port PR itself (#1) was +2,525 / −487 lines across 60 files.
- **Tests:** 131 xUnit tests (89 test methods). They run the interpreter against a synthetic render backend,
  which proves the core is headless, and exercise the controls with Avalonia.Headless.
- **Tools:** built with AI assistance (Claude Code); the commits carry `Co-Authored-By` trailers.

### What was easy, and what hurt

- **Easy:** XAML to AXAML, the Fluent theme, `Dispatcher.UIThread`, and headless UI testing. The UI
  layer was the smallest part of the port.
- **Hurt:** removing GDI+. It was part of the core interfaces, not just the view, so the renderer
  had to be redesigned before anything else could move. After that came per-OS native dependencies
  (libusb, fontconfig on Linux, macOS signing) and the browser sandbox (no sockets, no raw file
  system).
- **Would do again:** put the drawing surface behind an interface first. Once `IReceiptCanvas`
  existed, the ImageSharp backend (a community contribution) and the browser head each landed within
  a few days.

## Documentation

The long-form guides live in [`docs/`](docs/README.md) and are published as the
[wiki](https://github.com/danielmeza/CrossEscPosEmulator/wiki).

| Guide | What's in it |
|-------|--------------|
| [Using the app](docs/Using-the-App.md) | The Monitor, the printer state panel, PNG export, choosing the render backend |
| [Connecting](docs/Connecting.md) | TCP and serial, environment variables, testing serial without hardware |
| [Browser app](docs/Browser-App.md) | The browser version, Web Serial and WebUSB, and the SignalR host for TCP |
| [Supported commands](docs/Supported-Commands.md) | The emulated printer, the ESC/POS it understands, and what's missing |
| [Getting started](docs/Getting-Started.md) and [Packages](docs/Packages.md) | Using the NuGet libraries in your own app |
| [Architecture](docs/Architecture.md) | The packages, one app with two heads, the bundled font |
| [Building and testing](docs/Building-and-Testing.md) | Building from source, running the tests, how releases are published |

## Contributing and support

- Found a bug, or a command that prints wrong? [Open an issue](https://github.com/danielmeza/CrossEscPosEmulator/issues).
  Attach the ESC/POS bytes if you can: run the app with `ESCPOS_DEBUG_DUMP=1` to save them.
- Pull requests are welcome. New commands follow the `BaseCommand` pattern described in
  [Adding a command](docs/Supported-Commands.md#adding-a-command). Run `dotnet test CrossEscPos.slnx`
  before you open one; warnings fail the build. See [Building and testing](docs/Building-and-Testing.md).

**Project status:** actively developed, but ESC/POS support is partial. See
[Not yet implemented](docs/Supported-Commands.md#not-yet-implemented) and expect gaps.

## Credits and license

- The original emulator is [EscPosEmulator](https://github.com/roydejong/EscPosEmulator) by Roy de Jong,
  and all credit for its design goes to him and its contributors. Thanks to
  [@yhonc9](https://github.com/yhonc9) for the ImageSharp render backend.
- Built with [.NET 10](https://dotnet.microsoft.com/), [Avalonia 12](https://avaloniaui.net/),
  [SkiaSharp](https://github.com/mono/SkiaSharp), [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet),
  [ZXing.Net](https://github.com/micjahn/ZXing.Net), [QRCoder](https://github.com/codebude/QRCoder),
  [Ardalis.SmartEnum](https://github.com/ardalis/SmartEnum), [System.IO.Ports](https://www.nuget.org/packages/System.IO.Ports),
  [ESC-POS-.NET](https://github.com/lukevp/ESC-POS-.NET) (the Monitor) and
  [LibUsbDotNet](https://github.com/LibUsbDotNet/LibUsbDotNet) (direct USB printing).
- Released under the [MIT License](LICENSE). Receipts are rendered with JetBrains Mono under the SIL Open
  Font License; see [Fonts and license](docs/Architecture.md#fonts-and-license).

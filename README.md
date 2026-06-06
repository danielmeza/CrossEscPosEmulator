# ESC/POS Receipt Printer Emulator
🖨️ **This app emulates a networked receipt printer to test your ESC/POS commands against.**

![Emulator](docs/Example.png)

### About
- Cross-platform application (Avalonia 12 + SkiaSharp + .NET 10), runs on Windows, macOS and Linux
- Listens for ESC/POS commands over **TCP/IP** and (optionally) a **serial port**
- Logs commands and visually represents the resulting receipt(s)
- Renders 1D barcodes and 2D codes (QR, PDF417, DataMatrix, Aztec), bit images, and page mode
- Answers status queries (`DLE EOT`, `GS r`, Automatic Status Back) — simulate paper-out, cover, drawer, offline and error states from the **Printer state** panel
- Signals buzzer / cash-drawer events with a sound and on-screen toast
- Configure the TCP listen address/port and serial port live from the UI
- Export rendered tickets to PNG — all in one image, or one file per cut
- It support different text formattings in the same line, although a few combinations were tested.

> **Cross-platform fork.** This project began as a cross-platform port of
> [roydejong/EscPosEmulator](https://github.com/roydejong/EscPosEmulator) (originally a Windows/WPF
> app). It has been migrated to Avalonia + SkiaSharp + .NET 10 so it runs on Windows, macOS and
> Linux, and extended with barcode/QR rendering and a serial transport. All credit for the original
> emulator goes to the upstream author.

👷 **This is an unfinished experiment.** Use at your own risk and keep your expectations low. :)

### Download

Pre-built, **self-contained** apps (no .NET install required) are published on the
[Releases](../../releases) page:

| Platform | Artifact |
|----------|----------|
| Windows (x64) | `ReceiptPrinterEmulator-win-x64.zip` |
| Linux (x64) | `ReceiptPrinterEmulator-linux-x64.tar.gz` |
| macOS (Intel) | `ReceiptPrinterEmulator-osx-x64.zip` (`.app` bundle) |
| macOS (Apple Silicon) | `ReceiptPrinterEmulator-osx-arm64.zip` (`.app` bundle) |

Releases are produced by the [`Release`](.github/workflows/release.yml) GitHub Actions workflow on
each `v*` tag.

> **macOS first launch.** The `.app` is **ad-hoc signed but not notarized** (no paid Apple Developer
> ID). macOS quarantines anything downloaded from the internet, so on first launch you may see
> *"ReceiptPrinterEmulator is damaged and can't be opened"* (especially on Apple Silicon). Clear the
> quarantine flag once, then open it:
>
> ```sh
> xattr -dr com.apple.quarantine /path/to/ReceiptPrinterEmulator.app
> open /path/to/ReceiptPrinterEmulator.app
> ```
>
> (Right-click → **Open** also works once the quarantine is cleared.)

### Built with

- [.NET 10](https://dotnet.microsoft.com/) · [Avalonia 12](https://avaloniaui.net/) ·
  [SkiaSharp](https://github.com/mono/SkiaSharp) (rendering) ·
  [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) (MVVM)
- [ZXing.Net](https://github.com/micjahn/ZXing.Net) (1D barcodes) ·
  [QRCoder](https://github.com/codebude/QRCoder) (QR codes) ·
  [System.IO.Ports](https://www.nuget.org/packages/System.IO.Ports) (serial)
- [ESC-POS-.NET](https://github.com/lukevp/ESC-POS-.NET) (the Monitor test client) ·
  [LibUsbDotNet](https://github.com/LibUsbDotNet/LibUsbDotNet) (direct USB printing)

### Supported commands

⚠️ Support is currently limited to only a subset of ESC/POS. Even the commands listed here may only be partially implemented.

- Raw Text
- LF: Line feed
- CR: Carriage return
- ESC Commands:
  - Initialize printer (`ESC @`)
  - Toggle italic (`ESC 4` / `ESC 5`) *[possibly deprecated?]*
  - Select font (`ESC M`)
  - Select charset (`ESC R`)
  - Select character table (`ESC t`)
  - Select justification (`ESC a`)
  - Select line spacing (`ESC 2` / `ESC 3`)
  - Toggle emphasis (`ESC E`)
  - Toggle underline (`ESC -`)
  - Set print text mode (`ESC !`)
  - Full cut (`ESC m`)
  - Partial cut (`ESC i`)
  - Print and feed n lines (`ESC d`)
  - Print and feed paper (`ESC J`)
  - Generate pulse / kick cash drawer (`ESC p m t1 t2`)
  - Select character code table (`ESC t`) — PC437/850/852/858/860/863/865/866/1252 remapped to Unicode
  - Beeper (`ESC ( A`)
  - Bit image (`ESC *`) — 8-dot and 24-dot inline raster
  - Page mode: select page / standard mode (`ESC L` / `ESC S`), print area (`ESC W`), direction (`ESC T`), absolute position (`ESC $`)
  - User-defined characters (`ESC &` / `ESC %` / `ESC ?`) — parsed & stored
- Control characters:
  - Buzzer / beeper (`BEL`, 0x07)
  - Form feed (`FF`) — prints the page in page mode
  - Cancel (`CAN`) — cancels page data
- DLE (real-time) Commands:
  - Real-time status (`DLE EOT n`, n=1-4)
  - Real-time request / recover (`DLE ENQ`)
  - Real-time cash-drawer pulse (`DLE DC4 1 m t`)
- FS Commands:
  - Print stored logo (`FS p n m`)
  - Auto cut (`FS } 0x60 n`)
- GS Commands:
  - Select character size
  - Select cut mode and cut paper
  - Paper eject (`GS e n [m t]`)
  - Print raster image (`GS v 0 [m xL xH yL yH ...pixels]`)
  - Print 1D barcode (`GS k`) — UPC-A/E, EAN-13/8, CODE39, CODE93, CODE128, ITF, CODABAR (both function A & B forms)
  - Set barcode height / module width (`GS h` / `GS w`)
  - Select HRI text position / font (`GS H` / `GS f`)
  - Print 2D symbols (`GS ( k`) — QR Code (cn=49), PDF417 (cn=48), DataMatrix (cn=54), Aztec (cn=55)
  - **Status / transmit-back**: paper & drawer status (`GS r`), printer ID (`GS I`), Automatic Status Back (`GS a`)
  - Download bit image: define (`GS *`) and print (`GS /`)
  - Set motion units (`GS P`), absolute/relative vertical position (`GS $` / `GS \`)
  - Config (accepted/ignored): user setup (`GS ( E`), print control (`GS ( K`), response request (`GS ( H`)

The emulator is **bidirectional**: status commands (`DLE EOT`, `GS r`, `GS I`) and Automatic Status
Back reply to the host over the same TCP/serial connection, driven by the **Printer state** panel
(right side) where you can simulate paper-out/near-end, cover open, cash-drawer open/closed,
offline, and error conditions. Like a real device, the emulator **refuses to print** while it isn't
ready (out of paper, cover open, offline, or in an error state) and shows a notification instead.

Barcodes and QR codes render inline on the receipt, with optional HRI text:

![Barcode and QR example](docs/Example%20QR.png)

### Not yet implemented

A few things remain partial or unimplemented:

- **Page-mode coordinate system** — page mode buffers output and rasterizes it on `FF`, but absolute/relative positioning (`ESC $`, `GS $`, `GS \`) and print direction (`ESC T`) are accepted as no-ops rather than fully positioned.
- **User-defined glyph substitution** — `ESC &` glyphs are parsed and stored, but the inline text renderer still draws the font glyph rather than the custom bitmap.
- **MaxiCode / GS1 DataBar / Composite** 2D symbologies (`GS ( k` cn=50/51/52).
- **Graphics commands** `GS ( L` / `GS 8 L` (NV/raster graphics store-and-print) and other `ESC *`-family densities.
- **Real-time `DLE DC4`** functions other than the cash-drawer pulse (power-off, recover-and-cancel, buzzer).
- **Katakana / CJK code pages** render as missing glyphs since the bundled Latin font has no such glyphs.

Contributions welcome — new commands follow the simple `BaseCommand` pattern in
[`EscPos/Commands`](EscPos/Commands) and are registered in
[`EscPosInterpreter.RegisterCommands`](EscPos/EscPosInterpreter.cs).

### Connecting

The emulator accepts ESC/POS data over two transports. Both can be changed **live from the UI**
(left panel): pick a TCP listen address and port and Start/Stop the listener, or select a serial
port + baud and Open/Close it (⟳ refreshes the port list). The environment variables below set the
**initial** values at startup:

| Variable | Default | Meaning |
|----------|---------|---------|
| `ESCPOS_LISTEN_ADDRESS` | `0.0.0.0` | Initial TCP bind address (`0.0.0.0` = all interfaces, `127.0.0.1` = localhost). |
| `ESCPOS_TCP_PORT` | `9100` | Initial TCP listen port. Set to `off` / `0` to start with TCP stopped. |
| `ESCPOS_SERIAL_PORT` | *(unset)* | Serial device to auto-open (e.g. `/dev/ttys004`, `COM3`). Unset = serial closed. |
| `ESCPOS_SERIAL_BAUD` | `9600` | Serial baud rate. |
| `ESCPOS_DEBUG_DUMP` | *(off)* | Set to `1` to dump every received payload to `last_*` files. |

Examples:

```sh
dotnet run                                   # TCP only, port 9100
ESCPOS_TCP_PORT=9200 dotnet run              # TCP on 9200
ESCPOS_SERIAL_PORT=/dev/ttys004 dotnet run   # TCP 9100 + serial
ESCPOS_TCP_PORT=off ESCPOS_SERIAL_PORT=COM3 dotnet run   # serial only
```

The status panel shows the active TCP endpoint and serial port.

#### Testing serial without hardware (app-to-app on one machine)

You don't need a USB serial adapter. Create a **virtual serial bridge** — a pair of linked ports —
then point the emulator at one end and your POS application (or a shell) at the other. Bytes written
to one end appear on the other.

**macOS / Linux** — using [`socat`](http://www.dest-unreach.org/socat/) (`brew install socat` /
`apt install socat`). A helper script is included:

```sh
./scripts/serial-bridge.sh
# It prints a linked pair, e.g.:
#   PORT A (emulator): /dev/ttys004
#   PORT B (your app): /dev/ttys005
# Leave it running.
```

Then, in two more terminals:

```sh
# Terminal 2 — run the emulator on port A
ESCPOS_SERIAL_PORT=/dev/ttys004 dotnet run

# Terminal 3 — send a receipt from "another app" on port B
cat test_receipt.txt > /dev/ttys005
#   …or from your own program, just open /dev/ttys005 like a normal serial port
#   (9600 8N1) and write ESC/POS bytes to it.
```

The receipt appears in the emulator window. Because the interpreter is stateful across reads,
fragmented serial writes (commands split across packets) are handled correctly.

**Windows** — install [com0com](https://com0com.sourceforge.net/) and create a linked pair
(e.g. `COM3` ↔ `COM4`). Run the emulator with `ESCPOS_SERIAL_PORT=COM3` and have your application
write to `COM4`.

### Monitor (built-in test client)

Sending test jobs is the **monitor's** job — the emulator is the device, the monitor is the POS-side
client that drives it over the wire (just like a real application would). Click **Open monitor…** to
launch a second window (built on [ESC-POS-.NET](https://github.com/lukevp/ESC-POS-.NET)) and pick a
transport:

- **TCP/IP** — connect to the emulator's listener (or any networked printer).
- **Serial** — pick a port + baud; pairs with the emulator's serial transport via a virtual port bridge.
- **USB** — print **directly to a real USB printer** selected from the connected-device list (by
  VID:PID), via libusb. This is send-only (no status), and needs native **libusb** installed
  (macOS `brew install libusb`, Debian/Ubuntu `apt install libusb-1.0-0`; bundled on Windows) and the
  OS not already holding the device.

It then lets you exercise the target without writing any code:

- Print a sample receipt, all 1D barcodes, or QR / PDF417 / DataMatrix / Aztec.
- Send the full feature test receipt, open the cash drawer, buzz, or cut.
- Watch the **printer status** the emulator reports back: toggle paper-out / cover / drawer / offline
  in the **Printer state** panel and the monitor's status display updates live (via Automatic Status
  Back), confirming the emulator's status responses are wire-correct. When the printer isn't ready,
  the emulator drops the job and shows a notification, just like real hardware.

![Monitor](docs/Monitor.png)

Toggling the emulator's **Printer state** panel pushes status to the monitor in real time — here the
printer reports *paper low* and a *recoverable error*, so the monitor shows **Not ready**:

![Monitor reflecting printer state](docs/Monitor%20Invalid%20State.png)

### Exporting tickets

Each cut (`ESC i` / `ESC m` / `GS V`) starts a new receipt — a "page". The **Export** buttons in the
left panel save the rendered tickets as PNG:

- **Export all (single image)** — stacks every receipt into one tall PNG (a save dialog).
- **Export each cut (folder)** — writes one `receipt_NNN.png` per cut into a chosen folder.

### Building & running

```sh
dotnet run
```

Requires the .NET 10 SDK. The app runs on Windows, macOS and Linux.

- **Windows / macOS:** no extra setup — native rendering libraries ship with the Avalonia packages.
- **Linux:** install the usual font/render native deps if they are missing, e.g.
  `sudo apt install libfontconfig1 libfreetype6` (Debian/Ubuntu).

### Fonts & license

Receipt text is rendered with **[JetBrains Mono](https://www.jetbrains.com/lp/mono/)**, bundled under
[`Assets/Fonts/`](Assets/Fonts) so output is identical across platforms. JetBrains Mono is licensed
under the **SIL Open Font License 1.1**; the full license text is included at
[`Assets/Fonts/OFL.txt`](Assets/Fonts/OFL.txt). Per the OFL, the font is redistributed here under its
original license and "JetBrains Mono" is a trademark of JetBrains s.r.o. To swap in a different
monospace font, replace the `receipt-mono*.ttf` files (and keep its license alongside).

### Emulated printer

This program emulates a printer with the following specifications:

 - 80mm paper width
 - 72mm printing width
 - 180x180dpi
 - ASCII Font A/B: 12x24 pixels
 - Automatic line feed

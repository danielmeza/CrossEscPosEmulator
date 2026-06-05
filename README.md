# ESC/POS Receipt Printer Emulator
🖨️ **This app emulates a networked receipt printer to test your ESC/POS commands against.**



### About
- Cross-platform application (Avalonia 12 + SkiaSharp + .NET 10), runs on Windows, macOS and Linux
- Binds to a TCP/IP interface and listens for ESC/POS commands
- Logs commands and visually represents the resulting receipt(s)
- It support different text formattings in the same line, although a few combinations were tested.

👷 **This is an unfinished experiment.** Use at your own risk and keep your expectations low. :)

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
  - Print 2D QR Code (`GS ( k`, cn=49) — model, module size, error-correction level, store & print

### Example

![Emulator](docs/Example.png)

### Connecting

The emulator accepts ESC/POS data over two transports:

- **TCP/IP** — always listening on port **9100** (send to `localhost:9100`).
- **Serial** — optional. Set `ESCPOS_SERIAL_PORT` (and optionally `ESCPOS_SERIAL_BAUD`, default 9600)
  before launching to open a serial port. The status panel shows the active port.

To simulate a serial printer **without hardware**, create a virtual serial pair:

```sh
# macOS / Linux — prints two PTY device paths (e.g. /dev/ttys004 <-> /dev/ttys005)
socat -d -d pty,raw,echo=0 pty,raw,echo=0

# Point the app at one end…
ESCPOS_SERIAL_PORT=/dev/ttys004 dotnet run
# …and send a receipt to the other end:
cat test_receipt.txt > /dev/ttys005
```

On Windows use [com0com](https://com0com.sourceforge.net/) to create a linked COM pair (e.g. `COM3` ↔ `COM4`).

### Building & running

```sh
dotnet run
```

Requires the .NET 10 SDK. The app runs on Windows, macOS and Linux.

- **Windows / macOS:** no extra setup — native rendering libraries ship with the Avalonia packages.
- **Linux:** install the usual font/render native deps if they are missing, e.g.
  `sudo apt install libfontconfig1 libfreetype6` (Debian/Ubuntu).

Receipt text is rendered with the bundled **JetBrains Mono** font (under `Assets/Fonts/`, OFL),
so output is identical across platforms.

### Emulated printer

This program emulates a printer with the following specifications:

 - 80mm paper width
 - 72mm printing width
 - 180x180dpi
 - ASCII Font A/B: 12x24 pixels
 - Automatic line feed

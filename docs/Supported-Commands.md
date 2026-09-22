# Supported commands

What the emulated printer is, which ESC/POS commands it understands, and what is still missing.

## The emulated printer

This program emulates a printer with the following specifications:

 - 80mm paper width
 - 72mm printing width
 - 180x180dpi
 - ASCII Font A/B: 12x24 pixels
 - Automatic line feed

## Supported commands

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

![Barcode and QR example](Example%20QR.png)

## Not yet implemented

A few things remain partial or unimplemented:

- **Page-mode coordinate system** — page mode buffers output and rasterizes it on `FF`, but absolute/relative positioning (`ESC $`, `GS $`, `GS \`) and print direction (`ESC T`) are accepted as no-ops rather than fully positioned.
- **User-defined glyph substitution** — `ESC &` glyphs are parsed and stored, but the inline text renderer still draws the font glyph rather than the custom bitmap.
- **MaxiCode / GS1 DataBar / Composite** 2D symbologies (`GS ( k` cn=50/51/52).
- **Graphics commands** `GS ( L` / `GS 8 L` (NV/raster graphics store-and-print) and other `ESC *`-family densities.
- **Real-time `DLE DC4`** functions other than the cash-drawer pulse (power-off, recover-and-cancel, buzzer).
- **Katakana / CJK code pages** render as missing glyphs since the bundled Latin font has no such glyphs.

## Adding a command

Contributions are welcome. New commands follow the simple `BaseCommand` pattern in
[`EscPos/Commands`](../src/CrossEscPos.Core/EscPos/Commands) and are registered in
[`EscPosInterpreter.RegisterCommands`](../src/CrossEscPos.Core/EscPos/EscPosInterpreter.cs).

For the quirks found while implementing commands (for example, why italics are vendor-specific), see
[ESC/POS notes](ESC-POS-Notes.md).

#!/usr/bin/env python3
"""Generate the ESC/POS job for the GitHub Pages hero receipt.

The site's hero image is printed by the emulator itself:

    python3 scripts/gen-site-receipt.py > site-receipt.escpos
    dotnet run --project samples/CrossEscPos.Headless -- site-receipt.escpos site/receipt.png
"""
import sys

ESC, GS = b"\x1b", b"\x1d"
DEMO_URL = b"https://danielmeza.github.io/CrossEscPosEmulator/app/"


def line(text=""):
    return text.encode("ascii") + b"\n"


def row(left, right, width=42):
    return line(left + right.rjust(width - len(left)))


def qr(data, module=6):
    store = len(data) + 3
    return (
        GS + b"(k\x04\x00\x31\x41\x32\x00"                 # model 2
        + GS + b"(k\x03\x00\x31\x43" + bytes([module])    # module size
        + GS + b"(k\x03\x00\x31\x45\x31"                  # error correction M
        + GS + b"(k" + bytes([store & 0xFF, store >> 8]) + b"\x31\x50\x30" + data
        + GS + b"(k\x03\x00\x31\x51\x30"                  # print
    )


job = b"".join([
    ESC + b"@",
    ESC + b"a\x01",
    GS + b"!\x11", line("CrossEscPos"), GS + b"!\x00",
    line("ESC/POS receipt printer emulator"),
    line("-" * 42),
    row("Ported from", "WPF / .NET 9"),
    row("Ported to", "Avalonia 12 / .NET 10"),
    line(),
    ESC + b"E\x01", row("Runs on", ""), ESC + b"E\x00",
    row("  Windows", "x64"),
    row("  macOS", "x64 + arm64"),
    row("  Linux", "x64"),
    row("  Browser", "WebAssembly"),
    line("-" * 42),
    qr(DEMO_URL),
    line(),
    line("Scan to run it in your browser"),
    line(),
    ESC + b"E\x01", line("Thank you!"), ESC + b"E\x00",
    ESC + b"d\x02",
])

sys.stdout.buffer.write(job)

#!/usr/bin/env python3
"""Generate a comprehensive test_receipt.txt that exercises every supported feature:
text formatting, alignment, fonts, sizes, all 1D barcodes, all 2D symbols, a bit image,
the buzzer and the cash drawer. Run from the repo root: python3 scripts/gen-test-receipt.py"""

out = bytearray()


def raw(*xs):
    for x in xs:
        out.append(x & 0xFF)


def S(s):
    out.extend(s.encode("latin1"))


ESC, GS = 0x1B, 0x1D

# --- helpers --------------------------------------------------------------
def init():            raw(ESC, ord('@'))
def align(n):          raw(ESC, ord('a'), n)            # 0 left, 1 center, 2 right
def bold(on):          raw(ESC, ord('E'), 1 if on else 0)
def italic(on):        raw(ESC, 0x34 if on else 0x35)   # ESC 4 / ESC 5
def underline(n):      raw(ESC, ord('-'), n)            # 0 off, 1 one-dot, 2 two-dot
def font(n):           raw(ESC, ord('M'), n)            # 0 A, 1 B
def size(w, h):        raw(GS, ord('!'), ((w - 1) << 4) | (h - 1))
def feed(n=1):         raw(ESC, ord('d'), n)
def line(s=""):        S(s); raw(0x0A)
def rule():            line("-" * 42)
def cut():             raw(ESC, ord('i'))


def section(title):
    align(1); bold(True); size(1, 2); line(title)
    size(1, 1); bold(False); align(0); feed(1)


# --- header ---------------------------------------------------------------
init()
align(1)
size(2, 2); bold(True); line("ESC/POS")
size(1, 1); line("Emulator feature test"); bold(False)
align(0); rule()

# --- text formatting ------------------------------------------------------
section("TEXT STYLES")
line("Normal text")
bold(True); line("Bold text"); bold(False)
italic(True); line("Italic text"); italic(False)
underline(1); line("Underline (1 dot)"); underline(0)
underline(2); line("Underline (2 dot)"); underline(0)
font(1); line("Font B (small)"); font(0)
size(2, 1); line("Double width"); size(1, 1)
size(1, 2); line("Double height"); size(1, 1)
size(2, 2); line("Double both"); size(1, 1)
rule()

# --- alignment ------------------------------------------------------------
section("ALIGNMENT")
align(0); line("Left aligned")
align(1); line("Center aligned")
align(2); line("Right aligned")
align(0); rule()

# --- 1D barcodes ----------------------------------------------------------
section("1D BARCODES")
raw(GS, ord('h'), 70)     # height 70 dots
raw(GS, ord('w'), 2)      # module width 2
raw(GS, ord('H'), 2)      # HRI below
raw(GS, ord('f'), 0)      # HRI font A


def barcode(m, data, label):
    align(1); S(label); raw(0x0A)
    raw(GS, ord('k'), m, len(data)); S(data); raw(0x0A)
    align(0); feed(1)


barcode(65, "12345678901", "UPC-A")
barcode(67, "123456789012", "EAN-13")
barcode(68, "1234567", "EAN-8")
barcode(69, "CODE39", "CODE39")
barcode(72, "CODE93", "CODE93")
barcode(73, "Code128", "CODE128")
barcode(70, "12345678", "ITF")
barcode(71, "A123456A", "CODABAR")
rule()

# --- 2D symbols -----------------------------------------------------------
section("2D SYMBOLS")


def sym2d(cn, data, label, size_dots=5):
    align(1); S(label); raw(0x0A)
    raw(GS, ord('('), ord('k'), 3, 0, cn, 67, size_dots)        # module size
    if cn == 49:
        raw(GS, ord('('), ord('k'), 3, 0, cn, 69, 49)           # QR EC = M
    ln = 3 + len(data)
    raw(GS, ord('('), ord('k'), ln, 0, cn, 80, 48); S(data)     # store
    raw(GS, ord('('), ord('k'), 3, 0, cn, 81, 48)               # print
    align(0); feed(1)


sym2d(49, "https://example.com/qr", "QR Code", 6)
sym2d(48, "PDF417 sample data", "PDF417", 3)
sym2d(54, "DataMatrix sample", "DataMatrix")
sym2d(55, "Aztec sample", "Aztec")
rule()

# --- bit image (ESC *) ----------------------------------------------------
section("BIT IMAGE (ESC *)")
align(1)
width = 96
raw(ESC, ord('*'), 33, width & 0xFF, width >> 8)   # 24-dot mode
for col in range(width):
    # a wavy band
    top = 0xFF if (col // 6) % 2 == 0 else 0x18
    mid = 0x3C
    bot = 0x18 if (col // 6) % 2 == 0 else 0xFF
    raw(top, mid, bot)
raw(0x0A)
align(0); rule()

# --- peripherals ----------------------------------------------------------
section("PERIPHERALS")
align(1); bold(True); line(">> BEEP + OPEN DRAWER <<"); bold(False); align(0)
raw(0x07)                          # BEL buzzer
raw(ESC, ord('p'), 0, 25, 25)      # cash drawer kick
feed(1)
line("Thank you!")
feed(2)
cut()

with open("test_receipt.txt", "wb") as f:
    f.write(out)
print(f"wrote test_receipt.txt ({len(out)} bytes)")

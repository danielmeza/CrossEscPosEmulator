#!/usr/bin/env bash
#
# serial-bridge.sh — create a virtual serial port pair for testing the emulator without hardware.
#
# Requires socat (macOS: `brew install socat`, Debian/Ubuntu: `sudo apt install socat`).
#
# It links two PTY devices: write ESC/POS bytes to one and they arrive on the other. Point the
# emulator at PORT A and your POS application (or `cat file > PORT_B`) at PORT B.
#
# Usage:
#   ./scripts/serial-bridge.sh
#   # then, in other terminals:
#   ESCPOS_SERIAL_PORT=<PORT A> dotnet run
#   cat test_receipt.txt > <PORT B>
#
set -euo pipefail

if ! command -v socat >/dev/null 2>&1; then
  echo "error: socat is not installed." >&2
  echo "  macOS:        brew install socat" >&2
  echo "  Debian/Ubuntu: sudo apt install socat" >&2
  exit 1
fi

echo "Creating a virtual serial port pair (Ctrl-C to stop)…"
echo "Look for two 'PTY is /dev/ttysNNN' lines below:"
echo "  - the FIRST is PORT A  -> ESCPOS_SERIAL_PORT=<PORT A> dotnet run"
echo "  - the SECOND is PORT B -> write your ESC/POS to it (e.g. cat test_receipt.txt > <PORT B>)"
echo

exec socat -d -d pty,raw,echo=0 pty,raw,echo=0

#!/usr/bin/env bash
#
# make-macos-app.sh — wrap a `dotnet publish` output into a macOS .app bundle (with .icns icon).
# Must run on macOS (uses sips + iconutil).
#
# Usage: make-macos-app.sh <publish-dir> <output-dir> <version> [icon-png]
#
set -euo pipefail

PUBLISH_DIR=${1:?publish dir required}
OUTPUT_DIR=${2:?output dir required}
VERSION=${3:-1.0.0}
ICON_PNG=${4:-Assets/Icon/icon.png}
EXE_NAME=ReceiptPrinterEmulator

APP="$OUTPUT_DIR/$EXE_NAME.app"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

# Payload
cp -R "$PUBLISH_DIR"/. "$APP/Contents/MacOS/"
chmod +x "$APP/Contents/MacOS/$EXE_NAME"

# Icon (.icns) from the PNG.
# Prefer macOS tooling (sips + iconutil); fall back to png2icns (icnsutils) on Linux so the bundle
# can also be assembled off a Mac. If neither is available the .app still runs (default icon).
ICNS_OUT="$APP/Contents/Resources/icon.icns"
if command -v iconutil >/dev/null 2>&1 && command -v sips >/dev/null 2>&1; then
  ICONSET="$(mktemp -d)/icon.iconset"
  mkdir -p "$ICONSET"
  for s in 16 32 128 256 512; do
    sips -z "$s" "$s" "$ICON_PNG" --out "$ICONSET/icon_${s}x${s}.png"    >/dev/null
    d=$((s * 2))
    sips -z "$d" "$d" "$ICON_PNG" --out "$ICONSET/icon_${s}x${s}@2x.png" >/dev/null
  done
  iconutil -c icns "$ICONSET" -o "$ICNS_OUT"
elif command -v png2icns >/dev/null 2>&1; then
  TMP="$(mktemp -d)"
  if command -v convert >/dev/null 2>&1; then
    for s in 16 32 48 128 256 512; do convert "$ICON_PNG" -resize "${s}x${s}" "$TMP/i_${s}.png"; done
    png2icns "$ICNS_OUT" "$TMP"/i_*.png
  else
    png2icns "$ICNS_OUT" "$ICON_PNG"
  fi
else
  echo "warning: no iconutil/png2icns found — building .app without a custom icon" >&2
fi

# Info.plist
cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>ESC/POS Emulator</string>
  <key>CFBundleDisplayName</key><string>ESC/POS Receipt Printer Emulator</string>
  <key>CFBundleIdentifier</key><string>com.github.crossescposemulator</string>
  <key>CFBundleVersion</key><string>$VERSION</string>
  <key>CFBundleShortVersionString</key><string>$VERSION</string>
  <key>CFBundleExecutable</key><string>$EXE_NAME</string>
  <key>CFBundleIconFile</key><string>icon</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>LSMinimumSystemVersion</key><string>11.0</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>LSApplicationCategoryType</key><string>public.app-category.developer-tools</string>
</dict>
</plist>
PLIST

# Ad-hoc code-sign the whole bundle (deep) so it runs on Apple Silicon. Without any signature,
# Gatekeeper reports a downloaded app as "damaged" on arm64. This is NOT Developer-ID/notarized —
# users still need to clear the download quarantine on first launch (see README). Signing must be
# the LAST step, after all files (Info.plist, icns, payload) are in place.
if command -v codesign >/dev/null 2>&1; then
  echo "Ad-hoc code-signing $APP …"
  codesign --remove-signature "$APP" 2>/dev/null || true
  codesign --force --deep --sign - --timestamp=none "$APP"
  codesign --verify --deep --strict --verbose=2 "$APP" || echo "warning: codesign verify failed" >&2
else
  echo "warning: codesign not available — bundle left unsigned (will be 'damaged' on Apple Silicon)" >&2
fi

echo "Built $APP"

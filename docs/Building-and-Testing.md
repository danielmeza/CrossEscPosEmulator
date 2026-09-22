# Building and testing

Build and run every head from source, and run the test suites.

## Building and running

Requires the .NET 10 SDK (and the `wasm-tools` workload for the browser head:
`dotnet workload install wasm-tools`).

```sh
# Desktop app (Windows / macOS / Linux)
dotnet run --project src/CrossEscPos.App.Desktop

# Headless: ESC/POS bytes -> PNG, no UI, no Avalonia
dotnet run --project samples/CrossEscPos.Headless -- test_receipt.txt out.png

# Browser: the SAME app in the browser (Avalonia WASM head) — full parity with desktop
dotnet run --project src/CrossEscPos.App.Browser

# Web host: serves the browser app AND the SignalR broker on one origin, giving it TCP reception
# (browsers can't open raw sockets). First run publishes the WASM app into wwwroot (cached after).
# Browse to the printed URL; the emulator opens the TCP port itself.
dotnet run --project samples/CrossEscPos.Host

# Tests (unit + headless UI)
dotnet test CrossEscPos.slnx
```

- **Windows / macOS:** no extra setup — native rendering libraries ship with the Avalonia packages.
- **Linux:** install the usual font/render native deps if they are missing, e.g.
  `sudo apt install libfontconfig1 libfreetype6` (Debian/Ubuntu).

## Testing

Two test projects under [`tests/`](../tests):

- **`CrossEscPos.Core.Tests`** — emulation coverage. ESC/POS sequences are fed through the interpreter
  and the observable results (rendered draws, printer state, host responses, events) asserted: text
  and control characters, styling (emphasis/italic/size/justification), cutting and feeding, 1D/2D
  barcodes and bit images, status/transmit-back (`DLE EOT`, `GS r`, `GS I`, `GS a`), cash drawer and
  buzzer, real-time commands, code pages, page mode and not-ready handling — plus exact `StatusByteBuilder`
  bit layouts and the `SmartEnum` code maps. Rendering goes to a synthetic backend, so the suite runs
  with no SkiaSharp (proving the core is headless).
- **`CrossEscPos.Controls.Tests`** — headless Avalonia UI tests for the controls (two-way state binding,
  receipt image rendering).

```sh
dotnet test CrossEscPos.slnx
```

## Releases, the site and the wiki

Three GitHub Actions workflows publish the project:

- [`release.yml`](../.github/workflows/release.yml) runs on a `v*` tag. It builds self-contained desktop
  apps for win-x64, linux-x64, osx-x64 and osx-arm64 (the macOS ones as an ad-hoc signed `.app`), packs
  the NuGet libraries, publishes them to nuget.org, and attaches everything to a GitHub Release.
- [`pages.yml`](../.github/workflows/pages.yml) runs on every push to `main`. It publishes the project
  site from [`site/`](../site) and the browser app under `/app/` to GitHub Pages.
- [`wiki.yml`](../.github/workflows/wiki.yml) runs when `docs/` changes on `main` and publishes these
  pages to the [wiki](https://github.com/danielmeza/CrossEscPosEmulator/wiki). Edit the pages here, not in
  the wiki: the next sync overwrites wiki edits.

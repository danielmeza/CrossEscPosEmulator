# CrossEscPos.Core

The headless emulator: the ESC/POS interpreter, the `ReceiptPrinter` state machine, the receipt
document model, and barcode/QR generation. No SkiaSharp, no Avalonia — you supply a render backend
(an `IReceiptImageFactory` + `ITypefaceProvider`) when you construct the printer.

```csharp
using CrossEscPos.Emulator;
using CrossEscPos.Graphics;   // the abstraction types
```

## Constructing a printer

```csharp
var printer = new ReceiptPrinter(PaperConfiguration.Default, imageFactory, typefaces);
```

`PaperConfiguration` controls the paper geometry (defaults to 80mm @ 203dpi):

```csharp
var paper = new PaperConfiguration { PaperWidthMm = 58, PrintWidthMm = 54, DotsPerInch = 203 };
var printer = new ReceiptPrinter(paper, imageFactory, typefaces);
```

## Feeding ESC/POS

`FeedEscPos` is the single entry point. ESC/POS is binary, so the `byte[]` / `ReadOnlySpan<byte>`
overloads are the natural ones; a `string` overload (one char per byte, Latin1) is there for convenience.

```csharp
printer.FeedEscPos(bytes);                    // byte[]
printer.FeedEscPos(buffer.AsSpan(0, count));  // ReadOnlySpan<byte>
printer.FeedEscPos("Hello\n");                // string convenience
```

Each call is parsed and executed immediately. Malformed/unsupported commands are logged and skipped —
a bad byte never throws out of `FeedEscPos`.

## Receipts

```csharp
printer.CurrentReceipt      // the receipt currently being printed (Receipt)
printer.ReceiptStack        // IReadOnlyList<Receipt> — one entry per cut
receipt.IsEmpty             // nothing printed yet
using var image = receipt.Render();   // IReceiptImage (dispose it)
```

## Printer state

`ReceiptPrinter.State` is a `PrinterState` you can both read and drive. Driving it makes the emulator
answer status commands the way a real device would (out of paper, cover open, …). It implements
`INotifyPropertyChanged` and raises `Changed` on any mutation.

```csharp
printer.State.Online = false;             // now status replies report offline
printer.State.Paper = PaperLevel.Out;     // and "out of paper"
printer.State.Changed += () => { /* refresh UI */ };
```

While the printer isn't ready (offline, no paper, cover open, error), print operations are dropped and
`OnPrintBlocked` fires with a reason — mirroring real hardware.

## Status / transmit-back

Status commands (`DLE EOT`, `GS r`, `GS I`, Automatic Status Back) reply to the host. Provide a channel
by implementing `IPrinterResponder`:

```csharp
sealed class MyResponder : IPrinterResponder
{
    public void Send(byte[] data) { /* write back to the host */ }
}

// Per-request reply (the responder for the bytes currently being processed):
printer.FeedEscPos(requestBytes, new MyResponder());

// Or register a long-lived responder to also receive Automatic Status Back broadcasts:
printer.RegisterResponder(myResponder);
```

The transports in `CrossEscPos.Transports` already implement `IPrinterResponder` for you.

## Events

```csharp
printer.OnActivityEvent += (_, _) => { };   // a payload was received & processed
printer.OnBuzzer        += ()     => { };   // BEL / buzzer command
printer.OnCashDrawer    += ()     => { };   // cash-drawer kick (also sets State.DrawerOpen)
printer.OnPrintBlocked  += reason => { };   // print dropped because not ready
```

## Threading

Events and status can fire from a transport's receive thread. If you bind `State` to UI, marshal those
mutations onto the UI thread via `UiDispatch` (it runs synchronously by default — fine for headless):

```csharp
printer.UiDispatch = action => Dispatcher.UIThread.Post(action);  // Avalonia example
```

## Code pages

`ESC t n` selects a character code table; high bytes are remapped to Unicode for rendering. The legacy
code pages (437/850/852/858/866/1252, …) are provided by the .NET runtime, so this works on every
platform with no extra setup.

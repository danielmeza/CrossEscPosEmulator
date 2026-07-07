using System;
using System.Collections.Generic;
using System.Linq;
using CrossEscPos.Emulator;
using CrossEscPos.Graphics;
using CrossEscPos.Web.Rendering;

namespace CrossEscPos.Web.Services;

/// <summary>
/// Owns the headless <see cref="ReceiptPrinter"/> and the active <see cref="RenderBackend"/> for the
/// whole browser session. This is the composition root the UI talks to: feed it ESC/POS bytes, read
/// back the rendered receipts, and swap the render engine at runtime. The <see cref="ReceiptPrinter"/>
/// itself is entirely platform-agnostic — the only thing that changes between engines is the injected
/// backend triple.
/// </summary>
public sealed class EmulatorHost
{
    private RenderBackend _backend = RenderBackend.Default;
    private ReceiptPrinter _printer = null!;
    private IImageEncoder _encoder = null!;

    // Live transports (Web Serial / WebUSB) registered as responders — re-attached to the new printer
    // whenever the render engine is swapped, so a connection survives an engine switch.
    private readonly List<IPrinterResponder> _liveResponders = new();

    /// <summary>Raised whenever the printer, its receipts, or its state change (drives UI refresh).</summary>
    public event Action? Changed;

    public EmulatorHost() => Rebuild();

    public RenderBackend Backend => _backend;
    public IReadOnlyList<RenderBackend> Backends => RenderBackend.All;

    /// <summary>The simulated printer state the panel drives (online, cover, paper, drawer, …).</summary>
    public PrinterState State => _printer.State;

    /// <summary>The last ESC/POS payload rendered, replayed when the engine is switched.</summary>
    public byte[] LastInput { get; private set; } = Array.Empty<byte>();

    /// <summary>Non-empty receipts produced by the current input, top to bottom.</summary>
    public IReadOnlyList<Receipt> Receipts => _printer.ReceiptStack.Where(r => !r.IsEmpty).ToList();

    /// <summary>Switches the render engine and re-renders the current input through it.</summary>
    public void UseBackend(string id)
    {
        var next = RenderBackend.ById(id);
        if (next.Id == _backend.Id)
            return;
        _backend = next;
        Rebuild(preserveState: true);
    }

    /// <summary>Renders an ESC/POS payload from a clean slate (state is preserved).</summary>
    public void Render(byte[] escpos)
    {
        LastInput = escpos ?? Array.Empty<byte>();
        ResetReceipts();
        _printer.FeedEscPos(LastInput);
        Changed?.Invoke();
    }

    /// <summary>Clears all receipts and the buffered input.</summary>
    public void Clear()
    {
        LastInput = Array.Empty<byte>();
        ResetReceipts();
        Changed?.Invoke();
    }

    /// <summary>
    /// Feeds bytes from a live transport into the current printer <b>without</b> resetting the receipt
    /// stack — a connected device streams a continuous session, exactly like the desktop transports.
    /// <paramref name="responder"/> is the transport itself, so status replies go back over the same link.
    /// </summary>
    public void FeedLive(byte[] data, IPrinterResponder responder)
    {
        _printer.FeedEscPos(data, responder);
        Changed?.Invoke();
    }

    /// <summary>Registers a transport as a long-lived responder (also receives Automatic Status Back).</summary>
    public void AttachResponder(IPrinterResponder responder)
    {
        if (_liveResponders.Contains(responder))
            return;
        _liveResponders.Add(responder);
        _printer.RegisterResponder(responder);
    }

    /// <summary>Unregisters a transport responder.</summary>
    public void DetachResponder(IPrinterResponder responder)
    {
        _liveResponders.Remove(responder);
        _printer.UnregisterResponder(responder);
    }

    /// <summary>Renders a single receipt to a base64 PNG for an <c>&lt;img&gt;</c> data URI.</summary>
    public string RenderPngBase64(Receipt receipt)
    {
        using var image = receipt.Render();
        return Convert.ToBase64String(_encoder.EncodePng(image));
    }

    private void ResetReceipts()
    {
        _printer.ReceiptStack.Clear();
        _printer.Initialize();
        _printer.StartNewReceipt();
    }

    private void Rebuild(bool preserveState = false)
    {
        PrinterState? previous = preserveState ? _printer?.State : null;

        _encoder = _backend.CreateEncoder();
        _printer = new ReceiptPrinter(
            PaperConfiguration.Default,
            _backend.CreateImageFactory(),
            _backend.CreateTypefaces());

        if (previous is not null)
            CopyState(previous, _printer.State);

        _printer.OnActivityEvent += (_, _) => Changed?.Invoke();
        _printer.State.Changed += () => Changed?.Invoke();

        // Re-attach any live transports to the freshly built printer.
        foreach (var responder in _liveResponders)
            _printer.RegisterResponder(responder);

        if (LastInput.Length > 0)
            _printer.FeedEscPos(LastInput);

        Changed?.Invoke();
    }

    private static void CopyState(PrinterState from, PrinterState to)
    {
        to.Online = from.Online;
        to.CoverOpen = from.CoverOpen;
        to.Paper = from.Paper;
        to.DrawerOpen = from.DrawerOpen;
        to.Error = from.Error;
        to.FeedButtonPressed = from.FeedButtonPressed;
    }
}

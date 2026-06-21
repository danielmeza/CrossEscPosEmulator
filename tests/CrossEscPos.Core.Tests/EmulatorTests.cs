using System.Collections.Generic;
using System.Linq;
using CrossEscPos;
using CrossEscPos.Emulator;
using Xunit;

namespace CrossEscPos.Core.Tests;

/// <summary>
/// Exercises the emulator core against the <see cref="FakeImageFactory"/> — i.e. with no graphics
/// backend at all. These tests would not compile or run if Core had a hard dependency on SkiaSharp.
/// </summary>
public class EmulatorTests
{
    private static ReceiptPrinter NewPrinter(out FakeImageFactory factory)
    {
        factory = new FakeImageFactory();
        return new ReceiptPrinter(PaperConfiguration.Default, factory, new FakeTypefaceProvider());
    }

    [Fact]
    public void PlainText_ProducesNonEmptyReceipt()
    {
        var printer = NewPrinter(out _);

        printer.FeedEscPos("Hello world\n");

        Assert.Contains(printer.ReceiptStack, r => !r.IsEmpty);
    }

    [Fact]
    public void Core_RendersThroughAbstraction_WithNoSkia()
    {
        var printer = NewPrinter(out var factory);
        printer.FeedEscPos("Hello world\n");

        using var image = printer.CurrentReceipt.Render();

        // Rendered onto the fake backend at the paper's pixel width, and text was actually drawn.
        Assert.Equal(PaperConfiguration.Default.GetPaperWidthInPixels(), image.Width);
        Assert.True(image.Height > 0);
        Assert.True(factory.Canvases.Sum(c => c.TextDraws) > 0, "expected text to be drawn");
    }

    [Fact]
    public void Cut_StartsNewReceipt()
    {
        var printer = NewPrinter(out _);

        printer.FeedEscPos("First\n");
        printer.Cut();
        printer.FeedEscPos("Second\n");

        Assert.Equal(2, printer.ReceiptStack.Count(r => !r.IsEmpty));
    }

    [Fact]
    public void Offline_DropsPrint()
    {
        var printer = NewPrinter(out _);
        printer.State.Online = false;

        printer.FeedEscPos("Should not print\n");

        Assert.DoesNotContain(printer.ReceiptStack, r => !r.IsEmpty);
    }

    [Fact]
    public void PrinterState_RaisesChanged()
    {
        var printer = NewPrinter(out _);
        var raised = false;
        printer.State.Changed += () => raised = true;

        printer.State.CoverOpen = true;

        Assert.True(raised);
    }

    [Fact]
    public void AutomaticStatusBack_BroadcastsToResponder()
    {
        var printer = NewPrinter(out _);
        var responder = new CapturingResponder();
        printer.RegisterResponder(responder);

        printer.SetAutoStatusBack(0x01); // enable -> sends current status immediately

        Assert.NotEmpty(responder.Received);
    }

    private sealed class CapturingResponder : IPrinterResponder
    {
        public List<byte[]> Received { get; } = new();
        public void Send(byte[] data) => Received.Add(data);
    }
}

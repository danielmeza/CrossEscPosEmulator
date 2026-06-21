using System.Collections.Generic;
using System.Linq;
using CrossEscPos;
using CrossEscPos.Emulator;
using CrossEscPos.Emulator.Enums;
using Xunit;
using static CrossEscPos.Core.Tests.EscPosSequence;

namespace CrossEscPos.Core.Tests;

/// <summary>
/// End-to-end coverage of the ESC/POS interpreter and command set: raw byte sequences are fed through
/// <see cref="ReceiptPrinter.FeedEscPos(string, IPrinterResponder?)"/> and the observable results
/// (rendered draws, printer state, host responses, events) are asserted. Rendering goes to the fake
/// backend, so these tests exercise the whole core with no SkiaSharp.
/// </summary>
public class EscPosInterpretationTests
{
    private readonly FakeImageFactory _factory = new();
    private readonly FakeTypefaceProvider _typefaces = new();
    private readonly ReceiptPrinter _printer;

    public EscPosInterpretationTests()
        => _printer = new ReceiptPrinter(PaperConfiguration.Default, _factory, _typefaces);

    /// <summary>Renders the current receipt and returns the canvas it drew onto.</summary>
    private FakeCanvas RenderReceipt()
    {
        using var _ = _printer.CurrentReceipt.Render();
        return _factory.Canvases[^1];
    }

    private sealed class CapturingResponder : IPrinterResponder
    {
        public List<byte> Bytes { get; } = new();
        public void Send(byte[] data) => Bytes.AddRange(data);
    }

    // ---- text & control characters -------------------------------------------------------------

    [Fact]
    public void PlainText_DrawsText()
    {
        _printer.FeedEscPos("Hello" + Bytes(LF));
        Assert.Contains("Hello", RenderReceipt().DrawnText);
    }

    [Fact]
    public void LineFeed_AndCarriageReturn_BothEndLines()
    {
        _printer.FeedEscPos("A" + Bytes(LF) + "B" + Bytes(CR));
        var canvas = RenderReceipt();
        Assert.Contains("A", canvas.DrawnText);
        Assert.Contains("B", canvas.DrawnText);
    }

    [Fact]
    public void HorizontalTab_EmitsSpaces()
    {
        _printer.FeedEscPos(Bytes(HT) + "x" + Bytes(LF));
        // The tab expands to spaces before the 'x' on the same line.
        Assert.Contains(RenderReceipt().DrawnText, t => t.Contains(' '));
    }

    [Fact]
    public void NulByte_IsIgnored()
    {
        _printer.FeedEscPos(Bytes(NUL, NUL) + "x" + Bytes(LF));
        Assert.Contains("x", RenderReceipt().DrawnText);
    }

    // ---- text styling --------------------------------------------------------------------------

    [Fact]
    public void Emphasize_RequestsBoldFont()
    {
        _printer.FeedEscPos(Bytes(ESC, 'E', 1) + "x" + Bytes(LF));
        RenderReceipt();
        Assert.Contains(_typefaces.Requests, r => r.bold);
    }

    [Fact]
    public void EmphasizeOff_RequestsNonBoldFont()
    {
        _printer.FeedEscPos(Bytes(ESC, 'E', 1, ESC, 'E', 0) + "x" + Bytes(LF));
        RenderReceipt();
        Assert.All(_typefaces.Requests, r => Assert.False(r.bold));
    }

    [Fact]
    public void Italic_RequestsItalicFont()
    {
        _printer.FeedEscPos(Bytes(ESC, '4') + "x" + Bytes(LF)); // ESC 4 = italic on
        RenderReceipt();
        Assert.Contains(_typefaces.Requests, r => r.italic);
    }

    [Fact]
    public void Initialize_ClearsEmphasis()
    {
        _printer.FeedEscPos(Bytes(ESC, 'E', 1, ESC, '@') + "x" + Bytes(LF));
        RenderReceipt();
        Assert.All(_typefaces.Requests, r => Assert.False(r.bold));
    }

    [Fact]
    public void CharacterSize_AppliesScale()
    {
        _printer.FeedEscPos(Bytes(GS, '!', 0x11) + "x" + Bytes(LF)); // width x2, height x2
        Assert.Contains(RenderReceipt().Scales, s => s.sx == 2 && s.sy == 2);
    }

    [Theory]
    [InlineData(2)]   // numeric
    [InlineData(50)]  // ASCII '2'
    public void Justification_Right_ShiftsTextFartherThanLeft(int rightCode)
    {
        _printer.FeedEscPos("AAA" + Bytes(LF) +                 // left (default)
                            Bytes(ESC, 'a', rightCode) + "AAA" + Bytes(LF)); // right
        // Each text line translates to its justified x before drawing; right starts farther right.
        var dx = RenderReceipt().Translates.Select(t => t.dx).ToList();
        Assert.True(dx.Count >= 2);
        Assert.True(dx[1] > dx[0], "right-justified text should start farther right than left-justified");
    }

    // ---- cutting & feeding ---------------------------------------------------------------------

    [Fact]
    public void GsVCut_StartsNewReceipt()
    {
        _printer.FeedEscPos("First" + Bytes(LF, GS, 'V', 0) + "Second" + Bytes(LF));
        Assert.Equal(2, _printer.ReceiptStack.Count(r => !r.IsEmpty));
    }

    [Fact]
    public void EscMCut_StartsNewReceipt()
    {
        _printer.FeedEscPos("First" + Bytes(LF, ESC, 'm') + "Second" + Bytes(LF));
        Assert.Equal(2, _printer.ReceiptStack.Count(r => !r.IsEmpty));
    }

    [Fact]
    public void FeedNLines_AddsContent()
    {
        _printer.FeedEscPos(Bytes(ESC, 'd', 3));
        Assert.False(_printer.CurrentReceipt.IsEmpty);
    }

    // ---- barcodes & images ---------------------------------------------------------------------

    [Fact]
    public void Barcode_FunctionA_DrawsImageAndModules()
    {
        _printer.FeedEscPos(Barcode(4 /*CODE39*/, "ABC123")); // m=4 -> CODE39 Function A
        Assert.True(RenderReceipt().ImageDraws > 0, "barcode should be placed on the receipt");
        Assert.Contains(_factory.Canvases, c => c.RectDraws > 0); // modules were drawn
    }

    [Fact]
    public void QrSymbol_StoreAndPrint_DrawsImage()
    {
        _printer.FeedEscPos(Symbol2D(49 /*QR*/, "https://example.com"));
        Assert.True(RenderReceipt().ImageDraws > 0);
    }

    [Fact]
    public void DataMatrixSymbol_DrawsImage()
    {
        _printer.FeedEscPos(Symbol2D(54 /*DataMatrix*/, "DM-DATA"));
        Assert.True(RenderReceipt().ImageDraws > 0);
    }

    [Fact]
    public void RasterBitImage_DrawsImage()
    {
        _printer.FeedEscPos(Raster());
        Assert.True(RenderReceipt().ImageDraws > 0);
    }

    [Fact]
    public void BitImageMode_DrawsImage()
    {
        _printer.FeedEscPos(Bytes(ESC, '*', 0, 1, 0, 0xFF)); // ESC * m=0 nL=1 nH=0 + 1 byte
        Assert.True(RenderReceipt().ImageDraws > 0);
    }

    // ---- status / transmit-back ----------------------------------------------------------------

    [Fact]
    public void DleEot_PrinterStatus_ReflectsOnlineState()
    {
        var responder = new CapturingResponder();
        _printer.FeedEscPos(Bytes(DLE, EOT, 1), responder);
        Assert.Equal(new byte[] { StatusByteBuilder.PrinterStatus(_printer.State) }, responder.Bytes);
    }

    [Fact]
    public void DleEot_PrinterStatus_ReportsOffline()
    {
        _printer.State.Online = false;
        var responder = new CapturingResponder();
        _printer.FeedEscPos(Bytes(DLE, EOT, 1), responder);
        Assert.Equal(new byte[] { 0x1A }, responder.Bytes); // fixed 0x12 | offline 0x08
    }

    [Fact]
    public void GsR_PaperStatus_ReportsPaperOut()
    {
        _printer.State.Paper = PaperLevel.Out;
        var responder = new CapturingResponder();
        _printer.FeedEscPos(Bytes(GS, 'r', 1), responder);
        Assert.Equal(new byte[] { StatusByteBuilder.TransmitPaperStatus(_printer.State) }, responder.Bytes);
    }

    [Fact]
    public void GsI_PrinterId_ReturnsModelByte()
    {
        var responder = new CapturingResponder();
        _printer.FeedEscPos(Bytes(GS, 'I', 1), responder);
        Assert.Equal(new byte[] { 0x02 }, responder.Bytes);
    }

    [Fact]
    public void GsA_AutomaticStatusBack_BroadcastsFourBytes()
    {
        var responder = new CapturingResponder();
        _printer.RegisterResponder(responder);
        _printer.FeedEscPos(Bytes(GS, 'a', 0xFF));
        Assert.Equal(4, responder.Bytes.Count);
    }

    // ---- peripherals & real-time ---------------------------------------------------------------

    [Fact]
    public void Buzzer_Bell_RaisesEvent()
    {
        var buzzed = false;
        _printer.OnBuzzer += () => buzzed = true;
        _printer.FeedEscPos(Bytes(BEL));
        Assert.True(buzzed);
    }

    [Fact]
    public void CashDrawer_EscP_OpensDrawerAndRaisesEvent()
    {
        var kicked = false;
        _printer.OnCashDrawer += () => kicked = true;
        _printer.FeedEscPos(Bytes(ESC, 'p', 0, 50, 50)); // ESC p m t1 t2
        Assert.True(kicked);
        Assert.True(_printer.State.DrawerOpen);
    }

    [Fact]
    public void CashDrawer_RealtimeDc4_OpensDrawer()
    {
        _printer.FeedEscPos(Bytes(DLE, DC4, 1, 0, 0)); // DLE DC4 fn=1 m t
        Assert.True(_printer.State.DrawerOpen);
    }

    [Fact]
    public void RealtimeRecover_DleEnq_ClearsRecoverableError()
    {
        _printer.State.Error = PrinterErrorState.Recoverable;
        _printer.FeedEscPos(Bytes(DLE, ENQ));
        Assert.Equal(PrinterErrorState.None, _printer.State.Error);
    }

    // ---- code pages ----------------------------------------------------------------------------

    [Fact]
    public void CodePage_EscT_RemapsHighBytes()
    {
        // PC850 (table 2): byte 0x80 is 'Ç' (U+00C7).
        _printer.FeedEscPos(Bytes(ESC, 't', 2, 0x80) + Bytes(LF));
        Assert.Contains(RenderReceipt().DrawnText, t => t.Contains('Ç'));
    }

    // ---- page mode -----------------------------------------------------------------------------

    [Fact]
    public void PageMode_EscL_Ff_FlushesPageAsImage()
    {
        _printer.FeedEscPos(Bytes(ESC, 'L') + "page" + Bytes(LF, FF));
        Assert.True(RenderReceipt().ImageDraws > 0); // the page is rasterized onto the receipt
    }

    [Fact]
    public void PageMode_EscS_DiscardsBufferedPage()
    {
        _printer.FeedEscPos(Bytes(ESC, 'L') + "page" + Bytes(ESC, 'S'));
        Assert.True(_printer.CurrentReceipt.IsEmpty);
    }

    // ---- not-ready handling --------------------------------------------------------------------

    [Theory]
    [InlineData("offline")]
    [InlineData("paper")]
    [InlineData("cover")]
    public void Print_IsDropped_WhenNotReady(string condition)
    {
        switch (condition)
        {
            case "offline": _printer.State.Online = false; break;
            case "paper": _printer.State.Paper = PaperLevel.Out; break;
            case "cover": _printer.State.CoverOpen = true; break;
        }

        string? blockedReason = null;
        _printer.OnPrintBlocked += r => blockedReason = r;

        _printer.FeedEscPos("should not print" + Bytes(LF));

        Assert.True(_printer.CurrentReceipt.IsEmpty);
        Assert.NotNull(blockedReason);
    }

    // ---- robustness ----------------------------------------------------------------------------

    [Fact]
    public void UnsupportedCommand_DoesNotThrow()
    {
        // An unknown ESC sequence: the interpreter logs and recovers rather than crashing the feed.
        var ex = Record.Exception(() => _printer.FeedEscPos(Bytes(ESC, 0x99, 0x99) + "after" + Bytes(LF)));
        Assert.Null(ex);
    }

    [Fact]
    public void FeedEscPos_RaisesActivityEvent()
    {
        int activity = 0;
        _printer.OnActivityEvent += (_, _) => activity++;
        _printer.FeedEscPos("x" + Bytes(LF));
        Assert.Equal(1, activity);
    }

    [Fact]
    public void FeedEscPos_ByteArray_RendersSameAsString()
    {
        byte[] data = System.Text.Encoding.Latin1.GetBytes("Bytes" + Bytes(LF));
        _printer.FeedEscPos(data); // the byte[] overload — ESC/POS is binary
        Assert.Contains("Bytes", RenderReceipt().DrawnText);
    }
}

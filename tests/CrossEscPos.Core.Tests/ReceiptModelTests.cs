using CrossEscPos.Emulator;
using CrossEscPos.Emulator.Enums;
using Xunit;

namespace CrossEscPos.Core.Tests;

/// <summary>Coverage for the receipt document model and the supporting value types.</summary>
public class ReceiptModelTests
{
    private static Receipt NewReceipt() =>
        new(PaperConfiguration.Default, new PrintMode(), lineSpacing: 10, new FakeImageFactory(), new FakeTypefaceProvider());

    [Fact]
    public void NewReceipt_IsEmpty()
        => Assert.True(NewReceipt().IsEmpty);

    [Fact]
    public void PrintText_MakesReceiptNonEmpty()
    {
        var receipt = NewReceipt();
        receipt.PrintText("hello", new PrintMode());
        Assert.False(receipt.IsEmpty);
    }

    [Fact]
    public void Render_UsesPaperWidth_AndAccountsForContent()
    {
        var receipt = NewReceipt();
        using var empty = receipt.Render();
        receipt.PrintText("line", new PrintMode());
        receipt.AdvanceToNewLine();
        using var withText = receipt.Render();

        Assert.Equal(PaperConfiguration.Default.GetPaperWidthInPixels(), withText.Width);
        Assert.True(withText.Height >= empty.Height);
    }

    [Fact]
    public void PrintBitmap_AddsImageLine()
    {
        var factory = new FakeImageFactory();
        var receipt = new Receipt(PaperConfiguration.Default, new PrintMode(), 10, factory, new FakeTypefaceProvider());
        receipt.PrintBitmap(factory.Create(48, 48, default));

        using var _ = receipt.Render();
        Assert.Contains(factory.Canvases, c => c.ImageDraws > 0);
    }

    [Fact]
    public void PrinterState_Changed_FiresOncePerMutation()
    {
        var state = new PrinterState();
        int changes = 0;
        state.Changed += () => changes++;

        state.Online = false;
        state.Paper = PaperLevel.Out;

        Assert.Equal(2, changes);
    }

    [Fact]
    public void PaperConfiguration_PixelWidths_DeriveFromMillimetres()
    {
        var config = PaperConfiguration.Default;
        // 80mm @ 203 dpi ≈ 640 dots; print width is narrower than paper width.
        Assert.True(config.GetPaperWidthInPixels() > config.GetPrintWidthInPixels());
        Assert.InRange(config.GetPaperWidthInPixels(), 600, 700);
    }

    [Fact]
    public void PrintMode_Clone_IsIndependentButEqual()
    {
        var mode = new PrintMode { Emphasize = true, Justification = TextJustification.Center };
        var clone = mode.Clone();

        Assert.Equal(mode, clone);
        clone.Emphasize = false;
        Assert.NotEqual(mode, clone);
    }
}

using CrossEscPos.Emulator;
using CrossEscPos.Emulator.Enums;
using Xunit;

namespace CrossEscPos.Core.Tests;

/// <summary>Exact bit-layout coverage for the ESC/POS status bytes (DLE EOT, GS r, GS a).</summary>
public class StatusByteBuilderTests
{
    private const byte Fixed = 0x12; // bits 1 + 4, always set in the single-byte responses

    private static PrinterState State(
        bool online = true, bool cover = false, PaperLevel paper = PaperLevel.Adequate,
        bool drawer = false, PrinterErrorState error = PrinterErrorState.None, bool feed = false)
        => new() { Online = online, CoverOpen = cover, Paper = paper, DrawerOpen = drawer, Error = error, FeedButtonPressed = feed };

    [Fact]
    public void PrinterStatus_Default_IsFixedBitsOnly()
        => Assert.Equal(Fixed, StatusByteBuilder.PrinterStatus(State()));

    [Fact]
    public void PrinterStatus_DrawerOpen_SetsBit2()
        => Assert.Equal((byte)(Fixed | 0x04), StatusByteBuilder.PrinterStatus(State(drawer: true)));

    [Fact]
    public void PrinterStatus_Offline_SetsBit3()
        => Assert.Equal((byte)(Fixed | 0x08), StatusByteBuilder.PrinterStatus(State(online: false)));

    [Fact]
    public void OfflineStatus_CoverOpen_SetsBit2()
        => Assert.Equal((byte)(Fixed | 0x04), StatusByteBuilder.OfflineStatus(State(cover: true)));

    [Fact]
    public void OfflineStatus_PaperOut_SetsBit5()
        => Assert.Equal((byte)(Fixed | 0x20), StatusByteBuilder.OfflineStatus(State(paper: PaperLevel.Out)));

    [Fact]
    public void OfflineStatus_Error_SetsBit6()
        => Assert.Equal((byte)(Fixed | 0x40), StatusByteBuilder.OfflineStatus(State(error: PrinterErrorState.Recoverable)));

    [Fact]
    public void ErrorStatus_Unrecoverable_SetsBit5()
        => Assert.Equal((byte)(Fixed | 0x20), StatusByteBuilder.ErrorStatus(State(error: PrinterErrorState.Unrecoverable)));

    [Fact]
    public void ErrorStatus_Recoverable_SetsBit6()
        => Assert.Equal((byte)(Fixed | 0x40), StatusByteBuilder.ErrorStatus(State(error: PrinterErrorState.Recoverable)));

    [Fact]
    public void PaperSensorStatus_NearEnd_SetsBits2And3()
        => Assert.Equal((byte)(Fixed | 0x0C), StatusByteBuilder.PaperSensorStatus(State(paper: PaperLevel.NearEnd)));

    [Fact]
    public void PaperSensorStatus_Out_SetsNearEndAndEndBits()
        => Assert.Equal((byte)(Fixed | 0x0C | 0x60), StatusByteBuilder.PaperSensorStatus(State(paper: PaperLevel.Out)));

    [Theory]
    [InlineData(PaperLevel.Adequate, 0x00)]
    [InlineData(PaperLevel.NearEnd, 0x03)]
    [InlineData(PaperLevel.Out, 0x0F)]
    public void TransmitPaperStatus_MapsPaperLevel(PaperLevel paper, int expected)
        => Assert.Equal((byte)expected, StatusByteBuilder.TransmitPaperStatus(State(paper: paper)));

    [Theory]
    [InlineData(false, 0x00)]
    [InlineData(true, 0x01)]
    public void TransmitDrawerStatus_MapsDrawer(bool open, int expected)
        => Assert.Equal((byte)expected, StatusByteBuilder.TransmitDrawerStatus(State(drawer: open)));

    [Fact]
    public void AutoStatusBack_Default_HasDrawerClosedBitSet()
    {
        var asb = StatusByteBuilder.AutoStatusBack(State());
        Assert.Equal(4, asb.Length);
        Assert.Equal(0x14, asb[0]); // bit4 fixed + bit2 (drawer closed)
        Assert.Equal(0x00, asb[1]);
    }

    [Fact]
    public void AutoStatusBack_Offline_SetsBit3()
        => Assert.Equal(0x14 | 0x08, StatusByteBuilder.AutoStatusBack(State(online: false))[0]);
}

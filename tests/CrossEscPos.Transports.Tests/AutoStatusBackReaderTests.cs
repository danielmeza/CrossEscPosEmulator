using System;
using System.Collections.Generic;
using CrossEscPos.Emulator;
using CrossEscPos.Emulator.Enums;
using Xunit;

namespace CrossEscPos.Transports.Tests;

/// <summary>
/// Framing and bit-layout coverage for the Automatic Status Back reader — the decode a host does on
/// the printer's status channel, and the exact inverse of
/// <see cref="StatusByteBuilder.AutoStatusBack"/>.
/// </summary>
/// <remarks>
/// These cover <c>CrossEscPos.Core</c> and belong in <c>CrossEscPos.Core.Tests</c>; they live here
/// because that project cannot currently be built — it references
/// <c>CrossEscPos.Rendering.ImageSharp</c>, whose ImageSharp 3.1.12 pin fails the repository's
/// warnings-as-errors NuGet audit. Move them once that is resolved.
/// </remarks>
public class AutoStatusBackReaderTests
{
    private static readonly bool[] Bools = { false, true };

    /// <summary>Byte 0 with only the block's fixed bit set — an otherwise all-clear status.</summary>
    private const byte Byte0Fixed = 0x10;

    #region Round trip against the emulator

    [Fact]
    public void Parse_IsTheInverseOfAutoStatusBack_ForEveryPrinterState()
    {
        var mismatches = new List<string>();

        foreach (bool online in Bools)
            foreach (bool cover in Bools)
                foreach (var paper in Enum.GetValues<PaperLevel>())
                    foreach (bool drawer in Bools)
                        foreach (var error in Enum.GetValues<PrinterErrorState>())
                            foreach (bool feed in Bools)
                            {
                                var state = new PrinterState
                                {
                                    Online = online,
                                    CoverOpen = cover,
                                    Paper = paper,
                                    DrawerOpen = drawer,
                                    Error = error,
                                    FeedButtonPressed = feed,
                                };

                                var expected = new PrinterStatusReport(
                                    Online: online,
                                    PaperOut: paper == PaperLevel.Out,
                                    PaperLow: paper == PaperLevel.NearEnd,
                                    CoverOpen: cover,
                                    DrawerOpen: drawer,
                                    Error: error != PrinterErrorState.None);

                                var actual = AutoStatusBackReader.Parse(StatusByteBuilder.AutoStatusBack(state));

                                if (actual != expected)
                                    mismatches.Add(
                                        $"online={online} cover={cover} paper={paper} drawer={drawer} " +
                                        $"error={error} feed={feed}: expected {expected}, got " +
                                        (actual?.ToString() ?? "null"));
                            }

        Assert.True(mismatches.Count == 0,
            $"{mismatches.Count} state(s) did not round-trip:{Environment.NewLine}" +
            string.Join(Environment.NewLine, mismatches));
    }

    #endregion

    #region Rejecting what is not a block

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(8)]
    public void Parse_RejectsAnythingThatIsNotExactlyOneBlock(int length)
    {
        // Byte 0 is a valid block header wherever there is room for one, so the length is the only
        // thing that can make these invalid — otherwise the over-length rows would pass for the
        // wrong reason.
        var buffer = new byte[length];
        if (length > 0)
            buffer[0] = Byte0Fixed;

        Assert.Null(AutoStatusBackReader.Parse(buffer));
    }

    [Theory]
    [InlineData(0x00)] // bit 4 clear
    [InlineData(0x11)] // bit 0 set
    [InlineData(0x12)] // bits 1 + 4 — the DLE EOT / GS r fixed pattern, which is not an ASB block
    [InlineData(0x90)] // bit 7 set
    public void Parse_RejectsAByteZeroWithoutTheBlocksFixedBits(byte byte0)
        => Assert.Null(AutoStatusBackReader.Parse(new byte[] { byte0, 0x00, 0x00, 0x00 }));

    #endregion

    #region Bit layout

    [Fact]
    public void Parse_ReadsByteZero()
    {
        Assert.False(Parse(Byte0Fixed | 0x08).Online);     // bit 3 set = offline
        Assert.True(Parse(Byte0Fixed).Online);
        Assert.True(Parse(Byte0Fixed | 0x20).CoverOpen);   // bit 5
        Assert.False(Parse(Byte0Fixed).CoverOpen);

        // The drawer bit is inverted: set means the drawer is closed.
        Assert.True(Parse(Byte0Fixed).DrawerOpen);
        Assert.False(Parse(Byte0Fixed | 0x04).DrawerOpen);

        static PrinterStatusReport Parse(int byte0)
            => AutoStatusBackReader.Parse(new byte[] { (byte)byte0, 0x00, 0x00, 0x00 })!;
    }

    [Fact]
    public void Parse_FoldsAllFourErrorBitsIntoTheErrorFlag()
    {
        // ESC-POS-.NET's IsInErrorState was the OR of four byte-1 bits: recoverable (6),
        // unrecoverable (5), autocutter (3) and recoverable-non-autocutter (2). The emulator only
        // ever sets 5 and 6, but a real printer sets the other two, so all four are honoured.
        foreach (int bit in new[] { 2, 3, 5, 6 })
            Assert.True(Parse(1 << bit).Error, $"byte 1 bit {bit} should read as an error");

        // Bit 1 is "feed button pushed" and bit 4 is fixed — neither is an error.
        Assert.False(Parse(0b0001_0010).Error);

        static PrinterStatusReport Parse(int byte1)
            => AutoStatusBackReader.Parse(new byte[] { Byte0Fixed, (byte)byte1, 0x00, 0x00 })!;
    }

    [Fact]
    public void Parse_TreatsThePaperSensorBitsAsPairs()
    {
        // The Epson layout duplicates each sensor reading across two bits; a lone bit is not a
        // reading, so it is not reported as one.
        Assert.False(Parse(0b0000_0001).PaperLow);
        Assert.False(Parse(0b0000_0010).PaperLow);
        Assert.True(Parse(0b0000_0011).PaperLow);

        Assert.False(Parse(0b0000_0100).PaperOut);
        Assert.False(Parse(0b0000_1000).PaperOut);
        Assert.True(Parse(0b0000_1100).PaperOut);

        static PrinterStatusReport Parse(int byte2)
            => AutoStatusBackReader.Parse(new byte[] { Byte0Fixed, 0x00, (byte)byte2, 0x00 })!;
    }

    [Fact]
    public void Parse_LetsPaperOutWinOverPaperLow()
    {
        // The emulator sets the near-end pair as well once the roll is out, and every consumer reads
        // PaperLow only when PaperOut is false.
        var report = AutoStatusBackReader.Parse(new byte[] { Byte0Fixed, 0x00, 0b0000_1111, 0x00 })!;

        Assert.True(report.PaperOut);
        Assert.False(report.PaperLow);
    }

    [Fact]
    public void Parse_IgnoresByteThree()
    {
        // Byte 3 of the block carries nothing this reader uses; a value there must not change the
        // decode or get the block rejected.
        var plain = AutoStatusBackReader.Parse(new byte[] { Byte0Fixed, 0x00, 0x00, 0x00 });
        var noisy = AutoStatusBackReader.Parse(new byte[] { Byte0Fixed, 0x00, 0x00, 0xFF });

        Assert.Equal(plain, noisy);
    }

    #endregion

    #region Framing

    [Fact]
    public void Feed_EmitsOneFramePerFourBytes()
    {
        var frames = new List<byte[]>();
        new AutoStatusBackReader().Feed(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, frames.Add);

        Assert.Equal(2, frames.Count);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, frames[0]);
        Assert.Equal(new byte[] { 5, 6, 7, 8 }, frames[1]);
    }

    [Fact]
    public void Feed_HoldsBackAPartialFrameUntilItCompletes()
    {
        var reader = new AutoStatusBackReader();
        var frames = new List<byte[]>();

        reader.Feed(new byte[] { 1, 2, 3 }, frames.Add);
        Assert.Empty(frames);

        reader.Feed(new byte[] { 4, 5 }, frames.Add);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, Assert.Single(frames));

        reader.Feed(new byte[] { 6, 7, 8 }, frames.Add);
        Assert.Equal(2, frames.Count);
        Assert.Equal(new byte[] { 5, 6, 7, 8 }, frames[1]);
    }

    [Fact]
    public void Feed_HandsOutFramesThatOutliveTheNextOne()
    {
        // The reader fills one buffer repeatedly; a caller that keeps a frame must not watch it
        // change under them when the next block arrives.
        var frames = new List<byte[]>();
        new AutoStatusBackReader().Feed(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, frames.Add);

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, frames[0]);
        Assert.NotSame(frames[0], frames[1]);
    }

    [Fact]
    public void Feed_DoesNothingWithNoBytes()
    {
        var frames = new List<byte[]>();
        new AutoStatusBackReader().Feed(ReadOnlySpan<byte>.Empty, frames.Add);

        Assert.Empty(frames);
    }

    [Fact]
    public void Feed_RejectsANullCallback()
        => Assert.Throws<ArgumentNullException>(
            () => new AutoStatusBackReader().Feed(new byte[] { 1 }, null!));

    #endregion
}

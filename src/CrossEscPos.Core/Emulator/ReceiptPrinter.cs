using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CrossEscPos;
using CrossEscPos.Graphics;
using CrossEscPos.Emulator.Enums;
using CrossEscPos.Emulator.Rendering;
using CrossEscPos.EscPos;
using CrossEscPos.Logging;
using QRCoder;
using ZXing;

namespace CrossEscPos.Emulator;

public class ReceiptPrinter
{
    private readonly PaperConfiguration _paperConfiguration;
    private readonly EscPosInterpreter _escPosInterpreter;
    private readonly ITypefaceProvider _typefaces;
    private readonly BarcodeRenderer _barcodeRenderer;

    /// <summary>The image backend used to rasterize receipts, barcodes and bit images.</summary>
    public IReceiptImageFactory ImageFactory { get; }

    private PrintMode _printMode;
    private int _lineSpacing;
    private int _tabSpacing;

    // Barcode (1D) state — ESC/POS GS h / GS w / GS H / GS f
    private int _barcodeHeight = 162;
    private int _barcodeModuleWidth = 3;
    private HriPosition _hriPosition = HriPosition.None;
    private PrinterFont _hriFont = PrinterFont.FontA;

    // 2D symbol state — ESC/POS GS ( k
    private int _qrModuleSize = 3;
    private QRCodeGenerator.ECCLevel _qrEcc = QRCodeGenerator.ECCLevel.M;
    private string _qrData = string.Empty;
    private TwoDimensionCode _2dSymbol = TwoDimensionCode.QrCode;

    public Receipt CurrentReceipt { get; private set; } = null!; // set via StartNewReceipt in ctor
    public List<Receipt> ReceiptStack { get; private set; }

    /// <summary>Simulated physical/logical printer state (drives status commands; driven by the UI).</summary>
    public PrinterState State { get; } = new();

    /// <summary>
    /// Marshals state mutations that originate off the UI thread (e.g. a drawer kick arriving over
    /// TCP) onto the UI thread, so bound controls update safely. The App wires this to the Avalonia
    /// dispatcher; the default runs synchronously (fine for tests/headless).
    /// </summary>
    public Action<Action> UiDispatch { get; set; } = static a => a();

    // Host response channel (status / transmit-back commands).
    private readonly List<IPrinterResponder> _responders = new();
    private IPrinterResponder? _currentResponder;
    private int _asbMask; // Automatic Status Back: 0 = disabled

    public event EventHandler<EventArgs>? OnActivityEvent;
    public event Action? OnBuzzer;
    public event Action? OnCashDrawer;

    /// <summary>Raised (with a human-readable reason) when a print operation is dropped because the
    /// printer isn't ready (out of paper, cover open, offline, error).</summary>
    public event Action<string>? OnPrintBlocked;

    private bool _blockedNotified;

    /// <summary>Whether the printer can currently put marks on paper, like a real device.</summary>
    public bool CanPrint => State.Online
                            && !State.CoverOpen
                            && State.Paper != PaperLevel.Out
                            && State.Error == PrinterErrorState.None;

    private string NotReadyReason() =>
        !State.Online ? "Printer offline"
        : State.CoverOpen ? "Cover is open"
        : State.Paper == PaperLevel.Out ? "Out of paper"
        : State.Error != PrinterErrorState.None ? $"Printer error ({State.Error})"
        : string.Empty;

    /// <summary>Returns true (and notifies once) when printing must be dropped due to printer state.</summary>
    private bool Blocked()
    {
        if (CanPrint)
            return false;

        if (!_blockedNotified)
        {
            _blockedNotified = true;
            var reason = NotReadyReason();
            Logger.Info($"Print blocked: {reason}");
            OnPrintBlocked?.Invoke(reason);
        }
        return true;
    }

    // Active character code table (ESC t). Default PC437; high bytes are remapped to Unicode.
    private Encoding _codePage = Encoding.Latin1;

    static ReceiptPrinter()
    {
        // Enable legacy code pages (437/850/852/858/866/1252…) on every platform.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public ReceiptPrinter(PaperConfiguration paperConfiguration, IReceiptImageFactory imageFactory,
        ITypefaceProvider typefaces)
    {
        _paperConfiguration = paperConfiguration;
        ImageFactory = imageFactory;
        _typefaces = typefaces;
        _barcodeRenderer = new BarcodeRenderer(imageFactory, typefaces);
        _escPosInterpreter = new(this);

        _printMode = new PrintMode();

        ReceiptStack = new();

        StartNewReceipt();

        PowerCycle();

        // Push Automatic Status Back to the host whenever the simulated state changes, and re-arm
        // the "print blocked" notification once the printer becomes ready again.
        State.Changed += () =>
        {
            if (CanPrint) _blockedNotified = false;
            if (_asbMask != 0) BroadcastStatus(StatusByteBuilder.AutoStatusBack(State));
        };
    }

    #region Host responses

    public void RegisterResponder(IPrinterResponder responder)
    {
        lock (_responders) _responders.Add(responder);
    }

    public void UnregisterResponder(IPrinterResponder responder)
    {
        lock (_responders) _responders.Remove(responder);
    }

    /// <summary>Sends bytes back to the host that issued the request currently being processed.</summary>
    public void SendResponse(byte[] data) => _currentResponder?.Send(data);

    public void SendResponse(byte value) => SendResponse(new[] { value });

    /// <summary>Sends bytes to every connected host (used by Automatic Status Back).</summary>
    public void BroadcastStatus(byte[] data)
    {
        IPrinterResponder[] targets;
        lock (_responders) targets = _responders.ToArray();
        foreach (var r in targets)
        {
            try { r.Send(data); } catch (Exception ex) { Logger.Exception(ex, "Status broadcast failed"); }
        }
    }

    /// <summary>Enables/disables Automatic Status Back (GS a). Sends the current status immediately when enabled.</summary>
    public void SetAutoStatusBack(int mask)
    {
        _asbMask = mask;
        Logger.Info($"Automatic Status Back: {(mask != 0 ? $"enabled (0x{mask:X2})" : "disabled")}");
        if (mask != 0)
            BroadcastStatus(StatusByteBuilder.AutoStatusBack(State));
    }

    #endregion

    #region ESC/POS

    /// <summary>
    /// When set, every received ESC/POS payload is dumped to disk (last_escpos_receive.txt, and
    /// last_ticket.bin for large payloads) for debugging. Enable via the ESCPOS_DEBUG_DUMP
    /// environment variable. Off by default to avoid writing files on every receive.
    /// </summary>
    public static bool DebugDumpEnabled { get; set; } =
        Environment.GetEnvironmentVariable("ESCPOS_DEBUG_DUMP") is "1" or "true";

    public void FeedEscPos(string ascii, IPrinterResponder? responder = null)
    {
        if (DebugDumpEnabled)
        {
            if (ascii.Length > 10000)
                File.WriteAllText("last_ticket.bin", ascii, Encoding.ASCII);
            File.WriteAllText("last_escpos_receive.txt", ascii, Encoding.ASCII);
        }

        _currentResponder = responder;
        _blockedNotified = false; // re-arm per received job so every blocked job notifies once
        try
        {
            Logger.Info($"Received: {ascii}");
            _escPosInterpreter.Interpret(ascii);
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "ESC/POS Interpreter Error");
        }
        finally
        {
            _currentResponder = null;
        }

        OnActivityEvent?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Receipt meta

    public void StartNewReceipt()
    {
        CurrentReceipt = new(_paperConfiguration, _printMode, _lineSpacing, ImageFactory, _typefaces);
        ReceiptStack.Add(CurrentReceipt);
        
        Logger.Info($"Starting new receipt (#{ReceiptStack.Count})");
    }

    #endregion

    #region Emulated

    public void PowerCycle()
    {
        Initialize();
    }

    #endregion

    #region Direct API

    public void Initialize()
    {
        _escPosInterpreter.ClearBuffers();
    
        SelectFont(PrinterFont.FontA);
        SelectJustification(TextJustification.Left);
        SelectCharacterSize(1, 1);
        SelectEmphasizeMode(false);
        SelectItalicMode(false);
        SelectUnderlineMode(UnderlineMode.Off);
        SetDefaultLineSpacing();
        SetDefaultTabSpacing();
    }

    public void PrintText(string text)
    {
        if (Blocked()) return;

        text = RemapCodePage(text);
        Logger.Info($"Print: {text}");

        CurrentReceipt.PrintText(text, _printMode);
    }

    /// <summary>Selects the character code table (ESC t n), mapping the ESC/POS table to a code page.</summary>
    public void SetCodePage(int table)
    {
        var codeTable = CharacterCodeTable.FromTableId(table);
        try
        {
            _codePage = Encoding.GetEncoding(codeTable.CodePage);
            Logger.Info($"Select character table {table} -> {codeTable.Name} (code page {codeTable.CodePage})");
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, $"Code page {codeTable.CodePage} unavailable; keeping current");
        }
    }

    /// <summary>Remaps high bytes (>=0x80) of received text through the active code page to Unicode.</summary>
    private string RemapCodePage(string text)
    {
        if (ReferenceEquals(_codePage, Encoding.Latin1))
            return text;

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch < 0x80)
                sb.Append(ch);
            else
            {
                var mapped = _codePage.GetString(new[] { (byte)ch });
                sb.Append(mapped.Length > 0 ? mapped : "?");
            }
        }
        return sb.ToString();
    }

    public void Cut(CutFunction cutFunction = CutFunction.Cut, CutShape cutShape = CutShape.Full, int n = 0)
    {
        Logger.Info($"Execute cut: {cutFunction}, {cutShape}, {n}");
        
        LineFeed();
        
        // TODO Support alternate cut modes
        
        StartNewReceipt();
    }

    /// <summary>
    /// Feeds one line, based on the current line spacing.
    /// </summary>
    /// <remarks>
    /// - The amount of paper fed per line is based on the value set using the line spacing command (ESC 2 or ESC 3).
    /// </remarks>
    public void LineFeed()
    {
        if (Blocked()) return; // don't advance paper (adds blank lines) while not ready
        Logger.Info($"Line feed");
        CurrentReceipt.AdvanceToNewLine();
    }

    public void SelectFont(PrinterFont printerFont)
    {
        Logger.Info($"Select font: {printerFont}");
        
        _printMode.Font = printerFont;
        CurrentReceipt.ChangeFontConfiguration(_printMode);
    }

    public void SelectJustification(TextJustification justification)
    {
        Logger.Info($"Select justification: {justification}");

        _printMode.Justification = justification;
        CurrentReceipt.ChangeFontConfiguration(_printMode);
    }

    public void SelectCharacterSize(int width, int height)
    {
        Logger.Info($"Set character size scale: x{width} width, x{height} height");

        _printMode.CharWidthScale = width;
        _printMode.CharHeightScale = height;
        CurrentReceipt.ChangeFontConfiguration(_printMode);
    }

    public void SelectEmphasizeMode(bool enable)
    {
        Logger.Info($"Set emphasize mode: {enable}");

        _printMode.Emphasize = enable;
        CurrentReceipt.ChangeFontConfiguration(_printMode);
    }

    public void SelectItalicMode(bool enable)
    {
        Logger.Info($"Set italic mode: {enable}");

        _printMode.Italic = enable;
        CurrentReceipt.ChangeFontConfiguration(_printMode);
    }

    public void SelectUnderlineMode(UnderlineMode mode)
    {
        Logger.Info($"Set underline mode: {mode}");

        _printMode.Underline = mode;
        CurrentReceipt.ChangeFontConfiguration(_printMode);
    }

    public void SetLineSpacing(int value)
    {
        Logger.Info($"Set line spacing: {value}");

        _lineSpacing = value;
        CurrentReceipt.SetLineSpacing(_lineSpacing);
    }

    public void SetTabSpacing(int value)
    {
        Logger.Info($"Set tab spacing: {value}");

        _tabSpacing = value;
        CurrentReceipt.SetTabSpacing(_tabSpacing);
    }

    public void SetDefaultLineSpacing() => SetLineSpacing(_paperConfiguration.DefaultLineSpacing);
    public void SetDefaultTabSpacing() => SetTabSpacing(_paperConfiguration.DefaultTabSpacing);

    public void PrintBitmap(IReceiptImage bitmap)
    {
        if (Blocked()) return;

        Logger.Info($"Print bitmap: {bitmap.Width}x{bitmap.Height}");

        CurrentReceipt.PrintBitmap(bitmap);
    }

    // User-defined characters (ESC & / % / ?). Captured/stored; inline glyph substitution during
    // text rendering is not applied (the font glyph is drawn). These rarely appear in modern streams.
    private readonly Dictionary<int, IReceiptImage> _userGlyphs = new();
    private bool _userDefinedEnabled;

    public void DefineUserGlyph(int code, IReceiptImage bmp)
    {
        Logger.Info($"Define user glyph 0x{code:X2} ({bmp.Width}x{bmp.Height})");
        _userGlyphs[code] = bmp;
    }

    public void EnableUserDefined(bool on)
    {
        _userDefinedEnabled = on;
        Logger.Info($"User-defined characters: {(on ? "enabled" : "disabled")}");
    }

    public void CancelUserGlyph(int code)
    {
        Logger.Info($"Cancel user glyph 0x{code:X2}");
        _userGlyphs.Remove(code);
    }

    private IReceiptImage? _downloadBitImage;

    /// <summary>Stores a downloaded bit image (GS * x y ...) for later printing by GS /.</summary>
    public void DefineDownloadBitImage(IReceiptImage bmp)
    {
        Logger.Info($"Define download bit image {bmp.Width}x{bmp.Height}");
        _downloadBitImage?.Dispose();
        _downloadBitImage = bmp;
    }

    /// <summary>Prints the stored downloaded bit image (GS / m) with the given scaling mode.</summary>
    public void PrintDownloadBitImage(int mode)
    {
        if (_downloadBitImage is null)
            return;
        if (Blocked()) return;

        int sx = mode is 1 or 3 ? 2 : 1; // double-width on modes 1,3
        int sy = mode is 2 or 3 ? 2 : 1; // double-height on modes 2,3

        if (sx == 1 && sy == 1)
        {
            CurrentReceipt.PrintBitmap(_downloadBitImage.Copy());
            return;
        }

        var scaled = ImageFactory.Create(_downloadBitImage.Width * sx, _downloadBitImage.Height * sy, ReceiptColor.White);
        using (var canvas = ImageFactory.CreateCanvas(scaled))
        {
            canvas.DrawImage(_downloadBitImage, ReceiptRect.Create(0, 0, scaled.Width, scaled.Height));
            canvas.Flush();
        }
        CurrentReceipt.PrintBitmap(scaled);
    }

    public void Buzz()
    {
        Logger.Info("Buzzer");
        OnBuzzer?.Invoke();
    }

    public void KickCashDrawer(int pin)
    {
        Logger.Info($"Cash drawer kick (pin {pin})");
        // The kick opens the drawer; the sensor reads open until closed. Marshal to the UI thread.
        UiDispatch(() => State.DrawerOpen = true);
        OnCashDrawer?.Invoke();
    }

    /// <summary>Transmits a real-time status byte (DLE EOT n) back to the requesting host.</summary>
    public void TransmitRealtimeStatus(int n)
    {
        var request = RealtimeStatusRequest.FromParameter(n);
        byte b = request.Build(State);
        Logger.Info($"DLE EOT {n} ({request.Name}) -> 0x{b:X2}");
        SendResponse(b);
    }

    /// <summary>Transmits status (GS r n) back to the requesting host.</summary>
    public void TransmitStatus(int n)
    {
        var kind = TransmitStatusKind.FromParameter(n);
        byte b = kind?.Build(State) ?? 0;
        Logger.Info($"GS r {n} ({kind?.Name ?? "unsupported"}) -> 0x{b:X2}");
        SendResponse(b);
    }

    /// <summary>Transmits a printer info / ID response (GS I n).</summary>
    public void TransmitPrinterId(int n)
    {
        switch (n)
        {
            case 1 or 49: SendResponse(0x02); break;             // printer model ID
            case 2 or 50: SendResponse(0x00); break;             // type ID
            case 3 or 51: SendResponse(0x01); break;             // ROM version ID
            default:
                // Info B responses (firmware/maker/model name): 0x5F <text> 0x00
                var name = n switch { 65 => "1.0", 66 => "CrossEscPos", 67 => "EMU-80", _ => "EMU" };
                var bytes = new List<byte> { 0x5F };
                bytes.AddRange(Encoding.ASCII.GetBytes(name));
                bytes.Add(0x00);
                SendResponse(bytes.ToArray());
                break;
        }
        Logger.Info($"GS I {n}");
    }

    /// <summary>Real-time request to recover (DLE ENQ) — clears recoverable error.</summary>
    public void RealtimeRecover()
    {
        Logger.Info("DLE ENQ: real-time recover");
        UiDispatch(() =>
        {
            if (State.Error == PrinterErrorState.Recoverable)
                State.Error = PrinterErrorState.None;
        });
    }

    #endregion

    #region Page mode

    private bool _pageMode;
    private Receipt? _standardReceipt; // the standard-mode receipt, parked while in page mode

    public bool IsPageMode => _pageMode;

    /// <summary>
    /// ESC L — enter page mode. Output is buffered into an off-stack receipt and flushed as one image
    /// by FF (PrintPage). This is an approximation: content is buffered then rasterized, rather than
    /// positioned with the full page-mode coordinate system.
    /// </summary>
    public void EnterPageMode()
    {
        if (_pageMode) return;
        Logger.Info("Enter page mode");
        _standardReceipt = CurrentReceipt;
        // not added to the stack
        CurrentReceipt = new Receipt(_paperConfiguration, _printMode, _lineSpacing, ImageFactory, _typefaces);
        _pageMode = true;
    }

    /// <summary>ESC S — return to standard mode, discarding any buffered page data.</summary>
    public void SelectStandardMode()
    {
        if (!_pageMode) return;
        Logger.Info("Select standard mode");
        RestoreStandard();
    }

    /// <summary>FF — in page mode, rasterize the page buffer onto the receipt and return to standard mode.</summary>
    public void PrintPage()
    {
        if (!_pageMode) return;
        Logger.Info("Print page (FF)");
        var page = CurrentReceipt;
        RestoreStandard();
        if (!page.IsEmpty)
        {
            using var bmp = page.Render();
            CurrentReceipt.PrintBitmap(bmp.Copy());
        }
    }

    /// <summary>CAN — cancel the buffered page data and return to standard mode.</summary>
    public void CancelPageData()
    {
        if (!_pageMode) { Logger.Info("CAN (not in page mode)"); return; }
        Logger.Info("Cancel page data");
        RestoreStandard();
    }

    private void RestoreStandard()
    {
        CurrentReceipt = _standardReceipt!;
        _standardReceipt = null;
        _pageMode = false;
    }

    #endregion

    #region Barcodes (1D)

    public void SetBarcodeHeight(int dots)
    {
        Logger.Info($"Set barcode height: {dots}");
        _barcodeHeight = Math.Max(1, dots);
    }

    public void SetBarcodeModuleWidth(int dots)
    {
        Logger.Info($"Set barcode module width: {dots}");
        _barcodeModuleWidth = dots;
    }

    public void SetHriPosition(HriPosition position)
    {
        Logger.Info($"Set HRI position: {position}");
        _hriPosition = position;
    }

    public void SetHriFont(PrinterFont font)
    {
        Logger.Info($"Set HRI font: {font}");
        _hriFont = font;
    }

    public void PrintBarcode(BarcodeFormat format, string data)
    {
        if (Blocked()) return;
        Logger.Info($"Print barcode [{format}]: {data}");

        var hriFontConfig = _paperConfiguration.GetFont(_hriFont);
        int hriTextSize = (int)(hriFontConfig.CharacterHeight / 2f * 1.3333f);
        bool showHri = _hriPosition is HriPosition.Below or HriPosition.Both or HriPosition.Above;

        try
        {
            var bmp = _barcodeRenderer.RenderBarcode1D(
                data, format, _barcodeModuleWidth, _barcodeHeight,
                showHri, hriFontConfig.RenderFont, hriTextSize);

            CurrentReceipt.PrintBitmap(bmp);
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, $"Failed to render barcode [{format}] for data '{data}'");
            PrintText($"[barcode error: {format}]");
            LineFeed();
        }
    }

    #endregion

    #region 2D symbols (QR / PDF417 / DataMatrix / Aztec)

    public void SetQrModuleSize(int dots)
    {
        Logger.Info($"Set 2D module size: {dots}");
        _qrModuleSize = dots;
    }

    public void SetQrErrorCorrection(QRCodeGenerator.ECCLevel ecc)
    {
        Logger.Info($"Set QR error correction: {ecc}");
        _qrEcc = ecc;
    }

    /// <summary>Stores data for a 2D symbol of family <paramref name="cn"/> (GS ( k fn 80).</summary>
    public void Store2DData(int cn, string data)
    {
        _2dSymbol = TwoDimensionCode.FromCn(cn);
        Logger.Info($"Store 2D data ({_2dSymbol.Name}, {data.Length} bytes)");
        _qrData = data;
    }

    /// <summary>Renders and prints the stored 2D symbol (GS ( k fn 81).</summary>
    public void Print2D()
    {
        if (string.IsNullOrEmpty(_qrData))
            return;
        if (Blocked()) return;

        Logger.Info($"Print 2D symbol ({_2dSymbol.Name}): {_qrData}");
        try
        {
            var bmp = _2dSymbol.Render(_barcodeRenderer, _qrData, _qrModuleSize, _qrEcc);
            CurrentReceipt.PrintBitmap(bmp);
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, $"Failed to render 2D symbol ({_2dSymbol.Name}) for '{_qrData}'");
            PrintText("[2D symbol error]");
            LineFeed();
        }
    }

    #endregion

    #region Command API

    /// <summary>
    /// Prints the data in the print buffer and feeds one line, based on the current line spacing.
    /// </summary>
    public void PrintAndLineFeed(string printBuffer)
    {
        PrintText(printBuffer);
        LineFeed();
    }

    public void PrintTab()
    {
    		string tabs = "";
    		
    		for (var i = 0; i < _tabSpacing; i++) tabs += " ";
        PrintText(tabs);
    }

    #endregion
}
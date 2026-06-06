using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SkiaSharp;
using ReceiptPrinterEmulator.Emulator.Abstraction;
using ReceiptPrinterEmulator.Emulator.Enums;
using ReceiptPrinterEmulator.Emulator.Rendering;
using ReceiptPrinterEmulator.EscPos;
using ReceiptPrinterEmulator.Logging;
using QRCoder;
using ZXing;

namespace ReceiptPrinterEmulator.Emulator;

public class ReceiptPrinter
{
    private readonly PaperConfiguration _paperConfiguration;
    private readonly EscPosInterpreter _escPosInterpreter;
    
    private PrintMode _printMode;
    private int _lineSpacing;
    private int _tabSpacing;

    // Barcode (1D) state — ESC/POS GS h / GS w / GS H / GS f
    private int _barcodeHeight = 162;
    private int _barcodeModuleWidth = 3;
    private HriPosition _hriPosition = HriPosition.None;
    private PrinterFont _hriFont = PrinterFont.FontA;

    // 2D symbol state — ESC/POS GS ( k (cn: 48=PDF417, 49=QR, 54=DataMatrix, 55=Aztec)
    private int _qrModuleSize = 3;
    private QRCodeGenerator.ECCLevel _qrEcc = QRCodeGenerator.ECCLevel.M;
    private string _qrData = string.Empty;
    private int _2dCn = 49;

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

    public ReceiptPrinter(PaperConfiguration paperConfiguration)
    {
        _paperConfiguration = paperConfiguration;
        _escPosInterpreter = new(this);

        _printMode = new PrintMode();

        ReceiptStack = new();

        StartNewReceipt();

        PowerCycle();

        // Push Automatic Status Back to the host whenever the simulated state changes.
        State.Changed += () => { if (_asbMask != 0) BroadcastStatus(StatusByteBuilder.AutoStatusBack(State)); };
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
        CurrentReceipt = new(_paperConfiguration, _printMode, _lineSpacing);
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
        Logger.Info($"Print: {text}");
        
        CurrentReceipt.PrintText(text,_printMode);
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

    public void PrintBitmap(SKBitmap bitmap)
    {
        Logger.Info($"Print bitmap: {bitmap.Width}x{bitmap.Height}");

        CurrentReceipt.PrintBitmap(bitmap);
    }

    private SKBitmap? _downloadBitImage;

    /// <summary>Stores a downloaded bit image (GS * x y ...) for later printing by GS /.</summary>
    public void DefineDownloadBitImage(SKBitmap bmp)
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

        int sx = mode is 1 or 3 ? 2 : 1; // double-width on modes 1,3
        int sy = mode is 2 or 3 ? 2 : 1; // double-height on modes 2,3

        if (sx == 1 && sy == 1)
        {
            CurrentReceipt.PrintBitmap(_downloadBitImage.Copy());
            return;
        }

        var scaled = new SKBitmap(_downloadBitImage.Width * sx, _downloadBitImage.Height * sy);
        using (var canvas = new SKCanvas(scaled))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(_downloadBitImage, SKRect.Create(0, 0, scaled.Width, scaled.Height));
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
        byte b = n switch
        {
            1 => StatusByteBuilder.PrinterStatus(State),
            2 => StatusByteBuilder.OfflineStatus(State),
            3 => StatusByteBuilder.ErrorStatus(State),
            4 => StatusByteBuilder.PaperSensorStatus(State),
            _ => StatusByteBuilder.PrinterStatus(State)
        };
        Logger.Info($"DLE EOT {n} -> 0x{b:X2}");
        SendResponse(b);
    }

    /// <summary>Transmits status (GS r n) back to the requesting host.</summary>
    public void TransmitStatus(int n)
    {
        byte b = n switch
        {
            1 or 49 => StatusByteBuilder.TransmitPaperStatus(State),
            2 or 50 => StatusByteBuilder.TransmitDrawerStatus(State),
            _ => 0
        };
        Logger.Info($"GS r {n} -> 0x{b:X2}");
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
        CurrentReceipt = new Receipt(_paperConfiguration, _printMode, _lineSpacing); // not added to the stack
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
        Logger.Info($"Print barcode [{format}]: {data}");

        var hriFontConfig = _paperConfiguration.GetFont(_hriFont);
        int hriTextSize = (int)(hriFontConfig.CharacterHeight / 2f * 1.3333f);
        bool showHri = _hriPosition is HriPosition.Below or HriPosition.Both or HriPosition.Above;

        try
        {
            var bmp = BarcodeRenderer.RenderBarcode1D(
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
        Logger.Info($"Store 2D data (cn={cn}, {data.Length} bytes)");
        _2dCn = cn;
        _qrData = data;
    }

    /// <summary>Renders and prints the stored 2D symbol (GS ( k fn 81).</summary>
    public void Print2D()
    {
        if (string.IsNullOrEmpty(_qrData))
            return;

        Logger.Info($"Print 2D symbol (cn={_2dCn}): {_qrData}");
        try
        {
            SKBitmap bmp = _2dCn switch
            {
                49 => BarcodeRenderer.RenderQr(_qrData, _qrModuleSize, _qrEcc),                 // QR Code
                48 => BarcodeRenderer.Render2D(_qrData, BarcodeFormat.PDF_417, _qrModuleSize),  // PDF417
                54 => BarcodeRenderer.Render2D(_qrData, BarcodeFormat.DATA_MATRIX, _qrModuleSize),
                55 => BarcodeRenderer.Render2D(_qrData, BarcodeFormat.AZTEC, _qrModuleSize),
                _ => BarcodeRenderer.RenderQr(_qrData, _qrModuleSize, _qrEcc)
            };
            CurrentReceipt.PrintBitmap(bmp);
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, $"Failed to render 2D symbol (cn={_2dCn}) for '{_qrData}'");
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
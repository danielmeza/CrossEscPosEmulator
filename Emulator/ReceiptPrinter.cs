using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SkiaSharp;
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

    // QR (2D) state — ESC/POS GS ( k (cn=49)
    private int _qrModuleSize = 3;
    private QRCodeGenerator.ECCLevel _qrEcc = QRCodeGenerator.ECCLevel.M;
    private string _qrData = string.Empty;

    public Receipt CurrentReceipt { get; private set; } = null!; // set via StartNewReceipt in ctor
    public List<Receipt> ReceiptStack { get; private set; }

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
    }

    #region ESC/POS

    /// <summary>
    /// When set, every received ESC/POS payload is dumped to disk (last_escpos_receive.txt, and
    /// last_ticket.bin for large payloads) for debugging. Enable via the ESCPOS_DEBUG_DUMP
    /// environment variable. Off by default to avoid writing files on every receive.
    /// </summary>
    public static bool DebugDumpEnabled { get; set; } =
        Environment.GetEnvironmentVariable("ESCPOS_DEBUG_DUMP") is "1" or "true";

    public void FeedEscPos(string ascii)
    {
        if (DebugDumpEnabled)
        {
            if (ascii.Length > 10000)
                File.WriteAllText("last_ticket.bin", ascii, Encoding.ASCII);
            File.WriteAllText("last_escpos_receive.txt", ascii, Encoding.ASCII);
        }

        try
        {
            Logger.Info($"Received: {ascii}");
            _escPosInterpreter.Interpret(ascii);
            
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "ESC/POS Interpreter Error");
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

    public void Buzz()
    {
        Logger.Info("Buzzer");
        OnBuzzer?.Invoke();
    }

    public void KickCashDrawer(int pin)
    {
        Logger.Info($"Cash drawer kick (pin {pin})");
        OnCashDrawer?.Invoke();
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

    #region QR (2D)

    public void SetQrModuleSize(int dots)
    {
        Logger.Info($"Set QR module size: {dots}");
        _qrModuleSize = dots;
    }

    public void SetQrErrorCorrection(QRCodeGenerator.ECCLevel ecc)
    {
        Logger.Info($"Set QR error correction: {ecc}");
        _qrEcc = ecc;
    }

    public void StoreQrData(string data)
    {
        Logger.Info($"Store QR data ({data.Length} bytes)");
        _qrData = data;
    }

    public void PrintQr()
    {
        Logger.Info($"Print QR: {_qrData}");

        if (string.IsNullOrEmpty(_qrData))
            return;

        try
        {
            var bmp = BarcodeRenderer.RenderQr(_qrData, _qrModuleSize, _qrEcc);
            CurrentReceipt.PrintBitmap(bmp);
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, $"Failed to render QR for data '{_qrData}'");
            PrintText("[QR error]");
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
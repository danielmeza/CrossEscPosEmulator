using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ESCPOS_NET;
using ESCPOS_NET.Emitters;
using ESCPOS_NET.Utilities;

namespace ReceiptPrinterEmulator.ViewModels;

/// <summary>
/// View model for the Monitor window — a POS-client that connects to the emulator over TCP using the
/// ESC-POS-.NET library, sends test jobs, and displays the printer status the emulator reports back
/// (via Automatic Status Back). This is the "other side" of the wire, for exercising the emulator.
/// </summary>
public partial class MonitorWindowViewModel : ObservableObject
{
    private readonly EPSON _e = new();
    private NetworkPrinter? _printer;

    [ObservableProperty] private string _host = "127.0.0.1";
    [ObservableProperty] private string _port;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _connectButtonText = "Connect";
    [ObservableProperty] private string _statusText = "Not connected.";

    public ObservableCollection<string> Log { get; } = new();

    public MonitorWindowViewModel(int defaultPort)
    {
        _port = defaultPort.ToString();
    }

    private void Append(string message) => Dispatcher.UIThread.Post(() =>
    {
        Log.Insert(0, $"{message}");
        if (Log.Count > 200) Log.RemoveAt(Log.Count - 1);
    });

    private async Task Send(string label, params byte[][] parts)
    {
        if (_printer is null) { Append("Not connected."); return; }
        try
        {
            await Task.Run(() => _printer.Write(ByteSplicer.Combine(parts)));
            Append($"→ {label}");
        }
        catch (Exception ex)
        {
            Append($"send failed: {ex.Message}");
        }
    }

    #region Connection

    [RelayCommand]
    private void ToggleConnect()
    {
        if (_printer is not null) { Disconnect(); return; }

        try
        {
            _printer = new NetworkPrinter(new NetworkPrinterSettings
            {
                ConnectionString = $"{Host}:{Port}",
                PrinterName = "Monitor"
            });
            _printer.StatusChanged += OnStatusChanged;
            // The read/monitor loop starts automatically on connect. Ask the emulator to push status
            // automatically on every state change so we see panel toggles reflected here.
            _printer.Write(_e.EnableAutomaticStatusBack());

            IsConnected = true;
            ConnectButtonText = "Disconnect";
            StatusText = "Monitoring… toggle the Printer state panel to see updates.";
            Append($"Connected to {Host}:{Port}");
        }
        catch (Exception ex)
        {
            Append($"connect failed: {ex.Message}");
            Disconnect();
        }
    }

    private void Disconnect()
    {
        try
        {
            if (_printer is not null)
            {
                _printer.StatusChanged -= OnStatusChanged;
                _printer.Dispose();
            }
        }
        catch { /* ignore */ }
        _printer = null;
        IsConnected = false;
        ConnectButtonText = "Connect";
        StatusText = "Not connected.";
        Append("Disconnected");
    }

    private void OnStatusChanged(object? sender, EventArgs e)
    {
        if (e is not PrinterStatusEventArgs s) return;
        string Fmt(bool? b) => b is null ? "?" : (b.Value ? "yes" : "no");
        var text =
            $"Online: {Fmt(s.IsPrinterOnline)}   Cover open: {Fmt(s.IsCoverOpen)}\n" +
            $"Paper out: {Fmt(s.IsPaperOut)}   Paper low: {Fmt(s.IsPaperLow)}\n" +
            $"Drawer open: {Fmt(s.IsCashDrawerOpen)}   Error: {Fmt(s.IsInErrorState)}";
        Dispatcher.UIThread.Post(() => StatusText = text);
        Append("← status update");
    }

    #endregion

    #region Send commands

    [RelayCommand]
    private async Task SendSample() => await Send("sample receipt",
        _e.CenterAlign(),
        _e.SetStyles(PrintStyle.Bold | PrintStyle.DoubleWidth | PrintStyle.DoubleHeight),
        _e.PrintLine("MONITOR"),
        _e.SetStyles(PrintStyle.None),
        _e.PrintLine("Sent via ESC-POS-.NET"),
        _e.LeftAlign(),
        _e.PrintLine("Item ................ 9.99"),
        _e.SetStyles(PrintStyle.Bold),
        _e.PrintLine("TOTAL ............... 9.99"),
        _e.SetStyles(PrintStyle.None),
        _e.FeedLines(1),
        _e.CenterAlign(),
        _e.PrintQRCode("https://example.com/monitor"),
        _e.FeedLines(2),
        _e.PartialCut());

    [RelayCommand]
    private async Task SendBarcodes()
    {
        var parts = new List<byte[]>
        {
            _e.CenterAlign(),
            _e.SetBarcodeHeightInDots(80),
            _e.SetBarWidth(BarWidth.Default),
            _e.SetBarLabelPosition(BarLabelPrintPosition.Below)
        };
        void Bc(BarcodeType t, string data, string label)
        {
            parts.Add(_e.PrintLine(label));
            parts.Add(_e.PrintBarcode(t, data));
            parts.Add(_e.FeedLines(1));
        }
        Bc(BarcodeType.UPC_A, "12345678901", "UPC-A");
        Bc(BarcodeType.JAN13_EAN13, "123456789012", "EAN-13");
        Bc(BarcodeType.CODE39, "CODE39", "CODE39");
        Bc(BarcodeType.CODE128, "Code128", "CODE128");
        Bc(BarcodeType.ITF, "12345678", "ITF");
        parts.Add(_e.PartialCut());
        await Send("all barcodes", parts.ToArray());
    }

    [RelayCommand]
    private async Task SendQr() => await Send("QR + PDF417 + DataMatrix + Aztec",
        _e.CenterAlign(),
        _e.PrintLine("QR"),
        _e.PrintQRCode("https://example.com"),
        _e.PrintLine("PDF417"),
        _e.Print2DCode(TwoDimensionCodeType.PDF417, "PDF417 from monitor"),
        // DataMatrix / Aztec aren't in the ESC-POS-.NET emitter, so send the raw GS ( k bytes —
        // the emulator supports them (cn=54 / cn=55).
        _e.PrintLine("DataMatrix"),
        Raw2D(54, "DataMatrix from monitor", 5),
        _e.PrintLine("Aztec"),
        Raw2D(55, "Aztec from monitor", 5),
        _e.FeedLines(1),
        _e.PartialCut());

    /// <summary>Builds raw GS ( k bytes for a 2D symbol the emitter doesn't expose (DataMatrix/Aztec).</summary>
    private static byte[] Raw2D(int cn, string data, int moduleSize)
    {
        var b = new List<byte>();
        void By(params int[] xs) { foreach (var x in xs) b.Add((byte)x); }
        const int GS = 0x1D;
        By(GS, '(', 'k', 3, 0, cn, 67, moduleSize);                      // module size
        int len = 3 + data.Length;
        By(GS, '(', 'k', len & 0xFF, len >> 8, cn, 80, 48);             // store
        b.AddRange(System.Text.Encoding.ASCII.GetBytes(data));
        By(GS, '(', 'k', 3, 0, cn, 81, 48);                             // print
        return b.ToArray();
    }

    [RelayCommand]
    private async Task OpenDrawer() => await Send("open cash drawer", _e.CashDrawerOpenPin2());

    [RelayCommand]
    private async Task Beep() => await Send("buzzer (BEL)", new byte[] { 0x07 });

    [RelayCommand]
    private async Task Cut() => await Send("cut", _e.PartialCut());

    [RelayCommand]
    private async Task SendFullTest()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "test_receipt.txt");
        if (!File.Exists(path)) { Append("test_receipt.txt not found"); return; }
        await Send("full feature test receipt", File.ReadAllBytes(path));
    }

    #endregion

    public void Shutdown() => Disconnect();
}

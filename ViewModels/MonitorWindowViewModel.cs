using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ESCPOS_NET;
using ESCPOS_NET.Emitters;
using ESCPOS_NET.Utilities;
using ReceiptPrinterEmulator.Emulator.Rendering;

namespace ReceiptPrinterEmulator.ViewModels;

/// <summary>One status row in the monitor: a colored dot, a label and the current value.</summary>
public sealed class StatusIndicator
{
    public StatusIndicator(string label, string value, IBrush color)
    {
        Label = label;
        Value = value;
        Color = color;
    }

    public string Label { get; }
    public string Value { get; }
    public IBrush Color { get; }
}

/// <summary>
/// View model for the Monitor window — a POS-client that connects to the emulator over TCP using the
/// ESC-POS-.NET library, sends test jobs, and displays the printer status the emulator reports back
/// (via Automatic Status Back). This is the "other side" of the wire, for exercising the emulator.
/// </summary>
public partial class MonitorWindowViewModel : ObservableObject
{
    private const byte Bel = 0x07;          // bell / buzzer control code
    private const int DefaultModuleSize = 5; // dots per module for 2D symbols

    private readonly EPSON _e = new();
    private NetworkPrinter? _printer;

    [ObservableProperty] private string _host = "127.0.0.1";
    [ObservableProperty] private string _port;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _connectButtonText = "Connect";
    [ObservableProperty] private string _statusText = "Not connected.";

    public ObservableCollection<string> Log { get; } = new();
    public ObservableCollection<StatusIndicator> Indicators { get; } = new();

    private static readonly IBrush Green = new SolidColorBrush(Avalonia.Media.Color.Parse("#3CE07A"));
    private static readonly IBrush Red = new SolidColorBrush(Avalonia.Media.Color.Parse("#E0533C"));
    private static readonly IBrush Amber = new SolidColorBrush(Avalonia.Media.Color.Parse("#E0A93C"));
    private static readonly IBrush Blue = new SolidColorBrush(Avalonia.Media.Color.Parse("#3C9AE0"));
    private static readonly IBrush Gray = new SolidColorBrush(Avalonia.Media.Color.Parse("#777777"));

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

        bool online = s.IsPrinterOnline == true;
        bool paperOut = s.IsPaperOut == true;
        bool paperLow = s.IsPaperLow == true;
        bool cover = s.IsCoverOpen == true;
        bool drawer = s.IsCashDrawerOpen == true;
        bool error = s.IsInErrorState == true;
        bool ready = online && !paperOut && !cover && !error;

        Dispatcher.UIThread.Post(() =>
        {
            StatusText = ready ? "● Ready" : "● Not ready";
            Indicators.Clear();
            Indicators.Add(new("Printer", online ? "Online" : "Offline", online ? Green : Red));
            Indicators.Add(new("Paper", paperOut ? "Out" : paperLow ? "Low" : "OK",
                paperOut ? Red : paperLow ? Amber : Green));
            Indicators.Add(new("Cover", cover ? "Open" : "Closed", cover ? Red : Green));
            Indicators.Add(new("Cash drawer", drawer ? "Open" : "Closed", drawer ? Blue : Gray));
            Indicators.Add(new("Error", error ? "Yes" : "None", error ? Red : Green));
        });
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
        Raw2D(TwoDimensionCode.DataMatrix, "DataMatrix from monitor", DefaultModuleSize),
        _e.PrintLine("Aztec"),
        Raw2D(TwoDimensionCode.Aztec, "Aztec from monitor", DefaultModuleSize),
        _e.FeedLines(1),
        _e.PartialCut());

    /// <summary>Builds raw GS ( k bytes for a 2D symbol the emitter doesn't expose (DataMatrix/Aztec).</summary>
    private static byte[] Raw2D(int cn, string data, int moduleSize)
    {
        const int GS = 0x1D, ParenK0 = '(', ParenK1 = 'k';
        const int FnModuleSize = 67, FnStore = 80, FnPrint = 81, StoreM = 48;
        const int HeaderLen = 3; // cn + fn + m

        var b = new List<byte>();
        void By(params int[] xs) { foreach (var x in xs) b.Add((byte)x); }

        By(GS, ParenK0, ParenK1, HeaderLen, 0, cn, FnModuleSize, moduleSize);
        int len = HeaderLen + data.Length;
        By(GS, ParenK0, ParenK1, len & 0xFF, len >> 8, cn, FnStore, StoreM);
        b.AddRange(System.Text.Encoding.ASCII.GetBytes(data));
        By(GS, ParenK0, ParenK1, HeaderLen, 0, cn, FnPrint, StoreM);
        return b.ToArray();
    }

    [RelayCommand]
    private async Task OpenDrawer() => await Send("open cash drawer", _e.CashDrawerOpenPin2());

    [RelayCommand]
    private async Task Beep() => await Send("buzzer (BEL)", [Bel]);

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

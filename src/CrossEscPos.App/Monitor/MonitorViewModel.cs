using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ESCPOS_NET.Emitters;
using ESCPOS_NET.Utilities;
using CrossEscPos.App.Transports;
using CrossEscPos.Emulator.Rendering;

namespace CrossEscPos.App.Monitor;

/// <summary>One status row: a colored dot, a label, and the current value.</summary>
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
/// The shared Monitor: a POS-client that connects to the emulator (over TCP/serial/USB on desktop, or the
/// SignalR proxy in the browser), sends ESC/POS test jobs built with ESC-POS-.NET, and shows the printer
/// status the emulator reports back. Transport is delegated to an injected <see cref="IMonitorClient"/> so
/// the same view model, test jobs, and indicator UI run identically on both heads.
/// </summary>
public partial class MonitorViewModel : ObservableObject
{
    private const byte Bel = 0x07;           // bell / buzzer control code
    private const int DefaultModuleSize = 5; // dots per module for 2D symbols

    private readonly EPSON _e = new();
    private readonly IMonitorClient _client;

    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _connectButtonText = "Connect";
    [ObservableProperty] private string _statusText = "Not connected.";
    [ObservableProperty] private bool _canRefresh;

    public IReadOnlyList<string> Modes => _client.Modes;
    public bool HasModes => _client.Modes.Count > 1;

    public string Mode
    {
        get => _client.Mode;
        set
        {
            if (_client.Mode == value)
                return;
            _client.Mode = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<TransportField> Fields => _client.Fields;

    public ObservableCollection<string> Log { get; } = new();
    public ObservableCollection<StatusIndicator> Indicators { get; } = new();

    // Fully qualified: ESCPOS_NET.Emitters also defines a Color type.
    private static readonly IBrush Green = new SolidColorBrush(Avalonia.Media.Color.Parse("#3CE07A"));
    private static readonly IBrush Red = new SolidColorBrush(Avalonia.Media.Color.Parse("#E0533C"));
    private static readonly IBrush Amber = new SolidColorBrush(Avalonia.Media.Color.Parse("#E0A93C"));
    private static readonly IBrush Blue = new SolidColorBrush(Avalonia.Media.Color.Parse("#3C9AE0"));
    private static readonly IBrush Gray = new SolidColorBrush(Avalonia.Media.Color.Parse("#777777"));

    public MonitorViewModel(IMonitorClient client)
    {
        _client = client;
        _client.Log += Append;
        _client.StatusReceived += OnStatusReceived;
        _client.FieldsChanged += OnFieldsChanged;
        _canRefresh = _client.CanRefresh;
    }

    private void OnFieldsChanged() => Dispatcher.UIThread.Post(() =>
    {
        OnPropertyChanged(nameof(Fields));
        CanRefresh = _client.CanRefresh;
    });

    [RelayCommand]
    private async Task Refresh()
    {
        try { await _client.RefreshAsync(); }
        catch (Exception ex) { Append($"refresh failed: {ex.Message}"); }
        OnFieldsChanged();
    }

    private void Append(string message) => Dispatcher.UIThread.Post(() =>
    {
        Log.Insert(0, message);
        if (Log.Count > 200) Log.RemoveAt(Log.Count - 1);
    });

    #region Connection

    [RelayCommand]
    private async Task ToggleConnect()
    {
        if (IsConnected) { Disconnect(); return; }

        try
        {
            var target = await _client.ConnectAsync();
            IsConnected = true;
            ConnectButtonText = "Disconnect";
            StatusText = "Monitoring… toggle the Printer state panel to see updates.";
            Append($"Connected ({target})");
        }
        catch (Exception ex)
        {
            Append($"connect failed: {ex.Message}");
            Disconnect();
        }
    }

    private void Disconnect()
    {
        try { _client.Disconnect(); }
        catch { /* ignore */ }
        IsConnected = false;
        ConnectButtonText = "Connect";
        StatusText = "Not connected.";
        Indicators.Clear();
        Append("Disconnected");
    }

    private void OnStatusReceived(MonitorStatus s) => Dispatcher.UIThread.Post(() =>
    {
        StatusText = s.Ready ? "● Ready" : "● Not ready";
        Indicators.Clear();
        Indicators.Add(new("Printer", s.Online ? "Online" : "Offline", s.Online ? Green : Red));
        Indicators.Add(new("Paper", s.PaperOut ? "Out" : s.PaperLow ? "Low" : "OK",
            s.PaperOut ? Red : s.PaperLow ? Amber : Green));
        Indicators.Add(new("Cover", s.CoverOpen ? "Open" : "Closed", s.CoverOpen ? Red : Green));
        Indicators.Add(new("Cash drawer", s.DrawerOpen ? "Open" : "Closed", s.DrawerOpen ? Blue : Gray));
        Indicators.Add(new("Error", s.Error ? "Yes" : "None", s.Error ? Red : Green));
        Append("← status update");
    });

    #endregion

    #region Send commands

    private async Task Send(string label, params byte[][] parts)
    {
        try
        {
            await _client.SendAsync(ByteSplicer.Combine(parts));
            Append($"→ {label}");
        }
        catch (Exception ex)
        {
            Append($"send failed: {ex.Message}");
        }
    }

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
        Raw2D(TwoDimensionCode.DataMatrix.Value, "DataMatrix from monitor", DefaultModuleSize),
        _e.PrintLine("Aztec"),
        Raw2D(TwoDimensionCode.Aztec.Value, "Aztec from monitor", DefaultModuleSize),
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
        // Embedded in the shared app so it works on both heads (desktop and browser).
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("test_receipt.txt");
        if (stream is null) { Append("test_receipt.txt not found"); return; }
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        await Send("full feature test receipt", ms.ToArray());
    }

    #endregion

    public void Shutdown() => Disconnect();
}

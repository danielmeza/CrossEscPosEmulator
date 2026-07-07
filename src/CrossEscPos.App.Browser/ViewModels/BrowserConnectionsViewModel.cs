using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CrossEscPos;
using CrossEscPos.App.Browser.Transports;
using CrossEscPos.Emulator;
using CrossEscPos.Transports.Browser;

namespace CrossEscPos.App.Browser.ViewModels;

/// <summary>
/// The browser transports panel: Web Serial, WebUSB (via the shared <see cref="WebTransport"/> over the
/// Avalonia WASM JS bridge) and a WebSocket (to the CrossEscPos.WsProxy for TCP reception). This is the
/// browser's contribution to the shared app's connections slot, and its <see cref="ITransportSink"/> —
/// received bytes are fed to the shared printer, whose status replies go back over the same transport.
/// </summary>
public partial class BrowserConnectionsViewModel : ObservableObject, ITransportSink
{
    private readonly ReceiptPrinter _printer;
    private readonly WasmJsTransportBridge _bridge = new();
    private readonly WebTransport _serial;
    private readonly WebTransport _usb;
    private readonly WebSocketTransport _ws;

    public bool SerialSupported { get; private set; }
    public bool UsbSupported { get; private set; }

    [ObservableProperty] private string _serialBaud = "9600";
    [ObservableProperty] private string _wsUrl = "ws://localhost:5000/ws";

    public BrowserConnectionsViewModel(ReceiptPrinter printer)
    {
        _printer = printer;
        _serial = new WebTransport(_bridge, this, "serial", "Web Serial");
        _usb = new WebTransport(_bridge, this, "usb", "WebUSB");
        _ws = new WebSocketTransport(this, "WebSocket");
        _serial.StateChanged += OnChanged;
        _usb.StateChanged += OnChanged;
        _ws.StateChanged += OnChanged;
        _ = DetectAsync();
    }

    public string SerialStatus => _serial.IsConnected ? _serial.Description! : SerialSupported ? "not connected" : "unsupported";
    public string UsbStatus => _usb.IsConnected ? _usb.Description! : UsbSupported ? "not connected" : "unsupported";
    public string WsStatus => _ws.IsConnected ? _ws.Description! : "not connected";
    public string SerialButtonText => _serial.IsConnected ? "Disconnect" : "Connect";
    public string UsbButtonText => _usb.IsConnected ? "Disconnect" : "Connect";
    public string WsButtonText => _ws.IsConnected ? "Disconnect" : "Connect";

    [RelayCommand]
    private Task ToggleSerial()
    {
        _serial.Options = SerialBaud;   // baud rate for the port.open() / SET_LINE_CODING
        return Toggle(_serial);
    }

    [RelayCommand] private Task ToggleUsb() => Toggle(_usb);

    [RelayCommand]
    private Task ToggleWs()
    {
        _ws.Url = WsUrl;
        return Toggle(_ws);
    }

    private static async Task Toggle(IReceiptTransport transport)
    {
        if (transport.IsConnected) await transport.DisconnectAsync();
        else await transport.ConnectAsync();
    }

    private async Task DetectAsync()
    {
        SerialSupported = await _serial.IsSupportedAsync();
        UsbSupported = await _usb.IsSupportedAsync();
        OnChanged();
    }

    private void OnChanged()
    {
        OnPropertyChanged(nameof(SerialSupported));
        OnPropertyChanged(nameof(UsbSupported));
        OnPropertyChanged(nameof(SerialStatus));
        OnPropertyChanged(nameof(UsbStatus));
        OnPropertyChanged(nameof(WsStatus));
        OnPropertyChanged(nameof(SerialButtonText));
        OnPropertyChanged(nameof(UsbButtonText));
        OnPropertyChanged(nameof(WsButtonText));
    }

    // ITransportSink — a connected device streams a live session into the shared printer.
    void ITransportSink.Feed(byte[] data, IPrinterResponder responder) => _printer.FeedEscPos(data, responder);
    void ITransportSink.Attach(IPrinterResponder responder) => _printer.RegisterResponder(responder);
    void ITransportSink.Detach(IPrinterResponder responder) => _printer.UnregisterResponder(responder);
}

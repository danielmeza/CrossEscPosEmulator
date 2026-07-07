using System;
using System.Threading.Tasks;
using CrossEscPos.App.Transports;
using CrossEscPos.Transports.Browser;

namespace CrossEscPos.App.Browser.Transports;

/// <summary>
/// A <see cref="TransportEntry"/> that wraps any browser <see cref="IReceiptTransport"/> (Web Serial,
/// WebUSB, WebSocket) so it renders in the shared connections view. An optional field (serial baud,
/// WebSocket URL) is applied via <paramref name="applyOption"/> before connecting.
/// </summary>
public sealed class ReceiptTransportEntry : TransportEntry
{
    private readonly IReceiptTransport _transport;
    private readonly TransportField? _optionField;
    private readonly Action<string>? _applyOption;
    private bool _supported = true;

    public ReceiptTransportEntry(IReceiptTransport transport, string name,
        TransportField? optionField = null, Action<string>? applyOption = null) : base(name)
    {
        _transport = transport;
        _optionField = optionField;
        _applyOption = applyOption;
        if (optionField is not null)
            Fields = new[] { optionField };
        _transport.StateChanged += OnStateChanged;
        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        _supported = await _transport.IsSupportedAsync();
        OnStateChanged();
    }

    private void OnStateChanged()
        => Set(_transport.IsConnected,
            _transport.IsConnected ? (_transport.Description ?? "connected")
                                   : (_supported ? "not connected" : "unsupported"));

    protected override Task ToggleAsync()
    {
        if (_transport.IsConnected)
            return _transport.DisconnectAsync();
        if (_optionField is not null)
            _applyOption?.Invoke(_optionField.Value);
        return _transport.ConnectAsync(); // opens the device picker (user gesture)
    }

    public override void Shutdown() => _ = _transport.DisposeAsync();
}

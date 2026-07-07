using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CrossEscPos.App.Transports;
using CrossEscPos.Transports.Browser;

namespace CrossEscPos.App.Browser.Transports;

/// <summary>
/// A <see cref="TransportEntry"/> that wraps any browser <see cref="IReceiptTransport"/> (Web Serial,
/// WebUSB, SignalR proxy) so it renders in the shared connections view. Its <see cref="TransportField"/>s
/// are copied onto the transport via <paramref name="applyBeforeConnect"/> right before connecting.
/// </summary>
public sealed class ReceiptTransportEntry : TransportEntry
{
    private readonly IReceiptTransport _transport;
    private readonly Action? _applyBeforeConnect;
    private bool _supported = true;

    public ReceiptTransportEntry(IReceiptTransport transport, string name,
        IReadOnlyList<TransportField>? fields = null, Action? applyBeforeConnect = null) : base(name)
    {
        _transport = transport;
        _applyBeforeConnect = applyBeforeConnect;
        if (fields is not null)
            Fields = fields;
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
            : !_supported ? "unsupported"
            // A description while disconnected is a connect error (e.g. "port in use").
            : _transport.Description ?? "not connected");

    protected override Task ToggleAsync()
    {
        if (_transport.IsConnected)
            return _transport.DisconnectAsync();
        _applyBeforeConnect?.Invoke();
        return _transport.ConnectAsync(); // opens the device picker (user gesture)
    }

    public override void Shutdown() => _ = _transport.DisposeAsync();
}

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
    private readonly bool _autoConnect;
    private bool _supported = true;

    /// <param name="autoConnect">
    /// Connect on startup without a user gesture. Only valid for gesture-free transports (the SignalR
    /// proxy) — so the browser's TCP bridge comes up automatically, matching the desktop TCP listener.
    /// </param>
    public ReceiptTransportEntry(IReceiptTransport transport, string name,
        IReadOnlyList<TransportField>? fields = null, Action? applyBeforeConnect = null, bool autoConnect = false) : base(name)
    {
        _transport = transport;
        _applyBeforeConnect = applyBeforeConnect;
        _autoConnect = autoConnect;
        if (fields is not null)
            Fields = fields;
        _transport.StateChanged += OnStateChanged;
        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        _supported = await _transport.IsSupportedAsync();
        OnStateChanged();

        // Bring the proxy up on its own so POS software can reach the emulator over TCP immediately —
        // the browser equivalent of the desktop's auto-started TCP listener. Fails quietly if the host
        // isn't serving the hub (e.g. the app is served standalone).
        if (_autoConnect && _supported && !_transport.IsConnected)
        {
            _applyBeforeConnect?.Invoke();
            try { await _transport.ConnectAsync(); }
            catch { /* hub unreachable — the entry just shows "not connected" */ }
        }
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

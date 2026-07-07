using System;
using System.Threading.Tasks;

namespace CrossEscPos.Transports.Browser;

/// <summary>
/// The shared, host-agnostic browser transport. It owns the connection state machine and wires a
/// <see cref="IJsTransportBridge"/> (the platform API) to an <see cref="ITransportSink"/> (the host's
/// printer): received bytes are fed to the sink, and the printer's status replies go back out through
/// the bridge. Both the Blazor and Avalonia hosts use this class unchanged — they differ only in the
/// injected bridge and sink.
/// </summary>
public class WebTransport : IReceiptTransport
{
    private readonly IJsTransportBridge _bridge;
    private readonly ITransportSink _sink;
    private readonly string _kindId;

    /// <param name="kindId">Transport id passed to the bridge — <c>"serial"</c> or <c>"usb"</c>.</param>
    /// <param name="kind">Human label shown in the UI.</param>
    public WebTransport(IJsTransportBridge bridge, ITransportSink sink, string kindId, string kind)
    {
        _bridge = bridge;
        _sink = sink;
        _kindId = kindId;
        Kind = kind;
        _bridge.DataReceived += OnBridgeData;
        _bridge.Closed += OnBridgeClosed;
    }

    public string Kind { get; }
    public string? Description { get; private set; }
    public bool IsConnected { get; private set; }
    public event Action? StateChanged;

    /// <summary>Transport-specific connect hint passed to the bridge (for serial: the baud rate).</summary>
    public string? Options { get; set; }

    public ValueTask<bool> IsSupportedAsync() => _bridge.IsSupportedAsync(_kindId);

    public async Task ConnectAsync()
    {
        if (IsConnected)
            return;

        var description = await _bridge.ConnectAsync(_kindId, Options);   // opens the picker (user gesture)
        if (string.IsNullOrEmpty(description))
            return; // cancelled

        Description = description;
        IsConnected = true;
        _sink.Attach(this);
        StateChanged?.Invoke();
    }

    public async Task DisconnectAsync()
    {
        if (!IsConnected)
            return;

        _sink.Detach(this);
        IsConnected = false;
        Description = null;
        try { await _bridge.DisconnectAsync(_kindId); }
        catch { /* already gone */ }
        StateChanged?.Invoke();
    }

    /// <summary>Printer status / Automatic-Status-Back reply → write it back to the device.</summary>
    public void Send(byte[] data) => _ = _bridge.WriteAsync(_kindId, data);   // fire-and-forget

    private void OnBridgeData(string kind, byte[] data)
    {
        if (kind == _kindId && IsConnected && data.Length > 0)
            _sink.Feed(data, this);
    }

    private void OnBridgeClosed(string kind)
    {
        if (kind != _kindId || !IsConnected)
            return;
        _sink.Detach(this);
        IsConnected = false;
        Description = null;
        StateChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        _bridge.DataReceived -= OnBridgeData;
        _bridge.Closed -= OnBridgeClosed;
        await DisconnectAsync();
    }
}

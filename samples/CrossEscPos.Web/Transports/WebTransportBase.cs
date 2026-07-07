using System;
using System.Threading.Tasks;
using CrossEscPos.Web.Services;
using Microsoft.JSInterop;

namespace CrossEscPos.Web.Transports;

/// <summary>
/// Shared plumbing for the browser transports: a <see cref="DotNetObjectReference{T}"/> the JS side
/// calls back into, connect/disconnect, the receive callback (→ <see cref="EmulatorHost.FeedLive"/>),
/// and the write path (<see cref="IPrinterResponder.Send"/> → JS). Concrete transports only supply the
/// JS object name and a label. Bytes cross the interop boundary as <c>Uint8Array</c> in both directions
/// (Blazor's native byte-array interop — no base64).
/// </summary>
public abstract class WebTransportBase : IReceiptTransport
{
    private readonly EmulatorHost _host;
    private readonly IJSRuntime _js;
    private DotNetObjectReference<WebTransportBase>? _ref;

    protected WebTransportBase(EmulatorHost host, IJSRuntime js)
    {
        _host = host;
        _js = js;
    }

    /// <summary>The global JS object exposing <c>isSupported/connect/write/disconnect</c>.</summary>
    protected abstract string JsObject { get; }

    public abstract string Kind { get; }

    public string? Description { get; private set; }
    public bool IsConnected { get; private set; }
    public event Action? StateChanged;

    public ValueTask<bool> IsSupportedAsync() => _js.InvokeAsync<bool>($"{JsObject}.isSupported");

    public async Task ConnectAsync()
    {
        if (IsConnected)
            return;

        _ref ??= DotNetObjectReference.Create(this);

        // First await must be the picker call so the user-gesture activation is still live.
        var description = await _js.InvokeAsync<string?>($"{JsObject}.connect", _ref);
        if (string.IsNullOrEmpty(description))
            return; // user cancelled the device picker

        Description = description;
        IsConnected = true;
        _host.AttachResponder(this);
        StateChanged?.Invoke();
    }

    public async Task DisconnectAsync()
    {
        if (!IsConnected)
            return;

        _host.DetachResponder(this);
        IsConnected = false;
        Description = null;
        try { await _js.InvokeVoidAsync($"{JsObject}.disconnect"); }
        catch { /* already gone */ }
        StateChanged?.Invoke();
    }

    /// <summary>JS → .NET: a chunk of ESC/POS arrived from the device.</summary>
    [JSInvokable]
    public void OnDataReceived(byte[] data)
    {
        if (data is { Length: > 0 })
            _host.FeedLive(data, this);
    }

    /// <summary>JS → .NET: the port/device was closed or unplugged.</summary>
    [JSInvokable]
    public void OnClosed()
    {
        if (!IsConnected)
            return;
        _host.DetachResponder(this);
        IsConnected = false;
        Description = null;
        StateChanged?.Invoke();
    }

    /// <summary>The printer's status / Automatic-Status-Back reply → write it back to the device.</summary>
    public void Send(byte[] data)
        => _ = _js.InvokeVoidAsync($"{JsObject}.write", data).AsTask(); // fire-and-forget

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _ref?.Dispose();
    }
}

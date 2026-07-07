using System;
using System.Threading.Tasks;

namespace CrossEscPos.Transports.Browser;

/// <summary>
/// The host-specific bridge to the browser's JavaScript transport API (<c>transports.js</c>). This is
/// the <b>only</b> part of the transport stack that differs per host: the Blazor host implements it over
/// <c>IJSRuntime</c>, the Avalonia WASM host over <c>[JSImport]</c>/<c>[JSExport]</c>. Everything above
/// it (<see cref="WebTransport"/>, the UI) is shared.
///
/// <paramref name="kind"/> is the transport id — <c>"serial"</c> or <c>"usb"</c>.
/// </summary>
public interface IJsTransportBridge
{
    ValueTask<bool> IsSupportedAsync(string kind);

    /// <summary>Opens the device picker and connects; returns a description, or <c>null</c> if cancelled.</summary>
    ValueTask<string?> ConnectAsync(string kind);

    ValueTask WriteAsync(string kind, byte[] data);

    ValueTask DisconnectAsync(string kind);

    /// <summary>Raised when a chunk of ESC/POS arrives from a device: <c>(kind, bytes)</c>.</summary>
    event Action<string, byte[]> DataReceived;

    /// <summary>Raised when a device is closed / unplugged: <c>(kind)</c>.</summary>
    event Action<string> Closed;
}

using System;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using CrossEscPos.Transports.Browser;

namespace CrossEscPos.App.Browser.Transports;

/// <summary>
/// Avalonia WASM implementation of <see cref="IJsTransportBridge"/> — bridges the shared
/// <c>transports.js</c> over <c>[JSImport]</c>/<c>[JSExport]</c> (the raw browser interop Avalonia uses).
/// Outbound calls target the <c>globalThis.crossescpos.*</c> functions; inbound delivery arrives at the
/// <see cref="DeliverData"/>/<see cref="DeliverClosed"/> exports (wired to the JS callback slots in
/// <c>main.js</c>). Bytes cross as base64 so the interop marshaling stays trivial.
/// </summary>
public sealed partial class WasmJsTransportBridge : IJsTransportBridge
{
    private static WasmJsTransportBridge? _instance;

    public event Action<string, byte[]>? DataReceived;
    public event Action<string>? Closed;

    public WasmJsTransportBridge() => _instance = this;

    public ValueTask<bool> IsSupportedAsync(string kind) => ValueTask.FromResult(IsSupported(kind));

    public async ValueTask<string?> ConnectAsync(string kind)
    {
        var description = await Connect(kind);
        return string.IsNullOrEmpty(description) ? null : description;
    }

    public ValueTask WriteAsync(string kind, byte[] data) => new(Write(kind, Convert.ToBase64String(data)));

    public ValueTask DisconnectAsync(string kind) => new(Disconnect(kind));

    // JS -> .NET delivery (base64). Wired from main.js.
    [JSExport]
    internal static void DeliverData(string kind, string base64)
        => _instance?.DataReceived?.Invoke(kind, Convert.FromBase64String(base64));

    [JSExport]
    internal static void DeliverClosed(string kind)
        => _instance?.Closed?.Invoke(kind);

    // .NET -> JS (globals defined by transports.js; window === globalThis on the browser main thread).
    [JSImport("globalThis.crossescpos.isSupported")]
    private static partial bool IsSupported(string kind);

    [JSImport("globalThis.crossescpos.connect")]
    private static partial Task<string?> Connect(string kind);

    [JSImport("globalThis.crossescpos.write")]
    private static partial Task Write(string kind, string base64);

    [JSImport("globalThis.crossescpos.disconnect")]
    private static partial Task Disconnect(string kind);
}

import { dotnet } from './_framework/dotnet.js'

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const config = dotnetRuntime.getConfig();

// Wire the shared transports.js delivery slots to the app's [JSExport] callbacks. transports.js loads
// as a classic script first, so globalThis.crossescpos already exists; we override the no-op slots.
try {
    const exports = await dotnetRuntime.getAssemblyExports(config.mainAssemblyName);
    const bridge = exports.CrossEscPos.App.Browser.Transports.WasmJsTransportBridge;
    const cx = (globalThis.crossescpos = globalThis.crossescpos || {});
    const toBase64 = (u8) => {
        let s = '';
        for (let i = 0; i < u8.length; i++) s += String.fromCharCode(u8[i]);
        return btoa(s);
    };
    cx.onData = (kind, u8) => bridge.DeliverData(kind, toBase64(u8));
    cx.onClosed = (kind) => bridge.DeliverClosed(kind);
} catch (e) {
    console.warn('CrossEscPos: transport callbacks not wired', e);
}

await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);

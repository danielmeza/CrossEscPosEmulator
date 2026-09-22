import { dotnet } from './_framework/dotnet.js'

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

// Startup splash (index.html): the paper feeds out as the runtime files download. Avalonia hides it on
// its first frame by adding .splash-close; this script shows progress, the hand-off, and failures.
const splash = document.getElementById('avalonia-splash');
const paper = document.getElementById('splash-paper');
const status = document.getElementById('splash-status');
const detail = document.getElementById('splash-detail');

function showProgress(loaded, total) {
    const fraction = total > 0 ? Math.min(loaded / total, 1) : 0;
    paper.style.setProperty('--progress', fraction.toFixed(3));
    paper.setAttribute('aria-valuenow', String(Math.round(fraction * 100)));
    if (loaded >= total) {
        status.textContent = 'Starting the emulator…';
        detail.textContent = '';
    } else {
        detail.textContent = `Downloaded ${loaded} of ${total} files`;
    }
}

function showError(error) {
    splash.classList.remove('splash-close');
    splash.classList.add('splash-error');
    status.textContent = "The emulator couldn't start. Reload the page to try again.";
    detail.textContent = String(error?.message ?? error);
}

try {
    const dotnetRuntime = await dotnet
        .withModuleConfig({ onDownloadResourceProgress: showProgress })
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

    // Avalonia normally closes the splash on its first frame; make sure it goes once Main has run.
    splash.classList.add('splash-close');
} catch (e) {
    console.error('CrossEscPos: startup failed', e);
    showError(e);
}

import { dotnet } from './_framework/dotnet.js'

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

// Startup splash (index.html): a thermal printer prints one receipt line per downloaded file, fed by the
// Resource Timing API (real names and sizes, cache hits included). Avalonia hides the splash on its first
// frame by adding .splash-close; this script prints the downloads, the hand-off, and failures.
const splash = document.getElementById('avalonia-splash');
const paper = document.getElementById('splash-paper');
const status = document.getElementById('splash-status');
const detail = document.getElementById('splash-detail');

const COLUMNS = 30;       // characters per receipt line
const LINE_HEIGHT = 14;   // px, matches .splash-paper .line
const reduceMotion = matchMedia('(prefers-reduced-motion: reduce)').matches;

// Printer: queued lines print one at a time, each feeding the paper up one line in motor steps.
const queue = [];
let printing = false;

function print(text, ...classes) {
    queue.push({ text, classes });
    if (!printing) printNext();
}

function printNext() {
    const job = queue.shift();
    if (!job) {
        printing = false;
        return;
    }
    printing = true;
    const line = document.createElement('div');
    line.className = ['line', ...job.classes].join(' ');
    line.textContent = job.text;
    paper.append(line);
    // Keep the DOM small: lines far above the fade are gone for good on a real printer too.
    while (paper.childElementCount > 40) paper.firstElementChild.remove();
    const height = line.offsetHeight || LINE_HEIGHT;
    if (!reduceMotion) {
        paper.animate([{ transform: `translateY(${height}px)` }, { transform: 'none' }],
            { duration: 120, easing: 'steps(3, end)' });
    }
    // Catch up when files arrive faster than a printer would print them.
    setTimeout(printNext, queue.length > 12 ? 20 : 90);
}

function row(left, right) {
    const room = COLUMNS - right.length - 1;
    const name = left.length > room ? left.slice(0, room - 1) + '…' : left;
    return name.padEnd(COLUMNS - right.length) + right;
}

function formatSize(bytes) {
    if (bytes >= 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
    return `${Math.max(1, Math.round(bytes / 1024))} KB`;
}

// "System.Private.CoreLib.acqhqif0l6.wasm" -> "System.Private.CoreLib.wasm"
function displayName(url) {
    const file = decodeURIComponent(new URL(url).pathname.split('/').pop() || url);
    return file.replace(/\.[a-z0-9]{10}(?=\.[a-z]+$)/, '');
}

let files = 0;
let bytes = 0;

function onResource(entry) {
    if (!entry.name.startsWith(location.origin)) return;
    const size = entry.encodedBodySize || entry.decodedBodySize || entry.transferSize;
    if (!size || entry.responseStatus >= 400) return; // failed or empty requests aren't downloads
    files++;
    bytes += size;
    print(row(displayName(entry.name), formatSize(size)));
    detail.textContent = `Downloaded ${files} files (${formatSize(bytes)})`;
}

print('CrossEscPos', 'center', 'bold');
print('Loading the emulator', 'center');
print('-'.repeat(COLUMNS));

const downloads = new PerformanceObserver((list) => list.getEntries().forEach(onResource));
downloads.observe({ type: 'resource', buffered: true });

function showError(error) {
    downloads.disconnect();
    splash.classList.remove('splash-close');
    splash.classList.add('splash-error');
    queue.length = 0; // drop pending download lines so the failure prints right away
    print('-'.repeat(COLUMNS));
    print('*** PRINT FAILED ***', 'center', 'bold');
    status.textContent = "The emulator couldn't start. Reload the page to try again.";
    detail.textContent = String(error?.message ?? error);
}

try {
    const dotnetRuntime = await dotnet
        .withDiagnosticTracing(false)
        .withApplicationArgumentsFromQuery()
        .create();

    // Every runtime file is in: total the receipt while Avalonia starts.
    downloads.disconnect();
    splash.classList.add('splash-done');
    print('-'.repeat(COLUMNS));
    print(row(`TOTAL ${files} files`, formatSize(bytes)), 'bold');
    print('Starting the emulator', 'center');
    print('', 'barcode');
    status.textContent = 'Starting the emulator…';

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

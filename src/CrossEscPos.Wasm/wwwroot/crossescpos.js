// crossescpos.js — a tiny, framework-agnostic wrapper around the CrossEscPos WASM render module.
//
// Copy this file (and the published `_framework/` directory) into any web project — plain JS, React,
// Vue, Svelte, etc. It renders ESC/POS byte streams to PNG entirely in the browser.
//
//   import * as CrossEscPos from './crossescpos.js';
//   const url = await CrossEscPos.renderToObjectUrl(escposUint8Array);
//   document.querySelector('img').src = url;

import { dotnet } from './_framework/dotnet.js';

let _exportsPromise = null;

// Boots the .NET runtime once and resolves the exported interop surface.
function getExports() {
    if (!_exportsPromise) {
        _exportsPromise = (async () => {
            const { getAssemblyExports, getConfig } = await dotnet.create();
            const config = getConfig();
            const exports = await getAssemblyExports(config.mainAssemblyName);
            return exports.CrossEscPos.Wasm.ReceiptInterop;
        })();
    }
    return _exportsPromise;
}

/**
 * Render an ESC/POS byte stream to PNG bytes.
 * @param {Uint8Array} escpos raw ESC/POS bytes
 * @returns {Promise<Uint8Array>} PNG bytes (empty if the stream printed nothing)
 */
export async function renderToPng(escpos) {
    const interop = await getExports();
    return interop.RenderToPng(escpos);
}

/** Render to a PNG `Blob`. */
export async function renderToBlob(escpos) {
    return new Blob([await renderToPng(escpos)], { type: 'image/png' });
}

/**
 * Render to an object URL suitable for `<img>.src`. Remember to `URL.revokeObjectURL(url)` when done.
 */
export async function renderToObjectUrl(escpos) {
    return URL.createObjectURL(await renderToBlob(escpos));
}

/** Render to a `data:` URL (handy when you can't manage object-URL lifetimes). */
export async function renderToDataUrl(escpos) {
    const blob = await renderToBlob(escpos);
    return await new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(reader.result);
        reader.onerror = reject;
        reader.readAsDataURL(blob);
    });
}

/** Number of receipts (cuts) the stream would produce. */
export async function countReceipts(escpos) {
    const interop = await getExports();
    return interop.CountReceipts(escpos);
}

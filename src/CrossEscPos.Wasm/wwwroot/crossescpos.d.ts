// TypeScript type definitions for crossescpos.js — render ESC/POS to PNG in the browser.
//
//   import * as CrossEscPos from './crossescpos.js';
//   const url: string = await CrossEscPos.renderToObjectUrl(escpos);

/**
 * Renders an ESC/POS byte stream to PNG bytes (every non-empty receipt stacked top-to-bottom).
 * Returns an empty array when the stream produces no printable content.
 * @param escpos raw ESC/POS bytes
 */
export function renderToPng(escpos: Uint8Array): Promise<Uint8Array>;

/** Renders an ESC/POS byte stream to a PNG `Blob` (`image/png`). */
export function renderToBlob(escpos: Uint8Array): Promise<Blob>;

/**
 * Renders to an object URL suitable for `<img>.src`. Call `URL.revokeObjectURL(url)` when finished.
 */
export function renderToObjectUrl(escpos: Uint8Array): Promise<string>;

/** Renders to a `data:` URL (handy when you can't manage object-URL lifetimes). */
export function renderToDataUrl(escpos: Uint8Array): Promise<string>;

/** Number of receipts (cuts) the stream would produce. */
export function countReceipts(escpos: Uint8Array): Promise<number>;

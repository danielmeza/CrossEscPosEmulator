// Demo wiring — shows how a plain JS page consumes crossescpos.js. Not needed by consumers.
import * as CrossEscPos from './crossescpos.js';

const status = document.getElementById('status');
const input = document.getElementById('input');
const out = document.getElementById('out');

const encoder = new TextEncoder(); // ESC/POS bytes; for the text demo, Latin1-ish ASCII is fine.

async function show(escpos) {
    status.textContent = 'Rendering…';
    const url = await CrossEscPos.renderToObjectUrl(escpos);
    if (out.dataset.url) URL.revokeObjectURL(out.dataset.url);
    out.src = url;
    out.dataset.url = url;
    status.textContent = `Rendered ${await CrossEscPos.countReceipts(escpos)} receipt(s).`;
}

document.getElementById('render').addEventListener('click', () => show(encoder.encode(input.value)));

document.getElementById('sample').addEventListener('click', async () => {
    const bytes = new Uint8Array(await (await fetch('./sample.escpos')).arrayBuffer());
    await show(bytes);
});

// Warm up the runtime so the first click is instant.
CrossEscPos.countReceipts(new Uint8Array()).then(
    () => { status.textContent = 'Ready.'; },
    err => { status.textContent = 'Failed to load: ' + err; });

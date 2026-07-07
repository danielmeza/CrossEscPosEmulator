// Browser transports for the CrossEscPos web emulator.
//
// Two globals — `crossescposSerial` (Web Serial API) and `crossescposUsb` (WebUSB API) — each exposing
// isSupported / connect(dotNetRef) / write(bytes) / disconnect(). They read incoming ESC/POS from the
// device and hand each chunk to .NET via `dotNetRef.invokeMethodAsync('OnDataReceived', Uint8Array)`;
// the printer's status replies come back through `write()`. Bytes cross as Uint8Array both ways
// (Blazor's native byte-array interop). Requires Chromium + a secure context (HTTPS or localhost) and a
// user gesture for the device picker.

// ---------- Web Serial ----------
window.crossescposSerial = (function () {
  let port = null, reader = null, ref = null, keepReading = false;

  const isSupported = () => 'serial' in navigator;

  async function connect(dotNetRef) {
    let selected;
    try {
      selected = await navigator.serial.requestPort();   // must be first — needs the user gesture
    } catch { return null; }                             // user cancelled / no port
    try {
      await selected.open({ baudRate: 9600 });
    } catch { return null; }
    port = selected; ref = dotNetRef; keepReading = true;
    readLoop();                                          // fire-and-forget
    const info = selected.getInfo ? selected.getInfo() : {};
    const vid = info.usbVendorId ? ` (VID 0x${info.usbVendorId.toString(16)})` : '';
    return `Serial port${vid} @ 9600 baud`;
  }

  async function readLoop() {
    while (port && port.readable && keepReading) {
      reader = port.readable.getReader();
      try {
        while (true) {
          const { value, done } = await reader.read();
          if (done) break;
          if (value && value.length && ref) await ref.invokeMethodAsync('OnDataReceived', value);
        }
      } catch { /* stream error — usually a disconnect */ }
      finally { try { reader.releaseLock(); } catch {} reader = null; }
    }
    notifyClosed();
  }

  async function write(data) {
    if (!port || !port.writable) return;
    const writer = port.writable.getWriter();
    try { await writer.write(data instanceof Uint8Array ? data : new Uint8Array(data)); }
    catch {} finally { writer.releaseLock(); }
  }

  async function disconnect() {
    keepReading = false;
    try { if (reader) await reader.cancel(); } catch {}
    try { if (port) await port.close(); } catch {}
    port = null; reader = null;
  }

  function notifyClosed() { if (ref) { try { ref.invokeMethodAsync('OnClosed'); } catch {} } }

  return { isSupported, connect, write, disconnect };
})();

// ---------- WebUSB ----------
window.crossescposUsb = (function () {
  let device = null, epIn = null, epOut = null, packet = 64, ref = null, reading = false;

  const isSupported = () => 'usb' in navigator;

  async function connect(dotNetRef) {
    let dev;
    try {
      dev = await navigator.usb.requestDevice({ filters: [] });   // must be first — needs the user gesture
    } catch { return null; }
    try {
      await dev.open();
      if (dev.configuration === null) await dev.selectConfiguration(1);
      const claimed = await claimBulkInterface(dev);
      if (!claimed) { await safeClose(dev); return null; }
      device = dev; epIn = claimed.epIn; epOut = claimed.epOut; packet = claimed.packet;
      ref = dotNetRef; reading = true;
      readLoop();
      const name = [dev.manufacturerName, dev.productName].filter(Boolean).join(' ') ||
        `USB ${hex(dev.vendorId)}:${hex(dev.productId)}`;
      return name;
    } catch { await safeClose(dev); return null; }
  }

  async function claimBulkInterface(dev) {
    for (const iface of dev.configuration.interfaces) {
      const alt = iface.alternate;
      const inEp = alt.endpoints.find(e => e.direction === 'in' && e.type === 'bulk');
      if (!inEp) continue;
      const outEp = alt.endpoints.find(e => e.direction === 'out' && e.type === 'bulk');
      try {
        await dev.claimInterface(iface.interfaceNumber);
        return { epIn: inEp.endpointNumber, epOut: outEp ? outEp.endpointNumber : null, packet: inEp.packetSize || 64 };
      } catch { /* try the next interface */ }
    }
    return null;
  }

  async function readLoop() {
    while (device && reading) {
      try {
        const result = await device.transferIn(epIn, packet);
        if (result.status === 'stall') { await device.clearHalt('in', epIn); continue; }
        if (result.data && result.data.byteLength && ref) {
          await ref.invokeMethodAsync('OnDataReceived', new Uint8Array(result.data.buffer));
        }
      } catch { break; /* device gone */ }
    }
    notifyClosed();
  }

  async function write(data) {
    if (!device || epOut === null) return;
    try { await device.transferOut(epOut, data instanceof Uint8Array ? data : new Uint8Array(data)); } catch {}
  }

  async function disconnect() {
    reading = false;
    const d = device; device = null;
    await safeClose(d);
  }

  async function safeClose(d) { try { if (d && d.opened) await d.close(); } catch {} }
  function notifyClosed() { if (ref) { try { ref.invokeMethodAsync('OnClosed'); } catch {} } }
  const hex = n => (n || 0).toString(16).padStart(4, '0');

  return { isSupported, connect, write, disconnect };
})();

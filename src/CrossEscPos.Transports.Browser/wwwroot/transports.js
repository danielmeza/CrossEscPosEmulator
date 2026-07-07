// Shared browser transports for CrossEscPos (used by both the Blazor and the Avalonia WASM hosts).
//
// Exposes `window.crossescpos` with kind-dispatched calls — isSupported/connect/write/disconnect, where
// kind is "serial" (Web Serial API) or "usb" (WebUSB API). Incoming ESC/POS is delivered to the host by
// calling the `crossescpos.onData(kind, Uint8Array)` / `crossescpos.onClosed(kind)` slots, which each
// host wires to .NET (Blazor via a DotNetObjectReference — see registerDotNet; Avalonia WASM via
// [JSExport], wired in main.js). Requires Chromium + a secure context + a user gesture for the picker.

(function () {
  const cx = (window.crossescpos = window.crossescpos || {});

  // Host-set delivery slots (no-ops until a host wires them).
  cx.onData = cx.onData || function () {};       // (kind, Uint8Array)
  cx.onClosed = cx.onClosed || function () {};   // (kind)

  // Blazor helper: route the delivery slots through a DotNetObjectReference.
  cx.registerDotNet = function (ref) {
    cx.onData = (kind, u8) => ref.invokeMethodAsync('OnData', kind, u8);
    cx.onClosed = (kind) => ref.invokeMethodAsync('OnClosed', kind);
  };

  const toBytes = (payload) =>
    payload instanceof Uint8Array ? payload
      : (typeof payload === 'string' ? Uint8Array.from(atob(payload), c => c.charCodeAt(0))
      : new Uint8Array(payload));

  // A SerialPort-shaped object backed by WebUSB (CDC-ACM) — the "serial port lib in JS" that lets the
  // serial transport work on browsers without native Web Serial (mirrors google/web-serial-polyfill).
  // It exposes open()/readable/writable/close()/getInfo() so the serial code below is identical whether
  // the port came from navigator.serial or from here.
  function usbSerialPort(device) {
    let epIn = 0, epOut = 0, ctrlIface = 0, dataIface = 0, packet = 64, readable = null, writable = null;
    const findBulk = (alt) => ({
      inEp: alt.endpoints.find(e => e.direction === 'in' && e.type === 'bulk'),
      outEp: alt.endpoints.find(e => e.direction === 'out' && e.type === 'bulk'),
    });
    return {
      getInfo: () => ({ usbVendorId: device.vendorId, usbProductId: device.productId }),
      get readable() { return readable; },
      get writable() { return writable; },
      async open(options) {
        const baud = (options && options.baudRate) || 9600;
        await device.open();
        if (device.configuration === null) await device.selectConfiguration(1);
        // Prefer a CDC data interface (class 0x0A) + its comm interface (0x02) for the control transfers;
        // otherwise fall back to the first interface exposing bulk in+out.
        for (const iface of device.configuration.interfaces) {
          const alt = iface.alternate, b = findBulk(alt);
          if (alt.interfaceClass === 0x0a && b.inEp && b.outEp) {
            dataIface = iface.interfaceNumber; epIn = b.inEp.endpointNumber; epOut = b.outEp.endpointNumber; packet = b.inEp.packetSize || 64;
          }
          if (alt.interfaceClass === 0x02) ctrlIface = iface.interfaceNumber;
        }
        if (!epIn || !epOut) {
          for (const iface of device.configuration.interfaces) {
            const b = findBulk(iface.alternate);
            if (b.inEp && b.outEp) { dataIface = ctrlIface = iface.interfaceNumber; epIn = b.inEp.endpointNumber; epOut = b.outEp.endpointNumber; packet = b.inEp.packetSize || 64; break; }
          }
        }
        await device.claimInterface(dataIface);
        if (ctrlIface !== dataIface) { try { await device.claimInterface(ctrlIface); } catch {} }
        // CDC: SET_LINE_CODING (baud, 8N1) + SET_CONTROL_LINE_STATE (DTR|RTS). Harmless on non-CDC devices.
        try {
          const coding = new DataView(new ArrayBuffer(7));
          coding.setUint32(0, baud, true); coding.setUint8(6, 8);
          await device.controlTransferOut({ requestType: 'class', recipient: 'interface', request: 0x20, value: 0, index: ctrlIface }, coding.buffer);
          await device.controlTransferOut({ requestType: 'class', recipient: 'interface', request: 0x22, value: 0x03, index: ctrlIface });
        } catch { /* not a CDC device — raw bulk still works */ }
        readable = new ReadableStream({
          async pull(controller) {
            try {
              const r = await device.transferIn(epIn, packet);
              if (r.status === 'stall') { await device.clearHalt('in', epIn); return; }
              if (r.data && r.data.byteLength) controller.enqueue(new Uint8Array(r.data.buffer));
            } catch (e) { controller.error(e); }
          },
        });
        writable = new WritableStream({ write: (chunk) => device.transferOut(epOut, chunk) });
      },
      async close() { try { if (device.opened) await device.close(); } catch {} },
    };
  }

  // ---- Web Serial (native, with a WebUSB CDC-ACM fallback) ----
  // A factory (not a singleton) so independent channels can each own a device — e.g. the emulator
  // receiving on "serial" and the Monitor sending on "mon-serial" at the same time. `chan` is the kind
  // used for the onData/onClosed callbacks.
  function makeSerial(chan) {
    let port = null, reader = null, keep = false;
    // Supported natively (Chromium) or emulated over WebUSB anywhere WebUSB exists.
    const isSupported = () => ('serial' in navigator) || ('usb' in navigator);
    async function connect(options) {
      const baud = parseInt(options, 10) || 9600;
      let sel, via = '';
      if ('serial' in navigator) {
        try { sel = await navigator.serial.requestPort(); } catch { return null; }
      } else if ('usb' in navigator) {
        let dev; try { dev = await navigator.usb.requestDevice({ filters: [] }); } catch { return null; }
        sel = usbSerialPort(dev); via = ' (via WebUSB)';
      } else { return null; }
      try { await sel.open({ baudRate: baud }); } catch { return null; }
      port = sel; keep = true; loop();
      const info = sel.getInfo ? sel.getInfo() : {};
      return `Serial port${info.usbVendorId ? ` (VID 0x${info.usbVendorId.toString(16)})` : ''} @ ${baud} baud${via}`;
    }
    async function loop() {
      while (port && port.readable && keep) {
        reader = port.readable.getReader();
        try { for (;;) { const { value, done } = await reader.read(); if (done) break; if (value && value.length) cx.onData(chan, value); } }
        catch {} finally { try { reader.releaseLock(); } catch {} reader = null; }
      }
      cx.onClosed(chan);
    }
    async function write(payload) {
      if (!port || !port.writable) return;
      const w = port.writable.getWriter();
      try { await w.write(toBytes(payload)); } catch {} finally { w.releaseLock(); }
    }
    async function disconnect() {
      keep = false;
      try { if (reader) await reader.cancel(); } catch {}
      try { if (port) await port.close(); } catch {}
      port = null; reader = null;
    }
    return { isSupported, connect, write, disconnect };
  }

  // ---- WebUSB ----
  // Factory, like makeSerial. `connect(options)` uses the picker; when `options` is a numeric string it
  // opens that entry from navigator.usb.getDevices() instead (so the Monitor can pick from a list).
  function makeUsb(chan) {
    let device = null, epIn = null, epOut = null, packet = 64, reading = false;
    const isSupported = () => 'usb' in navigator;
    async function connect(options) {
      let dev;
      const idx = parseInt(options, 10);
      if (Number.isInteger(idx) && idx >= 0) {
        try { dev = (await navigator.usb.getDevices())[idx]; } catch {}
      }
      if (!dev) { try { dev = await navigator.usb.requestDevice({ filters: [] }); } catch { return null; } }
      try {
        await dev.open();
        if (dev.configuration === null) await dev.selectConfiguration(1);
        const claimed = await claim(dev);
        if (!claimed) { await close(dev); return null; }
        device = dev; epIn = claimed.epIn; epOut = claimed.epOut; packet = claimed.packet; reading = true; loop();
        const hex = n => (n || 0).toString(16).padStart(4, '0');
        return [dev.manufacturerName, dev.productName].filter(Boolean).join(' ') || `USB ${hex(dev.vendorId)}:${hex(dev.productId)}`;
      } catch { await close(dev); return null; }
    }
    async function claim(dev) {
      for (const iface of dev.configuration.interfaces) {
        const alt = iface.alternate;
        const inEp = alt.endpoints.find(e => e.direction === 'in' && e.type === 'bulk');
        if (!inEp) continue;
        const outEp = alt.endpoints.find(e => e.direction === 'out' && e.type === 'bulk');
        try { await dev.claimInterface(iface.interfaceNumber); return { epIn: inEp.endpointNumber, epOut: outEp ? outEp.endpointNumber : null, packet: inEp.packetSize || 64 }; }
        catch {}
      }
      return null;
    }
    async function loop() {
      while (device && reading) {
        try {
          const r = await device.transferIn(epIn, packet);
          if (r.status === 'stall') { await device.clearHalt('in', epIn); continue; }
          if (r.data && r.data.byteLength) cx.onData(chan, new Uint8Array(r.data.buffer));
        } catch { break; }
      }
      cx.onClosed(chan);
    }
    async function write(payload) {
      if (!device || epOut === null) return;
      try { await device.transferOut(epOut, toBytes(payload)); } catch {}
    }
    async function disconnect() { reading = false; const d = device; device = null; await close(d); }
    async function close(d) { try { if (d && d.opened) await d.close(); } catch {} }
    return { isSupported, connect, write, disconnect };
  }

  // One instance per kind, created on demand. "serial"/"usb" are the emulator's; "mon-serial"/"mon-usb"
  // are the Monitor's independent sender channels — a *usb kind uses WebUSB, anything else Web Serial.
  const channels = {};
  const impl = (kind) => (channels[kind] ||= (kind.indexOf('usb') >= 0 ? makeUsb(kind) : makeSerial(kind)));

  cx.isSupported = (kind) => impl(kind).isSupported();
  cx.connect = (kind, options) => impl(kind).connect(options);
  cx.write = (kind, payload) => impl(kind).write(payload);
  cx.disconnect = (kind) => impl(kind).disconnect();

  // Paired WebUSB devices as newline-joined "vid:pid name" labels (index = getDevices order) — lets the
  // Monitor list devices to pick from. Newline-joined (not JSON) so the .NET side stays trim-safe.
  cx.listUsb = async () => {
    if (!('usb' in navigator)) return '';
    const hex = n => (n || 0).toString(16).padStart(4, '0');
    const devices = await navigator.usb.getDevices();
    return devices.map(d =>
      `${hex(d.vendorId)}:${hex(d.productId)}${d.productName ? ' ' + d.productName : ''}`).join('\n');
  };

  // The page's origin, so the SignalR TCP-proxy transport defaults to the same host that served the app.
  cx.origin = () => globalThis.location.origin;

  // Download bytes as a file (blob + anchor) — works in every browser, unlike the File System Access API.
  cx.downloadFile = (name, b64) => {
    const bytes = Uint8Array.from(atob(b64), c => c.charCodeAt(0));
    const url = URL.createObjectURL(new Blob([bytes], { type: 'image/png' }));
    const a = document.createElement('a');
    a.href = url; a.download = name || 'receipt.png';
    document.body.appendChild(a); a.click(); a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  };
})();

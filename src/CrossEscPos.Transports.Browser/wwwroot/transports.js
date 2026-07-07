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

  // ---- Web Serial ----
  const serial = (() => {
    let port = null, reader = null, keep = false;
    const isSupported = () => 'serial' in navigator;
    async function connect() {
      let sel; try { sel = await navigator.serial.requestPort(); } catch { return null; }
      try { await sel.open({ baudRate: 9600 }); } catch { return null; }
      port = sel; keep = true; loop();
      const info = sel.getInfo ? sel.getInfo() : {};
      return `Serial port${info.usbVendorId ? ` (VID 0x${info.usbVendorId.toString(16)})` : ''} @ 9600 baud`;
    }
    async function loop() {
      while (port && port.readable && keep) {
        reader = port.readable.getReader();
        try { for (;;) { const { value, done } = await reader.read(); if (done) break; if (value && value.length) cx.onData('serial', value); } }
        catch {} finally { try { reader.releaseLock(); } catch {} reader = null; }
      }
      cx.onClosed('serial');
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
  })();

  // ---- WebUSB ----
  const usb = (() => {
    let device = null, epIn = null, epOut = null, packet = 64, reading = false;
    const isSupported = () => 'usb' in navigator;
    async function connect() {
      let dev; try { dev = await navigator.usb.requestDevice({ filters: [] }); } catch { return null; }
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
          if (r.data && r.data.byteLength) cx.onData('usb', new Uint8Array(r.data.buffer));
        } catch { break; }
      }
      cx.onClosed('usb');
    }
    async function write(payload) {
      if (!device || epOut === null) return;
      try { await device.transferOut(epOut, toBytes(payload)); } catch {}
    }
    async function disconnect() { reading = false; const d = device; device = null; await close(d); }
    async function close(d) { try { if (d && d.opened) await d.close(); } catch {} }
    return { isSupported, connect, write, disconnect };
  })();

  const impl = (kind) => (kind === 'usb' ? usb : serial);

  cx.isSupported = (kind) => impl(kind).isSupported();
  cx.connect = (kind) => impl(kind).connect();
  cx.write = (kind, payload) => impl(kind).write(payload);
  cx.disconnect = (kind) => impl(kind).disconnect();
})();

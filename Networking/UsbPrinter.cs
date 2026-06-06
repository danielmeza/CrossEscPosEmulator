using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using ESCPOS_NET;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;

namespace ReceiptPrinterEmulator.Networking;

/// <summary>A connected USB device, identified by vendor/product id.</summary>
public sealed record UsbDeviceInfo(int Vid, int Pid)
{
    public string Display => $"USB {Vid:X4}:{Pid:X4}";
}

/// <summary>
/// A <see cref="Stream"/> backed by a USB device's bulk endpoints: writes go to the bulk-OUT
/// endpoint, reads come from the bulk-IN endpoint (the printer's status channel).
///
/// USB bulk transfers are packet-oriented, but <see cref="BasePrinter"/> reads its status channel
/// one byte at a time. So reads are buffered: a single bulk transfer fills an internal buffer that
/// is then served byte-by-byte. <see cref="Read"/> returns 0 on timeout (no data yet) rather than
/// blocking, which keeps the printer's read loop responsive without hot-spinning.
/// </summary>
internal sealed class UsbStream : Stream
{
    private const int WriteTimeoutMs = 5000;
    private const int ReadTimeoutMs = 200;
    private const int ReadPacketSize = 64; // covers a full-speed bulk max packet; ASB blocks are 4 bytes.

    private readonly UsbContext _context;
    private readonly IDisposable _devices; // the device collection — keep alive so the device handle stays valid.
    private readonly IUsbDevice _device;
    private readonly UsbEndpointWriter _writer;
    private readonly UsbEndpointReader? _reader;

    private readonly byte[] _readBuffer = new byte[ReadPacketSize];
    private int _readPos;
    private int _readLen;

    // Native libusb transfers and the device/context teardown must never overlap: closing the
    // handle while a bulk read is in flight on the printer's background read thread is a native
    // use-after-free that crashes the whole process (no managed catch can stop it). All native I/O
    // and the close run under this lock, and _closing short-circuits any I/O issued after teardown.
    private readonly Lock _ioLock = new();
    private volatile bool _closing;

    public UsbStream(UsbContext context, IDisposable devices, IUsbDevice device,
        UsbEndpointWriter writer, UsbEndpointReader? reader)
    {
        _context = context;
        _devices = devices;
        _device = device;
        _writer = writer;
        _reader = reader;
    }

    public override bool CanRead => _reader is not null;
    public override bool CanWrite => true;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override void Flush() { }

    public override void Write(byte[] buffer, int offset, int count)
    {
        var data = offset == 0 && count == buffer.Length ? buffer : buffer.AsSpan(offset, count).ToArray();
        lock (_ioLock)
        {
            if (_closing) return;
            _writer.Write(data, WriteTimeoutMs, out _);
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_reader is null || _closing)
        {
            // Send-only device (no bulk-IN endpoint), or we're tearing down: idle briefly so the
            // printer's read loop doesn't spin, and never touch the (closing) native handle.
            Thread.Sleep(ReadTimeoutMs);
            return 0;
        }

        if (_readPos >= _readLen)
        {
            lock (_ioLock)
            {
                if (_closing) return 0; // device closed out from under us between the check and the lock.
                _reader.Read(_readBuffer, ReadTimeoutMs, out int got);
                _readPos = 0;
                _readLen = Math.Max(0, got);
            }
            if (_readLen == 0)
                return 0; // timed out with no status bytes — try again next loop.
        }

        int n = Math.Min(count, _readLen - _readPos);
        Array.Copy(_readBuffer, _readPos, buffer, offset, n);
        _readPos += n;
        return n;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Flag first so any read/write that hasn't yet taken the lock bails out without touching
            // the native handle, then take the lock to wait for any in-flight transfer (bounded by
            // the read/write timeouts) to finish before freeing the device and context.
            _closing = true;
            lock (_ioLock)
            {
                try { _device.Close(); } catch { /* ignore */ }
                try { _devices.Dispose(); } catch { /* ignore */ }
                try { _context.Dispose(); } catch { /* ignore */ }
            }
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Direct USB printing via libusb (LibUsbDotNet), exposed as an ESC-POS <see cref="BasePrinter"/> so
/// it shares the exact same write queue and Automatic-Status-Back pipeline as the serial/TCP
/// printers — bytes go out the bulk-OUT endpoint and status comes back on the bulk-IN endpoint, so
/// the monitor reflects the emulator's reported state for USB too.
///
/// Requires native libusb-1.0 at runtime (macOS: <c>brew install libusb</c>; Debian/Ubuntu:
/// <c>apt install libusb-1.0-0</c>; bundled on Windows). If it's missing, or the OS already owns the
/// device (e.g. a print queue has claimed it), construction throws and the caller surfaces the error.
/// </summary>
public sealed class UsbPrinter : BasePrinter
{
    private const byte EndpointDirectionMask = 0x80; // bit 7 of bEndpointAddress: 0 = OUT, 1 = IN.
    private const byte EndpointTransferTypeMask = 0x03; // low 2 bits of bmAttributes select the transfer type.

    private readonly UsbStream _stream;

    static UsbPrinter() => EnsureNativeSearchPath();

    public UsbPrinter(int vid, int pid) : base($"USB {vid:X4}:{pid:X4}")
    {
        EnsureNativeSearchPath();

        var context = new UsbContext();
        var devices = context.List();
        var transferred = false;
        try
        {
            var device = devices.FirstOrDefault(d => d.VendorId == vid && d.ProductId == pid)
                ?? throw new InvalidOperationException($"USB device {vid:X4}:{pid:X4} is not connected.");

            device.Open();
            if (device.Configs.Count == 0 || device.Configs[0].Interfaces.Count == 0)
                throw new InvalidOperationException("USB device exposes no usable interface.");

            device.ClaimInterface(device.Configs[0].Interfaces[0].Number);

            var (outEndpoint, inEndpoint) = FindBulkEndpoints(device);
            var writer = device.OpenEndpointWriter(outEndpoint);

            UsbEndpointReader? reader = null;
            if (inEndpoint.HasValue)
            {
                // A missing/unsupported status endpoint shouldn't block printing — fall back to send-only.
                try { reader = device.OpenEndpointReader(inEndpoint.Value); }
                catch { reader = null; }
            }

            _stream = new UsbStream(context, devices, device, writer, reader);
            Writer = new BinaryWriter(_stream);
            Reader = new BinaryReader(_stream);
            transferred = true; // _stream now owns the device collection + context.
        }
        finally
        {
            if (!transferred)
            {
                try { devices.Dispose(); } catch { /* ignore */ }
                context.Dispose();
            }
        }
    }

    protected override void OverridableDispose()
    {
        try { _stream?.Dispose(); } catch { /* ignore */ }
    }

    /// <summary>Enumerates connected USB devices (vendor/product ids).</summary>
    public static IReadOnlyList<UsbDeviceInfo> ListDevices()
    {
        EnsureNativeSearchPath();
        using var context = new UsbContext();
        using var devices = context.List();
        return devices.Select(d => new UsbDeviceInfo(d.VendorId, d.ProductId)).ToList();
    }

    /// <summary>
    /// Picks the device's first bulk-OUT endpoint (for printing) and first bulk-IN endpoint (for
    /// status), reading directions/types straight from the descriptors. Falls back to
    /// <see cref="WriteEndpointID.Ep01"/> for output if none is advertised; the status endpoint is
    /// optional (null ⇒ send-only).
    /// </summary>
    private static (WriteEndpointID outEndpoint, ReadEndpointID? inEndpoint) FindBulkEndpoints(IUsbDevice device)
    {
        WriteEndpointID? outEndpoint = null;
        ReadEndpointID? inEndpoint = null;
        try
        {
            foreach (var iface in device.Configs[0].Interfaces)
            {
                foreach (var endpoint in iface.Endpoints)
                {
                    if ((endpoint.Attributes & EndpointTransferTypeMask) != (byte)EndpointType.Bulk)
                        continue;

                    if ((endpoint.EndpointAddress & EndpointDirectionMask) != 0)
                        inEndpoint ??= (ReadEndpointID)endpoint.EndpointAddress;
                    else
                        outEndpoint ??= (WriteEndpointID)endpoint.EndpointAddress;
                }
            }
        }
        catch { /* descriptor walk failed — fall back below */ }

        return (outEndpoint ?? WriteEndpointID.Ep01, inEndpoint);
    }

    private static bool _searchPathConfigured;

    private static void EnsureNativeSearchPath()
    {
        if (_searchPathConfigured)
            return;
        _searchPathConfigured = true;

        // LibUsbDotNet loads libusb itself and searches NATIVE_DLL_SEARCH_DIRECTORIES (then the
        // default OS path). The default loader doesn't look in Homebrew/manual install locations, so
        // append them here — before any libusb call — making an installed libusb discoverable without
        // DYLD_LIBRARY_PATH or symlinks. (Do NOT register a DllImportResolver: LibUsbDotNet registers
        // its own on the same assembly and a second registration throws during its type init.)
        try
        {
            string[] dirs =
                OperatingSystem.IsMacOS()
                    ? new[] { "/opt/homebrew/lib", "/opt/homebrew/opt/libusb/lib", "/usr/local/lib", "/usr/local/opt/libusb/lib" }
                    : OperatingSystem.IsLinux()
                        ? new[] { "/usr/lib", "/usr/local/lib", "/usr/lib/x86_64-linux-gnu", "/usr/lib/aarch64-linux-gnu", "/lib/x86_64-linux-gnu" }
                        : Array.Empty<string>();

            if (dirs.Length > 0)
            {
                var existing = AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") as string;
                var all = string.IsNullOrEmpty(existing) ? dirs : new[] { existing }.Concat(dirs);
                AppContext.SetData("NATIVE_DLL_SEARCH_DIRECTORIES", string.Join(":", all));
            }
        }
        catch { /* best-effort; fall back to default resolution */ }
    }
}

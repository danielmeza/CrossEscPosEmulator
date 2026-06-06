using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;

namespace ReceiptPrinterEmulator.Networking;

/// <summary>A connected USB device, identified by vendor/product id.</summary>
public sealed record UsbDeviceInfo(int Vid, int Pid)
{
    public string Display => $"USB {Vid:X4}:{Pid:X4}";
}

/// <summary>
/// Direct USB printing via libusb (LibUsbDotNet): claim the device's first interface and write
/// ESC/POS bytes to its first bulk-OUT endpoint. Send-only — no status is read back.
///
/// Requires native libusb-1.0 at runtime (macOS: <c>brew install libusb</c>; Debian/Ubuntu:
/// <c>apt install libusb-1.0-0</c>; bundled on Windows). If it's missing, or the OS already owns the
/// device (e.g. a print queue has claimed it), opening throws and the caller surfaces the error.
/// </summary>
public sealed class UsbBulkTransport : IDisposable
{
    private readonly UsbContext _context;
    private readonly IUsbDevice _device;
    private readonly UsbEndpointWriter _writer;

    private UsbBulkTransport(UsbContext context, IUsbDevice device, UsbEndpointWriter writer)
    {
        _context = context;
        _device = device;
        _writer = writer;
    }

    static UsbBulkTransport()
    {
        // .NET's default loader doesn't search Homebrew/manual install paths, so an installed libusb
        // can still fail to load. Map LibUsbDotNet's "libusb-1.0" P/Invokes to the real dylib/so.
        try
        {
            NativeLibrary.SetDllImportResolver(typeof(UsbContext).Assembly, ResolveLibusb);
        }
        catch { /* resolver already set, or unsupported — fall back to default resolution */ }
    }

    private static IntPtr ResolveLibusb(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName.IndexOf("usb", StringComparison.OrdinalIgnoreCase) < 0)
            return IntPtr.Zero; // not ours — let the runtime handle it

        // Prefer a copy shipped next to the executable, then well-known system locations.
        var local = System.IO.Path.Combine(AppContext.BaseDirectory,
            OperatingSystem.IsMacOS() ? "libusb-1.0.0.dylib"
            : OperatingSystem.IsLinux() ? "libusb-1.0.so.0"
            : "libusb-1.0.dll");
        if (NativeLibrary.TryLoad(local, out var localHandle))
            return localHandle;

        string[] candidates =
            OperatingSystem.IsMacOS()
                ? new[]
                {
                    "/opt/homebrew/lib/libusb-1.0.0.dylib",
                    "/opt/homebrew/opt/libusb/lib/libusb-1.0.0.dylib",
                    "/usr/local/lib/libusb-1.0.0.dylib",
                    "/usr/local/opt/libusb/lib/libusb-1.0.0.dylib",
                    "libusb-1.0.0.dylib",
                }
                : OperatingSystem.IsLinux()
                    ? new[] { "libusb-1.0.so.0", "libusb-1.0.so", "/usr/lib/x86_64-linux-gnu/libusb-1.0.so.0" }
                    : new[] { "libusb-1.0.dll" };

        foreach (var candidate in candidates)
            if (NativeLibrary.TryLoad(candidate, out var handle))
                return handle;

        return IntPtr.Zero; // none found — default resolution will surface the "not found" error
    }

    /// <summary>Enumerates connected USB devices (vendor/product ids).</summary>
    public static IReadOnlyList<UsbDeviceInfo> List()
    {
        using var context = new UsbContext();
        using var devices = context.List();
        return devices.Select(d => new UsbDeviceInfo(d.VendorId, d.ProductId)).ToList();
    }

    /// <summary>Opens the device with the given VID/PID and prepares its bulk-OUT endpoint.</summary>
    public static UsbBulkTransport Open(int vid, int pid)
    {
        var context = new UsbContext();
        try
        {
            using var devices = context.List();
            var device = devices.FirstOrDefault(d => d.VendorId == vid && d.ProductId == pid)
                ?? throw new InvalidOperationException($"USB device {vid:X4}:{pid:X4} is not connected.");

            device.Open();
            if (device.Configs.Count == 0 || device.Configs[0].Interfaces.Count == 0)
                throw new InvalidOperationException("USB device exposes no usable interface.");

            device.ClaimInterface(device.Configs[0].Interfaces[0].Number);
            var writer = device.OpenEndpointWriter(WriteEndpointID.Ep01);

            return new UsbBulkTransport(context, device, writer);
        }
        catch
        {
            context.Dispose();
            throw;
        }
    }

    /// <summary>Writes ESC/POS bytes to the printer's bulk-OUT endpoint.</summary>
    public void Write(byte[] data)
    {
        _writer.Write(data, 5000, out int transferred);
        if (transferred < data.Length)
            throw new InvalidOperationException($"USB write incomplete: {transferred}/{data.Length} bytes.");
    }

    public void Dispose()
    {
        try { _device.Close(); } catch { /* ignore */ }
        try { _context.Dispose(); } catch { /* ignore */ }
    }
}

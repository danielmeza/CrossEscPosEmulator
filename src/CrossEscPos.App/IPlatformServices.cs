using System.Collections.Generic;
using Avalonia.Controls;
using CrossEscPos.Controls.Services;
using CrossEscPos.Emulator;
using CrossEscPos.Graphics;

namespace CrossEscPos.App;

/// <summary>
/// Everything the shared <see cref="App"/> / <see cref="ViewModels.MainViewModel"/> needs from the host
/// platform. The Desktop and Browser heads implement this; the rest of the app (rendering, receipts,
/// printer state, export, notifications, the paste/upload input) is shared and platform-agnostic.
/// </summary>
public interface IPlatformServices
{
    // Render backend (chosen by the head — Skia on desktop, Skia in the Avalonia browser).
    IReceiptImageFactory ImageFactory { get; }
    ITypefaceProvider Typefaces { get; }
    IImageEncoder Encoder { get; }
    string BackendName { get; }

    // Shared services (Avalonia's implementations are cross-platform — export via IStorageProvider even
    // triggers a browser download).
    IFileDialogService FileDialogs { get; }
    INotificationService Notifications { get; }

    /// <summary>The bundled sample ESC/POS ticket (text + barcodes + QR), or empty if none.</summary>
    byte[] SampleTicket { get; }

    /// <summary>
    /// The platform's transports, wired to <paramref name="printer"/> — desktop yields TCP + serial;
    /// the browser yields Web Serial + WebUSB + SignalR. The shared connections view renders them all.
    /// </summary>
    IReadOnlyList<Transports.TransportEntry> CreateTransports(ReceiptPrinter printer);

    /// <summary>
    /// Creates the platform's Monitor transport (desktop: TCP/serial/USB over ESC-POS-.NET; browser:
    /// SignalR to the host's proxy hub), or null if the platform has no Monitor. The shared Monitor view
    /// + test-job generation are platform-agnostic.
    /// </summary>
    Monitor.IMonitorClient? CreateMonitorClient();

    /// <summary>Whether the Monitor opens in its own window (desktop) or an in-page panel (browser).</summary>
    bool MonitorInWindow { get; }

    /// <summary>Opens the Monitor in a platform window (desktop only; unused when it hosts in-page).</summary>
    void ShowMonitorWindow(Monitor.MonitorViewModel monitor);

    /// <summary>
    /// Called with the shared main view once created, so the platform can wire top-level-dependent
    /// services (e.g. the export dialog resolves its <c>TopLevel</c> from this control).
    /// </summary>
    void AttachRoot(Control mainView);

    /// <summary>Wraps the shared main view in the platform's top-level window (desktop only).</summary>
    Window CreateMainWindow(Control content);

    /// <summary>Called on application shutdown so the head can stop transports, etc.</summary>
    void Shutdown();
}

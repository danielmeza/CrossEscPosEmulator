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
    /// Builds the platform's "connections" UI (its transports), wired to <paramref name="printer"/>.
    /// Desktop returns a TCP/serial panel; the browser returns a Web Serial / WebUSB / WebSocket panel.
    /// </summary>
    Control CreateConnectionsView(ReceiptPrinter printer);

    /// <summary>Whether this platform offers the separate Monitor test-client window (desktop only).</summary>
    bool SupportsMonitor { get; }

    /// <summary>Opens the Monitor window (desktop only; no-op elsewhere).</summary>
    void OpenMonitor();

    /// <summary>Wraps the shared main view in the platform's top-level window (desktop only).</summary>
    Window CreateMainWindow(Control content);

    /// <summary>Called on application shutdown so the head can stop transports, etc.</summary>
    void Shutdown();
}

using System;
using Avalonia.Controls;
using CrossEscPos.App;
using CrossEscPos.App.Browser.ViewModels;
using CrossEscPos.App.Browser.Views;
using CrossEscPos.Controls.Services;
using CrossEscPos.Emulator;
using CrossEscPos.Graphics;

namespace CrossEscPos.App.Browser;

/// <summary>
/// The browser platform: the Skia render backend (Avalonia's browser renderer already links it),
/// export via Avalonia's storage provider (a download), no-op notifications, and the Web Serial /
/// WebUSB / WebSocket transports. Injected into the shared <see cref="CrossEscPos.App.App"/>, which
/// runs as a single view here (no window, no Monitor).
/// </summary>
public sealed class BrowserPlatformServices : IPlatformServices
{
    private readonly RenderBackend _backend = RenderBackend.Select(Array.Empty<string>()); // Skia
    private readonly FileDialogService _dialogs = new();
    private readonly BrowserNotificationService _notifications = new();

    public IReceiptImageFactory ImageFactory => _backend.ImageFactory;
    public ITypefaceProvider Typefaces => _backend.Typefaces;
    public IImageEncoder Encoder => _backend.Encoder;
    public string BackendName => _backend.Name;
    public IFileDialogService FileDialogs => _dialogs;
    public INotificationService Notifications => _notifications;
    public byte[] SampleTicket => Sample.Ticket;

    public Control CreateConnectionsView(ReceiptPrinter printer)
        => new BrowserConnectionsView { DataContext = new BrowserConnectionsViewModel(printer) };

    public bool SupportsMonitor => false;
    public void OpenMonitor() { }

    public void AttachRoot(Control mainView) => _dialogs.AttachControl(mainView);

    public Window CreateMainWindow(Control content)
        => throw new NotSupportedException("The browser head runs as a single view.");

    public void Shutdown() { }
}

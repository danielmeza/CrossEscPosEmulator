using System;
using System.Threading.Tasks;
using CrossEscPos;   // IPrinterResponder

namespace CrossEscPos.Transports.Browser;

/// <summary>
/// A browser transport that feeds a live ESC/POS session into the emulator and carries the printer's
/// status replies back — the browser analogue of the desktop <c>NetServer</c> / <c>SerialServer</c>.
/// The receive → <c>FeedEscPos</c> → respond logic is identical to the desktop transports (which is why
/// this is also an <see cref="IPrinterResponder"/>); only the underlying Web API differs, and that is
/// hidden behind <see cref="IJsTransportBridge"/> so this type is shared by every browser host.
/// </summary>
public interface IReceiptTransport : IPrinterResponder, IAsyncDisposable
{
    /// <summary>Human label — e.g. "Web Serial", "WebUSB".</summary>
    string Kind { get; }

    /// <summary>The connected device's description, or <c>null</c> when disconnected.</summary>
    string? Description { get; }

    bool IsConnected { get; }

    /// <summary>Raised when the connection state or description changes.</summary>
    event Action? StateChanged;

    /// <summary>True when the running browser exposes this API (feature detection).</summary>
    ValueTask<bool> IsSupportedAsync();

    /// <summary>Prompts the user to pick a device and opens it — must be called from a user gesture.</summary>
    Task ConnectAsync();

    Task DisconnectAsync();
}

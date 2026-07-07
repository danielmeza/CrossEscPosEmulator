using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CrossEscPos.App.Transports;

namespace CrossEscPos.App.Monitor;

/// <summary>
/// The transport the shared <see cref="MonitorViewModel"/> uses to reach the emulator/printer. Desktop
/// implements TCP / serial / USB over ESC-POS-.NET; the browser implements a single SignalR mode that
/// rides the host's proxy hub to the in-page emulator. The view model owns the test-job generation and the
/// status/indicator UI; the client only carries bytes and surfaces the status the emulator sends back.
/// </summary>
public interface IMonitorClient
{
    /// <summary>Selectable transport modes (e.g. TCP / Serial / USB, or a single "SignalR proxy").</summary>
    IReadOnlyList<string> Modes { get; }

    /// <summary>The current mode; setting it swaps <see cref="Fields"/> and raises <see cref="FieldsChanged"/>.</summary>
    string Mode { get; set; }

    /// <summary>Connection fields for the current mode (host/port, serial port + baud, proxy URL, …).</summary>
    IReadOnlyList<TransportField> Fields { get; }

    /// <summary>Raised when <see cref="Fields"/> changes (mode switch or a device/port refresh).</summary>
    event Action? FieldsChanged;

    /// <summary>Whether the current mode can re-enumerate devices (serial ports / USB).</summary>
    bool CanRefresh { get; }

    /// <summary>Re-enumerate devices for the current mode.</summary>
    Task RefreshAsync();

    /// <summary>Connects using the current mode + fields; returns a human-readable target. Throws on failure.</summary>
    Task<string> ConnectAsync();

    /// <summary>Sends an ESC/POS job to the printer.</summary>
    Task SendAsync(byte[] data);

    /// <summary>Closes the connection (idempotent).</summary>
    void Disconnect();

    /// <summary>Raised when the emulator reports status back (ASB / parsed).</summary>
    event Action<MonitorStatus>? StatusReceived;

    /// <summary>Raised for connection/transport log lines to append to the activity log.</summary>
    event Action<string>? Log;
}

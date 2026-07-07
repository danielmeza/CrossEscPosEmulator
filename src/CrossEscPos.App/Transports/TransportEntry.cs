using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CrossEscPos.App.Transports;

/// <summary>
/// A platform-agnostic, self-describing transport shown in the shared connections view: a name, a
/// status line, some configurable <see cref="TransportField"/>s, and a toggle action. The desktop
/// (TCP, serial) and browser (Web Serial, WebUSB, WebSocket/SignalR) transports all subclass this, so
/// one shared view renders them uniformly.
/// </summary>
public abstract partial class TransportEntry : ObservableObject
{
    protected TransportEntry(string name) => Name = name;

    public string Name { get; }

    [ObservableProperty] private string _status = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ButtonText))]
    private bool _isActive;

    /// <summary>Connect/Disconnect by default; Start/Stop or Open/Close for the desktop server transports.</summary>
    public virtual string ButtonText => IsActive ? "Disconnect" : "Connect";

    public IReadOnlyList<TransportField> Fields { get; protected set; } = Array.Empty<TransportField>();

    /// <summary>True to show a refresh button (e.g. re-enumerate serial ports).</summary>
    public virtual bool CanRefresh => false;

    [RelayCommand] private Task Toggle() => ToggleAsync();
    [RelayCommand] private Task Refresh() => RefreshAsync();

    protected abstract Task ToggleAsync();
    protected virtual Task RefreshAsync() => Task.CompletedTask;

    protected void Set(bool active, string status)
    {
        IsActive = active;
        Status = status;
    }

    /// <summary>Stop/disconnect on shutdown.</summary>
    public virtual void Shutdown() { }
}

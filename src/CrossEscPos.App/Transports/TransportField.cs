using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CrossEscPos.App.Transports;

/// <summary>
/// One configurable field of a transport (e.g. a TCP port, a serial baud rate, a WebSocket URL). The
/// shared connections view renders it as a text box, or a combo box when <see cref="Options"/> is set.
/// </summary>
public sealed partial class TransportField : ObservableObject
{
    public TransportField(string label, string value, IEnumerable<string>? options = null)
    {
        Label = label;
        _value = value;
        Options = options is null ? null : new ObservableCollection<string>(options);
    }

    public string Label { get; }

    [ObservableProperty]
    private string _value;

    /// <summary>Non-null for a dropdown field; mutable so it can be refreshed (e.g. serial ports).</summary>
    public ObservableCollection<string>? Options { get; }

    public bool IsDropdown => Options is not null;
}

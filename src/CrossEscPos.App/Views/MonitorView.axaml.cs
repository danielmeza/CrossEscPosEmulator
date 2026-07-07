using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CrossEscPos.App.Monitor;

namespace CrossEscPos.App.Views;

/// <summary>
/// The shared Monitor view — hosted in a window on desktop and as an in-page panel in the browser. It
/// binds a <see cref="MonitorViewModel"/> whose transport is platform-specific (TCP/serial/USB or SignalR).
/// </summary>
public partial class MonitorView : UserControl
{
    public MonitorView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private async void OnCopyLog(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MonitorViewModel vm)
            return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
            return;

        // The log is newest-first; copy oldest-first so it reads chronologically when pasted.
        var text = string.Join(Environment.NewLine, vm.Log.Reverse());
        try { await clipboard.SetValueAsync(DataFormat.Text, text); }
        catch { /* clipboard unavailable — ignore */ }
    }
}

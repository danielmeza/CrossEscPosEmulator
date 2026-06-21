using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CrossEscPos.App.Desktop.ViewModels;

namespace CrossEscPos.App.Desktop.Views;

public partial class MonitorWindow : Window
{
    public MonitorWindow()
    {
        InitializeComponent();
        Closed += (_, _) => (DataContext as MonitorWindowViewModel)?.Shutdown();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private async void OnCopyLog(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MonitorWindowViewModel vm || Clipboard is null)
            return;

        // The log is newest-first; copy oldest-first so it reads chronologically when pasted.
        var text = string.Join(Environment.NewLine, vm.Log.Reverse());
        try { await Clipboard.SetValueAsync(DataFormat.Text, text); }
        catch { /* clipboard unavailable — ignore */ }
    }
}

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CrossEscPos.App.Desktop.Views;

/// <summary>Desktop window that hosts the shared <see cref="CrossEscPos.App.Views.MonitorView"/>.</summary>
public partial class MonitorWindow : Window
{
    public MonitorWindow() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}

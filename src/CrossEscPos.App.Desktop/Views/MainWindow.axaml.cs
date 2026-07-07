using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CrossEscPos.App.Desktop.Views;

/// <summary>Thin desktop window shell; its content is the shared MainView.</summary>
public partial class MainWindow : Window
{
    public MainWindow() => AvaloniaXamlLoader.Load(this);
}

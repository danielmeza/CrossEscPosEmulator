using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CrossEscPos.App.Browser;

public partial class BrowserMainView : UserControl
{
    public BrowserMainView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}

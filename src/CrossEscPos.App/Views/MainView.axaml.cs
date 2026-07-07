using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CrossEscPos.App.ViewModels;
using CrossEscPos.Controls;

namespace CrossEscPos.App.Views;

/// <summary>The shared main view (used inside a desktop window and as the browser single view).</summary>
public partial class MainView : UserControl
{
    private ReceiptView? _receipts;

    public MainView()
    {
        InitializeComponent();
        _receipts = this.FindControl<ReceiptView>("Receipts");
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.ReceiptsUpdated += (_, _) => _receipts?.ScrollToEnd();
    }
}

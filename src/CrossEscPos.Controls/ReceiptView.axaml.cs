using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CrossEscPos.Controls;

/// <summary>
/// Displays the live stream of rendered receipts. Bind <see cref="Receipts"/> to an
/// <see cref="IEnumerable"/> of <see cref="ReceiptViewModel"/>. Backend-agnostic — the view models
/// carry already-rendered Avalonia bitmaps.
/// </summary>
public partial class ReceiptView : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ReceiptsProperty =
        AvaloniaProperty.Register<ReceiptView, IEnumerable?>(nameof(Receipts));

    public IEnumerable? Receipts
    {
        get => GetValue(ReceiptsProperty);
        set => SetValue(ReceiptsProperty, value);
    }

    public ReceiptView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>Scrolls the receipt list to the most recent page.</summary>
    public void ScrollToEnd()
        => this.FindControl<ScrollViewer>("ReceiptScrollView")?.ScrollToEnd();
}

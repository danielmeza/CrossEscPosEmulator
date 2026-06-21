using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CrossEscPos.Emulator;
using CrossEscPos.Emulator.Enums;

namespace CrossEscPos.Controls;

/// <summary>
/// A panel that drives the simulated <see cref="PrinterState"/> (online, cover, paper, error, cash
/// drawer, feed button). Two-way bound, so toggling it feeds the status commands (DLE EOT, GS r) and
/// Automatic Status Back (GS a). Bind the <see cref="State"/> property to a printer's State.
/// </summary>
public partial class PrinterStatePanel : UserControl
{
    public static readonly StyledProperty<PrinterState?> StateProperty =
        AvaloniaProperty.Register<PrinterStatePanel, PrinterState?>(nameof(State));

    public PrinterState? State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>Paper-level choices for the combo box.</summary>
    public Array PaperLevels { get; } = Enum.GetValues(typeof(PaperLevel));

    /// <summary>Error-state choices for the combo box.</summary>
    public Array ErrorStates { get; } = Enum.GetValues(typeof(PrinterErrorState));

    public PrinterStatePanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}

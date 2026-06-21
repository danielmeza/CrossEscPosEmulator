using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CrossEscPos.Emulator.Enums;

namespace CrossEscPos.Emulator;

/// <summary>
/// The simulated physical/logical state of the printer (online, cover, paper, cash-drawer sensor,
/// errors, feed button). Status commands report from this, and the UI panel drives it. Implemented
/// as an <see cref="ObservableObject"/> so the panel can two-way bind directly; <see cref="Changed"/>
/// is raised on any change to drive Automatic Status Back (ASB).
/// </summary>
public partial class PrinterState : ObservableObject
{
    [ObservableProperty] private bool _online = true;
    [ObservableProperty] private bool _coverOpen;
    [ObservableProperty] private PaperLevel _paper = PaperLevel.Adequate;
    [ObservableProperty] private bool _drawerOpen;          // cash-drawer kick sensor: true = open
    [ObservableProperty] private PrinterErrorState _error = PrinterErrorState.None;
    [ObservableProperty] private bool _feedButtonPressed;   // momentary

    /// <summary>Raised after any state property changes (used to push ASB status to the host).</summary>
    public event Action? Changed;

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        Changed?.Invoke();
    }
}

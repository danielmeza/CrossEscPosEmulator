using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CrossEscPos.Controls;
using CrossEscPos.Controls.Services;
using CrossEscPos.Emulator;
using CrossEscPos.Emulator.Rendering;
using CrossEscPos.Graphics;
using CrossEscPos.App.Monitor;

namespace CrossEscPos.App.ViewModels;

/// <summary>
/// The shared main view model, reused by both heads. It owns the platform-agnostic features — the
/// paste/upload ESC/POS input, the rendered receipts, printer-state simulation, export, and the
/// buzzer/cash-drawer toast — and hosts the platform's transport UI via <see cref="ConnectionsView"/>.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private const string DefaultInput =
        "*** CrossEscPos ***\n" +
        "Type or paste ESC/POS here\n" +
        "and press Render, or connect\n" +
        "a device on the left.\n" +
        "\n" +
        "Item            $ 4.50\n" +
        "Tax             $ 0.36\n" +
        "TOTAL           $ 4.86\n" +
        "\n" +
        "Thank you!\n";

    private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(3.5);

    private readonly ReceiptPrinter _printer;
    private readonly IPlatformServices _platform;
    private readonly IImageEncoder _encoder;
    private readonly IFileDialogService _dialogs;
    private readonly INotificationService _notifications;
    private readonly DispatcherTimer _toastTimer;
    private readonly Dictionary<string, ReceiptViewModel> _receiptsById = new();

    public ObservableCollection<ReceiptViewModel> Receipts { get; } = new();
    public bool HasReceipts => Receipts.Count > 0;
    public PrinterState State => _printer.State;

    /// <summary>The platform's transports, rendered uniformly by the shared connections view.</summary>
    public IReadOnlyList<Transports.TransportEntry> Transports { get; }

    /// <summary>The shared Monitor test-client, or null if the platform has no Monitor.</summary>
    public MonitorViewModel? Monitor { get; }
    public bool SupportsMonitor => Monitor is not null;
    public string BackendName => _platform.BackendName;

    [ObservableProperty] private string _input = DefaultInput;
    [ObservableProperty] private string _toastMessage = string.Empty;
    [ObservableProperty] private bool _toastVisible;
    /// <summary>Browser only: shows the Monitor as an in-page overlay (desktop uses a window instead).</summary>
    [ObservableProperty] private bool _isMonitorOpen;

    /// <summary>Raised on the UI thread after receipts change (the view scrolls to the newest).</summary>
    public event EventHandler? ReceiptsUpdated;

    public MainViewModel(ReceiptPrinter printer, IPlatformServices platform)
    {
        _printer = printer;
        _platform = platform;
        _encoder = platform.Encoder;
        _dialogs = platform.FileDialogs;
        _notifications = platform.Notifications;

        _toastTimer = new DispatcherTimer { Interval = ToastDuration };
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); ToastVisible = false; };

        // Transport receive threads raise these — marshal to the UI thread.
        _printer.OnActivityEvent += (_, _) => Dispatcher.UIThread.Post(() => { RefreshReceipts(); _notifications.NotifyActivity(); });
        _printer.OnBuzzer += () => Dispatcher.UIThread.Post(() => { _notifications.Beep(); ShowToast("🔔  Buzzer"); });
        _printer.OnCashDrawer += () => Dispatcher.UIThread.Post(() => { _notifications.OpenCashDrawer(); ShowToast("💵  Cash drawer opened"); });
        _printer.OnPrintBlocked += reason => Dispatcher.UIThread.Post(() => ShowToast($"🚫  {reason} — print dropped"));

        Transports = platform.CreateTransports(printer);

        if (platform.CreateMonitorClient() is { } monitorClient)
            Monitor = new MonitorViewModel(monitorClient);

        Render(); // render the default input so the app shows something on launch
    }

    [RelayCommand]
    private void Render()
    {
        ResetPrinter();
        _printer.FeedEscPos(Encoding.Latin1.GetBytes(Input));
        RefreshReceipts();
    }

    [RelayCommand]
    private void LoadSample()
    {
        var sample = _platform.SampleTicket;
        if (sample.Length == 0)
            return;
        ResetPrinter();
        _printer.FeedEscPos(sample);
        RefreshReceipts();
    }

    [RelayCommand]
    private void Clear()
    {
        ResetPrinter();
        RefreshReceipts();
    }

    [RelayCommand]
    private void OpenMonitor()
    {
        if (Monitor is null)
            return;
        if (_platform.MonitorInWindow)
            _platform.ShowMonitorWindow(Monitor);
        else
            IsMonitorOpen = true; // in-page overlay (browser)
    }

    [RelayCommand]
    private void CloseMonitor() => IsMonitorOpen = false;

    private void ResetPrinter()
    {
        _printer.ReceiptStack.Clear();
        _printer.Initialize();
        _printer.StartNewReceipt();
        _receiptsById.Clear();
        Receipts.Clear();
    }

    #region Export

    private List<Receipt> NonEmptyReceipts() => _printer.ReceiptStack.Where(r => !r.IsEmpty).ToList();

    [RelayCommand(CanExecute = nameof(HasReceipts))]
    private async Task ExportAll()
    {
        var receipts = NonEmptyReceipts();
        if (receipts.Count == 0)
            return;

        var bitmaps = receipts.Select(r => r.Render()).ToList();
        try
        {
            using var combined = ReceiptExporter.StackVertical(bitmaps, _printer.ImageFactory);
            var stream = await _dialogs.SavePngAsync("receipts");
            if (stream is null)
                return;
            await using (stream)
                _encoder.EncodePng(combined, stream);
        }
        finally
        {
            foreach (var b in bitmaps)
                b.Dispose();
        }
    }

    [RelayCommand(CanExecute = nameof(HasReceipts))]
    private async Task ExportEach()
    {
        var receipts = NonEmptyReceipts();
        if (receipts.Count == 0)
            return;

        var folder = await _dialogs.PickFolderAsync();
        if (folder is null)
            return;

        for (int i = 0; i < receipts.Count; i++)
        {
            using var bmp = receipts[i].Render();
            var path = Path.Combine(folder, $"receipt_{i + 1:D3}.png");
            await using var fs = File.Create(path);
            _encoder.EncodePng(bmp, fs);
        }
    }

    #endregion

    /// <summary>Called on shutdown — stop every transport.</summary>
    public void Shutdown()
    {
        Monitor?.Shutdown();
        foreach (var transport in Transports)
            transport.Shutdown();
    }

    private void ShowToast(string message)
    {
        ToastMessage = message;
        ToastVisible = true;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void RefreshReceipts()
    {
        foreach (var receipt in _printer.ReceiptStack)
        {
            if (receipt.IsEmpty)
                continue;

            if (_receiptsById.TryGetValue(receipt.Guid, out var vm))
            {
                vm.Refresh();
            }
            else
            {
                vm = new ReceiptViewModel(receipt, _encoder, _dialogs, Receipts.Count + 1);
                _receiptsById[receipt.Guid] = vm;
                Receipts.Add(vm);
            }
        }

        OnPropertyChanged(nameof(HasReceipts));
        ExportAllCommand.NotifyCanExecuteChanged();
        ExportEachCommand.NotifyCanExecuteChanged();
        ReceiptsUpdated?.Invoke(this, EventArgs.Empty);
    }
}

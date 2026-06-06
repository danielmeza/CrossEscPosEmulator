using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceiptPrinterEmulator.Emulator;
using ReceiptPrinterEmulator.Emulator.Rendering;
using ReceiptPrinterEmulator.Logging;
using ReceiptPrinterEmulator.Networking;
using ReceiptPrinterEmulator.Services;
using ReceiptPrinterEmulator.Utils;

namespace ReceiptPrinterEmulator.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    // Resolve relative to the app directory, not the process working directory: a Finder/Explorer-
    // launched app has its CWD set to "/" (or elsewhere), so a bare relative path is not found.
    private static readonly string TestReceiptPath =
        Path.Combine(AppContext.BaseDirectory, "test_receipt.txt");

    private readonly ReceiptPrinter _printer;
    private readonly NetServer _tcp;
    private readonly SerialServer _serial;
    private readonly INotificationService _notifications;
    private readonly IFileDialogService _dialogs;

    private readonly Dictionary<string, ReceiptViewModel> _receiptsById = new();

    // --- TCP connection settings ---
    public ObservableCollection<string> AvailableAddresses { get; } = new();

    [ObservableProperty]
    private string _selectedAddress = "0.0.0.0";

    [ObservableProperty]
    private string _tcpPortText = "9100";

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private IBrush _statusBrush = Brushes.Gray;

    public bool IsTcpListening => _tcp.IsRunning;
    public string TcpButtonText => _tcp.IsRunning ? "Stop" : "Start";

    // --- Serial connection settings ---
    public ObservableCollection<string> AvailablePorts { get; } = new();

    [ObservableProperty]
    private string? _selectedSerialPort;

    [ObservableProperty]
    private string _baudText = "9600";

    [ObservableProperty]
    private string _serialStatusText = "Serial: closed";

    public bool IsSerialOpen => _serial.IsRunning;
    public string SerialButtonText => _serial.IsRunning ? "Close" : "Open";

    public ObservableCollection<ReceiptViewModel> Receipts { get; } = new();

    public bool HasReceipts => Receipts.Count > 0;

    // --- Printer state simulation (right-side panel) ---
    public PrinterState State => _printer.State;
    public Array PaperLevels { get; } = Enum.GetValues(typeof(Emulator.Enums.PaperLevel));
    public Array ErrorStates { get; } = Enum.GetValues(typeof(Emulator.Enums.PrinterErrorState));

    // Transient on-screen toast for buzzer / cash-drawer events (always-visible feedback).
    [ObservableProperty]
    private string _toastMessage = string.Empty;

    [ObservableProperty]
    private bool _toastVisible;

    /// <summary>Raised (on the UI thread) after receipts change, so the view can scroll to bottom.</summary>
    public event EventHandler? ReceiptsUpdated;

    /// <summary>Raised when the user asks to open the Monitor window (handled by the view).</summary>
    public event EventHandler? OpenMonitorRequested;

    /// <summary>The TCP port the monitor should connect to (the current listener port).</summary>
    public int CurrentTcpPort => int.TryParse(TcpPortText, out var p) ? p : 9100;

    [RelayCommand]
    private void OpenMonitor() => OpenMonitorRequested?.Invoke(this, EventArgs.Empty);

    public MainWindowViewModel(ReceiptPrinter printer, INotificationService notifications,
        IFileDialogService dialogs, string listenAddress = "0.0.0.0", int tcpPort = 9100,
        bool tcpEnabled = true, string? serialPort = null, int serialBaud = 9600)
    {
        _printer = printer;
        _notifications = notifications;
        _dialogs = dialogs;
        _tcp = new NetServer(printer);
        _serial = new SerialServer(printer);

        // Events fire from a transport receive thread — marshal everything to the UI thread.
        _printer.OnActivityEvent += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            RefreshReceipts();
            _notifications.NotifyActivity();
        });
        _printer.OnBuzzer += () => Dispatcher.UIThread.Post(() =>
        {
            _notifications.Beep();
            ShowToast("🔔  Buzzer");
        });
        _printer.OnCashDrawer += () => Dispatcher.UIThread.Post(() =>
        {
            _notifications.OpenCashDrawer();
            ShowToast("💵  Cash drawer opened");
        });
        _printer.OnPrintBlocked += reason => Dispatcher.UIThread.Post(() => ShowToast($"🚫  {reason} — print dropped"));

        PopulateAddresses(listenAddress);
        RefreshSerialPorts();

        SelectedAddress = AvailableAddresses.Contains(listenAddress) ? listenAddress : AvailableAddresses[0];
        TcpPortText = tcpPort.ToString();
        BaudText = serialBaud.ToString();
        if (serialPort is not null && AvailablePorts.Contains(serialPort))
            SelectedSerialPort = serialPort;
        else
            SelectedSerialPort = AvailablePorts.FirstOrDefault();

        if (tcpEnabled)
            StartTcp();
        if (serialPort is not null)
            StartSerial();

        RefreshReceipts();
        UpdateStatus();
    }

    #region Connection commands

    [RelayCommand]
    private void ToggleTcp()
    {
        if (_tcp.IsRunning)
            _tcp.Stop();
        else
            StartTcp();

        UpdateStatus();
    }

    private void StartTcp()
    {
        if (!IPAddress.TryParse(SelectedAddress, out var address))
        {
            StatusText = $"TCP: invalid address '{SelectedAddress}'";
            StatusBrush = Brushes.Crimson;
            return;
        }

        if (!int.TryParse(TcpPortText, out var port) || port is < 1 or > 65535)
        {
            StatusText = $"TCP: invalid port '{TcpPortText}'";
            StatusBrush = Brushes.Crimson;
            return;
        }

        _tcp.Start(address, port);
    }

    [RelayCommand]
    private void ToggleSerial()
    {
        if (_serial.IsRunning)
        {
            _serial.Stop();
        }
        else
        {
            StartSerial();
        }

        UpdateStatus();
    }

    private void StartSerial()
    {
        if (string.IsNullOrWhiteSpace(SelectedSerialPort))
        {
            SerialStatusText = "Serial: no port selected";
            return;
        }

        int baud = int.TryParse(BaudText, out var b) && b > 0 ? b : 9600;
        _serial.Start(SelectedSerialPort, baud);
    }

    [RelayCommand]
    private void RefreshSerialPorts()
    {
        var current = SelectedSerialPort;
        AvailablePorts.Clear();
        try
        {
            foreach (var name in SerialPort.GetPortNames().OrderBy(n => n))
                AvailablePorts.Add(name);
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "Failed to enumerate serial ports");
        }

        if (current is not null && AvailablePorts.Contains(current))
            SelectedSerialPort = current;
        else
            SelectedSerialPort = AvailablePorts.FirstOrDefault();
    }

    private void PopulateAddresses(string preferred)
    {
        AvailableAddresses.Clear();
        AvailableAddresses.Add("0.0.0.0");     // all interfaces
        AvailableAddresses.Add("127.0.0.1");   // localhost only

        try
        {
            foreach (var ip in Dns.GetHostAddresses(Dns.GetHostName()))
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    var s = ip.ToString();
                    if (!AvailableAddresses.Contains(s))
                        AvailableAddresses.Add(s);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "Failed to enumerate local addresses");
        }

        if (!string.IsNullOrWhiteSpace(preferred) && !AvailableAddresses.Contains(preferred))
            AvailableAddresses.Add(preferred);
    }

    #endregion

    #region Printer commands

    [RelayCommand]
    private void Reset()
    {
        Logger.Info("Resetting");

        _printer.ReceiptStack.Clear();
        _printer.Initialize();
        _printer.StartNewReceipt();

        _receiptsById.Clear();
        Receipts.Clear();

        RefreshReceipts();
    }

    [RelayCommand]
    private void TestPrint()
    {
        if (!File.Exists(TestReceiptPath))
            return;

        // Read as raw bytes (the file contains binary ESC/POS incl. barcode/QR), decoded the same
        // way as the network/serial path so byte values >127 survive.
        var bytes = File.ReadAllBytes(TestReceiptPath);
        _printer.FeedEscPos(Encoding.Latin1.GetString(bytes));
    }

    [RelayCommand]
    private void HexDump()
    {
        if (!File.Exists(TestReceiptPath))
        {
            Logger.Info("Hex dump: file not found.");
            return;
        }

        byte[] data = File.ReadAllBytes(TestReceiptPath);
        var sb = new StringBuilder();
        for (int i = 0; i < data.Length; i++)
        {
            sb.AppendFormat("{0:X2} ", data[i]);
            if ((i + 1) % 16 == 0)
                sb.AppendLine();
        }
        Console.WriteLine(sb.ToString());
    }

    #endregion

    #region Export commands

    private List<Receipt> NonEmptyReceipts() =>
        _printer.ReceiptStack.Where(r => !r.IsEmpty).ToList();

    [RelayCommand(CanExecute = nameof(HasReceipts))]
    private async Task ExportAll()
    {
        var receipts = NonEmptyReceipts();
        if (receipts.Count == 0)
            return;

        var bitmaps = receipts.Select(r => r.Render()).ToList();
        try
        {
            using var combined = ReceiptExporter.StackVertical(bitmaps);
            var stream = await _dialogs.SavePngAsync("receipts");
            if (stream is null)
                return;

            await using (stream)
                combined.SavePng(stream);

            Logger.Info($"Exported {receipts.Count} receipt(s) to a single image");
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
            bmp.SavePng(fs);
        }

        Logger.Info($"Exported {receipts.Count} receipt(s) to {folder}");
    }

    #endregion

    /// <summary>Stops both transports — call on application shutdown.</summary>
    public void Shutdown()
    {
        _tcp.Stop();
        _serial.Stop();
    }

    private void ShowToast(string message)
    {
        ToastMessage = message;
        ToastVisible = true;
        // Auto-hide after a moment (must run on the UI thread; callers already are).
        DispatcherTimer.RunOnce(() => ToastVisible = false, TimeSpan.FromMilliseconds(1800));
    }

    private void UpdateStatus()
    {
        if (_tcp.IsRunning)
        {
            StatusText = $"TCP listening on {_tcp.EndPoint}";
            StatusBrush = Brushes.SpringGreen;
        }
        else
        {
            StatusText = "TCP stopped";
            StatusBrush = Brushes.Gray;
        }

        SerialStatusText = _serial.IsRunning
            ? $"Serial {_serial.PortName} @ {_serial.BaudRate} (open)"
            : "Serial: closed";

        OnPropertyChanged(nameof(IsTcpListening));
        OnPropertyChanged(nameof(TcpButtonText));
        OnPropertyChanged(nameof(IsSerialOpen));
        OnPropertyChanged(nameof(SerialButtonText));
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
                vm = new ReceiptViewModel(receipt, _dialogs, Receipts.Count + 1);
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

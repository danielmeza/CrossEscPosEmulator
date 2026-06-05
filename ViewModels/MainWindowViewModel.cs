using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceiptPrinterEmulator.Emulator;
using ReceiptPrinterEmulator.Logging;
using ReceiptPrinterEmulator.Networking;
using ReceiptPrinterEmulator.Services;

namespace ReceiptPrinterEmulator.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private const string TestReceiptPath = "test_receipt.txt";

    private readonly ReceiptPrinter _printer;
    private readonly NetServer? _server;
    private readonly SerialServer? _serial;
    private readonly INotificationService _notifications;

    private readonly Dictionary<string, ReceiptViewModel> _receiptsById = new();

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private IBrush _statusBrush = Brushes.Crimson;

    [ObservableProperty]
    private string _serialStatusText = string.Empty;

    public ObservableCollection<ReceiptViewModel> Receipts { get; } = new();

    /// <summary>Raised (on the UI thread) after receipts change, so the view can scroll to bottom.</summary>
    public event EventHandler? ReceiptsUpdated;

    public MainWindowViewModel(ReceiptPrinter printer, INotificationService notifications,
        NetServer? server = null, SerialServer? serial = null)
    {
        _printer = printer;
        _server = server;
        _serial = serial;
        _notifications = notifications;

        // These events fire from a transport receive thread — marshal everything to the UI thread.
        _printer.OnActivityEvent += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            RefreshReceipts();
            _notifications.NotifyActivity();
        });
        _printer.OnBuzzer += () => Dispatcher.UIThread.Post(() => _notifications.Beep());
        _printer.OnCashDrawer += () => Dispatcher.UIThread.Post(() => _notifications.OpenCashDrawer());

        RefreshReceipts();
    }

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

    private void RefreshReceipts()
    {
        if (_server is not null)
        {
            StatusText = $"TCP {_server.EndPoint}";
            StatusBrush = _server.IsRunning ? Brushes.SpringGreen : Brushes.Crimson;
        }
        else
        {
            StatusText = "TCP off";
            StatusBrush = Brushes.Gray;
        }

        if (_serial is not null)
            SerialStatusText = $"Serial {_serial.PortName} @ {_serial.BaudRate} " +
                               (_serial.IsRunning ? "(open)" : "(closed)");

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
                vm = new ReceiptViewModel(receipt);
                _receiptsById[receipt.Guid] = vm;
                Receipts.Add(vm);
            }
        }

        ReceiptsUpdated?.Invoke(this, EventArgs.Empty);
    }
}

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
    private readonly NetServer _server;
    private readonly INotificationService _notifications;

    private readonly Dictionary<string, ReceiptViewModel> _receiptsById = new();

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private IBrush _statusBrush = Brushes.Crimson;

    public ObservableCollection<ReceiptViewModel> Receipts { get; } = new();

    /// <summary>Raised (on the UI thread) after receipts change, so the view can scroll to bottom.</summary>
    public event EventHandler? ReceiptsUpdated;

    public MainWindowViewModel(ReceiptPrinter printer, NetServer server, INotificationService notifications)
    {
        _printer = printer;
        _server = server;
        _notifications = notifications;

        // OnActivityEvent fires from the TCP receive thread — marshal everything to the UI thread.
        _printer.OnActivityEvent += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            RefreshReceipts();
            _notifications.NotifyActivity();
        });

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

        _printer.FeedEscPos(File.ReadAllText(TestReceiptPath, Encoding.ASCII));
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
        StatusText = _server.EndPoint.ToString();
        StatusBrush = _server.IsRunning ? Brushes.SpringGreen : Brushes.Crimson;

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

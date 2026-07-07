using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CrossEscPos.Emulator;
using CrossEscPos.Logging;
using CrossEscPos.Transports;

namespace CrossEscPos.App.Desktop.ViewModels;

/// <summary>
/// The desktop transports panel: a TCP/IP listener (port 9100 by convention) and a serial reader, both
/// feeding the shared printer. This is the desktop's contribution to the shared app's connections slot;
/// the browser head supplies Web Serial / WebUSB / WebSocket instead.
/// </summary>
public partial class ConnectionsViewModel : ObservableObject
{
    private readonly NetServer _tcp;
    private readonly SerialServer _serial;

    public ObservableCollection<string> AvailableAddresses { get; } = new();
    public ObservableCollection<string> AvailablePorts { get; } = new();

    [ObservableProperty] private string _selectedAddress = "0.0.0.0";
    [ObservableProperty] private string _tcpPortText = "9100";
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private IBrush _statusBrush = Brushes.Gray;
    [ObservableProperty] private string? _selectedSerialPort;
    [ObservableProperty] private string _baudText = "9600";
    [ObservableProperty] private string _serialStatusText = "Serial: closed";

    public bool IsTcpListening => _tcp.IsRunning;
    public string TcpButtonText => _tcp.IsRunning ? "Stop" : "Start";
    public bool IsSerialOpen => _serial.IsRunning;
    public string SerialButtonText => _serial.IsRunning ? "Close" : "Open";

    public int CurrentTcpPort => int.TryParse(TcpPortText, out var p) ? p : 9100;

    public ConnectionsViewModel(ReceiptPrinter printer, string listenAddress, int tcpPort,
        bool tcpEnabled, string? serialPort, int serialBaud)
    {
        _tcp = new NetServer(printer);
        _serial = new SerialServer(printer);

        PopulateAddresses(listenAddress);
        RefreshSerialPorts();

        SelectedAddress = AvailableAddresses.Contains(listenAddress) ? listenAddress : AvailableAddresses[0];
        TcpPortText = tcpPort.ToString();
        BaudText = serialBaud.ToString();
        SelectedSerialPort = serialPort is not null && AvailablePorts.Contains(serialPort)
            ? serialPort : AvailablePorts.FirstOrDefault();

        if (tcpEnabled)
            StartTcp();
        if (serialPort is not null)
            StartSerial();

        UpdateStatus();
    }

    [RelayCommand]
    private void ToggleTcp()
    {
        if (_tcp.IsRunning) _tcp.Stop();
        else StartTcp();
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
        if (_serial.IsRunning) _serial.Stop();
        else StartSerial();
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
        SelectedSerialPort = current is not null && AvailablePorts.Contains(current)
            ? current : AvailablePorts.FirstOrDefault();
    }

    private void PopulateAddresses(string preferred)
    {
        AvailableAddresses.Clear();
        AvailableAddresses.Add("0.0.0.0");
        AvailableAddresses.Add("127.0.0.1");
        try
        {
            foreach (var ip in Dns.GetHostAddresses(Dns.GetHostName()))
                if (ip.AddressFamily == AddressFamily.InterNetwork && !AvailableAddresses.Contains(ip.ToString()))
                    AvailableAddresses.Add(ip.ToString());
        }
        catch (Exception ex)
        {
            Logger.Exception(ex, "Failed to enumerate local addresses");
        }
        if (!string.IsNullOrWhiteSpace(preferred) && !AvailableAddresses.Contains(preferred))
            AvailableAddresses.Add(preferred);
    }

    private void UpdateStatus()
    {
        StatusText = _tcp.IsRunning ? $"TCP listening on {_tcp.EndPoint}" : "TCP stopped";
        StatusBrush = _tcp.IsRunning ? Brushes.SpringGreen : Brushes.Gray;
        SerialStatusText = _serial.IsRunning
            ? $"Serial {_serial.PortName} @ {_serial.BaudRate} (open)"
            : "Serial: closed";
        OnPropertyChanged(nameof(IsTcpListening));
        OnPropertyChanged(nameof(TcpButtonText));
        OnPropertyChanged(nameof(IsSerialOpen));
        OnPropertyChanged(nameof(SerialButtonText));
    }

    public void Shutdown()
    {
        _tcp.Stop();
        _serial.Stop();
    }
}

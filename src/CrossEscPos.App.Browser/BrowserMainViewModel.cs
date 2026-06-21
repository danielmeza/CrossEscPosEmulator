using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CrossEscPos.Graphics;
using CrossEscPos.Controls;
using CrossEscPos.Controls.Services;
using CrossEscPos.Emulator;

namespace CrossEscPos.App.Browser;

/// <summary>
/// Drives the browser demo: feed ESC/POS (typed text or the embedded sample ticket) into the headless
/// emulator and show the rendered receipts. No transports — the browser has no sockets/serial/USB.
/// </summary>
public partial class BrowserMainViewModel : ObservableObject
{
    private const string DefaultText =
        "CrossEscPos — WASM demo\n" +
        "------------------------\n" +
        "This receipt was rendered\n" +
        "in your browser with\n" +
        "SkiaSharp + Avalonia.\n" +
        "\n" +
        "Edit this text and press\n" +
        "Render, or load the sample\n" +
        "ticket for barcodes & QR.\n";

    private readonly ReceiptPrinter _printer;
    private readonly IImageEncoder _encoder;
    private readonly IFileDialogService _dialogs = new NullFileDialogService();

    public ObservableCollection<ReceiptViewModel> Receipts { get; } = new();

    public PrinterState State => _printer.State;

    [ObservableProperty]
    private string _input = DefaultText;

    public BrowserMainViewModel(ReceiptPrinter printer, IImageEncoder encoder)
    {
        _printer = printer;
        _encoder = encoder;
        Render();
    }

    [RelayCommand]
    private void Render()
    {
        ResetPrinter();
        _printer.FeedEscPos(Input);
        Refresh();
    }

    [RelayCommand]
    private void RenderSample()
    {
        ResetPrinter();
        _printer.FeedEscPos(LoadSample());
        Refresh();
    }

    [RelayCommand]
    private void Clear()
    {
        ResetPrinter();
        Receipts.Clear();
    }

    private void ResetPrinter()
    {
        _printer.ReceiptStack.Clear();
        _printer.Initialize();
        _printer.StartNewReceipt();
    }

    private void Refresh()
    {
        Receipts.Clear();
        int index = 1;
        foreach (var receipt in _printer.ReceiptStack)
        {
            if (receipt.IsEmpty)
                continue;
            Receipts.Add(new ReceiptViewModel(receipt, _encoder, _dialogs, index++));
        }
    }

    private static string LoadSample()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("sample.escpos");
        if (stream is null)
            return string.Empty;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        // The interpreter consumes one char per byte (Latin1), matching the transports.
        return Encoding.Latin1.GetString(buffer.ToArray());
    }
}

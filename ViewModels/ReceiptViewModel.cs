using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using ReceiptPrinterEmulator.Emulator;
using ReceiptPrinterEmulator.Utils;

namespace ReceiptPrinterEmulator.ViewModels;

/// <summary>
/// Wraps a single <see cref="Receipt"/> and exposes its rendered image for binding. Replaces the
/// old code-behind that manually created/updated WPF Image controls by GUID.
/// </summary>
public partial class ReceiptViewModel : ObservableObject
{
    private readonly Receipt _receipt;

    [ObservableProperty]
    private Bitmap? _image;

    public ReceiptViewModel(Receipt receipt)
    {
        _receipt = receipt;
        Refresh();
    }

    public string Id => _receipt.Guid;

    public bool IsEmpty => _receipt.IsEmpty;

    /// <summary>Re-renders the receipt bitmap from its current state.</summary>
    public void Refresh()
    {
        if (_receipt.IsEmpty)
            return;

        var old = Image;
        using var skBitmap = _receipt.Render();
        Image = skBitmap.ToAvaloniaBitmap();
        old?.Dispose();
    }
}

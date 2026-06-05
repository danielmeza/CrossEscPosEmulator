using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReceiptPrinterEmulator.Emulator;
using ReceiptPrinterEmulator.Services;
using ReceiptPrinterEmulator.Utils;

namespace ReceiptPrinterEmulator.ViewModels;

/// <summary>
/// Wraps a single <see cref="Receipt"/> and exposes its rendered image for binding, plus a per-page
/// export command. Replaces the old code-behind that manually created/updated WPF Image controls.
/// </summary>
public partial class ReceiptViewModel : ObservableObject
{
    private readonly Receipt _receipt;
    private readonly IFileDialogService _dialogs;
    private readonly int _index;

    [ObservableProperty]
    private Bitmap? _image;

    public ReceiptViewModel(Receipt receipt, IFileDialogService dialogs, int index)
    {
        _receipt = receipt;
        _dialogs = dialogs;
        _index = index;
        Refresh();
    }

    public string Id => _receipt.Guid;

    public string Title => $"Cut #{_index}";

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

    [RelayCommand]
    private async Task Export()
    {
        using var bmp = _receipt.Render();
        var stream = await _dialogs.SavePngAsync($"receipt_{_index:D3}");
        if (stream is null)
            return;

        await using (stream)
            bmp.SavePng(stream);
    }
}

using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CrossEscPos.Graphics;
using CrossEscPos.Controls.Services;
using CrossEscPos.Emulator;

namespace CrossEscPos.Controls;

/// <summary>
/// Wraps a single <see cref="Receipt"/> and exposes its rendered image for binding, plus a per-page
/// PNG export command. The render backend reaches the control only through <see cref="IImageEncoder"/>.
/// </summary>
public partial class ReceiptViewModel : ObservableObject
{
    private readonly Receipt _receipt;
    private readonly IImageEncoder _encoder;
    private readonly IFileDialogService _dialogs;
    private readonly int _index;

    [ObservableProperty]
    private Bitmap? _image;

    public ReceiptViewModel(Receipt receipt, IImageEncoder encoder, IFileDialogService dialogs, int index)
    {
        _receipt = receipt;
        _encoder = encoder;
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
        using var image = _receipt.Render();
        Image = image.ToAvaloniaBitmap(_encoder);
        old?.Dispose();
    }

    [RelayCommand]
    private async Task Export()
    {
        using var image = _receipt.Render();
        var stream = await _dialogs.SavePngAsync($"receipt_{_index:D3}");
        if (stream is null)
            return;

        await using (stream)
            _encoder.EncodePng(image, stream);
    }
}

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace ReceiptPrinterEmulator.Services;

/// <summary>
/// <see cref="IFileDialogService"/> backed by Avalonia's <see cref="IStorageProvider"/>.
/// </summary>
public class FileDialogService : IFileDialogService
{
    private TopLevel? _topLevel;

    public void AttachTopLevel(TopLevel topLevel) => _topLevel = topLevel;

    public async Task<Stream?> SavePngAsync(string suggestedName)
    {
        var provider = _topLevel?.StorageProvider;
        if (provider is null)
            return null;

        var file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export receipt",
            SuggestedFileName = suggestedName,
            DefaultExtension = "png",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("PNG image") { Patterns = new[] { "*.png" } }
            }
        });

        if (file is null)
            return null;

        return await file.OpenWriteAsync();
    }

    public async Task<string?> PickFolderAsync()
    {
        var provider = _topLevel?.StorageProvider;
        if (provider is null)
            return null;

        var folders = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose export folder",
            AllowMultiple = false
        });

        if (folders is null || folders.Count == 0)
            return null;

        return folders[0].TryGetLocalPath();
    }
}

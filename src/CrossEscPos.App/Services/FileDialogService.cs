using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace CrossEscPos.Controls.Services;

/// <summary>
/// <see cref="IFileDialogService"/> backed by Avalonia's <see cref="IStorageProvider"/> — cross-platform,
/// so on the browser head the same "save PNG" path becomes a download. The top level is resolved either
/// from an explicit window (desktop) or lazily from a control (browser single-view).
/// </summary>
public class FileDialogService : IFileDialogService
{
    private TopLevel? _topLevel;
    private Control? _control;

    public void AttachTopLevel(TopLevel topLevel) => _topLevel = topLevel;

    /// <summary>Attach a control; the top level is resolved from it at call time (browser single-view).</summary>
    public void AttachControl(Control control) => _control = control;

    private IStorageProvider? Provider =>
        (_topLevel ?? (_control is null ? null : TopLevel.GetTopLevel(_control)))?.StorageProvider;

    public async Task<Stream?> SavePngAsync(string suggestedName)
    {
        var provider = Provider;
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
        var provider = Provider;
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

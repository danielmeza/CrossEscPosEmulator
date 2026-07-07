using System;
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using CrossEscPos.Controls.Services;

namespace CrossEscPos.App.Browser;

/// <summary>
/// Browser "save PNG" = a real download. Avalonia's browser storage provider goes through the File System
/// Access API (Chromium-only, and its writable stream rejects the encoder's synchronous writes), so exports
/// silently failed. Here <see cref="SavePngAsync"/> hands back a memory stream that, when disposed, blobs
/// its bytes and downloads them via a JS anchor — works in every browser. Folder export isn't offered.
/// </summary>
public sealed class BrowserFileDialogService : IFileDialogService
{
    public Task<Stream?> SavePngAsync(string suggestedName)
    {
        var name = suggestedName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            ? suggestedName
            : suggestedName + ".png";
        return Task.FromResult<Stream?>(new DownloadStream(name));
    }

    // No folder picker in the browser; "export each cut to a folder" is a desktop feature.
    public Task<string?> PickFolderAsync() => Task.FromResult<string?>(null);
}

/// <summary>A <see cref="MemoryStream"/> that downloads its bytes as a file when disposed.</summary>
internal sealed partial class DownloadStream : MemoryStream
{
    private readonly string _name;
    private bool _saved;

    public DownloadStream(string name) => _name = name;

    protected override void Dispose(bool disposing)
    {
        // Stream.DisposeAsync() (used by `await using`) calls Dispose(), so this covers both paths.
        if (disposing && !_saved && Length > 0)
        {
            _saved = true;
            try { DownloadFile(_name, Convert.ToBase64String(GetBuffer(), 0, (int)Length)); }
            catch { /* download unavailable */ }
        }
        base.Dispose(disposing);
    }

    [JSImport("globalThis.crossescpos.downloadFile")]
    private static partial void DownloadFile(string name, string base64);
}

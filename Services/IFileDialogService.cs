using System.IO;
using System.Threading.Tasks;

namespace ReceiptPrinterEmulator.Services;

/// <summary>
/// Abstraction over native save/folder pickers so view models can export without referencing the UI.
/// </summary>
public interface IFileDialogService
{
    /// <summary>
    /// Shows a "save PNG" dialog. Returns a writable stream for the chosen file (caller disposes),
    /// or null if cancelled.
    /// </summary>
    Task<Stream?> SavePngAsync(string suggestedName);

    /// <summary>Shows a folder picker. Returns the chosen folder's local path, or null if cancelled.</summary>
    Task<string?> PickFolderAsync();
}

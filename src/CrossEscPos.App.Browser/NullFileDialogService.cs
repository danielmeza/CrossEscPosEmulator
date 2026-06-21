using System.IO;
using System.Threading.Tasks;
using CrossEscPos.Controls.Services;

namespace CrossEscPos.App.Browser;

/// <summary>
/// No-op file dialogs for the browser demo (native save/folder pickers aren't wired up here, so the
/// per-page export command simply does nothing).
/// </summary>
internal sealed class NullFileDialogService : IFileDialogService
{
    public Task<Stream?> SavePngAsync(string suggestedName) => Task.FromResult<Stream?>(null);

    public Task<string?> PickFolderAsync() => Task.FromResult<string?>(null);
}

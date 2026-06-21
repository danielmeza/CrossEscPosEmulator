using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CrossEscPos.Controls.Services;
using CrossEscPos.Emulator;
using CrossEscPos.Rendering.Skia;
using Xunit;

namespace CrossEscPos.Controls.Tests;

public class ControlsTests
{
    [AvaloniaFact]
    public void PrinterStatePanel_ToggleUpdatesBoundState()
    {
        var state = new PrinterState { Online = true };
        var panel = new PrinterStatePanel { State = state };
        var window = new Window { Content = panel };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // The first ToggleSwitch in the panel is the Online switch, two-way bound to State.Online.
        var online = panel.GetVisualDescendants().OfType<ToggleSwitch>().First();
        online.IsChecked = false;
        Dispatcher.UIThread.RunJobs();

        Assert.False(state.Online);
    }

    [AvaloniaFact]
    public void ReceiptView_RendersReceiptImages()
    {
        var printer = new ReceiptPrinter(PaperConfiguration.Default, new SkiaImageFactory(), new SkiaTypefaceProvider());
        printer.FeedEscPos("Hello control\n");

        var encoder = new SkiaImageEncoder();
        var dialogs = new StubFileDialogService();
        var receipts = new ObservableCollection<ReceiptViewModel>(
            printer.ReceiptStack.Where(r => !r.IsEmpty)
                .Select((r, i) => new ReceiptViewModel(r, encoder, dialogs, i + 1)));

        var view = new ReceiptView { Receipts = receipts };
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.NotEmpty(view.GetVisualDescendants().OfType<Image>());
    }

    private sealed class StubFileDialogService : IFileDialogService
    {
        public Task<Stream?> SavePngAsync(string suggestedName) => Task.FromResult<Stream?>(null);
        public Task<string?> PickFolderAsync() => Task.FromResult<string?>(null);
    }
}

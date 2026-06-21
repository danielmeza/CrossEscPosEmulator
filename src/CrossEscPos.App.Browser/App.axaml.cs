using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CrossEscPos.Emulator;
using CrossEscPos.Rendering.Skia;

namespace CrossEscPos.App.Browser;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            // Composition root for the browser: the same Core + Skia backend the desktop uses, but no
            // transports — the browser sandbox has no sockets/serial/USB, so input is fed in-page.
            var imageFactory = new SkiaImageFactory();
            var typefaces = new SkiaTypefaceProvider();
            var encoder = new SkiaImageEncoder();

            var printer = new ReceiptPrinter(PaperConfiguration.Default, imageFactory, typefaces);

            singleView.MainView = new BrowserMainView
            {
                DataContext = new BrowserMainViewModel(printer, encoder)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}

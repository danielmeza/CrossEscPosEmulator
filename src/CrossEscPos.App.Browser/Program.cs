using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;

namespace CrossEscPos.App.Browser;

internal sealed partial class Program
{
    private static Task Main(string[] args)
    {
        // Compose the browser platform (Skia backend, download export, web transports) and hand it to
        // the shared app, which runs here as a single view.
        CrossEscPos.App.App.Platform = new BrowserPlatformServices();
        return BuildAvaloniaApp()
            .WithInterFont()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<CrossEscPos.App.App>();
}

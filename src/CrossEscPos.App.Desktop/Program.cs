using Avalonia;
using CrossEscPos.App;
using CrossEscPos.Logging;

namespace CrossEscPos.App.Desktop;

public static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [System.STAThread]
    public static void Main(string[] args)
    {
        // Register process-wide exception logging before anything else so startup failures and
        // background-thread crashes (e.g. transport teardown) are recorded rather than lost.
        Logger.InstallGlobalHandlers();

        // Compose the desktop platform (render backend, native dialogs/notifications, TCP+serial
        // transports, Monitor) and hand it to the shared app.
        var backend = RenderBackend.Select(args);
        Logger.Info($"Render backend: {backend.Name}");
        CrossEscPos.App.App.Platform = new DesktopPlatformServices(backend);

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<CrossEscPos.App.App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}

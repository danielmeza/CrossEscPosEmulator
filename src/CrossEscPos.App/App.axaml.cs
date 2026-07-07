using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CrossEscPos.App.ViewModels;
using CrossEscPos.App.Views;
using CrossEscPos.Emulator;

namespace CrossEscPos.App;

/// <summary>
/// The shared Avalonia application, reused by the Desktop and Browser heads. It composes the headless
/// emulator with the head's <see cref="IPlatformServices"/> and shows the shared <see cref="MainView"/>
/// in whichever lifetime the head runs (a desktop <c>Window</c> or a browser single view).
/// </summary>
public partial class App : Application
{
    /// <summary>Set by the head's <c>Program</c> before the app starts.</summary>
    public static IPlatformServices Platform { get; set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var platform = Platform ?? throw new InvalidOperationException(
            "App.Platform must be set by the head before startup.");

        var printer = new ReceiptPrinter(PaperConfiguration.Default, platform.ImageFactory, platform.Typefaces);
        // Transports fire events from background threads (desktop) — marshal to the UI thread.
        printer.UiDispatch = a => Dispatcher.UIThread.Post(a);

        var viewModel = new MainViewModel(printer, platform);
        var mainView = new MainView { DataContext = viewModel };
        platform.AttachRoot(mainView);

        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                var window = platform.CreateMainWindow(mainView);
                desktop.MainWindow = window;
                desktop.ShutdownRequested += (_, _) => { viewModel.Shutdown(); platform.Shutdown(); };
                break;

            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = mainView;
                break;
        }

        base.OnFrameworkInitializationCompleted();
    }
}

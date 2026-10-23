using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;

namespace BoolSheet
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            // Exceptions that escape everything else (background threads, forgotten tasks) are at least logged.
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception exception)
                {
                    CrashLog.Write(exception);
                }
            };
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                CrashLog.Write(e.Exception);
                e.SetObserved();
            };

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                })
                .ConfigureLifecycleEvents(events =>
                {
#if WINDOWS
                    // Closing the window with × or Alt+F4 must not silently lose unsaved changes.
                    events.AddWindows(windows => windows.OnWindowCreated(window =>
                        window.AppWindow.Closing += OnWindowClosing));
#endif
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }

#if WINDOWS
        private static void OnWindowClosing(
            Microsoft.UI.Windowing.AppWindow sender,
            Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            if (Shell.Current?.CurrentPage is not MainPage page || !page.NeedsCloseConfirmation())
            {
                return;
            }

            // Cancel must be set before the first await; the page then asks and closes the app itself.
            args.Cancel = true;
            _ = page.CloseAfterConfirmationAsync();
        }
#endif
    }
}

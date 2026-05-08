using System.Windows;
using System.Windows.Threading;
using ScreenTime.Services;

namespace ScreenTime;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var startHidden = e.Args.Any(arg => arg.Equals("--startup", StringComparison.OrdinalIgnoreCase));
        var mainWindow = new MainWindow(startHidden);
        MainWindow = mainWindow;

        if (startHidden)
        {
            mainWindow.Opacity = 0;
            mainWindow.ShowInTaskbar = false;
        }

        mainWindow.Show();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLogger.Log(e.Exception, "Unhandled dispatcher exception");
        e.Handled = true;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            AppLogger.Log(exception, "Unhandled application exception");
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        AppLogger.Log(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }
}

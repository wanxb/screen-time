using System.Windows;

namespace ScreenTime;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
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
}

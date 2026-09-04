using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SDeleteGUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    /// <summary>
    /// Gets the current application window.
    /// </summary>
    public static Window? MainWindow { get; private set; }

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();

		UnhandledException += App_UnhandledException;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            WriteCrashLog("AppDomain", e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            WriteCrashLog("TaskScheduler", e.Exception);
            e.SetObserved();
        };
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        MainWindow = _window;
        _window.Activate();
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        WriteCrashLog("UnhandledException", e.Exception);
    }

    private static void WriteCrashLog(string source, Exception? exception)
    {
        try
        {
            var log = $"{DateTime.Now} [{source}]: {exception}\n\n";
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(AppContext.BaseDirectory, "crash.log"), log);
        }
        catch
        {
			// Ignore any exceptions while writing the crash log
		}
    }
}

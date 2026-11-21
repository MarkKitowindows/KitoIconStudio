using System.Windows;

namespace KitoIconStudio;

public partial class App : Application
{
    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        var splash = new SplashWindow();
        splash.Show();
        try
        {
            // An introductory splash animation, not a download progress indicator.
            await Task.Delay(950);
            var editor = new MainWindow();
            MainWindow = editor;
            editor.Show();
            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Unable to open Kito Icon Studio:\n" + ex.Message,
                "Kito Icon Studio", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
        finally
        {
            splash.Close();
        }
    }
}

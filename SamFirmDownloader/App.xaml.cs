using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace SamFirmDownloader;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    private static void ShowErrorMessage(string message) => MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

    protected override void OnStartup(StartupEventArgs e)
    {
        TaskScheduler.UnobservedTaskException += (s, e) => ShowErrorMessage(e.Exception.Message);
        AppDomain.CurrentDomain.UnhandledException += (s, e) => ShowErrorMessage(((Exception)e.ExceptionObject).Message);

        Current.DispatcherUnhandledException += (sender, args) =>
        {
            ShowErrorMessage((args.Exception).Message);
            args.Handled = true;
        };

        var service = new ServiceCollection();
        service.AddHttpClient();
        service.ConfigureHttpClientDefaults(config =>
        {
            config.RemoveAllLoggers();
            config.ConfigureHttpClient(client => client.DefaultRequestHeaders.Add("User-Agent", "SamFirm"));
        });
        ServiceProvider = service.BuildServiceProvider();

        base.OnStartup(e);
    }
}
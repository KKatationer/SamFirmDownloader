using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace SamFirmDownloader;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    private static void ShowErrorMessage(string message) => MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpClient();
        services.ConfigureHttpClientDefaults(config =>
        {
            config.RemoveAllLoggers();
            config.ConfigureHttpClient(client => client.DefaultRequestHeaders.Add("User-Agent", "SamFirm"));
        });
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        TaskScheduler.UnobservedTaskException += (s, e) => ShowErrorMessage(e.Exception.Message);
        AppDomain.CurrentDomain.UnhandledException += (s, e) => ShowErrorMessage(((Exception)e.ExceptionObject).Message);

        Current.DispatcherUnhandledException += (sender, args) =>
        {
            ShowErrorMessage((args.Exception).Message);
            args.Handled = true;
        };

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        ServiceProvider = serviceCollection.BuildServiceProvider();

        base.OnStartup(e);
    }
}
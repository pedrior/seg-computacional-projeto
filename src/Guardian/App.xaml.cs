using System.Diagnostics.CodeAnalysis;
using System.Windows;
using Guardian.Services;
using Guardian.ViewModels;
using Guardian.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Guardian;

public partial class App
{
    private static IHost? host;

    public App()
    {
        host = Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .Build();
    }

    public static IServiceProvider Services =>
        host?.Services ?? throw new InvalidOperationException("App not initialized.");

    [SuppressMessage("ReSharper", "AsyncVoidMethod")]
    protected override async void OnStartup(StartupEventArgs e)
    {
        await host!.StartAsync();

        Services.GetRequiredService<MainView>()
            .Show();

        base.OnStartup(e);
    }

    [SuppressMessage("ReSharper", "AsyncVoidMethod")]
    protected override async void OnExit(ExitEventArgs e)
    {
        await host!.StopAsync();

        base.OnExit(e);
    }

    private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        services.AddTransient<ISystemProxyManager, SystemProxyManager>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainView>();
    }
}
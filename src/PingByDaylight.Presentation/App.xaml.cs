using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PingByDaylight.Application.Interfaces;
using PingByDaylight.Application.ViewModels;
using PingByDaylight.Infrastructure.Services;

namespace PingByDaylight.Presentation;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Domain Services
                services.AddSingleton<IServerCatalogService, ServerCatalogService>();
                services.AddSingleton<IConnectionProbeService, ConnectionProbeService>();
                services.AddSingleton<IHostsManagementService, HostsManagementService>();
                services.AddSingleton<ILocalizationService, LocalizationService>();
                services.AddSingleton<IDialogNavigationService, DialogNavigationService>();
                
                // ViewModels
                services.AddTransient<MainWindowViewModel>();
                
                // Views
                services.AddSingleton<MainWindow>();
                
                // Logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                });
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();
        
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
        
        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        
        base.OnExit(e);
    }
}

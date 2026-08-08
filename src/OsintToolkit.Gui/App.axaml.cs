using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OsintToolkit.Data.Context;
using OsintToolkit.Core.Interfaces;
using OsintToolkit.Services.Services;
using OsintToolkit.Core.Models;
using OsintToolkit.Modules.Implementations;

namespace OsintToolkit.Gui;

public partial class App : Application
{
    public static IHost? Host { get; private set; }
    public static IServiceProvider? Services => Host?.Services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Build a minimal host to provide the same services the CLI uses
        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices((hostContext, services) =>
            {
                services.AddDbContext<OsintDbContext>(options =>
                {
                    options.UseSqlite("Data Source=osint_toolkit.db");
                });

                services.AddSingleton<IConfigService, ConfigService>();
                services.AddScoped<ITargetService, TargetService>();
                services.AddScoped<IScanSessionService, ScanSessionService>();
                services.AddScoped<IExportService, ExportService>();
                services.AddSingleton<ILocalNetworkDiscoveryService, LocalNetworkDiscoveryService>();
                services.AddSingleton<INmapScanOptions, NmapScanOptions>();

                services.AddScoped<IOsintModule, UsernameLookupModule>();
                services.AddScoped<IOsintModule, DomainInfoModule>();
                services.AddScoped<IOsintModule, IpInfoModule>();
                services.AddScoped<IOsintModule, EmailInfoModule>();
                services.AddScoped<IOsintModule, NmapModule>();

                services.AddScoped<IModuleRegistry, ModuleRegistry>();
            }).Build();

        Host.Start();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}

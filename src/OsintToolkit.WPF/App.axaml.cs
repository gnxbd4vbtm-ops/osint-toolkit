using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OsintToolkit.Core.Interfaces;
using OsintToolkit.Data.Context;
using OsintToolkit.Modules.Implementations;
using OsintToolkit.Services.Services;
using OsintToolkit.WPF.ViewModels;

namespace OsintToolkit.WPF;

public partial class App : Application
{
    private ServiceProvider? _services;
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Information));
            services.AddDbContext<OsintDbContext>(options => options.UseSqlite("Data Source=osint_toolkit.db"));
            services.AddSingleton<IConfigService, ConfigService>();
            services.AddSingleton<INmapScanOptions, NmapScanOptions>();
            services.AddSingleton<ILocalNetworkDiscoveryService, LocalNetworkDiscoveryService>();
            services.AddScoped<ITargetService, TargetService>(); services.AddScoped<IScanSessionService, ScanSessionService>(); services.AddScoped<IExportService, ExportService>();
            services.AddScoped<IOsintModule, UsernameLookupModule>(); services.AddScoped<IOsintModule, DomainInfoModule>(); services.AddScoped<IOsintModule, IpInfoModule>(); services.AddScoped<IOsintModule, EmailInfoModule>(); services.AddScoped<IOsintModule, NmapModule>();
            services.AddScoped<IModuleRegistry, ModuleRegistry>(); services.AddSingleton<MainViewModel>(); _services = services.BuildServiceProvider();
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var viewModel = _services.GetRequiredService<MainViewModel>();
                var window = new MainWindow { DataContext = viewModel };
                window.Opened += async (_, _) =>
                {
                    try
                    {
                        using var scope = _services.CreateScope();
                        await scope.ServiceProvider.GetRequiredService<OsintDbContext>().Database.EnsureCreatedAsync();
                        await _services.GetRequiredService<IConfigService>().LoadConfigAsync();
                        await viewModel.InitializeAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"OSINT Toolkit GUI initialization failed: {ex}");
                        viewModel.ReportStartupError(ex.Message);
                    }
                };
                desktop.MainWindow = window;
            }
        }
        catch (Exception ex) { Console.Error.WriteLine($"OSINT Toolkit GUI failed to start: {ex}"); throw; }
        finally { base.OnFrameworkInitializationCompleted(); }
    }
}

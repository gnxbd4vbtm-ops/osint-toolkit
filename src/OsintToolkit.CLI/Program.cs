using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OsintToolkit.CLI.Commands;
using OsintToolkit.CLI.UI;
using OsintToolkit.Core.Interfaces;
using OsintToolkit.Data.Context;
using OsintToolkit.Modules.Implementations;
using OsintToolkit.Services.Services;

namespace OsintToolkit.CLI;

public class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            var host = CreateHostBuilder(args).Build();

            // Initialize database schema automatically
            using (var scope = host.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var dbContext = services.GetRequiredService<OsintDbContext>();
                await dbContext.Database.EnsureCreatedAsync();

                var configService = services.GetRequiredService<IConfigService>();
                await configService.LoadConfigAsync();
            }

            // Command handling or interactive menu
            using (var scope = host.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var commandHandler = services.GetRequiredService<CommandHandler>();

                var processed = await commandHandler.ProcessArgsAsync(args);
                if (!processed)
                {
                    var interactiveMenu = services.GetRequiredService<InteractiveMenu>();
                    await interactiveMenu.RunAsync();
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleRenderer.RenderError($"Fatal Application Error: {ex.Message}");
            ConsoleRenderer.RenderError(ex.StackTrace ?? string.Empty);
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
                logging.AddFilter("System", LogLevel.Warning);
            })
            .ConfigureServices((hostContext, services) =>
            {
                // Database context configuration
                services.AddDbContext<OsintDbContext>(options =>
                {
                    options.UseSqlite("Data Source=osint_toolkit.db");
                });

                // Services DI Registration
                services.AddSingleton<IConfigService, ConfigService>();
                services.AddScoped<ITargetService, TargetService>();
                services.AddScoped<IScanSessionService, ScanSessionService>();
                services.AddScoped<IExportService, ExportService>();
                services.AddSingleton<ILocalNetworkDiscoveryService, LocalNetworkDiscoveryService>();
                services.AddSingleton<INmapScanOptions, NmapScanOptions>();

                // Register OSINT Modules
                services.AddScoped<IOsintModule, UsernameLookupModule>();
                services.AddScoped<IOsintModule, DomainInfoModule>();
                services.AddScoped<IOsintModule, IpInfoModule>();
                services.AddScoped<IOsintModule, EmailInfoModule>();
                services.AddScoped<IOsintModule, NmapModule>();

                // Module Registry
                services.AddScoped<IModuleRegistry, ModuleRegistry>();

                // CLI Handlers
                services.AddScoped<CommandHandler>();
                services.AddScoped<InteractiveMenu>();
            });
}

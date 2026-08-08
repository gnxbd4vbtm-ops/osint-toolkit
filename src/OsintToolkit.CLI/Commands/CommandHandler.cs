using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Spectre.Console;
using OsintToolkit.CLI.UI;
using OsintToolkit.Core.Enums;
using OsintToolkit.Core.Interfaces;
using OsintToolkit.Core.Models;

namespace OsintToolkit.CLI.Commands;

/// <summary>
/// Dispatches and processes non-interactive direct command line flags/args.
/// </summary>
public class CommandHandler
{
    private readonly ITargetService _targetService;
    private readonly IScanSessionService _scanSessionService;
    private readonly IModuleRegistry _moduleRegistry;
    private readonly IExportService _exportService;
    private readonly IConfigService _configService;
    private readonly INmapScanOptions _nmapOptions;
    private readonly ILocalNetworkDiscoveryService _localNetworkDiscovery;

    public CommandHandler(
        ITargetService targetService,
        IScanSessionService scanSessionService,
        IModuleRegistry moduleRegistry,
        IExportService exportService,
        IConfigService configService,
        INmapScanOptions nmapOptions,
        ILocalNetworkDiscoveryService localNetworkDiscovery)
    {
        _targetService = targetService;
        _scanSessionService = scanSessionService;
        _moduleRegistry = moduleRegistry;
        _exportService = exportService;
        _configService = configService; _nmapOptions = nmapOptions; _localNetworkDiscovery = localNetworkDiscovery;
    }

    public async Task<bool> ProcessArgsAsync(string[] args)
    {
        if (args == null || args.Length == 0) return false;

        var verb = args[0].ToLowerInvariant();

        switch (verb)
        {
            case "help":
            case "-h":
            case "--help":
                ShowHelp();
                return true;

            case "version":
            case "-v":
            case "--version":
                ShowVersion();
                return true;

            case "modules":
                ShowModules();
                return true;

            case "targets":
                await ListTargetsAsync();
                return true;

            case "scan":
                await HandleScanCommandAsync(args.Skip(1).ToArray());
                return true;

            case "export":
                await HandleExportCommandAsync(args.Skip(1).ToArray());
                return true;

            case "config":
                ShowConfig();
                return true;

            case "arp":
                await HandleArpCommandAsync(args.Skip(1).ToArray());
                return true;

            default:
                ConsoleRenderer.RenderError($"Unknown command '{verb}'. Run with 'help' for available commands.");
                return true;
        }
    }

    public void ShowHelp()
    {
        Banner.ShowBanner();
        ConsoleRenderer.RenderHeader("OSINT Toolkit CLI Usage");

        AnsiConsole.MarkupLine("[bold yellow]Usage:[/] osint-cli [command] [options]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold yellow]Available Commands:[/]");

        var table = new Table();
        table.Border(TableBorder.Minimal);
        table.AddColumn("[cyan]Command[/]");
        table.AddColumn("[white]Description[/]");

        table.AddRow("help, --help", "Display help and command line options");
        table.AddRow("version, --version", "Display application version and metadata");
        table.AddRow("modules", "List all registered OSINT modules");
        table.AddRow("targets", "List all targets saved in database");
        table.AddRow("scan --target <val> [--type <type>] [--nmap-profile <profile>]", "Run an OSINT scan; Nmap profiles: Discovery, Quick, Standard, FullTcp");
        table.AddRow("export --session <id> [--format <json|csv|md>]", "Export a scan session to file");
        table.AddRow("config", "View current toolkit settings");
        table.AddRow("arp hosts --localnet [--resolve]", "List local ARP/neighbor-table hosts; optionally reverse-resolve names");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]If no command is provided, the application starts in interactive menu mode.[/]");
    }

    public void ShowVersion()
    {
        AnsiConsole.MarkupLine($"[bold cyan]{AppInfo.Name}[/]");
        AnsiConsole.MarkupLine($"[bold white]Version:[/] {AppInfo.Version}");
        AnsiConsole.MarkupLine($"[bold white]Author:[/] {AppInfo.Author}");
        AnsiConsole.MarkupLine($"[bold white]License:[/] {AppInfo.License}");
        AnsiConsole.MarkupLine($"[bold white]Repository:[/] {AppInfo.Repository}");
    }

    public void ShowModules()
    {
        var modules = _moduleRegistry.GetAllModules();
        ConsoleRenderer.RenderHeader("Registered OSINT Modules");
        ConsoleRenderer.RenderModuleTable(modules);
    }

    private async Task ListTargetsAsync()
    {
        var targets = await _targetService.GetAllTargetsAsync();
        ConsoleRenderer.RenderHeader("Target Database");
        if (!targets.Any())
        {
            ConsoleRenderer.RenderWarning("No targets currently exist in database.");
            return;
        }
        ConsoleRenderer.RenderTargetTable(targets);
    }

    private async Task HandleScanCommandAsync(string[] args)
    {
        string? targetValue = null;
        string? targetTypeStr = null;
        string? nmapProfile = null;

        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--target" || args[i] == "-t") && i + 1 < args.Length)
                targetValue = args[++i];
            else if ((args[i] == "--type" || args[i] == "-type") && i + 1 < args.Length)
                targetTypeStr = args[++i];
            else if (args[i] == "--nmap-profile" && i + 1 < args.Length)
                nmapProfile = args[++i];
        }

        if (string.IsNullOrWhiteSpace(targetValue))
        {
            ConsoleRenderer.RenderError("Missing required parameter '--target <value>'.");
            return;
        }

        TargetType type;
        if (!string.IsNullOrWhiteSpace(targetTypeStr) && Enum.TryParse<TargetType>(targetTypeStr, true, out var parsedType))
        {
            type = parsedType;
        }
        else
        {
            type = _targetService.DetectTargetType(targetValue);
            ConsoleRenderer.RenderInfo($"Auto-detected target type: [bold cyan]{type}[/]");
        }

        var target = await _targetService.CreateTargetAsync(targetValue, type, "CLI direct scan target");
        if (nmapProfile is not null)
        {
            if (!Enum.TryParse(nmapProfile, true, out NmapScanProfile profile))
            {
                ConsoleRenderer.RenderError("Invalid Nmap profile. Use Discovery, Quick, Standard, or FullTcp.");
                return;
            }
            _nmapOptions.Profile = profile;
        }

        ConsoleRenderer.RenderInfo($"Executing OSINT scan session against target '{target.Value}'...");

        ScanSession session = null!;
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("green"))
            .StartAsync("Gathering OSINT intelligence...", async ctx =>
            {
                session = await _scanSessionService.ExecuteScanAsync(target.Id);
            });

        ConsoleRenderer.RenderSuccess($"Scan session #{session.Id} completed! Total findings: {session.Results.Count}");
        ConsoleRenderer.RenderScanResultDetails(session);
    }

    private async Task HandleExportCommandAsync(string[] args)
    {
        int sessionId = 0;
        string format = _configService.Config.DefaultExportFormat;

        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--session" || args[i] == "-s") && i + 1 < args.Length && int.TryParse(args[++i], out var id))
                sessionId = id;
            else if ((args[i] == "--format" || args[i] == "-f") && i + 1 < args.Length)
                format = args[++i];
        }

        if (sessionId <= 0)
        {
            ConsoleRenderer.RenderError("Missing or invalid parameter '--session <id>'.");
            return;
        }

        var session = await _scanSessionService.GetSessionByIdAsync(sessionId);
        if (session == null)
        {
            ConsoleRenderer.RenderError($"Scan session ID {sessionId} not found.");
            return;
        }

        var filePath = await _exportService.ExportScanSessionAsync(session, format);
        ConsoleRenderer.RenderSuccess($"Exported session #{sessionId} to: [bold white]{filePath}[/]");
    }

    private async Task HandleArpCommandAsync(string[] args)
    {
        if (args.Length == 0 || !args[0].Equals("hosts", StringComparison.OrdinalIgnoreCase) || !args.Any(arg => arg.Equals("--localnet", StringComparison.OrdinalIgnoreCase)))
        {
            ConsoleRenderer.RenderError("Usage: arp hosts --localnet [--resolve]");
            return;
        }
        var resolve = args.Any(arg => arg.Equals("--resolve", StringComparison.OrdinalIgnoreCase) || arg.Equals("/resolve", StringComparison.OrdinalIgnoreCase));
        try
        {
            var hosts = await _localNetworkDiscovery.GetArpHostsAsync(resolve);
            ConsoleRenderer.RenderHeader("Local ARP / Neighbor Hosts");
            if (!hosts.Any()) { ConsoleRenderer.RenderWarning("No reachable IPv4 neighbors are currently present in the local ARP table."); return; }
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("[cyan]Address[/]"); table.AddColumn("[cyan]MAC[/]"); table.AddColumn("[cyan]Interface[/]"); table.AddColumn("[cyan]State[/]"); table.AddColumn("[cyan]Hostname[/]");
            foreach (var host in hosts) table.AddRow(Markup.Escape(host.Address), Markup.Escape(host.MacAddress), Markup.Escape(host.Interface), Markup.Escape(host.State), Markup.Escape(host.Hostname ?? "—"));
            AnsiConsole.Write(table);
        }
        catch (Exception ex) { ConsoleRenderer.RenderError($"Could not read local ARP hosts: {ex.Message}"); }
    }

    private void ShowConfig()
    {
        ConsoleRenderer.RenderHeader("Toolkit Configuration Settings");
        var cfg = _configService.Config;

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[bold yellow]Setting[/]");
        table.AddColumn("[bold yellow]Value[/]");

        table.AddRow("Database Path", cfg.DatabasePath);
        table.AddRow("Default Export Format", cfg.DefaultExportFormat);
        table.AddRow("Log Level", cfg.LogLevel);
        table.AddRow("Max Concurrent Modules", cfg.MaxConcurrentModules.ToString());

        foreach (var kvp in cfg.ApiKeys)
        {
            table.AddRow($"API Key: {kvp.Key}", kvp.Value);
        }

        AnsiConsole.Write(table);
    }
}

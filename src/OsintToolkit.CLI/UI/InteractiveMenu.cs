using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Spectre.Console;
using OsintToolkit.Core.Enums;
using OsintToolkit.Core.Interfaces;
using OsintToolkit.Core.Models;
using OsintToolkit.CLI.Commands;

namespace OsintToolkit.CLI.UI;

/// <summary>
/// Main interactive CLI menu system powered by Spectre.Console prompts.
/// </summary>
public class InteractiveMenu
{
    private readonly ITargetService _targetService;
    private readonly IScanSessionService _scanSessionService;
    private readonly IModuleRegistry _moduleRegistry;
    private readonly IExportService _exportService;
    private readonly IConfigService _configService;
    private readonly INmapScanOptions _nmapOptions;

    public InteractiveMenu(
        ITargetService targetService,
        IScanSessionService scanSessionService,
        IModuleRegistry moduleRegistry,
        IExportService exportService,
        IConfigService configService,
        INmapScanOptions nmapOptions)
    {
        _targetService = targetService;
        _scanSessionService = scanSessionService;
        _moduleRegistry = moduleRegistry;
        _exportService = exportService;
        _configService = configService; _nmapOptions = nmapOptions;
    }

    public async Task RunAsync()
    {
        while (true)
        {
            Banner.ShowBanner();

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold yellow]OSINT Toolkit Navigation Menu:[/]")
                    .PageSize(10)
                    .MoreChoicesText("[grey](Move up and down to reveal more options)[/]")
                    .AddChoices(new[]
                    {
                        "🎯 Target Management",
                        "⚡ Run OSINT Scan",
                        "📊 Scan History & Findings",
                        "💾 Export Scan Results",
                        "🧩 Registered Modules",
                        "⚙️ Toolkit Configuration",
                        "❓ Help & Application Info",
                        "❌ Exit"
                    }));

            switch (choice)
            {
                case "🎯 Target Management":
                    await ManageTargetsMenuAsync();
                    break;
                case "⚡ Run OSINT Scan":
                    await RunScanMenuAsync();
                    break;
                case "📊 Scan History & Findings":
                    await ViewHistoryMenuAsync();
                    break;
                case "💾 Export Scan Results":
                    await ExportMenuAsync();
                    break;
                case "🧩 Registered Modules":
                    ShowModules();
                    break;
                case "⚙️ Toolkit Configuration":
                    await ManageConfigMenuAsync();
                    break;
                case "❓ Help & Application Info":
                    ShowHelp();
                    break;
                case "❌ Exit":
                    AnsiConsole.MarkupLine("[bold green]Thank you for using C# OSINT Toolkit. Goodbye![/]");
                    return;
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Press any key to return to main menu...[/]");
            Console.ReadKey(true);
        }
    }

    private async Task ManageTargetsMenuAsync()
    {
        ConsoleRenderer.RenderHeader("Target Management");

        var subChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Choose target operation:")
                .AddChoices(new[]
                {
                    "1. View All Targets",
                    "2. Add New Target",
                    "3. Delete Target",
                    "4. Back to Main Menu"
                }));

        if (subChoice.StartsWith("1"))
        {
            var targets = await _targetService.GetAllTargetsAsync();
            if (!targets.Any())
            {
                ConsoleRenderer.RenderWarning("No targets currently stored.");
                return;
            }
            ConsoleRenderer.RenderTargetTable(targets);
        }
        else if (subChoice.StartsWith("2"))
        {
            var input = AnsiConsole.Ask<string>("Enter target value (Username, Domain, IP, Email, Person):");
            if (string.IsNullOrWhiteSpace(input)) return;

            var detected = _targetService.DetectTargetType(input);
            var selectedType = AnsiConsole.Prompt(
                new SelectionPrompt<TargetType>()
                    .Title($"Detected target type is [bold cyan]{detected}[/]. Confirm or select override:")
                    .AddChoices(Enum.GetValues<TargetType>()));

            var description = AnsiConsole.Ask<string>("Enter description / context notes (optional):", string.Empty);

            var created = await _targetService.CreateTargetAsync(input, selectedType, description);
            ConsoleRenderer.RenderSuccess($"Target '{created.Value}' [{created.Type}] saved successfully with ID #{created.Id}!");
        }
        else if (subChoice.StartsWith("3"))
        {
            var targets = await _targetService.GetAllTargetsAsync();
            if (!targets.Any())
            {
                ConsoleRenderer.RenderWarning("No targets available to delete.");
                return;
            }

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<Target>()
                    .Title("Select target to delete:")
                    .UseConverter(t => $"#{t.Id} - {Markup.Escape(t.Value)} ({t.Type})")
                    .AddChoices(targets));

            if (AnsiConsole.Confirm($"Are you sure you want to delete target '{selected.Value}' and all associated scans?"))
            {
                await _targetService.DeleteTargetAsync(selected.Id);
                ConsoleRenderer.RenderSuccess($"Target #{selected.Id} deleted.");
            }
        }
    }

    private async Task RunScanMenuAsync()
    {
        ConsoleRenderer.RenderHeader("Run OSINT Scan");

        var targets = await _targetService.GetAllTargetsAsync();
        Target targetToScan;

        if (targets.Any() && AnsiConsole.Confirm("Select target from existing database?"))
        {
            targetToScan = AnsiConsole.Prompt(
                new SelectionPrompt<Target>()
                    .Title("Select target to scan:")
                    .UseConverter(t => $"#{t.Id} - {Markup.Escape(t.Value)} ({t.Type})")
                    .AddChoices(targets));
        }
        else
        {
            var input = AnsiConsole.Ask<string>("Enter target value to scan:");
            if (string.IsNullOrWhiteSpace(input)) return;

            var detected = _targetService.DetectTargetType(input);
            targetToScan = await _targetService.CreateTargetAsync(input, detected, "Created during scan run");
            ConsoleRenderer.RenderSuccess($"Target '{targetToScan.Value}' [{targetToScan.Type}] initialized with ID #{targetToScan.Id}");
        }

        var availableModules = _moduleRegistry.GetModulesForTargetType(targetToScan.Type).ToList();
        if (!availableModules.Any())
        {
            ConsoleRenderer.RenderWarning($"No registered modules support target type '{targetToScan.Type}'.");
            return;
        }

        List<IOsintModule> selectedModules = AnsiConsole.Prompt(
            new MultiSelectionPrompt<IOsintModule>()
                .Title("Select OSINT modules to execute:")
                .PageSize(10)
                .InstructionsText("[grey](Press [blue]<space>[/] to toggle a module, [blue]<enter>[/] to accept)[/]")
                .UseConverter(m => $"{Markup.Escape(m.Name)} - {Markup.Escape(m.Description)} [bold cyan]({Markup.Escape(m.Category)})[/]")
                .AddChoices(availableModules));

        if (!selectedModules.Any())
        {
            ConsoleRenderer.RenderWarning("No modules selected. Aborting scan.");
            return;
        }

        if (selectedModules.Any(module => module.Name.Equals("Nmap", StringComparison.OrdinalIgnoreCase)))
        {
            _nmapOptions.Profile = AnsiConsole.Prompt(
                new SelectionPrompt<NmapScanProfile>()
                    .Title("Select the authorized Nmap scan profile:")
                    .AddChoices(Enum.GetValues<NmapScanProfile>()));
            ConsoleRenderer.RenderWarning("Only scan hosts and networks you are explicitly authorized to assess.");
        }

        ConsoleRenderer.RenderInfo($"Executing {selectedModules.Count} module(s) against target '{targetToScan.Value}'...");

        ScanSession session = null!;
        var moduleNames = selectedModules.Select(m => m.Name).ToList();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync("Executing OSINT modules...", async ctx =>
            {
                session = await _scanSessionService.ExecuteScanAsync(targetToScan.Id, moduleNames);
            });

        ConsoleRenderer.RenderSuccess($"Scan session #{session.Id} finished with status '{session.Status}'! Total findings: {session.Results.Count}");
        ConsoleRenderer.RenderScanResultDetails(session);
    }

    private async Task ViewHistoryMenuAsync()
    {
        ConsoleRenderer.RenderHeader("Scan Session History");
        var sessions = await _scanSessionService.GetAllSessionsAsync();

        if (!sessions.Any())
        {
            ConsoleRenderer.RenderWarning("No scan sessions recorded yet.");
            return;
        }

        ConsoleRenderer.RenderSessionTable(sessions);

        if (AnsiConsole.Confirm("Inspect detailed findings of a scan session?"))
        {
            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<ScanSession>()
                    .Title("Select session to inspect:")
                    .UseConverter(s => $"Session #{s.Id} - Target: {Markup.Escape(s.Target?.Value ?? "N/A")} ({s.Target?.Type}) ({s.StartedAt:yyyy-MM-dd HH:mm})")
                    .AddChoices(sessions));

            ConsoleRenderer.RenderScanResultDetails(selected);
        }
    }

    private async Task ExportMenuAsync()
    {
        ConsoleRenderer.RenderHeader("Export Scan Results");

        var sessions = await _scanSessionService.GetAllSessionsAsync();
        if (!sessions.Any())
        {
            ConsoleRenderer.RenderWarning("No scan sessions available to export.");
            return;
        }

        var selectedSession = AnsiConsole.Prompt(
            new SelectionPrompt<ScanSession>()
                .Title("Select scan session to export:")
                .UseConverter(s => $"Session #{s.Id} - Target: {Markup.Escape(s.Target?.Value ?? "N/A")} ({s.Target?.Type}) ({s.Results.Count} Findings)")
                .AddChoices(sessions));

        var format = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select export file format:")
                .AddChoices(new[] { "json", "csv", "markdown" }));

        var filePath = await _exportService.ExportScanSessionAsync(selectedSession, format);
        ConsoleRenderer.RenderSuccess($"Exported session #{selectedSession.Id} to: [bold white]{filePath}[/]");
    }

    private void ShowModules()
    {
        ConsoleRenderer.RenderHeader("Registered OSINT Modules");
        var modules = _moduleRegistry.GetAllModules();
        ConsoleRenderer.RenderModuleTable(modules);
    }

    private async Task ManageConfigMenuAsync()
    {
        ConsoleRenderer.RenderHeader("Toolkit Configuration Settings");
        var cfg = _configService.Config;

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[bold yellow]Setting Key[/]");
        table.AddColumn("[bold yellow]Current Value[/]");

        table.AddRow("Database Path", cfg.DatabasePath);
        table.AddRow("Default Export Format", cfg.DefaultExportFormat);
        table.AddRow("Log Level", cfg.LogLevel);
        table.AddRow("Max Concurrent Modules", cfg.MaxConcurrentModules.ToString());

        foreach (var kvp in cfg.ApiKeys)
        {
            table.AddRow($"API Key: {kvp.Key}", kvp.Value);
        }

        AnsiConsole.Write(table);

        if (AnsiConsole.Confirm("Change default export format setting?"))
        {
            var newFormat = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select new default export format:")
                    .AddChoices(new[] { "json", "csv", "markdown" }));

            cfg.DefaultExportFormat = newFormat;
            await _configService.SaveConfigAsync();
            ConsoleRenderer.RenderSuccess($"Saved default export format as '{newFormat}'.");
        }
    }

    private static void ShowHelp()
    {
        Banner.ShowBanner();
        ConsoleRenderer.RenderHeader("OSINT Framework Information & Guidance");
        AnsiConsole.MarkupLine("[bold white]Framework Architecture Overview:[/]");
        AnsiConsole.MarkupLine("• [cyan]OsintToolkit.Core:[/] Contains domain entities, models, contracts, and enum specifications.");
        AnsiConsole.MarkupLine("• [cyan]OsintToolkit.Data:[/] SQLite persistence layer powered by Entity Framework Core.");
        AnsiConsole.MarkupLine("• [cyan]OsintToolkit.Services:[/] Business domain services (Target, ScanSession, Export, Config).");
        AnsiConsole.MarkupLine("• [cyan]OsintToolkit.Modules:[/] Extensible OSINT plugins implementing IOsintModule.");
        AnsiConsole.MarkupLine("• [cyan]OsintToolkit.CLI:[/] Spectre.Console CLI interface, interactive menus, and direct command execution.");
    }
}

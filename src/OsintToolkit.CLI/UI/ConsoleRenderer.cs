using System;
using System.Collections.Generic;
using System.Linq;
using Spectre.Console;
using OsintToolkit.Core.Enums;
using OsintToolkit.Core.Interfaces;
using OsintToolkit.Core.Models;

namespace OsintToolkit.CLI.UI;

/// <summary>
/// Spectre.Console helper methods for clean terminal output and tables.
/// </summary>
public static class ConsoleRenderer
{
    public static void RenderSuccess(string message)
    {
        AnsiConsole.MarkupLine($"[bold green][[✓]][/] {Markup.Escape(message)}");
    }

    public static void RenderInfo(string message)
    {
        AnsiConsole.MarkupLine($"[bold blue][[i]][/] {Markup.Escape(message)}");
    }

    public static void RenderWarning(string message)
    {
        AnsiConsole.MarkupLine($"[bold yellow][[!]][/] {Markup.Escape(message)}");
    }

    public static void RenderError(string message)
    {
        AnsiConsole.MarkupLine($"[bold red][[✗]][/] {Markup.Escape(message)}");
    }

    public static void RenderHeader(string title)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[bold cyan]{Markup.Escape(title)}[/]") { Justification = Justify.Left });
    }

    public static void RenderTargetTable(IEnumerable<Target> targets)
    {
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[bold yellow]ID[/]");
        table.AddColumn("[bold yellow]Target Value[/]");
        table.AddColumn("[bold yellow]Type[/]");
        table.AddColumn("[bold yellow]Description[/]");
        table.AddColumn("[bold yellow]Created At (UTC)[/]");
        table.AddColumn("[bold yellow]Last Scanned[/]");

        foreach (var t in targets)
        {
            table.AddRow(
                t.Id.ToString(),
                $"[bold white]{Markup.Escape(t.Value)}[/]",
                $"[cyan]{t.Type}[/]",
                Markup.Escape(string.IsNullOrWhiteSpace(t.Description) ? "-" : t.Description),
                t.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                t.LastScannedAt.HasValue ? t.LastScannedAt.Value.ToString("yyyy-MM-dd HH:mm") : "[grey]Never[/]"
            );
        }

        AnsiConsole.Write(table);
    }

    public static void RenderSessionTable(IEnumerable<ScanSession> sessions)
    {
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[bold yellow]Session ID[/]");
        table.AddColumn("[bold yellow]Target[/]");
        table.AddColumn("[bold yellow]Type[/]");
        table.AddColumn("[bold yellow]Status[/]");
        table.AddColumn("[bold yellow]Findings[/]");
        table.AddColumn("[bold yellow]Started At (UTC)[/]");

        foreach (var s in sessions)
        {
            var statusColor = s.Status switch
            {
                ScanStatus.Completed => "green",
                ScanStatus.Running => "blue",
                ScanStatus.Failed => "red",
                ScanStatus.Cancelled => "yellow",
                _ => "grey"
            };

            table.AddRow(
                s.Id.ToString(),
                $"[bold white]{Markup.Escape(s.Target?.Value ?? "N/A")}[/]",
                $"[cyan]{s.Target?.Type}[/]",
                $"[{statusColor}]{s.Status}[/]",
                s.Results.Count.ToString(),
                s.StartedAt.ToString("yyyy-MM-dd HH:mm:ss")
            );
        }

        AnsiConsole.Write(table);
    }

    public static void RenderModuleTable(IEnumerable<IOsintModule> modules)
    {
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[bold yellow]Name[/]");
        table.AddColumn("[bold yellow]Category[/]");
        table.AddColumn("[bold yellow]Supported Target Types[/]");
        table.AddColumn("[bold yellow]Description[/]");

        foreach (var m in modules)
        {
            table.AddRow(
                $"[bold green]{m.Name}[/]",
                $"[cyan]{m.Category}[/]",
                string.Join(", ", m.SupportedTypes.Select(t => $"[blue]{t}[/]")),
                Markup.Escape(m.Description)
            );
        }

        AnsiConsole.Write(table);
    }

    public static void RenderScanResultDetails(ScanSession session)
    {
        RenderHeader($"Scan Session #{session.Id} Detailed Findings for '{session.Target?.Value}'");

        if (!session.Results.Any())
        {
            RenderWarning("No findings registered during this scan session.");
            return;
        }

        foreach (var r in session.Results)
        {
            var severityColor = r.Severity switch
            {
                ResultSeverity.Critical => "bold red",
                ResultSeverity.High => "red",
                ResultSeverity.Medium => "yellow",
                ResultSeverity.Low => "blue",
                _ => "green"
            };

            var panel = new Panel(
                new Markup($"[bold]Module:[/] {Markup.Escape(r.ModuleName)}\n" +
                           $"[bold]Severity:[/] [{severityColor}]{r.Severity}[/]\n" +
                           $"[bold]Timestamp:[/] {r.Timestamp:yyyy-MM-dd HH:mm:ss UTC}\n\n" +
                           $"[bold]Summary:[/]\n{Markup.Escape(r.Summary)}\n\n" +
                           $"[bold]Raw Data Output:[/]\n[grey]{Markup.Escape(r.RawDataJson)}[/]")
            )
            {
                Header = new PanelHeader($"[{severityColor}] {Markup.Escape(r.Title)} [/]"),
                Border = BoxBorder.Rounded
            };

            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();
        }
    }
}

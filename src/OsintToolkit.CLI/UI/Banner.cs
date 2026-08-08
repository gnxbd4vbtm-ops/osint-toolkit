using Spectre.Console;

namespace OsintToolkit.CLI.UI;

public static class Banner
{
    public static void ShowBanner()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(
            new FigletText("OSINT TOOLKIT")
                .Color(Color.Cyan1));

        AnsiConsole.Write(new Rule($"[bold blue]Open Source Intelligence CLI Framework v{Commands.AppInfo.Version}[/]")
        {
            Justification = Justify.Left
        });
        AnsiConsole.MarkupLine("[grey]Target Management | Modular Recon Engine | SQLite Storage | Multi-Format Export[/]");
        AnsiConsole.WriteLine();
    }
}

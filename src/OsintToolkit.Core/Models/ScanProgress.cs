namespace OsintToolkit.Core.Models;

/// <summary>Describes scan execution progress for any presentation layer.</summary>
public sealed class ScanProgress
{
    public int CompletedModules { get; init; }
    public int TotalModules { get; init; }
    public string CurrentModule { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public double Percentage => TotalModules == 0 ? 100 : CompletedModules * 100d / TotalModules;
}

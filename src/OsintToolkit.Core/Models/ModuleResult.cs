using OsintToolkit.Core.Enums;

namespace OsintToolkit.Core.Models;

/// <summary>
/// Standardized result returned by OSINT modules upon execution.
/// </summary>
public class ModuleResult
{
    public bool IsSuccess { get; set; } = true;
    public string ModuleName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public object? RawData { get; set; }
    public ResultSeverity Severity { get; set; } = ResultSeverity.Info;
    public string ErrorMessage { get; set; } = string.Empty;

    public static ModuleResult Success(string moduleName, string title, string summary, object rawData, ResultSeverity severity = ResultSeverity.Info)
    {
        return new ModuleResult
        {
            IsSuccess = true,
            ModuleName = moduleName,
            Title = title,
            Summary = summary,
            RawData = rawData,
            Severity = severity
        };
    }

    public static ModuleResult Failure(string moduleName, string errorMessage)
    {
        return new ModuleResult
        {
            IsSuccess = false,
            ModuleName = moduleName,
            ErrorMessage = errorMessage,
            Severity = ResultSeverity.High
        };
    }
}

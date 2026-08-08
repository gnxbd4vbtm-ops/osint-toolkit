using System;
using System.Text.Json.Serialization;
using OsintToolkit.Core.Enums;

namespace OsintToolkit.Core.Models;

/// <summary>
/// Database record for a single finding produced by an OSINT module.
/// </summary>
public class ScanResult
{
    public int Id { get; set; }
    public int ScanSessionId { get; set; }
    
    [JsonIgnore]
    public ScanSession? ScanSession { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string RawDataJson { get; set; } = "{}";
    public ResultSeverity Severity { get; set; } = ResultSeverity.Info;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

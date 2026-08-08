using System.Collections.Generic;

namespace OsintToolkit.Core.Models;

/// <summary>
/// Application settings persisted to config.json.
/// </summary>
public class AppConfig
{
    public string DatabasePath { get; set; } = "osint_toolkit.db";
    public string DefaultExportFormat { get; set; } = "json"; // json, csv, markdown
    public string LogLevel { get; set; } = "Information";
    public int MaxConcurrentModules { get; set; } = 4;
    public string Theme { get; set; } = "Dark";

    /// <summary>
    /// Placeholder dictionary for future API keys (e.g. Shodan, VirusTotal, Hunter.io).
    /// </summary>
    public Dictionary<string, string> ApiKeys { get; set; } = new()
    {
        { "Shodan", "YOUR_SHODAN_API_KEY" },
        { "VirusTotal", "YOUR_VIRUSTOTAL_API_KEY" },
        { "HunterIo", "YOUR_HUNTER_API_KEY" }
    };
}

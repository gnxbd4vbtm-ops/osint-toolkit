using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OsintToolkit.Core.Interfaces;
using OsintToolkit.Core.Models;

namespace OsintToolkit.Services.Services;

/// <summary>
/// Handles exporting scan results into JSON, CSV, Markdown, and Interactive HTML formats.
/// </summary>
public class ExportService : IExportService
{
    private readonly ILogger<ExportService> _logger;

    public ExportService(ILogger<ExportService> logger)
    {
        _logger = logger;
    }

    public async Task<string> ExportScanSessionAsync(ScanSession session, string format, string outputDirectory = "exports")
    {
        if (session == null) throw new ArgumentNullException(nameof(session));

        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var normalizedFormat = format.Trim().ToLower();
        var fileName = $"scan_session_{session.Id}_{session.Target?.Value ?? "target"}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        string filePath;

        switch (normalizedFormat)
        {
            case "csv":
                filePath = Path.Combine(outputDirectory, $"{fileName}.csv");
                await ExportCsvAsync(session, filePath);
                break;
            case "md":
            case "markdown":
                filePath = Path.Combine(outputDirectory, $"{fileName}.md");
                await ExportMarkdownAsync(session, filePath);
                break;
            case "html":
            case "web":
                filePath = Path.Combine(outputDirectory, $"{fileName}.html");
                await ExportHtmlAsync(session, filePath);
                break;
            case "json":
            default:
                filePath = Path.Combine(outputDirectory, $"{fileName}.json");
                await ExportJsonAsync(session, filePath);
                break;
        }

        _logger.LogInformation("Exported scan session #{SessionId} to {FilePath}", session.Id, filePath);
        return filePath;
    }

    private static async Task ExportJsonAsync(ScanSession session, string filePath)
    {
        var exportData = new
        {
            SessionId = session.Id,
            Target = new
            {
                session.Target?.Id,
                session.Target?.Value,
                Type = session.Target?.Type.ToString(),
                session.Target?.Description
            },
            Status = session.Status.ToString(),
            session.StartedAt,
            session.CompletedAt,
            session.Notes,
            ResultsCount = session.Results.Count,
            Results = session.Results
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };
        var json = JsonSerializer.Serialize(exportData, options);
        await File.WriteAllTextAsync(filePath, json);
    }

    private static async Task ExportCsvAsync(ScanSession session, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ResultId,SessionId,Target,ModuleName,Title,Severity,Summary,Timestamp");

        foreach (var r in session.Results)
        {
            var targetValue = session.Target?.Value.Replace("\"", "\"\"") ?? "";
            var title = r.Title.Replace("\"", "\"\"");
            var summary = r.Summary.Replace("\"", "\"\"").Replace("\n", " ");

            sb.AppendLine($"{r.Id},{session.Id},\"{targetValue}\",\"{r.ModuleName}\",\"{title}\",\"{r.Severity}\",\"{summary}\",\"{r.Timestamp:o}\"");
        }

        await File.WriteAllTextAsync(filePath, sb.ToString());
    }

    private static async Task ExportMarkdownAsync(ScanSession session, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# OSINT Scan Report - Session #{session.Id}");
        sb.AppendLine();
        sb.AppendLine($"**Target:** `{session.Target?.Value}` ({session.Target?.Type})  ");
        sb.AppendLine($"**Status:** `{session.Status}`  ");
        sb.AppendLine($"**Started At:** {session.StartedAt:yyyy-MM-dd HH:mm:ss UTC}  ");
        sb.AppendLine($"**Completed At:** {session.CompletedAt:yyyy-MM-dd HH:mm:ss UTC}  ");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Findings Summary");
        sb.AppendLine();

        if (session.Results.Count == 0)
        {
            sb.AppendLine("_No results recorded for this session._");
        }
        else
        {
            foreach (var r in session.Results)
            {
                sb.AppendLine($"### [{r.Severity}] {r.Title}");
                sb.AppendLine($"* **Module:** `{r.ModuleName}`");
                sb.AppendLine($"* **Timestamp:** {r.Timestamp:yyyy-MM-dd HH:mm:ss UTC}");
                sb.AppendLine($"* **Summary:** {r.Summary}");
                sb.AppendLine();
                sb.AppendLine("```json");
                sb.AppendLine(r.RawDataJson);
                sb.AppendLine("```");
                sb.AppendLine();
            }
        }

        await File.WriteAllTextAsync(filePath, sb.ToString());
    }

    private static async Task ExportHtmlAsync(ScanSession session, string filePath)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };

        var sessionJson = JsonSerializer.Serialize(session, jsonOptions);

        var htmlContent = $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>OSINT Intelligence Report - Session #{{session.Id}}</title>
            <link href="https://fonts.googleapis.com/css2?family=Inter:wght@300;400;600;700&family=Fira+Code:wght@400;500&display=swap" rel="stylesheet">
            <style>
                :root {
                    --bg: #0d1117;
                    --card-bg: #161b22;
                    --border: #30363d;
                    --text: #c9d1d9;
                    --text-heading: #f0f6fc;
                    --accent: #58a6ff;
                    --success: #3fb950;
                    --warning: #d29922;
                    --danger: #f85149;
                }
                body {
                    font-family: 'Inter', -apple-system, BlinkMacSystemFont, sans-serif;
                    background-color: var(--bg);
                    color: var(--text);
                    margin: 0;
                    padding: 24px;
                }
                .container { max-width: 1100px; margin: 0 auto; }
                .header {
                    background: linear-gradient(135deg, #1f242d 0%, #161b22 100%);
                    border: 1px solid var(--border);
                    border-radius: 12px;
                    padding: 24px;
                    margin-bottom: 24px;
                }
                .header h1 { margin: 0 0 12px 0; color: var(--text-heading); font-size: 24px; }
                .meta-grid {
                    display: grid;
                    grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
                    gap: 16px;
                    margin-top: 16px;
                }
                .meta-card {
                    background: rgba(255,255,255,0.03);
                    border: 1px solid var(--border);
                    border-radius: 8px;
                    padding: 12px 16px;
                }
                .meta-card .label { font-size: 12px; color: #8b949e; text-transform: uppercase; }
                .meta-card .val { font-size: 16px; font-weight: 600; color: var(--text-heading); margin-top: 4px; }
                
                .finding-card {
                    background: var(--card-bg);
                    border: 1px solid var(--border);
                    border-radius: 10px;
                    margin-bottom: 20px;
                    overflow: hidden;
                }
                .finding-header {
                    padding: 16px 20px;
                    background: rgba(255,255,255,0.02);
                    border-bottom: 1px solid var(--border);
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                }
                .finding-title { font-size: 18px; font-weight: 600; color: var(--text-heading); }
                .badge {
                    padding: 4px 10px;
                    border-radius: 12px;
                    font-size: 12px;
                    font-weight: 600;
                    text-transform: uppercase;
                }
                .badge-medium { background: rgba(210, 153, 34, 0.2); color: var(--warning); border: 1px solid var(--warning); }
                .badge-info { background: rgba(88, 166, 255, 0.2); color: var(--accent); border: 1px solid var(--accent); }
                
                .finding-body { padding: 20px; }
                .summary { font-size: 15px; line-height: 1.6; margin-bottom: 20px; }

                .remediation-box {
                    background: rgba(63, 185, 80, 0.1);
                    border: 1px solid var(--success);
                    border-radius: 8px;
                    padding: 16px;
                    margin-top: 16px;
                }
                .remediation-box h4 { margin: 0 0 8px 0; color: var(--success); }

                pre {
                    background: #090d11;
                    border: 1px solid var(--border);
                    border-radius: 8px;
                    padding: 16px;
                    overflow-x: auto;
                    font-family: 'Fira Code', monospace;
                    font-size: 13px;
                }
            </style>
        </head>
        <body>
            <div class="container">
                <div class="header">
                    <h1>🔍 OSINT Intelligence Dashboard</h1>
                    <div class="meta-grid">
                        <div class="meta-card">
                            <div class="label">Target Value</div>
                            <div class="val">{{session.Target?.Value}}</div>
                        </div>
                        <div class="meta-card">
                            <div class="label">Target Type</div>
                            <div class="val">{{session.Target?.Type}}</div>
                        </div>
                        <div class="meta-card">
                            <div class="label">Total Findings</div>
                            <div class="val">{{session.Results.Count}}</div>
                        </div>
                        <div class="meta-card">
                            <div class="label">Scan Status</div>
                            <div class="val" style="color: var(--success)">{{session.Status}}</div>
                        </div>
                    </div>
                </div>

                <h2>Intelligence Findings</h2>
                <div id="findings-container"></div>
            </div>

            <script>
                const sessionData = {{sessionJson}};
                const container = document.getElementById('findings-container');

                if (!sessionData.Results || sessionData.Results.length === 0) {
                    container.innerHTML = '<p>No findings recorded in this scan session.</p>';
                } else {
                    sessionData.Results.forEach(result => {
                        const card = document.createElement('div');
                        card.className = 'finding-card';

                        let parsedData = {};
                        try { parsedData = JSON.parse(result.RawDataJson); } catch(e){}

                        let remediationHtml = '';
                        if (parsedData.BreachesCount && parsedData.BreachesCount > 0) {
                            remediationHtml = `
                                <div class="remediation-box">
                                    <h4>🛡️ Recommended Defense Guidance</h4>
                                    <ul>
                                        <li>Immediately change passwords on accounts associated with this email address.</li>
                                        <li>Enable Multi-Factor Authentication (MFA / 2FA) across all services.</li>
                                        <li>Use an isolated, unique password generator/manager for every service.</li>
                                    </ul>
                                </div>
                            `;
                        }

                        card.innerHTML = `
                            <div class="finding-header">
                                <div class="finding-title">${result.Title}</div>
                                <span class="badge badge-${(result.Severity || 'info').toLowerCase()}">${result.Severity}</span>
                            </div>
                            <div class="finding-body">
                                <div class="summary">${result.Summary}</div>
                                ${remediationHtml}
                                <h4>Structured Finding Metadata</h4>
                                <pre><code>${JSON.stringify(parsedData, null, 2)}</code></pre>
                            </div>
                        `;
                        container.appendChild(card);
                    });
                }
            </script>
        </body>
        </html>
        """;

        await File.WriteAllTextAsync(filePath, htmlContent);
    }
}

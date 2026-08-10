using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            Results = session.Results.Select(result =>
            {
                var rawData = ParseRawData(result.RawDataJson);
                var breachMetadata = ExtractBreachMetadata(rawData);
                return new
                {
                    ResultId = result.Id,
                    SessionId = result.ScanSessionId,
                    ModuleName = result.ModuleName,
                    Title = result.Title,
                    Summary = result.Summary,
                    Severity = result.Severity.ToString(),
                    Timestamp = result.Timestamp,
                    RawData = rawData,
                    BreachesCount = breachMetadata.BreachesCount,
                    BreachSummary = breachMetadata.BreachSummary,
                    BreachSource = breachMetadata.BreachSource,
                    BreachDetails = breachMetadata.BreachDetails
                };
            })
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
        sb.AppendLine("ResultId,SessionId,Target,ModuleName,Title,Severity,Summary,Timestamp,BreachesCount,BreachSummary,BreachSource,BreachDetails");

        foreach (var r in session.Results)
        {
            var targetValue = session.Target?.Value.Replace("\"", "\"\"") ?? "";
            var title = r.Title.Replace("\"", "\"").Replace("\n", " ");
            var summary = r.Summary.Replace("\"", "\"\"").Replace("\n", " ");
            var rawData = ParseRawData(r.RawDataJson);
            var breachMetadata = ExtractBreachMetadata(rawData);
            var breachSummary = (breachMetadata.BreachSummary ?? string.Empty).Replace("\"", "\"\"").Replace("\n", " ");
            var breachDetails = JsonSerializer.Serialize(breachMetadata.BreachDetails ?? new object[0]).Replace("\"", "\"\"");

            sb.AppendLine($"{r.Id},{session.Id},\"{targetValue}\",\"{r.ModuleName}\",\"{title}\",\"{r.Severity}\",\"{summary}\",\"{r.Timestamp:o}\",\"{breachMetadata.BreachesCount ?? 0}\",\"{breachSummary}\",\"{breachMetadata.BreachSource ?? string.Empty}\",\"{breachDetails}\"");
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
                var rawData = ParseRawData(r.RawDataJson);
                var breachMetadata = ExtractBreachMetadata(rawData);

                sb.AppendLine($"### [{r.Severity}] {r.Title}");
                sb.AppendLine($"* **Module:** `{r.ModuleName}`");
                sb.AppendLine($"* **Timestamp:** {r.Timestamp:yyyy-MM-dd HH:mm:ss UTC}");
                sb.AppendLine($"* **Summary:** {r.Summary}");
                if (breachMetadata.BreachesCount > 0)
                {
                    sb.AppendLine($"* **Breaches Count:** {breachMetadata.BreachesCount}");
                    sb.AppendLine($"* **Breach Summary:** {breachMetadata.BreachSummary}");
                    sb.AppendLine($"* **Breach Source:** {breachMetadata.BreachSource}");
                }
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
                .structured-data { display: grid; gap: 16px; margin-bottom: 20px; }
                .detail-section {
                    background: rgba(255,255,255,0.03);
                    border: 1px solid var(--border);
                    border-radius: 8px;
                    padding: 16px;
                }
                .detail-section h4 { margin: 0 0 12px 0; color: var(--text-heading); }
                .detail-grid {
                    display: grid;
                    grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
                    gap: 12px;
                }
                .detail-item {
                    background: rgba(255,255,255,0.04);
                    border: 1px solid var(--border);
                    border-radius: 8px;
                    padding: 12px;
                }
                .detail-label {
                    font-size: 12px;
                    color: #8b949e;
                    text-transform: uppercase;
                    letter-spacing: 0.04em;
                    margin-bottom: 6px;
                }
                .detail-value { font-size: 14px; color: var(--text-heading); line-height: 1.5; }
                .list { margin: 0; padding-left: 18px; }
                .list li { margin-bottom: 4px; }
                .pill {
                    display: inline-block;
                    padding: 4px 10px;
                    border-radius: 999px;
                    font-size: 12px;
                    font-weight: 600;
                    background: rgba(88, 166, 255, 0.16);
                    color: var(--accent);
                    border: 1px solid var(--accent);
                }
                .pill.success { background: rgba(63, 185, 80, 0.16); color: var(--success); border-color: var(--success); }
                .pill.danger { background: rgba(248, 81, 73, 0.16); color: var(--danger); border-color: var(--danger); }
                .pill.info { background: rgba(210, 153, 34, 0.16); color: var(--warning); border-color: var(--warning); }
                .table-wrap { overflow-x: auto; }
                .data-table { width: 100%; border-collapse: collapse; }
                .data-table th, .data-table td { text-align: left; padding: 10px 12px; border-bottom: 1px solid var(--border); }
                .data-table th { color: #8b949e; font-size: 12px; text-transform: uppercase; }
                .muted { color: #8b949e; }

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

                const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (char) => ({
                    '&': '&amp;',
                    '<': '&lt;',
                    '>': '&gt;',
                    '"': '&quot;',
                    "'": '&#39;'
                }[char]));

                const formatValue = (value) => {
                    if (value === null || value === undefined || value === '') return 'Not available';
                    if (typeof value === 'boolean') return value ? 'Yes' : 'No';
                    if (typeof value === 'number') return Number.isFinite(value) ? value.toLocaleString() : String(value);
                    return String(value);
                };

                const renderGenericObject = (data, title) => {
                    if (!data || typeof data !== 'object' || Array.isArray(data)) return '';
                    const entries = Object.entries(data).filter(([, value]) => value !== null && value !== undefined && value !== '');
                    if (!entries.length) return '';

                    return `
                        <div class="detail-section">
                            <h4>${escapeHtml(title)}</h4>
                            <div class="detail-grid">
                                ${entries.map(([key, value]) => `
                                    <div class="detail-item">
                                        <div class="detail-label">${escapeHtml(key)}</div>
                                        <div class="detail-value">${escapeHtml(formatValue(value))}</div>
                                    </div>
                                `).join('')}
                            </div>
                        </div>
                    `;
                };

                const normalizeStructuredData = (value) => {
                    if (!value) return {};
                    if (typeof value === 'string') {
                        try {
                            return JSON.parse(value);
                        } catch (e) {
                            return {};
                        }
                    }
                    return value;
                };

                const renderStructuredData = (data) => {
                    if (!data || typeof data !== 'object') return '';

                    const sections = [];

                    if (data.GeoLocation && typeof data.GeoLocation === 'object') {
                        const geo = data.GeoLocation;
                        const items = [
                            ['IP', geo.Ip],
                            ['Country', geo.Country],
                            ['Country Code', geo.CountryCode],
                            ['Region', geo.Region],
                            ['City', geo.City],
                            ['Latitude', geo.Latitude],
                            ['Longitude', geo.Longitude],
                            ['ISP', geo.Isp],
                            ['ASN', geo.Asn],
                            ['Organization', geo.Organization]
                        ].filter(([, value]) => value !== null && value !== undefined && value !== '');

                        if (items.length) {
                            sections.push(`
                                <div class="detail-section">
                                    <h4>Geo Location</h4>
                                    <div class="detail-grid">
                                        ${items.map(([label, value]) => `
                                            <div class="detail-item">
                                                <div class="detail-label">${escapeHtml(label)}</div>
                                                <div class="detail-value">${escapeHtml(formatValue(value))}</div>
                                            </div>
                                        `).join('')}
                                    </div>
                                </div>
                            `);
                        }
                    }

                    if (data.ExposedServices && Array.isArray(data.ExposedServices) && data.ExposedServices.length) {
                        sections.push(`
                            <div class="detail-section">
                                <h4>Exposed Services</h4>
                                <div class="table-wrap">
                                    <table class="data-table">
                                        <thead>
                                            <tr><th>Port</th><th>Service</th><th>State</th></tr>
                                        </thead>
                                        <tbody>
                                            ${data.ExposedServices.map(service => `
                                                <tr>
                                                    <td>${escapeHtml(service.Port ?? '—')}</td>
                                                    <td>${escapeHtml(service.Service ?? '—')}</td>
                                                    <td><span class="pill ${String(service.State || '').toLowerCase() === 'open' ? 'success' : 'info'}">${escapeHtml(service.State ?? 'Unknown')}</span></td>
                                                </tr>
                                            `).join('')}
                                        </tbody>
                                    </table>
                                </div>
                            </div>
                        `);
                    }

                    if (data.BreachDetails && Array.isArray(data.BreachDetails)) {
                        sections.push(`
                            <div class="detail-section">
                                <h4>Breach Exposure</h4>
                                ${data.BreachDetails.length
                                    ? `<div class="detail-grid">${data.BreachDetails.map(breach => {
                                        const title = breach.Name || breach.Title || breach.Breach || 'Unknown';
                                        const date = breach.BreachDate || breach.Date || breach.XposedDate || 'Unknown';
                                        const domain = breach.Domain || breach.Source || 'Unknown';
                                        const count = breach.PwnCount ?? breach.RecordCount ?? breach.Count;
                                        const description = breach.Description || breach.Details || 'Not listed';
                                        return `
                                            <div class="detail-item">
                                                <div class="detail-label">${escapeHtml(title)}</div>
                                                <div class="detail-value">
                                                    <div><strong>Domain:</strong> ${escapeHtml(domain)}</div>
                                                    <div><strong>Date:</strong> ${escapeHtml(date)}</div>
                                                    ${count !== undefined && count !== null ? `<div><strong>Records:</strong> ${escapeHtml(formatValue(count))}</div>` : ''}
                                                    <div>${escapeHtml(formatValue(description))}</div>
                                                </div>
                                            </div>`;
                                    }).join('')}</div>`
                                    : `<div class="detail-value">${escapeHtml(data.BreachSummary || 'No public breach records were discovered in the current offline analysis.')}</div>`}
                                ${data.BreachSummary ? `<div class="detail-value" style="margin-top: 8px;"><strong>Summary:</strong> ${escapeHtml(data.BreachSummary)}</div>` : ''}
                                ${data.BreachSource ? `<div class="detail-value" style="margin-top: 8px;"><strong>Source:</strong> ${escapeHtml(data.BreachSource)}</div>` : ''}
                            </div>
                        `);
                    }
                    else if (data.BreachesCount !== undefined || data.BreachSummary || data.BreachSource) {
                        sections.push(`
                            <div class="detail-section">
                                <h4>Breach Exposure</h4>
                                <div class="detail-grid">
                                    ${data.BreachesCount !== undefined ? `<div class="detail-item"><div class="detail-label">Count</div><div class="detail-value">${escapeHtml(formatValue(data.BreachesCount))}</div></div>` : ''}
                                    ${data.BreachSummary ? `<div class="detail-item"><div class="detail-label">Summary</div><div class="detail-value">${escapeHtml(data.BreachSummary)}</div></div>` : ''}
                                    ${data.BreachSource ? `<div class="detail-item"><div class="detail-label">Source</div><div class="detail-value">${escapeHtml(data.BreachSource)}</div></div>` : ''}
                                </div>
                            </div>
                        `);
                    }

                    if (data.DnsRecords && typeof data.DnsRecords === 'object') {
                        const dnsEntries = Object.entries(data.DnsRecords).filter(([, value]) => Array.isArray(value) && value.length);
                        if (dnsEntries.length) {
                            sections.push(`
                                <div class="detail-section">
                                    <h4>DNS Records</h4>
                                    <div class="detail-grid">
                                        ${dnsEntries.map(([recordType, values]) => `
                                            <div class="detail-item">
                                                <div class="detail-label">${escapeHtml(recordType)}</div>
                                                <div class="detail-value">
                                                    <ul class="list">
                                                        ${values.map(value => `<li>${escapeHtml(formatValue(value))}</li>`).join('')}
                                                    </ul>
                                                </div>
                                            </div>
                                        `).join('')}
                                    </div>
                                </div>
                            `);
                        }
                    }

                    if (data.WhoisInfo && typeof data.WhoisInfo === 'object') {
                        const whois = data.WhoisInfo;
                        const items = [
                            ['Registrar', whois.Registrar],
                            ['Created Date', whois.CreatedDate],
                            ['Expiry Date', whois.ExpiryDate],
                            ['Name Servers', whois.NameServers],
                            ['Privacy Enabled', whois.PrivacyEnabled]
                        ].filter(([, value]) => value !== null && value !== undefined && value !== '' && !(Array.isArray(value) && !value.length));

                        if (items.length) {
                            sections.push(`
                                <div class="detail-section">
                                    <h4>WHOIS / RDAP</h4>
                                    <div class="detail-grid">
                                        ${items.map(([label, value]) => `
                                            <div class="detail-item">
                                                <div class="detail-label">${escapeHtml(label)}</div>
                                                <div class="detail-value">
                                                    ${Array.isArray(value)
                                                        ? `<ul class="list">${value.map(item => `<li>${escapeHtml(formatValue(item))}</li>`).join('')}</ul>`
                                                        : escapeHtml(formatValue(value))}
                                                </div>
                                            </div>
                                        `).join('')}
                                    </div>
                                </div>
                            `);
                        }
                    }

                    if (data.SslValid !== undefined || data.SslIssuer) {
                        sections.push(`
                            <div class="detail-section">
                                <h4>SSL Certificate</h4>
                                <div class="detail-grid">
                                    <div class="detail-item">
                                        <div class="detail-label">Valid</div>
                                        <div class="detail-value"><span class="pill ${data.SslValid ? 'success' : 'danger'}">${data.SslValid ? 'Yes' : 'No'}</span></div>
                                    </div>
                                    <div class="detail-item">
                                        <div class="detail-label">Issuer</div>
                                        <div class="detail-value">${escapeHtml(data.SslIssuer || 'Not available')}</div>
                                    </div>
                                </div>
                            </div>
                        `);
                    }

                    const otherEntries = Object.entries(data).filter(([key]) => !['GeoLocation', 'ExposedServices', 'DnsRecords', 'WhoisInfo', 'SslValid', 'SslIssuer', 'BreachDetails', 'BreachSummary', 'LookupStatus'].includes(key));
                    if (otherEntries.length) {
                        sections.push(renderGenericObject(Object.fromEntries(otherEntries), 'Additional Context'));
                    }

                    return sections.join('');
                };

                if (!sessionData.Results || sessionData.Results.length === 0) {
                    container.innerHTML = '<p>No findings recorded in this scan session.</p>';
                } else {
                    sessionData.Results.forEach(result => {
                        const card = document.createElement('div');
                        card.className = 'finding-card';

                        let parsedData = {};
                        try {
                            parsedData = JSON.parse(result.RawDataJson);
                        } catch (e) {
                            try {
                                parsedData = normalizeStructuredData(result.RawDataJson);
                            } catch (err) {
                                parsedData = {};
                            }
                        }

                        const normalizedData = normalizeStructuredData(parsedData);
                        const structuredHtml = renderStructuredData(normalizedData);

                        card.innerHTML = `
                            <div class="finding-header">
                                <div class="finding-title">${escapeHtml(result.Title)}</div>
                                <span class="badge badge-${(result.Severity || 'info').toLowerCase()}">${escapeHtml(result.Severity)}</span>
                            </div>
                            <div class="finding-body">
                                <div class="summary">${escapeHtml(result.Summary)}</div>
                                ${structuredHtml ? `<div class="structured-data">${structuredHtml}</div>` : ''}
                                ${normalizedData.BreachDetails || normalizedData.BreachesCount !== undefined || normalizedData.BreachSummary || normalizedData.BreachSource ? `<div class="detail-section"><h4>Breach Exposure</h4><div class="detail-grid">${normalizedData.BreachesCount !== undefined ? `<div class="detail-item"><div class="detail-label">Count</div><div class="detail-value">${escapeHtml(formatValue(normalizedData.BreachesCount))}</div></div>` : ''}${normalizedData.BreachSummary ? `<div class="detail-item"><div class="detail-label">Summary</div><div class="detail-value">${escapeHtml(normalizedData.BreachSummary)}</div></div>` : ''}${normalizedData.BreachSource ? `<div class="detail-item"><div class="detail-label">Source</div><div class="detail-value">${escapeHtml(normalizedData.BreachSource)}</div></div>` : ''}</div></div>` : ''}
                                <h4>Raw Payload</h4>
                                <pre><code>${escapeHtml(JSON.stringify(parsedData, null, 2))}</code></pre>
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

    private static object? ParseRawData(string rawDataJson)
    {
        if (string.IsNullOrWhiteSpace(rawDataJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(rawDataJson);
            return ConvertJsonElement(doc.RootElement);
        }
        catch
        {
            return rawDataJson;
        }
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(property => property.Name, property => ConvertJsonElement(property.Value)),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt32(out var intValue) => intValue,
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }

    private static BreachMetadata ExtractBreachMetadata(object? rawData)
    {
        if (rawData is Dictionary<string, object?> values)
        {
            var breachesCount = ExtractInt(values, "BreachesCount", "breachesCount", "count") ?? 0;
            var breachSummary = ExtractString(values, "BreachSummary", "breachSummary", "summary") ?? ExtractString(values, "Summary", "summary");
            var breachSource = ExtractString(values, "BreachSource", "breachSource", "source");
            var breachDetails = ExtractObject(values, "BreachDetails", "breachDetails", "details", "ExposedBreaches");
            return new BreachMetadata(breachesCount, breachSummary, breachSource, breachDetails);
        }

        if (rawData is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var breachesCount = ExtractInt(element, "BreachesCount", "breachesCount", "count") ?? 0;
                var breachSummary = ExtractString(element, "BreachSummary", "breachSummary", "summary") ?? ExtractString(element, "Summary", "summary");
                var breachSource = ExtractString(element, "BreachSource", "breachSource", "source");
                var breachDetails = ExtractObject(element, "BreachDetails", "breachDetails", "details", "ExposedBreaches");
                return new BreachMetadata(breachesCount, breachSummary, breachSource, breachDetails);
            }

            if (element.ValueKind == JsonValueKind.Array)
            {
                var items = element.EnumerateArray().Select(ConvertJsonElement).ToList();
                return new BreachMetadata(items.Count, null, null, items);
            }
        }

        return new BreachMetadata(0, null, null, null);
    }

    private static int? ExtractInt(IDictionary<string, object?> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) && value is not null)
            {
                if (value is int intValue)
                {
                    return intValue;
                }

                if (value is long longValue)
                {
                    return (int)longValue;
                }

                if (int.TryParse(value.ToString(), out var parsed))
                {
                    return parsed;
                }
            }
        }

        return null;
    }

    private static int? ExtractInt(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (element.TryGetProperty(key, out var property))
            {
                if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var intValue))
                {
                    return intValue;
                }

                if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out var parsed))
                {
                    return parsed;
                }
            }
        }

        return null;
    }

    private static string? ExtractString(IDictionary<string, object?> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) && value is not null)
            {
                return value.ToString();
            }
        }

        return null;
    }

    private static string? ExtractString(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (element.TryGetProperty(key, out var property) && property.ValueKind is JsonValueKind.String)
            {
                return property.GetString();
            }
        }

        return null;
    }

    private static object? ExtractObject(IDictionary<string, object?> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) && value is not null)
            {
                return value;
            }
        }

        return null;
    }

    private static object? ExtractObject(JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (element.TryGetProperty(key, out var property))
            {
                return ConvertJsonElement(property);
            }
        }

        return null;
    }

    private sealed class BreachMetadata
    {
        public BreachMetadata(int breachesCount, string? breachSummary, string? breachSource, object? breachDetails)
        {
            BreachesCount = breachesCount;
            BreachSummary = breachSummary;
            BreachSource = breachSource;
            BreachDetails = breachDetails;
        }

        public int? BreachesCount { get; }
        public string? BreachSummary { get; }
        public string? BreachSource { get; }
        public object? BreachDetails { get; }
    }
}

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DnsClient;
using OsintToolkit.Core.Enums;
using OsintToolkit.Core.Models;
using OsintToolkit.Modules.Base;

namespace OsintToolkit.Modules.Implementations;

/// <summary>
/// Analyzes email address validity, domain MX presence, disposable-domain flags, and breach exposure.
/// </summary>
public class EmailInfoModule : BaseOsintModule
{
    public override string Name => "EmailInfo";
    public override string Description => "Validates email format, verifies MX reachability, checks for disposable domain flag, and flags data breaches.";
    public override string Category => "Identity Recon";
    public override TargetType[] SupportedTypes => new[] { TargetType.Email };

    protected override async Task<ModuleResult> ExecuteInternalAsync(string targetValue, TargetType targetType, CancellationToken cancellationToken)
    {
        await Task.Delay(250, cancellationToken);

        var isFormatValid = !string.IsNullOrWhiteSpace(targetValue) && targetValue.Contains('@') && targetValue.IndexOf('@') > 0 && targetValue.LastIndexOf('@') == targetValue.IndexOf('@') && !targetValue.EndsWith("@", StringComparison.Ordinal);
        var parts = targetValue.Split('@');
        var username = parts.Length > 0 ? parts[0] : string.Empty;
        var domain = parts.Length > 1 ? parts[1] : "unknown.com";

        var disposableDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mailinator.com",
            "10minutemail.com",
            "tempmail.com",
            "guerrillamail.com",
            "yopmail.com",
            "trashmail.com"
        };
        var isDisposable = disposableDomains.Contains(domain);

        bool hasMxRecords = false;
        try
        {
            var lookup = new LookupClient(new LookupClientOptions { Timeout = TimeSpan.FromSeconds(3) });
            var mx = await lookup.QueryAsync(domain, QueryType.MX);
            foreach (var _ in mx.Answers.MxRecords())
            {
                hasMxRecords = true;
                break;
            }
        }
        catch
        {
            hasMxRecords = false;
        }

        var breachResults = await LookupBreachesAsync(targetValue, cancellationToken);
        var breachSummary = "No breach-related results were found in online services.";
        var breachSource = "None";

        if (breachResults.Count > 0)
        {
            breachSummary = $"Online breach lookup found {breachResults.Count} breach record(s) for this email.";
            breachSource = "XposedOrNot";
        }
        else
        {
            var onlineSearchResults = new List<object>();
            var onlineQueries = new[]
            {
                $"{targetValue} breach",
                $"{targetValue} pwned",
                $"{domain} breach"
            };

            foreach (var query in onlineQueries)
            {
                try
                {
                    var matches = await SearchWebAsync(query, cancellationToken);
                    foreach (var match in matches)
                    {
                        onlineSearchResults.Add(match);
                    }
                }
                catch
                {
                    // ignore search failures and continue
                }
            }

            var breachRelated = onlineSearchResults.Any(result =>
            {
                var text = $"{result.GetType().GetProperty("Title")?.GetValue(result)} {result.GetType().GetProperty("Snippet")?.GetValue(result)}";
                return text.IndexOf("breach", StringComparison.OrdinalIgnoreCase) >= 0
                    || text.IndexOf("pwned", StringComparison.OrdinalIgnoreCase) >= 0
                    || text.IndexOf("leak", StringComparison.OrdinalIgnoreCase) >= 0
                    || text.IndexOf("comprom", StringComparison.OrdinalIgnoreCase) >= 0;
            });

            if (breachRelated)
            {
                breachSummary = "Online search results indicate possible breach exposure for this email.";
                breachSource = "DuckDuckGo";
            }
        }

        var breachCount = breachResults.Count;
        var severity = breachCount switch
        {
            <= 0 => ResultSeverity.Info,
            1 => ResultSeverity.Medium,
            2 or 3 => ResultSeverity.High,
            _ => ResultSeverity.Critical
        };

        var summary = $"Validated email '{targetValue}'. MX records {(hasMxRecords ? "were found" : "were not found")}; domain {(isDisposable ? "is flagged as disposable" : "is not flagged as disposable")}; {breachSummary}";

        return ModuleResult.Success(
            Name,
            $"Email Identity Analysis: {targetValue}",
            summary,
            new
            {
                Email = targetValue,
                Username = username,
                Domain = domain,
                IsFormatValid = isFormatValid,
                IsDisposable = isDisposable,
                HasMxRecords = hasMxRecords,
                BreachesCount = breachCount,
                BreachSummary = breachSummary,
                BreachDetails = breachResults.Take(5),
                BreachSource = breachSource,
                OnlineSearchSource = "DuckDuckGo"
            },
            severity
        );
    }

    private static async Task<List<object>> LookupBreachesAsync(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || email.IndexOf('@') <= 0 || email.LastIndexOf('@') != email.IndexOf('@'))
        {
            return new List<object>();
        }

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/json");

            var checkUrl = $"https://api.xposedornot.com/v1/check-email/{Uri.EscapeDataString(email)}?details=true";
            using var checkResponse = await http.GetAsync(checkUrl, cancellationToken);
            if (!checkResponse.IsSuccessStatusCode)
            {
                return new List<object>();
            }

            var checkContent = await checkResponse.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(checkContent))
            {
                return new List<object>();
            }

            using var checkDocument = JsonDocument.Parse(checkContent);
            if (checkDocument.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new List<object>();
            }

            if (!checkDocument.RootElement.TryGetProperty("status", out var statusProperty) ||
                !string.Equals(statusProperty.GetString(), "success", StringComparison.OrdinalIgnoreCase))
            {
                return new List<object>();
            }

            if (!checkDocument.RootElement.TryGetProperty("breaches", out var breachesProperty) ||
                breachesProperty.ValueKind != JsonValueKind.Array ||
                breachesProperty.GetArrayLength() == 0)
            {
                return new List<object>();
            }

            var breachNames = new List<string>();
            foreach (var breachEntry in breachesProperty.EnumerateArray())
            {
                if (breachEntry.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in breachEntry.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                        {
                            breachNames.Add(item.GetString()!);
                        }
                    }
                }
                else if (breachEntry.ValueKind == JsonValueKind.String)
                {
                    breachNames.Add(breachEntry.GetString()!);
                }
            }

            var uniqueBreaches = breachNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (uniqueBreaches.Count == 0)
            {
                return new List<object>();
            }

            var analyticsUrl = $"https://api.xposedornot.com/v1/breach-analytics?email={Uri.EscapeDataString(email)}";
            using var analyticsResponse = await http.GetAsync(analyticsUrl, cancellationToken);
            if (!analyticsResponse.IsSuccessStatusCode)
            {
                return uniqueBreaches.Select(name => new { Name = name, Domain = (string?)null, BreachDate = (string?)null, PwnCount = (int?)null, Description = (string?)null }).Cast<object>().ToList();
            }

            var analyticsContent = await analyticsResponse.Content.ReadAsStringAsync(cancellationToken);
            using var analyticsDocument = JsonDocument.Parse(analyticsContent);
            var results = new List<object>();

            if (analyticsDocument.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (analyticsDocument.RootElement.TryGetProperty("ExposedBreaches", out var exposedBreachesProperty) &&
                    exposedBreachesProperty.ValueKind == JsonValueKind.Object &&
                    exposedBreachesProperty.TryGetProperty("breaches_details", out var breachesDetailsProperty) &&
                    breachesDetailsProperty.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in breachesDetailsProperty.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        var breachName = item.TryGetProperty("breach", out var breachNameProperty) ? breachNameProperty.GetString() : null;
                        var domain = item.TryGetProperty("domain", out var domainProperty) ? domainProperty.GetString() : null;
                        var details = item.TryGetProperty("details", out var detailsProperty) ? detailsProperty.GetString() : null;
                        var xposedDate = item.TryGetProperty("xposed_date", out var xposedDateProperty) ? xposedDateProperty.GetString() : null;
                        var recordCount = item.TryGetProperty("xposed_records", out var recordsProperty) && recordsProperty.ValueKind == JsonValueKind.Number ? recordsProperty.GetInt32() : (int?)null;

                        if (!string.IsNullOrWhiteSpace(breachName))
                        {
                            results.Add(new
                            {
                                Name = breachName,
                                Domain = domain,
                                BreachDate = xposedDate,
                                PwnCount = recordCount,
                                Description = details
                            });
                        }
                    }
                }
            }

            if (results.Count == 0)
            {
                return uniqueBreaches.Select(name => new { Name = name, Domain = (string?)null, BreachDate = (string?)null, PwnCount = (int?)null, Description = (string?)null }).Cast<object>().ToList();
            }

            return results;
        }
        catch
        {
            return new List<object>();
        }
    }

    private static async Task<List<object>> SearchWebAsync(string query, CancellationToken cancellationToken)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");

        var url = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
        var response = await http.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new List<object>();
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var results = new List<object>();
        var titleMatches = Regex.Matches(html, @"result__a[^>]*href=""(?<href>[^""]+)""[^>]*>(?<title>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var snippetMatches = Regex.Matches(html, @"result__snippet[^>]*>(?<snippet>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        for (var index = 0; index < Math.Min(titleMatches.Count, Math.Max(1, snippetMatches.Count)); index++)
        {
            var titleText = WebUtility.HtmlDecode(Regex.Replace(titleMatches[index].Groups["title"].Value, "<.*?>", " ").Trim());
            var snippetText = WebUtility.HtmlDecode(Regex.Replace(index < snippetMatches.Count ? snippetMatches[index].Groups["snippet"].Value : string.Empty, "<.*?>", " ").Trim());
            if (string.IsNullOrWhiteSpace(titleText) && string.IsNullOrWhiteSpace(snippetText))
            {
                continue;
            }

            results.Add(new
            {
                Title = titleText,
                Snippet = snippetText,
                Url = titleMatches[index].Groups["href"].Value
            });
        }

        return results;
    }
}

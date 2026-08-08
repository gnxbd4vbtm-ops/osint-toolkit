using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;
using System.Text.RegularExpressions;
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

        var onlineSearchResults = new List<object>();
        var breachSummary = "No breach-related results were found in online searches.";
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
        }

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
                BreachesCount = onlineSearchResults.Count,
                BreachSummary = breachSummary,
                BreachDetails = onlineSearchResults.Take(5),
                OnlineSearchQueries = onlineQueries,
                OnlineSearchSource = "DuckDuckGo"
            },
            ResultSeverity.Medium
        );
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

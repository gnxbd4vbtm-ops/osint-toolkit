using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace OsintToolkit.Services.Utilities;

using OsintToolkit.Core.Enums;

public static class TargetValidator
{
    private static readonly HashSet<string> Tlds = LoadTlds();

    private static readonly Regex IpRegex = new(
        @"^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$",
        RegexOptions.Compiled);

    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled);

    private static readonly Regex DomainRegex = new(
        @"^(?:[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+([a-zA-Z]{2,})$",
        RegexOptions.Compiled);

    private static HashSet<string> LoadTlds()
    {
        const string fileName = "tlds-alpha-by-domain.txt";

        // Try several likely locations for the TLD file.
        var candidates = new List<string>();

        // App base (published output)
        candidates.Add(Path.Combine(AppContext.BaseDirectory, fileName));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "Utilities", fileName));

        // Current working directory
        candidates.Add(Path.Combine(Environment.CurrentDirectory, fileName));
        candidates.Add(Path.Combine(Environment.CurrentDirectory, "src", "OsintToolkit.Services", "Utilities", fileName));

        // Walk up from AppContext.BaseDirectory looking for the file in parent folders
        try
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 6 && dir != null; i++)
            {
                candidates.Add(Path.Combine(dir.FullName, fileName));
                candidates.Add(Path.Combine(dir.FullName, "Utilities", fileName));
                dir = dir.Parent;
            }
        }
        catch
        {
        }

        // Also walk up from current directory to find repository root and common source location
        try
        {
            var dir = new DirectoryInfo(Environment.CurrentDirectory);
            for (int i = 0; i < 8 && dir != null; i++)
            {
                // if we find solution file or README, consider this repo root
                var sln = Path.Combine(dir.FullName, "OsintToolkit.slnx");
                var readme = Path.Combine(dir.FullName, "README.md");
                if (File.Exists(sln) || File.Exists(readme))
                {
                    candidates.Add(Path.Combine(dir.FullName, "src", "OsintToolkit.Services", "Utilities", fileName));
                    break;
                }

                candidates.Add(Path.Combine(dir.FullName, fileName));
                candidates.Add(Path.Combine(dir.FullName, "Utilities", fileName));
                dir = dir.Parent;
            }
        }
        catch
        {
        }

        // Pick the first existing candidate
        var path = candidates.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p));

        if (string.IsNullOrEmpty(path))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return File.ReadAllLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !line.StartsWith("#"))
            .Select(line => line.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static TargetType DetectType(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return TargetType.Username;

        var trimmed = input.Trim();

        // IP address
        if (IpRegex.IsMatch(trimmed))
            return TargetType.IpAddress;

        // Email address
        if (EmailRegex.IsMatch(trimmed))
            return TargetType.Email;

        // Domain
        var domainMatch = DomainRegex.Match(trimmed);

        if (domainMatch.Success)
        {
            var tld = domainMatch.Groups[1].Value;

            if (Tlds.Contains(tld))
                return TargetType.Domain;
        }

        // Person
        if (trimmed.Contains(' '))
            return TargetType.Person;

        // Username
        return TargetType.Username;
    }
}

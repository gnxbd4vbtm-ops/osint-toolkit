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
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "tlds-alpha-by-domain.txt");

        if (!File.Exists(path))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

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

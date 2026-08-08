using System.Text.RegularExpressions;
using OsintToolkit.Core.Enums;

namespace OsintToolkit.Services.Utilities;

/// <summary>
/// Helper utility to validate and auto-detect target types based on string patterns.
/// </summary>
public static class TargetValidator
{
    private static readonly Regex IpRegex = new(@"^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private static readonly Regex DomainRegex = new(@"^(?:[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,}$", RegexOptions.Compiled);

    public static TargetType DetectType(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return TargetType.Username;

        var trimmed = input.Trim();

        if (IpRegex.IsMatch(trimmed)) return TargetType.IpAddress;
        if (EmailRegex.IsMatch(trimmed)) return TargetType.Email;
        if (DomainRegex.IsMatch(trimmed)) return TargetType.Domain;
        if (trimmed.Contains(" ")) return TargetType.Person;

        return TargetType.Username;
    }
}

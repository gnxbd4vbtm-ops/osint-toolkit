using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using OsintToolkit.Core.Enums;

namespace OsintToolkit.Core.Models;

/// <summary>
/// Represents an OSINT target (e.g. username, domain, IP, email, person).
/// </summary>
public class Target
{
    public int Id { get; set; }
    public string Value { get; set; } = string.Empty;
    public TargetType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastScannedAt { get; set; }

    // Navigation property
    [JsonIgnore]
    public List<ScanSession> ScanSessions { get; set; } = new();
}

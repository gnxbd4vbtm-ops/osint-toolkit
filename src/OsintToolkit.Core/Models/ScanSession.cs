using System;
using System.Collections.Generic;
using OsintToolkit.Core.Enums;

namespace OsintToolkit.Core.Models;

/// <summary>
/// Tracks an individual scan session performed against a specific target.
/// </summary>
public class ScanSession
{
    public int Id { get; set; }
    public int TargetId { get; set; }
    public Target? Target { get; set; }
    public ScanStatus Status { get; set; } = ScanStatus.Pending;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string Notes { get; set; } = string.Empty;

    // Findings generated during this scan
    public List<ScanResult> Results { get; set; } = new();
}

using System.Threading;
using System.Threading.Tasks;
using OsintToolkit.Core.Enums;
using OsintToolkit.Core.Models;

namespace OsintToolkit.Core.Interfaces;

/// <summary>
/// Contract that all OSINT plugins/modules must implement.
/// </summary>
public interface IOsintModule
{
    /// <summary>
    /// Unique identifier / name of the module.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human-readable description of what this module gathers.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Category or tag (e.g. Domain Recon, User Search, Email Validation).
    /// </summary>
    string Category { get; }

    /// <summary>
    /// Target types supported by this module.
    /// </summary>
    TargetType[] SupportedTypes { get; }

    /// <summary>
    /// Executes the module against the given target value and type.
    /// </summary>
    Task<ModuleResult> ExecuteAsync(string targetValue, TargetType targetType, CancellationToken cancellationToken = default);
}

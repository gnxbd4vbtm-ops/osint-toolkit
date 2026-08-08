using System;
using System.Threading;
using System.Threading.Tasks;
using OsintToolkit.Core.Enums;
using OsintToolkit.Core.Interfaces;
using OsintToolkit.Core.Models;

namespace OsintToolkit.Modules.Base;

/// <summary>
/// Abstract base class for all OSINT modules providing exception handling and logging.
/// </summary>
public abstract class BaseOsintModule : IOsintModule
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string Category { get; }
    public abstract TargetType[] SupportedTypes { get; }

    public async Task<ModuleResult> ExecuteAsync(string targetValue, TargetType targetType, CancellationToken cancellationToken = default)
    {
        if (Array.IndexOf(SupportedTypes, targetType) < 0)
        {
            return ModuleResult.Failure(Name, $"Module '{Name}' does not support target type '{targetType}'. Supported: {string.Join(", ", SupportedTypes)}");
        }

        try
        {
            return await ExecuteInternalAsync(targetValue, targetType, cancellationToken);
        }
        catch (Exception ex)
        {
            return ModuleResult.Failure(Name, $"Error executing module '{Name}': {ex.Message}");
        }
    }

    /// <summary>
    /// Derived modules implement their gathering logic here.
    /// </summary>
    protected abstract Task<ModuleResult> ExecuteInternalAsync(string targetValue, TargetType targetType, CancellationToken cancellationToken);
}

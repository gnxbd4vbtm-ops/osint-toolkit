using System.Collections.Generic;
using System.Threading.Tasks;
using OsintToolkit.Core.Enums;
using OsintToolkit.Core.Models;

namespace OsintToolkit.Core.Interfaces;

/// <summary>
/// Service for managing target entities.
/// </summary>
public interface ITargetService
{
    Task<Target> CreateTargetAsync(string value, TargetType type, string description = "");
    Task<Target?> GetByIdAsync(int id);
    Task<Target?> GetByValueAsync(string value);
    Task<List<Target>> GetAllTargetsAsync();
    Task<bool> DeleteTargetAsync(int id);
    TargetType DetectTargetType(string input);
}

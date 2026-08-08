using System.Threading.Tasks;
using OsintToolkit.Core.Models;

namespace OsintToolkit.Core.Interfaces;

/// <summary>
/// Service for managing persistent application configuration.
/// </summary>
public interface IConfigService
{
    AppConfig Config { get; }
    Task LoadConfigAsync();
    Task SaveConfigAsync();
    void UpdateConfig(AppConfig config);
}

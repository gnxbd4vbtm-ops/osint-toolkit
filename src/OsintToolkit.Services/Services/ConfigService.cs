using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OsintToolkit.Core.Interfaces;
using OsintToolkit.Core.Models;

namespace OsintToolkit.Services.Services;

/// <summary>
/// Manages loading, updating, and persisting configuration settings.
/// </summary>
public class ConfigService : IConfigService
{
    private readonly string _configFilePath;
    private readonly ILogger<ConfigService> _logger;
    private AppConfig _config;

    public AppConfig Config => _config;

    public ConfigService(ILogger<ConfigService> logger, string configFilePath = "config.json")
    {
        _logger = logger;
        _configFilePath = configFilePath;
        _config = new AppConfig();
    }

    public async Task LoadConfigAsync()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = await File.ReadAllTextAsync(_configFilePath);
                var loaded = JsonSerializer.Deserialize<AppConfig>(json);
                if (loaded != null)
                {
                    _config = loaded;
                    _logger.LogInformation("Loaded configuration from {ConfigPath}", _configFilePath);
                    return;
                }
            }

            _logger.LogInformation("No existing config file found at {ConfigPath}. Creating default config.", _configFilePath);
            await SaveConfigAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading config file {ConfigPath}. Reverting to defaults.", _configFilePath);
            _config = new AppConfig();
        }
    }

    public async Task SaveConfigAsync()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_config, options);
            await File.WriteAllTextAsync(_configFilePath, json);
            _logger.LogInformation("Saved configuration to {ConfigPath}", _configFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving config to {ConfigPath}", _configFilePath);
        }
    }

    public void UpdateConfig(AppConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using OsintToolkit.Core.Enums;
using OsintToolkit.Core.Interfaces;

namespace OsintToolkit.Services.Services;

/// <summary>
/// Central registry for managing available OSINT modules.
/// </summary>
public class ModuleRegistry : IModuleRegistry
{
    private readonly Dictionary<string, IOsintModule> _modules = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<ModuleRegistry> _logger;

    public ModuleRegistry(ILogger<ModuleRegistry> logger, IEnumerable<IOsintModule> initialModules)
    {
        _logger = logger;
        foreach (var module in initialModules)
        {
            RegisterModule(module);
        }
    }

    public void RegisterModule(IOsintModule module)
    {
        if (module == null) throw new ArgumentNullException(nameof(module));

        if (!_modules.ContainsKey(module.Name))
        {
            _modules[module.Name] = module;
            _logger.LogInformation("Registered OSINT Module: '{ModuleName}' ({Category})", module.Name, module.Category);
        }
    }

    public IEnumerable<IOsintModule> GetAllModules()
    {
        return _modules.Values;
    }

    public IEnumerable<IOsintModule> GetModulesForTargetType(TargetType targetType)
    {
        return _modules.Values.Where(m => m.SupportedTypes.Contains(targetType));
    }

    public IOsintModule? GetModuleByName(string name)
    {
        _modules.TryGetValue(name, out var module);
        return module;
    }
}

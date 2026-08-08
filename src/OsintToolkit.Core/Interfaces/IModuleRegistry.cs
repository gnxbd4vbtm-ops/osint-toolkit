using System.Collections.Generic;
using OsintToolkit.Core.Enums;

namespace OsintToolkit.Core.Interfaces;

/// <summary>
/// Registry responsible for module registration and retrieval.
/// </summary>
public interface IModuleRegistry
{
    void RegisterModule(IOsintModule module);
    IEnumerable<IOsintModule> GetAllModules();
    IEnumerable<IOsintModule> GetModulesForTargetType(TargetType targetType);
    IOsintModule? GetModuleByName(string name);
}

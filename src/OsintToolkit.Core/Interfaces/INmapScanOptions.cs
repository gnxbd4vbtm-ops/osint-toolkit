using OsintToolkit.Core.Enums;

namespace OsintToolkit.Core.Interfaces;

/// <summary>Provides the selected Nmap profile for the current application session.</summary>
public interface INmapScanOptions
{
    NmapScanProfile Profile { get; set; }
}

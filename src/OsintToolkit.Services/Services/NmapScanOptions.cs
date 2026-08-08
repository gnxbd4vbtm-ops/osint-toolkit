using OsintToolkit.Core.Enums;
using OsintToolkit.Core.Interfaces;

namespace OsintToolkit.Services.Services;

public sealed class NmapScanOptions : INmapScanOptions
{
    public NmapScanProfile Profile { get; set; } = NmapScanProfile.Standard;
}

namespace OsintToolkit.Core.Models;

/// <summary>A host observed in the local ARP/neighbor table.</summary>
public sealed class LocalNetworkHost
{
    public string Address { get; init; } = string.Empty;
    public string MacAddress { get; init; } = string.Empty;
    public string Interface { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string? Hostname { get; init; }
}

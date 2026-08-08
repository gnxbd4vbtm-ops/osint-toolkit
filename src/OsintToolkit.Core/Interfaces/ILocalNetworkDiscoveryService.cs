using OsintToolkit.Core.Models;

namespace OsintToolkit.Core.Interfaces;

public interface ILocalNetworkDiscoveryService
{
    Task<IReadOnlyList<LocalNetworkHost>> GetArpHostsAsync(bool resolveHostnames, CancellationToken cancellationToken = default);
}

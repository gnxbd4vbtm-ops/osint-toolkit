using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using OsintToolkit.Core.Interfaces;
using OsintToolkit.Core.Models;

namespace OsintToolkit.Services.Services;

/// <summary>Reads Linux's local IPv4 neighbor table; it does not probe remote networks.</summary>
public sealed class LocalNetworkDiscoveryService : ILocalNetworkDiscoveryService
{
    public async Task<IReadOnlyList<LocalNetworkHost>> GetArpHostsAsync(bool resolveHostnames, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Local ARP discovery currently requires Linux and the iproute2 'ip' command.");
        var startInfo = new ProcessStartInfo { FileName = "ip", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        startInfo.ArgumentList.Add("-j"); startInfo.ArgumentList.Add("-4"); startInfo.ArgumentList.Add("neigh"); startInfo.ArgumentList.Add("show");
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the 'ip' command.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken); var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = await outputTask;
        if (process.ExitCode != 0) throw new InvalidOperationException((await errorTask).Trim());
        var entries = JsonSerializer.Deserialize<List<NeighborEntry>>(output) ?? [];
        var hosts = entries.Where(entry => IPAddress.TryParse(entry.dst, out var address) && address.AddressFamily == AddressFamily.InterNetwork)
            .Where(entry => !string.IsNullOrWhiteSpace(entry.lladdr) && !entry.state.Contains("FAILED", StringComparer.OrdinalIgnoreCase) && !entry.state.Contains("INCOMPLETE", StringComparer.OrdinalIgnoreCase))
            .Select(entry => new LocalNetworkHost { Address = entry.dst ?? string.Empty, MacAddress = entry.lladdr ?? string.Empty, Interface = entry.dev ?? string.Empty, State = string.Join(", ", entry.state) }).ToList();
        if (!resolveHostnames) return hosts;
        return await ResolveAsync(hosts, cancellationToken);
    }

    private static async Task<IReadOnlyList<LocalNetworkHost>> ResolveAsync(IEnumerable<LocalNetworkHost> hosts, CancellationToken cancellationToken)
    {
        var resolved = new List<LocalNetworkHost>();
        foreach (var host in hosts)
        {
            string? hostname = null;
            try { hostname = (await Dns.GetHostEntryAsync(host.Address, cancellationToken)).HostName; } catch (SocketException) { }
            resolved.Add(new LocalNetworkHost { Address = host.Address, MacAddress = host.MacAddress, Interface = host.Interface, State = host.State, Hostname = hostname });
        }
        return resolved;
    }

    private sealed class NeighborEntry { public string? dst { get; set; } public string? lladdr { get; set; } public string? dev { get; set; } public List<string> state { get; set; } = []; }
}

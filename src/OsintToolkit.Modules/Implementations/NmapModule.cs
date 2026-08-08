using System.Diagnostics;
using System.Xml.Linq;
using OsintToolkit.Core.Enums;
using OsintToolkit.Core.Interfaces;
using OsintToolkit.Core.Models;
using OsintToolkit.Modules.Base;

namespace OsintToolkit.Modules.Implementations;

/// <summary>Runs a locally installed Nmap with one of the built-in bounded profiles.</summary>
public sealed class NmapModule : BaseOsintModule
{
    private readonly INmapScanOptions _options;
    public NmapModule(INmapScanOptions options) => _options = options;
    public override string Name => "Nmap";
    public override string Description => "Runs an authorized local Nmap discovery or TCP service scan with structured output.";
    public override string Category => "Network Recon";
    public override TargetType[] SupportedTypes => new[] { TargetType.Domain, TargetType.IpAddress };

    protected override async Task<ModuleResult> ExecuteInternalAsync(string targetValue, TargetType targetType, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo { FileName = "nmap", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        AddProfileArguments(startInfo.ArgumentList, _options.Profile);
        startInfo.ArgumentList.Add("-oX"); startInfo.ArgumentList.Add("-"); startInfo.ArgumentList.Add("--"); startInfo.ArgumentList.Add(targetValue);
        try
        {
            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the local nmap executable.");
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var output = await outputTask; var error = await errorTask;
            if (process.ExitCode != 0) return ModuleResult.Failure(Name, $"Nmap exited with code {process.ExitCode}: {error.Trim()}");
            var hosts = ParseHosts(output);
            var openPorts = hosts.SelectMany(h => h.Ports).Where(p => p.State == "open").ToList();
            var summary = _options.Profile == NmapScanProfile.Discovery
                ? $"Nmap discovery completed: {hosts.Count} responsive host(s)."
                : $"Nmap {_options.Profile} scan completed: {hosts.Count} host(s), {openPorts.Count} open TCP port(s).";
            return ModuleResult.Success(Name, $"Nmap {_options.Profile} scan: {targetValue}", summary, new { Target = targetValue, Profile = _options.Profile.ToString(), Hosts = hosts, OpenPorts = openPorts, RawXml = output }, openPorts.Count > 0 ? ResultSeverity.Low : ResultSeverity.Info);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return ModuleResult.Failure(Name, "Nmap is not installed or is not available on PATH. Install the 'nmap' package and retry.");
        }
    }

    private static void AddProfileArguments(ICollection<string> args, NmapScanProfile profile)
    {
        switch (profile)
        {
            case NmapScanProfile.Discovery: args.Add("-sn"); args.Add("-T3"); break;
            case NmapScanProfile.Quick: args.Add("-sV"); args.Add("--top-ports"); args.Add("100"); args.Add("-T3"); break;
            case NmapScanProfile.FullTcp: args.Add("-sV"); args.Add("-p-"); args.Add("-T3"); break;
            default: args.Add("-sV"); args.Add("--top-ports"); args.Add("1000"); args.Add("-T3"); break;
        }
    }

    private static List<NmapHost> ParseHosts(string xml)
    {
        var document = XDocument.Parse(xml);
        return document.Descendants("host").Select(host => new NmapHost(
            host.Element("address")?.Attribute("addr")?.Value ?? "unknown",
            host.Element("status")?.Attribute("state")?.Value ?? "unknown",
            host.Descendants("port").Select(port => new NmapPort(
                int.TryParse(port.Attribute("portid")?.Value, out var id) ? id : 0,
                port.Attribute("protocol")?.Value ?? "tcp",
                port.Element("state")?.Attribute("state")?.Value ?? "unknown",
                port.Element("service")?.Attribute("name")?.Value ?? "unknown",
                port.Element("service")?.Attribute("product")?.Value ?? string.Empty,
                port.Element("service")?.Attribute("version")?.Value ?? string.Empty)).ToList())).ToList();
    }

    private sealed record NmapHost(string Address, string Status, List<NmapPort> Ports);
    private sealed record NmapPort(int Port, string Protocol, string State, string Service, string Product, string Version);
}

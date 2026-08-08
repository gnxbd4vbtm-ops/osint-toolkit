using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using DnsClient;
using System.Text.Json;
using OsintToolkit.Core.Enums;
using OsintToolkit.Core.Models;
using OsintToolkit.Modules.Base;

namespace OsintToolkit.Modules.Implementations;

/// <summary>
/// Placeholder module for gathering domain DNS records, registrar WHOIS, and SSL configuration.
/// </summary>
public class DomainInfoModule : BaseOsintModule
{
    public override string Name => "DomainInfo";
    public override string Description => "Gathers DNS records (A, MX, TXT, NS), WHOIS registrar metadata, and SSL certificate context.";
    public override string Category => "Domain Recon";
    public override TargetType[] SupportedTypes => new[] { TargetType.Domain };

    protected override async Task<ModuleResult> ExecuteInternalAsync(string targetValue, TargetType targetType, CancellationToken cancellationToken)
    {
        var dnsRecords = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var lookupOptions = new LookupClientOptions { Timeout = TimeSpan.FromSeconds(5) };
            var lookup = new LookupClient(lookupOptions);
            var a = await lookup.QueryAsync(targetValue, QueryType.A);
            dnsRecords["A"] = new List<string>();
            foreach (var r in a.Answers.ARecords()) dnsRecords["A"].Add(r.Address.ToString());

            var aaaa = await lookup.QueryAsync(targetValue, QueryType.AAAA);
            dnsRecords["AAAA"] = new List<string>();
            foreach (var r in aaaa.Answers.AaaaRecords()) dnsRecords["AAAA"].Add(r.Address.ToString());

            var mx = await lookup.QueryAsync(targetValue, QueryType.MX);
            dnsRecords["MX"] = new List<string>();
            foreach (var r in mx.Answers.MxRecords()) dnsRecords["MX"].Add($"{r.Preference} {r.Exchange}");

            var ns = await lookup.QueryAsync(targetValue, QueryType.NS);
            dnsRecords["NS"] = new List<string>();
            foreach (var r in ns.Answers.NsRecords()) dnsRecords["NS"].Add(r.NSDName.ToString());

            var txt = await lookup.QueryAsync(targetValue, QueryType.TXT);
            dnsRecords["TXT"] = new List<string>();
            foreach (var r in txt.Answers.TxtRecords()) dnsRecords["TXT"].Add(string.Join(" ", r.Text));
        }
        catch
        {
            // ignore DNS errors and leave whatever was collected
        }

        // WHOIS / RDAP lookup (best-effort via rdap.org)
        object whoisInfo = new { Registrar = (string?)null, CreatedDate = (string?)null, ExpiryDate = (string?)null, NameServers = Array.Empty<string>(), PrivacyEnabled = (bool?)null };
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
            var rdapUrl = $"https://rdap.org/domain/{Uri.EscapeDataString(targetValue)}";
            var resp = await http.GetAsync(rdapUrl, cancellationToken);
            if (resp.IsSuccessStatusCode)
            {
                var text = await resp.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                string? registrar = null; string? created = null; string? expires = null; var nameservers = new List<string>(); bool? privacy = null;
                if (root.TryGetProperty("events", out var events))
                {
                    foreach (var ev in events.EnumerateArray())
                    {
                        if (ev.TryGetProperty("eventAction", out var action) && action.GetString() is string a)
                        {
                            if (a.Equals("registration", StringComparison.OrdinalIgnoreCase) && ev.TryGetProperty("eventDate", out var d)) created ??= d.GetString();
                            if (a.Equals("expiration", StringComparison.OrdinalIgnoreCase) && ev.TryGetProperty("eventDate", out var e)) expires ??= e.GetString();
                        }
                    }
                }
                if (root.TryGetProperty("nameservers", out var nsElem))
                {
                    foreach (var n in nsElem.EnumerateArray()) if (n.TryGetProperty("ldhName", out var nn)) nameservers.Add(nn.GetString() ?? string.Empty);
                }
                if (root.TryGetProperty("registrar", out var reg)) registrar = reg.GetString();
                whoisInfo = new { Registrar = registrar, CreatedDate = created, ExpiryDate = expires, NameServers = nameservers.ToArray(), PrivacyEnabled = privacy };
            }
        }
        catch
        {
        }

        // SSL certificate check
        bool sslValid = false; string? sslIssuer = null;
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(targetValue, 443, cancellationToken);
            using var stream = tcp.GetStream();
            using var ssl = new System.Net.Security.SslStream(stream, false, (_, __, ___, ____) => true);
            var sslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                TargetHost = targetValue,
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
            };
            await ssl.AuthenticateAsClientAsync(sslOptions, cancellationToken);
            if (ssl.RemoteCertificate is not null)
            {
                var cert = new X509Certificate2(ssl.RemoteCertificate);
                sslIssuer = cert.Issuer;
                sslValid = DateTime.UtcNow >= cert.NotBefore.ToUniversalTime() && DateTime.UtcNow <= cert.NotAfter.ToUniversalTime();
            }
        }
        catch
        {
            // ignore SSL errors
        }

        var summary = $"Domain '{targetValue}' lookup completed. DNS A:{(dnsRecords.TryGetValue("A", out var aList) ? aList.Count : 0)} MX:{(dnsRecords.TryGetValue("MX", out var mList) ? mList.Count : 0)}";

        return ModuleResult.Success(
            Name,
            $"Domain Reconnaissance: {targetValue}",
            summary,
            new
            {
                Domain = targetValue,
                DnsRecords = dnsRecords,
                WhoisInfo = whoisInfo,
                SslValid = sslValid,
                SslIssuer = sslIssuer
            },
            ResultSeverity.Info
        );
    }
}

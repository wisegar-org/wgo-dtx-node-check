using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Wisegar.DTXInspector.Configuration;

namespace Wisegar.DTXInspector.Checks;

internal interface INetworkProbe
{
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellation);
    Task ConnectAsync(string host, int port, CancellationToken cancellation);
}

internal sealed class SystemNetworkProbe : INetworkProbe
{
    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellation) => Dns.GetHostAddressesAsync(host, cancellation);
    public async Task ConnectAsync(string host, int port, CancellationToken cancellation)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, cancellation);
    }
}

internal static class NetworkChecks
{
    internal static async Task<IReadOnlyList<CheckResult>> RunAsync(NodeProfile profile, INetworkProbe? probe = null, CancellationToken cancellation = default)
    {
        probe ??= new SystemNetworkProbe();
        var settings = profile.Infrastructure;
        var hosts = settings.PeerHostnames.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(settings.CoreHostname)) hosts = hosts.Prepend(settings.CoreHostname);
        using var concurrency = new SemaphoreSlim(4);
        var tasks = hosts.Distinct(StringComparer.OrdinalIgnoreCase).Select(host => RunLimited(() => DnsCheck(host)))
            .Concat(settings.Endpoints.Select(endpoint => RunLimited(() => TcpCheck(endpoint)))).ToArray();
        return await Task.WhenAll(tasks);

        async Task<CheckResult> RunLimited(Func<Task<CheckResult>> run)
        {
            await concurrency.WaitAsync(cancellation);
            try { return await run(); }
            finally { concurrency.Release(); }
        }

        async Task<CheckResult> DnsCheck(string host)
        {
            var details = new Dictionary<string, string?>
            {
                ["node"] = profile.Node.Key(), ["host"] = host, ["timeoutMs"] = settings.NetworkTimeoutMs.ToString(),
                ["checkId"] = host.Equals(settings.CoreHostname, StringComparison.OrdinalIgnoreCase) ? "core-dns" : "bidirectional-dns",
                ["scope"] = "Risolutore di sistema da questo PC; cache/hosts/fallback possibili, nessuna prova della direzione remota"
            };
            if (IPAddress.TryParse(host, out _))
                return CheckResult.Warning("network-dns", host, "Un IP letterale non verifica il DNS: configurare un nome.", details);
            try
            {
                var samples = new List<string[]>();
                var elapsed = new List<long>();
                for (var i = 0; i < 3; i++)
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                    timeout.CancelAfter(settings.NetworkTimeoutMs);
                    var watch = Stopwatch.StartNew();
                    var addresses = await probe.ResolveAsync(host, timeout.Token).WaitAsync(timeout.Token);
                    elapsed.Add(watch.ElapsedMilliseconds);
                    samples.Add(addresses.Select(ip => ip.ToString()).Distinct().Order().ToArray());
                }
                details["addresses"] = string.Join("; ", samples.Select(sample => string.Join(",", sample)));
                details["latencyMs"] = string.Join(", ", elapsed);
                var isCore = host.Equals(settings.CoreHostname, StringComparison.OrdinalIgnoreCase);
                var expected = settings.ExpectedCoreAddresses.Select(IPAddress.Parse).ToArray();
                var mismatch = isCore && expected.Length > 0 && samples.Any(sample => sample.Any(ip => !expected.Contains(IPAddress.Parse(ip))));
                if (samples.Any(sample => sample.Length == 0) || mismatch)
                    return CheckResult.Fail("network-dns", host, "Nome senza indirizzi o Core risolto su IP diverso dagli indirizzi attesi.", details);
                if (elapsed.Any(ms => ms > settings.DnsMaxLatencyMs) || samples.Skip(1).Any(s => !s.SequenceEqual(samples[0])))
                    return CheckResult.Warning("network-dns", host, "DNS lento o risposte variabili nei tre campioni. Verificare round-robin, cache e fallback.", details);
                return CheckResult.Pass("network-dns", host, "Tre risoluzioni riuscite entro soglia con risposte uguali. Prova puntuale; non certifica DNS interno, stabilita' nel tempo o bidirezionalita'.", details);
            }
            catch (Exception ex) when (!cancellation.IsCancellationRequested && (ex is SocketException or OperationCanceledException or ArgumentException))
            {
                return CheckResult.Fail("network-dns", host, $"Risoluzione fallita o timeout: {ex.Message}", details);
            }
        }

        async Task<CheckResult> TcpCheck(NetworkEndpoint endpoint)
        {
            var details = new Dictionary<string, string?>
            {
                ["node"] = profile.Node.Key(), ["host"] = endpoint.Host, ["port"] = endpoint.Port.ToString(),
                ["checkId"] = endpoint.Protocol, ["timeoutMs"] = settings.NetworkTimeoutMs.ToString(),
                ["scope"] = "Sola apertura TCP dal nodo corrente, nessun payload applicativo"
            };
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                timeout.CancelAfter(settings.NetworkTimeoutMs);
                var watch = Stopwatch.StartNew();
                await probe.ConnectAsync(endpoint.Host, endpoint.Port, timeout.Token).WaitAsync(timeout.Token);
                details["latencyMs"] = watch.ElapsedMilliseconds.ToString();
                return CheckResult.Pass("network-tcp", endpoint.Name, "Connessione TCP riuscita. Non prova REST/gRPC/DICOM, assenza di ispezione o accesso nella direzione inversa.", details);
            }
            catch (Exception ex) when (!cancellation.IsCancellationRequested && (ex is SocketException or OperationCanceledException or ArgumentException))
            {
                return CheckResult.Fail("network-tcp", endpoint.Name, $"Connessione fallita o timeout: {ex.Message}", details);
            }
        }
    }
}

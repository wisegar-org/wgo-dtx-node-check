using System.Diagnostics;
using System.Net.NetworkInformation;
using Wisegar.DTXInspector.Configuration;
using Wisegar.DTXInspector.Inventory;

namespace Wisegar.DTXInspector.Checks;

internal sealed record InspectedProcess(int Id, string Name);

internal static class DtxInspection
{
    internal static IReadOnlyList<CheckResult> Run(NodeProfile profile)
    {
        var snapshot = InfrastructureSnapshot.Read();
        var errors = new List<string>(snapshot.Errors);
        var processes = new List<InspectedProcess>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try { processes.Add(new(process.Id, process.ProcessName)); }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                { errors.Add($"Processo non leggibile: {ex.Message}"); }
            }
        }
        var sockets = WindowsTcpOwners.Read(errors);
        return Analyze(profile, snapshot, processes, sockets, errors);
    }

    internal static bool LooksLikeDtx(string name) =>
        name.Contains("dtx", StringComparison.OrdinalIgnoreCase)
        && !name.Contains("inspector", StringComparison.OrdinalIgnoreCase)
        && !name.Contains("nodecheck", StringComparison.OrdinalIgnoreCase)
        && !name.Contains("node check", StringComparison.OrdinalIgnoreCase);

    internal static IReadOnlyList<CheckResult> Analyze(NodeProfile profile, InfrastructureSnapshot snapshot,
        IReadOnlyList<InspectedProcess> processes, IReadOnlyList<OwnedTcpEndpoint> sockets, IReadOnlyList<string> errors)
    {
        var results = new List<CheckResult>();
        void Observe(string category, string name, string message, Dictionary<string, string?> details)
        {
            details["node"] = profile.Node.Key();
            details["machine"] = snapshot.Hostname;
            results.Add(new CheckResult(category, name, CheckStatus.Observed, message, details));
        }
        results.Add(CheckResult.Warning("dtx-inspection", "Ambito dello scan",
            "Fotografia locale: nomi DTX e configurazione identificano candidati. Il PID collega porte e processi, non certifica il protocollo o il singolo servizio in processi condivisi. Campioni non atomici: i PID possono cambiare. Solo TCP; nessuna scansione di porte, UDP non incluso."));
        var services = snapshot.Services.Where(s => LooksLikeDtx(s.Name) || LooksLikeDtx(s.DisplayName)
            || profile.RequiredServices.Any(check => check.Matches(s))
            || profile.Infrastructure.DtxServiceNames.Contains(s.Name, StringComparer.OrdinalIgnoreCase)).ToArray();
        foreach (var service in services)
            Observe("dtx-service", service.DisplayName, $"{service.Name}: {service.Status}; PID {service.ProcessId}.", new()
            {
                ["serviceName"] = service.Name, ["status"] = service.Status, ["pid"] = service.ProcessId.ToString(),
                ["association"] = "Nome DTX o servizio configurato; stato osservato, salute applicativa da verificare"
            });
        if (services.Length == 0) results.Add(CheckResult.Warning("dtx-service", "Servizi DTX", "Nessun candidato rilevato; verificare configurazione, installazione e permessi di lettura."));
        var candidates = processes.Where(p => LooksLikeDtx(p.Name)
            || profile.RequiredProcesses.Any(c => Path.GetFileNameWithoutExtension(c.Name).Equals(p.Name, StringComparison.OrdinalIgnoreCase))
            || services.Any(s => s.ProcessId > 0 && s.ProcessId == p.Id)).ToDictionary(p => p.Id);
        foreach (var process in candidates.Values)
            Observe("dtx-process", process.Name, $"Processo osservato con PID {process.Id}.", new() { ["pid"] = process.Id.ToString() });
        var matched = 0;
        foreach (var socket in sockets)
        {
            var linkedServices = services.Where(s => s.ProcessId > 0 && s.ProcessId == socket.ProcessId).ToArray();
            var ownerCandidate = candidates.TryGetValue(socket.ProcessId, out var process) || linkedServices.Length > 0;
            var configuredPort = socket.State == TcpState.Listen && profile.RequiredTcpListeners.Any(c => c.Port == socket.LocalPort
                && (c.Address == "0.0.0.0" || c.Address == "::" || c.Address == socket.LocalAddress));
            if (!ownerCandidate && !configuredPort) continue;
            matched++;
            var owner = process?.Name ?? processes.FirstOrDefault(p => p.Id == socket.ProcessId)?.Name ?? "non leggibile/terminato";
            var details = new Dictionary<string, string?>
            {
                ["pid"] = socket.ProcessId.ToString(), ["process"] = owner,
                ["servicesInProcess"] = string.Join(", ", snapshot.Services.Where(s => s.ProcessId > 0 && s.ProcessId == socket.ProcessId).Select(s => s.Name)),
                ["localAddress"] = socket.LocalAddress, ["localPort"] = socket.LocalPort.ToString(),
                ["remoteAddress"] = socket.State == TcpState.Listen ? null : socket.RemoteAddress,
                ["remotePort"] = socket.State == TcpState.Listen ? null : socket.RemotePort.ToString(),
                ["state"] = socket.State.ToString(),
                ["association"] = ownerCandidate ? "PID di processo candidato DTX/configurato; attribuzione al singolo servizio non garantita"
                    : "Solo porta configurata: proprietario NON identificato come DTX"
            };
            var name = $"[{socket.LocalAddress}]:{socket.LocalPort}";
            var message = $"{socket.State}; PID {socket.ProcessId} ({owner})" +
                (socket.State == TcpState.Listen ? "." : $" -> [{socket.RemoteAddress}]:{socket.RemotePort}.");
            if (ownerCandidate) Observe("dtx-tcp", name, message, details);
            else results.Add(CheckResult.Warning("dtx-tcp", name, message + " Corrispondenza di porta, non prova di appartenenza a DTX.", details));
        }
        if (matched == 0) results.Add(CheckResult.Warning("dtx-tcp", "Porte DTX", "Nessun endpoint TCP candidato osservato. Avviare DTX e ripetere; non dimostra assenza di comunicazioni."));
        foreach (var adapter in snapshot.Adapters)
            Observe("dtx-network", adapter.Name, $"IPv4: {string.Join(", ", adapter.Ipv4)}; DNS: {string.Join(", ", adapter.Dns)}.", new()
            {
                ["adapterId"] = adapter.Id, ["description"] = adapter.Description, ["up"] = adapter.Up.ToString(),
                ["dhcp"] = adapter.Dhcp?.ToString() ?? "non verificato", ["ipv4"] = string.Join(", ", adapter.Ipv4),
                ["ipv6"] = string.Join(", ", adapter.Ipv6), ["dnsServers"] = string.Join(", ", adapter.Dns),
                ["association"] = adapter.Id.Equals(profile.Infrastructure.AdapterId, StringComparison.OrdinalIgnoreCase)
                    ? "Scheda DTX selezionata in configurazione" : "Scheda locale di contesto; associazione DTX non confermata"
            });
        if (snapshot.Adapters.Length == 0) results.Add(CheckResult.Warning("dtx-network", "Schede IP/DNS", "Nessuna scheda leggibile."));
        foreach (var error in errors.Distinct()) results.Add(CheckResult.Warning("dtx-inspection", "Lettura incompleta", error));
        return results;
    }
}

using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Principal;
using Wisegar.DTXInspector.Configuration;
using Wisegar.DTXInspector.Inventory;

namespace Wisegar.DTXInspector.Checks;

internal sealed record InfrastructureItem(string Id, string Name);
internal sealed record AdapterObservation(string Id, string Name, string Description, bool Up,
    bool? Dhcp, string[] Ipv4, string[] Ipv6, string[] Dns);

internal sealed record InfrastructureSnapshot(string Hostname, AdapterObservation[] Adapters,
    string[] Processes, ServiceInventory[] Services, string[] HostsEntries, bool? Elevated,
    string CurrentUser, string[] Errors)
{
    internal static InfrastructureSnapshot Read()
    {
        var errors = new List<string>();
        T Read<T>(string section, Func<T> read, T fallback)
        {
            try { return read(); }
            catch (Exception ex) { errors.Add($"{section}: {ex.Message}"); return fallback; }
        }
        var adapters = Read("Adapter", () => NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(nic =>
            {
                var ip = nic.GetIPProperties();
                return new AdapterObservation(nic.Id, nic.Name, nic.Description,
                    nic.OperationalStatus == OperationalStatus.Up,
                    OperatingSystem.IsWindows() && nic.Supports(NetworkInterfaceComponent.IPv4) ? ip.GetIPv4Properties().IsDhcpEnabled : null,
                    ip.UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                        .Select(a => a.Address.ToString()).ToArray(),
                    ip.UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetworkV6)
                        .Select(a => a.Address.ToString()).ToArray(),
                    ip.DnsAddresses.Select(a => a.ToString()).ToArray());
            }).ToArray(), []);
        var processes = Read("Processi", () =>
        {
            var running = Process.GetProcesses();
            try { return running.Select(p => p.ProcessName).ToArray(); }
            finally { foreach (var process in running) process.Dispose(); }
        }, Array.Empty<string>());
        var services = Read("Servizi", () => WindowsServiceEnumerator.ListServices().ToArray(), []);
        var hosts = Read("Hosts", () => File.ReadAllLines(Path.Combine(Environment.SystemDirectory, "drivers", "etc", "hosts"))
            .Select(line => line.Split('#')[0].Trim()).Where(line => line.Length > 0).ToArray(), []);
        bool? elevated = Read<bool?>("Token", () =>
        {
            if (!OperatingSystem.IsWindows()) return null;
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }, null);
        return new(Environment.MachineName, adapters, processes, services, hosts, elevated,
            $"{Environment.UserDomainName}\\{Environment.UserName}", errors.ToArray());
    }
}

internal static class InfrastructureChecklist
{
    internal const string Category = "infrastructure";
    internal static IReadOnlyList<InfrastructureItem> Items(NodeKind role)
    {
        var items = new List<InfrastructureItem>
        {
            new("ip", role == NodeKind.Client ? "DHCP solo con DNS rapido e stabile" : "IP statico del nodo DTX"),
            new("ipv6", "IPv6 disabilitato sulla scheda DTX (Client: se richiesto)"),
            new("hostname", "Hostname invariato dall'installazione/associazione"),
            new("core-dns", "Risoluzione del Core tramite DNS locale interno"),
            new("vpn", "Assenza di VPN/mesh attivi e adapter virtuali interferenti"),
            new("hosts", "Assenza di override hosts interferenti")
        };
        if (role == NodeKind.Core)
        {
            items.Add(new("services", "Servizi Windows DTX attivi"));
            items.Add(new("service-health", "Servizi DTX funzionanti a livello applicativo"));
        }
        if (role == NodeKind.Workstation)
        {
            items.Add(new("permissions", "Lettura/scrittura dell'utente operativo nelle directory locali DTX"));
            items.Add(new("installation-admin", "Installazioni e aggiornamenti DTX con amministratore locale"));
        }
        items.AddRange([
            new("bidirectional-dns", "DNS bidirezionale tra Core, workstation e client"),
            new("dns-fallback", "DNS interno senza fallback lenti verso DNS pubblici"),
            new("traffic", "Traffico tra nodi consentito e non ispezionato"),
            new("rest", "Comunicazioni REST DTX"),
            new("grpc", "Comunicazioni gRPC DTX"),
            new("dynamic-tcp", "Porte TCP dinamiche/servizio DTX"),
            new("dicom", "DICOM TCP 104 dove richiesto"),
            new("security-inspection", "Assenza di ispezione SSL/TLS e DPI antivirus/EDR"),
            new("smb", "Scambio dati DTX senza dipendenza da SMB o unita' mappate")
        ]);
        return items;
    }

    internal static IReadOnlyList<CheckResult> Run(NodeProfile profile, InfrastructureSnapshot snapshot, IReadOnlyList<CheckResult>? probes = null)
    {
        var settings = profile.Infrastructure;
        var adapter = snapshot.Adapters.SingleOrDefault(a => string.Equals(a.Id, settings.AdapterId, StringComparison.OrdinalIgnoreCase));
        return Items(profile.Node).Select(item =>
        {
            var details = new Dictionary<string, string?>
            {
                ["checkId"] = item.Id,
                ["node"] = profile.Node.Key(),
                ["machine"] = snapshot.Hostname,
                ["scope"] = "Osservazione locale; nessuna modifica di sistema",
                ["adapter"] = adapter is null ? "Impostare infrastructure.adapterId (GUID)" : $"{adapter.Name} ({adapter.Id})",
                ["availableAdapters"] = string.Join("; ", snapshot.Adapters.Select(a => $"{a.Name} [{a.Id}] up={a.Up}")),
                ["readErrors"] = string.Join("; ", snapshot.Errors)
            };
            if (settings.ManualEvidence.TryGetValue(item.Id, out var evidence))
                details["operatorEvidence"] = evidence + " (dichiarazione da verificare, non prova automatica)";
            var (status, message) = Evaluate(item.Id, profile, snapshot, adapter, details);
            var related = (probes ?? []).Where(p => p.Details.TryGetValue("checkId", out var id) && id == item.Id).ToArray();
            if (related.Length > 0)
            {
                details["networkProbes"] = string.Join("; ", related.Select(p => $"{p.Name}: {p.Status} - {p.Message}"));
                if (related.Any(p => p.Status == CheckStatus.Fail))
                {
                    status = CheckStatus.Fail;
                    message = "Almeno una prova di rete associata e' fallita. " + message;
                }
            }
            return new CheckResult(Category, item.Name, status, message, details);
        }).ToArray();
    }

    private static (CheckStatus, string) Evaluate(string id, NodeProfile profile, InfrastructureSnapshot snapshot,
        AdapterObservation? adapter, Dictionary<string, string?> details)
    {
        var settings = profile.Infrastructure;
        (CheckStatus, string) Unknown(string message) => (CheckStatus.Warning, "NON VERIFICATO: " + message);
        switch (id)
        {
            case "ip":
                if (adapter is null || !adapter.Up || adapter.Ipv4.Length == 0 || adapter.Dhcp is null)
                    return Unknown("Selezionare la scheda DTX attiva con IPv4; non viene scelta automaticamente tra piu' reti.");
                details["ipv4"] = string.Join(", ", adapter.Ipv4);
                details["dhcp"] = adapter.Dhcp.ToString();
                if (adapter.Ipv4.All(ip => ip.StartsWith("169.254.", StringComparison.Ordinal)))
                    return (CheckStatus.Fail, "Solo indirizzi IPv4 link-local; indirizzo DTX non valido.");
                if (profile.Node == NodeKind.Client)
                    return adapter.Dhcp.Value ? Unknown("Client DHCP: verificare velocita' e stabilita' del DNS interno nel tempo.")
                        : (CheckStatus.Pass, "Client con DHCP disabilitato sulla scheda DTX. DNS verificato separatamente.");
                return adapter.Dhcp.Value ? (CheckStatus.Fail, "DHCP attivo: il nodo richiede IP statico.")
                    : (CheckStatus.Pass, "DHCP disabilitato e IPv4 presente sulla scheda DTX; verificare assegnazione e unicita' IP.");
            case "ipv6":
                if (profile.Node == NodeKind.Client && settings.ClientRequiresIpv6Disabled == false)
                    return (CheckStatus.NotApplicable, "Configurazione: disabilitazione IPv6 non richiesta per questo client.");
                if (profile.Node == NodeKind.Client && settings.ClientRequiresIpv6Disabled is null)
                    return Unknown("Confermare con TiDental/Dexis l'applicabilita' e impostare clientRequiresIpv6Disabled.");
                if (adapter is null || !adapter.Up) return Unknown("Scheda DTX non identificata o inattiva.");
                details["ipv6Addresses"] = string.Join(", ", adapter.Ipv6);
                return adapter.Ipv6.Length > 0 ? (CheckStatus.Fail, "Indirizzi IPv6 presenti sulla scheda DTX per cui e' richiesta la disabilitazione.")
                    : Unknown("Nessun indirizzo IPv6 osservato: non dimostra che il binding IPv6 sia disabilitato. Verificare la scheda DTX.");
            case "hostname":
                details["expectedHostname"] = settings.ExpectedHostname;
                if (string.IsNullOrWhiteSpace(settings.ExpectedHostname)) return Unknown("Impostare expectedHostname dal verbale di installazione, non dal nome corrente.");
                return string.Equals(snapshot.Hostname, settings.ExpectedHostname, StringComparison.OrdinalIgnoreCase)
                    ? (CheckStatus.Pass, "Hostname corrente uguale alla baseline configurata. Non garantisce modifiche future.")
                    : (CheckStatus.Fail, "Hostname diverso dalla baseline di installazione/associazione.");
            case "core-dns":
                details["coreHostname"] = settings.CoreHostname;
                return Unknown("Vedere le prove DNS del Core (se coreHostname e' configurato). Confrontare expectedCoreAddresses e resolver interni; la risposta del sistema puo' provenire da cache/hosts/fallback.");
            case "vpn":
                string[] terms = ["vpn", "tailscale", "zerotier", "wireguard", "hamachi", "radmin", "openvpn", "anyconnect", "forticlient", "globalprotect", "virtual", "hyper-v", "vmware", "tap-", "tun"];
                bool Suspect(string value) => terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
                var found = snapshot.Processes.Where(Suspect)
                    .Concat(snapshot.Services.Where(s => s.Status == "Running" && (Suspect(s.Name) || Suspect(s.DisplayName))).Select(s => s.Name))
                    .Concat(snapshot.Adapters.Where(a => a.Up && Suspect(a.Name + " " + a.Description)).Select(a => a.Name)).Distinct().ToArray();
                details["suspectedComponents"] = string.Join("; ", found);
                return Unknown(found.Length == 0 ? "Nessun componente noto rilevato; euristica non esaustiva. Verificare adapter, DNS e routing effettivo."
                    : "Componenti VPN/mesh/virtuali attivi sospetti: verificare se interferiscono con DTX. La presenza non prova un conflitto.");
            case "hosts":
                if (snapshot.Errors.Any(e => e.StartsWith("Hosts:"))) return Unknown("File hosts non leggibile.");
                var overrides = snapshot.HostsEntries.Where(line =>
                {
                    var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    return parts.Length < 2 || parts.Skip(1).Any(name => !name.Equals("localhost", StringComparison.OrdinalIgnoreCase));
                }).ToArray();
                details["hostsEntries"] = string.Join("; ", overrides);
                return overrides.Length == 0 ? (CheckStatus.Pass, "Nessuna voce hosts attiva oltre a localhost nel file locale.")
                    : Unknown("Override hosts presenti: verificare gli alias DTX e l'impatto sulla risoluzione locale.");
            case "services":
                if (profile.RequiredServices.Any(check => check.MatchDisplayName && snapshot.Services.Count(check.Matches) != 1))
                    return Unknown("Servizio DTX atteso non identificato univocamente tramite nome visualizzato; verificare installazione e versione.");
                var names = settings.DtxServiceNames.Concat(profile.RequiredServices.Select(check => check.MatchDisplayName
                        ? snapshot.Services.Single(check.Matches).Name : check.Name))
                    .Concat(snapshot.Services.Where(s => (s.Name + s.DisplayName).Contains("dtx", StringComparison.OrdinalIgnoreCase)).Select(s => s.Name))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (names.Length == 0) return Unknown("Nessun servizio DTX identificato; configurare dtxServiceNames con i nomi reali dell'installazione.");
                var states = names.Select(name => (Name: name, Service: snapshot.Services.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))).ToArray();
                details["serviceStates"] = string.Join("; ", states.Select(s => $"{s.Name}={s.Service?.Status ?? "non rilevato"}"));
                if (states.Any(s => s.Service is null)) return Unknown("Servizi attesi non rilevati; verificare installazione e permessi di enumerazione.");
                return states.All(s => s.Service!.Status == "Running") ? (CheckStatus.Pass, "Servizi identificati in esecuzione; salute applicativa verificata separatamente.")
                    : (CheckStatus.Fail, "Almeno un servizio DTX non e' in esecuzione.");
            case "service-health": return Unknown("Eseguire un'operazione DTX e verificare log/health applicativo. Running non dimostra il funzionamento.");
            case "permissions":
                details["operationalUser"] = settings.OperationalUser;
                details["toolUser"] = snapshot.CurrentUser;
                details["directories"] = string.Join("; ", settings.LocalDtxDirectories);
                foreach (var directory in settings.LocalDtxDirectories)
                {
                    if (!Path.IsPathFullyQualified(directory) || directory.StartsWith(@"\\") || directory.StartsWith("//"))
                        return Unknown("Specificare solo percorsi locali assoluti in localDtxDirectories.");
                    if (!Directory.Exists(directory))
                        return (CheckStatus.Fail, $"Directory DTX attesa non accessibile o assente: {directory}. Permessi effettivi da verificare con l'utente operativo.");
                    if (OperatingSystem.IsWindows())
                    {
                        try
                        {
                            details[$"acl:{directory}"] = new DirectoryInfo(directory)
                                .GetAccessControl(System.Security.AccessControl.AccessControlSections.Access)
                                .GetSecurityDescriptorSddlForm(System.Security.AccessControl.AccessControlSections.Access);
                        }
                        catch (Exception ex) { details[$"acl:{directory}"] = "Non leggibile: " + ex.Message; }
                    }
                }
                return Unknown("Verificare ACL e accesso effettivo con l'utente operativo sulle directory locali DTX, soprattutto C:\\ProgramData\\DTX Studio... . Non vengono creati file di prova e il token amministrativo del tool non dimostra tali diritti.");
            case "installation-admin":
                details["toolElevated"] = snapshot.Elevated?.ToString();
                return Unknown("Verificare verbali/log di installazione e aggiornamento con amministratore locale; l'elevazione attuale non prova quella degli installer.");
            case "bidirectional-dns":
                details["peerHostnames"] = string.Join(", ", settings.PeerHostnames);
                return Unknown("Eseguire la verifica da ciascun nodo verso gli altri e allegare i report separati. Questo PC non prova la direzione remota.");
            case "dns-fallback":
                details["configuredDns"] = adapter is null ? "" : string.Join(", ", adapter.Dns);
                details["approvedInternalDns"] = string.Join(", ", settings.InternalDnsServers);
                if (adapter is null || adapter.Dns.Length == 0 || settings.InternalDnsServers.Length == 0)
                    return Unknown("Identificare adapterId e internalDnsServers per verificare i resolver della rete DTX.");
                if (adapter.Dns.Any(dns => !settings.InternalDnsServers.Contains(dns, StringComparer.OrdinalIgnoreCase)))
                    return (CheckStatus.Fail, "La scheda DTX usa resolver fuori dall'elenco DNS interni approvato; possibile fallback esterno.");
                return Unknown("Resolver della scheda DTX tutti nell'elenco interno. Verificare inoltri sui server DNS, altre schede e latenze: la configurazione locale non esclude fallback a monte.");
            case "traffic": return Unknown("Verificare firewall, routing e policy di ispezione su entrambi i nodi e sugli apparati intermedi.");
            case "rest": return Unknown("Configurare endpoint REST effettivi della versione DTX e verificare la risposta applicativa; nessuna porta presunta.");
            case "grpc": return Unknown("Configurare endpoint gRPC effettivi e verificare una chiamata applicativa; nessuna porta presunta.");
            case "dynamic-tcp": return Unknown("Confermare porte/range dinamici e di servizio della versione DTX; verificare apertura e traffico necessario.");
            case "dicom":
                return settings.DicomRequired == false ? (CheckStatus.NotApplicable, "DICOM non richiesto secondo la configurazione del nodo.")
                    : Unknown(settings.DicomRequired is null ? "Specificare dicomRequired: TCP 104 va controllata solo se richiesta."
                        : "Verificare TCP 104 verso il destinatario DICOM e un'associazione DICOM (AE Title e policy); un listener non basta.");
            case "security-inspection": return Unknown("Verificare policy AV/EDR e apparati intermedi: assenza di ispezione SSL/TLS o DPI sul traffico locale DTX non deducibile dal solo elenco processi.");
            case "smb": return Unknown("Verificare configurazione e flusso DTX senza share SMB o unita' mappate. La presenza/assenza di mapping non dimostra una dipendenza applicativa.");
            default: throw new ArgumentOutOfRangeException(nameof(id));
        }
    }
}

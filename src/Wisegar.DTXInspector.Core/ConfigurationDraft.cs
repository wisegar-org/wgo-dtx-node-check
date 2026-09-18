using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Wisegar.DTXInspector.Checks;
using Wisegar.DTXInspector.Configuration;

namespace Wisegar.DTXInspector.Core;

/// <summary>A detached, lossless JSON tree: only fields edited by the form are changed.</summary>
public sealed class ConfigurationDraft
{
    private readonly string _path;
    private readonly byte[] _originalHash;
    private readonly JsonObject _original;
    public JsonObject Root { get; }
    public JsonObject Node { get; }
    public JsonObject Infrastructure { get; }
    public DtxNodeRole Role { get; }

    public static IReadOnlyList<string> ChecklistIds(DtxNodeRole role) => InfrastructureChecklist.Items((NodeKind)(int)role).Select(x => x.Id).ToArray();

    public ConfigurationDraft(string path, DtxNodeRole role)
    {
        _path = Path.GetFullPath(path); Role = role;
        var bytes = File.ReadAllBytes(_path);
        _originalHash = SHA256.HashData(bytes);
        // Parse the same bytes that were fingerprinted, including UTF-8 BOM support.
        var text = System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        Root = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject
            ?? throw new InvalidOperationException("Il file deve contenere un oggetto JSON.");
        Validate();
        _original = (JsonObject)Root.DeepClone();
        Node = Root["nodes"]?[role.ToString().ToLowerInvariant()] as JsonObject
            ?? throw new InvalidOperationException($"Sezione {role} mancante. Correggere appsettings.json.");
        if (Node["infrastructure"] is null) Node["infrastructure"] = new JsonObject();
        Infrastructure = (JsonObject)Node["infrastructure"]!;
    }

    public void Validate()
    {
        var configuration = Deserialize();
        CheckConfigurationLoader.Validate(configuration);
        var settings = configuration.BuildProfile((NodeKind)(int)Role).Infrastructure;
        foreach (var path in settings.LocalDtxDirectories)
        {
            if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\") || path.StartsWith("//")
                || new DriveInfo(Path.GetPathRoot(path)!).DriveType == DriveType.Network)
                throw new InvalidOperationException($"Cartella DTX non locale o non assoluta: {path}");
        }
    }

    public IReadOnlyList<string> Missing()
    {
        var s = Deserialize().BuildProfile((NodeKind)(int)Role).Infrastructure;
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(s.AdapterId)) missing.Add("Scheda DTX: IP statico e IPv6 non verificabili senza selezione.");
        if (string.IsNullOrWhiteSpace(s.ExpectedHostname)) missing.Add("Nome approvato: stabilità dell'identità non verificabile.");
        if (string.IsNullOrWhiteSpace(s.CoreHostname)) missing.Add("Nome Core: risoluzione DNS non verificabile.");
        if (s.ExpectedCoreAddresses.Length == 0) missing.Add("IP approvati del Core mancanti.");
        if (s.InternalDnsServers.Length == 0) missing.Add("DNS interni approvati mancanti.");
        if (s.PeerHostnames.Length == 0) missing.Add("Nodi peer: direzione locale della comunicazione non verificabile.");
        if (s.Endpoints.Length == 0) missing.Add("Endpoint: comunicazioni TCP non verificabili.");
        foreach (var protocol in new[] { "rest", "grpc", "dynamic-tcp" })
            if (!s.Endpoints.Any(x => x.Protocol == protocol)) missing.Add($"Destinazione {protocol} mancante: verifica non eseguibile.");
        if (s.DicomRequired is null) missing.Add("Applicabilità DICOM da confermare.");
        if (s.DicomRequired == true && !s.Endpoints.Any(x => x.Protocol == "dicom" && x.Port == 104)) missing.Add("DICOM richiesto: endpoint TCP 104 mancante.");
        if (s.DnsMaxLatencyMs > s.NetworkTimeoutMs) missing.Add("La soglia DNS supera il timeout: verificare i valori.");
        if (!string.IsNullOrWhiteSpace(s.AdapterId) && !System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces().Any(x => x.Id.Equals(s.AdapterId, StringComparison.OrdinalIgnoreCase)))
            missing.Add("La scheda salvata non è presente su questo PC; selezionare quella corretta senza sostituire automaticamente la baseline.");
        if (Role == DtxNodeRole.Workstation)
        {
            if (string.IsNullOrWhiteSpace(s.OperationalUser)) missing.Add("Utente operativo mancante: ACL non verificabili.");
            if (s.LocalDtxDirectories.Length == 0) missing.Add("Cartelle locali DTX mancanti: ACL non verificabili.");
        }
        if (Role == DtxNodeRole.Core && s.DtxServiceNames.Length == 0) missing.Add("Servizi Core da confermare.");
        return missing;
    }

    public string DescribeChanges()
    {
        var changes = new List<string>();
        Compare(_original, Root, "");
        return changes.Count == 0 ? "Nessuna modifica." : string.Join("\n", changes);
        void Compare(JsonNode? old, JsonNode? current, string path)
        {
            if (JsonNode.DeepEquals(old, current)) return;
            if (old is JsonObject a && current is JsonObject b)
            {
                foreach (var key in a.Select(x => x.Key).Union(b.Select(x => x.Key))) Compare(a[key], b[key], path.Length == 0 ? key : path + "." + key);
            }
            else changes.Add($"{path}\n  Prima: {old?.ToJsonString() ?? "(assente)"}\n  Dopo: {current?.ToJsonString() ?? "(rimosso)"}");
        }
    }

    public string Save()
    {
        Validate();
        EnsureUnchanged();
        var temporary = _path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, Root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            CheckConfigurationLoader.Load(temporary);
            EnsureUnchanged();
            var backup = _path + $".{DateTime.Now:yyyyMMdd-HHmmss}.{Guid.NewGuid():N}.bak";
            File.Replace(temporary, _path, backup);
            return backup;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public async Task<IReadOnlyList<NodeCheckItem>> PreviewNetworkAsync(CancellationToken cancellation)
    {
        Validate();
        var profile = Deserialize().BuildProfile((NodeKind)(int)Role);
        var results = await NetworkChecks.RunAsync(profile, cancellation: cancellation);
        return results.Select(x => new NodeCheckItem(x.Category, x.Name, x.Status.ToString(), x.Message, x.Details)).ToArray();
    }

    private CheckConfiguration Deserialize() => JsonSerializer.Deserialize(Root.ToJsonString(), CheckConfigurationJsonContext.Default.CheckConfiguration)
        ?? throw new InvalidOperationException("Configurazione vuota.");

    private void EnsureUnchanged()
    {
        if (!File.Exists(_path) || !SHA256.HashData(File.ReadAllBytes(_path)).SequenceEqual(_originalHash))
            throw new InvalidOperationException("appsettings.json è cambiato sul disco. Nessuna scrittura eseguita. Chiudi e riapri la configurazione guidata per ricaricarlo.");
    }
}

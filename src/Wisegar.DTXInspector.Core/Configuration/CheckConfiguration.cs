using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wisegar.DTXInspector.Configuration;

internal sealed class CheckConfiguration
{
    public string? EnvironmentName { get; init; }
    public CheckSet Common { get; set; } = new();
    public NodeConfiguration Nodes { get; set; } = new();

    public NodeProfile BuildProfile(NodeKind node)
    {
        var nodeSet = Nodes.Get(node);
        if (nodeSet is null)
        {
            throw new ConfigurationException($"La configurazione non contiene la sezione nodes.{node.Key()}.");
        }

        return new NodeProfile(
            node,
            nodeSet.DisplayName ?? node.Key(),
            Merge(Common.RequiredDirectories, nodeSet.RequiredDirectories),
            Merge(Common.RequiredFiles, nodeSet.RequiredFiles),
            Merge(Common.RequiredProcesses, nodeSet.RequiredProcesses),
            Merge(Common.RequiredServices, nodeSet.RequiredServices),
            Merge(Common.RequiredTcpListeners, nodeSet.RequiredTcpListeners),
            nodeSet.Infrastructure ?? new InfrastructureSettings());
    }

    private static IReadOnlyList<T> Merge<T>(IReadOnlyList<T>? common, IReadOnlyList<T>? specific)
    {
        if ((common is null || common.Count == 0) && (specific is null || specific.Count == 0))
        {
            return [];
        }

        var merged = new List<T>();
        if (common is not null)
        {
            merged.AddRange(common);
        }

        if (specific is not null)
        {
            merged.AddRange(specific);
        }

        return merged;
    }
}

internal sealed class NodeConfiguration
{
    public CheckSet? Core { get; init; }
    public CheckSet? Workstation { get; init; }
    public CheckSet? Client { get; init; }

    public CheckSet? Get(NodeKind node) =>
        node switch
        {
            NodeKind.Core => Core,
            NodeKind.Workstation => Workstation,
            NodeKind.Client => Client,
            _ => null
        };
}

internal sealed class CheckSet
{
    public InfrastructureSettings? Infrastructure { get; set; }
    public string? DisplayName { get; init; }
    public IReadOnlyList<PathCheck> RequiredDirectories { get; set; } = [];
    public IReadOnlyList<PathCheck> RequiredFiles { get; set; } = [];
    public IReadOnlyList<ProcessCheck> RequiredProcesses { get; set; } = [];
    public IReadOnlyList<ServiceCheck> RequiredServices { get; set; } = [];
    public IReadOnlyList<TcpListenerCheck> RequiredTcpListeners { get; set; } = [];
}

internal sealed record NodeProfile(
    NodeKind Node,
    string DisplayName,
    IReadOnlyList<PathCheck> RequiredDirectories,
    IReadOnlyList<PathCheck> RequiredFiles,
    IReadOnlyList<ProcessCheck> RequiredProcesses,
    IReadOnlyList<ServiceCheck> RequiredServices,
    IReadOnlyList<TcpListenerCheck> RequiredTcpListeners,
    InfrastructureSettings Infrastructure);

internal sealed class InfrastructureSettings
{
    public string? AdapterId { get; set; }
    public string? ExpectedHostname { get; set; }
    public string? CoreHostname { get; set; }
    public string[] InternalDnsServers { get; set; } = [];
    public string[] ExpectedCoreAddresses { get; set; } = [];
    public string[] PeerHostnames { get; set; } = [];
    public string? OperationalUser { get; set; }
    public string[] LocalDtxDirectories { get; set; } = [];
    public string[] DtxServiceNames { get; set; } = [];
    public bool? DicomRequired { get; set; }
    public NetworkEndpoint[] Endpoints { get; set; } = [];
    public int NetworkTimeoutMs { get; set; } = 2000;
    public int DnsMaxLatencyMs { get; set; } = 250;
    public Dictionary<string, string> ManualEvidence { get; set; } = [];
}

internal sealed record NetworkEndpoint(string Name, string Host, int Port, string Protocol = "tcp");

internal sealed record PathCheck(
    string Path,
    string? Name = null,
    bool Required = true);

internal sealed record ProcessCheck(
    string Name,
    string? DisplayName = null,
    bool Required = true);

internal sealed record ServiceCheck(
    string Name,
    string? DisplayName = null,
    IReadOnlyList<string>? ExpectedStatuses = null,
    bool Required = true,
    bool MatchDisplayName = false)
{
    public bool Matches(Wisegar.DTXInspector.Inventory.ServiceInventory service) =>
        string.Equals(MatchDisplayName ? service.DisplayName : service.Name, Name, StringComparison.OrdinalIgnoreCase);
}

internal sealed record TcpListenerCheck(
    int Port,
    string? Name = null,
    string Address = "0.0.0.0",
    bool Required = true);

[JsonSerializable(typeof(CheckConfiguration))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    WriteIndented = true)]
internal sealed partial class CheckConfigurationJsonContext : JsonSerializerContext;

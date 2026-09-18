using System.Text.Json;
using System.Text.Json.Serialization;

namespace DtxNodeCheck.Configuration;

internal sealed class CheckConfiguration
{
    public string? EnvironmentName { get; init; }
    public CheckSet Common { get; init; } = new();
    public NodeConfiguration Nodes { get; init; } = new();

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
            Merge(Common.RequiredTcpListeners, nodeSet.RequiredTcpListeners));
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
    public string? DisplayName { get; init; }
    public IReadOnlyList<PathCheck> RequiredDirectories { get; init; } = [];
    public IReadOnlyList<PathCheck> RequiredFiles { get; init; } = [];
    public IReadOnlyList<ProcessCheck> RequiredProcesses { get; init; } = [];
    public IReadOnlyList<ServiceCheck> RequiredServices { get; init; } = [];
    public IReadOnlyList<TcpListenerCheck> RequiredTcpListeners { get; init; } = [];
}

internal sealed record NodeProfile(
    NodeKind Node,
    string DisplayName,
    IReadOnlyList<PathCheck> RequiredDirectories,
    IReadOnlyList<PathCheck> RequiredFiles,
    IReadOnlyList<ProcessCheck> RequiredProcesses,
    IReadOnlyList<ServiceCheck> RequiredServices,
    IReadOnlyList<TcpListenerCheck> RequiredTcpListeners);

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
    bool Required = true);

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

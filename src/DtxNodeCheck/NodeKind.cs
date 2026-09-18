namespace DtxNodeCheck;

internal enum NodeKind
{
    Core,
    Workstation,
    Client
}

internal static class NodeKindExtensions
{
    public static string Key(this NodeKind node) =>
        node switch
        {
            NodeKind.Core => "core",
            NodeKind.Workstation => "workstation",
            NodeKind.Client => "client",
            _ => throw new ArgumentOutOfRangeException(nameof(node))
        };
}

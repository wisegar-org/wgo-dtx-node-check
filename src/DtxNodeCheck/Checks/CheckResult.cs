namespace DtxNodeCheck.Checks;

internal sealed record CheckResult(
    string Category,
    string Name,
    CheckStatus Status,
    string Message,
    IReadOnlyDictionary<string, string?> Details)
{
    public static CheckResult Pass(string category, string name, string message, IReadOnlyDictionary<string, string?>? details = null) =>
        new(category, name, CheckStatus.Pass, message, details ?? EmptyDetails.Value);

    public static CheckResult Warning(string category, string name, string message, IReadOnlyDictionary<string, string?>? details = null) =>
        new(category, name, CheckStatus.Warning, message, details ?? EmptyDetails.Value);

    public static CheckResult Fail(string category, string name, string message, IReadOnlyDictionary<string, string?>? details = null) =>
        new(category, name, CheckStatus.Fail, message, details ?? EmptyDetails.Value);
}

internal static class EmptyDetails
{
    public static readonly IReadOnlyDictionary<string, string?> Value = new Dictionary<string, string?>();
}

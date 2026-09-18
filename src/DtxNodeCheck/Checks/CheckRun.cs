using DtxNodeCheck.Configuration;
using DtxNodeCheck.Reporting;

namespace DtxNodeCheck.Checks;

internal sealed record CheckRun(
    DateTimeOffset StartedAt,
    string MachineName,
    string UserDomainName,
    string UserName,
    string OperatingSystemDescription,
    string ProcessArchitecture,
    string? EnvironmentName,
    NodeProfile Profile,
    IReadOnlyList<CheckResult> Results) : IRunResult
{
    public bool HasFailures => Results.Any(result => result.Status == CheckStatus.Fail);
    public int PassedCount => Results.Count(result => result.Status == CheckStatus.Pass);
    public int WarningCount => Results.Count(result => result.Status == CheckStatus.Warning);
    public int FailedCount => Results.Count(result => result.Status == CheckStatus.Fail);
    public int ExitCode => HasFailures ? ExitCodes.CheckFailed : ExitCodes.Success;
}

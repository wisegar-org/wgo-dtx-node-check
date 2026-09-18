using Wisegar.DTXInspector.Configuration;
using Wisegar.DTXInspector.Reporting;

namespace Wisegar.DTXInspector.Checks;

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
    public int NotApplicableCount => Results.Count(result => result.Status == CheckStatus.NotApplicable);
    public int ObservedCount => Results.Count(result => result.Status == CheckStatus.Observed);
    public string Assessment => HasFailures ? "NON CONFORME: presenti controlli falliti."
        : WarningCount > 0 ? "VERIFICA INCOMPLETA: presenti requisiti non verificati/segnalazioni."
        : "Controlli applicabili completati senza segnalazioni.";
    public int ExitCode => HasFailures ? ExitCodes.CheckFailed : ExitCodes.Success;
}

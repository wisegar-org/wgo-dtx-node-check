using DtxNodeCheck.Checks;
using DtxNodeCheck.Configuration;
using DtxNodeCheck.Diagnostics;
using DtxNodeCheck.Inventory;
using DtxNodeCheck.Reporting;

namespace DtxNodeCheck.Core;

public enum DtxNodeRole
{
    Core,
    Workstation,
    Client
}

public sealed record NodeCheckPaths(
    string ConfigPath,
    string ReportPath,
    string LogPath);

public sealed record NodeCheckItem(
    string Category,
    string Name,
    string Status,
    string Message);

public sealed record NodeCheckExecutionResult(
    int ExitCode,
    string ReportPath,
    string RenderedReport,
    IReadOnlyList<NodeCheckItem> Items);

public sealed record NodeDetectionCandidate(
    DtxNodeRole Role,
    int Score,
    IReadOnlyList<string> Reasons);

public sealed record NodeDetectionResult(
    DtxNodeRole? Role,
    bool IsConfident,
    string Message,
    IReadOnlyList<NodeDetectionCandidate> Candidates);

public static class NodeCheckService
{
    public static NodeCheckPaths CreateDefaultPaths(DtxNodeRole role, string applicationName, string? configFileName = null)
    {
        var node = role.ToString().ToLowerInvariant();
        var baseDir = AppContext.BaseDirectory;
        var dataRoot = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), applicationName)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), applicationName);

        return new NodeCheckPaths(
            Path.Combine(baseDir, configFileName ?? $"dtx-node-check-{node}.json"),
            Path.Combine(dataRoot, "Reports", $"DTX-{node}-Report.html"),
            Path.Combine(dataRoot, "Logs", $"DtxNodeCheck-{node}-debug.log"));
    }

    public static string CreateDefaultConfigPath(string configFileName) =>
        Path.Combine(AppContext.BaseDirectory, configFileName);

    public static NodeDetectionResult DetectNodeRole(string configPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new NodeDetectionResult(
                null,
                false,
                "Inferenza nodo disponibile solo su Windows.",
                []);
        }

        var candidates = NodeRoleDetector.Detect(configPath)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Role.ToString(), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (candidates.Length == 0)
        {
            return new NodeDetectionResult(null, false, "Nessun segnale locale sufficiente per inferire il nodo.", []);
        }

        var best = candidates[0];
        var second = candidates.Length > 1 ? candidates[1] : null;
        var confident = best.Score >= 3 && (second is null || best.Score - second.Score >= 2);
        return confident
            ? new NodeDetectionResult(best.Role, true, $"Nodo inferito: {best.Role}.", candidates)
            : new NodeDetectionResult(null, false, "Nodo non inferito con certezza. Seleziona il nodo manualmente.", candidates);
    }

    public static IReadOnlyList<NodeCheckItem> GetPlannedChecks(string configPath, DtxNodeRole role)
    {
        var configuration = CheckConfigurationLoader.Load(configPath);
        var profile = configuration.BuildProfile(ToNodeKind(role));
        var items = new List<NodeCheckItem>
        {
            new("runtime", "Windows", "Pending", "Verifica sistema operativo Windows."),
            new("identity", "Machine", "Pending", "Lettura identita' locale macchina/utente.")
        };

        items.AddRange(profile.RequiredDirectories.Select(item => new NodeCheckItem("directory", item.Name ?? item.Path, "Pending", item.Path)));
        items.AddRange(profile.RequiredFiles.Select(item => new NodeCheckItem("file", item.Name ?? item.Path, "Pending", item.Path)));
        items.AddRange(profile.RequiredProcesses.Select(item => new NodeCheckItem("process", item.DisplayName ?? item.Name, "Pending", item.Name)));
        items.AddRange(profile.RequiredServices.Select(item => new NodeCheckItem("service", item.DisplayName ?? item.Name, "Pending", item.Name)));
        items.AddRange(profile.RequiredTcpListeners.Select(item => new NodeCheckItem("tcp-listener", item.Name ?? $"TCP {item.Port}", "Pending", $"{item.Address}:{item.Port}")));
        return items;
    }

    public static NodeCheckExecutionResult RunCheck(
        string configPath,
        DtxNodeRole role,
        string reportPath,
        string logPath,
        bool openReport)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DtxNodeCheck puo' eseguire i controlli solo su Windows.");
        }

        DebugLog.TryInitialize(logPath);
        DebugLog.Info("Avvio controllo da GUI.", new Dictionary<string, string?>
        {
            ["node"] = role.ToString(),
            ["config"] = configPath,
            ["report"] = reportPath
        });

        var configuration = CheckConfigurationLoader.Load(configPath);
        var profile = configuration.BuildProfile(ToNodeKind(role));
        var run = CheckRunner.Run(configuration, profile);
        var rendered = ReportRenderer.Render(run, ReportFormat.Html);
        ReportWriter.Write(reportPath, rendered);

        if (openReport)
        {
            ReportOpener.Open(reportPath);
        }

        return new NodeCheckExecutionResult(
            run.ExitCode,
            reportPath,
            rendered,
            run.Results.Select(result => new NodeCheckItem(result.Category, result.Name, result.Status.ToString(), result.Message)).ToArray());
    }

    public static NodeCheckExecutionResult RunInventory(string reportPath, string logPath, bool openReport)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DtxNodeCheck puo' eseguire l'inventario solo su Windows.");
        }

        DebugLog.TryInitialize(logPath);
        var run = InventoryRunner.Run();
        var rendered = ReportRenderer.Render(run, ReportFormat.Html);
        ReportWriter.Write(reportPath, rendered);
        if (openReport)
        {
            ReportOpener.Open(reportPath);
        }

        return new NodeCheckExecutionResult(
            run.ExitCode,
            reportPath,
            rendered,
            [new NodeCheckItem("inventory", "PC Inventory", "Pass", "Inventario completato.")]);
    }

    private static NodeKind ToNodeKind(DtxNodeRole role) =>
        role switch
        {
            DtxNodeRole.Core => NodeKind.Core,
            DtxNodeRole.Workstation => NodeKind.Workstation,
            DtxNodeRole.Client => NodeKind.Client,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
        };
}

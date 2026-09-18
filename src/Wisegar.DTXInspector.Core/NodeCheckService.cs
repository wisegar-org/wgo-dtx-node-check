using Wisegar.DTXInspector.Checks;
using Wisegar.DTXInspector.Configuration;
using Wisegar.DTXInspector.Diagnostics;
using Wisegar.DTXInspector.Inventory;
using Wisegar.DTXInspector.Reporting;

namespace Wisegar.DTXInspector.Core;

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

public static class NodeCheckService
{
    public static string? LoadSettingsFromInventory(string configPath, DtxNodeRole role)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("L'inventario delle impostazioni richiede Windows.");
        var inventory = InventoryRunner.Run();
        var existingJson = File.Exists(configPath) ? File.ReadAllText(configPath) : null;
        var json = InventoryConfiguration.Create(inventory, role, existingJson);
        return InventoryConfiguration.Save(configPath, json);
    }

    public static NodeCheckPaths CreateDefaultPaths(DtxNodeRole role, string applicationName, string? configFileName = null)
    {
        var node = role.ToString().ToLowerInvariant();
        var baseDir = AppContext.BaseDirectory;
        var dataRoot = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), applicationName)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), applicationName);

        return new NodeCheckPaths(
            Path.Combine(baseDir, configFileName ?? "appsettings.json"),
            Path.Combine(dataRoot, "Reports", $"DTX-{node}-Report.html"),
            Path.Combine(dataRoot, "Logs", $"Wisegar.DTXInspector-{node}-debug.log"));
    }

    public static string CreateDefaultConfigPath(string configFileName) =>
        Path.Combine(AppContext.BaseDirectory, configFileName);

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
        items.AddRange(InfrastructureChecklist.Items(profile.Node).Select(item =>
            new NodeCheckItem(InfrastructureChecklist.Category, item.Name, "Pending", $"Checklist obbligatoria: {item.Id}")));
        items.AddRange(profile.Infrastructure.PeerHostnames.Prepend(profile.Infrastructure.CoreHostname ?? "")
            .Where(host => !string.IsNullOrWhiteSpace(host)).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(host => new NodeCheckItem("network-dns", host, "Pending", $"3 risoluzioni, timeout {profile.Infrastructure.NetworkTimeoutMs} ms ciascuna")));
        items.AddRange(profile.Infrastructure.Endpoints.Select(endpoint => new NodeCheckItem("network-tcp", endpoint.Name,
            "Pending", $"{endpoint.Host}:{endpoint.Port}, timeout {profile.Infrastructure.NetworkTimeoutMs} ms")));
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
            throw new PlatformNotSupportedException("Wisegar.DTXInspector puo' eseguire i controlli solo su Windows.");
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
        reportPath = CreateReportPathForRun(reportPath, role, Environment.MachineName);
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
            throw new PlatformNotSupportedException("Wisegar.DTXInspector puo' eseguire l'inventario solo su Windows.");
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

    internal static string CreateReportPathForRun(string requestedPath, DtxNodeRole role, string machine)
    {
        var safeMachine = string.Concat(machine.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(requestedPath))!,
            $"DTX-{role.ToString().ToLowerInvariant()}-{safeMachine}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}.html");
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

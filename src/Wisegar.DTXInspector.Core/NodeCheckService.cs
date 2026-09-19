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
    string Message,
    IReadOnlyDictionary<string, string?>? Details = null,
    string? Id = null);

public sealed record NodeActivity(DateTimeOffset Time, string Level, string Phase, string Message, string? Details = null, NodeCheckItem? Item = null);

public sealed record NodeCheckExecutionResult(
    int ExitCode,
    string ReportPath,
    string RenderedReport,
    IReadOnlyList<NodeCheckItem> Items,
    string? LogPath = null);

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
        var dataRoot = CreateDefaultDataRoot(applicationName);

        return new NodeCheckPaths(
            Path.Combine(baseDir, configFileName ?? "appsettings.json"),
            Path.Combine(dataRoot, "Reports", $"DTX-{node}-Report.html"),
            Path.Combine(dataRoot, "Logs", $"Wisegar.DTXInspector-{node}-debug.log"));
    }

    public static string CreateDefaultDataRoot(string applicationName) =>
        Path.Combine(Environment.GetFolderPath(OperatingSystem.IsWindows()
            ? Environment.SpecialFolder.CommonApplicationData
            : Environment.SpecialFolder.LocalApplicationData), "Wisegar", applicationName);

    public static string CreateDefaultConfigPath(string configFileName) =>
        Path.Combine(AppContext.BaseDirectory, configFileName);

    public static IReadOnlyList<NodeCheckItem> GetPlannedChecks(string configPath, DtxNodeRole role)
    {
        var configuration = CheckConfigurationLoader.Load(configPath);
        var profile = configuration.BuildProfile(ToNodeKind(role));
        return GetPlannedChecks(profile);
    }

    private static IReadOnlyList<NodeCheckItem> GetPlannedChecks(NodeProfile profile)
    {
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
        return WithIds(items);
    }

    public static NodeCheckExecutionResult RunCheck(
        string configPath,
        DtxNodeRole role,
        string reportPath,
        string logPath,
        bool openReport,
        bool inspectDtx = false,
        IProgress<NodeActivity>? progress = null,
        CancellationToken cancellation = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Wisegar.DTXInspector puo' eseguire i controlli solo su Windows.");
        }

        logPath = UniquePath(logPath);
        using var diagnostics = DebugLog.Begin(logPath, (level, message, details) => progress?.Report(new(DateTimeOffset.Now, level, "Diagnostica", message, details)));
        progress?.Report(new(DateTimeOffset.Now, "INFO", "Log", logPath));
        DebugLog.Info("Avvio controllo da GUI.", new Dictionary<string, string?>
        {
            ["node"] = role.ToString(),
            ["config"] = configPath,
            ["report"] = reportPath
        });

        var configuration = CheckConfigurationLoader.Load(configPath);
        var profile = configuration.BuildProfile(ToNodeKind(role));
        var occurrences = new Dictionary<string, int>();
        var run = CheckRunner.Run(configuration, profile,
            phase => progress?.Report(new(DateTimeOffset.Now, "INFO", phase, "In corso")), cancellation,
            result =>
            {
                var index = occurrences.GetValueOrDefault(result.Category);
                occurrences[result.Category] = index + 1;
                var item = new NodeCheckItem(result.Category, result.Name, result.Status.ToString(), result.Message, result.Details, $"{result.Category}:{index}");
                progress?.Report(new(DateTimeOffset.Now, "RESULT", result.Category, result.Message, Item: item));
            });
        if (cancellation.IsCancellationRequested)
        {
            var existing = run.Results.GroupBy(x => (x.Category, x.Name)).ToDictionary(g => g.Key, g => g.Count());
            var missing = new List<CheckResult>();
            foreach (var item in GetPlannedChecks(profile))
            {
                var key = (item.Category, item.Name);
                if (existing.GetValueOrDefault(key) > 0) existing[key]--;
                else missing.Add(CheckResult.Warning(item.Category, item.Name, "Non eseguito: operazione interrotta."));
            }
            run = run with { Results = run.Results.Concat(missing).ToArray() };
        }
        if (inspectDtx && !cancellation.IsCancellationRequested)
        {
            progress?.Report(new(DateTimeOffset.Now, "INFO", "Scan DTX", "Correlazione servizi, processi e porte"));
            run = run with { Results = DtxInspection.Run(profile).Concat(run.Results).ToArray() };
        }
        if (cancellation.IsCancellationRequested && !run.Results.Any(x => x.Category == "execution"))
            run = run with { Results = run.Results.Append(CheckResult.Warning("execution", "Interruzione richiesta", "Dati raccolti fino all'interruzione; esecuzione da considerare parziale.")).ToArray() };
        reportPath = CreateReportPathForRun(reportPath, role, Environment.MachineName);
        if (inspectDtx)
            reportPath = Path.Combine(Path.GetDirectoryName(reportPath)!, "Inspection-" + Path.GetFileName(reportPath));
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
            WithIds(run.Results.Select(result => new NodeCheckItem(result.Category, result.Name, result.Status.ToString(), result.Message, result.Details))), logPath);
    }

    public static NodeCheckExecutionResult RunInventory(string reportPath, string logPath, bool openReport, IProgress<NodeActivity>? progress = null, CancellationToken cancellation = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Wisegar.DTXInspector puo' eseguire l'inventario solo su Windows.");
        }

        reportPath = UniquePath(reportPath);
        logPath = UniquePath(logPath);
        var readErrors = new List<string>();
        using var diagnostics = DebugLog.Begin(logPath, (level, message, details) =>
        {
            if (level == "ERROR" || message.Contains("non leggibile", StringComparison.OrdinalIgnoreCase)) readErrors.Add(message + " " + details);
            progress?.Report(new(DateTimeOffset.Now, level, "Inventario locale", message, details));
        });
        progress?.Report(new(DateTimeOffset.Now, "INFO", "Log", logPath));
        var run = InventoryRunner.Run(cancellation);
        run = run with { ObservationErrors = (run.ObservationErrors ?? []).Concat(readErrors).ToArray() };
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
            new[] { new NodeCheckItem("PC", run.Machine.MachineName, "Observed", run.Machine.OperatingSystemDescription) }
                .Concat(run.Services.Select(x => new NodeCheckItem("Servizi", x.DisplayName, "Observed", $"{x.Name}: {x.Status}; PID {x.ProcessId}")))
                .Concat(run.Processes.Select(x => new NodeCheckItem("Processi", x.Name, "Observed", $"Istanze: {x.Count}")))
                .Concat(run.TcpListeners.Select(x => new NodeCheckItem("Porte locali", $"TCP {x.Port}", "Observed", x.Address)))
                .Concat(run.InstalledPrograms.Select(x => new NodeCheckItem("Software", x.DisplayName, "Observed", $"{x.DisplayVersion} | {x.Publisher}")))
                .Concat(run.CandidatePaths.Select(x => new NodeCheckItem("Directory", x.Name, "Observed", x.Path)))
                .Concat((run.Adapters ?? []).Select(x => new NodeCheckItem("Rete locale", x.Name, "Observed",
                    $"IPv4: {string.Join(", ", x.Ipv4)} · IPv6: {string.Join(", ", x.Ipv6)} · DHCP: {x.Dhcp} · DNS: {string.Join(", ", x.Dns)}",
                    new Dictionary<string, string?> { ["adapterId"] = x.Id, ["description"] = x.Description, ["up"] = x.Up.ToString(), ["scope"] = "Osservazione locale; nessuna prova DNS/TCP" })))
                .Concat((run.ObservationErrors ?? []).Select(x => new NodeCheckItem("Letture incomplete", "Dati non disponibili", "Warning", x)))
                .ToArray(), logPath);
    }

    private static string UniquePath(string path) => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!,
        $"{Path.GetFileNameWithoutExtension(path)}-{Environment.MachineName}-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}{Path.GetExtension(path)}");

    private static NodeCheckItem[] WithIds(IEnumerable<NodeCheckItem> items)
    {
        var counts = new Dictionary<string, int>();
        return items.Select(item => { var index = counts.GetValueOrDefault(item.Category); counts[item.Category] = index + 1; return item with { Id = $"{item.Category}:{index}" }; }).ToArray();
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

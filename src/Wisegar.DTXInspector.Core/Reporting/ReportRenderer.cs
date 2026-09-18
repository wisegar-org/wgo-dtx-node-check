using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wisegar.DTXInspector.Checks;
using Wisegar.DTXInspector.Inventory;

namespace Wisegar.DTXInspector.Reporting;

internal static class ReportRenderer
{
    public static string Render(IRunResult run, ReportFormat format) =>
        run switch
        {
            CheckRun checkRun => Render(checkRun, format),
            InventoryRun inventoryRun => Render(inventoryRun, format),
            _ => throw new ArgumentException("Tipo risultato non supportato.", nameof(run))
        };

    private static string Render(CheckRun run, ReportFormat format) =>
        format switch
        {
            ReportFormat.Text => RenderText(run),
            ReportFormat.Json => RenderJson(run),
            ReportFormat.Html => RenderCheckHtml(run),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

    private static string RenderText(CheckRun run)
    {
        var builder = new StringBuilder();
        builder.AppendLine("WGO DTX Inspector");
        builder.AppendLine("============================");
        builder.AppendLine($"Started at: {run.StartedAt:O}");
        builder.AppendLine($"Environment: {run.EnvironmentName ?? "(not set)"}");
        builder.AppendLine($"Node: {run.Profile.Node.Key()} ({run.Profile.DisplayName})");
        builder.AppendLine($"Machine: {run.MachineName}");
        builder.AppendLine($"User: {run.UserDomainName}\\{run.UserName}");
        builder.AppendLine($"OS: {run.OperatingSystemDescription}");
        builder.AppendLine($"Architecture: {run.ProcessArchitecture}");
        builder.AppendLine();
        builder.AppendLine($"Summary: {run.PassedCount} passed, {run.WarningCount} warning, {run.FailedCount} failed");
        builder.AppendLine($"Assessment: {run.Assessment} Not applicable: {run.NotApplicableCount}.");
        builder.AppendLine("Scope: solo il nodo e il PC indicati. Eseguire separatamente sugli altri nodi; PASS TCP/DNS non certifica il funzionamento applicativo.");
        builder.AppendLine();

        foreach (var result in run.Results)
        {
            builder.AppendLine($"[{result.Status.ToString().ToUpperInvariant()}] {result.Category} - {result.Name}");
            builder.AppendLine($"  {result.Message}");

            foreach (var detail in result.Details.Where(detail => !string.IsNullOrWhiteSpace(detail.Value)))
            {
                builder.AppendLine($"  {detail.Key}: {detail.Value}");
            }
        }

        return builder.ToString();
    }

    private static string RenderJson(CheckRun run)
    {
        var document = new
        {
            startedAt = run.StartedAt,
            environmentName = run.EnvironmentName,
            node = run.Profile.Node.Key(),
            nodeDisplayName = run.Profile.DisplayName,
            machine = new
            {
                name = run.MachineName,
                userDomainName = run.UserDomainName,
                userName = run.UserName,
                operatingSystem = run.OperatingSystemDescription,
                processArchitecture = run.ProcessArchitecture
            },
            summary = new
            {
                passed = run.PassedCount,
                warnings = run.WarningCount,
                failed = run.FailedCount,
                notApplicable = run.NotApplicableCount,
                assessment = run.Assessment
            },
            results = run.Results.Select(result => new
            {
                category = result.Category,
                name = result.Name,
                status = result.Status,
                message = result.Message,
                details = result.Details
            })
        };

        return JsonSerializer.Serialize(document, JsonOptions);
    }

    private static string Render(InventoryRun run, ReportFormat format) =>
        format switch
        {
            ReportFormat.Text => RenderInventoryText(run),
            ReportFormat.Json => JsonSerializer.Serialize(run, JsonOptions),
            ReportFormat.Html => RenderInventoryHtml(run),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

    private static string RenderCheckHtml(CheckRun run)
    {
        var builder = CreateHtml("WGO DTX Inspector Report");
        builder.AppendLine("<main>");
        builder.AppendLine("<h1>WGO DTX Inspector Report</h1>");
        builder.AppendLine($"<p><strong>{Html(run.Assessment)}</strong></p>");
        builder.AppendLine("<p>Report separato del solo nodo e PC indicati. Eseguire i controlli sugli altri nodi per la direzione inversa. Un PASS DNS/TCP non certifica funzionamento applicativo o assenza di ispezione. WARNING indica anche requisiti non verificati.</p>");
        builder.AppendLine("<section class=\"summary\">");
        builder.AppendLine($"<div><strong>Started</strong><span>{Html(run.StartedAt.ToString("O"))}</span></div>");
        builder.AppendLine($"<div><strong>Environment</strong><span>{Html(run.EnvironmentName ?? "(not set)")}</span></div>");
        builder.AppendLine($"<div><strong>Node</strong><span>{Html(run.Profile.Node.Key())} ({Html(run.Profile.DisplayName)})</span></div>");
        builder.AppendLine($"<div><strong>Machine</strong><span>{Html(run.MachineName)}</span></div>");
        builder.AppendLine($"<div><strong>User</strong><span>{Html(run.UserDomainName)}\\{Html(run.UserName)}</span></div>");
        builder.AppendLine($"<div><strong>OS</strong><span>{Html(run.OperatingSystemDescription)}</span></div>");
        builder.AppendLine($"<div><strong>Architecture</strong><span>{Html(run.ProcessArchitecture)}</span></div>");
        builder.AppendLine("</section>");
        builder.AppendLine("<section class=\"counts\">");
        builder.AppendLine($"<div class=\"pass\"><strong>{run.PassedCount}</strong><span>Passed</span></div>");
        builder.AppendLine($"<div class=\"warning\"><strong>{run.WarningCount}</strong><span>Warnings</span></div>");
        builder.AppendLine($"<div class=\"fail\"><strong>{run.FailedCount}</strong><span>Failed</span></div>");
        builder.AppendLine($"<div><strong>{run.NotApplicableCount}</strong><span>Not applicable</span></div>");
        builder.AppendLine("</section>");
        builder.AppendLine("<table><thead><tr><th>Status</th><th>Category</th><th>Name</th><th>Message</th><th>Details</th></tr></thead><tbody>");

        foreach (var result in run.Results)
        {
            builder.AppendLine($"<tr class=\"{Html(result.Status.ToString().ToLowerInvariant())}\">");
            builder.AppendLine($"<td>{Html(result.Status.ToString())}</td>");
            builder.AppendLine($"<td>{Html(result.Category)}</td>");
            builder.AppendLine($"<td>{Html(result.Name)}</td>");
            builder.AppendLine($"<td>{Html(result.Message)}</td>");
            builder.AppendLine($"<td>{RenderDetails(result.Details)}</td>");
            builder.AppendLine("</tr>");
        }

        builder.AppendLine("</tbody></table>");
        builder.AppendLine("</main></body></html>");
        return builder.ToString();
    }

    private static string RenderInventoryText(InventoryRun run)
    {
        var builder = new StringBuilder();
        builder.AppendLine("WGO DTX Inspector - PC Inventory");
        builder.AppendLine("==============================");
        builder.AppendLine($"Started at: {run.StartedAt:O}");
        builder.AppendLine($"Machine: {run.Machine.MachineName}");
        builder.AppendLine($"User: {run.Machine.UserDomainName}\\{run.Machine.UserName}");
        builder.AppendLine($"OS: {run.Machine.OperatingSystemDescription}");
        builder.AppendLine($"Architecture: {run.Machine.ProcessArchitecture}");
        builder.AppendLine($"System directory: {run.Machine.SystemDirectory}");
        builder.AppendLine();

        builder.AppendLine("Drives");
        builder.AppendLine("------");
        foreach (var drive in run.Drives)
        {
            builder.AppendLine($"{drive.Name} {drive.DriveType} ready={drive.IsReady} format={drive.DriveFormat ?? "-"} total={FormatBytes(drive.TotalSizeBytes)} free={FormatBytes(drive.AvailableFreeSpaceBytes)}");
        }

        builder.AppendLine();
        builder.AppendLine("Candidate Paths");
        builder.AppendLine("---------------");
        foreach (var path in run.CandidatePaths)
        {
            builder.AppendLine($"{path.Kind}: {path.Path}");
        }

        builder.AppendLine();
        builder.AppendLine("Installed Programs");
        builder.AppendLine("------------------");
        foreach (var program in run.InstalledPrograms)
        {
            builder.AppendLine($"{program.DisplayName} | version={program.DisplayVersion ?? "-"} | publisher={program.Publisher ?? "-"} | installLocation={program.InstallLocation ?? "-"}");
        }

        builder.AppendLine();
        builder.AppendLine("Processes");
        builder.AppendLine("---------");
        foreach (var process in run.Processes)
        {
            builder.AppendLine($"{process.Name} ({process.Count})");
        }

        builder.AppendLine();
        builder.AppendLine("Services");
        builder.AppendLine("--------");
        foreach (var service in run.Services)
        {
            builder.AppendLine($"{service.Name} | {service.Status} | {service.DisplayName}");
        }

        builder.AppendLine();
        builder.AppendLine("TCP Listeners");
        builder.AppendLine("-------------");
        foreach (var listener in run.TcpListeners)
        {
            builder.AppendLine($"{listener.Address}:{listener.Port}");
        }

        return builder.ToString();
    }

    private static string RenderInventoryHtml(InventoryRun run)
    {
        var builder = CreateHtml("WGO DTX Inspector Inventory");
        builder.AppendLine("<main>");
        builder.AppendLine("<h1>WGO DTX Inspector Inventory</h1>");
        builder.AppendLine("<section class=\"summary\">");
        builder.AppendLine($"<div><strong>Started</strong><span>{Html(run.StartedAt.ToString("O"))}</span></div>");
        builder.AppendLine($"<div><strong>Machine</strong><span>{Html(run.Machine.MachineName)}</span></div>");
        builder.AppendLine($"<div><strong>User</strong><span>{Html(run.Machine.UserDomainName)}\\{Html(run.Machine.UserName)}</span></div>");
        builder.AppendLine($"<div><strong>OS</strong><span>{Html(run.Machine.OperatingSystemDescription)}</span></div>");
        builder.AppendLine($"<div><strong>Architecture</strong><span>{Html(run.Machine.ProcessArchitecture)}</span></div>");
        builder.AppendLine("</section>");
        AppendInventoryTable(builder, "Drives", ["Name", "Type", "Ready", "Format", "Total", "Free"], run.Drives.Select(drive => new[]
        {
            drive.Name,
            drive.DriveType,
            drive.IsReady.ToString(),
            drive.DriveFormat ?? "-",
            FormatBytes(drive.TotalSizeBytes),
            FormatBytes(drive.AvailableFreeSpaceBytes)
        }));
        AppendInventoryTable(builder, "Candidate Paths", ["Kind", "Name", "Path"], run.CandidatePaths.Select(path => new[] { path.Kind, path.Name, path.Path }));
        AppendInventoryTable(builder, "Installed Programs", ["Name", "Version", "Publisher", "Install Location"], run.InstalledPrograms.Select(program => new[]
        {
            program.DisplayName,
            program.DisplayVersion ?? "-",
            program.Publisher ?? "-",
            program.InstallLocation ?? "-"
        }));
        AppendInventoryTable(builder, "Processes", ["Name", "Count"], run.Processes.Select(process => new[] { process.Name, process.Count.ToString() }));
        AppendInventoryTable(builder, "Services", ["Name", "Status", "Display Name"], run.Services.Select(service => new[] { service.Name, service.Status, service.DisplayName }));
        AppendInventoryTable(builder, "TCP Listeners", ["Address", "Port"], run.TcpListeners.Select(listener => new[] { listener.Address, listener.Port.ToString() }));
        builder.AppendLine("</main></body></html>");
        return builder.ToString();
    }

    private static StringBuilder CreateHtml(string title)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html>");
        builder.AppendLine("<html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.AppendLine($"<title>{Html(title)}</title>");
        builder.AppendLine("<style>");
        builder.AppendLine("""
            :root { color-scheme: light; --border:#d8dee8; --text:#172033; --muted:#5d6678; --pass:#177245; --warning:#996600; --fail:#b42318; --bg:#f6f8fb; }
            body { margin:0; font-family: "Segoe UI", Arial, sans-serif; background:var(--bg); color:var(--text); }
            main { max-width:1180px; margin:0 auto; padding:28px; }
            h1 { font-size:28px; margin:0 0 18px; }
            h2 { font-size:20px; margin:28px 0 10px; }
            .summary, .counts { display:grid; grid-template-columns:repeat(auto-fit, minmax(220px, 1fr)); gap:10px; margin:16px 0; }
            .summary div, .counts div { background:white; border:1px solid var(--border); border-radius:6px; padding:12px; }
            .summary strong, .counts span { display:block; color:var(--muted); font-size:12px; text-transform:uppercase; letter-spacing:.04em; }
            .summary span { display:block; margin-top:4px; word-break:break-word; }
            .counts strong { font-size:28px; display:block; }
            .counts .pass strong { color:var(--pass); } .counts .warning strong { color:var(--warning); } .counts .fail strong { color:var(--fail); }
            table { width:100%; border-collapse:collapse; background:white; border:1px solid var(--border); margin:10px 0 22px; }
            th, td { border-bottom:1px solid var(--border); padding:8px 10px; text-align:left; vertical-align:top; font-size:13px; }
            th { background:#eef2f7; color:#2b3448; }
            tr.pass td:first-child { color:var(--pass); font-weight:700; }
            tr.warning td:first-child { color:var(--warning); font-weight:700; }
            tr.fail td:first-child { color:var(--fail); font-weight:700; }
            ul { margin:0; padding-left:18px; }
            """);
        builder.AppendLine("</style></head><body>");
        return builder;
    }

    private static string RenderDetails(IReadOnlyDictionary<string, string?> details)
    {
        if (details.Count == 0)
        {
            return "";
        }

        var builder = new StringBuilder();
        builder.Append("<ul>");
        foreach (var detail in details.Where(detail => !string.IsNullOrWhiteSpace(detail.Value)))
        {
            builder.Append("<li><strong>");
            builder.Append(Html(detail.Key));
            builder.Append("</strong>: ");
            builder.Append(Html(detail.Value!));
            builder.Append("</li>");
        }

        builder.Append("</ul>");
        return builder.ToString();
    }

    private static void AppendInventoryTable(StringBuilder builder, string title, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        builder.AppendLine($"<h2>{Html(title)}</h2>");
        builder.AppendLine("<table><thead><tr>");
        foreach (var header in headers)
        {
            builder.AppendLine($"<th>{Html(header)}</th>");
        }

        builder.AppendLine("</tr></thead><tbody>");
        foreach (var row in rows)
        {
            builder.AppendLine("<tr>");
            foreach (var cell in row)
            {
                builder.AppendLine($"<td>{Html(cell)}</td>");
            }

            builder.AppendLine("</tr>");
        }

        builder.AppendLine("</tbody></table>");
    }

    private static string Html(string value) =>
        System.Net.WebUtility.HtmlEncode(value);

    private static string FormatBytes(long? bytes)
    {
        if (bytes is null)
        {
            return "-";
        }

        var value = bytes.Value;
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        var suffixIndex = 0;
        var display = (double)value;
        while (display >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            display /= 1024;
            suffixIndex++;
        }

        return $"{display:0.##} {suffixes[suffixIndex]}";
    }

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

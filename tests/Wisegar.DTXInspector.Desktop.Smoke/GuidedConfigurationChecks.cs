using System.Text.Json.Nodes;
using Wisegar.DTXInspector;
using Wisegar.DTXInspector.Checks;
using Wisegar.DTXInspector.Configuration;
using Wisegar.DTXInspector.Core;

namespace Wisegar.DTXInspector.Desktop.Smoke;

internal static class GuidedConfigurationChecks
{
    internal static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "DTX-Editor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "appsettings.json");
            var root = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.json")))!.AsObject();
            root["customRoot"] = 42;
            root["nodes"]!["workstation"]!["customNode"] = "preserve";
            root["nodes"]!["workstation"]!["infrastructure"]!["customNested"] = "preserve";
            root["nodes"]!["workstation"]!["requiredProcesses"]![0]!["vendorExtension"] = "preserve";
            File.WriteAllText(path, root.ToJsonString());
            var original = File.ReadAllBytes(path);
            var draft = new ConfigurationDraft(path, DtxNodeRole.Workstation);
            draft.Infrastructure["expectedHostname"] = "APPROVED-WS";
            Assert(File.ReadAllBytes(path).SequenceEqual(original), "Editing draft performs no writes");
            Assert(draft.Missing().Count > 0, "Missing data explicitly identified");
            Assert(draft.DescribeChanges().Contains("APPROVED-WS"), "Review includes changed value");
            var backup = draft.Save();
            Assert(File.ReadAllBytes(backup).SequenceEqual(original), "Backup preserves exact original bytes");
            var saved = JsonNode.Parse(File.ReadAllText(path))!;
            Assert(JsonNode.DeepEquals(saved["common"], root["common"]), "Common unchanged");
            Assert(JsonNode.DeepEquals(saved["nodes"]!["core"], root["nodes"]!["core"]), "Other roles unchanged");
            Assert(saved["nodes"]!["workstation"]!["requiredProcesses"]![0]!["vendorExtension"]!.ToString() == "preserve", "Unknown list item fields preserved");
            Assert(saved["nodes"]!["workstation"]!["infrastructure"]!["customNested"]!.ToString() == "preserve", "Unknown infrastructure fields preserved");
            var conflict = new ConfigurationDraft(path, DtxNodeRole.Core);
            File.AppendAllText(path, " ");
            var modified = File.ReadAllBytes(path);
            Throws(() => conflict.Save(), "Changed disk file cannot be overwritten");
            Assert(File.ReadAllBytes(path).SequenceEqual(modified), "Conflict leaves disk intact");
            var invalid = new ConfigurationDraft(path, DtxNodeRole.Workstation);
            invalid.Infrastructure["networkTimeoutMs"] = 0;
            Throws(() => invalid.Save(), "Invalid timeout cannot be saved");
            Assert(File.ReadAllBytes(path).SequenceEqual(modified), "Validation failure leaves disk intact");
            var config = CheckConfigurationLoader.Load(path);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            var run = CheckRunner.Run(config, config.BuildProfile(NodeKind.Workstation), cancellation: cancelled.Token);
            Assert(run.Results.Count(x => x.Category == InfrastructureChecklist.Category) == InfrastructureChecklist.Items(NodeKind.Workstation).Count, "Cancelled report retains mandatory checklist");
            Assert(run.Results.Any(x => x.Category == "execution") && run.Assessment.Contains("INCOMPLETA"), "Cancellation never yields completed assessment");
            var planned = NodeCheckService.GetPlannedChecks(path, DtxNodeRole.Workstation);
            Assert(planned.Select(x => x.Id).Distinct().Count() == planned.Count, "Stable IDs distinguish every planned row");
            var partial = NodeCheckService.RunCheck(path, DtxNodeRole.Workstation, Path.Combine(directory, "node.html"), Path.Combine(directory, "node.log"), false, cancellation: cancelled.Token);
            Assert(partial.Items.Count >= planned.Count && partial.Items.All(x => x.Status != "Pending"), "Partial report explicitly accounts for unexecuted checks");
            Assert(partial.RenderedReport.Contains("INCOMPLETA"), "Partial HTML assessment remains incomplete");
            Assert(!Wisegar.DTXInspector.Diagnostics.DebugLog.IsEnabled, "Run logging scope does not leak into configuration reads");
            var first = NodeCheckService.RunInventory(Path.Combine(directory, "pc.html"), Path.Combine(directory, "pc.log"), false, cancellation: cancelled.Token);
            var second = NodeCheckService.RunInventory(Path.Combine(directory, "pc.html"), Path.Combine(directory, "pc.log"), false, cancellation: cancelled.Token);
            Assert(first.ReportPath != second.ReportPath && first.LogPath != second.LogPath, "PC reports and logs never overwrite another run");
            Assert(first.Items.Any(x => x.Status == "Warning") && first.RenderedReport.Contains("interrotto"), "Partial inventory stays explicitly incomplete");
            var previewProfile = config.BuildProfile(NodeKind.Workstation) with { Infrastructure = new InfrastructureSettings { CoreHostname = "example.invalid" } };
            Throws(() => NetworkChecks.RunAsync(previewProfile, cancellation: cancelled.Token).GetAwaiter().GetResult(), "External cancellation aborts DNS without treating it as a failed probe");
            File.WriteAllText(path, "invalid json");
            Throws(() => new ConfigurationDraft(path, DtxNodeRole.Core), "Invalid source cannot be regenerated by wizard");
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void Throws(Action action, string message) { try { action(); } catch { return; } throw new InvalidOperationException(message); }
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

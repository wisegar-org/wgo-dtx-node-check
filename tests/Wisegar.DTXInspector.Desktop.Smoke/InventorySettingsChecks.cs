using System.Text.Json.Nodes;
using Wisegar.DTXInspector.Configuration;
using Wisegar.DTXInspector.Core;
using Wisegar.DTXInspector.Inventory;

namespace Wisegar.DTXInspector.Desktop.Smoke;

internal static class InventorySettingsChecks
{
    internal static void Run()
    {
        var inventory = new InventoryRun(DateTimeOffset.Now,
            new MachineInventory("PC", "", "", "Windows", "X64", true, "", ""), [],
            [new("DTXStudioClinic", 1), new("dtxstudioclinic", 2), new("WgoDtxNodeCheck", 1), new("WgoDtxInspector", 1), new("notepad", 1)],
            [new("DtxService", "DTX Service", "Running"), new("Spooler", "Print Spooler", "Running")],
            [new("0.0.0.0", 12345)], [],
            [new("DTX", @"C:\DTX", "Directory"), new("WGO DTX Node Check", @"C:\WGO DTX Node Check", "Directory"),
             new("WGO DTX Inspector", @"C:\WGO DTX Inspector", "Directory"),
             new("DTX remote", @"\\server\DTX", "Directory")]);
        const string original = """
            {"environmentName":"Custom","customMetadata":42,
             "common":{"requiredFiles":[{"path":"C:\\common.txt"}]},
             "nodes":{"core":{"requiredServices":[{"name":"KeepMe"}]},
             "workstation":{"requiredProcesses":[{"name":"ReplaceMe"}],"infrastructure":{"expectedHostname":"APPROVED"}},
             "client":{"displayName":"Keep Client"}}}
            """;
        var json = InventoryConfiguration.Create(inventory, DtxNodeRole.Workstation, original);
        var before = JsonNode.Parse(original)!;
        var after = JsonNode.Parse(json)!;
        Assert(JsonNode.DeepEquals(before["common"], after["common"]), "Common settings preserved");
        Assert(JsonNode.DeepEquals(before["nodes"]!["core"], after["nodes"]!["core"]), "Other node checks preserved");
        Assert(JsonNode.DeepEquals(before["nodes"]!["client"], after["nodes"]!["client"]), "Other node metadata preserved");
        Assert(after["customMetadata"]!.GetValue<int>() == 42, "Unknown root settings preserved");
        var node = after["nodes"]!["workstation"]!;
        Assert(JsonNode.DeepEquals(before["nodes"]!["workstation"]!["infrastructure"], node["infrastructure"]), "Infrastructure baseline survives inventory import");
        Assert(node["requiredProcesses"]!.AsArray().Count == 1, "Only DTX processes, deduplicated and excluding this tool");
        Assert(node["requiredDirectories"]!.AsArray().Count == 1, "Only local DTX paths excluding this tool");
        Assert(node["requiredServices"]!.AsArray().Count == 1, "Only DTX services");
        Assert(node["requiredTcpListeners"]!.AsArray().Count == 0, "Unattributed ports excluded");
        Assert(!node["requiredProcesses"]![0]!["required"]!.GetValue<bool>(), "Generated checks are optional");
        Assert(!json.Contains("ReplaceMe"), "Selected node settings replaced");
        Throws(() => InventoryConfiguration.Create(inventory with { Processes = [], Services = [], CandidatePaths = [] },
            DtxNodeRole.Core, original), "Empty inventory must not erase settings");
        Throws(() => InventoryConfiguration.Create(inventory, DtxNodeRole.Core, "broken JSON"), "Invalid JSON must not be overwritten");

        var directory = Path.Combine(Path.GetTempPath(), "DtxSettingsTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var config = Path.Combine(directory, "appsettings.json");
            File.WriteAllText(config, original);
            var backup = InventoryConfiguration.Save(config, json);
            Assert(backup is not null && File.ReadAllText(backup) == original, "Exact original backed up");
            Assert(File.ReadAllText(config) == json, "New settings saved");
            foreach (var role in Enum.GetValues<DtxNodeRole>())
                NodeCheckService.GetPlannedChecks(config, role);
            Throws(() => InventoryConfiguration.Save(config, "bad JSON"), "Invalid generated JSON rejected");
            Assert(File.ReadAllText(config) == json, "Failed validation leaves current settings intact");
            Assert(Directory.GetFiles(directory, "*.tmp").Length == 0, "Temporary files cleaned up");
            var newConfig = Path.Combine(directory, "new.json");
            Assert(InventoryConfiguration.Save(newConfig, InventoryConfiguration.Create(inventory, DtxNodeRole.Core, null)) is null,
                "Missing config created without a backup");
            foreach (var role in Enum.GetValues<DtxNodeRole>())
                NodeCheckService.GetPlannedChecks(newConfig, role);
        }
        finally
        {
            foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
        Console.WriteLine("PASS: inventory filtering, node preservation, replacement, backup and failure safety.");
    }

    private static void Assert(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void Throws(Action action, string message)
    {
        try { action(); }
        catch (Exception ex) when (ex is InvalidOperationException or System.Text.Json.JsonException or ConfigurationException) { return; }
        throw new InvalidOperationException(message);
    }
}

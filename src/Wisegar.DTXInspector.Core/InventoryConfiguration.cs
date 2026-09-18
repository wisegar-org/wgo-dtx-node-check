using System.Text.Json;
using System.Text.Json.Nodes;
using Wisegar.DTXInspector.Configuration;
using Wisegar.DTXInspector.Inventory;

namespace Wisegar.DTXInspector.Core;

internal static class InventoryConfiguration
{
    internal static string Create(InventoryRun inventory, DtxNodeRole role, string? existingJson)
    {
        if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role));
        var directories = inventory.CandidatePaths
            .Where(path => path.Kind == "Directory" && IsDtx(path.Name) && IsLocalPath(path.Path))
            .Select(path => path.Path)
            .Concat(inventory.InstalledPrograms.Where(program => IsDtx(program.DisplayName)
                && IsLocalPath(program.InstallLocation) && Directory.Exists(program.InstallLocation))
                .Select(program => program.InstallLocation!))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)
            .Select(path => new PathCheck(path, Required: false)).ToArray();
        var processes = inventory.Processes.Where(process => IsDtx(process.Name))
            .Select(process => process.Name).Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(name => new ProcessCheck(name, Required: false)).ToArray();
        var services = inventory.Services.Where(service => IsDtx(service.Name) || IsDtx(service.DisplayName))
            .DistinctBy(service => service.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(service => service.Name, StringComparer.OrdinalIgnoreCase)
            .Select(service => new ServiceCheck(service.Name, service.DisplayName, Required: false)).ToArray();
        if (directories.Length + processes.Length + services.Length == 0)
            throw new InvalidOperationException("Nessun componente DTX utile rilevato. Configurazione non modificata.");

        var root = existingJson is null
            ? new JsonObject { ["environmentName"] = "WGO DTX Inspector", ["common"] = new JsonObject() }
            : JsonNode.Parse(existingJson, documentOptions: new JsonDocumentOptions
                { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }) as JsonObject
                ?? throw new InvalidOperationException("La configurazione deve essere un oggetto JSON.");
        if (root["nodes"] is null) root["nodes"] = new JsonObject();
        var nodes = root["nodes"] as JsonObject
            ?? throw new InvalidOperationException("La sezione nodes non e' valida.");
        foreach (var node in Enum.GetValues<DtxNodeRole>())
            if (nodes[node.ToString().ToLowerInvariant()] is null)
                nodes[node.ToString().ToLowerInvariant()] = new JsonObject();
        var profile = new CheckSet
        {
            DisplayName = $"DTX {role}",
            RequiredDirectories = directories,
            RequiredProcesses = processes,
            RequiredServices = services
        };
        var key = role.ToString().ToLowerInvariant();
        var infrastructure = nodes[key]?["infrastructure"]?.DeepClone();
        nodes[key] = JsonSerializer.SerializeToNode(
            profile, CheckConfigurationJsonContext.Default.CheckSet);
        if (infrastructure is not null) nodes[key]!["infrastructure"] = infrastructure;
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    // Ignore this diagnostic tool itself and avoid probing network paths.
    private static bool IsDtx(string name) => name.Contains("dtx", StringComparison.OrdinalIgnoreCase)
        && !name.Contains("nodecheck", StringComparison.OrdinalIgnoreCase)
        && !name.Contains("node check", StringComparison.OrdinalIgnoreCase)
        && !name.Contains("dtxinspector", StringComparison.OrdinalIgnoreCase)
        && !name.Contains("dtx inspector", StringComparison.OrdinalIgnoreCase);

    private static bool IsLocalPath(string? path) => !string.IsNullOrWhiteSpace(path)
        && Path.IsPathFullyQualified(path) && !path.StartsWith(@"\\") && !path.StartsWith("//");

    internal static string? Save(string configPath, string json)
    {
        var fullPath = Path.GetFullPath(configPath);
        var temporaryPath = fullPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, json);
            CheckConfigurationLoader.Load(temporaryPath);
            if (File.Exists(fullPath))
            {
                var backupPath = fullPath + $".{DateTime.Now:yyyyMMdd-HHmmss}.{Guid.NewGuid():N}.bak";
                File.Replace(temporaryPath, fullPath, backupPath);
                return backupPath;
            }
            File.Move(temporaryPath, fullPath);
            return null;
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}

using System.Diagnostics;
using System.Net.NetworkInformation;
using DtxNodeCheck.Configuration;
using DtxNodeCheck.Inventory;

namespace DtxNodeCheck.Core;

internal static class NodeRoleDetector
{
    public static IReadOnlyList<NodeDetectionCandidate> Detect(string configPath)
    {
        var context = DetectionContext.Read();
        var candidates = new List<NodeDetectionCandidate>();

        foreach (var role in Enum.GetValues<DtxNodeRole>())
        {
            var reasons = new List<string>();
            var score = ScoreConfiguredProfile(configPath, role, context, reasons);
            score += ScoreGeneralSignals(role, context, reasons);
            candidates.Add(new NodeDetectionCandidate(role, score, reasons));
        }

        return candidates;
    }

    private static int ScoreConfiguredProfile(string configPath, DtxNodeRole role, DetectionContext context, List<string> reasons)
    {
        if (!File.Exists(configPath))
        {
            reasons.Add($"Config non trovata: {configPath}");
            return 0;
        }

        try
        {
            var configuration = CheckConfigurationLoader.Load(configPath);
            var profile = configuration.BuildProfile(ToNodeKind(role));
            var score = 0;

            foreach (var check in profile.RequiredDirectories)
            {
                if (Directory.Exists(check.Path))
                {
                    score += check.Required ? 3 : 1;
                    reasons.Add($"Directory presente: {check.Path}");
                }
            }

            foreach (var check in profile.RequiredFiles)
            {
                if (File.Exists(check.Path))
                {
                    score += check.Required ? 3 : 1;
                    reasons.Add($"File presente: {check.Path}");
                }
            }

            foreach (var check in profile.RequiredProcesses)
            {
                var processName = Path.GetFileNameWithoutExtension(check.Name);
                if (context.Processes.Contains(processName))
                {
                    score += check.Required ? 3 : 1;
                    reasons.Add($"Processo in esecuzione: {processName}");
                }
            }

            foreach (var check in profile.RequiredServices)
            {
                if (context.Services.TryGetValue(check.Name, out var status))
                {
                    score += check.Required ? 4 : 2;
                    reasons.Add($"Servizio presente: {check.Name} ({status})");
                }
            }

            foreach (var check in profile.RequiredTcpListeners)
            {
                if (context.TcpPorts.Contains(check.Port))
                {
                    score += check.Required ? 4 : 2;
                    reasons.Add($"Listener TCP locale presente: {check.Port}");
                }
            }

            return score;
        }
        catch (Exception ex) when (ex is ConfigurationException or IOException or UnauthorizedAccessException)
        {
            reasons.Add($"Config non utilizzabile per detection: {ex.Message}");
            return 0;
        }
    }

    private static int ScoreGeneralSignals(DtxNodeRole role, DetectionContext context, List<string> reasons)
    {
        var score = 0;
        var hasDtxProgram = context.InstalledPrograms.Any(name => name.Contains("dtx", StringComparison.OrdinalIgnoreCase));
        var hasDtxProcess = context.Processes.Any(name => name.Contains("dtx", StringComparison.OrdinalIgnoreCase));
        var dtxServices = context.Services.Keys.Where(name => name.Contains("dtx", StringComparison.OrdinalIgnoreCase)).ToArray();

        if (role == DtxNodeRole.Core && dtxServices.Length > 0)
        {
            score += 3;
            reasons.Add($"Servizi DTX locali rilevati: {string.Join(", ", dtxServices.Take(4))}");
        }

        if ((role == DtxNodeRole.Workstation || role == DtxNodeRole.Client) && (hasDtxProgram || hasDtxProcess))
        {
            score += 1;
            reasons.Add("Componenti DTX locali rilevati.");
        }

        return score;
    }

    private static NodeKind ToNodeKind(DtxNodeRole role) =>
        role switch
        {
            DtxNodeRole.Core => NodeKind.Core,
            DtxNodeRole.Workstation => NodeKind.Workstation,
            DtxNodeRole.Client => NodeKind.Client,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
        };

    private sealed record DetectionContext(
        HashSet<string> Processes,
        Dictionary<string, string> Services,
        HashSet<int> TcpPorts,
        IReadOnlyList<string> InstalledPrograms)
    {
        public static DetectionContext Read() =>
            new(ReadProcesses(), ReadServices(), ReadTcpPorts(), ReadInstalledPrograms());

        private static HashSet<string> ReadProcesses()
        {
            var processes = Process.GetProcesses();
            try
            {
                return processes
                    .Select(process =>
                    {
                        try
                        {
                            return process.ProcessName;
                        }
                        catch (InvalidOperationException)
                        {
                            return "";
                        }
                    })
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }

        private static Dictionary<string, string> ReadServices()
        {
            return WindowsServiceEnumerator.ListServices()
                .GroupBy(service => service.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Status, StringComparer.OrdinalIgnoreCase);
        }

        private static HashSet<int> ReadTcpPorts()
        {
            try
            {
                return IPGlobalProperties.GetIPGlobalProperties()
                    .GetActiveTcpListeners()
                    .Select(listener => listener.Port)
                    .ToHashSet();
            }
            catch (NetworkInformationException)
            {
                return [];
            }
        }

        private static IReadOnlyList<string> ReadInstalledPrograms()
        {
            var inventory = InventoryRunner.Run();
            return inventory.InstalledPrograms.Select(program => program.DisplayName).ToArray();
        }
    }
}

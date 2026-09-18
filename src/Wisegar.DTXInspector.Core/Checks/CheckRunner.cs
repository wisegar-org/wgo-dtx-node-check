using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Wisegar.DTXInspector.Configuration;
using Wisegar.DTXInspector.Diagnostics;

namespace Wisegar.DTXInspector.Checks;

internal static class CheckRunner
{
    public static CheckRun Run(CheckConfiguration configuration, NodeProfile profile)
    {
        DebugLog.Info("Avvio controlli nodo.", new Dictionary<string, string?>
        {
            ["node"] = profile.Node.Key(),
            ["displayName"] = profile.DisplayName,
            ["directoryChecks"] = profile.RequiredDirectories.Count.ToString(),
            ["fileChecks"] = profile.RequiredFiles.Count.ToString(),
            ["processChecks"] = profile.RequiredProcesses.Count.ToString(),
            ["serviceChecks"] = profile.RequiredServices.Count.ToString(),
            ["tcpListenerChecks"] = profile.RequiredTcpListeners.Count.ToString()
        });

        var results = new List<CheckResult>
        {
            CheckResult.Pass(
                "runtime",
                "Windows",
                "Sistema operativo Windows rilevato.",
                new Dictionary<string, string?>
                {
                    ["os"] = RuntimeInformation.OSDescription,
                    ["architecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
                    ["is64BitOperatingSystem"] = Environment.Is64BitOperatingSystem.ToString()
                }),
            CheckResult.Pass(
                "identity",
                "Machine",
                "Identita' locale letta senza modifiche.",
                new Dictionary<string, string?>
                {
                    ["machineName"] = Environment.MachineName,
                    ["userDomainName"] = Environment.UserDomainName,
                    ["userName"] = Environment.UserName
                })
        };

        AddDirectoryChecks(results, profile.RequiredDirectories);
        AddFileChecks(results, profile.RequiredFiles);
        AddProcessChecks(results, profile.RequiredProcesses);
        AddServiceChecks(results, profile.RequiredServices);
        AddTcpListenerChecks(results, profile.RequiredTcpListeners);
        var network = NetworkChecks.RunAsync(profile).GetAwaiter().GetResult();
        results.AddRange(InfrastructureChecklist.Run(profile, InfrastructureSnapshot.Read(), network));
        results.AddRange(network);

        DebugLog.Info("Controlli nodo completati.", new Dictionary<string, string?>
        {
            ["passed"] = results.Count(result => result.Status == CheckStatus.Pass).ToString(),
            ["warnings"] = results.Count(result => result.Status == CheckStatus.Warning).ToString(),
            ["failed"] = results.Count(result => result.Status == CheckStatus.Fail).ToString()
        });

        return new CheckRun(
            DateTimeOffset.Now,
            Environment.MachineName,
            Environment.UserDomainName,
            Environment.UserName,
            RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture.ToString(),
            configuration.EnvironmentName,
            profile,
            results);
    }

    private static void AddDirectoryChecks(List<CheckResult> results, IReadOnlyList<PathCheck> checks)
    {
        foreach (var check in checks)
        {
            var name = check.Name ?? check.Path;
            var exists = Directory.Exists(check.Path);
            DebugLog.Debug("Controllo directory.", new Dictionary<string, string?>
            {
                ["name"] = name,
                ["path"] = check.Path,
                ["required"] = check.Required.ToString(),
                ["exists"] = exists.ToString()
            });
            results.Add(ResultFromPresence(
                exists,
                check.Required,
                "directory",
                name,
                exists ? "Directory presente." : "Directory non trovata.",
                new Dictionary<string, string?> { ["path"] = check.Path }));
        }
    }

    private static void AddFileChecks(List<CheckResult> results, IReadOnlyList<PathCheck> checks)
    {
        foreach (var check in checks)
        {
            var name = check.Name ?? check.Path;
            var exists = File.Exists(check.Path);
            DebugLog.Debug("Controllo file.", new Dictionary<string, string?>
            {
                ["name"] = name,
                ["path"] = check.Path,
                ["required"] = check.Required.ToString(),
                ["exists"] = exists.ToString()
            });
            results.Add(ResultFromPresence(
                exists,
                check.Required,
                "file",
                name,
                exists ? "File presente." : "File non trovato.",
                new Dictionary<string, string?> { ["path"] = check.Path }));
        }
    }

    private static void AddProcessChecks(List<CheckResult> results, IReadOnlyList<ProcessCheck> checks)
    {
        foreach (var check in checks)
        {
            var processName = Path.GetFileNameWithoutExtension(check.Name);
            var displayName = check.DisplayName ?? check.Name;
            try
            {
                using var processes = new ProcessList(Process.GetProcessesByName(processName));
                var details = new Dictionary<string, string?>
                {
                    ["processName"] = processName,
                    ["count"] = processes.Count.ToString()
                };
                DebugLog.Debug("Controllo processo.", new Dictionary<string, string?>
                {
                    ["name"] = displayName,
                    ["processName"] = processName,
                    ["required"] = check.Required.ToString(),
                    ["count"] = processes.Count.ToString()
                });

                results.Add(ResultFromPresence(
                    processes.Count > 0,
                    check.Required,
                    "process",
                    displayName,
                    processes.Count > 0 ? "Processo in esecuzione." : "Processo non in esecuzione.",
                    details));
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                DebugLog.Exception(ex, $"Errore lettura processo '{displayName}'.");
                results.Add(CheckResult.Warning(
                    "process",
                    displayName,
                    $"Impossibile leggere i processi: {ex.Message}",
                    new Dictionary<string, string?> { ["processName"] = processName }));
            }
        }
    }

    private static void AddServiceChecks(List<CheckResult> results, IReadOnlyList<ServiceCheck> checks)
    {
        var installed = checks.Any(check => check.MatchDisplayName)
            ? Wisegar.DTXInspector.Inventory.WindowsServiceEnumerator.ListServices() : [];
        foreach (var check in checks)
        {
            var displayName = check.DisplayName ?? check.Name;
            var matchesByName = installed.Where(check.Matches).ToArray();
            if (check.MatchDisplayName && matchesByName.Length != 1)
            {
                results.Add(CheckResult.Warning("service", displayName,
                    "Nome visualizzato del servizio non identificato univocamente; verificare versione, installazione e permessi."));
                continue;
            }
            var serviceName = check.MatchDisplayName ? matchesByName[0].Name : check.Name;
            var service = WindowsServiceReader.TryQuery(serviceName);
            DebugLog.Debug("Controllo servizio.", new Dictionary<string, string?>
            {
                ["name"] = displayName,
                ["serviceName"] = check.Name,
                ["required"] = check.Required.ToString(),
                ["exists"] = service.Exists.ToString(),
                ["canQuery"] = service.CanQuery.ToString(),
                ["status"] = service.Status,
                ["message"] = service.Message
            });
            var details = new Dictionary<string, string?>
            {
                ["serviceName"] = serviceName,
                ["matchDisplayName"] = check.MatchDisplayName.ToString(),
                ["status"] = service.Status,
                ["win32Error"] = service.Win32Error?.ToString()
            };

            if (!service.Exists)
            {
                results.Add(ResultFromPresence(
                    false,
                    check.Required,
                    "service",
                    displayName,
                    service.Message,
                    details));
                continue;
            }

            if (!service.CanQuery)
            {
                results.Add(check.Required
                    ? CheckResult.Fail("service", displayName, service.Message, details)
                    : CheckResult.Warning("service", displayName, service.Message, details));
                continue;
            }

            var expected = check.ExpectedStatuses is { Count: > 0 }
                ? check.ExpectedStatuses
                : ["Running"];

            var matches = expected.Any(status => string.Equals(status, service.Status, StringComparison.OrdinalIgnoreCase));
            details["expectedStatuses"] = string.Join(", ", expected);

            results.Add(matches
                ? CheckResult.Pass("service", displayName, "Servizio nello stato atteso.", details)
                : check.Required
                    ? CheckResult.Fail("service", displayName, "Servizio presente ma in stato non atteso.", details)
                    : CheckResult.Warning("service", displayName, "Servizio presente ma in stato non atteso.", details));
        }
    }

    private static void AddTcpListenerChecks(List<CheckResult> results, IReadOnlyList<TcpListenerCheck> checks)
    {
        IPEndPoint[] listeners;
        try
        {
            listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
        }
        catch (NetworkInformationException ex)
        {
            DebugLog.Exception(ex, "Errore lettura listener TCP locali.");
            foreach (var check in checks)
            {
                results.Add(CheckResult.Warning(
                    "tcp-listener",
                    check.Name ?? $"TCP {check.Port}",
                    $"Impossibile leggere i listener TCP locali: {ex.Message}",
                    new Dictionary<string, string?> { ["port"] = check.Port.ToString() }));
            }

            return;
        }

        foreach (var check in checks)
        {
            var displayName = check.Name ?? $"TCP {check.Port}";
            var matching = listeners
                .Where(listener => listener.Port == check.Port && AddressMatches(check.Address, listener.Address))
                .Select(listener => listener.Address.ToString())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var details = new Dictionary<string, string?>
            {
                ["port"] = check.Port.ToString(),
                ["address"] = check.Address,
                ["matchingListeners"] = string.Join(", ", matching)
            };
            DebugLog.Debug("Controllo listener TCP locale.", new Dictionary<string, string?>
            {
                ["name"] = displayName,
                ["port"] = check.Port.ToString(),
                ["address"] = check.Address,
                ["required"] = check.Required.ToString(),
                ["matchCount"] = matching.Length.ToString()
            });

            results.Add(ResultFromPresence(
                matching.Length > 0,
                check.Required,
                "tcp-listener",
                displayName,
                matching.Length > 0 ? "Listener TCP locale presente." : "Listener TCP locale non trovato.",
                details));
        }
    }

    private static bool AddressMatches(string expected, IPAddress actual)
    {
        if (string.IsNullOrWhiteSpace(expected) ||
            expected == "*" ||
            expected == "0.0.0.0" ||
            expected == "::")
        {
            return true;
        }

        return string.Equals(expected, actual.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static CheckResult ResultFromPresence(
        bool present,
        bool required,
        string category,
        string name,
        string message,
        IReadOnlyDictionary<string, string?> details)
    {
        if (present)
        {
            return CheckResult.Pass(category, name, message, details);
        }

        return required
            ? CheckResult.Fail(category, name, message, details)
            : CheckResult.Warning(category, name, message, details);
    }

    private sealed class ProcessList : IDisposable
    {
        private readonly Process[] _processes;

        public ProcessList(Process[] processes)
        {
            _processes = processes;
        }

        public int Count => _processes.Length;

        public void Dispose()
        {
            foreach (var process in _processes)
            {
                process.Dispose();
            }
        }
    }
}

using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Wisegar.DTXInspector.Diagnostics;
using Microsoft.Win32;

namespace Wisegar.DTXInspector.Inventory;

internal static class InventoryRunner
{
    private static readonly string[] CandidateTerms =
    [
        "dtx"
    ];

    public static InventoryRun Run(CancellationToken cancellation = default)
    {
        DebugLog.Info("Avvio inventario PC.");

        var run = new InventoryRun(DateTimeOffset.Now, ReadMachine(), [], [], [], [], [], [], [], []);
        try
        {
            Phase("Schede di rete, indirizzi e DNS locali");
            var snapshot = Wisegar.DTXInspector.Checks.InfrastructureSnapshot.Read();
            run = run with { Adapters = snapshot.Adapters, ObservationErrors = snapshot.Errors };
            Phase("Dischi locali"); run = run with { Drives = ReadDrives() };
            Phase("Processi"); run = run with { Processes = ReadProcesses() };
            Phase("Servizi Windows"); run = run with { Services = WindowsServiceEnumerator.ListServices() };
            Phase("Porte in ascolto"); run = run with { TcpListeners = ReadTcpListeners() };
            Phase("Software installato"); run = run with { InstalledPrograms = ReadInstalledPrograms() };
            Phase("Directory DTX"); run = run with { CandidatePaths = ReadCandidatePaths(cancellation) };
            cancellation.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            run = run with { ObservationErrors = (run.ObservationErrors ?? []).Append("Inventario interrotto: dati parziali, sezioni rimanenti non lette.").ToArray() };
        }

        DebugLog.Info("Inventario PC completato.", new Dictionary<string, string?>
        {
            ["drives"] = run.Drives.Count.ToString(),
            ["processes"] = run.Processes.Count.ToString(),
            ["services"] = run.Services.Count.ToString(),
            ["tcpListeners"] = run.TcpListeners.Count.ToString(),
            ["installedPrograms"] = run.InstalledPrograms.Count.ToString(),
            ["candidatePaths"] = run.CandidatePaths.Count.ToString()
        });

        return run;

        void Phase(string name) { cancellation.ThrowIfCancellationRequested(); DebugLog.Info(name); }
    }

    private static MachineInventory ReadMachine() =>
        new(
            Environment.MachineName,
            Environment.UserDomainName,
            Environment.UserName,
            RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture.ToString(),
            Environment.Is64BitOperatingSystem,
            Environment.SystemDirectory,
            Environment.CurrentDirectory);

    private static IReadOnlyList<DriveInventory> ReadDrives()
    {
        return DriveInfo.GetDrives()
            .Select(drive =>
            {
                if (!drive.IsReady)
                {
                    return new DriveInventory(drive.Name, drive.DriveType.ToString(), false, null, null, null, null);
                }

                return new DriveInventory(
                    drive.Name,
                    drive.DriveType.ToString(),
                    true,
                    drive.DriveFormat,
                    drive.VolumeLabel,
                    drive.TotalSize,
                    drive.AvailableFreeSpace);
            })
            .OrderBy(drive => drive.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<ProcessInventory> ReadProcesses()
    {
        var processes = Process.GetProcesses();
        try
        {
            return processes
                .GroupBy(process => SafeProcessName(process), StringComparer.OrdinalIgnoreCase)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .Select(group => new ProcessInventory(group.Key, group.Count()))
                .OrderBy(process => process.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static string SafeProcessName(Process process)
    {
        try
        {
            return process.ProcessName;
        }
        catch (InvalidOperationException)
        {
            return "";
        }
    }

    private static IReadOnlyList<TcpListenerInventory> ReadTcpListeners()
    {
        try
        {
            return IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners()
                .Select(listener => new TcpListenerInventory(listener.Address.ToString(), listener.Port))
                .OrderBy(listener => listener.Port)
                .ThenBy(listener => listener.Address, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (NetworkInformationException ex)
        {
            DebugLog.Exception(ex, "Errore lettura listener TCP durante inventario.");
            return [];
        }
    }

    private static IReadOnlyList<InstalledProgramInventory> ReadInstalledPrograms()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var programs = new List<InstalledProgramInventory>();
        ReadInstalledProgramsFromHive(programs, RegistryHive.LocalMachine, RegistryView.Registry64);
        ReadInstalledProgramsFromHive(programs, RegistryHive.LocalMachine, RegistryView.Registry32);
        ReadInstalledProgramsFromHive(programs, RegistryHive.CurrentUser, RegistryView.Registry64);
        ReadInstalledProgramsFromHive(programs, RegistryHive.CurrentUser, RegistryView.Registry32);

        return programs
            .GroupBy(program => $"{program.DisplayName}|{program.DisplayVersion}|{program.Publisher}|{program.InstallLocation}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(program => program.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    [SupportedOSPlatform("windows")]
    private static void ReadInstalledProgramsFromHive(List<InstalledProgramInventory> programs, RegistryHive hive, RegistryView view)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", writable: false);
            if (uninstall is null)
            {
                return;
            }

            foreach (var subKeyName in uninstall.GetSubKeyNames())
            {
                using var appKey = uninstall.OpenSubKey(subKeyName, writable: false);
                var displayName = appKey?.GetValue("DisplayName") as string;
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    continue;
                }

                programs.Add(new InstalledProgramInventory(
                    displayName,
                    appKey?.GetValue("DisplayVersion") as string,
                    appKey?.GetValue("Publisher") as string,
                    appKey?.GetValue("InstallLocation") as string));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            DebugLog.Exception(ex, $"Errore lettura programmi installati da {hive}/{view}.");
        }
    }

    private static IReadOnlyList<CandidatePathInventory> ReadCandidatePaths(CancellationToken cancellation)
    {
        var roots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
            }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var candidates = new List<CandidatePathInventory>();
        foreach (var root in roots)
        {
            AddCandidateDirectories(candidates, root, maxDepth: 4, maxCandidates: 300, cancellation);
        }

        return candidates
            .GroupBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddCandidateDirectories(List<CandidatePathInventory> candidates, string root, int maxDepth, int maxCandidates, CancellationToken cancellation)
    {
        if (!Directory.Exists(root) || candidates.Count >= maxCandidates)
        {
            return;
        }

        var pending = new Queue<(string Path, int Depth)>();
        pending.Enqueue((root, 0));

        while (pending.Count > 0 && candidates.Count < maxCandidates)
        {
            cancellation.ThrowIfCancellationRequested();
            var current = pending.Dequeue();
            IEnumerable<string> directories;
            try
            {
                directories = Directory.GetDirectories(current.Path);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                DebugLog.Debug("Directory non leggibile durante inventario.", new Dictionary<string, string?>
                {
                    ["path"] = current.Path,
                    ["error"] = ex.Message
                });
                continue;
            }

            foreach (var directory in directories)
            {
                cancellation.ThrowIfCancellationRequested();
                // Do not follow junctions to other volumes or network locations.
                try { if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue; }
                catch (IOException) { continue; }
                catch (UnauthorizedAccessException) { continue; }
                var name = Path.GetFileName(directory);
                if (CandidateTerms.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase)))
                {
                    candidates.Add(new CandidatePathInventory(name, directory, "Directory"));
                }

                if (current.Depth + 1 < maxDepth && candidates.Count < maxCandidates)
                {
                    pending.Enqueue((directory, current.Depth + 1));
                }
            }
        }
    }
}

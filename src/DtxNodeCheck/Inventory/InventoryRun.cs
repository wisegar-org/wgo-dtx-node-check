using DtxNodeCheck.Reporting;

namespace DtxNodeCheck.Inventory;

internal sealed record InventoryRun(
    DateTimeOffset StartedAt,
    MachineInventory Machine,
    IReadOnlyList<DriveInventory> Drives,
    IReadOnlyList<ProcessInventory> Processes,
    IReadOnlyList<ServiceInventory> Services,
    IReadOnlyList<TcpListenerInventory> TcpListeners,
    IReadOnlyList<InstalledProgramInventory> InstalledPrograms,
    IReadOnlyList<CandidatePathInventory> CandidatePaths) : IRunResult
{
    public int ExitCode => ExitCodes.Success;
}

internal sealed record MachineInventory(
    string MachineName,
    string UserDomainName,
    string UserName,
    string OperatingSystemDescription,
    string ProcessArchitecture,
    bool Is64BitOperatingSystem,
    string SystemDirectory,
    string CurrentDirectory);

internal sealed record DriveInventory(
    string Name,
    string DriveType,
    bool IsReady,
    string? DriveFormat,
    string? VolumeLabel,
    long? TotalSizeBytes,
    long? AvailableFreeSpaceBytes);

internal sealed record ProcessInventory(
    string Name,
    int Count);

internal sealed record ServiceInventory(
    string Name,
    string DisplayName,
    string Status);

internal sealed record TcpListenerInventory(
    string Address,
    int Port);

internal sealed record InstalledProgramInventory(
    string DisplayName,
    string? DisplayVersion,
    string? Publisher,
    string? InstallLocation);

internal sealed record CandidatePathInventory(
    string Name,
    string Path,
    string Kind);

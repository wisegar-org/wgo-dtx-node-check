using System.Runtime.InteropServices;
using Wisegar.DTXInspector.Diagnostics;

namespace Wisegar.DTXInspector.Inventory;

internal static class WindowsServiceEnumerator
{
    private const uint ScManagerEnumerateService = 0x0004;
    private const uint ServiceWin32 = 0x00000030;
    private const uint ServiceStateAll = 0x00000003;
    private const int ScEnumProcessInfo = 0;
    private const int ErrorMoreData = 234;

    public static IReadOnlyList<ServiceInventory> ListServices()
    {
        var manager = OpenSCManager(null, null, ScManagerEnumerateService);
        if (manager == IntPtr.Zero)
        {
            var error = Marshal.GetLastPInvokeError();
            DebugLog.Debug("OpenSCManager fallito per enumerazione servizi.", new Dictionary<string, string?>
            {
                ["win32Error"] = error.ToString()
            });
            return [];
        }

        try
        {
            var bytesNeeded = 0;
            var servicesReturned = 0;
            var resumeHandle = 0;
            EnumServicesStatusEx(
                manager,
                ScEnumProcessInfo,
                ServiceWin32,
                ServiceStateAll,
                IntPtr.Zero,
                0,
                out bytesNeeded,
                out servicesReturned,
                ref resumeHandle,
                null);

            var error = Marshal.GetLastPInvokeError();
            if (error != ErrorMoreData || bytesNeeded <= 0)
            {
                DebugLog.Debug("EnumServicesStatusEx non ha restituito servizi.", new Dictionary<string, string?>
                {
                    ["win32Error"] = error.ToString(),
                    ["bytesNeeded"] = bytesNeeded.ToString()
                });
                return [];
            }

            var buffer = Marshal.AllocHGlobal(bytesNeeded);
            try
            {
                resumeHandle = 0;
                var ok = EnumServicesStatusEx(
                    manager,
                    ScEnumProcessInfo,
                    ServiceWin32,
                    ServiceStateAll,
                    buffer,
                    bytesNeeded,
                    out _,
                    out servicesReturned,
                    ref resumeHandle,
                    null);

                if (!ok)
                {
                    error = Marshal.GetLastPInvokeError();
                    DebugLog.Debug("EnumServicesStatusEx fallito.", new Dictionary<string, string?>
                    {
                        ["win32Error"] = error.ToString()
                    });
                    return [];
                }

                var itemSize = Marshal.SizeOf<EnumServiceStatusProcess>();
                var services = new List<ServiceInventory>(servicesReturned);
                for (var i = 0; i < servicesReturned; i++)
                {
                    var itemPtr = IntPtr.Add(buffer, i * itemSize);
                    var item = Marshal.PtrToStructure<EnumServiceStatusProcess>(itemPtr);
                    services.Add(new ServiceInventory(
                        item.ServiceName,
                        item.DisplayName,
                        ServiceStateName(item.ServiceStatus.CurrentState),
                        (int)item.ServiceStatus.ProcessId));
                }

                return services
                    .OrderBy(service => service.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            CloseServiceHandle(manager);
        }
    }

    private static string ServiceStateName(uint state) =>
        state switch
        {
            1 => "Stopped",
            2 => "StartPending",
            3 => "StopPending",
            4 => "Running",
            5 => "ContinuePending",
            6 => "PausePending",
            7 => "Paused",
            _ => $"Unknown({state})"
        };

    [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint desiredAccess);

    [DllImport("advapi32.dll", EntryPoint = "EnumServicesStatusExW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumServicesStatusEx(
        IntPtr serviceControlManager,
        int infoLevel,
        uint serviceType,
        uint serviceState,
        IntPtr services,
        int bufferSize,
        out int bytesNeeded,
        out int servicesReturned,
        ref int resumeHandle,
        string? groupName);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseServiceHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct EnumServiceStatusProcess
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string ServiceName;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string DisplayName;

        public ServiceStatusProcess ServiceStatus;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatusProcess
    {
        public uint ServiceType;
        public uint CurrentState;
        public uint ControlsAccepted;
        public uint Win32ExitCode;
        public uint ServiceSpecificExitCode;
        public uint CheckPoint;
        public uint WaitHint;
        public uint ProcessId;
        public uint ServiceFlags;
    }
}

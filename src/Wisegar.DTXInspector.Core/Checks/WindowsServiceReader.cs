using System.Runtime.InteropServices;
using Wisegar.DTXInspector.Diagnostics;

namespace Wisegar.DTXInspector.Checks;

internal static class WindowsServiceReader
{
    private const uint ScManagerConnect = 0x0001;
    private const uint ServiceQueryStatus = 0x0004;
    private const int ScStatusProcessInfo = 0;
    private const int ErrorServiceDoesNotExist = 1060;

    public static ServiceQueryResult TryQuery(string serviceName)
    {
        var manager = OpenSCManager(null, null, ScManagerConnect);
        if (manager == IntPtr.Zero)
        {
            var error = Marshal.GetLastPInvokeError();
            DebugLog.Debug("OpenSCManager fallito.", new Dictionary<string, string?>
            {
                ["serviceName"] = serviceName,
                ["win32Error"] = error.ToString()
            });
            return ServiceQueryResult.QueryFailed(
                $"Impossibile aprire il Service Control Manager in sola lettura. Errore Win32 {error}.",
                error);
        }

        try
        {
            var service = OpenService(manager, serviceName, ServiceQueryStatus);
            if (service == IntPtr.Zero)
            {
                var error = Marshal.GetLastPInvokeError();
                return error == ErrorServiceDoesNotExist
                    ? ServiceQueryResult.NotFound("Servizio non installato.", error)
                    : ServiceQueryResult.QueryFailed($"Impossibile interrogare il servizio. Errore Win32 {error}.", error);
            }

            try
            {
                var status = new ServiceStatusProcess();
                var size = Marshal.SizeOf<ServiceStatusProcess>();
                var ok = QueryServiceStatusEx(service, ScStatusProcessInfo, ref status, size, out _);
                if (!ok)
                {
                    var error = Marshal.GetLastPInvokeError();
                    DebugLog.Debug("QueryServiceStatusEx fallito.", new Dictionary<string, string?>
                    {
                        ["serviceName"] = serviceName,
                        ["win32Error"] = error.ToString()
                    });
                    return ServiceQueryResult.QueryFailed($"Impossibile leggere lo stato del servizio. Errore Win32 {error}.", error);
                }

                return ServiceQueryResult.Found(ServiceStateName(status.CurrentState));
            }
            finally
            {
                CloseServiceHandle(service);
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

    [DllImport("advapi32.dll", EntryPoint = "OpenServiceW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenService(IntPtr serviceControlManager, string serviceName, uint desiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceStatusEx(
        IntPtr service,
        int infoLevel,
        ref ServiceStatusProcess buffer,
        int bufferSize,
        out int bytesNeeded);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseServiceHandle(IntPtr handle);

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

internal sealed record ServiceQueryResult(
    bool Exists,
    bool CanQuery,
    string? Status,
    string Message,
    int? Win32Error)
{
    public static ServiceQueryResult Found(string status) =>
        new(true, true, status, "Servizio interrogato in sola lettura.", null);

    public static ServiceQueryResult NotFound(string message, int error) =>
        new(false, true, null, message, error);

    public static ServiceQueryResult QueryFailed(string message, int error) =>
        new(true, false, null, message, error);
}

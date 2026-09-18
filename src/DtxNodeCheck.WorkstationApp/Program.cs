using Avalonia;
using DtxNodeCheck.Core;
using DtxNodeCheck.Desktop.Shared;

namespace DtxNodeCheck.WorkstationApp;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppSettings.NodeRole = DtxNodeRole.Workstation;
        AppSettings.ApplicationName = "DTX Node Check Workstation";
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}

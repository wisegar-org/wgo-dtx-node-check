using Avalonia;
using DtxNodeCheck.Desktop.Shared;

namespace DtxNodeCheck.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppSettings.NodeRole = null;
        AppSettings.InferNodeRole = true;
        AppSettings.ApplicationName = "WGO DTX Node Check";
        AppSettings.ConfigFileName = "dtx-node-check.json";
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<DtxNodeCheck.Desktop.Shared.App>()
            .UsePlatformDetect()
            .LogToTrace();
}

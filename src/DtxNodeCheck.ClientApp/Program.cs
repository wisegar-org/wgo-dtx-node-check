using Avalonia;
using DtxNodeCheck.Core;
using DtxNodeCheck.Desktop.Shared;

namespace DtxNodeCheck.ClientApp;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppSettings.NodeRole = DtxNodeRole.Client;
        AppSettings.ApplicationName = "DTX Node Check Client";
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}

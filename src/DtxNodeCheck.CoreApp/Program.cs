using Avalonia;
using DtxNodeCheck.Core;
using DtxNodeCheck.Desktop.Shared;

namespace DtxNodeCheck.CoreApp;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppSettings.NodeRole = DtxNodeRole.Core;
        AppSettings.ApplicationName = "DTX Node Check Core";
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}

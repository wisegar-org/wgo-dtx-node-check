using Avalonia;

namespace Wisegar.DTXInspector.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Wisegar.DTXInspector.Desktop.App>()
            .UsePlatformDetect()
            .LogToTrace();
}

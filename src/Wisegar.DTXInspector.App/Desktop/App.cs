using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Wisegar.DTXInspector.Desktop;

public sealed class App : Application
{
    public override void Initialize()
    {
        DesktopTheme.Install(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace DtxNodeCheck.Desktop.Shared;

public sealed class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(
                AppSettings.NodeRole,
                AppSettings.ApplicationName,
                AppSettings.ConfigFileName,
                AppSettings.InferNodeRole);
        }

        base.OnFrameworkInitializationCompleted();
    }
}

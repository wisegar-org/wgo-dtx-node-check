using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using DtxNodeCheck.Core;
using DtxNodeCheck.Desktop.Shared;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        AppBuilder.Configure<Application>().UsePlatformDetect().SetupWithoutStarting();
        var window = new MainWindow(null, "WGO DTX Node Check", "dtx-node-check.json", true);
        try
        {
            Assert(!Field<Button>(window, "_runButton").IsEnabled, "Tests require a node at startup");
            Assert(Field<Button>(window, "_openConfigButton").IsEnabled, "Config accessible before detection");
            Assert(Field<Button>(window, "_reloadConfigButton").IsEnabled, "Detection can be retried before selection");
            Busy(window, true);
            Assert(!Field<ComboBox>(window, "_nodeSelector").IsEnabled, "Selection locked during work");
            Busy(window, false);
            Assert(!Field<Button>(window, "_runButton").IsEnabled, "Inventory completion must not enable tests without a node");
            Assert(Field<Button>(window, "_openConfigButton").IsEnabled, "Config restored after work");
            foreach (var role in Enum.GetValues<DtxNodeRole>())
            {
                Field<ComboBox>(window, "_nodeSelector").SelectedIndex = (int)role;
                var paths = Field<NodeCheckPaths>(window, "_paths");
                Assert(Path.GetFileName(paths.ConfigPath) == "dtx-node-check.json", "All roles use the unified config");
                Assert(paths.ReportPath.Contains($"DTX-{role.ToString().ToLowerInvariant()}-Report.html"), "Report follows selected role");
                Busy(window, true);
                Busy(window, false);
                Assert(Field<Button>(window, "_runButton").IsEnabled == OperatingSystem.IsWindows(), "Tests enabled for selected role only on Windows");
            }
        }
        finally { window.Close(); }

        var legacy = new MainWindow(DtxNodeRole.Core, "Legacy", null, false);
        try { Assert(!Field<ComboBox>(legacy, "_nodeSelector").IsEnabled, "Legacy role remains fixed"); }
        finally { legacy.Close(); }
        Console.WriteLine("PASS: unified desktop selection, config access, busy state, role paths and legacy compatibility.");
    }

    // Exercise the completion transition without running inventory or opening a browser.
    private static void Busy(MainWindow window, bool busy) =>
        typeof(MainWindow).GetMethod("SetBusy", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, [busy, "Smoke test"]);

    private static T Field<T>(MainWindow window, string name) =>
        (T)typeof(MainWindow).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(window)!;

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

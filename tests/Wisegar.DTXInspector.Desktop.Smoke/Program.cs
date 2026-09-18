using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Wisegar.DTXInspector.Core;
using Wisegar.DTXInspector.Desktop;

namespace Wisegar.DTXInspector.Desktop.Smoke;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        AppBuilder.Configure<Application>().UsePlatformDetect().SetupWithoutStarting();
        InventorySettingsChecks.Run();
        InfrastructureChecks.Run();
        var window = new MainWindow();
        try
        {
            Assert(!Field<Button>(window, "_runButton").IsEnabled, "Tests require a node at startup");
            Assert(!Field<Button>(window, "_inventorySettingsButton").IsEnabled, "Settings import requires selected node");
            Assert(Field<ComboBox>(window, "_nodeSelector").SelectedIndex == -1, "No role is automatically selected");
            Assert(Field<TextBlock>(window, "_status").Text!.Contains("Seleziona"), "Startup requests a role");
            typeof(MainWindow).GetMethod("ReloadPlan", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            Assert(Field<ComboBox>(window, "_nodeSelector").SelectedIndex == -1, "Reload cannot infer a role");
            Assert(Field<TextBlock>(window, "_status").Text!.Contains("Seleziona"), "Reload requests a role");
            Assert(Field<Button>(window, "_openConfigButton").IsEnabled, "Config accessible before selection");
            Assert(Field<Button>(window, "_reloadConfigButton").IsEnabled, "Reload accessible before selection");
            Busy(window, true);
            Assert(!Field<ComboBox>(window, "_nodeSelector").IsEnabled, "Selection locked during work");
            Busy(window, false);
            Assert(!Field<Button>(window, "_runButton").IsEnabled, "Inventory completion must not enable tests without a node");
            Assert(Field<Button>(window, "_openConfigButton").IsEnabled, "Config restored after work");
            foreach (var role in Enum.GetValues<DtxNodeRole>())
            {
                Field<ComboBox>(window, "_nodeSelector").SelectedIndex = (int)role;
                var paths = Field<NodeCheckPaths>(window, "_paths");
                Assert(Path.GetFileName(paths.ConfigPath) == "appsettings.json", "All roles use the unified config");
                var checks = NodeCheckService.GetPlannedChecks(paths.ConfigPath, role);
                Assert(checks.Count >= 2, "Every role loads successfully from the distributed config");
                Assert(checks.Any(check => check.Category == "process" && check.Name == "DTX Studio Clinic")
                    == (role != DtxNodeRole.Core), "Node-specific checks are preserved in unified config");
                Assert(paths.ReportPath.Contains($"DTX-{role.ToString().ToLowerInvariant()}-Report.html"), "Report follows selected role");
                Busy(window, true);
                Assert(!Field<Button>(window, "_inventorySettingsButton").IsEnabled, "Settings import disabled during work");
                Busy(window, false);
                Assert(Field<Button>(window, "_inventorySettingsButton").IsEnabled, "Settings import available after node selection");
                Assert(Field<Button>(window, "_runButton").IsEnabled == OperatingSystem.IsWindows(), "Tests enabled for selected role only on Windows");
            }
            Field<ComboBox>(window, "_nodeSelector").SelectedIndex = -1;
            Assert(!Field<Button>(window, "_runButton").IsEnabled, "Clearing selection blocks tests");
            Assert(!Field<Button>(window, "_inventorySettingsButton").IsEnabled, "Clearing selection blocks settings import");
            Assert(Field<TextBlock>(window, "_status").Text!.Contains("Seleziona"), "Clearing selection requests a new choice");
            Assert(typeof(MainWindow).GetField("_paths", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window) is null,
                "Clearing selection discards previous role paths");
        }
        finally { window.Close(); }

        var confirmation = (Window)typeof(MainWindow).GetMethod("CreateInventorySettingsConfirmation",
            BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [DtxNodeRole.Core, "config.json"])!;
        try
        {
            var content = (StackPanel)confirmation.Content!;
            var warning = (TextBlock)content.Children[0];
            Assert(warning.Text!.Contains("perse") && warning.Text.Contains("Core"), "Confirmation warns about settings loss and selected node");
            var buttons = (StackPanel)content.Children[1];
            Assert(((Button)buttons.Children[0]).IsDefault && ((Button)buttons.Children[0]).IsCancel,
                "Cancel is default and Escape action");
            Assert(!((Button)buttons.Children[1]).IsDefault, "Replacement requires explicit selection");
        }
        finally { confirmation.Close(); }

        Console.WriteLine("PASS: single desktop app, node selection, config access, busy state and role paths.");
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

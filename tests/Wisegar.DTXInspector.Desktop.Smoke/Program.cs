using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Wisegar.DTXInspector.Core;
using Wisegar.DTXInspector.Desktop;

namespace Wisegar.DTXInspector.Desktop.Smoke;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        AppBuilder.Configure<Application>().UsePlatformDetect().SetupWithoutStarting();
        Application.Current!.Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
        InventorySettingsChecks.Run(); InfrastructureChecks.Run(); DtxInspectionChecks.Run();
        GuidedConfigurationChecks.Run();
        var window = new MainWindow();
        try
        {
            var tabs = Field<TabControl>(window, "_tabs");
            Assert(tabs.Items.Count == 4, "Four independent workspaces");
            Assert(tabs.SelectedIndex == -1, "No inferred role at startup");
            Assert(!Field<Button>(window, "_runButton").IsEnabled, "Tests blocked until manual selection");
            var spaces = Field<NodeWorkspace[]>(window, "_workspaces");
            tabs.SelectedIndex = 1;
            Assert(Field<Button>(window, "_runButton").IsEnabled, "Explicit workstation selection enables tests");
            var result = new NodeCheckExecutionResult(0, "workstation-report.html", "", [
                new("infrastructure", "DNS", "Warning", "Da verificare", new Dictionary<string, string?> { ["scope"] = "locale" }),
                new("service", "DTX", "Pass", "In esecuzione")]);
            spaces[1].Complete(result); spaces[1].Search.Text = "DNS"; spaces[1].Filter.SelectedIndex = 2;
            spaces[1].AddActivity(new(DateTimeOffset.Now, "INFO", "Test", "Evento workstation"));
            tabs.SelectedIndex = 0;
            Assert(spaces[0].ReportPath is null, "Core cannot open workstation report");
            Assert(!Field<Button>(window, "_openReportButton").IsEnabled, "Report button belongs to selected context");
            typeof(MainWindow).GetField("_busy", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, true);
            tabs.SelectedIndex = 3;
            Assert(spaces[3].Role is null, "PC inspection has no DTX role");
            Assert(!Field<Button>(window, "_inventoryButton").IsEnabled, "Only one global operation can run");
            Assert(!Field<Button>(window, "_configureButton").IsVisible, "PC inspection has no node configuration");
            spaces[1].Complete(result with { ReportPath = "workstation-new.html" });
            Assert(spaces[3].ReportPath is null, "Completion while another tab is open stays with origin");
            typeof(MainWindow).GetField("_busy", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, false);
            tabs.SelectedIndex = 1;
            Assert(spaces[1].Search.Text == "DNS" && spaces[1].Filter.SelectedIndex == 2, "Filters survive tab switching and completion");
            Assert(spaces[1].ReportPath == "workstation-new.html", "Report retained by originating tab");
            Assert(Field<Button>(window, "_openReportButton").IsEnabled, "Originating report accessible");
            foreach (var size in new[] { new Size(800, 620), new Size(1100, 820) })
            {
                var content = (Grid)window.Content!;
                content.Measure(size); content.Arrange(new Rect(size));
                var toolbar = (WrapPanel)content.Children[1];
                Assert(toolbar.Children.Where(x => x.IsVisible).All(x => x.Bounds.Right <= size.Width), "Toolbar fits minimum and default size");
            }
            window.Show(); Dispatcher.UIThread.RunJobs();
        }
        finally { window.Close(); }
        var neutral = new MainWindow();
        try { neutral.Show(); Dispatcher.UIThread.RunJobs(); Assert(Field<TabControl>(neutral, "_tabs").SelectedIndex == -1, "Showing window never selects a node"); }
        finally { neutral.Close(); }
        foreach (var role in Enum.GetValues<DtxNodeRole>())
        {
            var wizard = new ConfigurationWizard(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), role);
            try
            {
                wizard.Show(); Dispatcher.UIThread.RunJobs();
                var draft = Field<ConfigurationDraft>(wizard, "_draft");
                Assert(draft.DescribeChanges() == "Nessuna modifica.", "Opening wizard must not alter saved values in draft");
                for (var step = 1; step <= 4; step++)
                    typeof(ConfigurationWizard).GetMethod("ShowStep", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(wizard, [step]);
                Assert(Field<int>(wizard, "_step") == 4, "All roles can navigate to the review without editing JSON");
                Assert(Field<CheckBox>(wizard, "_approve").IsChecked != true, "Review requires explicit approval");
                Assert(draft.DescribeChanges() == "Nessuna modifica.", "Navigating pages must preserve settings");
            }
            finally { typeof(ConfigurationWizard).GetField("_closingConfirmed", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(wizard, true); wizard.Close(); }
        }
        Console.WriteLine("PASS: tabs, manual selection, independent state, filters, busy guards and configuration drafts.");
    }

    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

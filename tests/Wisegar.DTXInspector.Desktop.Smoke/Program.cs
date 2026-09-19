using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Wisegar.DTXInspector.Core;
using Wisegar.DTXInspector.Desktop;

namespace Wisegar.DTXInspector.Desktop.Smoke;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        AppBuilder.Configure<Application>().UsePlatformDetect().SetupWithoutStarting();
        DesktopTheme.Install(Application.Current!);
        InventorySettingsChecks.Run(); InfrastructureChecks.Run(); DtxInspectionChecks.Run();
        GuidedConfigurationChecks.Run();
        var window = new MainWindow();
        try
        {
            var selector = Field<ComboBox>(window, "_nodeSelector");
            Assert(selector.Items.Count == 5, "DTX inspection is a fifth selector entry");
            Assert(selector.SelectedIndex == -1, "No inferred role at startup");
            Assert(!Field<Button>(window, "_runButton").IsEnabled, "Tests blocked until manual selection");
            var spaces = Field<NodeWorkspace[]>(window, "_workspaces");
            selector.SelectedIndex = 1;
            Assert(Field<Button>(window, "_runButton").IsEnabled, "Explicit workstation selection enables tests");
            var result = new NodeCheckExecutionResult(0, "workstation-report.html", "", [
                new("infrastructure", "DNS", "Warning", "Da verificare", new Dictionary<string, string?> { ["scope"] = "locale" }),
                new("service", "DTX", "Pass", "In esecuzione")]);
            spaces[1].Complete(result); spaces[1].Search.Text = "DNS"; spaces[1].Filter.SelectedIndex = 2;
            spaces[1].AddActivity(new(DateTimeOffset.Now, "INFO", "Test", "Evento workstation"));
            selector.SelectedIndex = 0;
            Assert(spaces[0].ReportPath is null, "Core cannot open workstation report");
            Assert(!Field<Button>(window, "_openReportButton").IsEnabled, "Report button belongs to selected context");
            typeof(MainWindow).GetField("_busy", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, true);
            selector.SelectedIndex = 3;
            Assert(spaces[3].Role is null, "PC inspection has no DTX role");
            Assert(!Field<Button>(window, "_inventoryButton").IsEnabled, "Only one global operation can run");
            Assert(!Field<Button>(window, "_configureButton").IsVisible, "PC inspection has no node configuration");
            spaces[1].Complete(result with { ReportPath = "workstation-new.html" });
            Assert(spaces[3].ReportPath is null, "Completion while another tab is open stays with origin");
            typeof(MainWindow).GetField("_busy", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, false);
            selector.SelectedIndex = 1;
            Assert(spaces[1].Search.Text == "DNS" && spaces[1].Filter.SelectedIndex == 2, "Filters survive context switching and completion");
            Assert(spaces[1].ReportPath == "workstation-new.html", "Report retained by originating context");
            Assert(Field<Button>(window, "_openReportButton").IsEnabled, "Originating report accessible");
            selector.SelectedIndex = 4;
            var inspectionRoles = Field<ComboBox>(window, "_inspectionRoleSelector");
            Assert(inspectionRoles.SelectedIndex == -1 && !Field<Button>(window, "_runButton").IsEnabled, "Inspection cannot infer the previously selected workstation role");
            Assert(!Field<Button>(window, "_inventoryButton").IsVisible, "DTX inspection cannot execute generic PC inventory");
            inspectionRoles.SelectedIndex = 1;
            var inspections = Field<NodeWorkspace[]>(window, "_inspectionWorkspaces");
            Assert(inspections[1].IsVisible && inspections[1].IsDtxInspection && inspections[1].Role == DtxNodeRole.Workstation, "Explicit inspection role uses its own workspace");
            Assert(Field<Button>(window, "_runButton").IsEnabled && !Field<Button>(window, "_openReportButton").IsEnabled, "Inspection enabled without borrowing the normal workstation report");
            inspections[1].Complete(result with { ReportPath = "inspection-workstation.html" });
            inspectionRoles.SelectedIndex = 0;
            Assert(!Field<Button>(window, "_openReportButton").IsEnabled, "Inspection reports remain separate by role");
            inspectionRoles.SelectedIndex = 1;
            Assert(Field<Button>(window, "_openReportButton").IsEnabled, "Returning to inspection role restores its report");
            selector.SelectedIndex = 1;
            Assert(spaces[1].ReportPath == "workstation-new.html" && !inspections[1].IsVisible, "Normal checks keep their report separate from DTX inspection");
            foreach (var size in new[] { new Size(800, 620), new Size(960, 700) })
            {
                var content = (Grid)window.Content!;
                content.Measure(size); content.Arrange(new Rect(size));
                var toolbar = (WrapPanel)content.Children[1];
                Assert(toolbar.Children.Where(x => x.IsVisible).All(x => x.Bounds.Right <= size.Width), "Toolbar fits minimum and default size");
            }
            window.Show(); Dispatcher.UIThread.RunJobs();
            if (Environment.GetEnvironmentVariable("DTX_UI_PREVIEW") is { Length: > 0 } previewPath)
            {
                spaces[1].Search.Text = ""; spaces[1].Filter.SelectedIndex = 0;
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                using var preview = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height));
                preview.Render(window);
                preview.Save(previewPath, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                if (Environment.GetEnvironmentVariable("DTX_SELECTOR_PREVIEW") is { Length: > 0 } selectorPreviewPath)
                {
                    selector.SelectedIndex = -1;
                    Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                    using var startup = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height));
                    startup.Render(window);
                    startup.Save(selectorPreviewPath + ".startup.png", Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                    selector.IsDropDownOpen = true;
                    Dispatcher.UIThread.RunJobs();
                    var selectorPopup = selector.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.Popup>().Single();
                    var popupRoot = TopLevel.GetTopLevel(selectorPopup.Child!)!;
                    using var dropdown = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)popupRoot.Bounds.Width, (int)popupRoot.Bounds.Height));
                    dropdown.Render(popupRoot);
                    dropdown.Save(selectorPreviewPath, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                    selector.IsDropDownOpen = false;
                    selector.SelectedIndex = 1;
                }
                if (Environment.GetEnvironmentVariable("DTX_MENU_PREVIEW") is { Length: > 0 } menuPreviewPath)
                {
                    var help = window.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "HelpButton");
                    help.Flyout!.ShowAt(help);
                    Dispatcher.UIThread.RunJobs();
                    var root = TopLevel.GetTopLevel(((MenuFlyout)help.Flyout).Items.OfType<MenuItem>().First())!;
                    using var menuPreview = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)root.Bounds.Width, (int)root.Bounds.Height));
                    menuPreview.Render(root);
                    menuPreview.Save(menuPreviewPath, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                    help.Flyout.Hide();
                }
            }
        }
        finally { window.Close(); }
        var neutral = new MainWindow();
        try
        {
            neutral.Show(); Dispatcher.UIThread.RunJobs();
            var selector = Field<ComboBox>(neutral, "_nodeSelector");
            Assert(selector.SelectedIndex == -1, "Showing window never selects a node");
            Assert(selector.Template is not null && selector.IsEffectivelyVisible && selector.Bounds.Width > 0 && selector.Bounds.Height > 0, "Node selector is rendered and visible");
            Assert(selector.Items.Count == 5, "Selector offers all five contexts");
            var spaces = Field<NodeWorkspace[]>(neutral, "_workspaces");
            Assert(spaces.All(x => !x.IsVisible), "No workspace visible before manual selection");
            for (var index = 0; index < spaces.Length; index++)
            {
                selector.SelectedIndex = index;
                Dispatcher.UIThread.RunJobs();
                Assert(spaces.Count(x => x.IsVisible) == 1 && spaces[index].IsEffectivelyVisible, "Only the chosen workspace is visible");
                Assert(spaces[index].Bounds.Width > 0 && spaces[index].Bounds.Height > 0, "Selected workspace occupies screen space");
            }
            selector.SelectedIndex = -1;
            Assert(spaces.All(x => !x.IsVisible) && !Field<Button>(neutral, "_runButton").IsEnabled, "Clearing selection hides workspaces and blocks node checks");

        }
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
        Console.WriteLine("PASS: selector, manual selection, independent state, filters, busy guards and configuration drafts.");
    }

    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

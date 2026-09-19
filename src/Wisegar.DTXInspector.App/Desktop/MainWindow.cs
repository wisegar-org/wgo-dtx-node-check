using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Wisegar.DTXInspector.Core;

namespace Wisegar.DTXInspector.Desktop;

public sealed class MainWindow : Window
{
    private readonly NodeWorkspace[] _workspaces = [
        new(DtxNodeRole.Core), new(DtxNodeRole.Workstation), new(DtxNodeRole.Client), new(null), new(null, true)
    ];
    private readonly NodeWorkspace[] _inspectionWorkspaces = [new(DtxNodeRole.Core, true), new(DtxNodeRole.Workstation, true), new(DtxNodeRole.Client, true)];
    private readonly ComboBox _inspectionRoleSelector = new() { PlaceholderText = "Scegli ruolo DTX", ItemsSource = new[] { "Core", "Workstation", "Client" }, MinWidth = 175, SelectedIndex = -1 };
    private readonly StackPanel _inspectionRolePanel = new() { Spacing = 5, IsVisible = false };
    private readonly ComboBox _nodeSelector = new() { PlaceholderText = "Seleziona nodo", MinWidth = 190, SelectedIndex = -1 };
    private readonly Grid _workspaceHost = new();
    private readonly Button _runButton = new() { Content = "Esegui test" };
    private readonly Button _inventoryButton = new() { Content = "Avvia scan PC" };
    private readonly Button _configureButton = new() { Content = "Configura nodo…" };
    private readonly Button _openReportButton = new() { Content = "Apri report" };
    private readonly Button _cancelButton = new() { Content = "Interrompi", IsVisible = false };
    private readonly MenuItem _openConfigButton = new() { Header = "Apri appsettings.json" };
    private readonly MenuItem _reloadConfigButton = new() { Header = "Ricarica piano" };
    private readonly MenuItem _inventorySettingsButton = new() { Header = "Impostazioni da inventario…" };
    private readonly TextBlock _status = new() { Text = "Seleziona un nodo per iniziare. Nessun nodo viene scelto automaticamente.", TextWrapping = TextWrapping.Wrap };
    private bool _busy;
    private CancellationTokenSource? _cancellation;
    private NodeWorkspace? Current => _nodeSelector.SelectedIndex == 4 && _inspectionRoleSelector.SelectedIndex >= 0
        ? _inspectionWorkspaces[_inspectionRoleSelector.SelectedIndex]
        : _nodeSelector.SelectedIndex >= 0 ? _workspaces[_nodeSelector.SelectedIndex] : null;
    private static string ConfigPath => NodeCheckService.CreateDefaultConfigPath(AppSettings.ConfigFileName);

    public MainWindow()
    {
        Title = AppSettings.ApplicationName;
        Width = 960; Height = 700; MinWidth = 800; MinHeight = 620;
        DesktopTheme.ToolbarButton(_runButton, "Esegui test", "M 4,2 L 14,8 L 4,14 Z", primary: true);
        DesktopTheme.ToolbarButton(_inventoryButton, "Avvia scan PC", "M 1,2 H 15 V 12 H 9 V 14 H 12 V 16 H 4 V 14 H 7 V 12 H 1 Z M 3,4 V 10 H 13 V 4 Z", primary: true);
        DesktopTheme.ToolbarButton(_configureButton, "Configura nodo", "M 6,0 H 10 L 11,3 L 14,4 L 16,7 V 9 L 13,11 L 12,14 L 9,16 H 7 L 5,13 L 2,12 L 0,9 V 7 L 3,5 L 4,2 Z M 8,5 A 3,3 0 1 0 8,11 A 3,3 0 1 0 8,5 Z");
        DesktopTheme.ToolbarButton(_openReportButton, "Apri report", "M 3,0 H 10 L 15,5 V 16 H 3 Z M 5,7 V 8 H 13 V 7 Z M 5,10 V 11 H 13 V 10 Z M 5,13 V 14 H 11 V 13 Z");
        DesktopTheme.ToolbarButton(_cancelButton, "Interrompi", "M 3,3 H 13 V 13 H 3 Z");
        var advancedMenu = new MenuItem { Header = "_Strumenti avanzati", ItemsSource = new Control[] {
            _openConfigButton, _reloadConfigButton, new Separator(), _inventorySettingsButton
        } };
        var docs = new MenuItem { Header = "_Documentazione" };
        foreach (var (label, url) in new[] {
            ("Supporto", "https://www.dtxstudio.com/en-us/support"),
            ("DTX Studio Go", "https://www.dtxstudio.com/en-us/dtx-studio-go"),
            ("Help e IFU", "https://helpfiles.dtxstudio.com/"),
            ("Installazione", "https://helpfiles.dtxstudio.com/Help/50784413-8047-4699-82f7-d1e9a868909e/4.1/EN/Installation_and_updates.htm")
        })
        {
            var item = new MenuItem { Header = label }; item.Click += (_, _) => OpenFile(url); docs.Items.Add(item);
        }
        var about = new MenuItem { Header = "_Informazioni" };
        about.Click += async (_, _) => await ShowMessageAsync("Informazioni", $"{AppSettings.ApplicationName}\nVersione {typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}\nWindows x64 · self-contained\n\nControlli in sola lettura. DNS/TCP sui target configurati solo durante test richiesti, con timeout.\n\nConfigurazione: {ConfigPath}");
        var log = new MenuItem { Header = "Apri log completo del contesto selezionato" };
        log.Click += (_, _) => { if (Current?.LogPath is { } path) OpenFile(path); };
        var help = new Button {
            Name = "HelpButton", Width = 40, Height = 40, Padding = new Thickness(10),
            Content = new Avalonia.Controls.Shapes.Path {
                Data = Geometry.Parse("M 10,1 A 9,9 0 1 0 10,19 A 9,9 0 1 0 10,1 M 7,7 A 3,3 0 0 1 13,7 C 13,9 10,9 10,12 M 10,14 V 16"),
                Stroke = DesktopTheme.Ink, StrokeThickness = 1.7, Width = 20, Height = 20
            },
            Flyout = new MenuFlyout { ItemsSource = new Control[] { docs, advancedMenu, log, about } }
        };
        help.Classes.Add("secondary");
        Avalonia.Automation.AutomationProperties.SetName(help, "Aiuto");
        ToolTip.SetTip(help, "Aiuto");
        _inspectionRolePanel.Children.Add(new TextBlock { Text = "Ruolo da analizzare", FontSize = 12, Foreground = DesktopTheme.Muted });
        _inspectionRolePanel.Children.Add(_inspectionRoleSelector);
        Avalonia.Automation.AutomationProperties.SetName(_inspectionRoleSelector, "Ruolo da analizzare");
        var toolbar = new WrapPanel { Name = "MainToolbar", Children = {
            new StackPanel { Spacing = 5, Children = { new TextBlock { Text = "Nodo da controllare", FontSize = 12, Foreground = DesktopTheme.Muted }, _nodeSelector } },
            _runButton, _inventoryButton, _inspectionRolePanel, _configureButton, _openReportButton, _cancelButton,
            help
        } };
        foreach (var control in toolbar.Children) { control.Margin = new Thickness(0, 4, 10, 4); control.VerticalAlignment = VerticalAlignment.Bottom; }
        toolbar.Margin = new Thickness(0, 14, 0, 6);
        _nodeSelector.ItemsSource = _workspaces.Select(x => x.Label).ToArray();
        Avalonia.Automation.AutomationProperties.SetName(_nodeSelector, "Nodo da controllare");
        foreach (var view in _workspaces.Concat(_inspectionWorkspaces)) { view.IsVisible = false; _workspaceHost.Children.Add(view); }
        void SelectWorkspace()
        {
            foreach (var view in _workspaces.Concat(_inspectionWorkspaces)) view.IsVisible = ReferenceEquals(view, Current);
            if (Current is { Role: not null, Results.Count: 0 } workspace) LoadPlan(workspace);
            UpdateControls();
        }
        _nodeSelector.SelectionChanged += (_, _) => SelectWorkspace();
        _inspectionRoleSelector.SelectionChanged += (_, _) => SelectWorkspace();
        _runButton.Click += async (_, _) => await RunAsync();
        _inventoryButton.Click += async (_, _) => await RunAsync();
        _cancelButton.Click += (_, _) => { _cancellation?.Cancel(); _cancelButton.IsEnabled = false; ShowStatus("Interruzione richiesta; attendo la fine della lettura locale in corso."); };
        _configureButton.Click += async (_, _) => await ConfigureAsync();
        _openReportButton.Click += (_, _) => { if (Current?.ReportPath is { } path) OpenFile(path); };
        _openConfigButton.Click += (_, _) => OpenFile(ConfigPath);
        _reloadConfigButton.Click += (_, _) => { if (Current is { Role: not null } workspace) LoadPlan(workspace); };
        _inventorySettingsButton.Click += async (_, _) => await ImportAsync();
        var grid = new Grid { Margin = new Thickness(18), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*") };
        grid.Children.Add(new TextBlock { Text = AppSettings.ApplicationName, FontSize = 22, FontWeight = FontWeight.SemiBold });
        Grid.SetRow(toolbar, 1); grid.Children.Add(toolbar);
        _status.Margin = new Thickness(0, 5, 0, 12); Grid.SetRow(_status, 2); grid.Children.Add(_status);
        _status.Foreground = DesktopTheme.Muted;
        _status.FontSize = 12;
        Grid.SetRow(_workspaceHost, 3); grid.Children.Add(_workspaceHost);
        Content = grid;
        Closing += (_, e) => { if (_busy) { e.Cancel = true; ShowStatus("Attendere il completamento oppure interrompere il test prima di chiudere."); } };
        UpdateControls();
    }

    private void UpdateControls()
    {
        var selected = Current;
        var inspecting = _nodeSelector.SelectedIndex == 4;
        _inspectionRolePanel.IsVisible = inspecting;
        _runButton.IsVisible = _configureButton.IsVisible = inspecting || selected?.Role is not null || selected is null;
        _inventoryButton.IsVisible = selected is { Role: null, IsDtxInspection: false };
        _runButton.IsEnabled = _configureButton.IsEnabled = !_busy && selected?.Role is not null;
        _inventoryButton.IsEnabled = !_busy && _inventoryButton.IsVisible;
        var label = inspecting ? "Avvia scan" : "Esegui test";
        ((TextBlock)((StackPanel)_runButton.Content!).Children[1]).Text = label;
        Avalonia.Automation.AutomationProperties.SetName(_runButton, label);
        ToolTip.SetTip(_runButton, label);
        _inventorySettingsButton.IsEnabled = !_busy && selected?.Role is not null;
        _openConfigButton.IsEnabled = _reloadConfigButton.IsEnabled = !_busy;
        _openReportButton.IsEnabled = selected?.ReportPath is not null;
        if (!_busy) ShowStatus(selected is null ? "Seleziona un nodo oppure Scan PC / Scan DTX." : null);
    }

    private void ShowStatus(string? message)
    {
        _status.Text = message;
        _status.IsVisible = !string.IsNullOrWhiteSpace(message);
    }

    private void LoadPlan(NodeWorkspace workspace)
    {
        if (workspace.Role is not { } role) return;
        try { workspace.SetResults(NodeCheckService.GetPlannedChecks(ConfigPath, role)); workspace.Status.Text = "Piano caricato · controlli non ancora eseguiti con queste impostazioni"; }
        catch (Exception ex) { workspace.Status.Text = ex.Message; }
    }

    private async Task RunAsync()
    {
        var origin = Current;
        var inspect = origin?.IsDtxInspection == true;
        var inventory = origin is { Role: null, IsDtxInspection: false };
        if (_busy || origin is null || (!inventory && origin.Role is null) || (inventory && origin.Role is not null)) return;
        _busy = true; UpdateControls();
        origin.Progress.IsVisible = origin.Progress.IsIndeterminate = true;
        _cancellation = new CancellationTokenSource();
        _cancelButton.IsVisible = true; _cancelButton.IsEnabled = true;
        ShowStatus($"{origin.Label}: operazione in corso. Puoi selezionare gli altri contesti.");
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), AppSettings.ApplicationName);
        var progress = new UiProgress(activity => origin.AddActivity(activity));
        try
        {
            if (!inventory) LoadPlan(origin);
            origin.AddActivity(new(DateTimeOffset.Now, "INFO", origin.Label, inventory ? "Lettura locale, senza prove di rete" : "Avvio controlli e prove DNS/TCP con timeout"));
            NodeCheckExecutionResult result;
            if (inventory)
                result = await Task.Run(() => NodeCheckService.RunInventory(Path.Combine(root, "Reports", "PC-Inventory.html"),
                    Path.Combine(root, "Logs", "PC-Inventory.log"), openReport: false, progress, _cancellation.Token));
            else
            {
                var role = origin.Role!.Value;
                var paths = NodeCheckService.CreateDefaultPaths(role, AppSettings.ApplicationName);
                var token = _cancellation!.Token;
                result = await Task.Run(() => NodeCheckService.RunCheck(paths.ConfigPath, role, paths.ReportPath, paths.LogPath, openReport: false, inspect, progress, token));
            }
            // Flush progress queued by the worker before publishing the final status.
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            origin.Complete(result);
        }
        catch (Exception ex)
        {
            origin.AddActivity(new(DateTimeOffset.Now, "ERROR", origin.Label, ex.Message, ex.ToString()));
            origin.Status.Text = $"Esecuzione non completata: {ex.Message}";
        }
        finally
        {
            origin.Progress.IsVisible = origin.Progress.IsIndeterminate = false;
            _cancellation?.Dispose(); _cancellation = null; _busy = false; _cancelButton.IsVisible = false;
            UpdateControls();
        }
    }

    private async Task ConfigureAsync()
    {
        if (_busy || Current?.Role is not { } role) return;
        var origin = Current;
        _busy = true; UpdateControls();
        bool run = false;
        try
        {
            var wizard = new ConfigurationWizard(ConfigPath, role);
            var outcome = await wizard.ShowDialog<int>(this);
            if (outcome > 0) { LoadPlan(origin); origin.AddActivity(new(DateTimeOffset.Now, "INFO", "Configurazione", $"Salvata. Backup: {wizard.BackupPath}")); }
            run = outcome == 2;
        }
        catch (Exception ex) { await ShowMessageAsync("Configurazione", ex.Message); }
        finally { _busy = false; UpdateControls(); }
        if (run) await RunAsync();
    }

    private async Task ImportAsync()
    {
        if (_busy || Current?.Role is not { } role) return;
        var origin = Current;
        _busy = true; UpdateControls();
        try
        {
            if (!await CreateInventorySettingsConfirmation(role, ConfigPath).ShowDialog<bool>(this)) return;
            var backup = await Task.Run(() => NodeCheckService.LoadSettingsFromInventory(ConfigPath, role));
            LoadPlan(origin);
            origin.AddActivity(new(DateTimeOffset.Now, "INFO", "Configurazione", $"Importazione completata. Backup: {backup}"));
        }
        catch (Exception ex) { await ShowMessageAsync("Importazione", ex.Message); }
        finally { _busy = false; UpdateControls(); }
    }

    private void OpenFile(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { ShowStatus(ex.Message); }
    }

    private async Task ShowMessageAsync(string title, string text)
    {
        var dialog = new Window { Title = title, Width = 580, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var close = new Button { Content = "Chiudi", IsDefault = true, IsCancel = true };
        close.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel { Margin = new Thickness(20), Spacing = 15, Children = { new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }, close } };
        await dialog.ShowDialog(this);
    }

    private sealed class UiProgress(Action<NodeActivity> callback) : IProgress<NodeActivity>
    {
        public void Report(NodeActivity value) => Dispatcher.UIThread.Post(() => callback(value));
    }


    private static Window CreateInventorySettingsConfirmation(DtxNodeRole role, string configPath)
    {
        var dialog = new Window
        {
            Title = "Sostituire le impostazioni correnti?",
            Width = 600,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var cancel = new Button { Content = "Annulla", IsDefault = true, IsCancel = true, MinWidth = 100 };
        var confirm = new Button { Content = "Sostituisci impostazioni", MinWidth = 190 };
        cancel.Click += (_, _) => dialog.Close(false);
        confirm.Click += (_, _) => dialog.Close(true);
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = $"Le impostazioni correnti del nodo {role} verranno sostituite e le personalizzazioni potrebbero andare perse.\n\n"
                        + "Verra' salvata una copia di sicurezza del file precedente. I controlli comuni e gli altri nodi saranno conservati.\n\n"
                        + "Saranno importati directory, processi e servizi DTX rilevati, come controlli facoltativi da verificare. Le porte TCP non saranno importate. Se non vengono rilevati componenti DTX, il file non verra' modificato.\n\n"
                        + $"File: {configPath}\n\nContinuare?",
                    TextWrapping = TextWrapping.Wrap
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancel, confirm }
                }
            }
        };
        return dialog;
    }


}

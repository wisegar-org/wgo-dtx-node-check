using System.Diagnostics;
using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using DtxNodeCheck.Core;

namespace DtxNodeCheck.Desktop.Shared;

public sealed class MainWindow : Window
{
    private static readonly DtxNodeRole[] NodeRoles =
    [
        DtxNodeRole.Core,
        DtxNodeRole.Workstation,
        DtxNodeRole.Client
    ];

    private static readonly OfficialLink[] OfficialLinks =
    [
        new("Supporto", "https://www.dtxstudio.com/en-us/support"),
        new("DTX Studio Go", "https://www.dtxstudio.com/en-us/dtx-studio-go"),
        new("Help e IFU", "https://helpfiles.dtxstudio.com/"),
        new("Installazione", "https://helpfiles.dtxstudio.com/Help/50784413-8047-4699-82f7-d1e9a868909e/4.1/EN/Installation_and_updates.htm")
    ];

    private readonly string _applicationName;
    private readonly string? _configFileName;
    private readonly bool _inferNodeRole;
    private DtxNodeRole? _nodeRole;
    private NodeCheckPaths? _paths;
    private readonly TextBlock _subtitle;
    private readonly TextBlock _status;
    private readonly TextBox _log;
    private readonly ProgressBar _progress;
    private readonly ComboBox _nodeSelector;
    private readonly Button _runButton;
    private readonly Button _inventoryButton;
    private readonly Button _openReportButton;
    private readonly Button _openConfigButton;
    private readonly Button _reloadConfigButton;
    private readonly Button _aboutButton;
    private string? _lastReportPath;
    private bool _updatingNodeSelector;

    public MainWindow(DtxNodeRole? nodeRole, string applicationName, string? configFileName, bool inferNodeRole)
    {
        _nodeRole = nodeRole;
        _applicationName = applicationName;
        _configFileName = configFileName;
        _inferNodeRole = inferNodeRole;
        if (_nodeRole is not null)
        {
            _paths = NodeCheckService.CreateDefaultPaths(_nodeRole.Value, _applicationName, _configFileName);
        }

        Title = applicationName;
        Width = 980;
        Height = 720;
        MinWidth = 760;
        MinHeight = 560;

        var title = new TextBlock
        {
            Text = applicationName,
            FontSize = 24,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        };

        _subtitle = new TextBlock
        {
            Foreground = Brushes.DimGray,
            TextWrapping = TextWrapping.Wrap
        };
        UpdateSubtitle();

        _status = new TextBlock
        {
            Text = OperatingSystem.IsWindows()
                ? "Pronto."
                : "Controlli disponibili solo su Windows.",
            Margin = new Thickness(0, 14, 0, 8)
        };

        _progress = new ProgressBar
        {
            Minimum = 0,
            Maximum = 1,
            Value = 0,
            Height = 10
        };

        _log = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = FontFamily.Parse("Consolas, Menlo, monospace"),
            Text = ""
        };
        ScrollViewer.SetVerticalScrollBarVisibility(_log, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(_log, ScrollBarVisibility.Auto);

        _nodeSelector = new ComboBox
        {
            MinWidth = 170,
            ItemsSource = NodeRoles.Select(role => role.ToString()).ToArray(),
            PlaceholderText = "Seleziona nodo"
        };
        _nodeSelector.SelectionChanged += (_, _) => SelectNodeFromUi();

        _runButton = new Button { Content = "Esegui test", MinWidth = 120 };
        _runButton.Click += async (_, _) => await RunChecksAsync();

        _inventoryButton = new Button { Content = "Inventario PC", MinWidth = 120 };
        _inventoryButton.Click += async (_, _) => await RunInventoryAsync();

        _openReportButton = new Button { Content = "Apri report", MinWidth = 120, IsEnabled = false };
        _openReportButton.Click += (_, _) => OpenLastReport();

        _openConfigButton = new Button { Content = "Apri config", MinWidth = 120 };
        _openConfigButton.Click += (_, _) => OpenConfigFile();

        _reloadConfigButton = new Button { Content = "Ricarica config", MinWidth = 120 };
        _reloadConfigButton.Click += async (_, _) =>
        {
            if (_nodeRole is null && _inferNodeRole)
                await InitializeNodeSelectionAsync();
            else
                ReloadPlan();
        };

        _aboutButton = new Button { Content = "About", MinWidth = 96 };
        _aboutButton.Click += async (_, _) => await ShowAboutAsync();

        var buttons = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 12, 0, 12),
            Children = { _nodeSelector, _runButton, _inventoryButton, _openReportButton, _openConfigButton, _reloadConfigButton, _aboutButton }
        };
        foreach (var button in buttons.Children)
            button.Margin = new Thickness(0, 0, 8, 8);

        var documentationLinks = CreateDocumentationLinks();

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*"),
            Margin = new Thickness(18)
        };
        layout.Children.Add(new StackPanel { Children = { title, _subtitle, _status } });
        Grid.SetRow(_progress, 1);
        layout.Children.Add(_progress);
        Grid.SetRow(buttons, 2);
        layout.Children.Add(buttons);
        Grid.SetRow(documentationLinks, 3);
        layout.Children.Add(documentationLinks);
        Grid.SetRow(_log, 4);
        layout.Children.Add(_log);

        Content = layout;
        UpdateControlsForNode();
        _nodeSelector.IsEnabled = _inferNodeRole;
        Opened += async (_, _) => await InitializeNodeSelectionAsync();
    }

    private Control CreateDocumentationLinks()
    {
        var title = new TextBlock
        {
            Text = "Documentazione",
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var links = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        foreach (var officialLink in OfficialLinks)
        {
            var button = new Button
            {
                Content = officialLink.Label,
                Tag = officialLink.Url,
                Padding = new Thickness(10, 5),
                MinWidth = 96
            };
            ToolTip.SetTip(button, officialLink.Url);
            button.Click += (_, _) => OpenOfficialLink(officialLink);
            links.Children.Add(button);
        }

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Margin = new Thickness(0, 0, 0, 12),
            Children = { title, links }
        };
    }

    private void ReloadPlan()
    {
        _log.Text = "";
        LoadPlan();
    }

    private async Task InitializeNodeSelectionAsync()
    {
        if (_nodeRole is null && _inferNodeRole)
        {
            var configPath = NodeCheckService.CreateDefaultConfigPath(_configFileName ?? "dtx-node-check.json");
            SetBusy(true, "Rilevamento nodo in corso...");
            NodeDetectionResult detection;
            try
            {
                detection = await Task.Run(() => NodeCheckService.DetectNodeRole(configPath));
            }
            catch (Exception ex)
            {
                AppendLine($"[ERROR] detection - {ex.Message}");
                _status.Text = "Rilevamento non disponibile. Seleziona il nodo manualmente.";
                return;
            }
            finally
            {
                SetBusy(false, _status.Text ?? "Seleziona Core, Workstation o Client.");
            }
            AppendLine("Inferenza nodo");
            AppendLine("==============");
            AppendLine($"[INFO] detection - {detection.Message}");
            foreach (var candidate in detection.Candidates)
            {
                var reasons = candidate.Reasons.Count == 0 ? "nessun segnale" : string.Join("; ", candidate.Reasons.Take(4));
                AppendLine($"[INFO] detection - {candidate.Role}: score={candidate.Score}; {reasons}");
            }

            if (detection.IsConfident && detection.Role is not null)
            {
                SetNodeRole(detection.Role.Value, $"Nodo inferito automaticamente: {detection.Role}.", reloadPlan: true);
                return;
            }

            _status.Text = "Nodo non inferito. Seleziona Core, Workstation o Client.";
            UpdateControlsForNode();
            return;
        }

        if (_nodeRole is not null)
        {
            SetNodeRole(_nodeRole.Value, $"Nodo selezionato: {_nodeRole}.", reloadPlan: true);
            return;
        }

        _status.Text = "Seleziona Core, Workstation o Client.";
        UpdateControlsForNode();
    }

    private void SelectNodeFromUi()
    {
        if (_updatingNodeSelector)
        {
            return;
        }

        if (_nodeSelector.SelectedIndex < 0 || _nodeSelector.SelectedIndex >= NodeRoles.Length)
        {
            return;
        }

        SetNodeRole(NodeRoles[_nodeSelector.SelectedIndex], $"Nodo selezionato manualmente: {NodeRoles[_nodeSelector.SelectedIndex]}.", reloadPlan: true);
    }

    private void SetNodeRole(DtxNodeRole role, string status, bool reloadPlan)
    {
        _nodeRole = role;
        _paths = NodeCheckService.CreateDefaultPaths(role, _applicationName, _configFileName);
        _lastReportPath = null;
        _openReportButton.IsEnabled = false;
        var index = Array.IndexOf(NodeRoles, role);
        if (_nodeSelector.SelectedIndex != index)
        {
            _updatingNodeSelector = true;
            try
            {
                _nodeSelector.SelectedIndex = index;
            }
            finally
            {
                _updatingNodeSelector = false;
            }
        }

        UpdateSubtitle();
        UpdateControlsForNode();
        _status.Text = status;
        if (reloadPlan)
        {
            LoadPlan();
        }
    }

    private void UpdateSubtitle()
    {
        var node = _nodeRole?.ToString() ?? "da selezionare";
        var config = _paths?.ConfigPath ?? NodeCheckService.CreateDefaultConfigPath(_configFileName ?? "dtx-node-check.json");
        _subtitle.Text = $"Nodo: {node}  |  Config: {config}";
    }

    private void UpdateControlsForNode()
    {
        var hasNode = _nodeRole is not null && _paths is not null;
        _runButton.IsEnabled = hasNode && OperatingSystem.IsWindows();
        _inventoryButton.IsEnabled = OperatingSystem.IsWindows();
        _openConfigButton.IsEnabled = hasNode || _configFileName is not null;
        _reloadConfigButton.IsEnabled = hasNode || _inferNodeRole;
    }

    private void LoadPlan()
    {
        AppendLine("Piano controlli");
        AppendLine("===============");
        if (_nodeRole is null || _paths is null)
        {
            AppendLine("[INFO] nodo - Seleziona un nodo per caricare il piano controlli.");
            return;
        }

        AppendLine($"[INFO] config - Lettura file: {_paths.ConfigPath}");

        if (!OperatingSystem.IsWindows())
        {
            AppendLine("Questa applicazione puo' mostrare l'interfaccia su piu' piattaforme, ma i controlli DTX sono Windows-only.");
            return;
        }

        try
        {
            foreach (var item in NodeCheckService.GetPlannedChecks(_paths.ConfigPath, _nodeRole.Value))
            {
                AppendLine($"[PENDING] {item.Category} - {item.Name}: {item.Message}");
            }
        }
        catch (Exception ex)
        {
            AppendLine($"[ERROR] Impossibile caricare il piano controlli: {ex.Message}");
        }
    }

    private async Task RunChecksAsync()
    {
        SetBusy(true, "Esecuzione controlli...");
        if (_nodeRole is null || _paths is null)
        {
            AppendLine("[ERROR] nodo - Nodo non selezionato.");
            SetBusy(false, "Seleziona Core, Workstation o Client.");
            return;
        }

        _progress.Value = 0;
        _lastReportPath = _paths.ReportPath;
        _openReportButton.IsEnabled = false;

        try
        {
            var planned = NodeCheckService.GetPlannedChecks(_paths.ConfigPath, _nodeRole.Value);
            AppendLine($"[INFO] config - Rilettura file prima dell'esecuzione: {_paths.ConfigPath}");
            _progress.Maximum = Math.Max(planned.Count + 1, 1);
            var index = 0;
            foreach (var item in planned)
            {
                AppendLine($"[RUNNING] {item.Category} - {item.Name}");
                _progress.Value = ++index;
                await Task.Delay(60);
            }

            var result = await Task.Run(() => NodeCheckService.RunCheck(_paths.ConfigPath, _nodeRole.Value, _paths.ReportPath, _paths.LogPath, openReport: true));
            _progress.Value = _progress.Maximum;
            AppendLine("");
            AppendLine("Risultati");
            AppendLine("=========");
            foreach (var item in result.Items)
            {
                AppendLine($"[{item.Status.ToUpperInvariant()}] {item.Category} - {item.Name}: {item.Message}");
            }

            _lastReportPath = result.ReportPath;
            _openReportButton.IsEnabled = true;
            _status.Text = result.ExitCode == 0
                ? $"Completato. Report: {result.ReportPath}"
                : $"Completato con segnalazioni. Report: {result.ReportPath}";
        }
        catch (Exception ex)
        {
            AppendLine($"[ERROR] {ex}");
            _status.Text = ex.Message;
        }
        finally
        {
            SetBusy(false, _status.Text ?? "Pronto.");
        }
    }

    private async Task RunInventoryAsync()
    {
        SetBusy(true, "Esecuzione inventario...");
        var paths = _paths ?? NodeCheckService.CreateDefaultPaths(DtxNodeRole.Client, _applicationName, _configFileName);
        _progress.Maximum = 1;
        _progress.Value = 0;

        try
        {
            var reportPath = Path.Combine(Path.GetDirectoryName(paths.ReportPath) ?? AppContext.BaseDirectory, "DTX-Inventory.html");
            var logPath = Path.Combine(Path.GetDirectoryName(paths.LogPath) ?? AppContext.BaseDirectory, "DtxNodeCheck-Inventory-debug.log");
            AppendLine("[RUNNING] inventory - PC Inventory");
            var result = await Task.Run(() => NodeCheckService.RunInventory(reportPath, logPath, openReport: true));
            _progress.Value = 1;
            _lastReportPath = result.ReportPath;
            _openReportButton.IsEnabled = true;
            AppendLine($"[PASS] inventory - Report: {result.ReportPath}");
            _status.Text = $"Inventario completato. Report: {result.ReportPath}";
        }
        catch (Exception ex)
        {
            AppendLine($"[ERROR] {ex}");
            _status.Text = ex.Message;
        }
        finally
        {
            SetBusy(false, _status.Text ?? "Pronto.");
        }
    }

    private void SetBusy(bool busy, string status)
    {
        UpdateControlsForNode();
        _runButton.IsEnabled &= !busy;
        _inventoryButton.IsEnabled &= !busy;
        _nodeSelector.IsEnabled = !busy && _inferNodeRole;
        _openConfigButton.IsEnabled &= !busy;
        _reloadConfigButton.IsEnabled &= !busy;
        _aboutButton.IsEnabled = !busy;
        _status.Text = status;
    }

    private async Task ShowAboutAsync()
    {
        var assembly = typeof(MainWindow).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "n/a";

        var dialog = new Window
        {
            Title = $"About {_applicationName}",
            Width = 620,
            Height = 520,
            MinWidth = 520,
            MinHeight = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };

        var heading = new TextBlock
        {
            Text = _applicationName,
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        };

        var description = new TextBlock
        {
            Text = "DTX Node Check desktop app",
            Foreground = Brushes.DimGray,
            Margin = new Thickness(0, 0, 0, 14)
        };

        var details = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = FontFamily.Parse("Consolas, Menlo, monospace"),
            Text = BuildAboutText(version)
        };
        ScrollViewer.SetVerticalScrollBarVisibility(details, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(details, ScrollBarVisibility.Disabled);

        var closeButton = new Button
        {
            Content = "Chiudi",
            MinWidth = 96,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        closeButton.Click += (_, _) => dialog.Close();

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Margin = new Thickness(18)
        };

        layout.Children.Add(new StackPanel { Children = { heading, description } });
        Grid.SetRow(details, 1);
        layout.Children.Add(details);
        Grid.SetRow(closeButton, 2);
        layout.Children.Add(closeButton);

        dialog.Content = layout;
        await dialog.ShowDialog(this);
    }

    private string BuildAboutText(string version)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Applicazione");
        builder.AppendLine("============");
        builder.AppendLine($"Nome: {_applicationName}");
        builder.AppendLine($"Nodo: {_nodeRole?.ToString() ?? "da selezionare"}");
        builder.AppendLine($"Versione: {version}");
        builder.AppendLine($"Assembly: {typeof(MainWindow).Assembly.GetName().Name}");
        builder.AppendLine($"Base directory: {AppContext.BaseDirectory}");
        builder.AppendLine();
        builder.AppendLine("Ambiente");
        builder.AppendLine("========");
        builder.AppendLine($"OS: {Environment.OSVersion}");
        builder.AppendLine($"Windows: {(OperatingSystem.IsWindows() ? "si" : "no")}");
        builder.AppendLine($"Architettura processo: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
        builder.AppendLine($"Architettura OS: {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}");
        builder.AppendLine($"Self-contained: {IsLikelySelfContained()}");
        builder.AppendLine();
        builder.AppendLine("Percorsi");
        builder.AppendLine("========");
        builder.AppendLine($"Config: {_paths?.ConfigPath ?? NodeCheckService.CreateDefaultConfigPath(_configFileName ?? "dtx-node-check.json")}");
        builder.AppendLine($"Report: {_paths?.ReportPath ?? "n/a"}");
        builder.AppendLine($"Log: {_paths?.LogPath ?? "n/a"}");
        builder.AppendLine();
        builder.AppendLine("Modalita'");
        builder.AppendLine("=========");
        builder.AppendLine("Controlli locali read-only.");
        builder.AppendLine("Nessun accesso rete automatico.");
        builder.AppendLine("Il file config viene riletto da disco a ogni esecuzione.");
        return builder.ToString();
    }

    private static string IsLikelySelfContained()
    {
        var runtimeConfigPath = Path.ChangeExtension(Environment.ProcessPath, ".runtimeconfig.json");
        return File.Exists(runtimeConfigPath) ? "no" : "si";
    }

    private void OpenConfigFile()
    {
        var configPath = _paths?.ConfigPath ?? NodeCheckService.CreateDefaultConfigPath(_configFileName ?? "dtx-node-check.json");
        if (!File.Exists(configPath))
        {
            AppendLine($"[ERROR] config - File non trovato: {configPath}");
            _status.Text = "File di configurazione non trovato.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = configPath,
                UseShellExecute = true
            });
            AppendLine($"[INFO] config - Apertura file per modifica: {configPath}");
            _status.Text = "Config aperta. Usa Ricarica config dopo il salvataggio.";
        }
        catch (Exception ex)
        {
            AppendLine($"[ERROR] config - Impossibile aprire {configPath}: {ex.Message}");
            _status.Text = "Impossibile aprire il file di configurazione.";
        }
    }

    private void OpenLastReport()
    {
        if (string.IsNullOrWhiteSpace(_lastReportPath) || !File.Exists(_lastReportPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = _lastReportPath,
            UseShellExecute = true
        });
    }

    private void OpenOfficialLink(OfficialLink link)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = link.Url,
                UseShellExecute = true
            });
            AppendLine($"[INFO] documentazione - Apertura link: {link.Url}");
        }
        catch (Exception ex)
        {
            AppendLine($"[ERROR] documentazione - Impossibile aprire {link.Url}: {ex.Message}");
            _status.Text = "Impossibile aprire il link della documentazione.";
        }
    }

    private void AppendLine(string line)
    {
        var builder = new StringBuilder(_log.Text ?? "");
        builder.AppendLine(line);
        _log.Text = builder.ToString();
        _log.CaretIndex = _log.Text.Length;
    }

    private sealed record OfficialLink(string Label, string Url);
}

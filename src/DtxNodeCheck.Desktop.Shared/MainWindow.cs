using System.Diagnostics;
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
    private static readonly OfficialLink[] OfficialLinks =
    [
        new("Supporto", "https://www.dtxstudio.com/en-us/support"),
        new("DTX Studio Go", "https://www.dtxstudio.com/en-us/dtx-studio-go"),
        new("Help e IFU", "https://helpfiles.dtxstudio.com/"),
        new("Installazione", "https://helpfiles.dtxstudio.com/Help/50784413-8047-4699-82f7-d1e9a868909e/4.1/EN/Installation_and_updates.htm")
    ];

    private readonly DtxNodeRole _nodeRole;
    private readonly string _applicationName;
    private readonly NodeCheckPaths _paths;
    private readonly TextBlock _status;
    private readonly TextBox _log;
    private readonly ProgressBar _progress;
    private readonly Button _runButton;
    private readonly Button _inventoryButton;
    private readonly Button _openReportButton;
    private readonly Button _openConfigButton;
    private readonly Button _reloadConfigButton;
    private string? _lastReportPath;

    public MainWindow(DtxNodeRole nodeRole, string applicationName)
    {
        _nodeRole = nodeRole;
        _applicationName = applicationName;
        _paths = NodeCheckService.CreateDefaultPaths(nodeRole, applicationName);

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

        var subtitle = new TextBlock
        {
            Text = $"Nodo: {_nodeRole}  |  Config: {_paths.ConfigPath}",
            Foreground = Brushes.DimGray,
            TextWrapping = TextWrapping.Wrap
        };

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

        _runButton = new Button { Content = "Esegui test", MinWidth = 120 };
        _runButton.Click += async (_, _) => await RunChecksAsync();

        _inventoryButton = new Button { Content = "Inventario PC", MinWidth = 120 };
        _inventoryButton.Click += async (_, _) => await RunInventoryAsync();

        _openReportButton = new Button { Content = "Apri report", MinWidth = 120, IsEnabled = false };
        _openReportButton.Click += (_, _) => OpenLastReport();

        _openConfigButton = new Button { Content = "Apri config", MinWidth = 120 };
        _openConfigButton.Click += (_, _) => OpenConfigFile();

        _reloadConfigButton = new Button { Content = "Ricarica config", MinWidth = 120 };
        _reloadConfigButton.Click += (_, _) => ReloadPlan();

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 12, 0, 12),
            Children = { _runButton, _inventoryButton, _openReportButton, _openConfigButton, _reloadConfigButton }
        };

        var documentationLinks = CreateDocumentationLinks();

        var layout = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*"),
            Margin = new Thickness(18)
        };
        layout.Children.Add(new StackPanel { Children = { title, subtitle, _status } });
        Grid.SetRow(_progress, 1);
        layout.Children.Add(_progress);
        Grid.SetRow(buttons, 2);
        layout.Children.Add(buttons);
        Grid.SetRow(documentationLinks, 3);
        layout.Children.Add(documentationLinks);
        Grid.SetRow(_log, 4);
        layout.Children.Add(_log);

        Content = layout;
        Opened += (_, _) => ReloadPlan();
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

    private void LoadPlan()
    {
        AppendLine("Piano controlli");
        AppendLine("===============");
        AppendLine($"[INFO] config - Lettura file: {_paths.ConfigPath}");

        if (!OperatingSystem.IsWindows())
        {
            AppendLine("Questa applicazione puo' mostrare l'interfaccia su piu' piattaforme, ma i controlli DTX sono Windows-only.");
            return;
        }

        try
        {
            foreach (var item in NodeCheckService.GetPlannedChecks(_paths.ConfigPath, _nodeRole))
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
        _progress.Value = 0;
        _lastReportPath = _paths.ReportPath;
        _openReportButton.IsEnabled = false;

        try
        {
            var planned = NodeCheckService.GetPlannedChecks(_paths.ConfigPath, _nodeRole);
            AppendLine($"[INFO] config - Rilettura file prima dell'esecuzione: {_paths.ConfigPath}");
            _progress.Maximum = Math.Max(planned.Count + 1, 1);
            var index = 0;
            foreach (var item in planned)
            {
                AppendLine($"[RUNNING] {item.Category} - {item.Name}");
                _progress.Value = ++index;
                await Task.Delay(60);
            }

            var result = await Task.Run(() => NodeCheckService.RunCheck(_paths.ConfigPath, _nodeRole, _paths.ReportPath, _paths.LogPath, openReport: true));
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
        _progress.Maximum = 1;
        _progress.Value = 0;

        try
        {
            var reportPath = Path.Combine(Path.GetDirectoryName(_paths.ReportPath) ?? AppContext.BaseDirectory, "DTX-Inventory.html");
            var logPath = Path.Combine(Path.GetDirectoryName(_paths.LogPath) ?? AppContext.BaseDirectory, "DtxNodeCheck-Inventory-debug.log");
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
        _runButton.IsEnabled = !busy;
        _inventoryButton.IsEnabled = !busy;
        _openConfigButton.IsEnabled = !busy;
        _reloadConfigButton.IsEnabled = !busy;
        _status.Text = status;
    }

    private void OpenConfigFile()
    {
        if (!File.Exists(_paths.ConfigPath))
        {
            AppendLine($"[ERROR] config - File non trovato: {_paths.ConfigPath}");
            _status.Text = "File di configurazione non trovato.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _paths.ConfigPath,
                UseShellExecute = true
            });
            AppendLine($"[INFO] config - Apertura file per modifica: {_paths.ConfigPath}");
            _status.Text = "Config aperta. Usa Ricarica config dopo il salvataggio.";
        }
        catch (Exception ex)
        {
            AppendLine($"[ERROR] config - Impossibile aprire {_paths.ConfigPath}: {ex.Message}");
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

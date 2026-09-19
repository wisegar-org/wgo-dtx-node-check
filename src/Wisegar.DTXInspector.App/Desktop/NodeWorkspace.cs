using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Wisegar.DTXInspector.Core;

namespace Wisegar.DTXInspector.Desktop;

public sealed class NodeWorkspace : Grid
{
    public DtxNodeRole? Role { get; }
    public bool IsDtxInspection { get; }
    public string Label => IsDtxInspection ? (Role is null ? "Scan DTX" : $"Scan DTX · {Role}") : Role == DtxNodeRole.Core ? "DTX Core" : Role?.ToString() ?? "Scan PC";
    public string? ReportPath { get; private set; }
    public string? LogPath { get; set; }
    public IReadOnlyList<NodeCheckItem> Results { get; private set; } = [];
    public TextBlock Status { get; } = new() { Text = "Mai eseguito", TextWrapping = TextWrapping.Wrap };
    public ProgressBar Progress { get; } = new() { Height = 5, IsVisible = false };
    public TextBox Search { get; } = new() { PlaceholderText = "Cerca controllo, categoria o messaggio", MinWidth = 220 };
    public ComboBox Filter { get; } = new() { ItemsSource = new[] { "Tutti", "Fail", "Warning", "Pass", "NotApplicable", "Observed", "Pending" }, SelectedIndex = 0 };
    public ComboBox Category { get; } = new() { MinWidth = 120 };
    private readonly TextBlock _counts = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ListBox _results = new();
    private readonly TextBox _details = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 95 };

    public NodeWorkspace(DtxNodeRole? role, bool isDtxInspection = false)
    {
        Role = role;
        IsDtxInspection = isDtxInspection;
        RowDefinitions = new RowDefinitions("Auto,Auto,*");
        var color = role switch { DtxNodeRole.Core => "#2563A6", DtxNodeRole.Workstation => "#7953A6", DtxNodeRole.Client => "#087E8B", _ => "#56616F" };
        var description = isDtxInspection ? "SCAN DTX · Servizi · Processi · Porte TCP · IP e DNS · Checklist del ruolo" : role switch
        {
            DtxNodeRole.Core => "SERVER DTX CORE · Servizi Windows · Identità e rete · Comunicazioni",
            DtxNodeRole.Workstation => "ACQUISIZIONE / RICOSTRUZIONE · Utente operativo · Cartelle DTX · Collegamento al Core",
            DtxNodeRole.Client => "VISUALIZZAZIONE · Collegamento al Core · Identità e rete",
            _ => "SCAN LOCALE · Software · Servizi · Processi · Porte · Nessuna verifica di conformità DTX"
        };
        var bannerText = new StackPanel { Spacing = 6, Margin = new Thickness(12, 0, 0, 0), Children = {
                new TextBlock { Text = description, Foreground = Brush.Parse(color), TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold, FontSize = 13 },
                new TextBlock { Text = isDtxInspection && role is null ? "Scegli il ruolo da analizzare: Core, Workstation o Client. Nessun ruolo viene dedotto automaticamente." : role is null ? "Le osservazioni descrivono questo PC; non attestano il funzionamento di DTX." : "Requisiti: IPv4 statico e IPv6 disabilitato. Le verifiche manuali restano da confermare.", Foreground = DesktopTheme.Muted, TextWrapping = TextWrapping.Wrap, FontSize = 12 }
            } };
        Status.Foreground = DesktopTheme.Muted; Status.FontSize = 12;
        _counts.Foreground = DesktopTheme.Ink; _counts.FontSize = 12;
        var state = new StackPanel { Margin = new Thickness(0, 10, 0, 0), Spacing = 8, Children = { Status, _counts, Progress } };
        bannerText.Children.Add(state);
        var bannerLayout = new Grid { ColumnDefinitions = new ColumnDefinitions("4,*") };
        bannerLayout.Children.Add(new Border { Background = Brush.Parse(color), CornerRadius = new CornerRadius(2) });
        Grid.SetColumn(bannerText, 1); bannerLayout.Children.Add(bannerText);
        Children.Add(new Border { Background = Brushes.White, BorderBrush = DesktopTheme.Line, BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 16), CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 0, 12), Child = bannerLayout });
        var filters = new WrapPanel { Children = { Filter, Category, Search } };
        foreach (var child in filters.Children) child.Margin = new Thickness(0, 0, 8, 8);
        Grid.SetRow(filters, 1); Children.Add(filters);
        _results.ItemTemplate = new FuncDataTemplate<NodeCheckItem>((item, _) =>
        {
            if (item is null) return null;
            var (foreground, background) = item.Status switch {
                "Fail" => ("#991B1B", "#FEE2E2"), "Warning" => ("#92400E", "#FEF3C7"),
                "Pass" => ("#166534", "#DCFCE7"), _ => ("#475569", "#F1F5F9")
            };
            var badge = new Border { Background = Brush.Parse(background), CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 3), Margin = new Thickness(0, 0, 10, 4), Child =
                new TextBlock { Text = StatusLabel(item.Status), Foreground = Brush.Parse(foreground), FontSize = 11, FontWeight = FontWeight.SemiBold } };
            return new Border { BorderBrush = DesktopTheme.Line, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(8, 10), Child =
                new StackPanel { Spacing = 5, Children = {
                    new WrapPanel { Children = { badge, new TextBlock { Text = item.Name, Foreground = DesktopTheme.Ink, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 4) } } },
                    new TextBlock { Text = item.Category, Foreground = DesktopTheme.Muted, FontSize = 11 },
                    new TextBlock { Text = item.Message, TextWrapping = TextWrapping.Wrap, Foreground = DesktopTheme.Muted, FontSize = 13 }
                } } };
        });
        _results.SelectionChanged += (_, _) =>
        {
            if (_results.SelectedItem is NodeCheckItem item)
                _details.Text = $"{item.Name}\n{item.Message}\n\n" + string.Join("\n", item.Details?.Select(x => $"{x.Key}: {x.Value}") ?? []);
        };
        var resultLayout = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        resultLayout.Children.Add(_results);
        var detail = new Expander { Header = "Dettagli del controllo selezionato", Content = _details, HorizontalAlignment = HorizontalAlignment.Stretch };
        Grid.SetRow(detail, 1); resultLayout.Children.Add(detail);
        _results.Background = Brushes.White;
        _results.BorderThickness = new Thickness(0);
        var surface = new Border { Background = Brushes.White, BorderBrush = DesktopTheme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(8), Child = resultLayout };
        Grid.SetRow(surface, 2); Children.Add(surface);
        Search.TextChanged += (_, _) => RefreshResults();
        Filter.SelectionChanged += (_, _) => RefreshResults();
        Category.SelectionChanged += (_, _) => RefreshResults();
        SetResults([]);
    }

    public void SetResults(IReadOnlyList<NodeCheckItem> items)
    {
        var selectedId = (_results.SelectedItem as NodeCheckItem)?.Id;
        Results = items;
        var category = Category.SelectedItem as string;
        Category.ItemsSource = new[] { "Tutte le categorie" }.Concat(items.Select(x => x.Category).Distinct().Order()).ToArray();
        Category.SelectedItem = category;
        if (Category.SelectedIndex < 0) Category.SelectedIndex = 0;
        _counts.Text = string.Join("   ·   ", items.GroupBy(x => x.Status).Select(g => $"{StatusLabel(g.Key)}: {g.Count()}"));
        _details.Text = "Seleziona una riga per leggere evidenze e limiti della verifica.";
        RefreshResults();
        if (selectedId is not null) _results.SelectedItem = Results.FirstOrDefault(x => x.Id == selectedId);
    }

    public void Complete(NodeCheckExecutionResult result)
    {
        SetResults(result.Items); ReportPath = result.ReportPath; LogPath = result.LogPath;
        Status.Text = $"{DateTime.Now:g} · " + (Role is null ? (result.Items.Any(x => x.Status == "Warning") ? "Osservazioni parziali: consultare le segnalazioni" : "Osservazioni locali raccolte") :
            result.Items.Any(x => x.Status is "Warning" or "Fail") ? "Sono presenti problemi o verifiche incomplete" : "Controlli completati")
            + $" · {Environment.MachineName}";
    }

    public void AddActivity(NodeActivity item)
    {
        if (item.Phase == "Log") LogPath = item.Message;
        if (item.Item is { Id: not null } result)
        {
            var updated = Results.ToList();
            var index = updated.FindIndex(x => x.Id == result.Id);
            if (index >= 0) updated[index] = result; else updated.Add(result);
            SetResults(updated);
            return;
        }
        if (item.Level == "INFO") Status.Text = $"{item.Phase}: {item.Message}";
    }

    private void RefreshResults()
    {
        var query = Search.Text?.Trim() ?? "";
        _results.ItemsSource = Results.Where(x => (Filter.SelectedIndex <= 0 || x.Status == Filter.SelectedItem?.ToString())
            && (Category.SelectedIndex <= 0 || x.Category == Category.SelectedItem?.ToString())
            && $"{x.Name} {x.Category} {x.Message}".Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    private static string StatusLabel(string status) => status switch {
        "Fail" => "ERRORE", "Warning" => "DA VERIFICARE", "Pass" => "SUPERATO", "NotApplicable" => "NON APPLICABILE",
        "Observed" => "OSSERVATO", "Pending" => "DA ESEGUIRE", _ => status
    };
}

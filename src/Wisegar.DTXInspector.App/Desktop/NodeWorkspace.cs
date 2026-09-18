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
    public string Label => Role == DtxNodeRole.Core ? "DTX Core" : Role?.ToString() ?? "Ispezione PC";
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

    public NodeWorkspace(DtxNodeRole? role)
    {
        Role = role;
        RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*");
        var color = role switch { DtxNodeRole.Core => "#2563A6", DtxNodeRole.Workstation => "#7953A6", DtxNodeRole.Client => "#087E8B", _ => "#56616F" };
        var description = role switch
        {
            DtxNodeRole.Core => "SERVER DTX CORE · Servizi Windows · Identità e rete · Comunicazioni",
            DtxNodeRole.Workstation => "ACQUISIZIONE / RICOSTRUZIONE · Utente operativo · Cartelle DTX · Collegamento al Core",
            DtxNodeRole.Client => "VISUALIZZAZIONE · Collegamento al Core · Identità e rete",
            _ => "ISPEZIONE LOCALE · Software · Servizi · Processi · Porte · Nessuna verifica di conformità DTX"
        };
        Children.Add(new Border { Background = Brush.Parse(color), Padding = new Thickness(14), CornerRadius = new CornerRadius(5), Child =
            new StackPanel { Spacing = 6, Children = {
                new TextBlock { Text = description, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = role is null ? "Le osservazioni descrivono questo PC; non attestano il funzionamento di DTX." : "Requisiti: IPv4 statico e IPv6 disabilitato. Le verifiche manuali restano da confermare.", Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap }
            } } });
        var state = new StackPanel { Margin = new Thickness(0, 10), Spacing = 5, Children = { Status, _counts, Progress } };
        Grid.SetRow(state, 1); Children.Add(state);
        var filters = new WrapPanel { Children = { Filter, Category, Search } };
        foreach (var child in filters.Children) child.Margin = new Thickness(0, 0, 8, 8);
        Grid.SetRow(filters, 2); Children.Add(filters);
        _results.ItemTemplate = new FuncDataTemplate<NodeCheckItem>((item, _) =>
        {
            if (item is null) return null;
            var brush = item!.Status switch { "Fail" => Brushes.Firebrick, "Warning" => Brushes.DarkGoldenrod, "Pass" => Brushes.ForestGreen, _ => Brushes.SlateGray };
            return new StackPanel { Margin = new Thickness(4, 5), Spacing = 3, Children = {
                new TextBlock { Text = $"{StatusLabel(item.Status)}  ·  {item.Category}  ·  {item.Name}", Foreground = brush, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = item.Message, TextWrapping = TextWrapping.Wrap }
            } };
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
        Grid.SetRow(resultLayout, 3); Children.Add(resultLayout);
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

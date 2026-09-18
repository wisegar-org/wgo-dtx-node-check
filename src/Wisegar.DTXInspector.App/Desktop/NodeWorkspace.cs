using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Input.Platform;
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
    private readonly ObservableCollection<NodeActivity> _events = [];
    private readonly ObservableCollection<NodeActivity> _visibleEvents = [];
    private readonly ListBox _activity = new();
    private readonly CheckBox _follow = new() { Content = "Segui attività", IsChecked = true };
    private readonly CheckBox _technical = new() { Content = "Mostra diagnostica" };
    private readonly TextBox _eventDetails = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 80 };

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
        _activity.ItemTemplate = new FuncDataTemplate<NodeActivity>((entry, _) => new TextBlock
        {
            Text = $"{entry!.Time:HH:mm:ss}  [{entry.Level}]  {entry.Phase} — {entry.Message}", TextWrapping = TextWrapping.Wrap,
            Foreground = entry.Level == "ERROR" ? Brushes.Firebrick : Brushes.DimGray, Margin = new Thickness(4)
        });
        _activity.SelectionChanged += (_, _) => _eventDetails.Text = (_activity.SelectedItem as NodeActivity)?.Details;
        _activity.ItemsSource = _visibleEvents;
        // Wheel navigation suspends follow; the user can explicitly resume it.
        _activity.PointerWheelChanged += (_, _) => _follow.IsChecked = false;
        _technical.IsCheckedChanged += (_, _) => RefreshActivity();
        var resultLayout = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        resultLayout.Children.Add(_results);
        var detail = new Expander { Header = "Dettagli del controllo selezionato", Content = _details, HorizontalAlignment = HorizontalAlignment.Stretch };
        Grid.SetRow(detail, 1); resultLayout.Children.Add(detail);
        var activityLayout = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        var copy = new Button { Content = "Copia attività" };
        copy.Click += async (_, _) =>
        {
            try
            {
                if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
                    await clipboard.SetTextAsync(string.Join("\n", _events.Select(x => $"{x.Time:O} [{x.Level}] {x.Phase}: {x.Message}")));
            }
            catch (Exception ex) { Status.Text = ex.Message; }
        };
        var export = new Button { Content = "Esporta log completo" };
        export.Click += async (_, _) =>
        {
            try
            {
                if (LogPath is null || !File.Exists(LogPath)) { Status.Text = "Nessun log di esecuzione disponibile per questo tab."; return; }
                if (TopLevel.GetTopLevel(this) is not { } top) return;
                var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Esporta log", SuggestedFileName = Path.GetFileName(LogPath) });
                if (file is null) return;
                if (string.Equals(file.TryGetLocalPath(), LogPath, StringComparison.OrdinalIgnoreCase)) { Status.Text = "Il file scelto è già il log originale."; return; }
                var bytes = await File.ReadAllBytesAsync(LogPath);
                await using var output = await file.OpenWriteAsync(); output.SetLength(0); await output.WriteAsync(bytes);
            }
            catch (Exception ex) { Status.Text = ex.Message; }
        };
        activityLayout.Children.Add(new WrapPanel { Children = { _follow, _technical, copy, export } });
        Grid.SetRow(_activity, 1); activityLayout.Children.Add(_activity);
        var technicalDetails = new Expander { Header = "Dettagli tecnici dell'evento", Content = _eventDetails, HorizontalAlignment = HorizontalAlignment.Stretch };
        Grid.SetRow(technicalDetails, 2); activityLayout.Children.Add(technicalDetails);
        var pages = new TabControl { ItemsSource = new[] {
            new TabItem { Header = "Risultati", Content = resultLayout },
            new TabItem { Header = "Attività", Content = activityLayout }
        } };
        Grid.SetRow(pages, 3); Children.Add(pages);
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
        _events.Add(item);
        if (_technical.IsChecked == true || item.Level != "DEBUG") _visibleEvents.Add(item);
        if (_events.Count > 2000) { var expired = _events[0]; _events.RemoveAt(0); _visibleEvents.Remove(expired); }
        if (_follow.IsChecked == true && _visibleEvents.Count > 0) _activity.ScrollIntoView(_visibleEvents[^1]);
        if (item.Level == "INFO") Status.Text = $"{item.Phase}: {item.Message}";
    }

    private void RefreshResults()
    {
        var query = Search.Text?.Trim() ?? "";
        _results.ItemsSource = Results.Where(x => (Filter.SelectedIndex <= 0 || x.Status == Filter.SelectedItem?.ToString())
            && (Category.SelectedIndex <= 0 || x.Category == Category.SelectedItem?.ToString())
            && $"{x.Name} {x.Category} {x.Message}".Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    private void RefreshActivity()
    {
        _visibleEvents.Clear();
        foreach (var item in _events.Where(x => _technical.IsChecked == true || x.Level != "DEBUG")) _visibleEvents.Add(item);
        if (_follow.IsChecked == true && _visibleEvents.Count > 0) _activity.ScrollIntoView(_visibleEvents[^1]);
    }

    private static string StatusLabel(string status) => status switch {
        "Fail" => "ERRORE", "Warning" => "DA VERIFICARE", "Pass" => "SUPERATO", "NotApplicable" => "NON APPLICABILE",
        "Observed" => "OSSERVATO", "Pending" => "DA ESEGUIRE", _ => status
    };
}

using System.Net.NetworkInformation;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Wisegar.DTXInspector.Core;

namespace Wisegar.DTXInspector.Desktop;

public sealed class ConfigurationWizard : Window
{
    private readonly ConfigurationDraft _draft;
    private readonly List<StackPanel> _pages = [];
    private readonly ContentControl _body = new();
    private readonly TextBlock _heading = new() { FontSize = 19, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock _error = new() { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };
    private readonly Button _back = new() { Content = "Indietro" };
    private readonly Button _next = new() { Content = "Avanti" };
    private readonly Button _save = new() { Content = "Salva" };
    private readonly Button _saveRun = new() { Content = "Salva ed esegui test" };
    private readonly CheckBox _approve = new() { Content = "Confermo le modifiche elencate e gli eventuali dati mancanti." };
    private readonly TextBox _review = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
    private readonly Dictionary<string, string> _formatErrors = [];
    private int _step;
    private bool _saved;
    private bool _closingConfirmed;
    private bool _testing;
    private CancellationTokenSource? _probeCancellation;
    public string? BackupPath { get; private set; }

    public ConfigurationWizard(string path, DtxNodeRole role)
    {
        _draft = new ConfigurationDraft(path, role);
        Title = $"Configura {role} · {Environment.MachineName}";
        Width = 850; Height = 780; MinWidth = 680; MinHeight = 570;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        for (var i = 0; i < 5; i++) _pages.Add(new StackPanel { Spacing = 12 });
        BuildIdentity(); BuildNetwork(); BuildUser(); BuildComponents();
        _pages[4].Children.Add(Info($"File: {path}\nIl salvataggio modifica solo le impostazioni dell'Inspector. Il file originale viene conservato in un backup .bak univoco nella stessa cartella. Formattazione e commenti JSON vengono normalizzati."));
        _pages[4].Children.Add(_review); _pages[4].Children.Add(_approve);
        var cancel = new Button { Content = "Annulla", IsCancel = true };
        cancel.Click += (_, _) => Close(0);
        _back.Click += (_, _) => ShowStep(_step - 1);
        _next.Click += (_, _) => ShowStep(_step + 1);
        _save.Click += (_, _) => Save(false);
        _saveRun.Click += (_, _) => Save(true);
        var actions = new WrapPanel { Children = { cancel, _back, _next, _save, _saveRun } };
        foreach (var child in actions.Children) child.Margin = new Thickness(0, 6, 8, 0);
        var grid = new Grid { Margin = new Thickness(20), RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto") };
        grid.Children.Add(_heading);
        var intro = Info($"Ruolo scelto: {role} · PC attuale: {Environment.MachineName}\nI valori caricati provengono dal file; i dati rilevati diventano impostazioni solo dopo una tua scelta.");
        intro.Margin = new Thickness(0, 10); Grid.SetRow(intro, 1); grid.Children.Add(intro);
        var scroll = new ScrollViewer { Content = _body, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 2); grid.Children.Add(scroll);
        Grid.SetRow(_error, 3); grid.Children.Add(_error); Grid.SetRow(actions, 4); grid.Children.Add(actions);
        Content = grid;
        Closing += async (_, e) =>
        {
            if (_testing) { e.Cancel = true; _probeCancellation?.Cancel(); _error.Text = "Interruzione richiesta. Attendere la fine della prova prima di chiudere."; return; }
            if (_saved || _closingConfirmed || (_formatErrors.Count == 0 && _draft.DescribeChanges() == "Nessuna modifica.")) return;
            e.Cancel = true;
            if (await ConfirmDiscard()) { _closingConfirmed = true; Close(0); }
        };
        ShowStep(0);
    }

    private void BuildIdentity()
    {
        var panel = _pages[0];
        panel.Children.Add(Info("Requisiti fissi per ogni nodo: IPv4 statico e IPv6 disabilitato sulla scheda DTX. Questo strumento verifica Windows, non ne modifica le impostazioni."));
        Field(panel, _draft.Infrastructure, "expectedHostname", "Nome del PC approvato all'installazione", "Usare la baseline documentata; il nome attuale non dimostra che il PC non sia stato rinominato.");
        var adapterField = Field(panel, _draft.Infrastructure, "adapterId", "Scheda DTX (identificatore salvato)", "Rileva le schede e seleziona esplicitamente quella usata da DTX.");
        var adapters = new ComboBox { PlaceholderText = "Scegli una scheda rilevata", HorizontalAlignment = HorizontalAlignment.Stretch };
        adapters.SelectionChanged += (_, _) => { if (adapters.SelectedItem is AdapterChoice choice) adapterField.Text = choice.Id; };
        var detect = new Button { Content = "Rileva dati locali" };
        detect.Click += async (_, _) =>
        {
            detect.IsEnabled = false;
            try
            {
                adapters.ItemsSource = await Task.Run(() => NetworkInterface.GetAllNetworkInterfaces().Select(n =>
                {
                    var properties = n.GetIPProperties();
                    return new AdapterChoice(n.Id, $"{n.Name} · {n.OperationalStatus} · IP: {string.Join(", ", properties.UnicastAddresses.Select(x => x.Address))} · DNS: {string.Join(", ", properties.DnsAddresses)}");
                }).ToArray());
                adapters.SelectedIndex = -1;
            }
            catch (Exception ex) { _error.Text = ex.Message; }
            finally { detect.IsEnabled = true; }
        };
        panel.Children.Add(detect); panel.Children.Add(adapters);
    }

    private void BuildNetwork()
    {
        var panel = _pages[1]; var settings = _draft.Infrastructure;
        Field(panel, settings, "coreHostname", "Nome DNS del server DTX Core", "Inserire il nome DNS, non un URL o un indirizzo con credenziali.");
        var url = new TextBox { PlaceholderText = "URL copiato da Clinic (facoltativo, non viene salvato)" };
        var propose = new Button { Content = "Estrai proposta da URL" };
        propose.Click += async (_, _) =>
        {
            if (!Uri.TryCreate(url.Text, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo))
            { _error.Text = "Usare un URL HTTP/HTTPS senza credenziali."; return; }
            var proposal = new Window { Title = "Proposta", Width = 480, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var cancel = new Button { Content = "Annulla", IsDefault = true, IsCancel = true };
            var accept = new Button { Content = "Usa questo nome Core" };
            cancel.Click += (_, _) => proposal.Close(false); accept.Click += (_, _) => proposal.Close(true);
            proposal.Content = new StackPanel { Margin = new Thickness(20), Spacing = 12, Children = {
                Info($"Nome: {uri.Host}\nPorta URL: {uri.Port}\nLa porta non viene aggiunta automaticamente agli endpoint: confermarne prima funzione e destinazione."), cancel, accept } };
            if (await proposal.ShowDialog<bool>(this))
            {
                var field = panel.Children.OfType<StackPanel>().First().Children.OfType<TextBox>().Single(); field.Text = uri.Host;
            }
        };
        panel.Children.Add(url); panel.Children.Add(propose);
        Field(panel, settings, "expectedCoreAddresses", "IP approvati del Core", "Un indirizzo per riga; non ricavare automaticamente la baseline dal DNS corrente.", true);
        Field(panel, settings, "internalDnsServers", "Server DNS interni approvati", "Un IP per riga. I DNS pubblici non sostituiscono la risoluzione interna.", true);
        Field(panel, settings, "peerHostnames", "Altri nodi DTX", "Un nome DNS per riga, massimo 16. La direzione inversa richiede un test anche sugli altri PC.", true);
        var advanced = new StackPanel { Spacing = 10 };
        Number(advanced, settings, "networkTimeoutMs", "Timeout per tentativo (100–10000 ms)", 2000);
        Number(advanced, settings, "dnsMaxLatencyMs", "Soglia latenza DNS (1–10000 ms)", 250);
        panel.Children.Add(new Expander { Header = "Timeout e soglie", Content = advanced, HorizontalAlignment = HorizontalAlignment.Stretch });
    }

    private void BuildUser()
    {
        var panel = _pages[2];
        panel.Children.Add(Info(_draft.Role == DtxNodeRole.Workstation
            ? "Questi dati servono per verificare i permessi della workstation. L'account elevato dell'Inspector può essere diverso da quello operativo."
            : "Questi campi sono particolarmente rilevanti per le workstation di acquisizione. Compilare solo se pertinenti al nodo."));
        Field(panel, _draft.Infrastructure, "operationalUser", "Utente operativo (DOMINIO\\utente o PC\\utente)", "Nessuna password. I privilegi dell'Inspector non attestano i permessi di questo account.");
        var paths = Field(panel, _draft.Infrastructure, "localDtxDirectories", "Cartelle locali DTX", "Una cartella assoluta per riga, per esempio C:\\ProgramData\\DTX Studio\\Clinic. Niente UNC o unità di rete.", true);
        var browse = new Button { Content = "Sfoglia e aggiungi cartella" };
        browse.Click += async (_, _) =>
        {
            var selected = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Cartella locale DTX", AllowMultiple = true });
            var local = selected.Select(x => x.TryGetLocalPath()).Where(x => x is not null);
            paths.Text = string.Join("\n", Lines(paths.Text).Concat(local!).Distinct(StringComparer.OrdinalIgnoreCase));
        };
        panel.Children.Add(browse);
    }

    private void BuildComponents()
    {
        var panel = _pages[3];
        panel.Children.Add(Info("Gli elementi caricati sono i requisiti salvati, da verificare per la versione DTX installata. Una porta locale non è un endpoint remoto; un collegamento TCP riuscito non certifica il protocollo."));
        Field(panel, _draft.Infrastructure, "dtxServiceNames", "Servizi Windows DTX da verificare nella checklist", "Un nome servizio per riga. Inserire anche servizi attesi che oggi non risultano installati.", true);
        var dicom = new ComboBox { ItemsSource = new[] { "Da confermare", "Sì, richiesto", "No, non richiesto" }, SelectedIndex = _draft.Infrastructure["dicomRequired"] is null ? 0 : _draft.Infrastructure["dicomRequired"]!.GetValue<bool>() ? 1 : 2 };
        dicom.SelectionChanged += (_, _) => _draft.Infrastructure["dicomRequired"] = dicom.SelectedIndex == 0 ? null : JsonValue.Create(dicom.SelectedIndex == 1);
        panel.Children.Add(Info("Comunicazione DICOM TCP 104 richiesta?")); panel.Children.Add(dicom);
        Collection(panel, _draft.Infrastructure, "endpoints", "Collegamenti verso altri nodi", [new("name", "Descrizione"), new("host", "Host"), new("port", "Porta", "number"), new("protocol", "Tipo di comunicazione", "protocol", "tcp")]);
        Collection(panel, _draft.Node, "requiredServices", "Controlli dei servizi Windows", [new("name", "Nome servizio"), new("displayName", "Etichetta"), new("required", "Obbligatorio", "bool", "true"), new("matchDisplayName", "Cerca per nome visualizzato", "bool", "false"), new("expectedStatuses", "Stati attesi (uno per riga)", "list")]);
        Collection(panel, _draft.Node, "requiredProcesses", "Processi locali", [new("name", "Nome processo"), new("displayName", "Etichetta"), new("required", "Obbligatorio", "bool", "true")]);
        Collection(panel, _draft.Node, "requiredTcpListeners", "Porte in ascolto sul PC locale", [new("name", "Etichetta"), new("port", "Porta", "number"), new("address", "Indirizzo locale", Default: "0.0.0.0"), new("required", "Obbligatorio", "bool", "true")]);
        Collection(panel, _draft.Node, "requiredDirectories", "Presenza directory", [new("path", "Percorso"), new("name", "Etichetta"), new("required", "Obbligatorio", "bool", "true")]);
        Collection(panel, _draft.Node, "requiredFiles", "Presenza file", [new("path", "Percorso"), new("name", "Etichetta"), new("required", "Obbligatorio", "bool", "true")]);
        if (_draft.Root["common"] is JsonObject common)
        {
            var inherited = new StackPanel { Spacing = 5 };
            foreach (var list in common.Where(x => x.Value is JsonArray))
                foreach (var row in (JsonArray)list.Value!)
                    inherited.Children.Add(Info($"{list.Key}: {row?["name"] ?? row?["path"] ?? row?["port"]} (ereditato da common)"));
            panel.Children.Add(new Expander { Header = "Controlli comuni ereditati (sola lettura)", Content = inherited });
        }
        var notes = new StackPanel { Spacing = 8 };
        var evidence = _draft.Infrastructure["manualEvidence"] as JsonObject;
        foreach (var id in ConfigurationDraft.ChecklistIds(_draft.Role).Union(evidence?.Select(x => x.Key) ?? []))
        {
            var note = new TextBox { Text = evidence?[id]?.GetValue<string>(), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
            notes.Children.Add(Info(id)); notes.Children.Add(note);
            note.TextChanged += (_, _) =>
            {
                if (_draft.Infrastructure["manualEvidence"] is null) _draft.Infrastructure["manualEvidence"] = new JsonObject();
                _draft.Infrastructure["manualEvidence"]![id] = note.Text;
            };
        }
        panel.Children.Add(new Expander { Header = "Note manuali (non trasformano WARNING in PASS)", Content = notes, HorizontalAlignment = HorizontalAlignment.Stretch });
        var probe = new Button { Content = "Prova collegamenti della bozza" };
        var stop = new Button { Content = "Interrompi prova", IsVisible = false };
        var preview = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        stop.Click += (_, _) => _probeCancellation?.Cancel();
        probe.Click += async (_, _) =>
        {
            try
            {
                ValidateForm(); _testing = true; _probeCancellation = new();
                probe.IsEnabled = _back.IsEnabled = _next.IsEnabled = false; stop.IsVisible = true;
                // Freeze fields but leave the preview cancellation action accessible.
                foreach (var child in panel.Children) if (!ReferenceEquals(child, stop) && !ReferenceEquals(child, preview)) child.IsEnabled = false;
                preview.Text = "DNS: " + string.Join(", ", Lines(_draft.Infrastructure["coreHostname"]?.ToString()).Concat((_draft.Infrastructure["peerHostnames"] as JsonArray ?? []).Select(x => x!.ToString())))
                    + "\nTCP: " + string.Join(", ", (_draft.Infrastructure["endpoints"] as JsonArray ?? []).Select(x => $"{x?["host"]}:{x?["port"]}")) + "\nProva in corso…";
                var results = await _draft.PreviewNetworkAsync(_probeCancellation.Token);
                preview.Text = "Anteprima senza salvataggio o report di conformità.\n" + (results.Count == 0 ? "Nessun target configurato: nessuna prova eseguita." : string.Join("\n", results.Select(x => $"{x.Status} · {x.Name}: {x.Message}")));
            }
            catch (OperationCanceledException) { preview.Text = "Prova interrotta; nessuna impostazione salvata."; }
            catch (Exception ex) { _error.Text = ex.Message; }
            finally
            {
                _testing = false; _probeCancellation?.Dispose(); _probeCancellation = null;
                foreach (var child in panel.Children) child.IsEnabled = true;
                probe.IsEnabled = _back.IsEnabled = _next.IsEnabled = true; stop.IsVisible = false;
            }
        };
        panel.Children.Add(probe); panel.Children.Add(stop); panel.Children.Add(preview);
    }

    private void ShowStep(int step)
    {
        if (step < 0 || step > 4) return;
        _error.Text = "";
        if (step == 4)
        {
            try
            {
                ValidateForm();
                var missing = _draft.Missing();
                _review.Text = _draft.DescribeChanges() + "\n\nDati mancanti:\n" + (missing.Count == 0 ? "Nessuno dei campi essenziali." : string.Join("\n", missing))
                    + "\n\nRestano verifiche manuali: direzione remota del DNS, salute applicativa, privilegi di installazione, traffico non ispezionato, TLS/DPI e indipendenza da SMB.";
                _save.Content = missing.Count > 0 ? "Salva incompleto" : "Salva";
                _approve.IsChecked = false;
            }
            catch (Exception ex) { _error.Text = ex.Message; return; }
        }
        _step = step;
        _heading.Text = $"{step + 1}/5 · {new[] { "Identità e scheda DTX", "Server Core e DNS", "Utente e dati locali", "Servizi e comunicazioni", "Riepilogo e salvataggio" }[step]}";
        _body.Content = _pages[step]; _back.IsEnabled = step > 0;
        _next.IsVisible = step < 4; _save.IsVisible = _saveRun.IsVisible = step == 4;
    }

    private void ValidateForm()
    {
        if (_formatErrors.Count > 0) throw new InvalidOperationException(string.Join("\n", _formatErrors.Values));
        _draft.Validate();
    }

    private void Save(bool run)
    {
        try
        {
            if (_approve.IsChecked != true) { _error.Text = "Confermare il riepilogo delle modifiche prima del salvataggio."; return; }
            ValidateForm(); BackupPath = _draft.Save(); _saved = true; Close(run ? 2 : 1);
        }
        catch (Exception ex) { _error.Text = ex.Message; }
    }

    private TextBox Field(StackPanel panel, JsonObject target, string key, string title, string help, bool list = false)
    {
        var input = new TextBox { Text = list ? string.Join("\n", (target[key] as JsonArray ?? []).Select(x => x?.ToString())) : target[key]?.ToString(), AcceptsReturn = list, TextWrapping = TextWrapping.Wrap, MinHeight = list ? 65 : 32 };
        var initialText = input.Text;
        var existed = target.ContainsKey(key);
        var initialValue = target[key]?.DeepClone();
        panel.Children.Add(new StackPanel { Spacing = 4, Children = { new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap }, Info(help), input } });
        input.TextChanged += (_, _) =>
        {
            if (input.Text == initialText)
            {
                if (existed) target[key] = initialValue?.DeepClone(); else target.Remove(key);
            }
            else target[key] = list ? new JsonArray(Lines(input.Text).Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()) : JsonValue.Create(input.Text?.Trim());
        };
        return input;
    }

    private string Number(StackPanel panel, JsonObject target, string key, string title, int defaultValue)
    {
        var input = new TextBox { Text = target[key]?.ToString() ?? defaultValue.ToString() };
        var errorKey = Guid.NewGuid().ToString();
        var validation = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(Info(title)); panel.Children.Add(input);
        panel.Children.Add(validation);
        input.TextChanged += (_, _) =>
        {
            if (int.TryParse(input.Text, out var value)) { target[key] = value; _formatErrors.Remove(errorKey); }
            else _formatErrors[errorKey] = $"{title}: inserire un numero intero.";
            validation.Text = _formatErrors.GetValueOrDefault(errorKey);
        };
        return errorKey;
    }

    private void Collection(StackPanel parent, JsonObject target, string key, string title, FieldSpec[] fields)
    {
        var rows = new StackPanel { Spacing = 10 };
        var body = new StackPanel { Spacing = 10 };
        var add = new Button { Content = "Aggiungi" };
        var array = target[key] as JsonArray;
        if (array is not null) foreach (var item in array.OfType<JsonObject>()) AddRow(item);
        add.Click += (_, _) =>
        {
            if (array is null) { array = new JsonArray(); target[key] = array; }
            var item = new JsonObject();
            foreach (var field in fields)
                if (field.Kind == "bool") item[field.Key] = field.Default == "true";
                else if (field.Kind == "number") item[field.Key] = 0;
                else if (field.Kind == "list") item[field.Key] = new JsonArray();
                else item[field.Key] = field.Default;
            array.Add(item); AddRow(item);
        };
        body.Children.Add(rows); body.Children.Add(add);
        parent.Children.Add(new Expander { Header = title, Content = body, HorizontalAlignment = HorizontalAlignment.Stretch });
        void AddRow(JsonObject item)
        {
            var row = new StackPanel { Spacing = 5, Margin = new Thickness(8) };
            var rowErrorKeys = new List<string>();
            foreach (var field in fields)
            {
                if (field.Kind == "protocol")
                {
                    var input = new ComboBox { ItemsSource = new[] { "tcp", "rest", "grpc", "dynamic-tcp", "dicom" }, SelectedItem = item[field.Key]?.ToString() ?? "tcp" };
                    input.SelectionChanged += (_, _) => item[field.Key] = input.SelectedItem?.ToString();
                    row.Children.Add(Info(field.Label)); row.Children.Add(input);
                }
                else if (field.Kind == "bool")
                {
                    var input = new CheckBox { Content = field.Label, IsChecked = item[field.Key]?.GetValue<bool>() ?? field.Default == "true" };
                    input.IsCheckedChanged += (_, _) => item[field.Key] = input.IsChecked == true; row.Children.Add(input);
                }
                else if (field.Kind == "number") rowErrorKeys.Add(Number(row, item, field.Key, field.Label, 0));
                else Field(row, item, field.Key, field.Label, "", field.Kind == "list");
            }
            var remove = new Button { Content = "Rimuovi questa voce" };
            remove.Click += (_, _) => { array!.Remove(item); rows.Children.Remove(row); foreach (var errorKey in rowErrorKeys) _formatErrors.Remove(errorKey); };
            row.Children.Add(remove); rows.Children.Add(row);
        }
    }

    private async Task<bool> ConfirmDiscard()
    {
        var dialog = new Window { Title = "Scartare la bozza?", Width = 420, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var keep = new Button { Content = "Continua a modificare", IsDefault = true, IsCancel = true };
        var discard = new Button { Content = "Scarta modifiche" };
        keep.Click += (_, _) => dialog.Close(false); discard.Click += (_, _) => dialog.Close(true);
        dialog.Content = new StackPanel { Margin = new Thickness(20), Spacing = 15, Children = { Info("Le modifiche non salvate andranno perse."), keep, discard } };
        return await dialog.ShowDialog<bool>(this);
    }

    private static string[] Lines(string? value) => (value ?? "").Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    private static TextBlock Info(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };
    private sealed record AdapterChoice(string Id, string Description) { public override string ToString() => Description; }
    private sealed record FieldSpec(string Key, string Label, string Kind = "text", string? Default = null);
}

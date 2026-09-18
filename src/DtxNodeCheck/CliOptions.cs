namespace DtxNodeCheck;

using DtxNodeCheck.Reporting;

internal sealed record CliOptions(
    AppMode Mode,
    string? ConfigPath,
    NodeKind? Node,
    string? ReportPath,
    string? LogPath,
    ReportFormat Format,
    bool OpenReport,
    bool ShowHelp)
{
    public static string HelpText =>
        """
        Uso:
          DtxNodeCheck.exe --config .\dtx-node-check.json --node workstation
          DtxNodeCheck.exe --config .\dtx-node-check.json --node core --report .\DTX-Core-Report.txt
          DtxNodeCheck.exe --config .\dtx-node-check.json --node client --report .\DTX-Client-Report.json --format json
          DtxNodeCheck.exe --config .\dtx-node-check.json --node core --report .\DTX-Core-Report.html --format html --open-report
          DtxNodeCheck.exe --config .\dtx-node-check.json --node core --log .\DtxNodeCheck-debug.log
          DtxNodeCheck.exe --config .\dtx-node-check.json --node core --report .\DTX-Core-Report.txt --open-report
          DtxNodeCheck.exe --inventory --report .\DTX-Inventory.json --format json --open-report

        Opzioni:
          --inventory        Crea un inventario read-only del PC per preparare configurazioni.
          --config <file>    File JSON di configurazione. Obbligatorio nei controlli nodo.
          --node <role>      Nodo da controllare: core, workstation o client. Obbligatorio nei controlli nodo.
          --report <file>    Scrive il report nel percorso indicato. Opzionale.
          --log <file>       Scrive log debug/info/eccezioni nel percorso indicato. Opzionale.
          --open-report      Apre il report alla fine dell'esecuzione. Richiede --report.
          --format <format>  Formato report: text, json o html. Default: text.
          --help             Mostra questo aiuto.
        """;

    public static CliOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            throw new CliException("Errore: parametri mancanti.");
        }

        string? configPath = null;
        NodeKind? node = null;
        string? reportPath = null;
        string? logPath = null;
        var mode = AppMode.Check;
        var format = ReportFormat.Text;
        var openReport = false;
        var showHelp = false;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--help":
                case "-h":
                case "/?":
                    showHelp = true;
                    break;
                case "--config":
                    configPath = ReadValue(args, ref i, "--config");
                    break;
                case "--inventory":
                    mode = AppMode.Inventory;
                    break;
                case "--node":
                    node = ParseNode(ReadValue(args, ref i, "--node"));
                    break;
                case "--report":
                    reportPath = ReadValue(args, ref i, "--report");
                    break;
                case "--log":
                    logPath = ReadValue(args, ref i, "--log");
                    break;
                case "--format":
                    format = ParseFormat(ReadValue(args, ref i, "--format"));
                    break;
                case "--open-report":
                    openReport = true;
                    break;
                default:
                    throw new CliException($"Errore: opzione non riconosciuta '{arg}'.");
            }
        }

        if (showHelp)
        {
            return new CliOptions(mode, null, node, null, logPath, ReportFormat.Text, openReport, true);
        }

        if (mode == AppMode.Check && string.IsNullOrWhiteSpace(configPath))
        {
            throw new CliException("Errore: --config e' obbligatorio.");
        }

        if (mode == AppMode.Check && node is null)
        {
            throw new CliException("Errore: --node e' obbligatorio.");
        }

        if (mode == AppMode.Inventory && node is not null)
        {
            throw new CliException("Errore: --inventory non usa --node.");
        }

        if (openReport && string.IsNullOrWhiteSpace(reportPath))
        {
            throw new CliException("Errore: --open-report richiede --report.");
        }

        return new CliOptions(mode, configPath, node, reportPath, logPath, format, openReport, false);
    }

    public static string? TryReadLogPath(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] != "--log")
            {
                continue;
            }

            if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                return null;
            }

            return args[i + 1];
        }

        return null;
    }

    private static string ReadValue(IReadOnlyList<string> args, ref int index, string option)
    {
        if (index + 1 >= args.Count)
        {
            throw new CliException($"Errore: {option} richiede un valore.");
        }

        var value = args[++index];
        if (value.StartsWith("--", StringComparison.Ordinal))
        {
            throw new CliException($"Errore: {option} richiede un valore.");
        }

        return value;
    }

    private static NodeKind ParseNode(string value) =>
        value.ToLowerInvariant() switch
        {
            "core" => NodeKind.Core,
            "workstation" => NodeKind.Workstation,
            "client" => NodeKind.Client,
            _ => throw new CliException("Errore: --node deve essere core, workstation o client.")
        };

    private static ReportFormat ParseFormat(string value) =>
        value.ToLowerInvariant() switch
        {
            "text" => ReportFormat.Text,
            "json" => ReportFormat.Json,
            "html" => ReportFormat.Html,
            _ => throw new CliException("Errore: --format deve essere text, json o html.")
        };
}

internal sealed class CliException(string message) : Exception(message);

internal enum AppMode
{
    Check,
    Inventory
}

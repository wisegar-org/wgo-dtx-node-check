using DtxNodeCheck.Checks;
using DtxNodeCheck.Configuration;
using DtxNodeCheck.Diagnostics;
using DtxNodeCheck.Inventory;
using DtxNodeCheck.Reporting;

namespace DtxNodeCheck;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("Errore: DtxNodeCheck puo' essere eseguito solo su Windows.");
                return ExitCodes.UnsupportedOperatingSystem;
            }

            DebugLog.TryInitialize(CliOptions.TryReadLogPath(args));
            DebugLog.Info("Avvio DtxNodeCheck.", new Dictionary<string, string?>
            {
                ["argumentCount"] = args.Length.ToString()
            });

            var options = CliOptions.Parse(args);
            if (options.ShowHelp)
            {
                DebugLog.Info("Richiesto help CLI.");
                Console.WriteLine(CliOptions.HelpText);
                return ExitCodes.Success;
            }

            DebugLog.Info("Opzioni CLI valide.", new Dictionary<string, string?>
            {
                ["mode"] = options.Mode.ToString(),
                ["config"] = options.ConfigPath,
                ["node"] = options.Node?.Key(),
                ["report"] = options.ReportPath,
                ["openReport"] = options.OpenReport.ToString(),
                ["logEnabled"] = DebugLog.IsEnabled.ToString()
            });

            var run = Run(options);
            var output = ReportRenderer.Render(run, options.Format);

            Console.WriteLine(output);

            if (options.ReportPath is not null)
            {
                DebugLog.Info("Scrittura report richiesta.", new Dictionary<string, string?>
                {
                    ["report"] = options.ReportPath,
                    ["format"] = options.Format.ToString()
                });
                ReportWriter.Write(options.ReportPath, output);

                if (options.OpenReport)
                {
                    ReportOpener.Open(options.ReportPath);
                }
            }

            DebugLog.Info("Esecuzione completata.", new Dictionary<string, string?>
            {
                ["mode"] = options.Mode.ToString(),
                ["exitCode"] = run.ExitCode.ToString()
            });

            return run.ExitCode;
        }
        catch (CliException ex)
        {
            DebugLog.Exception(ex, "Errore CLI.");
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine();
            Console.Error.WriteLine(CliOptions.HelpText);
            return ExitCodes.UsageOrConfigurationError;
        }
        catch (ConfigurationException ex)
        {
            DebugLog.Exception(ex, "Errore di configurazione.");
            Console.Error.WriteLine($"Errore di configurazione: {ex.Message}");
            return ExitCodes.UsageOrConfigurationError;
        }
        catch (UnauthorizedAccessException ex)
        {
            DebugLog.Exception(ex, "Permessi insufficienti.");
            Console.Error.WriteLine($"Permessi insufficienti: {ex.Message}");
            return ExitCodes.UsageOrConfigurationError;
        }
        catch (IOException ex)
        {
            DebugLog.Exception(ex, "Errore I/O.");
            Console.Error.WriteLine($"Errore I/O: {ex.Message}");
            return ExitCodes.UsageOrConfigurationError;
        }
        catch (Exception ex)
        {
            DebugLog.Exception(ex, "Errore inatteso.");
            Console.Error.WriteLine($"Errore inatteso: {ex.Message}");
            return ExitCodes.UsageOrConfigurationError;
        }
    }

    private static IRunResult Run(CliOptions options)
    {
        if (options.Mode == AppMode.Inventory)
        {
            return InventoryRunner.Run();
        }

        var configuration = CheckConfigurationLoader.Load(options.ConfigPath!);
        var profile = configuration.BuildProfile(options.Node!.Value);
        return CheckRunner.Run(configuration, profile);
    }
}

internal static class ExitCodes
{
    public const int Success = 0;
    public const int CheckFailed = 1;
    public const int UsageOrConfigurationError = 2;
    public const int UnsupportedOperatingSystem = 3;
}

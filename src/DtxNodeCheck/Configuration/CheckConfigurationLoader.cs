using System.Text.Json;
using DtxNodeCheck.Diagnostics;

namespace DtxNodeCheck.Configuration;

internal static class CheckConfigurationLoader
{
    public static CheckConfiguration Load(string path)
    {
        DebugLog.Info("Caricamento configurazione.", new Dictionary<string, string?>
        {
            ["path"] = path
        });

        if (!File.Exists(path))
        {
            throw new ConfigurationException($"File non trovato: {path}");
        }

        try
        {
            using var stream = File.OpenRead(path);
            var configuration = JsonSerializer.Deserialize(stream, CheckConfigurationJsonContext.Default.CheckConfiguration);
            if (configuration is null)
            {
                throw new ConfigurationException("Il file JSON e' vuoto.");
            }

            Validate(configuration);
            DebugLog.Info("Configurazione caricata e validata.", new Dictionary<string, string?>
            {
                ["environmentName"] = configuration.EnvironmentName,
                ["hasCore"] = (configuration.Nodes.Core is not null).ToString(),
                ["hasWorkstation"] = (configuration.Nodes.Workstation is not null).ToString(),
                ["hasClient"] = (configuration.Nodes.Client is not null).ToString()
            });
            return configuration;
        }
        catch (JsonException ex)
        {
            DebugLog.Exception(ex, "JSON configurazione non valido.");
            throw new ConfigurationException($"JSON non valido: {ex.Message}");
        }
    }

    private static void Validate(CheckConfiguration configuration)
    {
        ValidateSet("common", configuration.Common);
        ValidateNode(NodeKind.Core, configuration.Nodes.Core);
        ValidateNode(NodeKind.Workstation, configuration.Nodes.Workstation);
        ValidateNode(NodeKind.Client, configuration.Nodes.Client);
    }

    private static void ValidateNode(NodeKind node, CheckSet? set)
    {
        if (set is not null)
        {
            ValidateSet($"nodes.{node.Key()}", set);
        }
    }

    private static void ValidateSet(string name, CheckSet set)
    {
        foreach (var item in set.RequiredDirectories)
        {
            RequireText(item.Path, $"{name}.requiredDirectories[].path");
        }

        foreach (var item in set.RequiredFiles)
        {
            RequireText(item.Path, $"{name}.requiredFiles[].path");
        }

        foreach (var item in set.RequiredProcesses)
        {
            RequireText(item.Name, $"{name}.requiredProcesses[].name");
        }

        foreach (var item in set.RequiredServices)
        {
            RequireText(item.Name, $"{name}.requiredServices[].name");
        }

        foreach (var item in set.RequiredTcpListeners)
        {
            if (item.Port is < 1 or > 65535)
            {
                throw new ConfigurationException($"{name}.requiredTcpListeners[].port deve essere tra 1 e 65535.");
            }
        }
    }

    private static void RequireText(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ConfigurationException($"{field} e' obbligatorio.");
        }
    }
}

internal sealed class ConfigurationException(string message) : Exception(message);

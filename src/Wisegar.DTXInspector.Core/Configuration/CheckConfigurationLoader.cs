using System.Text.Json;
using Wisegar.DTXInspector.Diagnostics;

namespace Wisegar.DTXInspector.Configuration;

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
        if (set.Infrastructure is { } infrastructure)
        {
            if (infrastructure.NetworkTimeoutMs is < 100 or > 10000 || infrastructure.DnsMaxLatencyMs is < 1 or > 10000)
                throw new ConfigurationException($"{name}.infrastructure: timeout 100..10000 ms, latenza DNS 1..10000 ms.");
            if (infrastructure.PeerHostnames is null || infrastructure.Endpoints is null || infrastructure.InternalDnsServers is null
                || infrastructure.ExpectedCoreAddresses is null || infrastructure.LocalDtxDirectories is null
                || infrastructure.DtxServiceNames is null || infrastructure.ManualEvidence is null)
                throw new ConfigurationException($"{name}.infrastructure: liste e manualEvidence non possono essere null.");
            if (infrastructure.PeerHostnames.Length > 16 || infrastructure.Endpoints.Length > 16)
                throw new ConfigurationException($"{name}.infrastructure: massimo 16 peer e 16 endpoint per nodo.");
            foreach (var host in infrastructure.PeerHostnames)
                ValidateHost(host, name);
            if (!string.IsNullOrWhiteSpace(infrastructure.CoreHostname)) ValidateHost(infrastructure.CoreHostname, name);
            foreach (var address in infrastructure.InternalDnsServers.Concat(infrastructure.ExpectedCoreAddresses))
                if (!System.Net.IPAddress.TryParse(address, out _))
                    throw new ConfigurationException($"{name}.infrastructure: indirizzo IP non valido: {address}");
            foreach (var endpoint in infrastructure.Endpoints)
            {
                if (endpoint is null) throw new ConfigurationException($"{name}.infrastructure: endpoint nullo.");
                RequireText(endpoint.Name, $"{name}.infrastructure.endpoints[].name");
                ValidateHost(endpoint.Host, name);
                if (endpoint.Port is < 1 or > 65535 || endpoint.Protocol is not ("rest" or "grpc" or "dynamic-tcp" or "dicom" or "tcp"))
                    throw new ConfigurationException($"{name}.infrastructure: porta o protocollo endpoint non valido.");
                if (endpoint.Protocol == "dicom" && (infrastructure.DicomRequired != true || endpoint.Port != 104))
                    throw new ConfigurationException($"{name}.infrastructure: endpoint DICOM richiede dicomRequired=true e porta 104.");
            }
        }
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

    private static void ValidateHost(string host, string field)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Length > 253 || System.Uri.CheckHostName(host) == System.UriHostNameType.Unknown)
            throw new ConfigurationException($"{field}.infrastructure: hostname/IP non valido: {host}");
    }
}

internal sealed class ConfigurationException(string message) : Exception(message);

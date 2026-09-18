using System.Buffers.Binary;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Wisegar.DTXInspector;
using Wisegar.DTXInspector.Checks;
using Wisegar.DTXInspector.Configuration;
using Wisegar.DTXInspector.Inventory;
using Wisegar.DTXInspector.Reporting;

namespace Wisegar.DTXInspector.Desktop.Smoke;

internal static class DtxInspectionChecks
{
    internal static void Run()
    {
        var configuration = CheckConfigurationLoader.Load(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
        var profile = configuration.BuildProfile(NodeKind.Core) with { Infrastructure = new() { AdapterId = "nic" } };
        var snapshot = new InfrastructureSnapshot("TEST-PC", [new("nic", "Ethernet", "NIC", true, false,
            ["10.0.0.1"], [], ["10.0.0.2"])], [],
            [new("core-key", "DTX Studio Core", "Running", 101), new("other-service", "Other", "Running", 101)], [], true, "User", []);
        InspectedProcess[] processes = [new(101, "java"), new(102, "Other"), new(103, "WgoDtxInspector"), new(104, "DTXStudioClinic")];
        OwnedTcpEndpoint[] sockets = [
            new("0.0.0.0", 55555, "0.0.0.0", 0, TcpState.Listen, 101),
            new("127.0.0.1", 26850, "0.0.0.0", 0, TcpState.Listen, 102),
            new("127.0.0.1", 49999, "0.0.0.0", 0, TcpState.Listen, 103),
            new("10.0.0.1", 50000, "10.0.0.10", 26850, TcpState.Established, 104)];
        var results = DtxInspection.Analyze(profile, snapshot, processes, sockets, ["TCP IPv6: access denied"]);
        var correlated = results.Single(r => r.Category == "dtx-tcp" && r.Details["localPort"] == "55555");
        Assert(correlated.Details["pid"] == "101" && correlated.Details["servicesInProcess"]!.Contains("other-service"),
            "Unknown port linked by service PID, including shared service ambiguity");
        Assert(results.Single(r => r.Category == "dtx-tcp" && r.Details["localPort"] == "26850").Status == CheckStatus.Warning,
            "Known port owned by unrelated process is only a candidate");
        Assert(!results.Any(r => r.Category == "dtx-tcp" && r.Details["pid"] == "103"), "Inspector excluded from DTX candidates");
        Assert(results.Any(r => r.Category == "dtx-tcp" && r.Details["remoteAddress"] == "10.0.0.10"), "DTX remote TCP address recorded");
        Assert(results.Any(r => r.Category == "dtx-network" && r.Details["dnsServers"] == "10.0.0.2"), "Adapter DNS recorded");
        Assert(results.Any(r => r.Message == "TCP IPv6: access denied" && r.Status == CheckStatus.Warning), "Partial failures visible");
        var report = new CheckRun(DateTimeOffset.Now, "TEST-PC", "DOMAIN", "User", "Windows", "X64", "Inspection", profile,
            results.Concat(InfrastructureChecklist.Run(profile, snapshot)).ToArray());
        foreach (var format in Enum.GetValues<ReportFormat>())
        {
            var rendered = ReportRenderer.Render(report, format);
            Assert(rendered.Contains("55555") && rendered.Contains("servicesInProcess") && rendered.Contains("security-inspection"),
                "Every report format preserves inspection evidence and mandatory checklist");
        }

        var bytes = new byte[60];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        IPAddress.IPv6Loopback.GetAddressBytes().CopyTo(bytes, 4);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(24), 26850);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(52), (int)TcpState.Listen);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(56), 1234);
        var decoded = WindowsTcpOwners.Decode(bytes, true).Single();
        Assert(decoded.LocalAddress == "::1" && decoded.LocalPort == 26850 && decoded.ProcessId == 1234, "IPv6 native layout decoded");
        var rejected = false;
        try { WindowsTcpOwners.Decode(bytes[..10], true); }
        catch (InvalidDataException) { rejected = true; }
        Assert(rejected, "Truncated TCP table rejected");

        if (OperatingSystem.IsWindows())
        {
            VerifyLocalOwner(IPAddress.Loopback);
            if (Socket.OSSupportsIPv6) VerifyLocalOwner(IPAddress.IPv6Loopback);
        }
        Console.WriteLine("PASS: DTX PID/service correlation, unrelated ports, IP/DNS evidence, native TCP IPv4/IPv6 owners and reports.");
    }

    private static void VerifyLocalOwner(IPAddress address)
    {
        using var listener = new TcpListener(address, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var errors = new List<string>();
        var endpoints = WindowsTcpOwners.Read(errors);
        Assert(errors.Count == 0, "Native TCP tables readable: " + string.Join("; ", errors));
        Assert(endpoints.Any(s => s.LocalPort == port && s.LocalAddress == address.ToString()
            && s.ProcessId == Environment.ProcessId && s.State == TcpState.Listen), "Actual local listener mapped to current PID");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

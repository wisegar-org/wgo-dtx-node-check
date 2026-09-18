using System.Net;
using Wisegar.DTXInspector;
using Wisegar.DTXInspector.Checks;
using Wisegar.DTXInspector.Configuration;
using Wisegar.DTXInspector.Core;
using Wisegar.DTXInspector.Inventory;
using Wisegar.DTXInspector.Reporting;

namespace Wisegar.DTXInspector.Desktop.Smoke;

internal static class InfrastructureChecks
{
    internal static void Run()
    {
        var configuration = CheckConfigurationLoader.Load(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
        var snapshot = new InfrastructureSnapshot("TEST-PC",
            [new("nic", "Ethernet", "Physical", true, false, ["10.0.0.10"], [], ["10.0.0.1"])],
            [], [new ServiceInventory("DTXCore", "DTX Core", "Running")], [], true, "DOMAIN\\Admin", []);
        foreach (var role in Enum.GetValues<NodeKind>())
        {
            var profile = configuration.BuildProfile(role);
            var results = InfrastructureChecklist.Run(profile, snapshot);
            var ids = results.Select(r => r.Details["checkId"]).ToHashSet();
            string[] common = ["ip", "ipv6", "hostname", "core-dns", "vpn", "hosts", "bidirectional-dns", "dns-fallback",
                "traffic", "rest", "grpc", "dynamic-tcp", "dicom", "security-inspection", "smb"];
            Assert(common.All(ids.Contains), "Mandatory shared checklist must survive empty lists");
            Assert(ids.Contains("services") == (role == NodeKind.Core), "Core services only on Core report");
            Assert(ids.Contains("service-health") == (role == NodeKind.Core), "Core application health retained");
            Assert(ids.Contains("permissions") == (role == NodeKind.Workstation), "Operational permissions only on workstation");
            Assert(ids.Contains("installation-admin") == (role == NodeKind.Workstation), "Installer admin evidence required on workstation");
            Assert(results.Single(r => r.Details["checkId"] == "hostname").Status == CheckStatus.Warning, "Missing baseline is not PASS");
            var report = new CheckRun(DateTimeOffset.Now, snapshot.Hostname, "DOMAIN", "Admin", "Windows", "X64", "Test", profile, results);
            Assert(report.Assessment.Contains("INCOMPLETA"), "Warnings must prevent complete assessment");
            foreach (var format in Enum.GetValues<ReportFormat>())
            {
                var rendered = ReportRenderer.Render(report, format);
                Assert(rendered.Contains(role.Key()), "Every report identifies its role");
                Assert(rendered.Contains("security-inspection") && rendered.Contains("smb"), "Every report format includes checklist evidence");
                Assert(rendered.Contains("INCOMPLETA"), "Every report format includes incomplete assessment");
            }
        }
        var settings = new InfrastructureSettings { AdapterId = "nic", ExpectedHostname = "TEST-PC", InternalDnsServers = ["10.0.0.1"],
            DicomRequired = false, ClientRequiresIpv6Disabled = false };
        var core = configuration.BuildProfile(NodeKind.Core) with { Infrastructure = settings };
        CheckResult Check(string id, NodeProfile profile, InfrastructureSnapshot data) =>
            InfrastructureChecklist.Run(profile, data).Single(r => r.Details["checkId"] == id);
        var namedService = new ServiceInventory("VendorInternalKey", "DTX Studio Core", "Running");
        var serviceSnapshot = snapshot with { Services = [namedService] };
        Assert(Check("services", core, serviceSnapshot).Status == CheckStatus.Pass,
            "Documented display name resolves real internal service key");
        Assert(Check("services", core, snapshot with { Services = [namedService with { Status = "Stopped" }] }).Status == CheckStatus.Fail,
            "Discovered stopped Core service fails mandatory checklist");
        Assert(Check("services", core, snapshot with { Services = [] }).Status == CheckStatus.Warning,
            "Missing version-specific service stays unverified");
        Assert(Check("services", core, snapshot with { Services = [namedService, namedService with { Name = "AnotherKey" }] }).Status == CheckStatus.Warning,
            "Ambiguous display names cannot certify service state");
        Assert(!new ServiceCheck("DTX Studio Core").Matches(namedService),
            "Internal name matching remains the default for existing configs");
        Assert(new ServiceCheck("dtx studio core", MatchDisplayName: true).Matches(namedService),
            "Display matching is exact and case insensitive");
        Assert(Check("ip", core, snapshot).Status == CheckStatus.Pass, "Static IPv4 accepted for selected adapter");
        var dhcp = snapshot with { Adapters = [snapshot.Adapters[0] with { Dhcp = true }] };
        Assert(Check("ip", core, dhcp).Status == CheckStatus.Fail, "Core DHCP rejected");
        var client = configuration.BuildProfile(NodeKind.Client) with { Infrastructure = settings };
        Assert(Check("ip", client, dhcp).Status == CheckStatus.Warning, "Client DHCP needs stable DNS proof");
        Assert(Check("ip", client, snapshot with { Adapters = [snapshot.Adapters[0] with { Ipv4 = ["169.254.1.1"] }] }).Status == CheckStatus.Fail,
            "APIPA is not a valid static client address");
        Assert(Check("ipv6", client, snapshot).Status == CheckStatus.NotApplicable, "Client IPv6 applicability explicit");
        Assert(Check("dicom", core, snapshot).Status == CheckStatus.NotApplicable, "DICOM applicability explicit");
        Assert(Check("hostname", core, snapshot with { Hostname = "RENAMED" }).Status == CheckStatus.Fail, "Renamed host rejected");
        Assert(Check("ipv6", core, snapshot).Status == CheckStatus.Warning, "No IPv6 addresses is not proof of disabled binding");
        Assert(Check("ipv6", core, snapshot with { Adapters = [snapshot.Adapters[0] with { Ipv6 = ["fe80::1"] }] }).Status == CheckStatus.Fail,
            "IPv6 presence rejected when disabled is required");
        Assert(Check("service-health", core, snapshot).Status == CheckStatus.Warning, "Running service does not prove application health");
        var first = NodeCheckService.CreateReportPathForRun("report.html", DtxNodeRole.Core, "PC");
        var second = NodeCheckService.CreateReportPathForRun("report.html", DtxNodeRole.Core, "PC");
        var third = NodeCheckService.CreateReportPathForRun("report.html", DtxNodeRole.Client, "PC");
        Assert(first != second && first != third && Path.GetFileName(first).StartsWith("DTX-core-PC-"), "Reports isolated per machine, role and run");
        Task.Run(() => NetworkChecksAsync(core)).GetAwaiter().GetResult();
        if (OperatingSystem.IsWindows())
        {
            foreach (var role in Enum.GetValues<NodeKind>())
            {
                var localProfile = configuration.BuildProfile(role) with { Infrastructure = new() };
                var actual = Task.Run(() => CheckRunner.Run(configuration, localProfile)).GetAwaiter().GetResult();
                Assert(actual.Results.Count(result => result.Category == InfrastructureChecklist.Category)
                    == InfrastructureChecklist.Items(role).Count, "Real local run retains every mandatory checklist item");
                Assert(actual.Profile.Node == role, "Real run keeps selected role separate");
            }
        }
        Console.WriteLine("PASS: mandatory role checklist, honest statuses, separate reports and bounded DNS/TCP probes.");
    }

    private static async Task NetworkChecksAsync(NodeProfile core)
    {
        var settings = new InfrastructureSettings { CoreHostname = "core.test", ExpectedCoreAddresses = ["10.0.0.10"],
            NetworkTimeoutMs = 100, DnsMaxLatencyMs = 1000,
            Endpoints = [new("REST", "core.test", 5000, "rest")] };
        var profile = core with { Infrastructure = settings };
        var probe = new FakeProbe();
        var results = await NetworkChecks.RunAsync(profile, probe);
        Assert(probe.DnsCalls == 3 && probe.TcpCalls == 1, "Every configured probe runs with three DNS samples");
        Assert(results.All(r => r.Status == CheckStatus.Pass), "Matching fast DNS and reachable TCP pass only their probe scope");
        probe.Address = "10.0.0.11";
        results = await NetworkChecks.RunAsync(profile, probe);
        Assert(results.Single(r => r.Category == "network-dns").Status == CheckStatus.Fail, "Wrong Core IP rejected");
        probe.Hang = true;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        results = await NetworkChecks.RunAsync(profile, probe);
        Assert(results.All(r => r.Status == CheckStatus.Fail) && watch.Elapsed < TimeSpan.FromSeconds(3), "DNS/TCP timeout bounded even if provider ignores cancellation");
        var empty = new FakeProbe();
        await NetworkChecks.RunAsync(core with { Infrastructure = new() }, empty);
        Assert(empty.DnsCalls == 0 && empty.TcpCalls == 0, "No inferred network targets or ports");
    }

    private sealed class FakeProbe : INetworkProbe
    {
        internal int DnsCalls, TcpCalls;
        internal string Address = "10.0.0.10";
        internal bool Hang;
        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellation)
        {
            DnsCalls++;
            return Hang ? new TaskCompletionSource<IPAddress[]>().Task : Task.FromResult(new[] { IPAddress.Parse(Address) });
        }
        public Task ConnectAsync(string host, int port, CancellationToken cancellation)
        {
            TcpCalls++;
            return Hang ? new TaskCompletionSource().Task : Task.CompletedTask;
        }
    }

    private static void Assert(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}

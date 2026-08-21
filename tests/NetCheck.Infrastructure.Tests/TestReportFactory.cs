using NetCheck.Core.Models;

namespace NetCheck.Infrastructure.Tests;

internal static class TestReportFactory
{
    public static DiagnosticReport Create(string machineName = "PRIVATE-PC")
    {
        var adapter = new NetworkAdapterSnapshot
        {
            Id = "adapter-1",
            Name = "Ethernet <primary>",
            Description = "Test adapter",
            OperationalStatus = "Up",
            InterfaceType = "Ethernet",
            MacAddress = "AA-BB-CC-DD-EE-FF",
            IpAddresses = ["192.168.1.20"],
            Gateways = ["192.168.1.1"],
            DnsServers = ["1.1.1.1"]
        };

        return new DiagnosticReport
        {
            Id = Guid.Parse("8c9ef0e2-a00c-4a78-81bc-329632749bf2"),
            StartedAtUtc = new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.Zero),
            CompletedAtUtc = new DateTimeOffset(2026, 8, 21, 10, 0, 5, TimeSpan.Zero),
            Network = new NetworkSnapshot
            {
                MachineName = machineName,
                NetworkAvailable = true,
                Adapters = [adapter],
                PrimaryAdapter = adapter
            },
            Diagnosis = new Diagnosis
            {
                Outcome = DiagnosticOutcome.Healthy,
                Headline = "Connection <healthy>",
                Summary = "All checks passed & completed."
            },
            Checks =
            [
                new DiagnosticCheckResult
                {
                    CheckId = "test",
                    Title = "Test <check>",
                    Category = DiagnosticCategory.Internet,
                    Status = CheckStatus.Passed,
                    Severity = FindingSeverity.Information,
                    Summary = "Passed & safe"
                }
            ]
        };
    }
}


using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using NetCheck.Core.Diagnostics;
using NetCheck.Core.Models;

namespace NetCheck.Infrastructure.Diagnostics;

public sealed class StabilityDiagnosticCheck : DiagnosticCheckBase
{
    public override string Id => DiagnosticCheckIds.Stability;

    public override string Name => "Connection stability";

    public override string Description => "Samples latency and packet loss over several requests.";

    public override DiagnosticCategory Category => DiagnosticCategory.Stability;

    public override int Order => 70;

    public override async Task<DiagnosticCheckResult> ExecuteAsync(
        DiagnosticContext context,
        CancellationToken cancellationToken)
    {
        if (!context.TryGet<IPAddress>("internet-ping-target", out var target) || target is null)
        {
            return Skip("Skipped because no public ping target responded during the reachability check.");
        }

        var sampleCount = Math.Clamp(context.Options.StabilitySampleCount, 3, 20);
        var successfulLatencies = new List<long>(sampleCount);
        for (var index = 0; index < sampleCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var latency = await SampleAsync(target, context.Options.PingTimeoutMilliseconds, cancellationToken)
                .ConfigureAwait(false);
            if (latency.HasValue)
            {
                successfulLatencies.Add(latency.Value);
            }

            if (index < sampleCount - 1)
            {
                await Task.Delay(180, cancellationToken).ConfigureAwait(false);
            }
        }

        var lost = sampleCount - successfulLatencies.Count;
        var lossPercent = lost * 100d / sampleCount;
        var average = successfulLatencies.Count == 0 ? 0 : successfulLatencies.Average();
        var jitter = CalculateJitter(successfulLatencies);
        var evidence = new Dictionary<string, string>
        {
            ["Target"] = target.ToString(),
            ["Samples"] = sampleCount.ToString(),
            ["Successful"] = successfulLatencies.Count.ToString(),
            ["Packet loss"] = $"{lossPercent:0.#}%",
            ["Average latency"] = successfulLatencies.Count == 0 ? "No replies" : $"{average:0.#} ms",
            ["Jitter"] = successfulLatencies.Count < 2 ? "Not enough data" : $"{jitter:0.#} ms"
        };

        if (lossPercent >= context.Options.PacketLossWarningPercent
            || average >= context.Options.LatencyWarningMilliseconds)
        {
            return Result(
                CheckStatus.Warning,
                FindingSeverity.Warning,
                $"Connection quality is degraded ({lossPercent:0.#}% loss, {average:0.#} ms average latency).",
                "Packet loss and high latency can cause slow pages, video buffering, and interrupted calls.",
                evidence,
                [
                    "Move closer to the Wi-Fi access point or use Ethernet for comparison.",
                    "Pause large downloads, cloud backups, or other bandwidth-heavy activity.",
                    "Restart the router and compare results at a different time.",
                    "If Ethernet is also unstable, contact the network administrator or internet provider."
                ]);
        }

        return Result(
            CheckStatus.Passed,
            FindingSeverity.Information,
            $"The connection is stable ({average:0.#} ms average, {lossPercent:0.#}% loss).",
            "The short quality sample did not detect significant packet loss or latency.",
            evidence);
    }

    private static async Task<long?> SampleAsync(
        IPAddress target,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var stopwatch = Stopwatch.StartNew();
            var reply = await ping.SendPingAsync(target, timeoutMilliseconds)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            stopwatch.Stop();
            return reply.Status == IPStatus.Success ? reply.RoundtripTime : null;
        }
        catch (PingException)
        {
            return null;
        }
    }

    private static double CalculateJitter(IReadOnlyList<long> latencies)
    {
        if (latencies.Count < 2)
        {
            return 0;
        }

        var changes = new List<long>(latencies.Count - 1);
        for (var index = 1; index < latencies.Count; index++)
        {
            changes.Add(Math.Abs(latencies[index] - latencies[index - 1]));
        }

        return changes.Average();
    }
}


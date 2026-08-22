using System.Collections.Concurrent;
using System.Net;
using NetCheck.Core.Models;
using NetCheck.Infrastructure.Network;

namespace NetCheck.Infrastructure.Tests;

public sealed class CloudflareSpeedTestServiceTests
{
    [Fact]
    public void DefaultOptions_TargetAnApproximatelyThirtySecondMeasurement()
    {
        var options = new CloudflareSpeedTestOptions();

        Assert.Equal(TimeSpan.FromSeconds(17), options.DownloadTargetDuration);
        Assert.Equal(TimeSpan.FromSeconds(11), options.UploadTargetDuration);
        Assert.Equal(28, (options.DownloadTargetDuration + options.UploadTargetDuration).TotalSeconds);
        Assert.Equal(199_500_000,
            options.MaximumDownloadBytes
            + options.MaximumUploadBytes
            + ((long)options.DownloadProbeBytes * options.DownloadParallelism)
            + ((long)options.UploadProbeBytes * options.UploadParallelism));
    }

    [Fact]
    public async Task RunAsync_MeasuresAllPhasesAndHonorsConfiguredTrafficCaps()
    {
        var requests = new ConcurrentBag<(HttpMethod Method, int Bytes)>();
        var versionPolicies = new ConcurrentBag<(Version Version, HttpVersionPolicy Policy)>();
        using var client = new HttpClient(new DelegateHandler(async (request, cancellationToken) =>
        {
            var bytes = ReadRequestedBytes(request.RequestUri!);
            requests.Add((request.Method, bytes));
            versionPolicies.Add((request.Version, request.VersionPolicy));
            if (request.Method == HttpMethod.Post)
            {
                var uploaded = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
                Assert.Equal(bytes, uploaded.Length);
            }

            return CreateResponse(request, HttpStatusCode.OK, new byte[request.Method == HttpMethod.Get ? bytes : 0]);
        }))
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        var options = CreateSmallOptions();
        using var service = new CloudflareSpeedTestService(client, options);
        var progress = new ConcurrentBag<SpeedTestProgress>();

        var result = await service.RunAsync(new InlineProgress<SpeedTestProgress>(progress.Add));

        Assert.True(result.DownloadMegabitsPerSecond > 0);
        Assert.True(result.PeakDownloadMegabitsPerSecond >= result.DownloadMegabitsPerSecond);
        Assert.True(result.UploadMegabitsPerSecond > 0);
        Assert.True(result.PeakUploadMegabitsPerSecond >= result.UploadMegabitsPerSecond);
        Assert.True(result.LatencyMilliseconds >= 0);
        Assert.True(
            result.Duration >= options.DownloadTargetDuration + options.UploadTargetDuration - TimeSpan.FromMilliseconds(5),
            $"The observation window ended too early after {result.Duration.TotalMilliseconds:N1} ms.");
        Assert.Equal(
            (long)options.DownloadProbeBytes * options.DownloadParallelism + options.MaximumDownloadBytes,
            result.DownloadBytes);
        Assert.Equal(
            (long)options.UploadProbeBytes * options.UploadParallelism + options.MaximumUploadBytes,
            result.UploadBytes);
        Assert.Equal("Cloudflare", result.Provider);
        Assert.Contains(progress, item => item.Phase == SpeedTestPhase.Latency);
        Assert.Contains(progress, item => item.Phase == SpeedTestPhase.Download);
        Assert.Contains(progress, item => item.Phase == SpeedTestPhase.Upload);
        Assert.Contains(progress, item => item is { Phase: SpeedTestPhase.Complete, Percentage: 100 });
        Assert.Equal(
            1 + options.LatencySampleCount
            + options.DownloadParallelism
            + (options.DownloadRoundCount * options.DownloadParallelism),
            requests.Count(item => item.Method == HttpMethod.Get));
        Assert.Equal(
            options.UploadParallelism
            + (options.UploadRoundCount * options.UploadParallelism),
            requests.Count(item => item.Method == HttpMethod.Post));
        Assert.All(versionPolicies, item =>
        {
            Assert.Equal(HttpVersion.Version20, item.Version);
            Assert.Equal(HttpVersionPolicy.RequestVersionOrLower, item.Policy);
        });
    }

    [Fact]
    public async Task RunAsync_RejectsAnIncompleteDownloadResponse()
    {
        using var client = new HttpClient(new DelegateHandler((request, _) =>
        {
            var requestedBytes = ReadRequestedBytes(request.RequestUri!);
            var returnedBytes = request.Method == HttpMethod.Get && requestedBytes > 0
                ? requestedBytes - 1
                : 0;
            return Task.FromResult(CreateResponse(request, HttpStatusCode.OK, new byte[returnedBytes]));
        }))
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        using var service = new CloudflareSpeedTestService(client, CreateSmallOptions());

        var exception = await Assert.ThrowsAsync<SpeedTestException>(() => service.RunAsync());

        Assert.Equal(SpeedTestFailure.UnexpectedResponse, exception.Failure);
    }

    [Fact]
    public async Task RunAsync_PropagatesUserCancellation()
    {
        using var client = new HttpClient(new DelegateHandler(async (request, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            return CreateResponse(request, HttpStatusCode.OK, []);
        }))
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        using var service = new CloudflareSpeedTestService(client, CreateSmallOptions());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RunAsync(cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task RunAsync_TimesOutAStalledResponseBody()
    {
        using var client = new HttpClient(new DelegateHandler((request, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StreamContent(new StalledReadStream())
            })))
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        var options = CreateSmallOptions() with { RequestTimeout = TimeSpan.FromMilliseconds(30) };
        using var service = new CloudflareSpeedTestService(client, options);

        var exception = await Assert.ThrowsAsync<SpeedTestException>(() => service.RunAsync());

        Assert.Equal(SpeedTestFailure.TimedOut, exception.Failure);
    }

    private static CloudflareSpeedTestOptions CreateSmallOptions() => new()
    {
        LatencySampleCount = 3,
        DownloadProbeBytes = 128,
        MinimumDownloadBytes = 2048,
        MaximumDownloadBytes = 2048,
        DownloadTargetDuration = TimeSpan.FromMilliseconds(10),
        DownloadParallelism = 2,
        DownloadRoundCount = 2,
        UploadProbeBytes = 96,
        MinimumUploadBytes = 1536,
        MaximumUploadBytes = 1536,
        UploadTargetDuration = TimeSpan.FromMilliseconds(10),
        UploadParallelism = 2,
        UploadRoundCount = 2,
        SampleInterval = TimeSpan.FromMilliseconds(1),
        RequestTimeout = TimeSpan.FromSeconds(2)
    };

    private static int ReadRequestedBytes(Uri uri)
    {
        var bytesPart = uri.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Single(part => part.StartsWith("bytes=", StringComparison.Ordinal));
        return int.Parse(bytesPart.AsSpan("bytes=".Length), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static HttpResponseMessage CreateResponse(
        HttpRequestMessage request,
        HttpStatusCode statusCode,
        byte[] content) =>
        new(statusCode)
        {
            RequestMessage = request,
            Content = new ByteArrayContent(content)
        };

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request, cancellationToken);
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class StalledReadStream : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }
}

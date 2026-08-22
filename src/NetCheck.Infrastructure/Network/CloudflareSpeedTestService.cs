using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using NetCheck.Core.Abstractions;
using NetCheck.Core.Models;

namespace NetCheck.Infrastructure.Network;

public sealed class CloudflareSpeedTestService : ISpeedTestService, IDisposable
{
    private const double BitsPerMegabit = 1_000_000d;
    private readonly HttpClient _httpClient;
    private readonly CloudflareSpeedTestOptions _options;
    private readonly bool _ownsHttpClient;
    private bool _disposed;

    public CloudflareSpeedTestService(
        HttpClient? httpClient = null,
        CloudflareSpeedTestOptions? options = null)
    {
        _options = options ?? new CloudflareSpeedTestOptions();
        ValidateOptions(_options);
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? CreateHttpClient();
    }

    public async Task<SpeedTestResult> RunAsync(
        IProgress<SpeedTestProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        var totalStopwatch = Stopwatch.StartNew();
        progress?.Report(new SpeedTestProgress(SpeedTestPhase.Preparing, 0, 0, 0, 0));

        try
        {
            var latency = await MeasureLatencyAsync(progress, cancellationToken).ConfigureAwait(false);

            var downloadProbe = await MeasureSingleDownloadAsync(
                    _options.DownloadProbeBytes,
                    cancellationToken)
                .ConfigureAwait(false);
            progress?.Report(new SpeedTestProgress(
                SpeedTestPhase.Download,
                18,
                downloadProbe.MegabitsPerSecond,
                downloadProbe.Bytes,
                downloadProbe.Bytes));

            var downloadTargetBytes = CalculateTargetBytes(
                downloadProbe.MegabitsPerSecond,
                _options.DownloadTargetDuration,
                _options.MinimumDownloadBytes,
                _options.MaximumDownloadBytes);
            var download = await MeasureDownloadBatchAsync(
                    downloadTargetBytes,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);

            var uploadProbe = await MeasureSingleUploadAsync(
                    _options.UploadProbeBytes,
                    cancellationToken)
                .ConfigureAwait(false);
            progress?.Report(new SpeedTestProgress(
                SpeedTestPhase.Upload,
                68,
                uploadProbe.MegabitsPerSecond,
                uploadProbe.Bytes,
                uploadProbe.Bytes));

            var uploadTargetBytes = CalculateTargetBytes(
                uploadProbe.MegabitsPerSecond,
                _options.UploadTargetDuration,
                _options.MinimumUploadBytes,
                _options.MaximumUploadBytes);
            var upload = await MeasureUploadBatchAsync(
                    uploadTargetBytes,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);

            totalStopwatch.Stop();
            progress?.Report(new SpeedTestProgress(
                SpeedTestPhase.Complete,
                100,
                0,
                download.Bytes + upload.Bytes,
                download.Bytes + upload.Bytes));

            return new SpeedTestResult(
                latency,
                download.MegabitsPerSecond,
                download.PeakMegabitsPerSecond,
                upload.MegabitsPerSecond,
                upload.PeakMegabitsPerSecond,
                download.Bytes + downloadProbe.Bytes,
                upload.Bytes + uploadProbe.Bytes,
                totalStopwatch.Elapsed,
                "Cloudflare",
                DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SpeedTestException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            throw new SpeedTestException(
                SpeedTestFailure.NetworkUnavailable,
                "The speed-test service could not be reached.",
                exception);
        }
        catch (IOException exception)
        {
            throw new SpeedTestException(
                SpeedTestFailure.NetworkUnavailable,
                "The speed-test transfer was interrupted.",
                exception);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        _disposed = true;
    }

    private async Task<double> MeasureLatencyAsync(
        IProgress<SpeedTestProgress>? progress,
        CancellationToken cancellationToken)
    {
        await DownloadOnceAsync(0, null, cancellationToken).ConfigureAwait(false);
        var samples = new double[_options.LatencySampleCount];

        for (var index = 0; index < samples.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stopwatch = Stopwatch.StartNew();
            await DownloadOnceAsync(0, null, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            samples[index] = stopwatch.Elapsed.TotalMilliseconds;
            progress?.Report(new SpeedTestProgress(
                SpeedTestPhase.Latency,
                2 + (int)Math.Round(13d * (index + 1) / samples.Length),
                0,
                index + 1,
                samples.Length));
        }

        Array.Sort(samples);
        var middle = samples.Length / 2;
        return samples.Length % 2 == 0
            ? (samples[middle - 1] + samples[middle]) / 2
            : samples[middle];
    }

    private async Task<SingleMeasurement> MeasureSingleDownloadAsync(
        int bytes,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        await DownloadOnceAsync(bytes, null, cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();
        return new SingleMeasurement(bytes, ToMegabitsPerSecond(bytes, stopwatch.Elapsed));
    }

    private async Task<SingleMeasurement> MeasureSingleUploadAsync(
        int bytes,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        await UploadOnceAsync(bytes, null, cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();
        return new SingleMeasurement(bytes, ToMegabitsPerSecond(bytes, stopwatch.Elapsed));
    }

    private Task<BatchMeasurement> MeasureDownloadBatchAsync(
        long totalBytes,
        IProgress<SpeedTestProgress>? progress,
        CancellationToken cancellationToken) =>
        MeasureBatchAsync(
            totalBytes,
            _options.DownloadParallelism,
            SpeedTestPhase.Download,
            20,
            45,
            progress,
            DownloadOnceAsync,
            cancellationToken);

    private Task<BatchMeasurement> MeasureUploadBatchAsync(
        long totalBytes,
        IProgress<SpeedTestProgress>? progress,
        CancellationToken cancellationToken) =>
        MeasureBatchAsync(
            totalBytes,
            _options.UploadParallelism,
            SpeedTestPhase.Upload,
            70,
            25,
            progress,
            UploadOnceAsync,
            cancellationToken);

    private async Task<BatchMeasurement> MeasureBatchAsync(
        long totalBytes,
        int parallelism,
        SpeedTestPhase phase,
        int progressStart,
        int progressRange,
        IProgress<SpeedTestProgress>? progress,
        Func<int, Action<int>?, CancellationToken, Task> transfer,
        CancellationToken cancellationToken)
    {
        var sizes = CreateTransferSizes(totalBytes, parallelism);
        long transferredBytes = 0;
        var stopwatch = Stopwatch.StartNew();
        var tasks = sizes
            .Select(size => transfer(
                size,
                count => Interlocked.Add(ref transferredBytes, count),
                cancellationToken))
            .ToArray();
        var allTransfers = Task.WhenAll(tasks);
        var peakMegabitsPerSecond = 0d;
        var previousBytes = 0L;
        var previousElapsed = TimeSpan.Zero;

        while (!allTransfers.IsCompleted)
        {
            var delay = Task.Delay(_options.SampleInterval, cancellationToken);
            await Task.WhenAny(allTransfers, delay).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var currentBytes = Interlocked.Read(ref transferredBytes);
            var currentElapsed = stopwatch.Elapsed;
            var interval = currentElapsed - previousElapsed;
            if (interval >= TimeSpan.FromMilliseconds(80))
            {
                peakMegabitsPerSecond = Math.Max(
                    peakMegabitsPerSecond,
                    ToMegabitsPerSecond(currentBytes - previousBytes, interval));
                previousBytes = currentBytes;
                previousElapsed = currentElapsed;
            }

            ReportTransferProgress(
                phase,
                progressStart,
                progressRange,
                currentBytes,
                totalBytes,
                ToMegabitsPerSecond(currentBytes, currentElapsed),
                progress);
        }

        await allTransfers.ConfigureAwait(false);
        stopwatch.Stop();

        var finalBytes = Interlocked.Read(ref transferredBytes);
        if (finalBytes != totalBytes)
        {
            throw new SpeedTestException(
                SpeedTestFailure.UnexpectedResponse,
                "The speed-test transfer ended before all bytes were processed.");
        }

        var averageMegabitsPerSecond = ToMegabitsPerSecond(finalBytes, stopwatch.Elapsed);
        peakMegabitsPerSecond = Math.Max(peakMegabitsPerSecond, averageMegabitsPerSecond);
        ReportTransferProgress(
            phase,
            progressStart,
            progressRange,
            finalBytes,
            totalBytes,
            averageMegabitsPerSecond,
            progress);

        return new BatchMeasurement(
            finalBytes,
            averageMegabitsPerSecond,
            peakMegabitsPerSecond);
    }

    private async Task DownloadOnceAsync(
        int expectedBytes,
        Action<int>? bytesTransferred,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            BuildEndpoint(_options.DownloadEndpoint, expectedBytes));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.RequestTimeout);
        try
        {
            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                .ConfigureAwait(false);
            ValidateResponse(response, _options.DownloadEndpoint);

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            var buffer = new byte[128 * 1024];
            long receivedBytes = 0;
            while (true)
            {
                var read = await stream.ReadAsync(buffer, timeout.Token).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                receivedBytes += read;
                bytesTransferred?.Invoke(read);
                if (receivedBytes > expectedBytes)
                {
                    throw new SpeedTestException(
                        SpeedTestFailure.UnexpectedResponse,
                        "The download endpoint returned more data than requested.");
                }
            }

            if (receivedBytes != expectedBytes)
            {
                throw new SpeedTestException(
                    SpeedTestFailure.UnexpectedResponse,
                    "The download endpoint returned an unexpected amount of data.");
            }
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SpeedTestException(
                SpeedTestFailure.TimedOut,
                "The speed-test request timed out.",
                exception);
        }
    }

    private async Task UploadOnceAsync(
        int bytes,
        Action<int>? bytesTransferred,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            BuildEndpoint(_options.UploadEndpoint, bytes));
        request.Content = new GeneratedUploadContent(bytes, bytesTransferred, cancellationToken);
        using var response = await SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        ValidateResponse(response, _options.UploadEndpoint);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.RequestTimeout);
        try
        {
            return await _httpClient.SendAsync(request, completionOption, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SpeedTestException(
                SpeedTestFailure.TimedOut,
                "The speed-test request timed out.",
                exception);
        }
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri)
    {
        var request = new HttpRequestMessage(method, uri)
        {
            Version = HttpVersion.Version20,
            VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };
        request.Headers.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true,
            NoStore = true
        };
        request.Headers.TryAddWithoutValidation("Accept-Encoding", "identity");
        request.Headers.UserAgent.ParseAdd("NetCheck/1.0");
        return request;
    }

    private static void ValidateResponse(HttpResponseMessage response, Uri expectedEndpoint)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new SpeedTestException(
                SpeedTestFailure.UnexpectedResponse,
                $"The speed-test endpoint returned HTTP {(int)response.StatusCode}.");
        }

        var finalUri = response.RequestMessage?.RequestUri;
        if (finalUri is not null
            && (!string.Equals(finalUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(finalUri.Host, expectedEndpoint.Host, StringComparison.OrdinalIgnoreCase)))
        {
            throw new SpeedTestException(
                SpeedTestFailure.UnexpectedResponse,
                "The speed-test request was redirected to an unexpected destination.");
        }
    }

    private static Uri BuildEndpoint(Uri endpoint, int bytes)
    {
        var builder = new UriBuilder(endpoint)
        {
            Query = $"bytes={bytes}&cacheBust={Guid.NewGuid():N}"
        };
        return builder.Uri;
    }

    private static long CalculateTargetBytes(
        double megabitsPerSecond,
        TimeSpan targetDuration,
        long minimumBytes,
        long maximumBytes)
    {
        var estimatedBytes = megabitsPerSecond * BitsPerMegabit * targetDuration.TotalSeconds / 8d;
        return (long)Math.Clamp(estimatedBytes, minimumBytes, maximumBytes);
    }

    private static int[] CreateTransferSizes(long totalBytes, int parallelism)
    {
        var sizes = new int[parallelism];
        var baseSize = totalBytes / parallelism;
        var remainder = totalBytes % parallelism;
        for (var index = 0; index < sizes.Length; index++)
        {
            sizes[index] = checked((int)(baseSize + (index < remainder ? 1 : 0)));
        }

        return sizes;
    }

    private static void ReportTransferProgress(
        SpeedTestPhase phase,
        int progressStart,
        int progressRange,
        long transferredBytes,
        long totalBytes,
        double megabitsPerSecond,
        IProgress<SpeedTestProgress>? progress)
    {
        var ratio = totalBytes == 0 ? 1d : Math.Clamp((double)transferredBytes / totalBytes, 0d, 1d);
        progress?.Report(new SpeedTestProgress(
            phase,
            progressStart + (int)Math.Round(progressRange * ratio),
            megabitsPerSecond,
            transferredBytes,
            totalBytes));
    }

    private static double ToMegabitsPerSecond(long bytes, TimeSpan elapsed) =>
        elapsed <= TimeSpan.Zero ? 0 : bytes * 8d / elapsed.TotalSeconds / BitsPerMegabit;

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            MaxConnectionsPerServer = 8,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            UseCookies = false
        };
        return new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    private static void ValidateOptions(CloudflareSpeedTestOptions options)
    {
        if (!options.DownloadEndpoint.IsAbsoluteUri
            || !options.UploadEndpoint.IsAbsoluteUri
            || options.DownloadEndpoint.Scheme != Uri.UriSchemeHttps
            || options.UploadEndpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Speed-test endpoints must be absolute HTTPS URLs.", nameof(options));
        }

        if (options.LatencySampleCount < 1
            || options.DownloadProbeBytes < 1
            || options.UploadProbeBytes < 1
            || options.MinimumDownloadBytes < 1
            || options.MinimumUploadBytes < 1
            || options.MaximumDownloadBytes < options.MinimumDownloadBytes
            || options.MaximumUploadBytes < options.MinimumUploadBytes
            || options.MaximumDownloadBytes > int.MaxValue * (long)options.DownloadParallelism
            || options.MaximumUploadBytes > int.MaxValue * (long)options.UploadParallelism
            || options.DownloadParallelism < 1
            || options.UploadParallelism < 1
            || options.DownloadTargetDuration <= TimeSpan.Zero
            || options.UploadTargetDuration <= TimeSpan.Zero
            || options.SampleInterval <= TimeSpan.Zero
            || options.RequestTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Speed-test options contain an invalid value.");
        }
    }

    private sealed record SingleMeasurement(long Bytes, double MegabitsPerSecond);

    private sealed record BatchMeasurement(
        long Bytes,
        double MegabitsPerSecond,
        double PeakMegabitsPerSecond);

    private sealed class GeneratedUploadContent : HttpContent
    {
        private readonly int _length;
        private readonly Action<int>? _bytesTransferred;
        private readonly CancellationToken _requestCancellationToken;

        public GeneratedUploadContent(
            int length,
            Action<int>? bytesTransferred,
            CancellationToken requestCancellationToken)
        {
            _length = length;
            _bytesTransferred = bytesTransferred;
            _requestCancellationToken = requestCancellationToken;
            Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            SerializeCoreAsync(stream, _requestCancellationToken);

        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context,
            CancellationToken cancellationToken) =>
            SerializeCoreAsync(stream, cancellationToken);

        protected override bool TryComputeLength(out long length)
        {
            length = _length;
            return true;
        }

        private async Task SerializeCoreAsync(Stream stream, CancellationToken cancellationToken)
        {
            var buffer = new byte[64 * 1024];
            var remaining = _length;
            while (remaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = Math.Min(buffer.Length, remaining);
                await stream.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
                _bytesTransferred?.Invoke(count);
                remaining -= count;
            }
        }
    }
}

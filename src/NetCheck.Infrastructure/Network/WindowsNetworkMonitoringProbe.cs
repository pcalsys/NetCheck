using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Win32;
using NetCheck.Core.Abstractions;
using NetCheck.Core.Models;

namespace NetCheck.Infrastructure.Network;

public sealed partial class WindowsNetworkMonitoringProbe : INetworkMonitoringProbe, IDisposable
{
    private const string NetworkAdapterClassKey =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";
    private static readonly byte[] PingBuffer = Encoding.ASCII.GetBytes("NetCheck-monitoring");
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private bool _disposed;

    public WindowsNetworkMonitoringProbe(HttpClient? httpClient = null)
    {
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? CreateHttpClient();
    }

    public async Task<NetworkEnvironmentSnapshot> CaptureEnvironmentAsync(
        MonitoringOptions options,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        var errors = new List<string>();
        var adapter = GetPrimaryAdapter(errors);
        var properties = GetProperties(adapter, errors);
        var driver = GetDriverDetails(adapter?.Id, errors);

        var wifiTask = CaptureWifiAsync(cancellationToken);
        var ipv4RouteTask = TraceRouteSafelyAsync(
            options.Ipv4Target,
            AddressFamily.InterNetwork,
            options.MaximumTracerouteHops,
            options.PingTimeoutMilliseconds,
            cancellationToken);
        var ipv6RouteTask = TraceRouteSafelyAsync(
            options.Ipv6Target,
            AddressFamily.InterNetworkV6,
            options.MaximumTracerouteHops,
            options.PingTimeoutMilliseconds,
            cancellationToken);
        await Task.WhenAll(wifiTask, ipv4RouteTask, ipv6RouteTask).ConfigureAwait(false);
        var wifiResult = await wifiTask.ConfigureAwait(false);
        if (wifiResult.Error is not null)
        {
            errors.Add(wifiResult.Error);
        }

        var ipv4Route = await ipv4RouteTask.ConfigureAwait(false);
        var ipv6Route = await ipv6RouteTask.ConfigureAwait(false);
        if (ipv4Route.Error is not null)
        {
            errors.Add(ipv4Route.Error);
        }

        if (ipv6Route.Error is not null)
        {
            errors.Add(ipv6Route.Error);
        }

        return new NetworkEnvironmentSnapshot
        {
            CapturedAtUtc = DateTimeOffset.UtcNow,
            AdapterId = adapter?.Id ?? string.Empty,
            AdapterName = adapter?.Name ?? string.Empty,
            AdapterDescription = driver.Description ?? adapter?.Description ?? string.Empty,
            InterfaceType = adapter?.NetworkInterfaceType.ToString() ?? string.Empty,
            DriverVersion = driver.Version ?? string.Empty,
            LinkSpeedBitsPerSecond = adapter?.Speed ?? 0,
            SupportsIpv4 = SupportsAddressFamily(properties, AddressFamily.InterNetwork, errors),
            SupportsIpv6 = SupportsAddressFamily(properties, AddressFamily.InterNetworkV6, errors),
            Wifi = wifiResult.Details,
            VpnAdapters = GetVpnAdapters(errors),
            Firewall = GetFirewallStatus(errors),
            Ipv4Route = ipv4Route.Hops,
            Ipv6Route = ipv6Route.Hops,
            Errors = errors
        };
    }

    public async Task<NetworkMonitoringProbeResult> ProbeAsync(
        MonitoringOptions options,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        var adapterErrors = new List<string>();
        var adapter = GetPrimaryAdapter(adapterErrors);
        var properties = GetProperties(adapter, adapterErrors);
        var gateway = properties?.GatewayAddresses
            .Select(item => item.Address)
            .FirstOrDefault(address => !IPAddress.Any.Equals(address)
                && !IPAddress.IPv6Any.Equals(address))
            ?.ToString() ?? string.Empty;

        var ipv4Task = PingTargetSafelyAsync(
            options.Ipv4Target,
            AddressFamily.InterNetwork,
            options.PingTimeoutMilliseconds,
            cancellationToken);
        var ipv6Task = PingTargetSafelyAsync(
            options.Ipv6Target,
            AddressFamily.InterNetworkV6,
            options.PingTimeoutMilliseconds,
            cancellationToken);
        var dnsTask = ResolveDnsSafelyAsync(
            options.DnsTestHost,
            Math.Max(options.PingTimeoutMilliseconds * 2, 1500),
            cancellationToken);
        var webTask = CheckWebSafelyAsync(options.WebFallbackUri, options.PingTimeoutMilliseconds, cancellationToken);
        await Task.WhenAll(ipv4Task, ipv6Task, dnsTask, webTask).ConfigureAwait(false);
        var ipv4 = await ipv4Task.ConfigureAwait(false);
        var ipv6 = await ipv6Task.ConfigureAwait(false);
        var dns = await dnsTask.ConfigureAwait(false);
        var web = await webTask.ConfigureAwait(false);
        var errors = adapterErrors
            .Concat(new[] { ipv4.Error, ipv6.Error, dns.Error, web.Error })
            .Where(error => !string.IsNullOrWhiteSpace(error))
            .Cast<string>()
            .ToArray();

        return new NetworkMonitoringProbeResult
        {
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Ipv4LatencyMilliseconds = ipv4.LatencyMilliseconds,
            Ipv6LatencyMilliseconds = ipv6.LatencyMilliseconds,
            DnsIpv4Resolved = dns.Ipv4,
            DnsIpv6Resolved = dns.Ipv6,
            WebReachable = web.Reachable,
            AdapterId = adapter?.Id ?? string.Empty,
            AdapterName = adapter?.Name ?? string.Empty,
            Gateway = gateway,
            Errors = errors
        };
    }

    public async Task<IReadOnlyList<WindowsNetworkEvent>> GetWindowsNetworkEventsAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (toUtc < fromUtc)
        {
            throw new ArgumentException("The event query time range is invalid.", nameof(toUtc));
        }

        var logs = new[]
        {
            "Microsoft-Windows-WLAN-AutoConfig/Operational",
            "Microsoft-Windows-Dhcp-Client/Admin",
            "Microsoft-Windows-NetworkProfile/Operational"
        };
        var tasks = logs.Select(log => QueryWindowsEventsAsync(
            log,
            fromUtc,
            toUtc,
            cancellationToken)).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);
        return tasks.SelectMany(task => task.Result)
            .OrderBy(item => item.OccurredAtUtc)
            .TakeLast(300)
            .ToArray();
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

    private static NetworkInterface? GetPrimaryAdapter(ICollection<string> errors)
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(item => item.OperationalStatus == OperationalStatus.Up)
                .Where(item => item.NetworkInterfaceType is not NetworkInterfaceType.Loopback
                    and not NetworkInterfaceType.Tunnel)
                .Select(item => new
                {
                    Adapter = item,
                    Properties = GetProperties(item, errors: null)
                })
                .OrderByDescending(item => item.Properties?.GatewayAddresses.Count > 0)
                .ThenByDescending(item => item.Adapter.Speed)
                .Select(item => item.Adapter)
                .FirstOrDefault();
        }
        catch (Exception exception) when (exception is NetworkInformationException
            or PlatformNotSupportedException)
        {
            errors.Add($"Adapter: {exception.GetType().Name}: {exception.Message}");
            return null;
        }
    }

    private static IPInterfaceProperties? GetProperties(
        NetworkInterface? adapter,
        ICollection<string>? errors)
    {
        if (adapter is null)
        {
            return null;
        }

        try
        {
            return adapter.GetIPProperties();
        }
        catch (NetworkInformationException exception)
        {
            errors?.Add($"IP properties: {exception.Message}");
            return null;
        }
    }

    private static bool SupportsAddressFamily(
        IPInterfaceProperties? properties,
        AddressFamily family,
        ICollection<string> errors)
    {
        if (properties is null)
        {
            return false;
        }

        try
        {
            return family == AddressFamily.InterNetwork
                ? properties.GetIPv4Properties() is not null
                : properties.GetIPv6Properties() is not null;
        }
        catch (Exception exception) when (exception is NetworkInformationException
            or PlatformNotSupportedException)
        {
            errors.Add($"{family} properties: {exception.GetType().Name}: {exception.Message}");
            return false;
        }
    }

    private static IReadOnlyList<string> GetVpnAdapters(ICollection<string> errors)
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(IsVpnAdapter)
                .Select(item => item.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
        catch (Exception exception) when (exception is NetworkInformationException
            or PlatformNotSupportedException)
        {
            errors.Add($"VPN adapters: {exception.GetType().Name}: {exception.Message}");
            return Array.Empty<string>();
        }
    }

    private static bool IsVpnAdapter(NetworkInterface adapter)
    {
        if (adapter.NetworkInterfaceType is NetworkInterfaceType.Ppp or NetworkInterfaceType.Tunnel)
        {
            return true;
        }

        var identity = $"{adapter.Name} {adapter.Description}";
        return VpnNameRegex().IsMatch(identity);
    }

    private static (string? Description, string? Version) GetDriverDetails(
        string? adapterId,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(adapterId) || !OperatingSystem.IsWindows())
        {
            return (null, null);
        }

        try
        {
            using var adaptersKey = Registry.LocalMachine.OpenSubKey(NetworkAdapterClassKey);
            if (adaptersKey is null)
            {
                return (null, null);
            }

            foreach (var subKeyName in adaptersKey.GetSubKeyNames())
            {
                using var adapterKey = adaptersKey.OpenSubKey(subKeyName);
                var instanceId = adapterKey?.GetValue("NetCfgInstanceId") as string;
                if (!string.Equals(instanceId?.Trim('{', '}'), adapterId.Trim('{', '}'), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return (
                    adapterKey?.GetValue("DriverDesc") as string,
                    adapterKey?.GetValue("DriverVersion") as string);
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException
            or IOException
            or System.Security.SecurityException)
        {
            errors.Add($"Network driver: {exception.GetType().Name}: {exception.Message}");
        }

        return (null, null);
    }

    private static FirewallProfileStatus GetFirewallStatus(ICollection<string> errors)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new FirewallProfileStatus();
        }

        try
        {
            return new FirewallProfileStatus
            {
                DomainEnabled = ReadFirewallProfile("DomainProfile"),
                PrivateEnabled = ReadFirewallProfile("StandardProfile"),
                PublicEnabled = ReadFirewallProfile("PublicProfile")
            };
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException
            or IOException
            or System.Security.SecurityException)
        {
            errors.Add($"Windows Firewall: {exception.GetType().Name}: {exception.Message}");
            return new FirewallProfileStatus();
        }
    }

    private static bool ReadFirewallProfile(string profile)
    {
        const string firewallRoot =
            @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy";
        using var key = Registry.LocalMachine.OpenSubKey($@"{firewallRoot}\{profile}");
        return key?.GetValue("EnableFirewall") is int value && value != 0;
    }

    private static async Task<(WifiNetworkDetails? Details, string? Error)> CaptureWifiAsync(
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return (null, null);
        }

        try
        {
            var result = await RunProcessAsync(
                "netsh.exe",
                ["wlan", "show", "interfaces"],
                TimeSpan.FromSeconds(5),
                cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                return (null, $"Wi-Fi: netsh exited with code {result.ExitCode}.");
            }

            return (ParseWifiDetails(result.StandardOutput), null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return (null, $"Wi-Fi: {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static WifiNetworkDetails? ParseWifiDetails(string output)
    {
        var values = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(':', 2))
            .Where(parts => parts.Length == 2)
            .Select(parts => new KeyValuePair<string, string>(NormalizeKey(parts[0]), parts[1].Trim()))
            .ToLookup(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        string? Value(params string[] keys) => keys
            .SelectMany(key => values[key])
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var state = Value("state", "status");
        var ssid = Value("ssid") ?? string.Empty;
        var channel = ParseInteger(Value("channel", "kanal"));
        var band = Value("band", "frequenzband") ?? GetBandFromChannel(channel);
        if (string.IsNullOrWhiteSpace(state) && string.IsNullOrWhiteSpace(ssid))
        {
            return null;
        }

        return new WifiNetworkDetails
        {
            IsConnected = string.Equals(state, "connected", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "verbunden", StringComparison.OrdinalIgnoreCase),
            Ssid = ssid,
            SignalQualityPercent = ParseInteger(Value("signal", "signalstarke")),
            Channel = channel,
            Band = band,
            RadioType = Value("radio type", "funktyp") ?? string.Empty,
            ReceiveRateMegabitsPerSecond = ParseDouble(Value(
                "receive rate (mbps)",
                "empfangsrate (mbit/s)")),
            TransmitRateMegabitsPerSecond = ParseDouble(Value(
                "transmit rate (mbps)",
                "ubertragungsrate (mbit/s)",
                "senderate (mbit/s)"))
        };
    }

    private static string NormalizeKey(string value) => value
        .Trim()
        .ToLowerInvariant()
        .Replace('ä', 'a')
        .Replace('ö', 'o')
        .Replace('ü', 'u');

    private static int? ParseInteger(string? value)
    {
        var match = NumberRegex().Match(value ?? string.Empty);
        return match.Success
            && int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    private static double? ParseDouble(string? value)
    {
        var match = DecimalRegex().Match(value ?? string.Empty);
        return match.Success
            && double.TryParse(
                match.Value.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var number)
            ? number
            : null;
    }

    private static string GetBandFromChannel(int? channel) => channel switch
    {
        >= 1 and <= 14 => "2.4 GHz",
        >= 32 and <= 177 => "5 GHz",
        > 177 => "6 GHz",
        _ => string.Empty
    };

    private static async Task<(double? LatencyMilliseconds, string? Error)> PingTargetSafelyAsync(
        string target,
        AddressFamily family,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!IPAddress.TryParse(target, out var address) || address.AddressFamily != family)
            {
                return (null, $"{family} ping: invalid target.");
            }

            using var ping = new Ping();
            var reply = await ping.SendPingAsync(
                address,
                TimeSpan.FromMilliseconds(timeoutMilliseconds),
                PingBuffer,
                new PingOptions(64, dontFragment: true),
                cancellationToken).ConfigureAwait(false);
            return reply.Status == IPStatus.Success
                ? (reply.RoundtripTime, null)
                : (null, $"{family} ping: {reply.Status}.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is PingException
            or SocketException
            or PlatformNotSupportedException)
        {
            return (null, $"{family} ping: {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static async Task<(bool Ipv4, bool Ipv6, string? Error)> ResolveDnsSafelyAsync(
        string host,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(timeoutMilliseconds));
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, timeout.Token).ConfigureAwait(false);
            return (
                addresses.Any(address => address.AddressFamily == AddressFamily.InterNetwork),
                addresses.Any(address => address.AddressFamily == AddressFamily.InterNetworkV6),
                null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            return (false, false, $"DNS: timeout: {exception.Message}");
        }
        catch (Exception exception) when (exception is SocketException or ArgumentException)
        {
            return (false, false, $"DNS: {exception.GetType().Name}: {exception.Message}");
        }
    }

    private async Task<(bool Reachable, string? Error)> CheckWebSafelyAsync(
        Uri target,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        if (target.Scheme != Uri.UriSchemeHttps)
        {
            return (false, "Web: only HTTPS targets are allowed.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(timeoutMilliseconds, 500)));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, target);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("NetCheck", "1.2"));
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token).ConfigureAwait(false);
            return ((int)response.StatusCode is >= 200 and < 500, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or OperationCanceledException)
        {
            return (false, $"Web: {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static async Task<(IReadOnlyList<RouteHop> Hops, string? Error)> TraceRouteSafelyAsync(
        string target,
        AddressFamily family,
        int maximumHops,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!IPAddress.TryParse(target, out var address) || address.AddressFamily != family)
            {
                return (Array.Empty<RouteHop>(), $"{family} traceroute: invalid target.");
            }

            var hops = new List<RouteHop>();
            using var ping = new Ping();
            for (var ttl = 1; ttl <= Math.Clamp(maximumHops, 1, 30); ttl++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PingReply reply;
                try
                {
                    reply = await ping.SendPingAsync(
                        address,
                        TimeSpan.FromMilliseconds(Math.Clamp(timeoutMilliseconds, 250, 3000)),
                        PingBuffer,
                        new PingOptions(ttl, dontFragment: false),
                        cancellationToken).ConfigureAwait(false);
                }
                catch (PingException)
                {
                    hops.Add(new RouteHop { Hop = ttl });
                    continue;
                }

                var reached = reply.Status == IPStatus.Success;
                hops.Add(new RouteHop
                {
                    Hop = ttl,
                    Address = reply.Address?.ToString() ?? "*",
                    LatencyMilliseconds = reply.Address is null ? null : reply.RoundtripTime,
                    ReachedDestination = reached
                });
                if (reached)
                {
                    break;
                }
            }

            return (hops, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is PingException
            or SocketException
            or PlatformNotSupportedException)
        {
            return (Array.Empty<RouteHop>(),
                $"{family} traceroute: {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static async Task<IReadOnlyList<WindowsNetworkEvent>> QueryWindowsEventsAsync(
        string logName,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Array.Empty<WindowsNetworkEvent>();
        }

        try
        {
            var from = fromUtc.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            var to = toUtc.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            var query = $"*[System[TimeCreated[@SystemTime>='{from}' and @SystemTime<='{to}']]]";
            var result = await RunProcessAsync(
                "wevtutil.exe",
                ["qe", logName, $"/q:{query}", "/f:xml", "/rd:true", "/c:200"],
                TimeSpan.FromSeconds(8),
                cancellationToken).ConfigureAwait(false);
            return result.ExitCode == 0
                ? ParseWindowsEvents(result.StandardOutput)
                : Array.Empty<WindowsNetworkEvent>();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Array.Empty<WindowsNetworkEvent>();
        }
    }

    private static IReadOnlyList<WindowsNetworkEvent> ParseWindowsEvents(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return Array.Empty<WindowsNetworkEvent>();
        }

        try
        {
            var document = XDocument.Parse(xml, LoadOptions.None);
            XNamespace ns = "http://schemas.microsoft.com/win/2004/08/events/event";
            return document.Descendants(ns + "Event").Select(element =>
            {
                var system = element.Element(ns + "System");
                var provider = system?.Element(ns + "Provider")?.Attribute("Name")?.Value ?? string.Empty;
                var eventId = int.TryParse(system?.Element(ns + "EventID")?.Value, out var id) ? id : 0;
                var level = system?.Element(ns + "Level")?.Value ?? string.Empty;
                var timestampValue = system?.Element(ns + "TimeCreated")?.Attribute("SystemTime")?.Value;
                var timestamp = DateTimeOffset.TryParse(
                    timestampValue,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var parsedTimestamp)
                    ? parsedTimestamp
                    : DateTimeOffset.MinValue;
                var detail = string.Join(
                    "; ",
                    element.Descendants(ns + "Data")
                        .Select(item => item.Value.Trim())
                        .Where(value => value.Length > 0)
                        .Take(8));
                return new WindowsNetworkEvent
                {
                    OccurredAtUtc = timestamp,
                    Provider = provider,
                    EventId = eventId,
                    Level = level,
                    Detail = detail
                };
            }).Where(item => item.OccurredAtUtc != DateTimeOffset.MinValue).ToArray();
        }
        catch (System.Xml.XmlException)
        {
            return Array.Empty<WindowsNetworkEvent>();
        }
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {executable}.");
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // The process exited between cancellation and cleanup.
            }

            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException($"{executable} exceeded its {timeout.TotalSeconds:N0}-second timeout.");
        }

        return new ProcessResult(
            process.ExitCode,
            await outputTask.ConfigureAwait(false),
            await errorTask.ConfigureAwait(false));
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All
        };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
    }

    [GeneratedRegex("\\b(vpn|wireguard|wintun|openvpn|tap|tun|nordvpn|protonvpn|tailscale|zerotier)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VpnNameRegex();

    [GeneratedRegex("[0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();

    [GeneratedRegex("[0-9]+(?:[.,][0-9]+)?", RegexOptions.CultureInvariant)]
    private static partial Regex DecimalRegex();

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}

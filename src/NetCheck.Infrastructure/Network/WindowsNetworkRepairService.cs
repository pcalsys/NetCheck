using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.Json;
using Microsoft.Win32;
using NetCheck.Core.Abstractions;
using NetCheck.Core.Diagnostics;
using NetCheck.Core.Models;
using NetCheck.Infrastructure.Storage;

namespace NetCheck.Infrastructure.Network;

public sealed class WindowsNetworkRepairService : INetworkRepairService
{
    public const string HelperSwitch = "--netcheck-repair-helper";

    private static readonly TimeSpan HelperTimeout = TimeSpan.FromMinutes(3);

    public async Task<NetworkRepairResult> ExecuteAsync(
        NetworkRepairPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.CanExecute)
        {
            throw new ArgumentException("The repair plan contains no actions.", nameof(plan));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var operationId = Guid.NewGuid();
        Directory.CreateDirectory(RepairWorkspace.RootDirectory);
        var requestPath = RepairWorkspace.GetRequestPath(operationId);
        var resultPath = RepairWorkspace.GetResultPath(operationId);
        var request = new RepairRequest
        {
            ActionIds = plan.Actions.Select(action => action.Id).Distinct().ToArray()
        };

        await WriteJsonAtomicallyAsync(requestPath, request, cancellationToken).ConfigureAwait(false);
        try
        {
            using var process = new Process
            {
                StartInfo = CreateHelperStartInfo(operationId, plan.RequiresElevation)
            };

            try
            {
                if (!process.Start())
                {
                    return FailedResult(plan, "Windows could not start the repair process.");
                }
            }
            catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
            {
                return new NetworkRepairResult
                {
                    StartedAtUtc = DateTimeOffset.UtcNow,
                    CompletedAtUtc = DateTimeOffset.UtcNow,
                    Cancelled = true
                };
            }

            using var timeout = new CancellationTokenSource(HelperTimeout);
            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                return FailedResult(plan, "The repair process did not finish within three minutes.");
            }

            var result = await ReadResultAsync(resultPath).ConfigureAwait(false);
            return result ?? FailedResult(
                plan,
                process.ExitCode == 0
                    ? "Windows did not return a repair result."
                    : $"The repair process ended with code {process.ExitCode}.");
        }
        finally
        {
            DeleteIfPresent(requestPath);
            DeleteIfPresent(resultPath);
        }
    }

    public static bool IsHelperInvocation(IReadOnlyList<string> arguments) =>
        arguments.Count == 2
        && string.Equals(arguments[0], HelperSwitch, StringComparison.Ordinal)
        && Guid.TryParse(arguments[1], out _);

    public static async Task<int> RunElevatedHelperAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        if (!IsHelperInvocation(arguments) || !Guid.TryParse(arguments[1], out var operationId))
        {
            return 64;
        }

        var requestPath = RepairWorkspace.GetRequestPath(operationId);
        var resultPath = RepairWorkspace.GetResultPath(operationId);
        NetworkRepairResult result;
        try
        {
            var request = await ReadRequestAsync(requestPath, cancellationToken).ConfigureAwait(false);
            var actionIds = ValidateActionIds(request?.ActionIds);
            var executor = new WindowsNetworkRepairExecutor();
            result = await executor.ExecuteAsync(actionIds, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            result = new NetworkRepairResult
            {
                StartedAtUtc = DateTimeOffset.UtcNow,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                Steps =
                [
                    new NetworkRepairStepResult
                    {
                        ActionId = NetworkRepairActionId.FlushDnsCache,
                        Title = "Start approved repairs",
                        Succeeded = false,
                        Summary = "NetCheck could not start the approved repair plan.",
                        Detail = SanitizeDetail(exception.Message)
                    }
                ]
            };
        }

        try
        {
            await WriteJsonAtomicallyAsync(resultPath, result, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return 74;
        }

        return result.Succeeded ? 0 : 1;
    }

    private static ProcessStartInfo CreateHelperStartInfo(Guid operationId, bool requiresElevation)
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("The current executable path is unavailable.");
        var startInfo = new ProcessStartInfo
        {
            FileName = processPath,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        if (requiresElevation)
        {
            startInfo.Verb = "runas";
        }

        if (string.Equals(
                Path.GetFileNameWithoutExtension(processPath),
                "dotnet",
                StringComparison.OrdinalIgnoreCase))
        {
            var entryAssembly = Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrWhiteSpace(entryAssembly))
            {
                throw new InvalidOperationException("The NetCheck entry assembly path is unavailable.");
            }

            startInfo.ArgumentList.Add(entryAssembly);
        }

        startInfo.ArgumentList.Add(HelperSwitch);
        startInfo.ArgumentList.Add(operationId.ToString("D"));
        return startInfo;
    }

    private static IReadOnlyList<NetworkRepairActionId> ValidateActionIds(
        IReadOnlyList<NetworkRepairActionId>? actionIds)
    {
        if (actionIds is null || actionIds.Count is 0 or > 6)
        {
            throw new InvalidDataException("The repair request contains an invalid number of actions.");
        }

        if (actionIds.Any(actionId => !Enum.IsDefined(actionId)))
        {
            throw new InvalidDataException("The repair request contains an unknown action.");
        }

        return actionIds.Distinct().ToArray();
    }

    private static async Task<RepairRequest?> ReadRequestAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<RepairRequest>(
            stream,
            JsonDefaults.Options,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<NetworkRepairResult?> ReadResultAsync(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<NetworkRepairResult>(
            stream,
            JsonDefaults.Options).ConfigureAwait(false);
    }

    private static async Task WriteJsonAtomicallyAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    value,
                    JsonDefaults.Options,
                    cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            DeleteIfPresent(temporaryPath);
        }
    }

    private static NetworkRepairResult FailedResult(NetworkRepairPlan plan, string message) => new()
    {
        StartedAtUtc = DateTimeOffset.UtcNow,
        CompletedAtUtc = DateTimeOffset.UtcNow,
        Steps = plan.Actions.Select(action => new NetworkRepairStepResult
        {
            ActionId = action.Id,
            Title = action.Title,
            Succeeded = false,
            RequiresRestart = action.RequiresRestart,
            Summary = message
        }).ToArray()
    };

    private static void DeleteIfPresent(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Temporary repair files contain only action identifiers and bounded command results.
        }
    }

    private static string SanitizeDetail(string detail)
    {
        var sanitized = detail.Replace('\0', ' ').Trim();
        return sanitized.Length <= 2000 ? sanitized : sanitized[..2000];
    }

    private sealed record RepairRequest
    {
        public IReadOnlyList<NetworkRepairActionId> ActionIds { get; init; } =
            Array.Empty<NetworkRepairActionId>();
    }

    private static class RepairWorkspace
    {
        public static string RootDirectory { get; } = Path.Combine(
            Path.GetTempPath(),
            "NetCheck",
            "Repair");

        public static string GetRequestPath(Guid operationId) =>
            Path.Combine(RootDirectory, $"{operationId:D}.request.json");

        public static string GetResultPath(Guid operationId) =>
            Path.Combine(RootDirectory, $"{operationId:D}.result.json");
    }

    private sealed class WindowsNetworkRepairExecutor
    {
        private const string InternetSettingsPath =
            @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
        private const int InternetOptionRefresh = 37;
        private const int InternetOptionSettingsChanged = 39;

        public async Task<NetworkRepairResult> ExecuteAsync(
            IReadOnlyList<NetworkRepairActionId> actionIds,
            CancellationToken cancellationToken)
        {
            var startedAt = DateTimeOffset.UtcNow;
            var steps = new List<NetworkRepairStepResult>(actionIds.Count);
            foreach (var actionId in actionIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                steps.Add(await ExecuteActionSafelyAsync(actionId, cancellationToken).ConfigureAwait(false));
            }

            return new NetworkRepairResult
            {
                StartedAtUtc = startedAt,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                Steps = steps
            };
        }

        private static async Task<NetworkRepairStepResult> ExecuteActionSafelyAsync(
            NetworkRepairActionId actionId,
            CancellationToken cancellationToken)
        {
            var action = NetworkRepairActions.Get(actionId);
            try
            {
                return actionId switch
                {
                    NetworkRepairActionId.FlushDnsCache => await CommandStepAsync(
                        action,
                        "ipconfig.exe",
                        ["/flushdns"],
                        "The DNS cache was cleared.",
                        cancellationToken).ConfigureAwait(false),
                    NetworkRepairActionId.RenewDhcpLease => await RenewDhcpAsync(
                        action,
                        cancellationToken).ConfigureAwait(false),
                    NetworkRepairActionId.ClearArpCache => await CommandStepAsync(
                        action,
                        "netsh.exe",
                        ["interface", "ip", "delete", "arpcache"],
                        "The local network address cache was refreshed.",
                        cancellationToken).ConfigureAwait(false),
                    NetworkRepairActionId.ResetUserProxy => ResetUserProxy(action),
                    NetworkRepairActionId.ResetWinsockCatalog => await CommandStepAsync(
                        action,
                        "netsh.exe",
                        ["winsock", "reset"],
                        "The Windows Sockets catalog was reset.",
                        cancellationToken).ConfigureAwait(false),
                    NetworkRepairActionId.ResetTcpIpStack => await CommandStepAsync(
                        action,
                        "netsh.exe",
                        ["int", "ip", "reset"],
                        "The TCP/IP stack was reset.",
                        cancellationToken).ConfigureAwait(false),
                    _ => throw new ArgumentOutOfRangeException(nameof(actionId), actionId, null)
                };
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return new NetworkRepairStepResult
                {
                    ActionId = action.Id,
                    Title = action.Title,
                    Succeeded = false,
                    RequiresRestart = action.RequiresRestart,
                    Summary = "Windows could not apply this repair.",
                    Detail = SanitizeDetail(exception.Message)
                };
            }
        }

        private static async Task<NetworkRepairStepResult> RenewDhcpAsync(
            NetworkRepairAction action,
            CancellationToken cancellationToken)
        {
            var release = await RunCommandAsync(
                "ipconfig.exe",
                ["/release"],
                cancellationToken).ConfigureAwait(false);
            var renew = await RunCommandAsync(
                "ipconfig.exe",
                ["/renew"],
                cancellationToken).ConfigureAwait(false);
            return new NetworkRepairStepResult
            {
                ActionId = action.Id,
                Title = action.Title,
                Succeeded = renew.ExitCode == 0,
                RequiresRestart = action.RequiresRestart,
                Summary = renew.ExitCode == 0
                    ? "Windows requested a fresh DHCP address."
                    : "Windows could not renew the DHCP address.",
                Detail = SanitizeDetail(
                    $"Release ({release.ExitCode}): {release.Output}\nRenew ({renew.ExitCode}): {renew.Output}")
            };
        }

        private static async Task<NetworkRepairStepResult> CommandStepAsync(
            NetworkRepairAction action,
            string executableName,
            IReadOnlyList<string> arguments,
            string successMessage,
            CancellationToken cancellationToken)
        {
            var result = await RunCommandAsync(
                executableName,
                arguments,
                cancellationToken).ConfigureAwait(false);
            return new NetworkRepairStepResult
            {
                ActionId = action.Id,
                Title = action.Title,
                Succeeded = result.ExitCode == 0,
                RequiresRestart = action.RequiresRestart,
                Summary = result.ExitCode == 0
                    ? successMessage
                    : "Windows reported that this repair did not complete.",
                Detail = SanitizeDetail(result.Output)
            };
        }

        private static NetworkRepairStepResult ResetUserProxy(NetworkRepairAction action)
        {
            using var key = Registry.CurrentUser.CreateSubKey(InternetSettingsPath, writable: true)
                ?? throw new SecurityException("Windows proxy settings are unavailable.");
            key.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
            key.DeleteValue("AutoConfigURL", throwOnMissingValue: false);
            InternetSetOption(IntPtr.Zero, InternetOptionSettingsChanged, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, InternetOptionRefresh, IntPtr.Zero, 0);
            return new NetworkRepairStepResult
            {
                ActionId = action.Id,
                Title = action.Title,
                Succeeded = true,
                RequiresRestart = action.RequiresRestart,
                Summary = "The current user proxy was turned off."
            };
        }

        private static async Task<CommandResult> RunCommandAsync(
            string executableName,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            var executablePath = Path.Combine(Environment.SystemDirectory, executableName);
            if (!File.Exists(executablePath))
            {
                throw new FileNotFoundException("A required Windows repair tool is unavailable.", executablePath);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                throw new InvalidOperationException("Windows could not start a repair command.");
            }

            var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var output = string.Join(
                Environment.NewLine,
                new[] { await standardOutput.ConfigureAwait(false), await standardError.ConfigureAwait(false) }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
            return new CommandResult(process.ExitCode, output);
        }

        [DllImport("wininet.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InternetSetOption(
            IntPtr internet,
            int option,
            IntPtr buffer,
            int bufferLength);

        private sealed record CommandResult(int ExitCode, string Output);
    }
}

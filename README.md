# NetCheck

NetCheck is a native Windows desktop application that explains *why* a computer has no internet access, limited connectivity, or an unstable connection. It performs a read-only assessment, correlates the evidence, and presents a plain-language diagnosis with safe next steps.

## What it checks

| Stage | What NetCheck determines |
|---|---|
| Network adapter | Whether Windows sees an active Ethernet or Wi-Fi link |
| IP configuration | Whether the adapter has a usable address or a DHCP/APIPA problem |
| Default gateway | Whether a route exists and the local router responds |
| DNS resolution | Whether a known hostname resolves through the configured DNS service |
| Internet reachability | Whether public IP addresses are directly reachable |
| Web connectivity | Whether HTTP access works or a captive/sign-in portal intercepts it |
| Connection stability | Short-sample packet loss, average latency, and jitter |
| Proxy configuration | Whether a user proxy may explain a failed web check |

The diagnosis engine correlates results instead of treating every failed ping as an internet outage. For example, it can distinguish a DNS failure from a gateway failure, and it uses the web check to avoid classifying an ICMP-blocking network as completely offline.

## User experience

- Modern WPF dashboard with live progress and cancellation
- Plain-language overall diagnosis and prioritized next steps
- Expandable technical evidence for every check
- Local diagnostic history with report review
- Privacy-aware HTML, JSON, and text export
- Configurable endpoints, timeouts, sample count, and quality thresholds
- No administrator rights and no automatic network-setting changes
- Keyboard navigation, high-contrast status text, and automation labels

## Requirements

- A Microsoft-supported x64 Windows release: Windows 11, or a supported Windows 10 LTSC/Enterprise edition
- .NET 10 SDK for development

Release builds are self-contained; end users do not need to install .NET. The application manifest and target platform retain Windows build 17763 as the technical minimum, but production use should remain on an operating-system release supported by Microsoft.

## Build and run

From PowerShell:

```powershell
.\scripts\build.ps1
.\.dotnet10\dotnet.exe run --project .\src\NetCheck.App\NetCheck.App.csproj
```

If the repository-local SDK is not present, `build.ps1` uses `dotnet` from `PATH`.

To build manually:

```powershell
dotnet restore .\NetCheck.sln
dotnet build .\NetCheck.sln --configuration Debug --no-restore
dotnet test .\NetCheck.sln --configuration Debug --no-build
```

## Publish

Create a self-contained Windows x64 release and ZIP archive:

```powershell
.\scripts\publish.ps1
```

Outputs are written under `artifacts\publish\win-x64` and `artifacts\NetCheck-1.0.0-win-x64.zip`.

## Privacy and data

NetCheck sends only the traffic required by its configured checks: DNS lookup, ICMP echo requests, and one lightweight HTTP connectivity request. The defaults are visible and editable in Settings.

Local files are stored in `%LOCALAPPDATA%\NetCheck`:

- `Reports\` — completed diagnostic history
- `settings.json` — user preferences
- `NetCheck.log` — created only when an application error is recorded

Computer names are excluded from exports by default. Adapter MAC addresses are always redacted from exported reports. NetCheck does not upload reports or telemetry.

See [Architecture](docs/ARCHITECTURE.md), [Security and privacy](SECURITY.md), and [Support guide](docs/SUPPORT.md) for details.

## Repository structure

```text
src/
  NetCheck.Core/             Domain models, interfaces, engine, diagnosis rules
  NetCheck.Infrastructure/   Windows probes, storage, export, logging
  NetCheck.App/              WPF application and MVVM presentation
tests/
  NetCheck.Core.Tests/
  NetCheck.Infrastructure.Tests/
scripts/
  build.ps1
  publish.ps1
```

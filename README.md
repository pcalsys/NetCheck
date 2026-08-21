# NetCheck

NetCheck is a native Windows desktop application that explains *why* a computer has no internet access, limited connectivity, or an unstable connection. It performs a read-only assessment, correlates the evidence, presents a plain-language diagnosis, and can apply an explicitly approved repair plan for supported Windows configuration problems.

## What is new in 1.3

- Complete English and German localization across the shell, dashboard, diagnostics, history, settings, dialogs, and exported reports
- Immediate language switching while retaining language-neutral saved diagnostic data
- Simplified application shell without the former product badge, tagline, or private/local status badge
- High-contrast white language choices and a concise `created by pcalsys` credit
- A prominent experienced-users-only warning for advanced diagnostic settings
- The evidence-based, confirmation-gated repair workflow introduced in 1.2 remains unchanged

See the [changelog](CHANGELOG.md) for release details.

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

- Friendly, modern WPF dashboard with live progress and cancellation
- Complete, switchable English or German interface and diagnostic presentation
- Plain-language overall diagnosis and prioritized next steps
- Evidence-based repair plans with per-step results and restart guidance
- Expandable technical evidence for every check
- Local diagnostic history with report review
- Privacy-aware HTML, JSON, and text export
- Configurable endpoints, timeouts, sample count, and quality thresholds
- Diagnostics run without elevation; repairs are never automatic and may request UAC after confirmation
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

Outputs are written under `artifacts\publish\win-x64` and `artifacts\NetCheck-1.3.0-win-x64.zip`.

## Privacy and data

NetCheck sends only the traffic required by its configured checks: DNS lookup, ICMP echo requests, and one lightweight HTTP connectivity request. The defaults are visible and editable in Settings.

The Fix workflow runs only after confirmation. It may renew DHCP, clear DNS or ARP caches, disable an identified current-user proxy, or reset Windows network components. NetCheck does not attempt to change physical connectivity, managed static addressing, captive portals, Wi-Fi signal quality, router configuration, or provider infrastructure.

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

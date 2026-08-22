# Changelog

All notable NetCheck changes are documented here. Versions follow semantic versioning.

## [1.2.0] - 2026-08-22

### Added

- Integrated Monitoring page with 15/30/60-minute and continuous runs plus Standard, Gaming, Streaming, and Home office profiles.
- Live latency, jitter, and packet-loss charts, current online/degraded/offline status, exact outage/recovery events, and session summaries.
- Atomic local monitoring history, profile-specific local baseline comparison, and safely finalized partial sessions during shutdown.
- Independent IPv4/IPv6 probes and traceroutes, Wi-Fi signal/channel/band/rates, network driver, VPN adapter, and Windows Firewall inspection.
- Correlation with local WLAN, DHCP, and NetworkProfile Windows events without allowing one failed probe to stop monitoring.
- App-wide accessible outage and recovery notifications that remain active across page navigation.
- Download/upload trend chart for previous speed-test history.
- Locally generated support ZIP with automatic user name, computer name, SSID, MAC-address, and IP-address redaction.
- Manual HTTPS-only update check against the fixed official GitHub repository, requiring a matching release ZIP and SHA-256 asset pair.
- Inno Setup release pipeline with optional CA-issued Authenticode signing and explicit unsigned behavior when secrets are absent.
- Automated tests for the monitoring engine, baseline, persistence, support bundle, update checking, History presentation, and Monitoring ViewModel.

### Changed

- Unified History now includes monitoring sessions and clears all three local history stores after confirmation.
- Application, assembly, manifest, package, and workflow versions are consistently 1.2.0.
- The sidebar version label now reads the running assembly version instead of displaying a hard-coded value.

### Security

- Release signing rejects self-signed certificates and verifies SHA-256 Authenticode signatures after timestamping.
- Update links are restricted to HTTPS URLs under `github.com/pcalsys/NetCheck` and downloaded files are never auto-executed.
- Support packages use generic entry names, bounded source files, atomic creation, and conservative redaction.

## [1.1.0] - 2026-08-22

### Added

- Approximately 30-second speed-test observation window with five download and four upload rounds.
- Structured local history for completed speed tests.
- Structured audit entries for actual settings changes with previous and new values.
- Local history entries for English and German menu-language switches.
- Unified, bilingual History view for diagnoses, speed tests, and configuration activity.

### Changed

- Speed-test probes now measure aggregate parallel throughput before sizing the full run.
- Speed-test traffic remains bounded to approximately 200 MB while samples are spread across the observation window.
- History clearing now removes diagnostic reports and activity entries after confirmation.

### Fixed

- Speed-test requests automatically fall back from HTTP/2 to HTTP/1.1 when required by a router, proxy, or provider network.
- The speed-test progress bar uses a one-way binding appropriate for its read-only view-model property.

## [1.0.0] - 2026-08-22

### Added

- Guided Windows network diagnosis covering adapter, IP configuration, gateway, DNS, internet reachability, web connectivity, stability, and proxy configuration.
- Evidence-based repair plans that always require explicit approval.
- English and German user interface and diagnostic presentation.
- Local diagnostic reports with privacy-aware HTML, JSON, and text exports.
- Self-contained Windows x64 package and automated GitHub release workflow.

[1.2.0]: https://github.com/pcalsys/NetCheck/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/pcalsys/NetCheck/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/pcalsys/NetCheck/releases/tag/v1.0.0

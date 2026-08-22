# Changelog

All notable NetCheck changes are documented here. Versions follow semantic versioning.

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

[1.1.0]: https://github.com/pcalsys/NetCheck/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/pcalsys/NetCheck/releases/tag/v1.0.0

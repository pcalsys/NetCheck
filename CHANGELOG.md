# Changelog

All notable changes to NetCheck are documented here.

## Unreleased

### Added

- One-command `build.cmd` entry point for clean Windows clones
- Automatic, non-administrative installation and integrity checking of the required .NET 10 SDK
- Release ZIP and SHA-256 checksum generation as part of the default build

### Changed

- The default build now uses Release configuration, runs every test, and creates the self-contained Windows x64 executable
- GitHub Actions exercises the same build entry point documented for contributors
- Source-build documentation now lists the exact clone, build, output, and launch commands

## 1.3.0 — 2026-08-21

### Added

- Complete English and German localization for pages, live diagnostic results, history, dialogs, repair copy, and report exports
- Experienced-users-only warning above the advanced settings
- Localization parity and presentation-projection tests

### Changed

- Replaced the lower-left safety card with `created by pcalsys`
- Made both language choices white for stronger contrast on the dark-blue sidebar
- Removed the upper-left logo, `NetCheck` wordmark, and `Network Clarity` tagline
- Removed the upper-right `Private & local` badge
- Kept persisted diagnostics language-neutral so switching languages can re-render existing reports

## 1.2.0 — 2026-08-21

### Added

- Evidence-based **Fix issue** workflow on the diagnostic dashboard
- Explicit repair-plan confirmation, UAC boundary, per-step outcomes, and restart guidance
- Supported DHCP renewal, DNS and ARP cache refresh, user-proxy reset, Winsock reset, and TCP/IP reset
- Automatic post-repair diagnostics when no restart is required
- Repair-planner tests covering safe automation and manual-only boundaries

### Security

- Elevated repair helper accepts only validated, bounded action identifiers
- Native Windows tools use structured argument lists without arbitrary shell commands
- Physical, managed/static, captive-portal, isolated ping, and stability issues remain manual-only

## 1.1.0 — 2026-08-21

### Added

- Persistent English and German navigation menu options
- Active navigation states and a local-privacy indicator
- Human-readable onboarding cards before the first diagnostic
- Release documentation for the menu localization boundary

### Changed

- Redesigned the application shell, dashboard, history, and settings views
- Improved status hierarchy, spacing, typography, icons, and technical evidence presentation
- Refined user-facing copy while keeping diagnostic output in English
- Updated the self-contained Windows x64 release version to 1.1.0

### Quality

- Added settings-store tests for language normalization and English fallback
- Verified the live WPF interface in both menu languages

## 1.0.0

- Initial NetCheck release with read-only network diagnostics, report history, settings, and exports

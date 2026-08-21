# Security and privacy

## Security model

NetCheck runs with the current user’s permissions and declares `asInvoker` in its application manifest. It does not require administrator rights, install a service or driver, capture packets, modify the registry, reset adapters, flush caches, or change network configuration.

Diagnostics are intentionally observational. Recommended repair commands are displayed as guidance and are never executed by the application.

## Network activity

The default assessment may contact:

- The configured DNS resolver for `www.microsoft.com`
- `1.1.1.1` and `8.8.8.8` using ICMP echo until one responds
- `http://www.msftconnecttest.com/connecttest.txt` using HTTP

Targets are editable. The connectivity endpoint intentionally uses HTTP so NetCheck can observe captive-portal redirects; response reading is capped at 1,024 characters. NetCheck sends no diagnostic report, unique identifier, account information, or usage telemetry.

## Stored data

Diagnostic history includes local adapter names, IP configuration, DNS/gateway addresses, check evidence, and the Windows computer name. It is stored only in the current user’s `%LOCALAPPDATA%\NetCheck\Reports` directory and can be disabled or cleared in the application.

Exports always redact MAC addresses. The Windows computer name is redacted unless the user explicitly enables it in Settings. Users should still review exported reports before sharing them because local IP addresses and infrastructure details can be sensitive.

## Reporting a vulnerability

Do not include private diagnostic reports, credentials, or internal network details in a public issue. Provide the smallest reproducible example and remove identifying evidence before sharing it with the project maintainer.


# Security and privacy

## Security model

NetCheck runs with the current user’s permissions and declares `asInvoker` in its application manifest. Diagnostics do not require administrator rights. NetCheck does not install a service or driver, capture packets, or make a repair without an explicit confirmation.

Diagnostics are intentionally observational. For supported Windows configuration failures, the Fix workflow displays an evidence-based repair plan before execution. Plans may renew DHCP, clear DNS or ARP caches, disable the current user’s identified proxy configuration, or reset Winsock and TCP/IP components. Physical links, static or managed addressing, captive portals, signal quality, routers, and provider infrastructure are never changed automatically.

Repairs requiring administrator rights run in a short-lived elevated NetCheck helper after UAC approval. The elevation boundary accepts only a validated operation identifier and a bounded list of known repair enums. Native Windows tools are invoked directly with structured arguments; arbitrary command text and shell execution are not supported. Temporary request and result files are deleted after the operation. Winsock and TCP/IP resets require a Windows restart.

## Network activity

The default assessment may contact:

- The configured DNS resolver for `www.microsoft.com`
- `1.1.1.1` and `8.8.8.8` using ICMP echo until one responds
- `http://www.msftconnecttest.com/connecttest.txt` using HTTP

The optional speed test contacts `https://speed.cloudflare.com/__down` and `https://speed.cloudflare.com/__up` only after the user starts it. It uses cache-bypassed, bounded transfers with a combined cap of about 210 MB. NetCheck displays both average throughput and the fastest sustained sample, does not persist the result, and sends no report or NetCheck telemetry with the test. The endpoint operator still receives the ordinary network metadata inherent in any HTTPS request, such as the public IP address.

Targets are editable. The connectivity endpoint intentionally uses HTTP so NetCheck can observe captive-portal redirects; response reading is capped at 1,024 characters. NetCheck sends no diagnostic report, unique identifier, account information, or usage telemetry.

## Stored data

Diagnostic history includes local adapter names, IP configuration, DNS/gateway addresses, check evidence, and the Windows computer name. It is stored only in the current user’s `%LOCALAPPDATA%\NetCheck\Reports` directory and can be disabled or cleared in the application.

Exports always redact MAC addresses. The Windows computer name is redacted unless the user explicitly enables it in Settings. Users should still review exported reports before sharing them because local IP addresses and infrastructure details can be sensitive.

## Reporting a vulnerability

Do not include private diagnostic reports, credentials, or internal network details in a public issue. Provide the smallest reproducible example and remove identifying evidence before sharing it with the project maintainer.

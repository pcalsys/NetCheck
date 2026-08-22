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

The optional speed test contacts `https://speed.cloudflare.com/__down` and `https://speed.cloudflare.com/__up` only after the user starts it. It uses cache-bypassed, bounded measurement rounds with a combined cap of about 200 MB. NetCheck displays both average throughput and the fastest sustained sample, stores the completed result only in local history, and sends no report or NetCheck telemetry with the test. The endpoint operator still receives the ordinary network metadata inherent in any HTTPS request, such as the public IP address.

Targets are editable. The connectivity endpoint intentionally uses HTTP so NetCheck can observe captive-portal redirects; response reading is capped at 1,024 characters. NetCheck sends no diagnostic report, unique identifier, account information, or usage telemetry.

Monitoring is also opt-in. Its defaults use ICMP with `1.1.1.1` and `2606:4700:4700::1111`, DNS resolution for `www.microsoft.com`, and HTTPS to `www.msftconnecttest.com`. IPv4 and IPv6 are independent, traceroutes stop after 12 hops, and all network/process operations have cancellation and bounded timeouts. Wi-Fi, driver, VPN, firewall, and WLAN/DHCP/NetworkProfile event information is read locally.

The manual update checker contacts only `https://api.github.com/repos/pcalsys/NetCheck/releases/latest`. It requires HTTPS, validates that release and download paths belong to `pcalsys/NetCheck`, and treats a package as complete only when the version-matched ZIP and SHA-256 file both exist. NetCheck neither downloads nor executes release content automatically.

## Stored data

Diagnostic history includes local adapter names, IP configuration, DNS/gateway addresses, check evidence, and the Windows computer name. It is stored only in the current user’s `%LOCALAPPDATA%\NetCheck\Reports` directory and can be disabled in Settings.

Completed speed-test results and actual configuration changes are stored as structured entries under `%LOCALAPPDATA%\NetCheck\Activities`. Completed and user-stopped monitoring sessions are stored under `%LOCALAPPDATA%\NetCheck\Monitoring`; they may include adapter names, SSIDs, IP/gateway data, routes, driver versions, and relevant Windows network events. The History page can clear report, activity, and monitoring history after explicit confirmation.

Exports always redact MAC addresses. The Windows computer name is redacted unless the user explicitly enables it in Settings. Users should still review exported reports before sharing them because local IP addresses and infrastructure details can be sensitive.

The support-bundle command is stricter than ordinary report export. It works locally, reads only bounded known NetCheck text files, gives archive entries generic names, and redacts the current user name, computer name, discovered SSIDs, MAC addresses, and IPv4/IPv6 addresses. The archive is never uploaded by NetCheck. Users should still inspect it before sharing.

## Release signing

Release signing is optional. The GitHub workflow expects a CA-issued PFX in `NETCHECK_SIGNING_CERTIFICATE_BASE64` and its password in `NETCHECK_SIGNING_CERTIFICATE_PASSWORD`. Both must be configured together. The signing helper rejects self-signed leaf certificates, timestamps over HTTPS, and requires Windows to report a valid Authenticode signature. When the secrets are absent, the workflow explicitly produces unsigned artifacts and does not claim they are trusted.

## Reporting a vulnerability

Do not include private diagnostic reports, credentials, or internal network details in a public issue. Provide the smallest reproducible example and remove identifying evidence before sharing it with the project maintainer.

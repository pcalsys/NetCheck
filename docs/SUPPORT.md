# NetCheck support guide

## The application does not start

Release builds are for 64-bit Windows 10 version 1809 or later. If NetCheck closes unexpectedly, inspect `%LOCALAPPDATA%\NetCheck\NetCheck.log`. The log contains application errors only; it does not contain passwords, browser history, or captured network traffic.

## A check is skipped

Skipped checks normally depend on an earlier layer. For example, internet checks cannot provide useful results when no active adapter or usable address exists. Resolve the first failed check and run the diagnostic again.

## Ping fails but websites work

Some routers, firewalls, providers, and managed networks block ICMP echo requests. NetCheck correlates the direct ping and web checks; a failed ping by itself should be reported as attention rather than a complete outage when web access succeeds.

## A sign-in page is detected

Open a browser and complete the network sign-in flow, then rerun NetCheck. Hotels, airports, schools, and guest networks commonly use these portals. On a trusted private network without a portal, review VPN, proxy, DNS-filtering, and security software.

## Corporate networks

Do not disable organization-managed proxies, VPNs, firewalls, or DNS settings without approval. Export a report and share it with the network administrator. Computer names are excluded by default and MAC addresses are always redacted; still review a report before sharing it outside the organization.

## Reset settings

Use Settings → Restore defaults → Save settings. If the settings file is malformed or unavailable, NetCheck safely falls back to defaults.


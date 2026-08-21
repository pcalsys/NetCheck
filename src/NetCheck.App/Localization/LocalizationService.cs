using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using NetCheck.App.Mvvm;
using NetCheck.Core.Abstractions;

namespace NetCheck.App.Localization;

public sealed partial class LocalizationService : ObservableObject, ITextLocalizer
{
    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo GermanCulture = CultureInfo.GetCultureInfo("de-DE");
    private static readonly ReadOnlyDictionary<string, string> GermanTranslations =
        new(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Not available"] = "Nicht verfügbar",
            ["Unknown"] = "Unbekannt",
            ["None"] = "Keine",
            ["None detected"] = "Keine erkannt",
            ["Not configured"] = "Nicht konfiguriert",
            ["System default"] = "Systemstandard",
            ["No reply"] = "Keine Antwort",
            ["No replies"] = "Keine Antworten",
            ["Not enough data"] = "Nicht genügend Daten",
            ["Not supplied"] = "Nicht angegeben",
            ["Enabled"] = "Aktiviert",
            ["Disabled"] = "Deaktiviert",
            ["Manual or system-managed"] = "Manuell oder systemverwaltet",
            ["Redacted"] = "Ausgeblendet",

            ["Network adapter"] = "Netzwerkadapter",
            ["Checks whether Windows has an active Ethernet or Wi-Fi connection."] = "Prüft, ob Windows über eine aktive Ethernet- oder WLAN-Verbindung verfügt.",
            ["No active Ethernet or Wi-Fi adapter was detected."] = "Es wurde kein aktiver Ethernet- oder WLAN-Adapter erkannt.",
            ["Windows reports that no usable network interface is connected."] = "Windows meldet, dass keine verwendbare Netzwerkschnittstelle verbunden ist.",
            ["Adapters found"] = "Gefundene Adapter",
            ["Active adapters"] = "Aktive Adapter",
            ["Turn on Wi-Fi or reconnect the Ethernet cable."] = "Aktivieren Sie WLAN oder schließen Sie das Ethernet-Kabel erneut an.",
            ["Disable Airplane mode."] = "Deaktivieren Sie den Flugmodus.",
            ["Open Windows Network Connections and make sure the adapter is enabled."] = "Öffnen Sie die Windows-Netzwerkverbindungen und stellen Sie sicher, dass der Adapter aktiviert ist.",
            ["If the adapter is missing, reinstall or update its driver in Device Manager."] = "Wenn der Adapter fehlt, installieren oder aktualisieren Sie seinen Treiber im Geräte-Manager.",
            ["Windows reports an operational network link."] = "Windows meldet eine funktionsfähige Netzwerkverbindung.",
            ["Adapter"] = "Adapter",
            ["Type"] = "Typ",
            ["Status"] = "Status",
            ["Link speed"] = "Verbindungsgeschwindigkeit",

            ["IP configuration"] = "IP-Konfiguration",
            ["Validates the address assigned to the active adapter."] = "Überprüft die dem aktiven Adapter zugewiesene Adresse.",
            ["Skipped because no active network adapter is available."] = "Übersprungen, da kein aktiver Netzwerkadapter verfügbar ist.",
            ["An address in 169.254.0.0/16 normally means the computer could not obtain an IPv4 address from DHCP."] = "Eine Adresse im Bereich 169.254.0.0/16 bedeutet normalerweise, dass der Computer keine IPv4-Adresse per DHCP erhalten konnte.",
            ["Reconnect to the network, then run the diagnostic again."] = "Stellen Sie die Netzwerkverbindung erneut her und führen Sie die Diagnose noch einmal aus.",
            ["Restart the router or DHCP server if other devices are also affected."] = "Starten Sie den Router oder DHCP-Server neu, wenn auch andere Geräte betroffen sind.",
            ["In an elevated Command Prompt, run ‘ipconfig /release’ followed by ‘ipconfig /renew’."] = "Führen Sie in einer Eingabeaufforderung mit Administratorrechten zuerst ›ipconfig /release‹ und danach ›ipconfig /renew‹ aus.",
            ["Verify that the adapter is configured to obtain an IP address automatically."] = "Prüfen Sie, ob der Adapter seine IP-Adresse automatisch bezieht.",
            ["The active adapter has no usable IP address."] = "Der aktive Adapter besitzt keine verwendbare IP-Adresse.",
            ["A valid IPv4 or globally routable IPv6 address is required to reach other networks."] = "Für den Zugriff auf andere Netzwerke ist eine gültige IPv4- oder global routbare IPv6-Adresse erforderlich.",
            ["Disconnect and reconnect the network adapter."] = "Trennen und verbinden Sie den Netzwerkadapter erneut.",
            ["Verify DHCP or the manually configured address, subnet, and gateway."] = "Prüfen Sie DHCP oder die manuell konfigurierte Adresse, das Subnetz und das Gateway.",
            ["Restart the computer and router if the address remains unavailable."] = "Starten Sie Computer und Router neu, wenn weiterhin keine Adresse verfügbar ist.",
            ["The local IP configuration appears usable."] = "Die lokale IP-Konfiguration scheint verwendbar zu sein.",
            ["IP addresses"] = "IP-Adressen",
            ["Address assignment"] = "Adresszuweisung",
            ["Default gateways"] = "Standardgateways",
            ["DNS servers"] = "DNS-Server",

            ["Default gateway"] = "Standardgateway",
            ["Checks the path from this computer to the local router."] = "Prüft den Pfad von diesem Computer zum lokalen Router.",
            ["Skipped because the local network configuration is not usable."] = "Übersprungen, da die lokale Netzwerkkonfiguration nicht verwendbar ist.",
            ["No default gateway is configured."] = "Es ist kein Standardgateway konfiguriert.",
            ["Without a default gateway, the computer normally cannot reach the internet."] = "Ohne Standardgateway kann der Computer normalerweise nicht auf das Internet zugreifen.",
            ["Renew the DHCP lease or verify the manually configured default gateway."] = "Erneuern Sie die DHCP-Lease oder prüfen Sie das manuell konfigurierte Standardgateway.",
            ["Compare the IP settings with another working device on the same network."] = "Vergleichen Sie die IP-Einstellungen mit einem funktionierenden Gerät im selben Netzwerk.",
            ["Communication with the local router is working."] = "Die Kommunikation mit dem lokalen Router funktioniert.",
            ["Gateway"] = "Gateway",
            ["Round-trip time"] = "Paketumlaufzeit",
            ["The default gateway did not answer ping requests."] = "Das Standardgateway hat nicht auf Ping-Anfragen geantwortet.",
            ["Some routers block ping, so NetCheck will continue with direct internet checks before drawing a conclusion."] = "Einige Router blockieren Ping. NetCheck setzt deshalb die direkten Internetprüfungen fort, bevor eine Schlussfolgerung gezogen wird.",
            ["Attempts"] = "Versuche",
            ["Check the Wi-Fi signal or Ethernet cable."] = "Prüfen Sie das WLAN-Signal oder das Ethernet-Kabel.",
            ["Restart the router if internet checks also fail."] = "Starten Sie den Router neu, wenn auch die Internetprüfungen fehlschlagen.",
            ["Verify that the configured gateway belongs to the adapter’s local subnet."] = "Prüfen Sie, ob das konfigurierte Gateway zum lokalen Subnetz des Adapters gehört.",

            ["DNS resolution"] = "DNS-Namensauflösung",
            ["Checks whether domain names can be translated into IP addresses."] = "Prüft, ob Domänennamen in IP-Adressen aufgelöst werden können.",
            ["No DNS test host is configured."] = "Es ist kein DNS-Testhost konfiguriert.",
            ["Choose a valid public hostname in Settings, then run the diagnostic again."] = "Wählen Sie in den Einstellungen einen gültigen öffentlichen Hostnamen und führen Sie die Diagnose erneut aus.",
            ["The DNS response contained no usable address."] = "Die DNS-Antwort enthielt keine verwendbare Adresse.",
            ["The configured DNS resolver returned one or more addresses."] = "Der konfigurierte DNS-Resolver hat eine oder mehrere Adressen zurückgegeben.",
            ["Test host"] = "Testhost",
            ["Resolved addresses"] = "Aufgelöste Adressen",
            ["Configured DNS"] = "Konfiguriertes DNS",
            ["The DNS server did not return a usable answer within the allowed time."] = "Der DNS-Server hat innerhalb der zulässigen Zeit keine verwendbare Antwort geliefert.",
            ["Error"] = "Fehler",
            ["Restart the router and run the diagnostic again."] = "Starten Sie den Router neu und führen Sie die Diagnose erneut aus.",
            ["Verify the DNS addresses in the adapter settings."] = "Prüfen Sie die DNS-Adressen in den Adaptereinstellungen.",
            ["Temporarily test a trusted public DNS resolver, if permitted by your organization."] = "Testen Sie vorübergehend einen vertrauenswürdigen öffentlichen DNS-Resolver, sofern Ihre Organisation dies erlaubt.",
            ["On managed networks, contact the network administrator before changing DNS settings."] = "Wenden Sie sich in verwalteten Netzwerken an den Netzwerkadministrator, bevor Sie DNS-Einstellungen ändern.",

            ["Internet reachability"] = "Interneterreichbarkeit",
            ["Tests direct connectivity to reliable public IP addresses."] = "Prüft die direkte Verbindung zu zuverlässigen öffentlichen IP-Adressen.",
            ["No valid internet ping target is configured."] = "Es ist kein gültiges Internet-Pingziel konfiguriert.",
            ["Restore the default diagnostic targets in Settings."] = "Stellen Sie in den Einstellungen die standardmäßigen Diagnoseziele wieder her.",
            ["A public IP address is reachable without relying on DNS."] = "Eine öffentliche IP-Adresse ist ohne DNS erreichbar.",
            ["Target"] = "Ziel",
            ["All attempts"] = "Alle Versuche",
            ["No public ping target responded."] = "Kein öffentliches Pingziel hat geantwortet.",
            ["Direct internet reachability could not be confirmed. Some networks block ping, so the web check will provide additional evidence."] = "Die direkte Interneterreichbarkeit konnte nicht bestätigt werden. Da einige Netzwerke Ping blockieren, liefert die Webprüfung weitere Hinweise.",
            ["Check whether other devices on the same network can reach the internet."] = "Prüfen Sie, ob andere Geräte im selben Netzwerk auf das Internet zugreifen können.",
            ["Restart the modem or router if all devices are affected."] = "Starten Sie Modem oder Router neu, wenn alle Geräte betroffen sind.",
            ["Temporarily disconnect a VPN and run the diagnostic again."] = "Trennen Sie vorübergehend die VPN-Verbindung und führen Sie die Diagnose erneut aus.",
            ["Contact the internet provider if the local network works but all internet checks fail."] = "Wenden Sie sich an den Internetanbieter, wenn das lokale Netzwerk funktioniert, aber alle Internetprüfungen fehlschlagen.",

            ["Web connectivity"] = "Webverbindung",
            ["Checks web access and detects common network sign-in pages."] = "Prüft den Webzugriff und erkennt typische Netzwerkanmeldeseiten.",
            ["The configured connectivity URL is not valid."] = "Die konfigurierte URL für die Verbindungsprüfung ist ungültig.",
            ["Choose an HTTP or HTTPS connectivity URL in Settings."] = "Wählen Sie in den Einstellungen eine HTTP- oder HTTPS-URL für die Verbindungsprüfung.",
            ["Web access is working without redirection."] = "Der Webzugriff funktioniert ohne Umleitung.",
            ["The connectivity endpoint returned the expected response."] = "Der Verbindungsendpunkt hat die erwartete Antwort geliefert.",
            ["Endpoint"] = "Endpunkt",
            ["HTTP status"] = "HTTP-Status",
            ["The endpoint returned different content than expected."] = "Der Endpunkt hat andere Inhalte als erwartet zurückgegeben.",
            ["A network sign-in page or web interception was detected."] = "Eine Netzwerkanmeldeseite oder eine Umleitung des Webverkehrs wurde erkannt.",
            ["Issue type"] = "Problemtyp",
            ["Captive portal"] = "Anmeldeportal",
            ["Response"] = "Antwort",
            ["Open a web browser and complete the Wi-Fi or network sign-in page."] = "Öffnen Sie einen Webbrowser und schließen Sie die WLAN- oder Netzwerkanmeldung ab.",
            ["After signing in, run the diagnostic again."] = "Führen Sie die Diagnose nach der Anmeldung erneut aus.",
            ["On a trusted network without a sign-in page, review proxy, VPN, and security software settings."] = "Prüfen Sie in einem vertrauenswürdigen Netzwerk ohne Anmeldeseite die Proxy-, VPN- und Sicherheitssoftwareeinstellungen.",
            ["The web connectivity test failed."] = "Die Webverbindungsprüfung ist fehlgeschlagen.",
            ["NetCheck could not retrieve the configured connectivity endpoint."] = "NetCheck konnte den konfigurierten Verbindungsendpunkt nicht abrufen.",
            ["Verify proxy, VPN, firewall, and security software settings."] = "Prüfen Sie die Einstellungen von Proxy, VPN, Firewall und Sicherheitssoftware.",
            ["Try opening a known website in a browser."] = "Versuchen Sie, eine bekannte Website im Browser zu öffnen.",
            ["If direct internet checks also fail, restart the router or contact the network provider."] = "Wenn auch direkte Internetprüfungen fehlschlagen, starten Sie den Router neu oder wenden Sie sich an den Netzbetreiber.",

            ["Connection stability"] = "Verbindungsstabilität",
            ["Samples latency and packet loss over several requests."] = "Misst Latenz und Paketverlust über mehrere Anfragen.",
            ["Skipped because no public ping target responded during the reachability check."] = "Übersprungen, da während der Erreichbarkeitsprüfung kein öffentliches Pingziel geantwortet hat.",
            ["Samples"] = "Messungen",
            ["Successful"] = "Erfolgreich",
            ["Packet loss"] = "Paketverlust",
            ["Average latency"] = "Durchschnittliche Latenz",
            ["Jitter"] = "Jitter",
            ["Packet loss and high latency can cause slow pages, video buffering, and interrupted calls."] = "Paketverlust und hohe Latenz können langsame Webseiten, Videopufferung und unterbrochene Gespräche verursachen.",
            ["Move closer to the Wi-Fi access point or use Ethernet for comparison."] = "Gehen Sie näher an den WLAN-Zugangspunkt oder verwenden Sie zum Vergleich Ethernet.",
            ["Pause large downloads, cloud backups, or other bandwidth-heavy activity."] = "Pausieren Sie große Downloads, Cloud-Sicherungen oder andere bandbreitenintensive Aktivitäten.",
            ["Restart the router and compare results at a different time."] = "Starten Sie den Router neu und vergleichen Sie die Ergebnisse zu einem anderen Zeitpunkt.",
            ["If Ethernet is also unstable, contact the network administrator or internet provider."] = "Wenn auch Ethernet instabil ist, wenden Sie sich an den Netzwerkadministrator oder Internetanbieter.",
            ["The short quality sample did not detect significant packet loss or latency."] = "Die kurze Qualitätsmessung hat keinen erheblichen Paketverlust und keine hohe Latenz erkannt.",

            ["Proxy configuration"] = "Proxy-Konfiguration",
            ["Reviews the current user’s Windows proxy settings."] = "Prüft die Windows-Proxy-Einstellungen des aktuellen Benutzers.",
            ["Manual proxy"] = "Manueller Proxy",
            ["Proxy server"] = "Proxyserver",
            ["Automatic configuration"] = "Automatische Konfiguration",
            ["A proxy is configured and the web connectivity check failed."] = "Ein Proxy ist konfiguriert und die Webverbindungsprüfung ist fehlgeschlagen.",
            ["The proxy may be required by your organization, or it may be outdated or unavailable."] = "Der Proxy wird möglicherweise von Ihrer Organisation benötigt oder ist veraltet beziehungsweise nicht verfügbar.",
            ["Confirm the proxy address with your network administrator."] = "Bestätigen Sie die Proxyadresse mit Ihrem Netzwerkadministrator.",
            ["Do not disable an organization-managed proxy without approval."] = "Deaktivieren Sie einen von der Organisation verwalteten Proxy nicht ohne Genehmigung.",
            ["If this is a personal computer and the proxy is unexpected, review Windows proxy settings."] = "Prüfen Sie auf einem privaten Computer die Windows-Proxy-Einstellungen, wenn der Proxy unerwartet ist.",
            ["The configured proxy did not prevent the connectivity test."] = "Der konfigurierte Proxy hat die Verbindungsprüfung nicht verhindert.",
            ["No explicit user proxy is enabled."] = "Es ist kein ausdrücklicher Benutzerproxy aktiviert.",
            ["A proxy configuration is present, and web access was not attributed to it."] = "Eine Proxy-Konfiguration ist vorhanden; der Webzugriff wurde dadurch nicht beeinträchtigt.",
            ["Windows is using direct or automatically discovered network settings."] = "Windows verwendet direkte oder automatisch erkannte Netzwerkeinstellungen.",

            ["No active network connection"] = "Keine aktive Netzwerkverbindung",
            ["Windows cannot see an active Ethernet or Wi-Fi adapter. The problem is between this computer and the local network."] = "Windows erkennt keinen aktiven Ethernet- oder WLAN-Adapter. Das Problem liegt zwischen diesem Computer und dem lokalen Netzwerk.",
            ["The computer has no valid IP configuration"] = "Der Computer besitzt keine gültige IP-Konfiguration",
            ["The network adapter is connected, but it did not receive a usable address from the network."] = "Der Netzwerkadapter ist verbunden, hat jedoch keine verwendbare Adresse vom Netzwerk erhalten.",
            ["The local gateway is unreachable"] = "Das lokale Gateway ist nicht erreichbar",
            ["The computer has an IP address but cannot communicate with the router or default gateway."] = "Der Computer besitzt eine IP-Adresse, kann aber nicht mit dem Router oder Standardgateway kommunizieren.",
            ["DNS is preventing internet access"] = "DNS verhindert den Internetzugriff",
            ["The internet is reachable by IP address, but domain names cannot be resolved."] = "Das Internet ist über IP-Adressen erreichbar, Domänennamen können jedoch nicht aufgelöst werden.",
            ["A sign-in page may be blocking access"] = "Eine Anmeldeseite blockiert möglicherweise den Zugriff",
            ["The network redirected the connectivity test or returned unexpected content, which commonly indicates a captive portal."] = "Das Netzwerk hat die Verbindungsprüfung umgeleitet oder unerwartete Inhalte geliefert. Dies deutet häufig auf ein Anmeldeportal hin.",
            ["The internet connection is unavailable"] = "Die Internetverbindung ist nicht verfügbar",
            ["The local network is reachable, but both direct internet and web connectivity checks failed."] = "Das lokale Netzwerk ist erreichbar, aber sowohl die direkte Internet- als auch die Webverbindungsprüfung sind fehlgeschlagen.",
            ["Web traffic is being blocked"] = "Der Webverkehr wird blockiert",
            ["The internet responds to direct network traffic, but the HTTP connectivity check failed. A proxy, firewall, VPN, or upstream policy may be responsible."] = "Das Internet antwortet auf direkten Netzwerkverkehr, aber die HTTP-Verbindungsprüfung ist fehlgeschlagen. Ursache können Proxy, Firewall, VPN oder eine übergeordnete Richtlinie sein.",
            ["Domain name resolution failed"] = "Die Namensauflösung ist fehlgeschlagen",
            ["NetCheck could not resolve a known internet hostname. The configured DNS service may be unavailable."] = "NetCheck konnte einen bekannten Internethostnamen nicht auflösen. Der konfigurierte DNS-Dienst ist möglicherweise nicht verfügbar.",
            ["The connection appears unstable"] = "Die Verbindung scheint instabil zu sein",
            ["Internet access is available, but packet loss or high latency was detected."] = "Internetzugriff ist verfügbar, jedoch wurden Paketverlust oder hohe Latenz erkannt.",
            ["Internet access works, with some warnings"] = "Der Internetzugriff funktioniert mit einigen Warnungen",
            ["A network problem was detected"] = "Ein Netzwerkproblem wurde erkannt",
            ["Your internet connection looks healthy"] = "Ihre Internetverbindung sieht einwandfrei aus",
            ["The adapter, local network, DNS, internet access, and connection quality checks completed successfully."] = "Die Prüfungen von Adapter, lokalem Netzwerk, DNS, Internetzugriff und Verbindungsqualität wurden erfolgreich abgeschlossen.",
            ["Diagnostic cancelled"] = "Diagnose abgebrochen",
            ["The diagnostic was stopped before every check completed."] = "Die Diagnose wurde beendet, bevor alle Prüfungen abgeschlossen waren.",
            ["Run a new diagnostic when you are ready."] = "Starten Sie eine neue Diagnose, wenn Sie bereit sind.",
            ["This check could not be completed."] = "Diese Prüfung konnte nicht abgeschlossen werden.",
            ["NetCheck handled an unexpected error and continued with the remaining checks."] = "NetCheck hat einen unerwarteten Fehler behandelt und die übrigen Prüfungen fortgesetzt.",
            ["Error type"] = "Fehlertyp",
            ["Error message"] = "Fehlermeldung",
            ["Run the diagnostic again."] = "Führen Sie die Diagnose erneut aus.",
            ["If the issue persists, export the report for technical support."] = "Wenn das Problem weiterhin besteht, exportieren Sie den Bericht für den technischen Support.",

            ["Clear DNS cache"] = "DNS-Cache leeren",
            ["Removes cached DNS answers so Windows requests fresh name-resolution data."] = "Entfernt zwischengespeicherte DNS-Antworten, damit Windows aktuelle Daten zur Namensauflösung anfordert.",
            ["Renew IP address"] = "IP-Adresse erneuern",
            ["Releases and requests a new DHCP address for connected network adapters."] = "Gibt die DHCP-Adresse verbundener Netzwerkadapter frei und fordert eine neue an.",
            ["Refresh local network cache"] = "Lokalen Netzwerkcache aktualisieren",
            ["Clears stale address mappings used to communicate with the local router."] = "Entfernt veraltete Adresszuordnungen für die Kommunikation mit dem lokalen Router.",
            ["Turn off the current proxy"] = "Aktuellen Proxy deaktivieren",
            ["Disables the current user’s manual proxy and automatic proxy script. Managed-network users should confirm this with their administrator first."] = "Deaktiviert den manuellen Proxy und das automatische Proxyskript des aktuellen Benutzers. Benutzer verwalteter Netzwerke sollten dies vorher mit ihrem Administrator abstimmen.",
            ["Reset Windows network sockets"] = "Windows-Netzwerksockets zurücksetzen",
            ["Restores the Windows Sockets catalog used by applications for network access."] = "Stellt den von Anwendungen für den Netzwerkzugriff verwendeten Windows-Sockets-Katalog wieder her.",
            ["Reset the TCP/IP stack"] = "TCP/IP-Stack zurücksetzen",
            ["Restores core Windows TCP/IP components to their default state."] = "Setzt zentrale Windows-TCP/IP-Komponenten auf den Standardzustand zurück.",
            ["Turn on Wi-Fi, reconnect Ethernet, or enable the adapter in Windows before trying again."] = "Aktivieren Sie WLAN, verbinden Sie Ethernet erneut oder aktivieren Sie den Adapter in Windows, bevor Sie es erneut versuchen.",
            ["The adapter uses manual or system-managed addressing. Review its IP, subnet, gateway, and DNS settings."] = "Der Adapter verwendet eine manuelle oder systemverwaltete Adressierung. Prüfen Sie IP-, Subnetz-, Gateway- und DNS-Einstellungen.",
            ["Open a browser and complete the network sign-in page, then run NetCheck again."] = "Öffnen Sie einen Browser, schließen Sie die Netzwerkanmeldung ab und führen Sie NetCheck erneut aus.",
            ["Connection quality problems need a signal, cable, router, or provider check and cannot be repaired safely by software."] = "Probleme mit der Verbindungsqualität erfordern eine Prüfung von Signal, Kabel, Router oder Anbieter und können nicht sicher per Software behoben werden.",

            ["The DNS cache was cleared."] = "Der DNS-Cache wurde geleert.",
            ["The local network address cache was refreshed."] = "Der lokale Netzwerkadresscache wurde aktualisiert.",
            ["The Windows Sockets catalog was reset."] = "Der Windows-Sockets-Katalog wurde zurückgesetzt.",
            ["The TCP/IP stack was reset."] = "Der TCP/IP-Stack wurde zurückgesetzt.",
            ["Windows could not apply this repair."] = "Windows konnte diese Reparatur nicht anwenden.",
            ["Windows requested a fresh DHCP address."] = "Windows hat eine neue DHCP-Adresse angefordert.",
            ["Windows could not renew the DHCP address."] = "Windows konnte die DHCP-Adresse nicht erneuern.",
            ["Windows reported that this repair did not complete."] = "Windows meldet, dass diese Reparatur nicht abgeschlossen wurde.",
            ["The current user proxy was turned off."] = "Der Proxy des aktuellen Benutzers wurde deaktiviert.",
            ["Start approved repairs"] = "Bestätigte Reparaturen starten",
            ["NetCheck could not start the approved repair plan."] = "NetCheck konnte den bestätigten Reparaturplan nicht starten.",
            ["Windows could not start the repair process."] = "Windows konnte den Reparaturprozess nicht starten.",
            ["The repair process did not finish within three minutes."] = "Der Reparaturprozess wurde nicht innerhalb von drei Minuten abgeschlossen.",
            ["Windows did not return a repair result."] = "Windows hat kein Reparaturergebnis zurückgegeben.",

            ["Dashboard"] = "Übersicht",
            ["History"] = "Verlauf",
            ["Settings"] = "Einstellungen",
            ["LANGUAGE"] = "SPRACHE",
            ["NAVIGATION"] = "NAVIGATION",
            ["Fixing issues…"] = "Probleme werden behoben…",
            ["Fix unavailable"] = "Behebung nicht verfügbar",
            ["Fix issue"] = "Problem beheben",
            ["Fix {0} issues"] = "{0} Probleme beheben",
            ["Review and apply the repair plan for the detected issues."] = "Prüfen und starten Sie den Reparaturplan für die erkannten Probleme.",
            ["This issue needs a manual, physical, router, or provider fix; no safe Windows repair matches the evidence."] = "Dieses Problem erfordert eine manuelle, physische, Router- oder Anbietermaßnahme; die Hinweise rechtfertigen keine sichere Windows-Reparatur.",
            ["Repair cancelled"] = "Reparatur abgebrochen",
            ["Approved repairs were applied"] = "Bestätigte Reparaturen wurden angewendet",
            ["Some repairs were applied"] = "Einige Reparaturen wurden angewendet",
            ["Repairs could not be applied"] = "Reparaturen konnten nicht angewendet werden",
            ["No changes were made because administrator approval was cancelled."] = "Es wurden keine Änderungen vorgenommen, da die Administratorbestätigung abgebrochen wurde.",
            ["Restart Windows to finish the network-stack repair, then run NetCheck again."] = "Starten Sie Windows neu, um die Reparatur des Netzwerkstacks abzuschließen, und führen Sie NetCheck anschließend erneut aus.",
            ["NetCheck applied the repair plan and checked the connection again."] = "NetCheck hat den Reparaturplan angewendet und die Verbindung erneut geprüft.",
            ["Windows applied part of the plan. Restart the computer before checking again."] = "Windows hat einen Teil des Plans angewendet. Starten Sie den Computer vor der erneuten Prüfung neu.",
            ["Windows applied part of the plan and NetCheck checked the connection again."] = "Windows hat einen Teil des Plans angewendet und NetCheck hat die Verbindung erneut geprüft.",
            ["Review the failed steps below and use the recommended manual next steps."] = "Prüfen Sie die fehlgeschlagenen Schritte und verwenden Sie die empfohlenen manuellen nächsten Schritte.",
            ["Applying approved repairs"] = "Bestätigte Reparaturen werden angewendet",
            ["Checking your connection"] = "Ihre Verbindung wird geprüft",
            ["Ready to diagnose your network"] = "Bereit für die Netzwerkdiagnose",
            ["Windows may ask for administrator approval. NetCheck will only run the repairs shown in the confirmation."] = "Windows fordert möglicherweise eine Administratorbestätigung an. NetCheck führt ausschließlich die in der Bestätigung angezeigten Reparaturen aus.",
            ["Preparing network checks…"] = "Netzwerkprüfungen werden vorbereitet…",
            ["Running {0}…"] = "{0} wird ausgeführt…",
            ["NetCheck will test the adapter, local network, DNS, internet access, and connection quality."] = "NetCheck prüft Adapter, lokales Netzwerk, DNS, Internetzugriff und Verbindungsqualität.",
            ["Completed {0} in {1:0.0} seconds"] = "Abgeschlossen am {0} in {1:0.0} Sekunden",
            ["{0} complete"] = "{0} abgeschlossen",
            ["Completed in {0:0} ms"] = "Abgeschlossen in {0:0} ms",
            ["{0:0.0} seconds"] = "{0:0.0} Sekunden",
            ["NetCheck could not finish"] = "NetCheck konnte die Diagnose nicht abschließen",
            ["An unexpected error interrupted the diagnostic. No system settings were changed. Please try again."] = "Ein unerwarteter Fehler hat die Diagnose unterbrochen. Es wurden keine Systemeinstellungen geändert. Versuchen Sie es erneut.",
            ["NetCheck can try these repairs:"] = "NetCheck kann diese Reparaturen versuchen:",
            ["Windows will ask for administrator approval."] = "Windows fordert eine Administratorbestätigung an.",
            ["One or more repairs require a Windows restart."] = "Mindestens eine Reparatur erfordert einen Windows-Neustart.",
            ["Only the listed changes will be made. Continue?"] = "Es werden ausschließlich die aufgeführten Änderungen vorgenommen. Fortfahren?",
            ["Fix detected network issues?"] = "Erkannte Netzwerkprobleme beheben?",
            ["Report exported"] = "Bericht exportiert",
            ["The diagnostic report was saved to:\n{0}"] = "Der Diagnosebericht wurde hier gespeichert:\n{0}",
            ["Export failed"] = "Export fehlgeschlagen",
            ["Copy failed"] = "Kopieren fehlgeschlagen",
            ["Windows could not access the clipboard. Please try again."] = "Windows konnte nicht auf die Zwischenablage zugreifen. Versuchen Sie es erneut.",
            ["Unexpected error"] = "Unerwarteter Fehler",
            ["NetCheck handled an unexpected error. Please try again."] = "NetCheck hat einen unerwarteten Fehler behandelt. Versuchen Sie es erneut.",
            ["History unavailable"] = "Verlauf nicht verfügbar",
            ["NetCheck could not load the saved diagnostic history."] = "NetCheck konnte den gespeicherten Diagnoseverlauf nicht laden.",
            ["Clear diagnostic history?"] = "Diagnoseverlauf löschen?",
            ["This permanently removes the diagnostic reports saved by NetCheck on this computer."] = "Dadurch werden die von NetCheck auf diesem Computer gespeicherten Diagnoseberichte dauerhaft gelöscht.",
            ["Operation failed"] = "Vorgang fehlgeschlagen",
            ["Settings could not be saved"] = "Einstellungen konnten nicht gespeichert werden",
            ["Settings saved. They will be used for the next diagnostic."] = "Die Einstellungen wurden gespeichert und werden für die nächste Diagnose verwendet.",
            ["Enter a valid DNS test hostname without spaces."] = "Geben Sie einen gültigen DNS-Testhostnamen ohne Leerzeichen ein.",
            ["Enter one or more valid IP addresses for internet ping targets."] = "Geben Sie mindestens eine gültige IP-Adresse als Internet-Pingziel ein.",
            ["Enter a valid HTTP or HTTPS connectivity URL."] = "Geben Sie eine gültige HTTP- oder HTTPS-URL für die Verbindungsprüfung ein.",
            ["Ping timeout must be between 500 and 5000 milliseconds."] = "Das Ping-Zeitlimit muss zwischen 500 und 5000 Millisekunden liegen.",
            ["Stability samples must be between 3 and 20."] = "Die Anzahl der Stabilitätsmessungen muss zwischen 3 und 20 liegen.",
            ["The packet-loss warning threshold must be between 1 and 100 percent."] = "Die Warnschwelle für Paketverlust muss zwischen 1 und 100 Prozent liegen.",
            ["The latency warning threshold must be between 10 and 2000 milliseconds."] = "Die Latenzwarnschwelle muss zwischen 10 und 2000 Millisekunden liegen.",
            ["Defaults restored in the form. Choose Save settings to apply them."] = "Die Standardwerte wurden im Formular wiederhergestellt. Wählen Sie ›Einstellungen speichern‹, um sie anzuwenden.",
            ["Export NetCheck report"] = "NetCheck-Bericht exportieren",
            ["Web report (*.html)"] = "Webbericht (*.html)",
            ["JSON data (*.json)"] = "JSON-Daten (*.json)",
            ["Plain text (*.txt)"] = "Textdatei (*.txt)",
            ["NetCheck encountered an unexpected error. The error was logged locally. Repairs never run without your approval."] = "In NetCheck ist ein unerwarteter Fehler aufgetreten. Der Fehler wurde lokal protokolliert. Reparaturen werden niemals ohne Ihre Zustimmung ausgeführt.",
            ["NetCheck error"] = "NetCheck-Fehler",

            ["NETCHECK DIAGNOSTIC REPORT"] = "NETCHECK-DIAGNOSEBERICHT",
            ["Report ID"] = "Berichts-ID",
            ["Completed"] = "Abgeschlossen",
            ["Computer"] = "Computer",
            ["Outcome"] = "Ergebnis",
            ["Diagnosis"] = "Diagnose",
            ["NETWORK"] = "NETZWERK",
            ["IP address"] = "IP-Adresse",
            ["CHECKS"] = "PRÜFUNGEN",
            ["NetCheck report"] = "NetCheck-Bericht",
            ["Report"] = "Bericht",
            ["Recommended actions"] = "Empfohlene Maßnahmen",
            ["NetCheck can export HTML, JSON, or plain-text reports."] = "NetCheck kann HTML-, JSON- oder Textberichte exportieren.",
            ["Healthy"] = "Fehlerfrei",
            ["Attention"] = "Achtung",
            ["Problem"] = "Problem",
            ["Cancelled"] = "Abgebrochen",
            ["Warning"] = "Warnung",
            ["Passed"] = "Bestanden",
            ["Failed"] = "Fehlgeschlagen",
            ["Skipped"] = "Übersprungen",
            ["Running"] = "Läuft",

            ["PASSED"] = "BESTANDEN",
            ["ATTENTION"] = "ACHTUNG",
            ["FAILED"] = "FEHLGESCHLAGEN",
            ["SKIPPED"] = "ÜBERSPRUNGEN",
            ["RUNNING"] = "LÄUFT",
            ["PENDING"] = "AUSSTEHEND"
        });

    private string _language = "en";

    public LocalizationService()
    {
        Current = this;
    }

    public static LocalizationService? Current { get; private set; }

    public event EventHandler? LanguageChanged;

    public string Language => _language;

    public CultureInfo Culture => IsGerman ? GermanCulture : EnglishCulture;

    public bool IsGerman => string.Equals(_language, "de", StringComparison.Ordinal);

    public void SetLanguage(string language)
    {
        var normalized = string.Equals(language, "de", StringComparison.OrdinalIgnoreCase) ? "de" : "en";
        if (string.Equals(_language, normalized, StringComparison.Ordinal))
        {
            EnsureResourceDictionary(normalized);
            ApplyCulture();
            return;
        }

        _language = normalized;
        EnsureResourceDictionary(normalized);
        ApplyCulture();
        OnPropertiesChanged(nameof(Language), nameof(Culture), nameof(IsGerman));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Translate(string source)
    {
        if (!IsGerman || string.IsNullOrEmpty(source))
        {
            return source;
        }

        if (GermanTranslations.TryGetValue(source, out var translated))
        {
            return translated;
        }

        return TranslatePattern(source);
    }

    public string Format(string sourceFormat, params object?[] arguments) =>
        string.Format(Culture, Translate(sourceFormat), arguments);

    private string TranslatePattern(string source)
    {
        var match = ConnectedPattern().Match(source);
        if (match.Success)
        {
            return $"{match.Groups[1].Value} ist verbunden.";
        }

        match = AutomaticAddressPattern().Match(source);
        if (match.Success)
        {
            return $"Windows hat die automatische Adresse {match.Groups[1].Value} zugewiesen.";
        }

        match = ValidAddressPattern().Match(source);
        if (match.Success)
        {
            return $"Der Adapter besitzt eine gültige Adresse ({match.Groups[1].Value}).";
        }

        match = GatewayResponsePattern().Match(source);
        if (match.Success)
        {
            return $"Das Gateway {match.Groups[1].Value} hat nach {match.Groups[2].Value} ms geantwortet.";
        }

        match = InternetResponsePattern().Match(source);
        if (match.Success)
        {
            return $"Das Internet hat nach {match.Groups[1].Value} ms geantwortet.";
        }

        match = ResolvedSuccessfullyPattern().Match(source);
        if (match.Success)
        {
            return $"{match.Groups[1].Value} wurde erfolgreich aufgelöst.";
        }

        match = HostCouldNotResolvePattern().Match(source);
        if (match.Success)
        {
            return $"Der Hostname {match.Groups[1].Value} konnte nicht aufgelöst werden.";
        }

        match = RedirectPattern().Match(source);
        if (match.Success)
        {
            return $"Das Netzwerk hat die Anfrage an {match.Groups[1].Value} umgeleitet.";
        }

        match = HttpErrorPattern().Match(source);
        if (match.Success)
        {
            return $"Der Server hat HTTP {match.Groups[1].Value} zurückgegeben.";
        }

        match = RepairExitCodePattern().Match(source);
        if (match.Success)
        {
            return $"Der Reparaturprozess wurde mit Code {match.Groups[1].Value} beendet.";
        }

        match = DegradedPattern().Match(source);
        if (match.Success)
        {
            return $"Die Verbindungsqualität ist beeinträchtigt ({match.Groups[1].Value}% Verlust, {match.Groups[2].Value} ms durchschnittliche Latenz).";
        }

        match = StablePattern().Match(source);
        if (match.Success)
        {
            return $"Die Verbindung ist stabil ({match.Groups[1].Value} ms im Durchschnitt, {match.Groups[2].Value}% Verlust).";
        }

        if (source.Contains("No reply", StringComparison.Ordinal))
        {
            return source.Replace("No reply", "Keine Antwort", StringComparison.Ordinal);
        }

        return source;
    }

    private void EnsureResourceDictionary(string language)
    {
        if (Application.Current?.Resources is not { } resources)
        {
            return;
        }

        var source = new Uri($"Resources/Strings.{language}.xaml", UriKind.Relative);
        var dictionaries = resources.MergedDictionaries;
        var currentIndex = -1;
        for (var index = 0; index < dictionaries.Count; index++)
        {
            if (dictionaries[index].Source?.OriginalString.Contains("Resources/Strings.", StringComparison.OrdinalIgnoreCase) == true)
            {
                currentIndex = index;
                break;
            }
        }

        var replacement = new ResourceDictionary { Source = source };
        if (currentIndex >= 0)
        {
            dictionaries[currentIndex] = replacement;
        }
        else
        {
            dictionaries.Insert(0, replacement);
        }
    }

    private void ApplyCulture()
    {
        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = Culture;
        CultureInfo.DefaultThreadCurrentCulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
    }

    [GeneratedRegex("^(.+) is connected\\.$", RegexOptions.CultureInvariant)]
    private static partial Regex ConnectedPattern();

    [GeneratedRegex("^Windows assigned the automatic address (.+)\\.$", RegexOptions.CultureInvariant)]
    private static partial Regex AutomaticAddressPattern();

    [GeneratedRegex("^The adapter has a valid address \\((.+)\\)\\.$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidAddressPattern();

    [GeneratedRegex("^The gateway (.+) responded in ([0-9.,]+) ms\\.$", RegexOptions.CultureInvariant)]
    private static partial Regex GatewayResponsePattern();

    [GeneratedRegex("^The internet responded in ([0-9.,]+) ms\\.$", RegexOptions.CultureInvariant)]
    private static partial Regex InternetResponsePattern();

    [GeneratedRegex("^(.+) resolved successfully\\.$", RegexOptions.CultureInvariant)]
    private static partial Regex ResolvedSuccessfullyPattern();

    [GeneratedRegex("^The hostname (.+) could not be resolved\\.$", RegexOptions.CultureInvariant)]
    private static partial Regex HostCouldNotResolvePattern();

    [GeneratedRegex("^The network redirected the request to (.+)\\.$", RegexOptions.CultureInvariant)]
    private static partial Regex RedirectPattern();

    [GeneratedRegex("^The server returned HTTP (.+)\\.$", RegexOptions.CultureInvariant)]
    private static partial Regex HttpErrorPattern();

    [GeneratedRegex("^The repair process ended with code (.+)\\.$", RegexOptions.CultureInvariant)]
    private static partial Regex RepairExitCodePattern();

    [GeneratedRegex("^Connection quality is degraded \\(([0-9.,]+)% loss, ([0-9.,]+) ms average latency\\)\\.$", RegexOptions.CultureInvariant)]
    private static partial Regex DegradedPattern();

    [GeneratedRegex("^The connection is stable \\(([0-9.,]+) ms average, ([0-9.,]+)% loss\\)\\.$", RegexOptions.CultureInvariant)]
    private static partial Regex StablePattern();
}

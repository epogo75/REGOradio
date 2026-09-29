using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace REGOradio.Fernbedienung;

/// <summary>
/// Unter welcher Adresse das Handy das Notebook erreicht.
///
/// **Nicht die erste Adresse, die Windows nennt.** Auf einem Entwicklerrechner
/// hängen neben dem WLAN noch virtuelle Karten für WSL und Hyper-V
/// („vEthernet"), dazu vielleicht ein VPN. Deren Adressen sieht das Handy nie;
/// steht eine davon im QR-Code, lädt die Seite endlos. Genommen wird deshalb
/// eine Karte, die ein Gateway hat -- also wirklich in einem Netz hängt --, und
/// WLAN vor Kabel, weil das Handy im WLAN ist.
/// </summary>
public static class Adresse
{
    public sealed record Kandidat(string Name, string Beschreibung, NetworkInterfaceType Art, string Ipv4, bool HatGateway);

    public static string? Finden()
    {
        var kandidaten = new List<Kandidat>();
        try
        {
            foreach (var karte in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (karte.OperationalStatus != OperationalStatus.Up) continue;
                var eigenschaften = karte.GetIPProperties();
                var ipv4 = eigenschaften.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address;
                if (ipv4 is null) continue;
                var gateway = eigenschaften.GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
                kandidaten.Add(new Kandidat(karte.Name, karte.Description, karte.NetworkInterfaceType, ipv4.ToString(), gateway));
            }
        }
        catch (NetworkInformationException)
        {
            return null;
        }
        return Waehlen(kandidaten);
    }

    /// <summary>Reine Auswahl -- geprüft in `AdressePruefung`, ohne echte Netzkarten.</summary>
    public static string? Waehlen(IEnumerable<Kandidat> kandidaten) => kandidaten
        .Where(k => k.Art != NetworkInterfaceType.Loopback)
        .Where(k => !k.Ipv4.StartsWith("169.254.", StringComparison.Ordinal))   // keine Adresse bekommen
        .Where(k => !IstVirtuell(k))
        .OrderByDescending(k => k.HatGateway)
        .ThenByDescending(k => k.Art == NetworkInterfaceType.Wireless80211)
        .ThenByDescending(k => k.Art == NetworkInterfaceType.Ethernet)
        .Select(k => k.Ipv4)
        .FirstOrDefault();

    private static bool IstVirtuell(Kandidat k)
    {
        var text = $"{k.Name} {k.Beschreibung}";
        return text.Contains("vEthernet", StringComparison.OrdinalIgnoreCase)
               || text.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase)
               || text.Contains("WSL", StringComparison.OrdinalIgnoreCase)
               || text.Contains("VirtualBox", StringComparison.OrdinalIgnoreCase)
               || text.Contains("VMware", StringComparison.OrdinalIgnoreCase);
    }
}

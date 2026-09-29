using System.Diagnostics;
using System.Text.RegularExpressions;

namespace REGOradio.Netz;

/// <summary>In welchem Funknetz der Rechner hängt.</summary>
public sealed record Netzlage(string Netz, int Staerke, bool Verbunden)
{
    public string Anzeige => Verbunden
        ? (Staerke > 0 ? $"{Netz} · {Staerke} %" : Netz)
        : "Kein WLAN";
}

/// <summary>
/// Das WLAN, in dem der Rechner steckt -- abgelesen über `netsh`.
///
/// **Warum ein Unterprozess und keine Bibliothek?** Der Name des Netzes ist eine
/// Auskunft, kein Steuerbefehl: Wir verbinden nichts, wir zeigen nur an, wo das
/// Notebook hängt. Dafür ein WinRT-Ziel und eine SDK-Abhängigkeit ins Projekt zu
/// holen wäre viel für eine Zeile Text. `netsh` gehört zu Windows.
///
/// **Die Ausgabe ist übersetzt.** Feldnamen und Werte kommen in der Sprache von
/// Windows: „Status : Verbunden" auf Deutsch, „State : connected" auf Englisch.
/// Gelesen werden deshalb nur Felder, die in beiden Sprachen gleich heißen
/// („SSID", „Signal") oder deren Wert sich eindeutig erkennen lässt.
///
/// **SSID ist nicht BSSID.** Wer auf „enthält SSID" prüft, trifft auch die
/// Adresse des Zugangspunkts und zeigt eine MAC-Adresse als Netznamen an.
/// Verglichen wird deshalb der ganze Feldname.
/// </summary>
public static class Wlan
{
    public static Netzlage Lesen()
    {
        try
        {
            return AusAusgabe(NetshFragen());
        }
        catch (Exception)
        {
            // Kein WLAN-Dienst, kein Adapter, `netsh` fehlt: Die Anzeige sagt
            // dann „Kein WLAN" -- der Ton hängt nicht davon ab.
            return new Netzlage("", 0, false);
        }
    }

    private static string NetshFragen()
    {
        // ZEICHENSATZ: Eine Konsole antwortet im OEM-Zeichensatz (auf einem
        // deutschen Windows CP850), nicht in UTF-8. Ein WLAN namens
        // „Hotel-Gäste" käme sonst als „Hotel-GÃ¤ste" oder
        // „Hotel-G?ste" an -- je nachdem, wie man daneben liest. Und die
        // alten Codepages kennt .NET von sich aus gar nicht mehr; sie
        // nachzuladen verlangt ein zusätzliches Paket.
        //
        // Deshalb der Umweg über `cmd`: `chcp 65001` stellt die Konsole auf
        // UTF-8, und genau so lesen wir mit.
        var start = new ProcessStartInfo("cmd.exe", "/c chcp 65001>nul & netsh wlan show interfaces")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var prozess = Process.Start(start)
            ?? throw new InvalidOperationException("netsh ließ sich nicht starten.");
        var ausgabe = prozess.StandardOutput.ReadToEnd();
        prozess.WaitForExit(3000);
        return ausgabe;
    }

    /// <summary>
    /// Reine Funktion -- und damit die Stelle, an der geprüft wird, mit
    /// aufgezeichneten Ausgaben unter `tests/antworten/`.
    /// </summary>
    public static Netzlage AusAusgabe(string ausgabe)
    {
        var netz = "";
        var staerke = 0;
        var verbunden = false;

        foreach (var zeile in ausgabe.Split('\n'))
        {
            var doppelpunkt = zeile.IndexOf(':');
            if (doppelpunkt < 0) continue;
            var feld = zeile[..doppelpunkt].Trim();
            var wert = zeile[(doppelpunkt + 1)..].Trim();

            // BSSID ist die Adresse des Zugangspunkts, nicht der Netzname. Wer
            // nur auf „SSID" prüft, trifft sie mit und zeigt eine MAC-Adresse an.
            if (feld.Equals("SSID", StringComparison.OrdinalIgnoreCase) && netz.Length == 0)
            {
                netz = wert;
            }
            else if (feld.StartsWith("Signal", StringComparison.OrdinalIgnoreCase))
            {
                var zahl = Regex.Match(wert, @"\d+");
                if (zahl.Success) staerke = int.Parse(zahl.Value);
            }
            else if (feld.Equals("Status", StringComparison.OrdinalIgnoreCase)
                     || feld.Equals("State", StringComparison.OrdinalIgnoreCase))
            {
                // „disconnected" enthält „connected", „getrennt" nicht
                // „verbunden" -- deshalb erst das Verneinte ausschließen.
                var aus = wert.Contains("disconnected", StringComparison.OrdinalIgnoreCase)
                          || wert.Contains("getrennt", StringComparison.OrdinalIgnoreCase);
                verbunden = !aus
                            && (wert.Contains("connected", StringComparison.OrdinalIgnoreCase)
                                || wert.Contains("verbunden", StringComparison.OrdinalIgnoreCase));
            }
        }

        // Steht ein Netzname da, hängt der Rechner drin -- auch wenn die
        // Statuszeile in einer Sprache kommt, die wir nicht erkennen.
        if (netz.Length > 0) verbunden = true;
        return new Netzlage(netz, staerke, verbunden);
    }
}

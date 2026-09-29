using System.Security.Cryptography;
using System.Text;

namespace REGOradio.Fernbedienung;

/// <summary>
/// Wer die Fernbedienung benutzen darf: wer die PIN kennt.
///
/// **Eine vierstellige PIN, kein Kennwort.** So ist es gewünscht, und für den
/// Zweck reicht es: Wer im selben WLAN ist und die PIN am Notebook abliest,
/// darf Sender wechseln und leiser drehen. Mehr kann die Fernbedienung nicht --
/// keine Einstellungen, keine Dateien, nichts, was sich nicht mit einem Tipp
/// am Notebook zurückdrehen ließe.
///
/// **Gegen Durchprobieren:** 10 000 Möglichkeiten sind in Sekunden durch. Nach
/// fünf Fehlversuchen von einer Adresse ist deshalb eine Minute Ruhe. Damit
/// dauert das Durchprobieren im Mittel über 16 Stunden -- im Hotel-WLAN, wo
/// Fremde im selben Netz sind, der Unterschied, auf den es ankommt.
///
/// **Die Anmeldung überlebt Neustarts.** Das Handy bekommt ein Zeichen, das
/// aus einem geheimen Schlüssel und der PIN gerechnet wird. Das Programm muss
/// sich dafür nichts merken außer dem Schlüssel; eine neue PIN macht alle
/// alten Anmeldungen auf einen Schlag ungültig.
/// </summary>
public sealed class Zugang(Func<string> pin, Func<string> schluessel, Func<DateTime>? uhr = null)
{
    public const int Versuche = 5;
    public static readonly TimeSpan Sperre = TimeSpan.FromMinutes(1);

    private readonly Func<DateTime> _uhr = uhr ?? (() => DateTime.UtcNow);
    private readonly Dictionary<string, List<DateTime>> _fehlversuche = [];
    private readonly object _schloss = new();

    /// <summary>Eine neue PIN: vier Ziffern, aus dem Zufallsgenerator für Geheimnisse.</summary>
    public static string NeuePin() => RandomNumberGenerator.GetInt32(0, 10_000).ToString("D4");

    /// <summary>Ein neuer Schlüssel -- einmal je Einrichtung.</summary>
    public static string NeuerSchluessel() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    /// <summary>Das Zeichen, das ein angemeldetes Handy als Cookie trägt.</summary>
    public string Zeichen()
    {
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(schluessel()), Encoding.UTF8.GetBytes("REGOradio|" + pin()));
        return Convert.ToHexString(mac).ToLowerInvariant();
    }

    public bool ZeichenGilt(string? zeichen) =>
        zeichen is { Length: > 0 } && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(zeichen), Encoding.ASCII.GetBytes(Zeichen()));

    public enum Ergebnis { Angemeldet, Falsch, Gesperrt }

    /// <summary>Eine PIN prüfen -- mit Sperre nach zu vielen Fehlversuchen.</summary>
    public Ergebnis Anmelden(string absender, string? versuch)
    {
        lock (_schloss)
        {
            var jetzt = _uhr();
            var liste = _fehlversuche.TryGetValue(absender, out var bisher) ? bisher : [];
            liste.RemoveAll(zeit => jetzt - zeit > Sperre);
            if (liste.Count >= Versuche) return Ergebnis.Gesperrt;

            var richtig = versuch is { Length: 4 } && CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(versuch), Encoding.ASCII.GetBytes(pin()));
            if (richtig)
            {
                _fehlversuche.Remove(absender);
                return Ergebnis.Angemeldet;
            }
            liste.Add(jetzt);
            _fehlversuche[absender] = liste;
            return Ergebnis.Falsch;
        }
    }
}

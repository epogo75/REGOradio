using REGOradio.Modelle;

namespace REGOradio.Katalog;

/// <summary>
/// Welche Adressen probiert werden, wenn ein Sender nicht mehr spielt
/// (Bau 17). Reine Rechnung, geprüft in `SenderheilungPruefung`; das
/// Ausprobieren selbst steht in `Hauptfenster.Heilen.cs`.
///
/// **Warum.** Internetradios ziehen um: neuer Anbieter, neue Adresse. Im
/// Verzeichnis steht dann meist schon die neue, auf der Taste aber noch die
/// alte – und die Taste ist tot. Genau dafür wird die Kennung des
/// Verzeichnisses mitgespeichert (siehe `Sender`).
///
/// **Reihenfolge.** Zuerst derselbe Eintrag (gleiche Kennung) mit neuer
/// Adresse – das ist der Umzug. Dann Einträge mit genau demselben Namen, in
/// der Reihenfolge des Verzeichnisses (meistgewählt zuerst) – für einen
/// Eintrag, der gelöscht wurde, oder einen, dessen neue Adresse auch nicht
/// geht. Ähnliche Namen nicht: „SWR3" ist nicht „SWR3 Lounge". Höchstens
/// vier, damit ein wirklich verschwundener Sender nicht minutenlang sucht.
/// </summary>
public static class Senderheilung
{
    public const int Hoechstens = 4;

    public static List<Treffer> Kandidaten(Sender kaputt, IEnumerable<Treffer> nachKennung, IEnumerable<Treffer> nachName)
    {
        var alt = Normal(kaputt.Adresse);
        var liste = new List<Treffer>();
        var gesehen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { alt };

        void Dazu(Treffer t)
        {
            if (t.Adresse.Length == 0 || !gesehen.Add(Normal(t.Adresse))) return;
            liste.Add(t);
        }

        foreach (var t in nachKennung.Where(t => kaputt.Kennung.Length > 0 && t.Kennung == kaputt.Kennung)) Dazu(t);
        foreach (var t in nachName.Where(t => GleicherName(t.Name, kaputt.Name))) Dazu(t);
        return liste.Take(Hoechstens).ToList();
    }

    private static bool GleicherName(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Dieselbe Adresse, anders geschrieben, ist keine neue: Groß/klein beim
    /// Rechnernamen, ein Schrägstrich am Ende.
    /// </summary>
    private static string Normal(string adresse) => adresse.Trim().TrimEnd('/');

    /// <summary>
    /// Was von einem gelungenen Ersatz auf die Taste übergeht: die Adresse,
    /// dazu Kennung, Logo und Homepage, wo bisher keine standen. Name und
    /// Platz bleiben – die Taste soll so heißen wie bisher.
    /// </summary>
    public static void Uebernehmen(Sender sender, Treffer ersatz)
    {
        sender.Adresse = ersatz.Adresse;
        if (sender.Kennung.Length == 0) sender.Kennung = ersatz.Kennung;
        if (sender.Logo.Length == 0) sender.Logo = ersatz.Logo;
        if (sender.Homepage.Length == 0) sender.Homepage = ersatz.Homepage;
        if (ersatz.Codec.Length > 0) sender.Codec = ersatz.Codec;
        if (ersatz.Bitrate > 0) sender.Bitrate = ersatz.Bitrate;
    }
}

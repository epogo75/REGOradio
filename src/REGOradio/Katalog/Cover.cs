using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace REGOradio.Katalog;

/// <summary>
/// Ein Cover zum laufenden Lied finden.
///
/// **Warum das nötig ist.** Sender schicken ihren Titel mit („Milli Vanilli -
/// Girl You Know It's True"), aber fast nie ein Bild. Ein Cover gibt es also
/// nur, wenn man es zum Titel sucht.
///
/// **Gesucht wird bei der iTunes-Suche.** Kein Schlüssel, keine Anmeldung,
/// gute Trefferquote für Popmusik -- und genau ein Aufruf je Titel. Die
/// Bildadresse kommt als 100×100 und lässt sich auf 600×600 hochsetzen.
///
/// **Was nach draußen geht:** Interpret und Titel des laufenden Stücks, nicht
/// mehr. Und nur, wenn der Titel die Form „Interpret - Lied" hat; alles
/// andere ist Werbung, ein Sendungsname oder eine Durchsage. Im
/// Protokoll gesehen, was sonst bei einem fremden Dienst landet: „Kontakt zu
/// SWR3: info@swr3.de".
///
/// **Scheitert es, ist nichts kaputt.** Dann steht das Senderlogo da.
/// </summary>
public sealed class Cover(HttpClient? klient = null)
{
    private const string Suche = "https://itunes.apple.com/search";

    private readonly HttpClient _klient = klient ?? NeuerKlient();

    // Einmal je Titel fragen. Sender melden denselben Titel im Takt neu; ohne
    // Gedächtnis ginge bei jeder Meldung eine Anfrage raus.
    private readonly Dictionary<string, string> _gemerkt = new(StringComparer.OrdinalIgnoreCase);

    private static HttpClient NeuerKlient()
    {
        var klient = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        klient.DefaultRequestHeaders.UserAgent.ParseAdd(Bau.Kennung);
        return klient;
    }

    private static readonly Regex Beiwerk = new(
        @"\s*[\(\[](?:live|remaster(?:ed)?[^\)\]]*|\d{4}\s*remaster|radio edit|single version|album version|explicit|official.*?)[\)\]]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Trenner = new(@"\s+[-–—]\s+", RegexOptions.Compiled);

    /// <summary>
    /// „Interpret - Titel" auseinandernehmen. Fehlt der Trenner, gilt alles als
    /// Titel -- lieber ohne Interpret als einen falschen raten. Ein Bindestrich
    /// OHNE Leerzeichen trennt nicht: „Jean-Michel Jarre" ist ein Name.
    /// </summary>
    public static (string Interpret, string Titel) Zerlege(string icyTitel)
    {
        var teile = Trenner.Split(icyTitel.Trim(), 2);
        if (teile.Length == 2 && teile[0].Trim().Length > 0 && teile[1].Trim().Length > 0)
        {
            return (teile[0].Trim(), teile[1].Trim());
        }
        return ("", icyTitel.Trim());
    }

    /// <summary>Was in die Suche geht -- ohne das Beiwerk, das nichts trifft.</summary>
    public static string Suchbegriff(string interpret, string titel)
    {
        var sauber = Beiwerk.Replace($"{interpret} {titel}".Trim(), "");
        return Regex.Replace(sauber, @"\s+", " ").Trim();
    }

    /// <summary>
    /// Aus dem 100er-Vorschaubild ein 600er machen -- eine Eigenheit der
    /// iTunes-Suche. Trifft das Muster nicht, bleibt die Adresse: Ein kleines
    /// Bild ist besser als keines.
    /// </summary>
    public static string Gross(string adresse) =>
        Regex.Replace(adresse, @"/\d+x\d+bb\.(jpg|png)$", "/600x600bb.$1");

    private static readonly Regex Klammerzusatz = new(@"\s*[\(\[][^\)\]]*[\)\]]", RegexOptions.Compiled);

    /// <summary>
    /// Die Suchbegriffe, in der Reihenfolge, in der sie versucht werden.
    ///
    /// **Erst genau, dann ohne Klammerzusatz.** Radio Paradise meldete „Till
    /// Your Mind Is Shining (Bandcamp Version)" -- damit findet die Suche
    /// nichts, ohne den Zusatz das richtige Album. Das Beiwerk oben
    /// (Remaster, Radio Edit …) deckt nur bekannte Zusätze ab; Sender
    /// erfinden laufend neue. Ein zweiter Anlauf ohne jede Klammer fängt sie
    /// alle, ohne dass die Liste wächst.
    /// </summary>
    public static List<string> Suchbegriffe(string interpret, string titel)
    {
        var begriffe = new List<string>();
        var genau = Suchbegriff(interpret, titel);
        if (genau.Length >= 4) begriffe.Add(genau);
        var ohne = Suchbegriff(interpret, Klammerzusatz.Replace(titel, "").Trim());
        if (ohne.Length >= 4 && !begriffe.Contains(ohne, StringComparer.OrdinalIgnoreCase)) begriffe.Add(ohne);
        return begriffe;
    }

    /// <summary>Die Bildadresse zum ICY-Titel, oder leer.</summary>
    public async Task<string> AdresseSuchen(string icyTitel, CancellationToken abbruch = default)
    {
        var (interpret, titel) = Zerlege(icyTitel);
        if (interpret.Length == 0) return "";
        foreach (var begriff in Suchbegriffe(interpret, titel))
        {
            var adresse = await Frage(begriff, abbruch);
            if (adresse is null) return "";      // Netz weg: nicht weiter probieren
            if (adresse.Length > 0) return adresse;
        }
        return "";
    }

    /// <returns>Die Adresse, leer bei „nichts gefunden", null bei einem Netzfehler.</returns>
    private async Task<string?> Frage(string begriff, CancellationToken abbruch)
    {
        if (_gemerkt.TryGetValue(begriff, out var bekannt)) return bekannt;
        var adresse = "";
        try
        {
            var antwort = await _klient.GetFromJsonAsync<Antwort>(
                $"{Suche}?term={Uri.EscapeDataString(begriff)}&entity=song&limit=1&country=DE", abbruch);
            var treffer = antwort?.results?.FirstOrDefault();
            if (treffer?.artworkUrl100 is { Length: > 0 } klein) adresse = Gross(klein);
        }
        catch (Exception fehler) when (fehler is HttpRequestException or JsonException or TaskCanceledException)
        {
            if (abbruch.IsCancellationRequested) throw;
            // Kein Cover. Nicht merken: Beim nächsten Titel darf es wieder
            // versucht werden, vielleicht steht die Verbindung dann.
            return null;
        }
        _gemerkt[begriff] = adresse;
        return adresse;
    }

    private sealed record Antwort(List<Treffer>? results);

    private sealed record Treffer(string? artworkUrl100);
}

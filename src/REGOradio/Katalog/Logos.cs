using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

namespace REGOradio.Katalog;

/// <summary>
/// Senderlogos: einmal geholt, auf der Platte behalten.
///
/// **Warum ein Zwischenspeicher?** Die acht Stationstasten stehen bei jedem
/// Start da. Ohne Speicher würden acht Logos bei jedem Öffnen neu geladen --
/// im Hotel-WLAN ohne Anmeldung gar nicht, und dann stünden leere Kacheln da,
/// obwohl das Bild längst einmal angekommen war. Die Dateien liegen unter
/// `%APPDATA%\REGOradio\logos`, der Name ist ein Streuwert der Adresse.
///
/// **Im Zweifel kein Bild.** Logos im Senderverzeichnis sind von sehr
/// unterschiedlicher Güte: tote Adressen, SVG (das WPF nicht zeichnet),
/// Symbole in 16 Pixeln, HTML-Fehlerseiten statt Bildern. Jeder dieser Fälle
/// ergibt `null`, und die Taste zeigt den Namen allein -- das ist vollständig,
/// kein Fehler.
/// </summary>
public sealed class Logos
{
    private readonly HttpClient _klient;
    private readonly string _verzeichnis;

    public Logos(string? verzeichnis = null, HttpClient? klient = null)
    {
        _verzeichnis = verzeichnis ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "REGOradio", "logos");
        Directory.CreateDirectory(_verzeichnis);

        // Entpacken, was gepackt kommt: Homepages schicken ihr HTML fast immer
        // mit gzip oder brotli, und ungepackt gelesen steht dort Zeichensalat.
        _klient = klient ?? new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        })
        {
            // Ein Logo ist Zierde. Wer länger als fünf Sekunden braucht, bekommt
            // keine zweite Chance in dieser Sitzung -- sonst stauen sich bei
            // einem langsamen Netz die Abrufe hinter dem Ton.
            Timeout = TimeSpan.FromSeconds(5),
        };
        _klient.DefaultRequestHeaders.UserAgent.ParseAdd(Bau.Kennung);
    }

    /// <summary>
    /// Das Bild zu einer Logoadresse, oder null.
    ///
    /// Die Bytes kommen im Hintergrund, das Bild entsteht erst danach und wird
    /// eingefroren (`Freeze`): Nur ein eingefrorenes Bild darf WPF aus einem
    /// anderen Faden in die Oberfläche übernehmen.
    /// </summary>
    /// <param name="breite">
    /// Auf wie viele Pixel dekodiert wird. 192 für Logos auf Tasten; 600 für
    /// ein Cover, das im Vollbild den halben Schirm füllt.
    /// </param>
    /// <param name="mindestKante">
    /// Kleinere Bilder gelten als keines. Für die Ersatzquellen: Ein Favicon
    /// in 16 Pixeln auf eine Taste gezogen ist ein Fleck, das Kürzel ist
    /// besser.
    /// </param>
    public async Task<BitmapImage?> Holen(string adresse, int breite = 192, int mindestKante = 0)
    {
        if (!Brauchbar(adresse)) return null;

        var datei = Path.Combine(_verzeichnis, Schluessel(adresse));
        var fehlt = datei + ".fehlt";
        byte[]? bytes = null;
        try
        {
            if (File.Exists(datei))
            {
                bytes = await File.ReadAllBytesAsync(datei);
            }
            else
            {
                // Was der Server neulich nicht hatte, hat er heute auch nicht.
                // Ohne diese Marke fragte jeder Start bei jedem Sender ohne Logo
                // alle Ersatzquellen neu ab.
                if (File.Exists(fehlt) && DateTime.UtcNow - File.GetLastWriteTimeUtc(fehlt) < Fehltdauer) return null;

                using var antwort = await _klient.GetAsync(adresse);
                // Eine Fehlerseite als „Logo" ist häufiger, als man denkt: Der
                // Server antwortet 200 und schickt HTML. Nur Bilder behalten.
                var typ = antwort.Content.Headers.ContentType?.MediaType ?? "";
                if (!antwort.IsSuccessStatusCode
                    || (typ.Length > 0 && !typ.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    || typ.Contains("svg", StringComparison.OrdinalIgnoreCase))
                {
                    // Nur eine ANTWORT wird gemerkt. Ein Zeitablauf im Hotel-WLAN
                    // ist keine Auskunft über den Sender.
                    MarkeSetzen(fehlt);
                    return null;
                }

                bytes = await antwort.Content.ReadAsByteArrayAsync();
                if (bytes.Length is 0 or > 2_000_000) return null;
                await File.WriteAllBytesAsync(datei, bytes);
            }
            var bild = Bild(bytes, breite, mindestKante);
            if (bild is null) MarkeSetzen(fehlt);
            return bild;
        }
        catch (Exception fehler) when (fehler is HttpRequestException or TaskCanceledException)
        {
            // Kein Netz, Zeitablauf: keine Auskunft über den Sender.
            Interlocked.Increment(ref _netzfehler);
            return null;
        }
        catch (Exception)
        {
            // Kaputte Datei im Speicher? Weg damit, beim nächsten Mal wird neu
            // geholt. Ein einmal kaputt gespeichertes Logo bliebe sonst für
            // immer kaputt.
            if (bytes is not null) TryLoeschen(datei);
            return null;
        }
    }

    /// <summary>
    /// Die gespeicherten Bytes zu einer Adresse, oder null – fürs Sichern der
    /// Stationstasten (Bau 19). Fragt nie das Netz.
    /// </summary>
    public byte[]? Gespeichert(string adresse)
    {
        if (!Brauchbar(adresse)) return null;
        var datei = Path.Combine(_verzeichnis, Schluessel(adresse));
        try { return File.Exists(datei) ? File.ReadAllBytes(datei) : null; }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    /// <summary>
    /// Bytes aus einer Sicherung in den Speicher legen, als wären sie
    /// geholt worden. Was dort schon liegt, bleibt; eine Fehlanzeige für die
    /// Adresse fällt weg.
    /// </summary>
    public void Vorlegen(string adresse, byte[] bytes)
    {
        if (!Brauchbar(adresse) || bytes.Length is 0 or > 2_000_000) return;
        var datei = Path.Combine(_verzeichnis, Schluessel(adresse));
        try
        {
            if (!File.Exists(datei)) File.WriteAllBytes(datei, bytes);
            TryLoeschen(datei + ".fehlt");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>Wie lange ein „hat kein Bild" gilt, bevor wieder gefragt wird.</summary>
    private static readonly TimeSpan Fehltdauer = TimeSpan.FromDays(7);

    /// <summary>
    /// Das Logo eines Senders: das aus dem Verzeichnis, sonst eines von seiner
    /// Homepage (Bau 15).
    ///
    /// **Warum.** Gemeldet: streborn zeigt bei Schwarzwaldradio ein Bild,
    /// REGOradio nur das Kürzel. Der Eintrag im Verzeichnis hat kein Logo,
    /// wohl aber eine Homepage. Die Idee – nicht der Code – stammt aus
    /// streborn (`ResolveStationLogo`, MIT); REGOradio liest zusätzlich, was
    /// die Homepage selbst als Symbol angibt, und nimmt das größte – siehe
    /// `Waehlen`. Bei Schwarzwaldradio sind das 512 Pixel statt der 32 von
    /// DuckDuckGo.
    /// </summary>
    public async Task<BitmapImage?> HolenFuer(string logo, string homepage, int breite = 192)
    {
        // WELCHE Adresse gilt, wird je Sender einmal ausgesucht, auch wenn
        // mehrere Stellen zugleich fragen: Das Feld „Läuft" lädt sein Logo bei
        // jeder Meldung des Abspielers neu, und ohne dieses Gedächtnis gingen
        // die Ersatzanfragen dann mehrfach parallel hinaus.
        var schluessel = logo + "\u001f" + homepage;
        var wahl = _wahl.GetOrAdd(schluessel, _ => Waehlen(logo, homepage));
        var adresse = await wahl;
        if (adresse is null)
        {
            // Nichts gefunden – vielleicht nur kein Netz. Beim nächsten Mal
            // wieder suchen; was wirklich fehlt, steht als Marke auf der Platte
            // und kostet dann keine Anfrage.
            _wahl.TryRemove(new KeyValuePair<string, Task<string?>>(schluessel, wahl));
            return null;
        }
        return await Holen(adresse, breite);
    }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task<string?>> _wahl = new();
    private int _netzfehler;

    /// <summary>
    /// Die Adresse, die `HolenFuer` für diesen Sender gefunden hat – für das
    /// Handy, das Logos selbst lädt und sonst nur die Adresse aus dem
    /// Verzeichnis bekäme. Ist noch nichts gefunden, die aus dem Verzeichnis.
    /// </summary>
    public string Adresse(string logo, string homepage) =>
        _wahl.TryGetValue(logo + "\u001f" + homepage, out var wahl) && wahl.IsCompletedSuccessfully && wahl.Result is { } gefunden
            ? gefunden
            : logo;

    /// <summary>
    /// Die Reihenfolge: das Logo aus dem Verzeichnis, dann die Symbole, die
    /// die Homepage selbst angibt, dann `Ersatzadressen`. Das Ergebnis steht
    /// danach als `.wahl` auf der Platte – die Homepage wird also nicht bei
    /// jedem Start neu geladen, und ein Sender ohne jedes Bild wird eine Woche
    /// lang nicht mehr abgefragt.
    /// </summary>
    private async Task<string?> Waehlen(string logo, string homepage)
    {
        if (await Holen(logo) is not null) return logo;
        if (Ersatzadressen(homepage).Count == 0) return null;

        var wahl = Path.Combine(_verzeichnis, Schluessel(logo + "\u001f" + homepage) + ".wahl");
        try
        {
            if (File.Exists(wahl))
            {
                var gemerkt = (await File.ReadAllTextAsync(wahl)).Trim();
                if (gemerkt.Length == 0 && DateTime.UtcNow - File.GetLastWriteTimeUtc(wahl) < Fehltdauer) return null;
                if (gemerkt.Length > 0 && await Holen(gemerkt, mindestKante: 32) is not null) return gemerkt;
            }
        }
        catch (IOException) { }

        var netzVorher = Volatile.Read(ref _netzfehler);
        var kandidaten = (await SymboleDerHomepage(homepage)).Concat(Ersatzadressen(homepage)).Distinct().ToList();
        foreach (var ersatz in kandidaten)
        {
            if (await Holen(ersatz, mindestKante: 32) is null) continue;
            // Nur merken, wenn unterwegs nichts am Netz scheiterte. Sonst
            // hielte ein einziger Zeitablauf bei der Homepage das kleine Bild
            // von DuckDuckGo für immer fest, obwohl die Homepage ein großes hat.
            if (Volatile.Read(ref _netzfehler) == netzVorher) Merken(wahl, ersatz);
            return ersatz;
        }
        // „Gibt es nicht" nur merken, wenn wirklich jeder geantwortet hat.
        if (Volatile.Read(ref _netzfehler) == netzVorher) Merken(wahl, "");
        return null;
    }

    private static void Merken(string datei, string inhalt)
    {
        try { File.WriteAllText(datei, inhalt); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Die Symbole, die eine Homepage im Kopf angibt (`link rel="icon"`,
    /// `apple-touch-icon`, `msapplication-TileImage`). Gelesen werden höchstens
    /// die ersten 512 KB und nur bis `&lt;/head&gt;`.
    /// </summary>
    private async Task<List<string>> SymboleDerHomepage(string homepage)
    {
        try
        {
            using var anfrage = new HttpRequestMessage(HttpMethod.Get, homepage.Trim());
            anfrage.Headers.Accept.ParseAdd("text/html");
            using var antwort = await _klient.SendAsync(anfrage, HttpCompletionOption.ResponseHeadersRead);
            var typ = antwort.Content.Headers.ContentType?.MediaType ?? "";
            if (!antwort.IsSuccessStatusCode || !typ.Contains("html", StringComparison.OrdinalIgnoreCase)) return [];

            await using var strom = await antwort.Content.ReadAsStreamAsync();
            using var leser = new StreamReader(strom, Encoding.UTF8);
            var puffer = new char[512 * 1024];
            var gelesen = await leser.ReadBlockAsync(puffer, 0, puffer.Length);
            var html = new string(puffer, 0, gelesen);
            var kopfende = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
            if (kopfende > 0) html = html[..kopfende];

            return SymboleAusHtml(html, antwort.RequestMessage?.RequestUri ?? new Uri(homepage.Trim()));
        }
        catch (Exception fehler) when (fehler is HttpRequestException or TaskCanceledException)
        {
            Interlocked.Increment(ref _netzfehler);
            return [];
        }
        catch (Exception fehler) when (fehler is UriFormatException or IOException or InvalidOperationException)
        {
            return [];
        }
    }

    private static readonly Regex Verweis = new(@"<(link|meta)\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Merkmal = new(@"([a-zA-Z\-:]+)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.Compiled);

    /// <summary>
    /// Aus dem Kopf einer Homepage die Bildadressen, das größte zuerst. Reine
    /// Funktion, geprüft in `LogoPruefung`.
    ///
    /// Größe: aus `sizes`, sonst aus einer Zahl wie „512x512" in der Adresse,
    /// sonst 180 für ein apple-touch-icon (die übliche Größe) und 0 für ein
    /// schlichtes Favicon. SVG fällt weg – WPF zeichnet es nicht. Ob ein Bild
    /// wirklich so groß ist, prüft danach `Holen` mit seiner Mindestkante.
    /// </summary>
    public static List<string> SymboleAusHtml(string html, Uri basis)
    {
        var funde = new List<(string Adresse, int Groesse)>();
        foreach (Match tag in Verweis.Matches(html))
        {
            var merkmale = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in Merkmal.Matches(tag.Value))
            {
                merkmale[m.Groups[1].Value] = System.Net.WebUtility.HtmlDecode(
                    m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Success ? m.Groups[3].Value : m.Groups[4].Value);
            }

            string? ziel;
            var rel = merkmale.GetValueOrDefault("rel", "").ToLowerInvariant();
            var istApple = rel.Contains("apple-touch-icon");
            if (tag.Groups[1].Value.Equals("link", StringComparison.OrdinalIgnoreCase)
                && rel.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(r => r is "icon" or "apple-touch-icon" or "apple-touch-icon-precomposed"))
            {
                ziel = merkmale.GetValueOrDefault("href");
            }
            else if (merkmale.GetValueOrDefault("name", "").Equals("msapplication-TileImage", StringComparison.OrdinalIgnoreCase))
            {
                ziel = merkmale.GetValueOrDefault("content");
            }
            else continue;

            if (string.IsNullOrWhiteSpace(ziel) || !Uri.TryCreate(basis, ziel.Trim(), out var uri)) continue;
            if (uri.Scheme is not ("http" or "https")) continue;
            var adresse = uri.ToString();
            if (!Brauchbar(adresse) || merkmale.GetValueOrDefault("type", "").Contains("svg", StringComparison.OrdinalIgnoreCase)) continue;

            var groesse = Groesse(merkmale.GetValueOrDefault("sizes", "")) ?? Groesse(uri.AbsolutePath) ?? (istApple ? 180 : 0);
            funde.Add((adresse, groesse));
        }
        return funde
            .OrderByDescending(f => f.Groesse)
            .Select(f => f.Adresse)
            .Distinct()
            .ToList();
    }

    private static readonly Regex Masse = new(@"(\d{2,4})x(\d{2,4})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static int? Groesse(string text)
    {
        var m = Masse.Matches(text).LastOrDefault();
        return m is null ? null : Math.Min(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value));
    }

    /// <summary>
    /// Wo ein Logo zu suchen ist, wenn weder das Verzeichnis noch der Kopf der
    /// Homepage eines liefern. Reine Funktion, geprüft in `LogoPruefung`.
    ///
    /// 1. `apple-touch-icon.png` an der üblichen Stelle: Das Bild, das ein
    ///    Handy für die Seite auf den Startbildschirm legt, meist 180 × 180.
    /// 2. Der Symboldienst von DuckDuckGo, mit und ohne `www.`. Er kennt fast
    ///    jede Seite, liefert aber oft nur 32 × 32. Deshalb erst danach. Er
    ///    erfährt dabei nur die Domain des Senders. Bei einer Seite, die er
    ///    nicht kennt, antwortet er 404 mit einem grauen Platzhalterbild –
    ///    `Holen` nimmt nur Antworten mit 200.
    ///
    /// **Nur die Homepage, nie die Stream-Adresse.** Streams liegen bei
    /// Anbietern wie streamonkey oder streamtheworld, und deren Symbol wäre bei
    /// hundert Sendern dasselbe falsche.
    /// </summary>
    public static List<string> Ersatzadressen(string homepage)
    {
        var liste = new List<string>();
        if (!Uri.TryCreate(homepage.Trim(), UriKind.Absolute, out var uri)) return liste;
        if (uri.Scheme is not ("http" or "https") || uri.HostNameType != UriHostNameType.Dns) return liste;

        var host = uri.Host.ToLowerInvariant();
        var ohneWww = host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
        if (!ohneWww.Contains('.')) return liste;

        liste.Add($"https://{host}/apple-touch-icon.png");
        foreach (var name in new[] { host, ohneWww, "www." + ohneWww }.Distinct())
        {
            liste.Add($"https://icons.duckduckgo.com/ip3/{name}.ico");
        }
        return liste;
    }

    private static void MarkeSetzen(string datei)
    {
        try { File.WriteAllBytes(datei, []); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Adressen, bei denen sich der Abruf gar nicht erst lohnt. Reine Funktion,
    /// geprüft in `LogoPruefung`.
    /// </summary>
    public static bool Brauchbar(string adresse)
    {
        if (!Uri.TryCreate(adresse.Trim(), UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme is not ("http" or "https")) return false;
        // SVG zeichnet WPF nicht. Die Endung verrät es meistens schon vor dem
        // Abruf -- wenn nicht, fängt es der Inhaltstyp.
        return !uri.AbsolutePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
    }

    private static BitmapImage? Bild(byte[] bytes, int breite, int mindestKante)
    {
        if (mindestKante > 0)
        {
            // Die echte Größe vor dem Verkleinern: DecodePixelWidth zöge ein
            // 16er-Symbol sonst stillschweigend auf 192.
            var rahmen = BitmapDecoder.Create(new MemoryStream(bytes), BitmapCreateOptions.None, BitmapCacheOption.OnLoad)
                .Frames.MaxBy(f => f.PixelWidth);
            if (rahmen is null || Math.Min(rahmen.PixelWidth, rahmen.PixelHeight) < mindestKante) return null;
        }

        var bild = new BitmapImage();
        bild.BeginInit();
        bild.StreamSource = new MemoryStream(bytes);
        // Klein dekodieren: Ein Logo in 2000 Pixeln belegt sonst 16 MB
        // Arbeitsspeicher für eine Kachel von 96 Pixeln.
        bild.DecodePixelWidth = breite;
        bild.CacheOption = BitmapCacheOption.OnLoad;
        bild.EndInit();
        bild.Freeze();
        return bild;
    }

    private static string Schluessel(string adresse) =>
        Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(adresse))).ToLowerInvariant() + ".bild";

    private static void TryLoeschen(string datei)
    {
        try { File.Delete(datei); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
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

        _klient = klient ?? new HttpClient
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
    public async Task<BitmapImage?> Holen(string adresse, int breite = 192)
    {
        if (!Brauchbar(adresse)) return null;

        var datei = Path.Combine(_verzeichnis, Schluessel(adresse));
        byte[]? bytes = null;
        try
        {
            if (File.Exists(datei))
            {
                bytes = await File.ReadAllBytesAsync(datei);
            }
            else
            {
                using var antwort = await _klient.GetAsync(adresse);
                if (!antwort.IsSuccessStatusCode) return null;
                // Eine Fehlerseite als „Logo" ist häufiger, als man denkt: Der
                // Server antwortet 200 und schickt HTML. Nur Bilder behalten.
                var typ = antwort.Content.Headers.ContentType?.MediaType ?? "";
                if (typ.Length > 0 && !typ.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return null;
                if (typ.Contains("svg", StringComparison.OrdinalIgnoreCase)) return null;

                bytes = await antwort.Content.ReadAsByteArrayAsync();
                if (bytes.Length is 0 or > 2_000_000) return null;
                await File.WriteAllBytesAsync(datei, bytes);
            }
            return Bild(bytes, breite);
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

    private static BitmapImage Bild(byte[] bytes, int breite)
    {
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

using System.Text.Json;

using REGOradio.Modelle;

namespace REGOradio.Speicher;

/// <summary>
/// Stationstasten sichern und mitnehmen (Bau 19): eine Datei
/// `*.regoradio` mit allen Tasten und ihren Logos.
///
/// **Logos kommen mit.** Auf einem neuen Rechner, im Hotel-WLAN ohne
/// Anmeldung, stünden sonst acht Kürzel da, bis die Logos wieder geholt
/// sind – und für Sender, deren Logo erst über die Homepage gefunden wurde,
/// dauert das. Die Bilder liegen als Base64 in der Datei; zwei Dutzend
/// Logos sind ein paar hundert Kilobyte.
///
/// **Die Datei wird geprüft, bevor sie etwas ersetzt.** Eine fremde oder
/// beschädigte Datei ergibt null, nicht halb gelesene Tasten.
///
/// Reine Rechnung ohne Datei und Oberfläche, geprüft in
/// `SenderpaketPruefung`.
/// </summary>
public static class Senderpaket
{
    public const string Kennzeichen = "REGOradio-Stationstasten";
    public const string Endung = ".regoradio";

    private static readonly JsonSerializerOptions Format = new() { WriteIndented = true };

    public sealed class Inhalt
    {
        public string Art { get; set; } = "";
        public int Fassung { get; set; }
        public DateTime Gesichert { get; set; }
        public string Programm { get; set; } = "";
        public List<Sender> Sender { get; set; } = [];
        /// <summary>Logoadresse → Bild (Base64).</summary>
        public Dictionary<string, string> Logos { get; set; } = [];
    }

    /// <param name="logo">
    /// Zu einem Sender die Adresse seines Logos und die Bytes, oder null.
    /// </param>
    public static string Packen(IEnumerable<Sender> sender, Func<Sender, (string Adresse, byte[] Bytes)?> logo, DateTime jetzt, string programm)
    {
        var inhalt = new Inhalt
        {
            Art = Kennzeichen,
            Fassung = 1,
            Gesichert = jetzt,
            Programm = programm,
            Sender = sender.Where(s => s.Platz > 0).OrderBy(s => s.Platz).ToList(),
        };
        foreach (var s in inhalt.Sender)
        {
            if (logo(s) is { } bild && bild.Adresse.Length > 0 && bild.Bytes.Length > 0)
            {
                inhalt.Logos[bild.Adresse] = Convert.ToBase64String(bild.Bytes);
            }
        }
        return JsonSerializer.Serialize(inhalt, Format);
    }

    /// <summary>
    /// Die Tasten aus einer Datei, oder null, wenn es keine REGOradio-Sicherung
    /// ist. Sender ohne Adresse oder auf einem Platz außerhalb der Tasten fallen
    /// weg; liegen zwei auf demselben Platz, gilt der erste.
    /// </summary>
    public static (List<Sender> Sender, Dictionary<string, byte[]> Logos)? Auspacken(string json, int plaetze)
    {
        Inhalt? inhalt;
        try
        {
            inhalt = JsonSerializer.Deserialize<Inhalt>(json);
        }
        catch (JsonException)
        {
            return null;
        }
        if (inhalt is null || inhalt.Art != Kennzeichen || inhalt.Fassung < 1) return null;

        var belegt = new HashSet<int>();
        var sender = (inhalt.Sender ?? [])
            .Where(s => s is not null && !string.IsNullOrWhiteSpace(s.Adresse) && s.Platz >= 1 && s.Platz <= plaetze)
            .Where(s => belegt.Add(s.Platz))
            .Select(s => new Sender
            {
                Kennung = s.Kennung ?? "",
                Name = string.IsNullOrWhiteSpace(s.Name) ? s.Adresse : s.Name,
                Adresse = s.Adresse.Trim(),
                Logo = s.Logo ?? "",
                Homepage = s.Homepage ?? "",
                Land = s.Land ?? "",
                Genre = s.Genre ?? "",
                Codec = s.Codec ?? "",
                Bitrate = s.Bitrate,
                Platz = s.Platz,
            })
            .ToList();

        var logos = new Dictionary<string, byte[]>();
        foreach (var (adresse, base64) in inhalt.Logos ?? [])
        {
            try { logos[adresse] = Convert.FromBase64String(base64); }
            catch (FormatException) { }
        }
        return (sender, logos);
    }
}

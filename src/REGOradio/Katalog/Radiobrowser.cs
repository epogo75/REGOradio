using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using REGOradio.Modelle;

namespace REGOradio.Katalog;

/// <summary>
/// Das Senderverzeichnis von radio-browser.info.
///
/// **Ein freier Dienst mit mehreren Spiegeln.** Es gibt keinen festen
/// Hauptserver; die Empfehlung des Dienstes ist, `all.api.radio-browser.info`
/// aufzulösen und einen der Rechner dahinter zu nehmen. Fällt einer aus, nimmt
/// der nächste Versuch einen anderen -- genau dafür wird die Liste gemerkt und
/// nicht der erste Treffer festgehalten.
///
/// **Die Kennung gehört dazu.** Der Dienst bittet um einen erkennbaren
/// User-Agent; ohne den ist nicht zu sehen, wer da fragt, und es steht in der
/// Nutzungsordnung.
/// </summary>
public sealed class Radiobrowser(HttpClient? klient = null)
{
    private const string SpiegelPool = "all.api.radio-browser.info";

    private readonly HttpClient _klient = klient ?? StandardKlient();
    private readonly List<string> _spiegel = [];
    private int _woSindWir;

    private static HttpClient StandardKlient()
    {
        var klient = new HttpClient
        {
            // Ein Sendersuchlauf im Hotel-WLAN darf nicht ewig hängen. Zehn
            // Sekunden sind lange genug für eine langsame Leitung und kurz
            // genug, dass man nicht glaubt, das Programm sei tot.
            Timeout = TimeSpan.FromSeconds(10),
        };
        klient.DefaultRequestHeaders.UserAgent.ParseAdd(Bau.Kennung);
        return klient;
    }

    /// <summary>Sender nach Namen suchen.</summary>
    public async Task<List<Treffer>> Suche(string name, int hoechstens = 40, CancellationToken abbruch = default)
    {
        var frage = $"/json/stations/search?limit={hoechstens}&hidebroken=true&order=votes&reverse=true"
                    + $"&name={Uri.EscapeDataString(name)}";
        return await Hole(frage, abbruch);
    }

    /// <summary>Die meistgehörten Sender eines Landes, etwa "DE".</summary>
    public async Task<List<Treffer>> ImLand(string landeskennung, int hoechstens = 40, CancellationToken abbruch = default)
    {
        var frage = $"/json/stations/search?limit={hoechstens}&hidebroken=true&order=votes&reverse=true"
                    + $"&countrycode={Uri.EscapeDataString(landeskennung)}";
        return await Hole(frage, abbruch);
    }

    private async Task<List<Treffer>> Hole(string frage, CancellationToken abbruch)
    {
        var fehler = new List<Exception>();
        // Jeden Spiegel einmal, dann aufgeben. Ohne Grenze sucht das Programm
        // im Hotel ohne Internet minutenlang weiter, und niemand sieht, warum.
        for (var versuch = 0; versuch < Math.Max(1, (await Spiegel(abbruch)).Count); versuch++)
        {
            var adresse = await NaechsterSpiegel(abbruch);
            try
            {
                var rohe = await _klient.GetFromJsonAsync<List<RoherSender>>(
                    $"https://{adresse}{frage}", abbruch);
                return (rohe ?? []).Select(Umformen).Where(t => t.Adresse.Length > 0).ToList();
            }
            catch (Exception ausnahme) when (ausnahme is HttpRequestException or JsonException or TaskCanceledException)
            {
                if (abbruch.IsCancellationRequested) throw;
                fehler.Add(ausnahme);
                _woSindWir++;
            }
        }
        throw new KatalogFehler(
            "Das Senderverzeichnis antwortet nicht. Hängt der Rechner in einem "
            + "Hotel-WLAN, das noch eine Anmeldung im Browser verlangt?",
            fehler.Count > 0 ? fehler[0] : null);
    }

    private async Task<List<string>> Spiegel(CancellationToken abbruch)
    {
        if (_spiegel.Count > 0) return _spiegel;
        try
        {
            var namen = await System.Net.Dns.GetHostAddressesAsync(SpiegelPool, abbruch);
            foreach (var adresse in namen)
            {
                // Über den Namen, nicht über die Adresse: Der Dienst spricht
                // HTTPS, und ein Zertifikat gilt für den Namen.
                var eintrag = await System.Net.Dns.GetHostEntryAsync(adresse).WaitAsync(abbruch);
                if (!_spiegel.Contains(eintrag.HostName)) _spiegel.Add(eintrag.HostName);
            }
        }
        catch (Exception fehler) when (fehler is System.Net.Sockets.SocketException or OperationCanceledException)
        {
            // Ohne DNS bleibt der Sammelname. Er zeigt auf einen der Spiegel und
            // funktioniert meistens -- nur ohne Ausweichmöglichkeit.
        }
        if (_spiegel.Count == 0) _spiegel.Add(SpiegelPool);
        return _spiegel;
    }

    private async Task<string> NaechsterSpiegel(CancellationToken abbruch)
    {
        var liste = await Spiegel(abbruch);
        return liste[Math.Abs(_woSindWir) % liste.Count];
    }

    /// <summary>
    /// Aus der Antwort des Verzeichnisses wird ein Treffer -- öffentlich, damit
    /// die Prüfungen aufgezeichnete Antworten durchschicken können, ohne ins
    /// Netz zu greifen.
    /// </summary>
    public static List<Treffer> AusJson(string json)
    {
        var rohe = JsonSerializer.Deserialize<List<RoherSender>>(json) ?? [];
        return rohe.Select(Umformen).Where(t => t.Adresse.Length > 0).ToList();
    }

    private static Treffer Umformen(RoherSender roh) => new()
    {
        Kennung = roh.stationuuid ?? "",
        Name = (roh.name ?? "").Trim(),
        // `url_resolved` ist die Adresse, auf die eine Wiedergabeliste zeigt.
        // Sie zu nehmen erspart dem Abspieler den Umweg über .pls und .m3u --
        // und genau daran scheitern sonst die Sender, die eine Liste
        // ausliefern statt eines Stroms.
        Adresse = (roh.url_resolved ?? roh.url ?? "").Trim(),
        Logo = (roh.favicon ?? "").Trim(),
        Land = (roh.country ?? "").Trim(),
        Genre = (roh.tags ?? "").Split(',').FirstOrDefault()?.Trim() ?? "",
        Codec = (roh.codec ?? "").Trim(),
        Bitrate = roh.bitrate,
    };

    /// <summary>Die Felder, die wir aus der Antwort brauchen -- und keines mehr.</summary>
    [method: JsonConstructor]
    public sealed record RoherSender(
        string? stationuuid,
        string? name,
        string? url,
        string? url_resolved,
        string? favicon,
        string? country,
        string? tags,
        string? codec,
        int bitrate);
}

public sealed class KatalogFehler(string meldung, Exception? ursache = null)
    : Exception(meldung, ursache);

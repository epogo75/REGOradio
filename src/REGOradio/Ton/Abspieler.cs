using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace REGOradio.Ton;

/// <summary>Was das Fenster über den Ton wissen muss.</summary>
public sealed record Tonstand(bool Laeuft, string Sendername, string Titel, string Fehler);

/// <summary>
/// Der Abspieler: eine Adresse rein, Ton raus.
///
/// **Ein Weg für alles.** Media Foundation, das Dekodiersystem von Windows,
/// öffnet den Strom selbst und spielt MP3, AAC, HE-AAC, HLS, FLAC, Ogg und WMA.
/// Eine Weiche nach Format gab es hier einmal -- MP3 selbst gelesen, alles
/// andere über Windows --, und sie existierte nur für den Titel aus dem Strom.
/// Zwei Wege, die sich unterschiedlich verhalten, für eine Zeile Text: Das war
/// den Preis nicht wert.
///
/// **Der Titel kommt nebenher.** Media Foundation gibt die ICY-Metadaten nicht
/// heraus; deshalb fragt die `Titelwache` den Sender in Abständen kurz nach
/// dem laufenden Lied. Der Ton hängt davon nicht ab.
///
/// **Vier Verben, mehr braucht ein Radio nicht:** spielen, stoppen,
/// Lautstärke, Ausgang.
///
/// **Ein eigener Faden öffnet den Strom.** Netzlesen gehört nicht in den
/// Oberflächenfaden: Ein Sender, der zehn Sekunden zum Antworten braucht, würde
/// sonst das ganze Fenster einfrieren -- und genau das passiert im schwachen
/// Hotel-WLAN, nicht im Ausnahmefall.
///
/// **Beim Wechsel wird gestoppt, nicht pausiert.** Ein pausierter Abspieler hält
/// das Audiogerät offen; auf einer Bluetooth-Box sperrt das den nächsten
/// Versuch aus.
/// </summary>
public sealed class Abspieler : IDisposable
{
    private readonly object _schloss = new();

    private WasapiOut? _ausgabe;
    private CancellationTokenSource? _abbruch;
    private Thread? _faden;

    private readonly Titelwache _titelwache = new();

    private string _sendername = "";
    private string _titel = "";
    private string _fehler = "";
    private int _lautstaerke = 45;
    private string _ausgangKennung = "";

    /// <summary>Wird gemeldet, sobald sich Fehler oder Laufzustand ändert.</summary>
    public event Action<Tonstand>? StandGeaendert;

    public Tonstand Stand => new(_ausgabe is not null, _sendername, _titel, _fehler);

    public Abspieler()
    {
        _titelwache.TitelGeaendert += titel =>
        {
            _titel = titel;
            Melden();
        };
    }

    public void Spiele(string adresse, string sendername)
    {
        Stopp();
        lock (_schloss)
        {
            _sendername = sendername;
            _fehler = "";
            _abbruch = new CancellationTokenSource();
            var marke = _abbruch.Token;
            _faden = new Thread(() => Schleife(adresse, marke))
            {
                IsBackground = true,
                Name = "REGOradio-Strom",
            };
            _faden.Start();
        }
        _titelwache.Beobachten(adresse);
        Melden();
    }

    public void Stopp()
    {
        Thread? faden;
        lock (_schloss)
        {
            _abbruch?.Cancel();
            faden = _faden;
            _faden = null;
        }
        // Auf das Ende warten, aber nicht endlos: Ein Sender, der gerade
        // geöffnet wird, lässt sich erst abbrechen, wenn Media Foundation
        // aufgibt. Länger warten hieße, das Fenster blockieren.
        faden?.Join(TimeSpan.FromSeconds(2));

        lock (_schloss)
        {
            _ausgabe?.Stop();
            _ausgabe?.Dispose();
            _ausgabe = null;
            _sendername = "";
            _titel = "";
        }
        _titelwache.Anhalten();
        Melden();
    }

    /// <summary>
    /// Die eine Lautstärke, 0 bis 100.
    ///
    /// Gesetzt wird sie am Abspieler, nicht am Gerät: Eine Änderung am
    /// Windows-Mischer würde alle Programme betreffen, und die Box behält ihre
    /// eigene Zahl. Zurückgelesen wird nie.
    /// </summary>
    public int Lautstaerke
    {
        get => _lautstaerke;
        set
        {
            _lautstaerke = Math.Clamp(value, 0, 100);
            PegelSetzen();
        }
    }

    /// <summary>
    /// Stumm, ohne die Lautstärke anzufassen.
    ///
    /// Wer stumm schaltet, will nach dem Telefonat dieselbe Lautstärke zurück
    /// -- nicht 0 und von vorn hochregeln. Deshalb eine eigene Schranke statt
    /// „Lautstärke auf 0". Nicht gespeichert: Ein Programm, das nach dem
    /// Neustart stumm aufgeht, sieht kaputt aus.
    /// </summary>
    public bool Stumm
    {
        get => _stumm;
        set
        {
            _stumm = value;
            PegelSetzen();
        }
    }

    private bool _stumm;

    private void PegelSetzen()
    {
        lock (_schloss)
        {
            if (_ausgabe is not null) _ausgabe.Volume = _stumm ? 0f : _lautstaerke / 100f;
        }
    }

    /// <summary>
    /// Den Ausgang wechseln. Läuft gerade etwas, wird es auf dem neuen Gerät
    /// fortgesetzt -- WASAPI lässt das Gerät einer laufenden Ausgabe nicht
    /// wechseln, also wird neu aufgebaut.
    /// </summary>
    public void Ausgang(string kennung, string laufendeAdresse, string sendername)
    {
        _ausgangKennung = kennung;
        if (_ausgabe is null || laufendeAdresse.Length == 0) return;
        Spiele(laufendeAdresse, sendername);
    }

    private void Schleife(string adresse, CancellationToken abbruch)
    {
        MediaFoundationReader? leser = null;
        try
        {
            leser = new MediaFoundationReader(adresse);
            if (abbruch.IsCancellationRequested) return;
            AusgabeStarten(leser);

            // Media Foundation liest selbst weiter; dieser Faden wacht nur, ob
            // der Ton noch läuft. Endet der Strom -- der Sender legt auf oder
            // das WLAN reißt ab --, steht die Ausgabe still, und das wird
            // gemeldet statt still hingenommen.
            while (!abbruch.IsCancellationRequested)
            {
                if (_ausgabe is { PlaybackState: PlaybackState.Stopped })
                {
                    _fehler = "Der Sender hat aufgehört zu senden.";
                    Melden();
                    break;
                }
                Thread.Sleep(250);
            }
        }
        catch (Exception fehler) when (!abbruch.IsCancellationRequested)
        {
            // Media Foundation meldet ein nicht erreichbares Netz und ein
            // unbekanntes Format mit derselben Art Ausnahme. Beides hat im
            // Hotel meist denselben Grund: kein Durchkommen nach draußen.
            _fehler = "Der Sender spielt nicht. Steht die Verbindung, und verlangt "
                      + $"das WLAN noch eine Anmeldung im Browser? ({fehler.Message})";
            Melden();
        }
        catch (Exception)
        {
            // Gewollt: Stopp oder Senderwechsel während des Öffnens.
        }
        finally
        {
            // ERST die Ausgabe anhalten, DANN den Leser freigeben. Andersherum
            // greift die Ausgabe in ihrem eigenen Faden noch auf einen Leser
            // zu, den es nicht mehr gibt -- und wirft.
            lock (_schloss) { _ausgabe?.Stop(); }
            leser?.Dispose();
        }
    }

    private void AusgabeStarten(IWaveProvider quelle)
    {
        MMDevice? geraet = Ausgaenge.Geraet(_ausgangKennung);
        var ausgabe = geraet is null
            ? new WasapiOut()
            : new WasapiOut(geraet, AudioClientShareMode.Shared, useEventSync: true, latency: 200);
        ausgabe.Init(quelle);
        ausgabe.Volume = _stumm ? 0f : _lautstaerke / 100f;
        ausgabe.Play();

        lock (_schloss) { _ausgabe = ausgabe; }
        Melden();
    }

    private void Melden() => StandGeaendert?.Invoke(Stand);

    public void Dispose()
    {
        Stopp();
        _titelwache.Dispose();
    }
}

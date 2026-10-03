using System.Windows;
using System.Windows.Threading;

using REGOradio.Katalog;
using REGOradio.Modelle;
using REGOradio.Ton;

namespace REGOradio;

/// <summary>
/// Tote Sender reparieren (Bau 17). Welche Adressen in Frage kommen,
/// rechnet `Katalog/Senderheilung`; hier wird ausprobiert.
///
/// Ablauf, sobald ein Fehler neu auftritt:
/// 1. Riss der Strom nach dem Start ab, einmal dieselbe Adresse neu – meist
///    war es nur das WLAN.
/// 2. Sonst das Verzeichnis fragen und die Ersatzadressen der Reihe nach
///    spielen. Jede, die wieder scheitert, meldet einen neuen Fehler, und
///    die nächste kommt dran.
/// 3. Spielt eine, wird sie auf die Taste geschrieben – erst dann. Eine
///    ungeprüfte Adresse ersetzt nie eine, die vielleicht nur kurz hing.
///
/// Wer selbst eine Taste drückt oder stoppt, beendet die Suche.
/// </summary>
public partial class Hauptfenster
{
    private sealed class Heilung(Sender sender, bool abgerissen)
    {
        public Sender Sender { get; } = sender;
        public bool Abgerissen { get; } = abgerissen;
        public bool NeuVerbunden { get; set; }
        public Queue<Treffer>? Rest { get; set; }
        public Treffer? Versuch { get; set; }
        public bool Aufgegeben { get; set; }
    }

    private Heilung? _heilung;
    private string _fehlerVorher = "";
    private string _hinweis = "";
    private readonly DispatcherTimer _hinweisuhr = new() { Interval = TimeSpan.FromSeconds(12) };

    /// <summary>Aus `TonstandZeigen`, bei jeder Meldung des Abspielers.</summary>
    private void FehlerBeachten(Tonstand stand)
    {
        var neu = stand.Fehler.Length > 0 && _fehlerVorher.Length == 0;
        _fehlerVorher = stand.Fehler;

        if (_laufender is null || _heilung?.Sender != _laufender) _heilung = null;

        if (stand.Laeuft && stand.Fehler.Length == 0 && _heilung is { } h)
        {
            if (h.Versuch is { } ersatz)
            {
                Senderheilung.Uebernehmen(h.Sender, ersatz);
                if (h.Sender.Platz > 0) _ablage.SenderSchreiben(_sender);
                HinweisZeigen($"{h.Sender.Name} war umgezogen – die neue Adresse liegt jetzt auf der Taste.");
                _heilung = null;
            }
            else if (h.NeuVerbunden)
            {
                HinweisZeigen("");
                _heilung = null;
            }
            return;
        }

        if (neu && _laufender is not null) _ = HeilenWeiter(_laufender, stand.Fehler);
    }

    private async Task HeilenWeiter(Sender sender, string fehler)
    {
        _heilung ??= new Heilung(sender, abgerissen: fehler == Abspieler.Abrissmeldung);
        var h = _heilung;
        if (h.Aufgegeben) return;

        if (h.Abgerissen && !h.NeuVerbunden)
        {
            h.NeuVerbunden = true;
            HinweisZeigen("Die Verbindung ist abgerissen – verbinde neu …", bleibt: true);
            await Task.Delay(TimeSpan.FromSeconds(3));
            if (_heilung != h || _laufender != sender) return;
            _abspieler.Spiele(sender.Adresse, sender.Name, sender.Lautheit);
            return;
        }

        if (h.Rest is null)
        {
            // Erst fragen, ob es am WLAN liegt (Bau 22). Hängt eine
            // Anmeldeseite davor, ist kein Sender umgezogen – und die Suche im
            // Verzeichnis käme auch nicht durch.
            HinweisZeigen($"{sender.Name} antwortet nicht – prüfe die Verbindung …", bleibt: true);
            if (await AnmeldungPruefen())
            {
                if (_heilung == h) h.Aufgegeben = true;
                return;
            }
            if (_heilung != h || _laufender != sender) return;

            HinweisZeigen($"{sender.Name} antwortet nicht – suche eine neue Adresse …", bleibt: true);
            var kandidaten = await KandidatenHolen(sender);
            if (_heilung != h || _laufender != sender) return;
            h.Rest = new Queue<Treffer>(kandidaten);
        }

        if (h.Rest.Count == 0)
        {
            // Aufgeben: Die Fehlermeldung des Abspielers bleibt stehen, sie
            // sagt mehr als „nichts gefunden".
            h.Aufgegeben = true;
            HinweisZeigen("");
            FehlerzeileZeigen(_abspieler.Stand.Fehler);
            return;
        }

        h.Versuch = h.Rest.Dequeue();
        _abspieler.Spiele(h.Versuch.Adresse, sender.Name);
    }

    private async Task<List<Treffer>> KandidatenHolen(Sender sender)
    {
        try
        {
            var nachKennung = sender.Kennung.Length > 0 ? await _katalog.NachKennung([sender.Kennung]) : [];
            var nachName = await _katalog.Suche(sender.Name, hoechstens: 15);
            return Senderheilung.Kandidaten(sender, nachKennung, nachName);
        }
        catch (KatalogFehler)
        {
            return [];
        }
    }

    /// <summary>
    /// Ein Hinweis in der Zeile unter dem Sender. `bleibt`: solange gesucht
    /// wird; sonst verschwindet er nach zwölf Sekunden.
    /// </summary>
    private void HinweisZeigen(string text, bool bleibt = false)
    {
        _hinweis = text;
        _hinweisuhr.Stop();
        if (text.Length > 0 && !bleibt)
        {
            _hinweisuhr.Tick -= HinweisAbgelaufen;
            _hinweisuhr.Tick += HinweisAbgelaufen;
            _hinweisuhr.Start();
        }
        FehlerzeileZeigen(_abspieler.Stand.Fehler);
    }

    private void HinweisAbgelaufen(object? absender, EventArgs e)
    {
        _hinweisuhr.Stop();
        _hinweis = "";
        FehlerzeileZeigen(_abspieler.Stand.Fehler);
    }

    /// <summary>
    /// Während gesucht wird, steht dort, was gerade passiert – nicht die
    /// Fehlermeldung des letzten Versuchs, die sonst bei jeder Ersatzadresse
    /// aufblitzte.
    /// </summary>
    private void FehlerzeileZeigen(string fehler)
    {
        var suchtNoch = (_heilung is { Aufgegeben: false } || _wartetAufAnmeldung) && _hinweis.Length > 0;
        var text = suchtNoch ? _hinweis : fehler.Length > 0 ? fehler : _hinweis;
        FehlerZeile.Text = text;
        FehlerZeile.Visibility = text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}

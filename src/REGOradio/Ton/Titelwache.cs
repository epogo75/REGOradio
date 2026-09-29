using System.IO;
using System.Net.Http;

namespace REGOradio.Ton;

/// <summary>
/// Fragt den Sender in Abständen, welches Lied gerade läuft.
///
/// **Warum nicht aus dem Tonstrom selbst?** Den öffnet Media Foundation, und
/// es gibt die ICY-Metadaten nicht heraus. Den Strom selbst zu öffnen und an
/// Media Foundation weiterzureichen hieße, den einen Wiedergabeweg wieder
/// aufzuspalten -- genau die Weiche, die bewusst entfernt wurde.
///
/// **Warum keine zweite Dauerverbindung?** Sie würde den ganzen Strom ein
/// zweites Mal laden, nur um alle paar Kilobyte eine Zeile Text zu lesen. Im
/// Hotel-WLAN zählt jede Leitung, und manche Sender zählen jede Verbindung als
/// Hörer. Stattdessen: alle 10 Sekunden kurz verbinden, bis zum ersten
/// Metadatenblock lesen, auflegen. Ein neuer Titel erscheint damit bis zu
/// 10 Sekunden später.
///
/// **Sender ohne ICY** (kein `icy-metaint` im Kopf, HLS-Listen) werden nach
/// dem ersten Versuch in Ruhe gelassen: Sie haben keine Titel, und jede
/// weitere Anfrage wäre verschenkt.
///
/// Die Zeichensatzfallen (Latin-1, doppelt kodiertes UTF-8) behandelt
/// `IcyStrom.TextAus`.
/// </summary>
public sealed class Titelwache : IDisposable
{
    // Zehn Sekunden: Ein neues Lied soll auffallen, solange es noch neu ist.
    // Mit zwanzig stand oft noch das Cover des vorigen da. Jede Abfrage liest
    // nur bis zum ersten Metadatenblock -- bei 320 kBit/s rund 40 kB, also
    // etwa ein Zehntel dessen, was der Ton ohnehin lädt.
    private static readonly TimeSpan Abstand = TimeSpan.FromSeconds(10);

    private readonly HttpClient _klient;
    private CancellationTokenSource? _abbruch;
    private string _letzter = "";

    public Titelwache(HttpClient? klient = null)
    {
        _klient = klient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        _klient.DefaultRequestHeaders.UserAgent.ParseAdd(Bau.Kennung);
    }

    /// <summary>
    /// Ein neuer Titel -- oder leer, wenn der Sender keinen (mehr) meldet.
    /// Kommt aus einem Hintergrundfaden.
    /// </summary>
    public event Action<string>? TitelGeaendert;

    public void Beobachten(string adresse)
    {
        Anhalten();
        _abbruch = new CancellationTokenSource();
        _ = Schleife(adresse, _abbruch.Token);
    }

    public void Anhalten()
    {
        _abbruch?.Cancel();
        _abbruch = null;
        if (_letzter.Length > 0)
        {
            _letzter = "";
            TitelGeaendert?.Invoke("");
        }
    }

    private async Task Schleife(string adresse, CancellationToken abbruch)
    {
        if (adresse.Split('?')[0].EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)) return;

        // Kurz warten: Der Tonstrom soll zuerst stehen. Zwei Verbindungen im
        // selben Augenblick zum selben Sender sind genau das, was vermieden
        // werden soll.
        try { await Task.Delay(TimeSpan.FromSeconds(3), abbruch); }
        catch (TaskCanceledException) { return; }

        while (!abbruch.IsCancellationRequested)
        {
            string? titel;
            try
            {
                titel = await Abfragen(adresse, abbruch);
            }
            catch (OperationCanceledException) when (abbruch.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                titel = _letzter;   // Netz weg: Titel stehen lassen, später wieder fragen
            }

            if (titel is null) return;   // Sender ohne ICY -- nicht weiter fragen
            // Inzwischen umgeschaltet? Dann gehört dieser Titel zum vorigen
            // Sender und darf nicht mehr gemeldet werden -- sonst stünde der
            // alte Titel unter dem neuen Sender.
            if (abbruch.IsCancellationRequested) return;
            if (titel != _letzter)
            {
                _letzter = titel;
                TitelGeaendert?.Invoke(titel);
            }

            try { await Task.Delay(Abstand, abbruch); }
            catch (TaskCanceledException) { return; }
        }
    }

    /// <returns>Der Titel, leer bei leerem Titel, null bei einem Sender ohne ICY.</returns>
    private async Task<string?> Abfragen(string adresse, CancellationToken abbruch)
    {
        using var anfrage = new HttpRequestMessage(HttpMethod.Get, adresse);
        anfrage.Headers.TryAddWithoutValidation("Icy-MetaData", "1");
        using var antwort = await _klient.SendAsync(anfrage, HttpCompletionOption.ResponseHeadersRead, abbruch);
        if (!antwort.IsSuccessStatusCode) return _letzter;

        var metaAbstand = Kopfzahl(antwort, "icy-metaint");
        if (metaAbstand <= 0) return null;

        await using var netz = await antwort.Content.ReadAsStreamAsync(abbruch);

        // Das Zeitlimit des Klienten gilt nur bis zum Kopf. Danach kann ein
        // Lesevorgang im schwachen WLAN ewig hängen -- und mit ihm die ganze
        // Wache. Nach zehn Sekunden wird der Strom deshalb zugemacht, das
        // bricht auch ein gerade laufendes Lesen ab.
        using var frist = CancellationTokenSource.CreateLinkedTokenSource(abbruch);
        frist.CancelAfter(TimeSpan.FromSeconds(10));
        await using var _ = frist.Token.Register(() => netz.Dispose());

        using var icy = new IcyStrom(netz, metaAbstand);
        string? gefunden = null;
        icy.TitelGeaendert += t => gefunden = t;

        // Bis zum ersten Metadatenblock lesen -- und zur Sicherheit bis zum
        // zweiten, falls der erste leer ist (Längenbyte 0). Dann auflegen.
        var puffer = new byte[8192];
        long gelesen = 0;
        var grenze = 2L * metaAbstand + puffer.Length;
        while (gefunden is null && gelesen < grenze)
        {
            int n;
            try { n = await icy.ReadAsync(puffer, frist.Token); }
            catch (Exception) when (frist.IsCancellationRequested && !abbruch.IsCancellationRequested) { break; }
            if (n <= 0) break;
            gelesen += n;
        }
        return gefunden ?? "";
    }

    private static int Kopfzahl(HttpResponseMessage antwort, string name)
    {
        if (antwort.Headers.TryGetValues(name, out var werte) && int.TryParse(werte.FirstOrDefault(), out var zahl)) return zahl;
        if (antwort.Content.Headers.TryGetValues(name, out var inhalt) && int.TryParse(inhalt.FirstOrDefault(), out var zahl2)) return zahl2;
        return 0;
    }

    public void Dispose()
    {
        _abbruch?.Cancel();
        _klient.Dispose();
    }
}

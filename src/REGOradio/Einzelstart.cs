namespace REGOradio;

/// <summary>
/// REGOradio läuft nur einmal.
///
/// **Warum das nicht nur Ordnung ist.** Zwei Instanzen heißen zwei Abspieler,
/// zwei Tray-Symbole und zwei Fernbedienungen, die beide Port 8765 wollen –
/// der zweite Webdienst scheitert, und das Handy spricht mit irgendeiner. Am
/// Touchscreen passiert das leicht: Das Fenster ist im Tray, man tippt auf die
/// Verknüpfung, weil man es zurückhaben will.
///
/// Genau das wird deshalb erfüllt: Der zweite Start beendet sich und **weckt
/// den ersten**, der sein Fenster zeigt. Nur beim Autostart (`--tray`) weckt
/// er nicht – wer den Rechner hochfährt, will kein Fenster.
///
/// **`Local\`**, nicht `Global\`: gilt je Anmeldung. Ein zweiter Benutzer am
/// selben Rechner darf sein eigenes Radio haben.
///
/// Debug- und Release-Bau tragen denselben Namen und sperren sich deshalb
/// gegenseitig. Das ist Absicht: Beide wollen dieselben Einstellungen und
/// denselben Port.
/// </summary>
public sealed class Einzelstart : IDisposable
{
    public const string Standardname = "REGOradio";

    private readonly Mutex _marke;
    private readonly EventWaitHandle _wecker;
    private Thread? _horcher;
    private volatile bool _ende;

    private Einzelstart(Mutex marke, EventWaitHandle wecker, bool erster)
    {
        _marke = marke;
        _wecker = wecker;
        Erster = erster;
    }

    /// <summary>Ob dieser Start der erste ist. Wenn nicht: wecken und gehen.</summary>
    public bool Erster { get; }

    /// <param name="name">Nur die Prüfungen geben einen eigenen Namen, damit sie
    /// einem laufenden REGOradio nicht in die Quere kommen.</param>
    public static Einzelstart Versuchen(string name = Standardname)
    {
        // Es zählt nur, ob das benannte Objekt schon da war, nicht wem es
        // gehört. Deshalb ohne Besitz angelegt: Ein Mutex im Besitz ist an
        // seinen Faden gebunden und müsste von genau dem wieder freigegeben
        // werden. Stirbt der erste Start, schließt Windows seine Griffe, das
        // Objekt verschwindet, und der nächste Start ist wieder der erste.
        var marke = new Mutex(false, $@"Local\{name}-einmal", out var neu);
        var wecker = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{name}-wecken");
        return new Einzelstart(marke, wecker, neu);
    }

    /// <summary>Den ersten Start bitten, sein Fenster zu zeigen.</summary>
    public void Wecken() => _wecker.Set();

    /// <summary>
    /// Auf das Wecken warten – auf einem eigenen Faden. <paramref name="geweckt"/>
    /// läuft auf diesem Faden; wer die Oberfläche anfasst, geht über den Dispatcher.
    /// </summary>
    public void Lauschen(Action geweckt)
    {
        if (!Erster || _horcher is not null) return;

        _horcher = new Thread(() =>
        {
            while (true)
            {
                _wecker.WaitOne();
                if (_ende) return;
                geweckt();
            }
        })
        {
            IsBackground = true,
            Name = "REGOradio-Wecker",
        };
        _horcher.Start();
    }

    public void Dispose()
    {
        _ende = true;

        // Den eigenen Horcher aufwecken, damit er sieht, dass Schluss ist –
        // sonst wartet er auf einem Griff, der gleich geschlossen wird.
        if (_horcher is not null)
        {
            _wecker.Set();
            _horcher.Join(TimeSpan.FromSeconds(1));
        }

        _wecker.Dispose();
        _marke.Dispose();
    }
}

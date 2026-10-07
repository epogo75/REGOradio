using System.Windows;

using REGOradio.Anzeige;

namespace REGOradio;

public partial class App : Application
{
    private Hauptfenster? _fenster;
    private Traysymbol? _tray;
    private Einzelstart? _einzelstart;

    /// <summary>Tag oder Nacht -- die Farbtafel, die gerade gilt.</summary>
    public bool IstNacht { get; private set; }

    /// <summary>Das Farbthema, das gerade gilt: standard, holiday, mitternacht, neon.</summary>
    public string Thema { get; private set; } = "standard";

    /// <summary>
    /// Die Themen, die es gibt -- in der Reihenfolge der Auswahlliste. Die zwei
    /// Farben sind die Akzente bei Tag und bei Nacht, als Vorschau in der Liste.
    /// Der Schlüssel ist zugleich der Dateiname unter `Stil/Themen`
    /// ("neongruen" wird zu `Neongruen-Nacht.xaml`).
    /// </summary>
    public static readonly Themenwahl[] Themen =
    [
        // REGO (Bau 26): der Stil der Familie, Vorgabe für neue Installationen.
        new("rego", "REGO", "#00A87E", "#00D9A3"),
        new("standard", "Standard", "#1F6F5C", "#5FA790"),
        new("holiday", "Holiday", "#B5561A", "#F2A24A"),
        new("mitternacht", "Mitternacht", "#2451B8", "#5CC8FF"),
        new("neon", "Neon Pink", "#D1008F", "#FF00CC"),
        new("neongruen", "Neon Grün", "#3F8A00", "#8CFF00"),
        new("neonblau", "Neon Blau", "#0077D6", "#00EAFF"),
    ];

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // NUR EINMAL. Läuft REGOradio schon, holt dieser Start dessen Fenster
        // nach vorn und geht – siehe Einzelstart. Beim Autostart weckt er
        // nicht: wer den Rechner hochfährt, will kein Fenster.
        _einzelstart = Einzelstart.Versuchen();
        if (!_einzelstart.Erster)
        {
            if (!e.Args.Contains("--tray")) _einzelstart.Wecken();
            _einzelstart.Dispose();
            _einzelstart = null;
            Shutdown();
            return;
        }

        // Der Rahmen im Thema, für jedes Fenster, das ab jetzt aufgeht.
        Fensterkleid.Einschalten();

        // Tag oder Nacht entscheidet das Hauptfenster -- es kennt die
        // Einstellung (Tag, Nacht, Automatisch), und es tut das, bevor es
        // sich zeigt. Niemand soll um 23 Uhr erst geblendet werden.
        _fenster = new Hauptfenster();
        _tray = new Traysymbol(_fenster);

        // Ein zweiter Start meldet sich hier. Der Horcher läuft auf einem
        // eigenen Faden, das Fenster gehört dem Oberflächenfaden.
        _einzelstart.Lauschen(() => Dispatcher.InvokeAsync(() => _tray?.Zeigen()));

        // Beim Autostart mit Windows still ins Tray: Wer den Rechner
        // hochfährt, will kein Fenster, das sich über alles legt. Die letzte
        // Station spielt trotzdem, wenn eine lief.
        if (!e.Args.Contains("--tray")) _fenster.Show();
    }

    /// <summary>
    /// Die Farbtafel austauschen: Thema und Tageszeit zusammen.
    ///
    /// Alle Tafeln unter `Stil/Themen` haben dieselben Schlüssel, und die Stile
    /// greifen über `DynamicResource` darauf zu -- deshalb genügt es, das erste
    /// Wörterbuch zu ersetzen. Ein unbekanntes Thema (etwa aus einer älteren
    /// Einstellungsdatei) fällt auf Standard zurück, statt das Programm ohne
    /// Farben stehen zu lassen.
    /// </summary>
    public void Farbtafel(string thema, bool nacht)
    {
        if (!Themen.Any(t => t.Schluessel == thema)) thema = "standard";
        IstNacht = nacht;
        Thema = thema;
        var name = char.ToUpperInvariant(thema[0]) + thema[1..];
        var neue = new ResourceDictionary
        {
            Source = new Uri($"Stil/Themen/{name}-{(nacht ? "Nacht" : "Tag")}.xaml", UriKind.Relative),
        };
        Resources.MergedDictionaries[0] = neue;

        // Die Titelleiste malt Windows, nicht WPF – sie folgt der neuen Tafel
        // nicht von selbst.
        Fensterkleid.Alle();

        // Der Schriftzug rechnet seine Sperrung aus der Stärke der Schrift
        // (Bau 26) – das ist kein Ressourcenverweis, er muss neu bauen.
        Schriftzug.Alle();
    }

    /// <summary>
    /// Windows fährt herunter oder meldet ab: nicht fragen, sondern beenden.
    /// Eine Rückfrage, auf die niemand antwortet, hielte das Herunterfahren
    /// auf – und das Radio verstummt dabei ohnehin.
    /// </summary>
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _fenster?.WirklichSchliessen();
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _einzelstart?.Dispose();
        base.OnExit(e);
    }
}

/// <summary>Ein Eintrag der Themenliste in den Einstellungen.</summary>
public sealed record Themenwahl(string Schluessel, string Name, string TagFarbe, string NachtFarbe);

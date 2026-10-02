using System.IO;
using System.Text.Json;

using REGOradio.Modelle;

namespace REGOradio.Speicher;

/// <summary>
/// Was das Programm behält: Stationstasten und Einstellungen.
///
/// **Zwei Dateien unter `%APPDATA%\REGOradio`, kein Datenbankdienst.** Bei einem
/// Dutzend Stationstasten ist eine Datenbank Beiwerk; eine JSON-Datei lässt sich
/// ansehen, kopieren und in eine Sicherung legen, ohne ein Werkzeug dafür zu
/// brauchen.
///
/// **Geschrieben wird über eine Nebendatei.** Erst `.neu` schreiben, dann
/// ersetzen: Ein Stromausfall mitten im Schreiben hinterlässt sonst eine halbe
/// Datei, und die Stationstasten sind weg. Das ist kein Randfall auf einem
/// Notebook, das zuklappt.
/// </summary>
public sealed class Ablage
{
    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
    };

    private readonly string _verzeichnis;

    public Ablage(string? verzeichnis = null)
    {
        _verzeichnis = verzeichnis ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "REGOradio");
        Directory.CreateDirectory(_verzeichnis);
    }

    public string SenderDatei => Path.Combine(_verzeichnis, "stationen.json");
    public string EinstellungenDatei => Path.Combine(_verzeichnis, "einstellungen.json");

    public List<Sender> SenderLesen() => Lesen<List<Sender>>(SenderDatei) ?? [];

    public void SenderSchreiben(IEnumerable<Sender> sender) =>
        Schreiben(SenderDatei, sender.ToList());

    public Einstellungen EinstellungenLesen() =>
        Lesen<Einstellungen>(EinstellungenDatei) ?? new Einstellungen();

    public void EinstellungenSchreiben(Einstellungen einstellungen) =>
        Schreiben(EinstellungenDatei, einstellungen);

    /// <summary>
    /// Eine kaputte oder fehlende Datei ist kein Grund, nicht zu starten.
    ///
    /// Ein Programm, das wegen einer unlesbaren Einstellungsdatei gar nicht
    /// erst aufgeht, ist schlimmer als eines, das ohne Stationstasten
    /// hochkommt: Ohne Fenster kommt niemand an die Stelle, an der sich das
    /// richten ließe.
    /// </summary>
    private static T? Lesen<T>(string pfad) where T : class
    {
        try
        {
            if (!File.Exists(pfad)) return null;
            return JsonSerializer.Deserialize<T>(File.ReadAllText(pfad), Format);
        }
        catch (Exception fehler) when (fehler is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void Schreiben<T>(string pfad, T inhalt)
    {
        var neben = pfad + ".neu";
        File.WriteAllText(neben, JsonSerializer.Serialize(inhalt, Format));
        File.Move(neben, pfad, overwrite: true);
    }
}

/// <summary>
/// Die Einstellungen. Alles, was sich im Betrieb ändert und einen Neustart
/// überleben muss -- und nichts sonst.
/// </summary>
public sealed class Einstellungen
{
    /// <summary>
    /// Die eine Lautstärke, 0 bis 100.
    ///
    /// **Sie wird gesetzt, nie gelesen.** Wer an der Bluetooth-Box selbst
    /// leiser dreht, bewegt diesen Regler nicht -- sonst gäbe es zwei
    /// Fassungen derselben Zahl, und eine davon lügt.
    /// </summary>
    public int Lautstaerke { get; set; } = 45;

    /// <summary>
    /// Auf welches Audiogerät gespielt wird -- die WASAPI-Kennung, nicht der
    /// Name. Namen wiederholen sich („Kopfhörer"), Kennungen nicht.
    /// Leer heißt: das Standardgerät von Windows.
    /// </summary>
    public string Ausgang { get; set; } = "";

    /// <summary>Welche Stationstaste beim Start wieder gespielt wird. 0: keine.</summary>
    public int LetzterPlatz { get; set; }

    /// <summary>
    /// Ob das Programm beim Schließen des Fensters im Tray weiterläuft. Gilt
    /// nur, wenn nicht gefragt wird (<see cref="SchliessenFragen"/>).
    /// </summary>
    public bool ImTrayBleiben { get; set; } = true;

    /// <summary>
    /// Ob beim Schließen kurz gefragt wird: in den Tray oder ganz beenden.
    ///
    /// Gewünscht seit Bau 11. Vorher entschied allein <see cref="ImTrayBleiben"/>,
    /// und wer es nicht wusste, suchte nach dem Schließen das Radio, das im
    /// Tray weiterspielte. Wer „Nicht mehr fragen" ankreuzt, setzt dieses Feld
    /// zurück und legt damit zugleich <see cref="ImTrayBleiben"/> fest.
    /// </summary>
    public bool SchliessenFragen { get; set; } = true;

    /// <summary>
    /// Ob der Rechner wach bleibt, solange Radio läuft. Abschaltbar, weil es
    /// Leute gibt, die ihr Notebook abends bewusst mit laufendem Radio
    /// einschlafen lassen wollen.
    /// </summary>
    public bool KeinRuhezustand { get; set; } = true;

    /// <summary>
    /// "automatisch", "tag" oder "nacht". Automatisch heißt: von 21 bis 7 Uhr
    /// dunkel -- abends im Hotelzimmer soll niemand erst geblendet werden und
    /// dann umschalten müssen.
    /// </summary>
    public string Darstellung { get; set; } = "automatisch";

    /// <summary>
    /// Das Farbthema, ein Schlüssel aus `App.Themen` ("standard", "neon", ...). Jedes
    /// hat eine Tag- und eine Nachtfassung; welche gilt, entscheidet
    /// `Darstellung`.
    /// </summary>
    public string Thema { get; set; } = "standard";

    /// <summary>
    /// Ob die Handy-Fernbedienung läuft. Aus, bis jemand sie einschaltet: Ein
    /// offener Port im Hotel-WLAN, den niemand benutzt, ist nur Angriffsfläche.
    /// </summary>
    public bool FernAn { get; set; }

    /// <summary>Die vierstellige PIN der Fernbedienung. Leer, bis sie zum ersten Mal gebraucht wird.</summary>
    public string FernPin { get; set; } = "";

    /// <summary>
    /// Der Schlüssel, aus dem mit der PIN das Anmeldezeichen des Handys
    /// gerechnet wird. Einmal erzeugt, dann fest -- sonst wären alle Handys
    /// nach jedem Neustart abgemeldet.
    /// </summary>
    public string FernSchluessel { get; set; } = "";

    // „Mit Windows starten" steht NICHT hier. Die Wahrheit darüber ist der
    // Eintrag in der Registry (`Speicher/Autostart.cs`); eine Kopie hier wäre
    // die zweite Fassung derselben Sache und liefe auseinander, sobald jemand
    // den Eintrag im Task-Manager abschaltet.
}

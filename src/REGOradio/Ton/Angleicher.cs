using NAudio.Wave;

namespace REGOradio.Ton;

/// <summary>
/// Gleicht die Lautheit eines Senders an einen festen Wert an (Bau 21).
/// Sitzt im Tonweg zwischen Media Foundation und der Ausgabe; gemessen wird
/// <b>vor</b> der Verstärkung, sonst mäße er sich selbst.
///
/// **Langsam nachführen.** Die Verstärkung ändert sich um höchstens ein
/// Dezibel je Sekunde. Schneller hieße Pumpen: jede leise Stelle würde
/// hochgezogen, und aus Radio würde Brei.
///
/// **Mit dem Gedächtnis anfangen.** Kennt REGOradio den Sender schon
/// (`Sender.Lautheit`), gilt dessen Wert vom ersten Ton an – sonst käme der
/// Sprung beim Umschalten erst einmal doch und würde dann langsam
/// weggeregelt. Neue Sender starten bei 0 dB, bis zehn Sekunden gemessen
/// sind.
///
/// **Grenzen.** Abgesenkt wird um höchstens 12 dB, angehoben um höchstens
/// 6 dB und nie weiter, als die Spitzen im Messfenster es erlauben – ein
/// leiser Sender wird nicht zum übersteuerten.
/// </summary>
public sealed class Angleicher : ISampleProvider
{
    /// <summary>
    /// Wohin angeglichen wird. Gemessen am 03.10.2026: SWR3 −16,7,
    /// Schwarzwaldradio −16,5, Deutschlandfunk −18,9 LUFS. Ein Ziel in der
    /// Mitte lässt die üblichen Sender fast unberührt und holt nur die
    /// Ausreißer heran – ein Ziel von −14 hätte alles angehoben und damit
    /// lauter gemacht, als es vor Bau 21 war.
    /// </summary>
    public const double Ziel = -17.0;
    public const double HoechstensAb = -12.0;
    public const double HoechstensAuf = 6.0;
    public const double SchrittJeSekunde = 1.0;

    /// <summary>Ab so vielen Blöcken (je 100 ms) gilt die eigene Messung.</summary>
    public const int Mindestbloecke = 100;

    private readonly ISampleProvider _quelle;
    private readonly Laufmesser _messer;
    private readonly double? _vorgabe;
    private double _dbJetzt;

    public Angleicher(ISampleProvider quelle, double? bekannteLufs)
    {
        _quelle = quelle;
        _messer = new Laufmesser(quelle.WaveFormat.SampleRate, quelle.WaveFormat.Channels);
        _vorgabe = bekannteLufs;
        _dbJetzt = An && bekannteLufs is { } l ? Verstaerkung(l, 0) : 0;
    }

    public WaveFormat WaveFormat => _quelle.WaveFormat;

    /// <summary>Aus heißt: unverändert durchreichen (Einstellung „Alle Sender gleich laut").</summary>
    public volatile bool An = true;

    /// <summary>Die Messung, sobald sie zählt – zum Merken am Sender.</summary>
    public double? Gemessen => _messer.Bloecke >= Mindestbloecke ? _messer.Lufs : null;

    /// <summary>Wie viele Sekunden schon gemessen sind.</summary>
    public double Sekunden => _messer.Bloecke / 10.0;

    /// <summary>Die Verstärkung in dB, die gerade wirkt.</summary>
    public double Dezibel => _dbJetzt;

    /// <summary>Die Verstärkung, die für eine Lautheit gilt – reine Rechnung.</summary>
    public static double Verstaerkung(double lufs, float spitze)
    {
        var db = Math.Clamp(Ziel - lufs, HoechstensAb, HoechstensAuf);
        if (db > 0 && spitze > 0)
        {
            var luft = -20 * Math.Log10(spitze);   // Abstand der Spitze zu 0 dBFS
            db = Math.Min(db, Math.Max(0, luft - 0.5));
        }
        return db;
    }

    public int Read(float[] puffer, int offset, int anzahl)
    {
        var gelesen = _quelle.Read(puffer, offset, anzahl);
        if (gelesen <= 0) return gelesen;
        _messer.Fuettern(puffer.AsSpan(offset, gelesen));

        var ziel = !An ? 0
            : Gemessen is { } lufs ? Verstaerkung(lufs, _messer.Spitze)
            : _vorgabe is { } bekannt ? Verstaerkung(bekannt, 0)
            : 0;

        // Höchstens ein dB je Sekunde, verteilt über die Abtastwerte dieses
        // Stücks – so gibt es keine Stufe, die man als Knacken hört.
        var sekunden = (double)gelesen / WaveFormat.Channels / WaveFormat.SampleRate;
        var schritt = Math.Clamp(ziel - _dbJetzt, -SchrittJeSekunde * sekunden, SchrittJeSekunde * sekunden);
        var von = Faktor(_dbJetzt);
        _dbJetzt += schritt;
        var bis = Faktor(_dbJetzt);

        if (von == 1f && bis == 1f) return gelesen;
        for (var i = 0; i < gelesen; i++)
        {
            var faktor = von + (bis - von) * i / gelesen;
            puffer[offset + i] = Math.Clamp(puffer[offset + i] * faktor, -1f, 1f);
        }
        return gelesen;
    }

    private static float Faktor(double db) => db == 0 ? 1f : (float)Math.Pow(10, db / 20);
}

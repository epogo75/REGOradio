using NAudio.Wave;

using REGOradio.Ton;

using Xunit;

namespace REGOradio.Pruefung;

/// <summary>
/// „Alle Sender gleich laut" (Bau 21). Die Messung wird gegen die Sollwerte
/// der EBU geprüft (Tech 3341), nicht gegen die eigene Rechnung.
/// </summary>
public class LautheitPruefung
{
    /// <summary>Ein Sinus, Stereo, mit gegebenem Pegel in dBFS.</summary>
    private static float[] Sinus(int abtastrate, double frequenz, double dbfs, double sekunden)
    {
        var rahmen = (int)(abtastrate * sekunden);
        var werte = new float[rahmen * 2];
        var hoehe = Math.Pow(10, dbfs / 20);
        for (var i = 0; i < rahmen; i++)
        {
            var w = (float)(hoehe * Math.Sin(2 * Math.PI * frequenz * i / abtastrate));
            werte[2 * i] = w;
            werte[2 * i + 1] = w;
        }
        return werte;
    }

    [Theory]
    [InlineData(48000)]
    [InlineData(44100)]
    public void EbuSinusMinus23(int abtastrate)
    {
        // Tech 3341, Fall 1: Stereo-Sinus 1 kHz bei −23 dBFS ergibt −23,0 LUFS (±0,1).
        var messer = new Laufmesser(abtastrate, 2);
        messer.Fuettern(Sinus(abtastrate, 1000, -23, 20));
        Assert.InRange(messer.Lufs!.Value, -23.1, -22.9);
    }

    [Fact]
    public void EbuSinusMinus33()
    {
        var messer = new Laufmesser(48000, 2);
        messer.Fuettern(Sinus(48000, 1000, -33, 20));
        Assert.InRange(messer.Lufs!.Value, -33.1, -32.9);
    }

    [Fact]
    public void StilleIstNichts()
    {
        var messer = new Laufmesser(48000, 2);
        messer.Fuettern(new float[48000 * 2 * 5]);
        Assert.Null(messer.Lufs);
    }

    [Fact]
    public void PausenZiehenNichtRunter()
    {
        // Laut, dann eine lange Moderationspause bei −60: das zweite Tor wirft
        // die Pause hinaus, die Lautheit bleibt beim lauten Teil.
        var messer = new Laufmesser(48000, 2);
        messer.Fuettern(Sinus(48000, 1000, -20, 20));
        messer.Fuettern(Sinus(48000, 1000, -60, 20));
        Assert.InRange(messer.Lufs!.Value, -20.3, -19.7);
    }

    [Fact]
    public void NurDieLetztenFuenfMinutenZaehlen()
    {
        var messer = new Laufmesser(8000, 1);
        messer.Fuettern(new float[8000 * 400]);   // 400 s
        Assert.Equal(Laufmesser.Fensterbloecke, messer.Bloecke);
    }

    [Fact]
    public void VerstaerkungMitGrenzen()
    {
        Assert.Equal(-6, Angleicher.Verstaerkung(-11, 0), 3);         // zu laut: ab
        Assert.Equal(0.3, Angleicher.Verstaerkung(-17.3, 0), 3);      // üblich: fast nichts
        Assert.Equal(Angleicher.HoechstensAb, Angleicher.Verstaerkung(-0.5, 0), 3);
        Assert.Equal(Angleicher.HoechstensAuf, Angleicher.Verstaerkung(-30, 0), 3);
        // Leise, aber mit Spitzen bei −2 dBFS: höchstens bis 0,5 dB unter null.
        Assert.Equal(1.5, Angleicher.Verstaerkung(-23, (float)Math.Pow(10, -2.0 / 20)), 3);
    }

    /// <summary>Ein Sender als Tonquelle, der endlos denselben Sinus schickt.</summary>
    private sealed class Sinusquelle(int abtastrate, double dbfs) : ISampleProvider
    {
        private long _i;
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(abtastrate, 2);
        public int Read(float[] puffer, int offset, int anzahl)
        {
            var hoehe = Math.Pow(10, dbfs / 20);
            for (var n = 0; n < anzahl; n += 2, _i++)
            {
                var w = (float)(hoehe * Math.Sin(2 * Math.PI * 1000 * _i / abtastrate));
                puffer[offset + n] = w;
                puffer[offset + n + 1] = w;
            }
            return anzahl;
        }
    }

    private static void Lesen(ISampleProvider quelle, double sekunden)
    {
        var puffer = new float[4800];
        var gesamt = (long)(quelle.WaveFormat.SampleRate * 2 * sekunden);
        for (long n = 0; n < gesamt; n += puffer.Length) quelle.Read(puffer, 0, puffer.Length);
    }

    [Fact]
    public void LauterSenderWirdLangsamLeiser()
    {
        // −11 LUFS (Sinus bei −11 dBFS): Ziel −17, also −6 dB – in einem dB je Sekunde.
        var a = new Angleicher(new Sinusquelle(48000, -11), bekannteLufs: null);
        Lesen(a, 9);
        Assert.Equal(0, a.Dezibel, 3);           // unter zehn Sekunden: noch nichts
        Lesen(a, 3);
        Assert.InRange(a.Dezibel, -3, -0.5);     // fängt an, aber ohne Sprung
        Lesen(a, 10);
        Assert.Equal(-6, a.Dezibel, 1);
    }

    [Fact]
    public void BekannterSenderGiltSofort()
    {
        var a = new Angleicher(new Sinusquelle(48000, -11), bekannteLufs: -11);
        Assert.Equal(-6, a.Dezibel, 3);
        Lesen(a, 1);
        Assert.Equal(-6, a.Dezibel, 1);
    }

    [Fact]
    public void AusGehtZurueckAufNull()
    {
        // Mitten im Hören abgeschaltet: von −6 dB in einem dB je Sekunde zurück.
        var a = new Angleicher(new Sinusquelle(48000, -11), bekannteLufs: -11);
        a.An = false;
        Lesen(a, 3);
        Assert.InRange(a.Dezibel, -3.5, -2.5);
        Lesen(a, 5);
        Assert.Equal(0, a.Dezibel, 3);
    }

    [Fact]
    public void AusVonAnfangAnFasstNichtsAn()
    {
        var a = new Angleicher(new Sinusquelle(48000, -8), bekannteLufs: null) { An = false };
        var puffer = new float[4800];
        var vergleich = new float[4800];
        Lesen(a, 12);
        a.Read(puffer, 0, puffer.Length);
        var quelle = new Sinusquelle(48000, -8);
        var gelesen = 0L;
        while (gelesen < 48000L * 2 * 12) { quelle.Read(vergleich, 0, vergleich.Length); gelesen += vergleich.Length; }
        quelle.Read(vergleich, 0, vergleich.Length);
        Assert.Equal(vergleich, puffer);
    }
}

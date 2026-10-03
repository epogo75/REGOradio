namespace REGOradio.Ton;

/// <summary>
/// Wie laut ein Sender <b>empfunden</b> wird – nach EBU R 128 / ITU-R BS.1770
/// (Bau 21). Die Rechnung ist dieselbe wie in REGOdj (`Lautheit.cs`), nur
/// <b>gleitend</b>: Ein Radio hat kein Ende, an dem man das Ergebnis
/// abliest. Gemessen werden die letzten fünf Minuten.
///
/// **Warum nicht der Spitzenwert.** Fast alle Sender schlagen bis knapp
/// unter null aus – und trotzdem ist der eine acht Dezibel lauter als der
/// andere, weil er stärker verdichtet. Genau das ist der Sprung, den man
/// beim Umschalten hört.
///
/// **Was gemessen wird.** Die Energie hinter dem K-Filter (Kopfabschattung
/// und Hochpass) in Blöcken von 400 ms, alle 100 ms ein neuer. Stille unter
/// −70 LUFS zählt nicht, und Blöcke mehr als 10 LU unter dem Mittel auch
/// nicht – sonst machte jede Moderationspause den Sender leiser, als er ist.
///
/// Geprüft gegen die Sollwerte der EBU (Tech 3341): ein Sinus bei −23 dBFS
/// muss −23,0 LUFS ergeben. Siehe `LautheitPruefung`.
/// </summary>
public sealed class Laufmesser
{
    private const double Schrittdauer = 0.100;
    private const double Absolutschwelle = -70.0;
    private const double Relativabstand = -10.0;

    /// <summary>Fünf Minuten zu je zehn Blöcken pro Sekunde.</summary>
    public const int Fensterbloecke = 3000;

    private readonly int _kanaele;
    private readonly int _schrittlaenge;
    private readonly Kfilter[] _filter;
    private readonly double[] _summe;
    private readonly Queue<double[]> _teilstuecke = new();
    private readonly Queue<double> _bloecke = new();
    private readonly Queue<float> _spitzen = new();
    private int _imTeilstueck;
    private float _spitzeImTeil;

    public Laufmesser(int abtastrate, int kanaele)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(abtastrate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(kanaele);
        _kanaele = kanaele;
        _schrittlaenge = (int)Math.Round(abtastrate * Schrittdauer);
        _filter = Enumerable.Range(0, kanaele).Select(_ => new Kfilter(abtastrate)).ToArray();
        _summe = new double[kanaele];
    }

    /// <summary>Wie viele Blöcke im Fenster liegen – zehn je Sekunde.</summary>
    public int Bloecke => _bloecke.Count;

    private bool _spitzeVeraltet = true;
    private float _spitze;

    /// <summary>Die höchste Spitze im Fenster, 0 bis 1.</summary>
    public float Spitze
    {
        get
        {
            if (!_spitzeVeraltet) return _spitze;
            _spitzeVeraltet = false;
            return _spitze = _spitzen.Count == 0 ? 0 : _spitzen.Max();
        }
    }

    /// <summary>Abtastwerte, verschachtelt (L R L R …).</summary>
    public void Fuettern(ReadOnlySpan<float> werte)
    {
        for (var i = 0; i + _kanaele <= werte.Length; i += _kanaele)
        {
            for (var k = 0; k < _kanaele; k++)
            {
                var wert = werte[i + k];
                var betrag = Math.Abs(wert);
                if (betrag > _spitzeImTeil) _spitzeImTeil = betrag;
                var gefiltert = _filter[k].Durch(wert);
                _summe[k] += gefiltert * gefiltert;
            }
            if (++_imTeilstueck >= _schrittlaenge) Teilstueckschliessen();
        }
    }

    private void Teilstueckschliessen()
    {
        _teilstuecke.Enqueue((double[])_summe.Clone());
        Array.Clear(_summe);
        _imTeilstueck = 0;

        _spitzen.Enqueue(_spitzeImTeil);
        _spitzeImTeil = 0;
        if (_spitzen.Count > Fensterbloecke) _spitzen.Dequeue();
        _spitzeVeraltet = true;

        if (_teilstuecke.Count < 4) return;
        var energie = 0.0;
        foreach (var teil in _teilstuecke)
        {
            for (var k = 0; k < _kanaele; k++) energie += teil[k];
        }
        _teilstuecke.Dequeue();

        _bloecke.Enqueue(energie / (_schrittlaenge * 4.0));
        if (_bloecke.Count > Fensterbloecke) _bloecke.Dequeue();
        _veraltet = true;
    }

    private bool _veraltet = true;
    private double? _lufs;

    /// <summary>
    /// Die Lautheit im Fenster in LUFS, oder null, solange nichts Hörbares kam.
    /// Neu gerechnet nur, wenn ein Block dazukam – gefragt wird bei jedem
    /// Tonstück, also hundertmal je Sekunde.
    /// </summary>
    public double? Lufs
    {
        get
        {
            if (!_veraltet) return _lufs;
            _veraltet = false;
            var erste = _bloecke.Where(e => AusEnergie(e) > Absolutschwelle).ToList();
            if (erste.Count == 0) return _lufs = null;
            var schwelle = AusEnergie(erste.Average()) + Relativabstand;
            var zweite = erste.Where(e => AusEnergie(e) > schwelle).ToList();
            return _lufs = AusEnergie((zweite.Count > 0 ? zweite : erste).Average());
        }
    }

    private static double AusEnergie(double energie) =>
        energie <= 0 ? double.NegativeInfinity : -0.691 + 10 * Math.Log10(energie);

    /// <summary>
    /// Der K-Filter aus BS.1770, zwei Stufen. Die Koeffizienten werden für die
    /// tatsächliche Abtastrate gerechnet – Radiosender senden 44,1 und 48 kHz.
    /// </summary>
    private sealed class Kfilter
    {
        private const double Schulterfrequenz = 1681.974450955533;
        private const double Schulterhub = 3.999843853973347;
        private const double Schulterguete = 0.7071752369554196;
        private const double Hochpassfrequenz = 38.13547087602444;
        private const double Hochpassguete = 0.5003270373238773;

        private readonly double _a1, _a2, _b0, _b1, _b2, _c1, _c2;
        private double _x1, _x2, _y1, _y2, _u1, _u2, _v1, _v2;

        public Kfilter(int abtastrate)
        {
            var k = Math.Tan(Math.PI * Schulterfrequenz / abtastrate);
            var vh = Math.Pow(10, Schulterhub / 20);
            var vb = Math.Pow(vh, 0.4996667741545416);
            var nenner = 1 + k / Schulterguete + k * k;
            _b0 = (vh + vb * k / Schulterguete + k * k) / nenner;
            _b1 = 2 * (k * k - vh) / nenner;
            _b2 = (vh - vb * k / Schulterguete + k * k) / nenner;
            _a1 = 2 * (k * k - 1) / nenner;
            _a2 = (1 - k / Schulterguete + k * k) / nenner;

            var k2 = Math.Tan(Math.PI * Hochpassfrequenz / abtastrate);
            var nenner2 = 1 + k2 / Hochpassguete + k2 * k2;
            _c1 = 2 * (k2 * k2 - 1) / nenner2;
            _c2 = (1 - k2 / Hochpassguete + k2 * k2) / nenner2;
        }

        public double Durch(double wert)
        {
            var zwischen = _b0 * wert + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
            _x2 = _x1; _x1 = wert; _y2 = _y1; _y1 = zwischen;
            var heraus = zwischen - 2 * _u1 + _u2 - _c1 * _v1 - _c2 * _v2;
            _u2 = _u1; _u1 = zwischen; _v2 = _v1; _v1 = heraus;
            return heraus;
        }
    }
}

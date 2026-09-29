using System.IO;
using System.Text;

namespace REGOradio.Ton;

/// <summary>
/// Ein Strom, der die ICY-Metadaten herausnimmt und weitermeldet.
///
/// **Wie das Protokoll aussieht.** Wer beim Abruf `Icy-MetaData: 1` mitschickt,
/// bekommt einen Strom, in dem alle `icy-metaint` Bytes ein Metadatenblock
/// steckt: ein Längenbyte, dann so viele 16-Byte-Blöcke, darin Text der Form
/// `StreamTitle='Interpret - Titel';`. Ein Längenbyte 0 heißt: keine Änderung,
/// und das ist der Normalfall -- der Block kommt trotzdem und muss trotzdem
/// heraus.
///
/// **Wer ihn drinlässt, hört ein Knacken.** Die Metadaten sind keine
/// Tonbytes; landen sie im Dekodierer, gibt es alle paar Sekunden ein Störgeräusch
/// und zerbrochene MP3-Rahmen. Deshalb sitzt diese Schicht zwischen Netz und
/// Dekodierer und nicht danebe.
/// </summary>
public sealed class IcyStrom(Stream quelle, int metaAbstand) : Stream
{
    private readonly byte[] _metaPuffer = new byte[255 * 16];
    private readonly int _metaAbstand = metaAbstand;
    private int _bisZumBlock = metaAbstand;

    /// <summary>Was gerade läuft, wie der Sender es sagt. Leer, solange nichts kam.</summary>
    public string Titel { get; private set; } = "";

    public event Action<string>? TitelGeaendert;

    public override int Read(byte[] puffer, int ab, int anzahl)
    {
        if (_metaAbstand <= 0) return quelle.Read(puffer, ab, anzahl);

        if (_bisZumBlock == 0)
        {
            BlockLesen();
            _bisZumBlock = _metaAbstand;
        }

        // Nur bis zum nächsten Block lesen, nie darüber hinaus: Sonst landet
        // der Block mitten im Tonstrom.
        var gelesen = quelle.Read(puffer, ab, Math.Min(anzahl, _bisZumBlock));
        _bisZumBlock -= gelesen;
        return gelesen;
    }

    private void BlockLesen()
    {
        var laengenbyte = quelle.ReadByte();
        if (laengenbyte <= 0) return;   // -1: Strom zu Ende, 0: keine Änderung

        var laenge = laengenbyte * 16;
        var gelesen = 0;
        while (gelesen < laenge)
        {
            var nun = quelle.Read(_metaPuffer, gelesen, laenge - gelesen);
            if (nun <= 0) return;   // Abriss mitten im Block -- kein Titel, kein Drama
            gelesen += nun;
        }

        var titel = TitelAus(TextAus(_metaPuffer, laenge));
        if (titel.Length == 0 || titel == Titel) return;
        Titel = titel;
        TitelGeaendert?.Invoke(titel);
    }

    /// <summary>
    /// Bytes aus dem Metadatenblock zu Text machen -- und dabei die beiden
    /// Zeichensatzfallen umgehen, die bei ICY die Regel sind, nicht die
    /// Ausnahme.
    ///
    /// **Erste Falle: gar kein UTF-8.** Das ICY-Protokoll sagt nichts über den
    /// Zeichensatz. Viele Sender schicken Latin-1 (ISO-8859-1). Liest man das
    /// als UTF-8, ist das Ergebnis kein Text, sondern Ersatzzeichen: aus „Ärzte"
    /// wird „�rzte". Deshalb wird UTF-8 STRENG versucht -- mit
    /// Ausnahme bei ungültigen Bytes -- und bei Misserfolg Latin-1 genommen.
    /// Latin-1 kann nie scheitern, jedes Byte ist dort ein Zeichen.
    ///
    /// **Zweite Falle: doppelt kodiert.** Andere Sender kodieren UTF-8 und
    /// schicken es dann noch einmal durch dieselbe Mühle. Dann steht „Ã¤" da,
    /// wo „ä" stehen sollte. Das ist zuverlässig zu erkennen: Der Text lässt
    /// sich anstandslos als Latin-1 kodieren UND das Ergebnis ist gültiges
    /// UTF-8. Echter Text mit Umlauten scheitert an genau diesem Versuch,
    /// ein Emoji passt nicht einmal in ein Latin-1-Byte.
    /// </summary>
    public static string TextAus(byte[] rohe, int laenge)
    {
        var streng = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        string text;
        try
        {
            text = streng.GetString(rohe, 0, laenge);
        }
        catch (DecoderFallbackException)
        {
            return Latin1.GetString(rohe, 0, laenge);
        }
        return Entwirren(text);
    }

    private static readonly Encoding Latin1 = Encoding.Latin1;

    /// <summary>
    /// Doppelt kodierten Text geradebiegen. Im Zweifel bleibt der Text, wie er
    /// ist: Lieber einmal „Ã¤" anzeigen als aus richtigem Text Unsinn machen.
    /// </summary>
    public static string Entwirren(string text)
    {
        if (text.Length == 0) return text;
        // Zeichen jenseits von Latin-1 kommen in doppelt kodiertem Text nicht
        // vor -- dort ist jedes Zeichen ursprünglich ein Byte gewesen.
        foreach (var zeichen in text)
        {
            if (zeichen > 0xFF) return text;
        }
        try
        {
            var bytes = Latin1.GetBytes(text);
            var streng = new UTF8Encoding(false, throwOnInvalidBytes: true);
            var entwirrt = streng.GetString(bytes);
            // Nur übernehmen, wenn es dabei tatsächlich kürzer wurde: Reiner
            // ASCII-Text geht unverändert durch beide Schritte, und dann gibt es
            // nichts zu entwirren.
            return entwirrt.Length < text.Length ? entwirrt : text;
        }
        catch (DecoderFallbackException)
        {
            return text;
        }
    }

    /// <summary>
    /// Den Titel aus einem Metadatenblock holen.
    ///
    /// Reine Funktion, und deshalb die Stelle, an der geprüft wird: Sender
    /// schreiben hier alles hinein -- Hochkommas im Titel, leere Angaben,
    /// mehrere Felder hintereinander, Auffüllnullen am Ende.
    /// </summary>
    public static string TitelAus(string block)
    {
        const string schluessel = "StreamTitle='";
        var anfang = block.IndexOf(schluessel, StringComparison.Ordinal);
        if (anfang < 0) return "";
        anfang += schluessel.Length;

        // Bis zum abschließenden `';` -- NICHT bis zum ersten Hochkomma: In
        // „Guns N' Roses" steht eines mitten im Namen, und wer dort abschneidet,
        // zeigt „Guns N" an.
        var ende = block.IndexOf("';", anfang, StringComparison.Ordinal);
        if (ende < 0) ende = block.IndexOf('\'', anfang);
        if (ende < 0) ende = block.Length;

        return block[anfang..ende].Trim().Trim('\0');
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
    public override void Flush() { }
    public override long Seek(long versatz, SeekOrigin woher) => throw new NotSupportedException();
    public override void SetLength(long laenge) => throw new NotSupportedException();
    public override void Write(byte[] puffer, int ab, int anzahl) => throw new NotSupportedException();

    protected override void Dispose(bool verwaltet)
    {
        if (verwaltet) quelle.Dispose();
        base.Dispose(verwaltet);
    }
}

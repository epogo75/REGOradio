using System.Text;

using REGOradio.Ton;

using Xunit;

namespace REGOradio.Pruefung;

/// <summary>
/// Die Zeichensatzfallen in ICY-Titeln: Latin-1, das sich als UTF-8 ausgibt, und
/// doppelt kodiertes UTF-8.
/// </summary>
public class ZeichensatzPruefung
{
    [Fact]
    public void RichtigesUtf8BleibtUnangetastet()
    {
        var bytes = Encoding.UTF8.GetBytes("StreamTitle='Die Ärzte - Schrei nach Liebe';");
        Assert.Equal("StreamTitle='Die Ärzte - Schrei nach Liebe';",
            IcyStrom.TextAus(bytes, bytes.Length));
    }

    [Fact]
    public void Latin1WirdErkannt()
    {
        // Das ist der Normalfall bei vielen älteren Sendern: „Ä" als ein Byte
        // 0xC4. Als UTF-8 gelesen ist das ungültig -- und würde zum
        // Ersatzzeichen „�".
        var bytes = Encoding.Latin1.GetBytes("StreamTitle='Die Ärzte - Männer sind Schweine';");
        var text = IcyStrom.TextAus(bytes, bytes.Length);
        Assert.Contains("Ärzte", text);
        Assert.Contains("Männer", text);
        Assert.DoesNotContain("�", text);
    }

    [Fact]
    public void DoppeltKodiertWirdEntwirrt()
    {
        // „Ä" zweimal durch UTF-8 geschickt ergibt „Ã„" -- so stand es am Pi
        // in der Anzeige, bevor das entwirrt wurde.
        var kaputt = Encoding.Latin1.GetString(Encoding.UTF8.GetBytes("Fußballplätze"));
        Assert.Equal("Fußballplätze", IcyStrom.Entwirren(kaputt));
    }

    [Fact]
    public void EmojiBleibtStehen()
    {
        // Ein Emoji passt in kein Latin-1-Byte. Der Entwirrversuch muss daran
        // scheitern und den Text lassen, wie er ist.
        Assert.Equal("Sommerhits 🎵", IcyStrom.Entwirren("Sommerhits 🎵"));
    }

    [Fact]
    public void ReinesAsciiBleibt()
    {
        Assert.Equal("Radio Paradise", IcyStrom.Entwirren("Radio Paradise"));
    }

    [Fact]
    public void EchteUmlauteWerdenNichtVerschlimmbessert()
    {
        // „ä" allein ist gültiges Latin-1, aber 0xE4 ist kein gültiges UTF-8.
        // Der Versuch muss scheitern und „ä" stehen lassen.
        Assert.Equal("Männer", IcyStrom.Entwirren("Männer"));
    }
}

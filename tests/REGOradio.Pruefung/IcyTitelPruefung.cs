using REGOradio.Ton;

using Xunit;

namespace REGOradio.Pruefung;

/// <summary>
/// Der Titel aus dem Metadatenblock. Reine Rechenarbeit, und die Stelle, an der
/// Sender ihre Eigenheiten zeigen.
/// </summary>
public class IcyTitelPruefung
{
    [Fact]
    public void GewoehnlicherBlock()
    {
        Assert.Equal("Billy Idol - Flesh for Fantasy",
            IcyStrom.TitelAus("StreamTitle='Billy Idol - Flesh for Fantasy';StreamUrl='';"));
    }

    [Fact]
    public void HochkommaImNamenUeberlebt()
    {
        // Hier scheitert jeder Versuch, bis zum ERSTEN Hochkomma zu lesen:
        // Angezeigt würde „Guns N".
        Assert.Equal("Guns N' Roses - Paradise City",
            IcyStrom.TitelAus("StreamTitle='Guns N' Roses - Paradise City';StreamUrl='';"));
    }

    [Fact]
    public void AuffuellnullenFallenWeg()
    {
        // Der Block ist immer ein Vielfaches von 16 Bytes; der Rest wird mit
        // Nullbytes gefüllt. Bleiben sie stehen, hängt an jedem Titel
        // unsichtbarer Müll -- und der Vergleich „hat sich der Titel geändert"
        // schlägt bei jedem Block an.
        Assert.Equal("SWR3", IcyStrom.TitelAus("StreamTitle='SWR3';\0\0\0\0\0"));
    }

    [Fact]
    public void LeererTitelGiltAlsKeiner()
    {
        Assert.Equal("", IcyStrom.TitelAus("StreamTitle='';StreamUrl='';"));
    }

    [Fact]
    public void BlockOhneTitelfeld()
    {
        Assert.Equal("", IcyStrom.TitelAus("StreamUrl='https://example.invalid';"));
    }

    [Fact]
    public void OhneAbschlussZeichen()
    {
        // Abgeschnittener Block -- kommt bei einem Abriss mitten im Lesen vor.
        Assert.Equal("Radio Paradise", IcyStrom.TitelAus("StreamTitle='Radio Paradise"));
    }
}

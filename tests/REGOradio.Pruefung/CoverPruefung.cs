using REGOradio.Katalog;

using Xunit;

namespace REGOradio.Pruefung;

/// <summary>Die reinen Teile der Coversuche.</summary>
public class CoverPruefung
{
    [Fact]
    public void InterpretUndTitel()
    {
        Assert.Equal(("Milli Vanilli", "Girl You Know It's True"), Cover.Zerlege("Milli Vanilli - Girl You Know It's True"));
    }

    [Fact]
    public void BindestrichImNamenTrenntNicht()
    {
        Assert.Equal(("Jean-Michel Jarre", "Oxygène"), Cover.Zerlege("Jean-Michel Jarre - Oxygène"));
        Assert.Equal(("", "Jean-Michel Jarre"), Cover.Zerlege("Jean-Michel Jarre"));
    }

    [Fact]
    public void GedankenstrichTrenntAuch()
    {
        Assert.Equal(("Nena", "99 Luftballons"), Cover.Zerlege("Nena – 99 Luftballons"));
    }

    [Fact]
    public void DurchsageHatKeinenInterpreten()
    {
        // Genau das soll NICHT bei einem fremden Dienst landen.
        Assert.Equal("", Cover.Zerlege("Kontakt zu SWR3: info@swr3.de").Interpret);
    }

    [Fact]
    public void BeiwerkFaelltWeg()
    {
        Assert.Equal("Queen Bohemian Rhapsody", Cover.Suchbegriff("Queen", "Bohemian Rhapsody (Remastered 2011)"));
        Assert.Equal("a-ha Take On Me", Cover.Suchbegriff("a-ha", "Take On Me (Radio Edit)"));
    }

    [Fact]
    public void ZweiterAnlaufOhneKlammerzusatz()
    {
        // Am Notebook gesehen: Radio Paradise meldete den Titel mit
        // „(Bandcamp Version)", und die Suche fand nichts. Der Zusatz steht in
        // keiner Beiwerk-Liste; der zweite Anlauf ohne Klammer fängt ihn.
        Assert.Equal(
            ["Peter Gabriel Till Your Mind Is Shining (Bandcamp Version)", "Peter Gabriel Till Your Mind Is Shining"],
            Cover.Suchbegriffe("Peter Gabriel", "Till Your Mind Is Shining (Bandcamp Version)"));
    }

    [Fact]
    public void OhneKlammerKeinZweiterAnlauf()
    {
        Assert.Equal(["Nena 99 Luftballons"], Cover.Suchbegriffe("Nena", "99 Luftballons"));
    }

    [Fact]
    public void BildWirdGross()
    {
        Assert.Equal("https://is1.example/abc/600x600bb.jpg", Cover.Gross("https://is1.example/abc/100x100bb.jpg"));
        Assert.Equal("https://anders.example/bild.jpg", Cover.Gross("https://anders.example/bild.jpg"));
    }
}

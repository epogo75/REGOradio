using REGOradio.Modelle;

using Xunit;

namespace REGOradio.Pruefung;

public class TastenbelegungPruefung
{
    private static List<Sender> Belegung() =>
    [
        new() { Name = "80s80s", Platz = 1 },
        new() { Name = "Schwarzwaldradio", Platz = 2 },
        new() { Name = "SWR3", Platz = 4 },
    ];

    private static string? Auf(List<Sender> liste, int platz) => liste.FirstOrDefault(s => s.Platz == platz)?.Name;

    [Fact]
    public void ZweiBelegteTastenTauschen()
    {
        var liste = Belegung();
        Assert.True(Tastenbelegung.Tauschen(liste, 4, 1));
        Assert.Equal("SWR3", Auf(liste, 1));
        Assert.Equal("80s80s", Auf(liste, 4));
        // Unbeteiligte Tasten bleiben, wo sie sind -- genau das ist der Grund
        // für Tauschen statt Einschieben.
        Assert.Equal("Schwarzwaldradio", Auf(liste, 2));
    }

    [Fact]
    public void AufFreieTasteVerschieben()
    {
        var liste = Belegung();
        Assert.True(Tastenbelegung.Tauschen(liste, 2, 9));
        Assert.Equal("Schwarzwaldradio", Auf(liste, 9));
        Assert.Null(Auf(liste, 2));
    }

    [Fact]
    public void FreieTasteLaesstSichNichtZiehen()
    {
        var liste = Belegung();
        Assert.False(Tastenbelegung.Tauschen(liste, 3, 1));
        Assert.Equal("80s80s", Auf(liste, 1));
    }

    [Fact]
    public void AufSichSelbstAendertNichts()
    {
        Assert.False(Tastenbelegung.Tauschen(Belegung(), 1, 1));
    }
}

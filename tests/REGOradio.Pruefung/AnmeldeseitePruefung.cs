using REGOradio.Netz;

using Xunit;

namespace REGOradio.Pruefung;

/// <summary>Hotel-WLAN erkennen (Bau 22).</summary>
public class AnmeldeseitePruefung
{
    [Fact]
    public void FreiesNetz()
    {
        Assert.Equal(Netzzugang.Frei, Anmeldeseite.Deuten(200, null, "Microsoft Connect Test", false).Art);
        // Windows' Zweifel zählt nicht, wenn die Prüfadresse richtig antwortet.
        Assert.Equal(Netzzugang.Frei, Anmeldeseite.Deuten(200, null, "Microsoft Connect Test\n", true).Art);
    }

    [Fact]
    public void UmleitungZurAnmeldung()
    {
        var befund = Anmeldeseite.Deuten(302, "https://hotspot.hotel-beispiel.de/login?ziel=x", "", false);
        Assert.Equal(Netzzugang.Anmeldung, befund.Art);
        Assert.Equal("https://hotspot.hotel-beispiel.de/login?ziel=x", befund.Anmeldeadresse);
    }

    [Fact]
    public void FremdeSeiteStattDesSatzes()
    {
        // Manche Anmeldeseiten antworten 200 mit ihrem eigenen HTML.
        var befund = Anmeldeseite.Deuten(200, null, "<html><title>WLAN-Anmeldung</title>", false);
        Assert.Equal(Netzzugang.Anmeldung, befund.Art);
        Assert.Equal(Anmeldeseite.Umweg, befund.Anmeldeadresse);
    }

    [Fact]
    public void KeineAntwort()
    {
        Assert.Equal(Netzzugang.KeinNetz, Anmeldeseite.Deuten(0, null, "", false).Art);
        // Windows meldet eingeschränkten Zugang: eher eine Anmeldung als gar kein Netz.
        Assert.Equal(Netzzugang.Anmeldung, Anmeldeseite.Deuten(0, null, "", true).Art);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    [InlineData(null)]
    public void UnbrauchbareUmleitungFuehrtZumUmweg(string? ziel)
    {
        var befund = Anmeldeseite.Deuten(302, ziel, "", false);
        Assert.Equal(Netzzugang.Anmeldung, befund.Art);
        Assert.Equal(Anmeldeseite.Umweg, befund.Anmeldeadresse);
    }
}

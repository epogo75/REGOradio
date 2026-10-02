using REGOradio.Speicher;

using Xunit;

namespace REGOradio.Pruefung;

/// <summary>Was passiert beim Schließen des Fensters?</summary>
public class SchliessregelPruefung
{
    [Fact]
    public void FragenStichtDieGespeicherteWahl()
    {
        Assert.Equal(Schliessart.Fragen, Schliessregel.Entscheiden(fragen: true, imTray: true));
        Assert.Equal(Schliessart.Fragen, Schliessregel.Entscheiden(fragen: true, imTray: false));
    }

    [Fact]
    public void OhneFrageGiltDieGespeicherteWahl()
    {
        Assert.Equal(Schliessart.Tray, Schliessregel.Entscheiden(fragen: false, imTray: true));
        Assert.Equal(Schliessart.Beenden, Schliessregel.Entscheiden(fragen: false, imTray: false));
    }

    [Fact]
    public void AnfangsWirdGefragt()
    {
        // Auch mit einer Einstellungsdatei aus Bau 10, die das Feld nicht
        // kennt: Der Leser lässt es dann auf seiner Vorgabe stehen.
        Assert.True(new Einstellungen().SchliessenFragen);
    }
}

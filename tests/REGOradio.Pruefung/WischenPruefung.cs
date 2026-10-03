using REGOradio.Modelle;

using Xunit;

namespace REGOradio.Pruefung;

/// <summary>Wischen im Feld „Läuft" (Bau 23).</summary>
public class WischenPruefung
{
    [Fact]
    public void NachLinksIstWeiter() => Assert.Equal(1, Wischen.Deuten(-120, 10));

    [Fact]
    public void NachRechtsIstZurueck() => Assert.Equal(-1, Wischen.Deuten(120, -10));

    [Theory]
    [InlineData(5, 3)]       // ein Tipp, der wackelt
    [InlineData(-60, 0)]     // zu kurz
    [InlineData(-90, 80)]    // schräg nach unten, zur Lautstärke
    [InlineData(0, 200)]     // senkrecht
    public void KeinWechsel(double dx, double dy) => Assert.Equal(0, Wischen.Deuten(dx, dy));

    [Fact]
    public void FeldFolgtErstAbZwoelfPunkten()
    {
        Assert.False(Wischen.Waagerecht(8, 0));
        Assert.True(Wischen.Waagerecht(-20, 5));
        Assert.False(Wischen.Waagerecht(20, 20));
    }
}

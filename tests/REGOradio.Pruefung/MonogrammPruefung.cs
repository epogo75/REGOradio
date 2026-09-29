using REGOradio.Katalog;

using Xunit;

namespace REGOradio.Pruefung;

public class MonogrammPruefung
{
    [Theory]
    [InlineData("Bayern 3", "B3")]
    [InlineData("NDR 2", "N2")]
    [InlineData("SWR3", "S3")]
    [InlineData("FluxFM", "FL")]
    [InlineData("Radio Paradise", "RP")]
    [InlineData("Deutschlandfunk", "DE")]
    [InlineData("BBC Radio 2", "B2")]
    [InlineData("Antenne Bayern", "AB")]
    [InlineData("  ", "?")]
    public void Kuerzel(string name, string erwartet) => Assert.Equal(erwartet, Monogramm.Kuerzel(name));

    [Fact]
    public void FarbeIstBeiJedemStartDieselbe()
    {
        // string.GetHashCode wäre hier falsch: .NET mischt ihn bei jedem Start
        // neu, und die Taste hätte morgen eine andere Farbe.
        Assert.Equal(Monogramm.Farbe("SWR3"), Monogramm.Farbe("SWR3"));
        Assert.Equal(Monogramm.Farbe("swr3"), Monogramm.Farbe(" SWR3 "));
    }
}

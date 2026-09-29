using REGOradio.Uhr;

using Xunit;

namespace REGOradio.Pruefung;

/// <summary>
/// Trifft jedes Wort der Wortuhr wirklich die gemeinten Buchstaben?
///
/// Beim ersten Entwurf hat genau diese Prüfung vier Wörter gefunden, die eine
/// Spalte daneben saßen -- auf dem Schirm stand „FÜNF ACH".
/// </summary>
public class WortuhrPruefung
{
    [Fact]
    public void JedeZeileHatElfBuchstaben()
    {
        Assert.Equal(10, Wortuhr.Feld.Length);
        Assert.All(Wortuhr.Feld, zeile => Assert.Equal(11, zeile.Length));
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(5, "FÜNF NACH")]
    [InlineData(10, "ZEHN NACH")]
    [InlineData(15, "VIERTEL NACH")]
    [InlineData(20, "ZWANZIG NACH")]
    [InlineData(25, "FÜNF VOR HALB")]
    [InlineData(30, "HALB")]
    [InlineData(35, "FÜNF NACH HALB")]
    [InlineData(40, "ZWANZIG VOR")]
    [InlineData(45, "VIERTEL VOR")]
    [InlineData(50, "ZEHN VOR")]
    [InlineData(55, "FÜNF VOR")]
    public void Minutenstufen(int stufe, string soll) =>
        Assert.Equal(soll, string.Join(" ", Wortuhr.Minuten[stufe].Select(Wortuhr.Lesen)));

    [Theory]
    [InlineData(1, "EINS")]
    [InlineData(2, "ZWEI")]
    [InlineData(3, "DREI")]
    [InlineData(4, "VIER")]
    [InlineData(5, "FÜNF")]
    [InlineData(6, "SECHS")]
    [InlineData(7, "SIEBEN")]
    [InlineData(8, "ACHT")]
    [InlineData(9, "NEUN")]
    [InlineData(10, "ZEHN")]
    [InlineData(11, "ELF")]
    [InlineData(12, "ZWÖLF")]
    public void Stunden(int stunde, string soll) =>
        Assert.Equal(soll, string.Join(" ", Wortuhr.Stunden[stunde].Select(Wortuhr.Lesen)));

    [Fact]
    public void FesteWoerter()
    {
        Assert.Equal("ES IST", string.Join(" ", Wortuhr.EsIst.Select(Wortuhr.Lesen)));
        Assert.Equal("EIN", string.Join(" ", Wortuhr.EinKurz.Select(Wortuhr.Lesen)));
        Assert.Equal("UHR", string.Join(" ", Wortuhr.UhrWort.Select(Wortuhr.Lesen)));
    }

    [Theory]
    [InlineData(13, 0, "ES IST EIN UHR")]
    [InlineData(1, 5, "ES IST FÜNF NACH EINS")]
    [InlineData(11, 25, "ES IST FÜNF VOR HALB ZWÖLF")]
    [InlineData(23, 29, "ES IST FÜNF VOR HALB ZWÖLF")]
    [InlineData(12, 45, "ES IST VIERTEL VOR EINS")]
    [InlineData(0, 0, "ES IST ZWÖLF UHR")]
    [InlineData(16, 30, "ES IST HALB FÜNF")]
    [InlineData(21, 14, "ES IST ZEHN NACH NEUN")]
    public void GanzeSaetze(int stunde, int minute, string soll) =>
        Assert.Equal(soll, Wortuhr.Satz(new DateTime(2026, 9, 28, stunde, minute, 0)));
}

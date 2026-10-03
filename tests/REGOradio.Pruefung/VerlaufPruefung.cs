using REGOradio.Speicher;

using Xunit;

namespace REGOradio.Pruefung;

/// <summary>„Was lief vorhin?" (Bau 18).</summary>
public class VerlaufPruefung
{
    private static readonly DateTime Jetzt = new(2026, 10, 3, 14, 32, 0);

    [Fact]
    public void NeuestesObenAn()
    {
        var liste = new List<Gespielt>();
        Verlauf.Eintragen(liste, "SWR3", "Nena", "99 Luftballons", Jetzt);
        Verlauf.Eintragen(liste, "SWR3", "Queen", "Bohemian Rhapsody", Jetzt.AddMinutes(4));
        Assert.Equal(["Bohemian Rhapsody", "99 Luftballons"], liste.Select(l => l.Titel));
    }

    [Fact]
    public void DerselbeTitelImTaktNurEinmal()
    {
        var liste = new List<Gespielt>();
        Assert.True(Verlauf.Eintragen(liste, "SWR3", "Nena", "99 Luftballons", Jetzt));
        Assert.False(Verlauf.Eintragen(liste, "SWR3", "nena", "99 luftballons ", Jetzt.AddSeconds(20)));
        Assert.Single(liste);
    }

    [Fact]
    public void AndererSenderGleichesLiedIstNeu()
    {
        var liste = new List<Gespielt>();
        Verlauf.Eintragen(liste, "SWR3", "Nena", "99 Luftballons", Jetzt);
        Assert.True(Verlauf.Eintragen(liste, "SWR1", "Nena", "99 Luftballons", Jetzt.AddMinutes(1)));
    }

    [Theory]
    [InlineData("", "Kontakt zu SWR3: info@swr3.de")]
    [InlineData("Nena", "")]
    public void OhneInterpretOderTitelKeinEintrag(string interpret, string titel)
    {
        var liste = new List<Gespielt>();
        Assert.False(Verlauf.Eintragen(liste, "SWR3", interpret, titel, Jetzt));
        Assert.Empty(liste);
    }

    [Fact]
    public void HoechstensFuenfzig()
    {
        var liste = new List<Gespielt>();
        for (var i = 0; i < 60; i++) Verlauf.Eintragen(liste, "SWR3", "Band", $"Lied {i}", Jetzt.AddMinutes(i));
        Assert.Equal(Verlauf.Hoechstens, liste.Count);
        Assert.Equal("Lied 59", liste[0].Titel);
        Assert.Equal("Lied 10", liste[^1].Titel);
    }

    [Fact]
    public void CoverNurZumSelbenLied()
    {
        var liste = new List<Gespielt>();
        Verlauf.Eintragen(liste, "SWR3", "Nena", "99 Luftballons", Jetzt);
        Assert.False(Verlauf.CoverNachtragen(liste, "Queen", "Bohemian Rhapsody", "https://cover/q.jpg"));
        Assert.True(Verlauf.CoverNachtragen(liste, "Nena", "99 Luftballons", "https://cover/n.jpg"));
        Assert.Equal("https://cover/n.jpg", liste[0].Cover);
        // Ein zweites Cover ersetzt das erste nicht.
        Assert.False(Verlauf.CoverNachtragen(liste, "Nena", "99 Luftballons", "https://cover/anders.jpg"));
    }

    [Fact]
    public void Uhrzeiten()
    {
        Assert.Equal("14:05", Verlauf.Wann(new DateTime(2026, 10, 3, 14, 5, 0), Jetzt));
        Assert.Equal("gestern 22:10", Verlauf.Wann(new DateTime(2026, 10, 2, 22, 10, 0), Jetzt));
        Assert.Equal("Mi 30.9. 08:00", Verlauf.Wann(new DateTime(2026, 9, 30, 8, 0, 0), Jetzt));
    }
}

using REGOradio.Modelle;

using Xunit;

namespace REGOradio.Pruefung;

/// <summary>„Weiter" und „Zurück" auf der Medientaste (Bau 16).</summary>
public class MedientastenPruefung
{
    private static List<Sender> Tasten(params int[] plaetze) =>
        plaetze.Select(p => new Sender { Name = $"S{p}", Platz = p }).ToList();

    [Fact]
    public void WeiterSpringtUeberFreieTasten()
    {
        Assert.Equal(5, Tastenbelegung.Nachbar(Tasten(1, 2, 5, 8), 2, 1)!.Platz);
    }

    [Fact]
    public void WeiterNachDerLetztenIstDieErste()
    {
        Assert.Equal(1, Tastenbelegung.Nachbar(Tasten(1, 2, 5, 8), 8, 1)!.Platz);
    }

    [Fact]
    public void ZurueckVorDerErstenIstDieLetzte()
    {
        Assert.Equal(8, Tastenbelegung.Nachbar(Tasten(1, 2, 5, 8), 1, -1)!.Platz);
        Assert.Equal(2, Tastenbelegung.Nachbar(Tasten(1, 2, 5, 8), 5, -1)!.Platz);
    }

    [Fact]
    public void OhneTasteBeginntWeiterVorn()
    {
        // Platz 0: nichts lief, oder ein Sender nur zum Anhören.
        Assert.Equal(2, Tastenbelegung.Nachbar(Tasten(2, 7), 0, 1)!.Platz);
        Assert.Equal(7, Tastenbelegung.Nachbar(Tasten(2, 7), 0, -1)!.Platz);
    }

    [Fact]
    public void AuchVonEinerFreienTasteAus()
    {
        // Die laufende Taste wurde inzwischen geräumt.
        Assert.Equal(5, Tastenbelegung.Nachbar(Tasten(1, 5), 3, 1)!.Platz);
        Assert.Equal(1, Tastenbelegung.Nachbar(Tasten(1, 5), 3, -1)!.Platz);
    }

    [Fact]
    public void OhneBelegteTastenNichts()
    {
        Assert.Null(Tastenbelegung.Nachbar(Tasten(0, 0), 0, 1));
        Assert.Null(Tastenbelegung.Nachbar([], 3, -1));
    }
}

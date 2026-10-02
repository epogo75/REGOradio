using Xunit;

namespace REGOradio.Pruefung;

/// <summary>
/// Läuft REGOradio nur einmal, und holt ein zweiter Start den ersten nach vorn?
///
/// Jede Prüfung nimmt einen eigenen Namen. Mit dem echten Namen käme sie einem
/// laufenden REGOradio in die Quere – oder würde an ihm scheitern.
/// </summary>
public class EinzelstartPruefung
{
    private static string Neuername() => "REGOradio-Pruefung-" + Guid.NewGuid().ToString("N");

    [Fact]
    public void ZweiterStartWirdErkannt()
    {
        var name = Neuername();
        using var erster = Einzelstart.Versuchen(name);
        using var zweiter = Einzelstart.Versuchen(name);

        Assert.True(erster.Erster);
        Assert.False(zweiter.Erster);
    }

    [Fact]
    public void ZweiterStartWecktDenErsten()
    {
        var name = Neuername();
        using var geweckt = new ManualResetEventSlim();
        using var erster = Einzelstart.Versuchen(name);
        erster.Lauschen(() => geweckt.Set());

        using var zweiter = Einzelstart.Versuchen(name);
        zweiter.Wecken();

        Assert.True(geweckt.Wait(TimeSpan.FromSeconds(5)), "Der erste Start wurde nicht geweckt.");
    }

    [Fact]
    public void NachDemEndeIstDerNaechsteWiederDerErste()
    {
        // Sonst ließe sich REGOradio nach dem Beenden nicht wieder starten,
        // bis Windows neu startet.
        var name = Neuername();
        using (var vorher = Einzelstart.Versuchen(name))
        {
            Assert.True(vorher.Erster);
        }

        using var nachher = Einzelstart.Versuchen(name);
        Assert.True(nachher.Erster);
    }

    [Fact]
    public void EinZweiterStartLauschtNicht()
    {
        // Lauschen gehört dem ersten. Ein zweiter, der aus Versehen lauscht,
        // fängt das eigene Wecken ab, bevor es beim ersten ankommt.
        var name = Neuername();
        using var erster = Einzelstart.Versuchen(name);
        using var zweiter = Einzelstart.Versuchen(name);
        var aufgerufen = false;

        zweiter.Lauschen(() => aufgerufen = true);
        zweiter.Wecken();
        Thread.Sleep(200);

        Assert.False(aufgerufen);
    }
}

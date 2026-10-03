using REGOradio.Katalog;
using REGOradio.Modelle;

using Xunit;

namespace REGOradio.Pruefung;

/// <summary>Welche Ersatzadressen ein toter Sender bekommt (Bau 17).</summary>
public class SenderheilungPruefung
{
    private static readonly Sender Kaputt = new()
    {
        Kennung = "k1", Name = "Schwarzwaldradio", Adresse = "https://alt.example/stream", Platz = 3,
    };

    private static Treffer T(string kennung, string name, string adresse) =>
        new() { Kennung = kennung, Name = name, Adresse = adresse };

    [Fact]
    public void ErstDerUmzugDannGleicherName()
    {
        var liste = Senderheilung.Kandidaten(Kaputt,
            [T("k1", "Schwarzwaldradio", "https://neu.example/live")],
            [T("k2", "Schwarzwaldradio", "https://zwei.example/mp3"), T("k3", "schwarzwaldradio ", "https://drei.example/aac")]);

        Assert.Equal(["https://neu.example/live", "https://zwei.example/mp3", "https://drei.example/aac"],
            liste.Select(t => t.Adresse));
    }

    [Fact]
    public void DieAlteAdresseIstKeinErsatz()
    {
        // Das Verzeichnis kennt oft noch dieselbe tote Adresse – auch anders geschrieben.
        var liste = Senderheilung.Kandidaten(Kaputt,
            [T("k1", "Schwarzwaldradio", "https://alt.example/stream/")],
            [T("k2", "Schwarzwaldradio", "https://ALT.example/stream"), T("k3", "Schwarzwaldradio", "https://zwei.example/mp3")]);

        Assert.Equal(["https://zwei.example/mp3"], liste.Select(t => t.Adresse));
    }

    [Fact]
    public void AehnlicheNamenZaehlenNicht()
    {
        var liste = Senderheilung.Kandidaten(
            new Sender { Name = "SWR3", Adresse = "https://alt/" },
            [],
            [T("a", "SWR3 Lounge", "https://lounge/"), T("b", "SWR3", "https://swr3/")]);

        Assert.Equal(["https://swr3/"], liste.Select(t => t.Adresse));
    }

    [Fact]
    public void FremdeKennungIstKeinUmzug()
    {
        var liste = Senderheilung.Kandidaten(Kaputt, [T("anders", "Irgendwas", "https://x/")], []);
        Assert.Empty(liste);
    }

    [Fact]
    public void HoechstensVier()
    {
        var viele = Enumerable.Range(1, 9).Select(i => T($"k{i + 10}", "Schwarzwaldradio", $"https://s{i}/")).ToList();
        Assert.Equal(Senderheilung.Hoechstens, Senderheilung.Kandidaten(Kaputt, [], viele).Count);
    }

    [Fact]
    public void UebernehmenBehaeltNameUndPlatz()
    {
        var taste = new Sender { Name = "Mein Radio", Adresse = "https://alt/", Platz = 2, Logo = "https://logo/" };
        Senderheilung.Uebernehmen(taste, new Treffer
        {
            Kennung = "k9", Name = "Anderer Name", Adresse = "https://neu/", Logo = "https://anderes-logo/",
            Homepage = "https://radio.de/", Codec = "AAC", Bitrate = 64,
        });

        Assert.Equal("Mein Radio", taste.Name);
        Assert.Equal(2, taste.Platz);
        Assert.Equal("https://neu/", taste.Adresse);
        Assert.Equal("k9", taste.Kennung);
        Assert.Equal("https://logo/", taste.Logo);          // das eigene Logo bleibt
        Assert.Equal("https://radio.de/", taste.Homepage);  // fehlte, kommt dazu
        Assert.Equal("AAC", taste.Codec);
        Assert.Equal(64, taste.Bitrate);
    }
}

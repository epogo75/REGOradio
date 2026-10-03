using REGOradio.Modelle;
using REGOradio.Speicher;

using Xunit;

namespace REGOradio.Pruefung;

/// <summary>Stationstasten sichern und einlesen (Bau 19).</summary>
public class SenderpaketPruefung
{
    private static readonly DateTime Jetzt = new(2026, 10, 3, 10, 0, 0);

    private static List<Sender> Tasten() =>
    [
        new() { Kennung = "k1", Name = "Schwarzwaldradio", Adresse = "https://s/1", Homepage = "https://www.schwarzwaldradio.com/", Platz = 1 },
        new() { Kennung = "k2", Name = "SWR3", Adresse = "https://s/2", Logo = "https://logo/swr3.png", Platz = 4 },
        new() { Name = "Nur angehört", Adresse = "https://s/3", Platz = 0 },
    ];

    [Fact]
    public void HinUndZurueck()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var json = Senderpaket.Packen(Tasten(),
            s => s.Platz == 4 ? ("https://logo/swr3.png", bytes) : null, Jetzt, "REGOradio/0.1.19");

        var paket = Senderpaket.Auspacken(json, 24);

        Assert.NotNull(paket);
        var (sender, logos) = paket.Value;
        Assert.Equal(["Schwarzwaldradio", "SWR3"], sender.Select(s => s.Name));   // nur, was auf Tasten liegt
        Assert.Equal([1, 4], sender.Select(s => s.Platz));
        Assert.Equal("https://www.schwarzwaldradio.com/", sender[0].Homepage);
        Assert.Equal(bytes, logos["https://logo/swr3.png"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("kein json")]
    [InlineData("""{"Art":"etwas anderes","Fassung":1,"Sender":[]}""")]
    [InlineData("""[{"Name":"SWR3","Adresse":"https://s/2","Platz":1}]""")]
    public void FremdeDateienWerdenAbgelehnt(string json)
    {
        Assert.Null(Senderpaket.Auspacken(json, 24));
    }

    [Fact]
    public void UngueltigeTastenFallenWeg()
    {
        var json = """
            {"Art":"REGOradio-Stationstasten","Fassung":1,"Sender":[
              {"Name":"Gut","Adresse":"https://a/","Platz":2},
              {"Name":"Ohne Adresse","Adresse":"","Platz":3},
              {"Name":"Zu weit","Adresse":"https://b/","Platz":99},
              {"Name":"Doppelt","Adresse":"https://c/","Platz":2},
              {"Name":"","Adresse":"https://d/","Platz":5}],
             "Logos":{"https://x/":"kein base64!"}}
            """;
        var paket = Senderpaket.Auspacken(json, 24);

        Assert.NotNull(paket);
        Assert.Equal(["Gut", "https://d/"], paket.Value.Sender.Select(s => s.Name));
        Assert.Empty(paket.Value.Logos);
    }
}

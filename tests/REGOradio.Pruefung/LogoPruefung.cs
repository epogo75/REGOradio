using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using REGOradio.Katalog;

using Xunit;

namespace REGOradio.Pruefung;

/// <summary>
/// Senderlogos und ihre Ersatzquellen (Bau 15). Kein Netz: Ein erfundener
/// Server antwortet, wie die echten es tun – DuckDuckGo mit 404 und einem
/// Platzhalterbild für unbekannte Seiten, Homepages oft mit 404.
/// </summary>
public class LogoPruefung : IDisposable
{
    private readonly string _ordner = Path.Combine(Path.GetTempPath(), "REGOradio-LogoPruefung-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_ordner, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void ErsatzVonDerHomepage()
    {
        Assert.Equal(
            [
                "https://www.schwarzwaldradio.com/apple-touch-icon.png",
                "https://icons.duckduckgo.com/ip3/www.schwarzwaldradio.com.ico",
                "https://icons.duckduckgo.com/ip3/schwarzwaldradio.com.ico",
            ],
            Logos.Ersatzadressen("https://www.schwarzwaldradio.com/"));
    }

    [Fact]
    public void OhneWwwKommtWwwDazu()
    {
        Assert.Equal(
            [
                "https://antenne.de/apple-touch-icon.png",
                "https://icons.duckduckgo.com/ip3/antenne.de.ico",
                "https://icons.duckduckgo.com/ip3/www.antenne.de.ico",
            ],
            Logos.Ersatzadressen("http://Antenne.de/programm?x=1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("keine adresse")]
    [InlineData("ftp://radio.de/")]
    [InlineData("http://192.168.1.10/")]
    [InlineData("http://localhost/")]
    public void OhneBrauchbareHomepageKeinErsatz(string homepage)
    {
        Assert.Empty(Logos.Ersatzadressen(homepage));
    }

    [Fact]
    public void HomepageKommtAusDemVerzeichnis()
    {
        var treffer = Radiobrowser.AusJson("""
            [{"stationuuid":"abc","name":"Schwarzwaldradio","url":"https://s/x","url_resolved":"https://s/x",
              "favicon":"","country":"Germany","tags":"pop","codec":"MP3","bitrate":128,
              "homepage":" https://www.schwarzwaldradio.com/ "}]
            """);
        Assert.Equal("https://www.schwarzwaldradio.com/", treffer[0].Homepage);
        Assert.Equal("https://www.schwarzwaldradio.com/", treffer[0].AlsSender().Homepage);
    }

    [Fact]
    public async Task OhneLogoKommtDasVonDuckDuckGo()
    {
        var server = new ErfundenerServer
        {
            ["https://icons.duckduckgo.com/ip3/www.schwarzwaldradio.com.ico"] = (HttpStatusCode.OK, Png(64)),
        };
        var logos = new Logos(_ordner, new HttpClient(server));

        var bild = await logos.HolenFuer("", "https://www.schwarzwaldradio.com/");

        Assert.NotNull(bild);
        // Erst die Homepage gefragt, dann DuckDuckGo – und dort aufgehört.
        Assert.Equal(
            ["https://www.schwarzwaldradio.com/",
             "https://www.schwarzwaldradio.com/apple-touch-icon.png",
             "https://icons.duckduckgo.com/ip3/www.schwarzwaldradio.com.ico"],
            server.Gefragt);
    }

    [Fact]
    public async Task DasSymbolDerHomepageGehtVorDuckDuckGo()
    {
        var server = new ErfundenerServer
        {
            ["https://icons.duckduckgo.com/ip3/www.schwarzwaldradio.com.ico"] = (HttpStatusCode.OK, Png(32)),
            ["https://www.schwarzwaldradio.com/storage/thumbs/512x512/r:1/logo.png"] = (HttpStatusCode.OK, Png(512)),
        };
        server.Seite("https://www.schwarzwaldradio.com/", """
            <html><head>
            <link rel="icon" href="/storage/thumbs/32x32/r:1/logo.png" sizes="32x32">
            <link rel="shortcut icon" href="https://www.schwarzwaldradio.com/storage/thumbs/512x512/r:1/logo.png">
            </head><body>…</body></html>
            """);
        var logos = new Logos(_ordner, new HttpClient(server));

        var bild = await logos.HolenFuer("", "https://www.schwarzwaldradio.com/");

        Assert.NotNull(bild);
        Assert.Equal("https://www.schwarzwaldradio.com/storage/thumbs/512x512/r:1/logo.png", server.Gefragt[^1]);
        Assert.DoesNotContain(server.Gefragt, a => a.Contains("duckduckgo"));

        // Beim nächsten Start: keine Homepage mehr, das gemerkte Bild liegt da.
        var vorher = server.Gefragt.Count;
        await new Logos(_ordner, new HttpClient(server)).HolenFuer("", "https://www.schwarzwaldradio.com/");
        Assert.Equal(vorher, server.Gefragt.Count);
    }

    [Fact]
    public void SymboleDerHomepageNachGroesse()
    {
        // Aus dem echten Kopf von schwarzwaldradio.com (03.10.2026), gekürzt.
        var html = """
            <link rel="shortcut icon" href="https://www.schwarzwaldradio.com/storage/thumbs/512x512/r:1737717688/8co3znykd88qg.png">
            <link rel="icon" href="https://www.schwarzwaldradio.com/storage/thumbs/32x32/r:1737717688/8co3znykd88qg.png" sizes="32x32">
            <link rel="icon" href="https://www.schwarzwaldradio.com/storage/thumbs/192x192/r:1737717688/8co3znykd88qg.png" sizes="192x192">
            <link rel="apple-touch-icon-precomposed" href="https://www.schwarzwaldradio.com/storage/thumbs/180x180/r:1737717688/8co3znykd88qg.png">
            <link rel="preload" as="style" href="https://assets.welocal.world/fonts/Material+Icons:1-11-4">
            <meta name="msapplication-TileImage" content="https://www.schwarzwaldradio.com/storage/thumbs/270x270/r:1737717688/8co3znykd88qg.png">
            <link rel="icon" type="image/svg+xml" href="/logo.svg">
            """;
        var basis = new Uri("https://www.schwarzwaldradio.com/");
        Assert.Equal(
            [
                "https://www.schwarzwaldradio.com/storage/thumbs/512x512/r:1737717688/8co3znykd88qg.png",
                "https://www.schwarzwaldradio.com/storage/thumbs/270x270/r:1737717688/8co3znykd88qg.png",
                "https://www.schwarzwaldradio.com/storage/thumbs/192x192/r:1737717688/8co3znykd88qg.png",
                "https://www.schwarzwaldradio.com/storage/thumbs/180x180/r:1737717688/8co3znykd88qg.png",
                "https://www.schwarzwaldradio.com/storage/thumbs/32x32/r:1737717688/8co3znykd88qg.png",
            ],
            Logos.SymboleAusHtml(html, basis));
    }

    [Fact]
    public void RelativeAdressenUndAppleOhneGroesse()
    {
        var html = """<LINK REL='apple-touch-icon' HREF='/img/touch.png'><link rel=icon href=favicon.ico>""";
        Assert.Equal(
            ["https://sender.de/img/touch.png", "https://sender.de/radio/favicon.ico"],
            Logos.SymboleAusHtml(html, new Uri("https://sender.de/radio/start")));
    }

    [Fact]
    public async Task DasEigeneLogoGehtVor()
    {
        var server = new ErfundenerServer
        {
            ["https://sender.de/logo.png"] = (HttpStatusCode.OK, Png(200)),
        };
        var logos = new Logos(_ordner, new HttpClient(server));

        Assert.NotNull(await logos.HolenFuer("https://sender.de/logo.png", "https://sender.de/"));
        Assert.Equal(["https://sender.de/logo.png"], server.Gefragt);
    }

    [Fact]
    public async Task PlatzhalterMit404GiltNicht()
    {
        // DuckDuckGo schickt für unbekannte Seiten ein graues Bild – mit 404.
        var server = new ErfundenerServer
        {
            ["https://icons.duckduckgo.com/ip3/www.unbekannt.de.ico"] = (HttpStatusCode.NotFound, Png(48)),
            ["https://icons.duckduckgo.com/ip3/unbekannt.de.ico"] = (HttpStatusCode.NotFound, Png(48)),
        };
        var logos = new Logos(_ordner, new HttpClient(server));

        Assert.Null(await logos.HolenFuer("", "https://www.unbekannt.de/"));
    }

    [Fact]
    public async Task WinzigeSymboleGeltenNicht()
    {
        var server = new ErfundenerServer
        {
            ["https://icons.duckduckgo.com/ip3/www.klein.de.ico"] = (HttpStatusCode.OK, Png(16)),
        };
        var logos = new Logos(_ordner, new HttpClient(server));

        Assert.Null(await logos.HolenFuer("", "https://www.klein.de/"));
    }

    [Fact]
    public async Task WasFehltWirdNichtJedesMalNeuGefragt()
    {
        var server = new ErfundenerServer();
        var logos = new Logos(_ordner, new HttpClient(server));
        await logos.HolenFuer("", "https://www.nichts.de/");
        var beimErstenMal = server.Gefragt.Count;

        // Ein neues Programm, derselbe Speicher: wie der nächste Start.
        var spaeter = new Logos(_ordner, new HttpClient(server));
        await spaeter.HolenFuer("", "https://www.nichts.de/");

        // Homepage, apple-touch-icon, zweimal DuckDuckGo.
        Assert.Equal(4, beimErstenMal);
        Assert.Equal(beimErstenMal, server.Gefragt.Count);
    }

    [Fact]
    public async Task ZeitablaufWirdNichtGemerkt()
    {
        var server = new ErfundenerServer { Zeitablauf = true };
        var logos = new Logos(_ordner, new HttpClient(server));
        await logos.HolenFuer("", "https://www.funkloch.de/");

        server.Zeitablauf = false;
        server["https://icons.duckduckgo.com/ip3/www.funkloch.de.ico"] = (HttpStatusCode.OK, Png(64));

        Assert.NotNull(await logos.HolenFuer("", "https://www.funkloch.de/"));
    }

    private static byte[] Png(int kante)
    {
        var punkte = new byte[kante * kante * 4];
        Array.Fill(punkte, (byte)0x80);
        var bild = BitmapSource.Create(kante, kante, 96, 96, PixelFormats.Bgra32, null, punkte, kante * 4);
        var kodierer = new PngBitmapEncoder();
        kodierer.Frames.Add(BitmapFrame.Create(bild));
        using var strom = new MemoryStream();
        kodierer.Save(strom);
        return strom.ToArray();
    }

    /// <summary>Antwortet aus einer Tabelle; alles andere ist 404 mit HTML.</summary>
    private sealed class ErfundenerServer : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, byte[] Bild)> _antworten = new();
        private readonly Dictionary<string, string> _seiten = new();

        public void Seite(string adresse, string html) => _seiten[adresse] = html;

        public List<string> Gefragt { get; } = [];
        public bool Zeitablauf { get; set; }

        public (HttpStatusCode, byte[]) this[string adresse]
        {
            set => _antworten[adresse] = value;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage anfrage, CancellationToken abbruch)
        {
            var adresse = anfrage.RequestUri!.ToString();
            Gefragt.Add(adresse);
            if (Zeitablauf) throw new TaskCanceledException("Zeitablauf");

            if (_seiten.TryGetValue(adresse, out var html))
            {
                var inhalt = new StringContent(html);
                inhalt.Headers.ContentType = new MediaTypeHeaderValue("text/html");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = inhalt, RequestMessage = anfrage });
            }
            if (_antworten.TryGetValue(adresse, out var antwort))
            {
                var inhalt = new ByteArrayContent(antwort.Bild);
                inhalt.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                return Task.FromResult(new HttpResponseMessage(antwort.Status) { Content = inhalt });
            }
            var seite = new StringContent("<html>Nicht gefunden</html>");
            seite.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = seite });
        }
    }
}

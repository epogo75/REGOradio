using System.IO;
using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace REGOradio.Fernbedienung;

/// <summary>Was das Handy über das Radio erfährt.</summary>
public sealed record Fernstand(
    bool Laeuft, int Platz, string Sender, string Titel, string Interpret,
    string Cover, string Logo, int Lautstaerke, bool Stumm, int? Helligkeit, string Ansicht);

/// <summary>Ein Sender, wie das Handy ihn zeigt.</summary>
public sealed record Fernsender(int Platz, string Name, string Logo, string Herkunft);

/// <summary>
/// Was die Fernbedienung am Radio tun darf -- und nichts sonst.
///
/// **Bewusst schmal.** Sender wählen, Lautstärke, Stumm, Stopp, Helligkeit. Keine
/// Einstellungen, kein Umordnen, kein Löschen: Wer im Hotel-WLAN die PIN
/// errät, soll nichts anrichten können, was sich nicht mit einem Tipp am
/// Notebook zurückdrehen lässt.
/// </summary>
public interface IFernsteuerbar
{
    Fernstand Stand();
    IReadOnlyList<Fernsender> Sender();
    bool Spielen(int platz);
    void Stoppen();
    void Lautstaerke(int wert);
    void Stumm(bool stumm);
    void Helligkeit(int wert);

    /// <summary>
    /// Was der Notebook-Bildschirm zeigt: "bedienung", "cover" (Jetzt läuft
    /// im Vollbild) oder "uhr" (Wortuhr). False, wenn es nicht geht -- ein
    /// Cover gibt es nur, solange ein Sender läuft.
    /// </summary>
    bool Ansicht(string ansicht);
}

/// <summary>
/// Der kleine Webdienst für die Handy-Fernbedienung.
///
/// **Kestrel, nicht HttpListener.** HttpListener braucht für jede Adresse außer
/// localhost eine URL-Reservierung -- und die nur mit Administratorrechten.
/// Kestrel lauscht ohne. Beim ersten Start fragt die Windows-Firewall einmal
/// nach; im Hotel-WLAN (öffentliches Netz) muss dort „öffentlich" erlaubt sein.
///
/// **Aus, solange niemand ihn braucht.** Der Dienst läuft nur, wenn der
/// Schalter in den Einstellungen an ist. Ein offener Port im Hotel-WLAN, den
/// niemand benutzt, ist nur Angriffsfläche.
/// </summary>
public sealed class Dienst(IFernsteuerbar radio, Zugang zugang) : IAsyncDisposable
{
    public const int Port = 8765;
    private const string Cookie = "regoradio";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private WebApplication? _app;

    public bool Laeuft => _app is not null;

    public async Task Starten()
    {
        if (_app is not null) return;

        var bauer = WebApplication.CreateSlimBuilder();
        bauer.WebHost.UseKestrel(o => o.Listen(IPAddress.Any, Port));
        // Kein Protokoll auf die Konsole: Es gibt keine, und jede Anfrage im
        // Zehn-Sekunden-Takt des Handys wäre nur Rauschen.
        bauer.Logging.ClearProviders();
        var app = bauer.Build();

        app.MapGet("/", () => Results.Content(Handyseite(), "text/html; charset=utf-8"));

        app.MapPost("/api/anmelden", async (HttpContext kontext) =>
        {
            var anfrage = await kontext.Request.ReadFromJsonAsync<Anmeldung>(Json);
            var absender = kontext.Connection.RemoteIpAddress?.ToString() ?? "?";
            switch (zugang.Anmelden(absender, anfrage?.Pin))
            {
                case Zugang.Ergebnis.Angemeldet:
                    kontext.Response.Cookies.Append(Cookie, zugang.Zeichen(), new CookieOptions
                    {
                        HttpOnly = true,
                        SameSite = SameSiteMode.Strict,
                        MaxAge = TimeSpan.FromDays(365),
                    });
                    return Results.Ok();
                case Zugang.Ergebnis.Gesperrt:
                    return Results.Json(new { meldung = "Zu viele Versuche. Bitte eine Minute warten." }, statusCode: 429);
                default:
                    return Results.Json(new { meldung = "Die PIN stimmt nicht." }, statusCode: 401);
            }
        });

        // Alles unter /api außer der Anmeldung verlangt das Zeichen.
        var geschuetzt = app.MapGroup("/api").AddEndpointFilter(async (kontext, weiter) =>
        {
            var zeichen = kontext.HttpContext.Request.Cookies[Cookie];
            return zugang.ZeichenGilt(zeichen) ? await weiter(kontext) : Results.StatusCode(401);
        });

        geschuetzt.MapGet("/stand", () => Results.Json(radio.Stand(), Json));
        geschuetzt.MapGet("/sender", () => Results.Json(radio.Sender(), Json));
        geschuetzt.MapPost("/spielen/{platz:int}", (int platz) =>
            radio.Spielen(platz) ? Results.Json(radio.Stand(), Json) : Results.NotFound());
        geschuetzt.MapPost("/stopp", () =>
        {
            radio.Stoppen();
            return Results.Json(radio.Stand(), Json);
        });
        geschuetzt.MapPost("/lautstaerke/{wert:int}", (int wert) =>
        {
            radio.Lautstaerke(wert);
            return Results.Json(radio.Stand(), Json);
        });
        geschuetzt.MapPost("/stumm/{an:bool}", (bool an) =>
        {
            radio.Stumm(an);
            return Results.Json(radio.Stand(), Json);
        });
        geschuetzt.MapPost("/ansicht/{ansicht}", (string ansicht) =>
            radio.Ansicht(ansicht)
                ? Results.Json(radio.Stand(), Json)
                : Results.Json(new { meldung = "Es läuft kein Sender – ohne Sender gibt es kein Cover." }, statusCode: 409));
        geschuetzt.MapPost("/helligkeit/{wert:int}", (int wert) =>
        {
            radio.Helligkeit(wert);
            return Results.Json(radio.Stand(), Json);
        });

        await app.StartAsync();
        _app = app;
    }

    public async Task Anhalten()
    {
        if (_app is null) return;
        var app = _app;
        _app = null;
        await app.StopAsync();
        await app.DisposeAsync();
    }

    private static string? _seite;

    private static string Handyseite()
    {
        if (_seite is not null) return _seite;
        using var strom = typeof(Dienst).Assembly.GetManifestResourceStream("Handyseite.html")
            ?? throw new InvalidOperationException("Die Handyseite fehlt in der .exe.");
        using var leser = new StreamReader(strom);
        return _seite = leser.ReadToEnd();
    }

    private sealed record Anmeldung(string? Pin);

    public ValueTask DisposeAsync() => new(Anhalten());
}

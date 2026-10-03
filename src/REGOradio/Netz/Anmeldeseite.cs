using System.Net;
using System.Net.Http;

using Windows.Networking.Connectivity;

namespace REGOradio.Netz;

/// <summary>Wie der Rechner ins Internet kommt.</summary>
public enum Netzzugang { Frei, Anmeldung, KeinNetz }

/// <summary>Das Ergebnis: und, wenn eine Anmeldung fehlt, wohin.</summary>
public sealed record Zugangsbefund(Netzzugang Art, string Anmeldeadresse = "");

/// <summary>
/// Erkennt ein WLAN, das erst eine Anmeldung im Browser verlangt (Bau 22) –
/// Hotel, Zug, Café.
///
/// **Warum.** Bis Bau 21 stand dann nur „Der Sender spielt nicht" da, mit
/// der Frage, ob das WLAN noch eine Anmeldung verlange. Ob es das tut, lässt
/// sich aber herausfinden: Windows selbst fragt dazu eine feste Adresse von
/// Microsoft ab und erwartet einen bekannten Satz. Kommt stattdessen eine
/// Umleitung oder eine fremde Seite, hängt eine Anmeldeseite dazwischen.
/// REGOradio fragt dieselbe Adresse – Windows tut das ohnehin ständig, es
/// geht also nichts hinaus, was nicht schon hinausgeht.
///
/// **Zwei Meinungen.** Dazu kommt Windows' eigene Einschätzung
/// („eingeschränkter Internetzugang"). Antwortet die Prüfadresse gar nicht,
/// sagt sie, ob dahinter eine Anmeldung oder gar kein Netz steckt.
///
/// `Deuten` ist reine Rechnung, geprüft in `AnmeldeseitePruefung`.
/// </summary>
public sealed class Anmeldeseite
{
    public const string Pruefadresse = "http://www.msftconnecttest.com/connecttest.txt";
    public const string Erwartet = "Microsoft Connect Test";

    /// <summary>
    /// Die Adresse, die Windows zum Anmelden öffnet: Hinter einer
    /// Anmeldeseite leitet sie dorthin um, ohne landet man bei Microsoft.
    /// </summary>
    public const string Umweg = "http://www.msftconnecttest.com/redirect";

    private readonly HttpClient _klient;

    public Anmeldeseite(HttpClient? klient = null)
    {
        // Ohne Folgen von Umleitungen: Die Umleitung IST die Auskunft.
        _klient = klient ?? new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(5),
        };
    }

    public async Task<Zugangsbefund> Pruefen()
    {
        var eingeschraenkt = WindowsMeintEingeschraenkt();
        try
        {
            using var antwort = await _klient.GetAsync(Pruefadresse);
            var text = await antwort.Content.ReadAsStringAsync();
            return Deuten((int)antwort.StatusCode, antwort.Headers.Location?.ToString(), text, eingeschraenkt);
        }
        catch (Exception fehler) when (fehler is HttpRequestException or TaskCanceledException)
        {
            return Deuten(0, null, "", eingeschraenkt);
        }
    }

    /// <param name="status">0: keine Antwort.</param>
    public static Zugangsbefund Deuten(int status, string? umleitung, string text, bool windowsEingeschraenkt)
    {
        if (status == 200 && text.Trim() == Erwartet) return new(Netzzugang.Frei);

        if (status is >= 300 and < 400 && !string.IsNullOrWhiteSpace(umleitung)
            && Uri.TryCreate(umleitung, UriKind.Absolute, out var ziel) && ziel.Scheme is "http" or "https")
        {
            return new(Netzzugang.Anmeldung, ziel.ToString());
        }

        // Eine Antwort, aber nicht die erwartete: Die Anmeldeseite hat sich an
        // die Stelle gesetzt, ohne umzuleiten. Wohin, wissen wir nicht – der
        // Umweg von Windows findet sie.
        if (status != 0) return new(Netzzugang.Anmeldung, Umweg);

        return windowsEingeschraenkt ? new(Netzzugang.Anmeldung, Umweg) : new(Netzzugang.KeinNetz);
    }

    private static bool WindowsMeintEingeschraenkt()
    {
        try
        {
            return NetworkInformation.GetInternetConnectionProfile()?.GetNetworkConnectivityLevel()
                   == NetworkConnectivityLevel.ConstrainedInternetAccess;
        }
        catch (Exception fehler) when (fehler is System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

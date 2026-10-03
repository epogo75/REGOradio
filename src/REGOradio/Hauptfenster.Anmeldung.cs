using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

using REGOradio.Netz;

namespace REGOradio;

/// <summary>
/// Hotel-WLAN (Bau 22): Verlangt das WLAN eine Anmeldung im Browser, sagt
/// REGOradio das und bietet den Weg dorthin an. Ist die Anmeldung durch,
/// spielt der Sender von selbst weiter. Erkannt wird in `Netz/Anmeldeseite.cs`.
/// </summary>
public partial class Hauptfenster
{
    private readonly Anmeldeseite _anmeldeseite = new();
    private readonly DispatcherTimer _anmeldeuhr = new() { Interval = TimeSpan.FromSeconds(5) };
    private string _anmeldeadresse = "";
    private bool _wartetAufAnmeldung;

    /// <summary>
    /// Vor der Suche nach einer neuen Senderadresse: Liegt es am WLAN? Dann
    /// ist kein Sender tot, und eine Suche im Verzeichnis käme ohnehin nicht
    /// durch.
    /// </summary>
    /// <returns>Ob eine Anmeldung fehlt.</returns>
    private async Task<bool> AnmeldungPruefen()
    {
        var befund = await _anmeldeseite.Pruefen();
        if (befund.Art != Netzzugang.Anmeldung) return false;

        _anmeldeadresse = befund.Anmeldeadresse;
        _wartetAufAnmeldung = true;
        HinweisZeigen("Das WLAN verlangt eine Anmeldung im Browser. Sobald sie durch ist, spielt das Radio von selbst weiter.", bleibt: true);
        AnmeldeKnopf.Visibility = Visibility.Visible;

        _anmeldeuhr.Tick -= AnmeldungNachsehen;
        _anmeldeuhr.Tick += AnmeldungNachsehen;
        _anmeldeuhr.Start();
        return true;
    }

    private void AnmeldungOeffnen(object absender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = _anmeldeadresse.Length > 0 ? _anmeldeadresse : Anmeldeseite.Umweg, UseShellExecute = true });
        }
        catch (Exception fehler) when (fehler is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            HinweisZeigen($"Der Browser ließ sich nicht öffnen ({fehler.Message}). Im Browser eine beliebige Seite aufrufen – die Anmeldung erscheint dann.", bleibt: true);
        }
    }

    /// <summary>Alle fünf Sekunden: Ist die Anmeldung durch?</summary>
    private async void AnmeldungNachsehen(object? absender, EventArgs e)
    {
        _anmeldeuhr.Stop();
        if (!_wartetAufAnmeldung) return;

        var befund = await _anmeldeseite.Pruefen();
        if (befund.Art == Netzzugang.Anmeldung)
        {
            _anmeldeuhr.Start();
            return;
        }

        AnmeldungErledigt();
        if (befund.Art == Netzzugang.Frei && _laufender is { } sender)
        {
            Spielen(sender);
            HinweisZeigen("Angemeldet – das Radio spielt weiter.");
        }
    }

    /// <summary>Knopf weg, Warten vorbei – auch, wenn jemand selbst stoppt.</summary>
    private void AnmeldungErledigt()
    {
        _wartetAufAnmeldung = false;
        _anmeldeuhr.Stop();
        AnmeldeKnopf.Visibility = Visibility.Collapsed;
    }
}

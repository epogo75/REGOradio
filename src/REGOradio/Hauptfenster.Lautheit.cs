using System.Windows;
using System.Windows.Threading;

using REGOradio.Ton;

namespace REGOradio;

/// <summary>
/// „Alle Sender gleich laut" (Bau 21): Schalter im Blatt „Ton geht an" und
/// das Merken der gemessenen Lautheit am Sender. Gemessen und angeglichen
/// wird in `Ton/Angleicher.cs`.
/// </summary>
public partial class Hauptfenster
{
    private readonly DispatcherTimer _lautheitsuhr = new() { Interval = TimeSpan.FromMinutes(1) };

    private void LautheitAnmelden()
    {
        _abspieler.Angleichen = _einstellungen.Angleichen;
        _lautheitsuhr.Tick += (_, _) => LautheitMerken();
        _lautheitsuhr.Start();
    }

    /// <summary>
    /// Die Messung an den laufenden Sender schreiben – einmal je Minute und vor
    /// jedem Wechsel. Gespeichert nur, wenn sich mehr als ein halbes LU
    /// geändert hat: Die Datei soll nicht jede Minute neu geschrieben werden.
    /// </summary>
    private void LautheitMerken()
    {
        if (_laufender is null || _abspieler.GemesseneLautheit is not { } lufs) return;
        var alt = _laufender.Lautheit;
        _laufender.Lautheit = Math.Round(lufs, 1);
        if (_laufender.Platz > 0 && (alt is null || Math.Abs(alt.Value - lufs) > 0.5)) _ablage.SenderSchreiben(_sender);
    }

    private void AngleichenGeaendert(object absender, RoutedEventArgs e)
    {
        _einstellungen.Angleichen = AngleichenSchalter.IsChecked == true;
        _abspieler.Angleichen = _einstellungen.Angleichen;
        _ablage.EinstellungenSchreiben(_einstellungen);
        AngleichenZeigen();
    }

    private void AngleichenZeigen()
    {
        AngleichenSchalter.IsChecked = _einstellungen.Angleichen;
        var gemessen = _abspieler.GemesseneLautheit ?? _laufender?.Lautheit;
        AngleichenHinweis.Text = !_einstellungen.Angleichen
            ? "Aus: Jeder Sender spielt so laut, wie er sendet."
            : gemessen is { } lufs && _laufender is not null
                ? $"{_laufender.Name} sendet mit {lufs:0.0} LUFS und wird um {Angleicher.Verstaerkung(lufs, 0):+0.0;−0.0;0} dB angeglichen. "
                  + "Gemessen wird beim Hören; ein neuer Sender braucht dafür zehn Sekunden."
                : "Gemessen wird beim Hören; ein neuer Sender braucht dafür zehn Sekunden. Danach klingt er beim Umschalten so laut wie die anderen.";
    }
}

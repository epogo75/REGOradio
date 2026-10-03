using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

using REGOradio.Anzeige;
using REGOradio.Modelle;
using REGOradio.Uhr;

namespace REGOradio;

/// <summary>
/// Einschlafen und Wecken (Bau 20). Die Rechnung steht in `Uhr/Wecker.cs`,
/// der Weckauftrag an Windows in `Uhr/Weckauftrag.cs`.
///
/// **Beide fassen die gespeicherte Lautstärke nicht an.** Ausblenden und
/// Anschwellen setzen nur den Abspieler; `_einstellungen.Lautstaerke` bleibt
/// die Zahl, die man selbst gewählt hat. Sonst stünde nach dem Einschlafen
/// am nächsten Morgen 0 da.
/// </summary>
public partial class Hauptfenster
{
    private readonly DispatcherTimer _schlaftakt = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime? _schlafEnde;
    private DateTime? _weckStart;
    private bool _weckerWirdGezeigt;

    private void SchlafAnmelden()
    {
        _schlaftakt.Tick += (_, _) => SchlafTakt();
        _schlaftakt.Start();
        // Den Weckauftrag bei jedem Start erneuern: Nach einem Update liegt
        // das Programm vielleicht woanders, und der alte Auftrag zeigte ins
        // Leere.
        _ = WeckauftragAbgleichen();
        SchlafknopfZeigen();
    }

    /// <summary>Jede Sekunde: Ausblenden, Anschwellen, Wecken.</summary>
    private void SchlafTakt()
    {
        var jetzt = DateTime.Now;

        if (_schlafEnde is { } ende)
        {
            if (_laufender is null)
            {
                // Von Hand gestoppt: Einschlafen hat sich erledigt.
                SchlafBeenden();
            }
            else if (jetzt >= ende)
            {
                Stoppen(this, new RoutedEventArgs());
                SchlafBeenden();
                // Bau 24: Wer einschläft, will es dunkel. Ein Tipp holt den
                // Bildschirm zurück.
                if (_einstellungen.SchlafBildschirmAus)
                {
                    Bildschirm.Ausschalten(new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle());
                }
            }
            else
            {
                _abspieler.Lautstaerke = Wecker.Ausblenden(ende - jetzt, _einstellungen.Lautstaerke);
            }
            if (Schlafebene.Visibility == Visibility.Visible) SchlafHinweisZeigen();
        }

        if (_weckStart is not null && _laufender is null)
        {
            // Beim Anschwellen von Hand gestoppt: fertig, und die eigene
            // Lautstärke zurück für den nächsten Sender.
            _weckStart = null;
            _abspieler.Lautstaerke = _einstellungen.Lautstaerke;
        }
        if (_weckStart is { } start)
        {
            var ziel = Wecker.Rampe(jetzt - start, _einstellungen.Lautstaerke);
            _abspieler.Lautstaerke = ziel;
            if (ziel >= _einstellungen.Lautstaerke) _weckStart = null;
        }

        // Den Wecker nur alle 15 Sekunden prüfen – das Fenster ist zehn
        // Minuten breit, das reicht dicke.
        if (_einstellungen.WeckerAn && jetzt.Second % 15 == 0
            && Wecker.Faellig(jetzt, _einstellungen.WeckerMinuten, _einstellungen.WeckerTage, _einstellungen.WeckerZuletzt))
        {
            Wecken(jetzt);
        }
    }

    private void Wecken(DateTime jetzt)
    {
        _einstellungen.WeckerZuletzt = jetzt;
        if (_einstellungen.WeckerTage == "einmal")
        {
            _einstellungen.WeckerAn = false;
            _ = WeckauftragAbgleichen();
        }
        _ablage.EinstellungenSchreiben(_einstellungen);
        SchlafknopfZeigen();

        // Läuft schon Radio (eingeschlafen ohne Timer), bleibt es dabei.
        if (_laeuft) return;
        var sender = _sender.FirstOrDefault(s => s.Platz == _einstellungen.WeckerPlatz)
                     ?? Tastenbelegung.Nachbar(_sender, 0, 1);
        if (sender is null) return;

        if (_abspieler.Stumm) StummSetzen(false);
        _abspieler.Lautstaerke = Wecker.Startlautstaerke;
        _weckStart = jetzt;
        Spielen(sender);
    }

    private void SchlafBeenden()
    {
        _schlafEnde = null;
        _abspieler.Lautstaerke = _einstellungen.Lautstaerke;
        SchlafAus.IsChecked = true;
        SchlafknopfZeigen();
        if (Schlafebene.Visibility == Visibility.Visible) SchlafHinweisZeigen();
    }

    // ------------------------------------------------------------- Blatt

    private void SchlafOeffnen(object absender, RoutedEventArgs e)
    {
        _weckerWirdGezeigt = true;
        SchlafBildschirmSchalter.IsChecked = _einstellungen.SchlafBildschirmAus;
        WeckerSchalter.IsChecked = _einstellungen.WeckerAn;
        WeckerTaeglich.IsChecked = _einstellungen.WeckerTage == "taeglich";
        WeckerWerktags.IsChecked = _einstellungen.WeckerTage == "werktags";
        WeckerEinmal.IsChecked = _einstellungen.WeckerTage == "einmal";
        var tasten = _sender.Where(s => s.Platz > 0).OrderBy(s => s.Platz).ToList();
        WeckerSender.ItemsSource = tasten.Select(s => $"Taste {s.Platz} · {s.Name}").ToList();
        WeckerSender.Tag = tasten;
        WeckerSender.SelectedIndex = Math.Max(0, tasten.FindIndex(s => s.Platz == _einstellungen.WeckerPlatz));
        _weckerWirdGezeigt = false;
        if (_schlafEnde is null) SchlafAus.IsChecked = true;

        WeckerZeigen();
        SchlafHinweisZeigen();
        Schlafebene.Visibility = Visibility.Visible;
    }

    private void SchlafSchliessen(object absender, RoutedEventArgs e) =>
        Schlafebene.Visibility = Visibility.Collapsed;

    private void SchlafzeitGewaehlt(object absender, RoutedEventArgs e)
    {
        var minuten = int.Parse((string)((FrameworkElement)absender).Tag);
        if (minuten == 0)
        {
            SchlafBeenden();
            return;
        }
        _abspieler.Lautstaerke = _einstellungen.Lautstaerke;
        _schlafEnde = DateTime.Now.AddMinutes(minuten);
        SchlafknopfZeigen();
        SchlafHinweisZeigen();
    }

    private void SchlafHinweisZeigen()
    {
        if (_schlafEnde is not { } ende)
        {
            SchlafHinweis.Text = "Das Radio läuft, bis man es ausschaltet.";
            return;
        }
        SchlafHinweis.Text = _laufender is null
            ? $"Läuft gerade nichts. Wer jetzt einen Sender wählt, hört ihn bis {ende:HH:mm}."
            : $"Wird ab {ende.AddMinutes(-1):HH:mm} leiser und geht um {ende:HH:mm} aus ({Wecker.Abstand(ende - DateTime.Now)})"
              + (_einstellungen.SchlafBildschirmAus ? ", der Bildschirm mit." : ".");
    }

    private void SchlafBildschirmGeaendert(object absender, RoutedEventArgs e)
    {
        _einstellungen.SchlafBildschirmAus = SchlafBildschirmSchalter.IsChecked == true;
        _ablage.EinstellungenSchreiben(_einstellungen);
        SchlafHinweisZeigen();
    }

    private void WeckerGeaendert(object absender, RoutedEventArgs e)
    {
        _einstellungen.WeckerAn = WeckerSchalter.IsChecked == true;
        WeckerSpeichern();
    }

    private void WeckzeitAendern(object absender, RoutedEventArgs e)
    {
        var schritt = int.Parse((string)((FrameworkElement)absender).Tag);
        var minuten = (_einstellungen.WeckerMinuten + schritt) % (24 * 60);
        if (minuten < 0) minuten += 24 * 60;
        // Fünf-Minuten-Raster: Wer +5 drückt, will 6:50, nicht 6:48.
        _einstellungen.WeckerMinuten = minuten / 5 * 5;
        // Wer die Zeit stellt, will geweckt werden.
        _einstellungen.WeckerAn = true;
        WeckerSchalter.IsChecked = true;
        WeckerSpeichern();
    }

    private void WecktageGewaehlt(object absender, RoutedEventArgs e)
    {
        _einstellungen.WeckerTage = (string)((FrameworkElement)absender).Tag;
        WeckerSpeichern();
    }

    private void WeckersenderGewaehlt(object absender, SelectionChangedEventArgs e)
    {
        if (_weckerWirdGezeigt || WeckerSender.Tag is not List<Sender> tasten || WeckerSender.SelectedIndex < 0) return;
        _einstellungen.WeckerPlatz = tasten[WeckerSender.SelectedIndex].Platz;
        WeckerSpeichern();
    }

    private void WeckerSpeichern()
    {
        // Neu gestellt heißt: Heute darf wieder geweckt werden.
        _einstellungen.WeckerZuletzt = null;
        _ablage.EinstellungenSchreiben(_einstellungen);
        WeckerZeigen();
        SchlafknopfZeigen();
        _ = WeckauftragAbgleichen();
    }

    private void WeckerZeigen()
    {
        WeckerZeit.Text = $"{_einstellungen.WeckerMinuten / 60:00}:{_einstellungen.WeckerMinuten % 60:00}";
        WeckerZeit.Opacity = _einstellungen.WeckerAn ? 1 : 0.45;
        if (!_einstellungen.WeckerAn)
        {
            WeckerHinweis.Text = "Aus.";
            return;
        }
        var jetzt = DateTime.Now;
        var naechster = Wecker.Naechster(jetzt, _einstellungen.WeckerMinuten, _einstellungen.WeckerTage);
        WeckerHinweis.Text = $"Weckt {Wecker.Wann(naechster)}, {Wecker.Abstand(naechster - jetzt)} – leise anfangend. "
            + "Schläft der Rechner, weckt Windows ihn eine Minute vorher; zugeklappt oder ohne Netzteil erlaubt Windows das oft nicht.";
    }

    /// <summary>Der Rahmen der Taste links zeigt, ob etwas gestellt ist.</summary>
    private void SchlafknopfZeigen()
    {
        if (_schlafEnde is not null || _einstellungen.WeckerAn) SchlafKnopf.SetResourceReference(BorderBrushProperty, "Akzent");
        else SchlafKnopf.ClearValue(BorderBrushProperty);
    }

    /// <summary>Den Auftrag an Windows so stellen, wie die Einstellung es sagt.</summary>
    private async Task WeckauftragAbgleichen()
    {
        var an = _einstellungen.WeckerAn;
        if (!an && !_einstellungen.WeckauftragAngelegt) return;
        var naechster = Wecker.Naechster(DateTime.Now, _einstellungen.WeckerMinuten, _einstellungen.WeckerTage);
        var tage = _einstellungen.WeckerTage;

        // schtasks braucht eine halbe Sekunde; nicht auf dem Oberflächenfaden.
        var geklappt = await Task.Run(() => an ? Weckauftrag.Setzen(naechster, tage) : Weckauftrag.Entfernen());
        _einstellungen.WeckauftragAngelegt = an ? geklappt : !geklappt && _einstellungen.WeckauftragAngelegt;
        _ablage.EinstellungenSchreiben(_einstellungen);
    }
}

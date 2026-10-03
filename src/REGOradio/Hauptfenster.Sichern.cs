using System.IO;
using System.Windows;

using Microsoft.Win32;

using REGOradio.Speicher;

namespace REGOradio;

/// <summary>
/// Stationstasten sichern und einlesen (Bau 19), aus dem Blatt „Alle
/// Sender". Das Dateiformat steht in `Speicher/Senderpaket.cs`.
/// </summary>
public partial class Hauptfenster
{
    private void TastenSichern(object absender, RoutedEventArgs e)
    {
        if (_sender.All(s => s.Platz <= 0))
        {
            MessageBox.Show(this, "Es liegt noch kein Sender auf einer Taste.", "REGOradio",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Stationstasten sichern",
            Filter = $"REGOradio-Stationstasten (*{Senderpaket.Endung})|*{Senderpaket.Endung}",
            FileName = $"Stationstasten {DateTime.Now:yyyy-MM-dd}{Senderpaket.Endung}",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dialog.ShowDialog(this) != true) return;

        var json = Senderpaket.Packen(_sender, s =>
        {
            var adresse = _logos.Adresse(s.Logo, s.Homepage);
            return _logos.Gespeichert(adresse) is { } bytes ? (adresse, bytes) : null;
        }, DateTime.Now, Bau.Kennung);

        try
        {
            File.WriteAllText(dialog.FileName, json);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Die Datei ließ sich nicht schreiben:\n\n{fehler.Message}", "REGOradio",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var anzahl = _sender.Count(s => s.Platz > 0);
        HinweisZeigen($"{anzahl} Stationstasten gesichert: {Path.GetFileName(dialog.FileName)}");
    }

    private void TastenEinlesen(object absender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Stationstasten einlesen",
            Filter = $"REGOradio-Stationstasten (*{Senderpaket.Endung})|*{Senderpaket.Endung}|Alle Dateien (*.*)|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dialog.ShowDialog(this) != true) return;

        (List<Modelle.Sender> Sender, Dictionary<string, byte[]> Logos)? paket;
        try
        {
            paket = Senderpaket.Auspacken(File.ReadAllText(dialog.FileName), Plaetze);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Die Datei ließ sich nicht lesen:\n\n{fehler.Message}", "REGOradio",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (paket is not { } p || p.Sender.Count == 0)
        {
            MessageBox.Show(this, "Das ist keine Sicherung von REGOradio-Stationstasten, oder sie ist leer.",
                "REGOradio", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Ersetzen, nicht mischen: Gemischt lägen zwei Sender auf einer
        // Taste, und welcher bleibt, wäre Zufall. Vorher gefragt, und die
        // bisherigen Tasten landen als Datei daneben – ein Fehlgriff lässt
        // sich mit „Einlesen" rückgängig machen.
        var bisher = _sender.Count(s => s.Platz > 0);
        if (bisher > 0 && MessageBox.Show(this,
                $"{p.Sender.Count} Stationstasten aus der Datei übernehmen?\n\n"
                + $"Die jetzigen {bisher} werden ersetzt. Sie liegen danach als Sicherung unter\n{_ablage.Verzeichnis}",
                "REGOradio", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        if (bisher > 0)
        {
            var vorher = Path.Combine(_ablage.Verzeichnis, $"vor dem Einlesen {DateTime.Now:yyyy-MM-dd HHmm}{Senderpaket.Endung}");
            try
            {
                File.WriteAllText(vorher, Senderpaket.Packen(_sender, s =>
                {
                    var adresse = _logos.Adresse(s.Logo, s.Homepage);
                    return _logos.Gespeichert(adresse) is { } bytes ? (adresse, bytes) : null;
                }, DateTime.Now, Bau.Kennung));
            }
            catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
            {
                if (MessageBox.Show(this, $"Die bisherigen Tasten ließen sich nicht sichern ({fehler.Message}). Trotzdem ersetzen?",
                        "REGOradio", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            }
        }

        foreach (var (adresse, bytes) in p.Logos) _logos.Vorlegen(adresse, bytes);

        if (_laufender is not null && _laufender.Platz > 0) Stoppen(this, new RoutedEventArgs());
        _zuletzt = null;
        _sender = p.Sender;
        _ablage.SenderSchreiben(_sender);
        _einstellungen.LetzterPlatz = 0;
        _ablage.EinstellungenSchreiben(_einstellungen);
        TastenZeichnen();
        if (Senderebene.Visibility == Visibility.Visible) SenderfeldZeichnen();
        HinweisZeigen($"{p.Sender.Count} Stationstasten eingelesen.");
    }
}

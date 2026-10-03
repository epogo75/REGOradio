using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using REGOradio.Katalog;
using REGOradio.Speicher;

namespace REGOradio;

/// <summary>„Was lief vorhin?" (Bau 18). Die Regeln stehen in `Speicher/Verlauf.cs`.</summary>
public partial class Hauptfenster
{
    private List<Gespielt>? _verlauf;

    private List<Gespielt> VerlaufListe => _verlauf ??= _ablage.VerlaufLesen();

    /// <summary>Aus `TitelZeigen`: ein neues Lied eintragen.</summary>
    private void VerlaufMerken()
    {
        if (_laufender is null || _titelGezeigt.Length == 0) return;
        var (interpret, titel) = Cover.Zerlege(_titelGezeigt);
        if (!Verlauf.Eintragen(VerlaufListe, _laufender.Name, interpret, titel, DateTime.Now)) return;
        VerlaufSpeichern();
        if (Verlaufebene.Visibility == Visibility.Visible) VerlaufZeichnen();
    }

    /// <summary>Aus `CoverLaden`: das gefundene Cover zum neuesten Eintrag.</summary>
    private void VerlaufCover(string icyTitel, string coverAdresse)
    {
        var (interpret, titel) = Cover.Zerlege(icyTitel);
        if (!Verlauf.CoverNachtragen(VerlaufListe, interpret, titel, coverAdresse)) return;
        VerlaufSpeichern();
        if (Verlaufebene.Visibility == Visibility.Visible) VerlaufZeichnen();
    }

    private void VerlaufSpeichern()
    {
        // Ein volles Laufwerk ist kein Grund, das Radio anzuhalten.
        try { _ablage.VerlaufSchreiben(VerlaufListe); }
        catch (Exception fehler) when (fehler is System.IO.IOException or UnauthorizedAccessException) { }
    }

    private void VerlaufOeffnen(object absender, RoutedEventArgs e)
    {
        VerlaufZeichnen();
        Verlaufebene.Visibility = Visibility.Visible;
    }

    private void VerlaufSchliessen(object absender, RoutedEventArgs e) =>
        Verlaufebene.Visibility = Visibility.Collapsed;

    private void VerlaufZeichnen()
    {
        Verlaufliste.Children.Clear();
        if (VerlaufListe.Count == 0)
        {
            var leer = new TextBlock
            {
                Text = "Noch nichts. Sobald ein Sender meldet, welches Lied läuft, steht es hier – mit Uhrzeit und Cover.",
                FontSize = 18,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 8),
            };
            leer.SetResourceReference(TextBlock.ForegroundProperty, "Tinte2");
            Verlaufliste.Children.Add(leer);
            return;
        }

        var jetzt = DateTime.Now;
        foreach (var lied in VerlaufListe) Verlaufliste.Children.Add(VerlaufZeile(lied, jetzt));
    }

    private UIElement VerlaufZeile(Gespielt lied, DateTime jetzt)
    {
        var zeile = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        zeile.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        zeile.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        zeile.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var wann = new TextBlock
        {
            Text = Verlauf.Wann(lied.Zeit, jetzt),
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };
        wann.SetResourceReference(TextBlock.ForegroundProperty, "Tinte3");
        zeile.Children.Add(wann);

        // Das Cover; ohne eines das Kürzel des Senders auf seiner Farbe – wie
        // auf den Stationstasten.
        var kuerzel = new TextBlock
        {
            Text = Monogramm.Kuerzel(lied.Sender),
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var bild = new Image { Stretch = Stretch.UniformToFill };
        var rahmen = new Border
        {
            Width = 64,
            Height = 64,
            CornerRadius = new CornerRadius(10),
            ClipToBounds = true,
            Background = new SolidColorBrush(Monogramm.Farbe(lied.Sender)),
            Margin = new Thickness(0, 0, 16, 0),
            Child = new Grid { Children = { kuerzel, bild } },
        };
        Grid.SetColumn(rahmen, 1);
        zeile.Children.Add(rahmen);
        if (lied.Cover.Length > 0) _ = VerlaufBildLaden(bild, kuerzel, lied.Cover);

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(text, 2);
        text.Children.Add(new TextBlock { Text = lied.Titel, FontSize = 20, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        var wer = new TextBlock { Text = lied.Interpret, FontSize = 17, TextTrimming = TextTrimming.CharacterEllipsis };
        wer.SetResourceReference(TextBlock.ForegroundProperty, "Tinte2");
        text.Children.Add(wer);
        var wo = new TextBlock { Text = lied.Sender, FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis };
        wo.SetResourceReference(TextBlock.ForegroundProperty, "Tinte3");
        text.Children.Add(wo);
        zeile.Children.Add(text);
        return zeile;
    }

    private async Task VerlaufBildLaden(Image ziel, TextBlock kuerzel, string adresse)
    {
        var bild = await _logos.Holen(adresse, breite: 128);
        if (bild is null) return;
        ziel.Source = bild;
        kuerzel.Visibility = Visibility.Collapsed;
    }
}

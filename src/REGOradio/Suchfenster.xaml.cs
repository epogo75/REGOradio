using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using REGOradio.Katalog;
using REGOradio.Modelle;

namespace REGOradio;

public partial class Suchfenster : Window
{
    private readonly Radiobrowser _katalog;
    private readonly Logos _logos;
    private readonly int _platz;
    private CancellationTokenSource? _laufendeSuche;

    /// <summary>Was gewählt wurde, oder null.</summary>
    public Treffer? Gewaehlt { get; private set; }

    /// <summary>Behalten oder nur anhören.</summary>
    public bool AufTasteLegen { get; private set; }

    public Suchfenster(Radiobrowser katalog, Logos logos, int platz)
    {
        InitializeComponent();
        _katalog = katalog;
        _logos = logos;
        _platz = platz;
        Kopfzeile.Text = platz > 0 ? $"Sender für Taste {platz}" : "Sender suchen";
        HierKnopf.Content = $"Sender in {Landesname()}";
        Loaded += (_, _) => Suchfeld.Focus();
    }

    /// <summary>
    /// In welchem Land wir stehen -- aus der Windows-Region, nicht über einen
    /// Dienst im Netz. Im Hotel-WLAN ohne Anmeldung wäre eine Ortsabfrage das
    /// Erste, was scheitert, und das Programm soll dann trotzdem etwas
    /// anbieten.
    /// </summary>
    private static string Landeskennung() => RegionInfo.CurrentRegion.TwoLetterISORegionName;

    private static string Landesname() => RegionInfo.CurrentRegion.DisplayName;

    private async void Suchen(object absender, RoutedEventArgs e) => await SuchenNach(Suchfeld.Text);

    private async void HierSuchen(object absender, RoutedEventArgs e)
    {
        await Arbeiten(
            () => _katalog.ImLand(Landeskennung()),
            $"In {Landesname()} findet das Verzeichnis gerade nichts.");
    }

    private async void SuchfeldTaste(object absender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await SuchenNach(Suchfeld.Text);
    }

    private async Task SuchenNach(string text)
    {
        if (text.Trim().Length == 0) return;
        await Arbeiten(
            () => _katalog.Suche(text.Trim()),
            $"Zu „{text.Trim()}“ findet das Verzeichnis nichts.");
    }

    private async Task Arbeiten(Func<Task<List<Treffer>>> suche, string leerSatz)
    {
        // Eine neue Suche bricht die alte ab: Wer zweimal tippt, will das
        // zweite Ergebnis, nicht beide durcheinander.
        _laufendeSuche?.Cancel();
        _laufendeSuche = new CancellationTokenSource();

        Trefferliste.Items.Clear();
        Hinweis.Text = "Wird gesucht …";
        IsEnabled = false;
        try
        {
            var treffer = await suche();
            Hinweis.Text = treffer.Count == 0 ? leerSatz : "";
            foreach (var t in treffer) Trefferliste.Items.Add(Zeile(t));
        }
        catch (KatalogFehler fehler)
        {
            Hinweis.Text = fehler.Message;
        }
        finally
        {
            IsEnabled = true;
            Suchfeld.Focus();
        }
    }

    /// <summary>
    /// Eine Trefferzeile: links Name und Herkunft, rechts der Knopf, der sie auf
    /// die Taste legt. Land, Genre und Bitrate stehen dabei, weil „SWR3"
    /// mehrfach im Verzeichnis steht und man sonst raten muss.
    /// </summary>
    private UIElement Zeile(Treffer treffer)
    {
        var zeile = new Grid();
        zeile.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
        zeile.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        zeile.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Das Logo hilft beim Unterscheiden: „SWR3" steht mehrfach im
        // Verzeichnis, das richtige erkennt man oft am Bild. Die Spalte ist
        // fest breit, damit die Namen untereinander bündig stehen, auch wenn
        // ein Logo fehlt.
        var logo = new Image
        {
            Width = 52,
            Height = 52,
            Stretch = System.Windows.Media.Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(logo, 0);
        zeile.Children.Add(logo);
        _ = LogoZeigen(logo, treffer.Logo);

        // Schriftfarben ausdrücklich aus der Farbtafel: Geerbt kam in den
        // dunklen Themen Schwarz an, und die Treffer standen unlesbar auf
        // dunklem Grund.
        var name = new TextBlock
        {
            Text = treffer.Name,
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        name.SetResourceReference(TextBlock.ForegroundProperty, "Tinte");
        var links = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        links.Children.Add(name);
        var unten = string.Join(" · ", new[]
        {
            treffer.Land,
            treffer.Genre,
            treffer.Bitrate > 0 ? $"{treffer.Bitrate} kBit/s" : "",
            treffer.Codec,
        }.Where(t => t.Length > 0));
        var herkunft = new TextBlock
        {
            Text = unten,
            FontSize = 15,
            Margin = new Thickness(0, 4, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        herkunft.SetResourceReference(TextBlock.ForegroundProperty, "Tinte2");
        links.Children.Add(herkunft);
        Grid.SetColumn(links, 1);
        zeile.Children.Add(links);

        var anhoeren = new Button
        {
            Style = (Style)FindResource("Taste"),
            Content = "Anhören",
            MinWidth = 150,
            Margin = new Thickness(10, 0, 8, 0),
        };
        anhoeren.Click += (_, _) => Fertig(treffer, aufTaste: false);

        var merken = new Button
        {
            Style = (Style)FindResource("Haupttaste"),
            Content = _platz > 0 ? $"Auf Taste {_platz}" : "Auf eine Taste",
            MinWidth = 190,
        };
        merken.Click += (_, _) => Fertig(treffer, aufTaste: true);

        var rechts = new StackPanel { Orientation = Orientation.Horizontal };
        rechts.Children.Add(anhoeren);
        rechts.Children.Add(merken);
        Grid.SetColumn(rechts, 2);
        zeile.Children.Add(rechts);

        return zeile;
    }

    private async Task LogoZeigen(Image ziel, string adresse)
    {
        var bild = await _logos.Holen(adresse);
        if (bild is not null) ziel.Source = bild;
    }

    private void Fertig(Treffer treffer, bool aufTaste)
    {
        Gewaehlt = treffer;
        AufTasteLegen = aufTaste;
        DialogResult = true;
    }

    private void Abbrechen(object absender, RoutedEventArgs e) => DialogResult = false;
}

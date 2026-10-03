using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

using REGOradio.Anzeige;
using REGOradio.Fernbedienung;
using REGOradio.Katalog;
using REGOradio.Modelle;
using REGOradio.Netz;
using REGOradio.Speicher;
using REGOradio.Ton;
using REGOradio.Uhr;

namespace REGOradio;

public partial class Hauptfenster : Window, IFernsteuerbar
{
    /// <summary>Alle Plätze -- so viele stehen im Blatt „Alle Sender".</summary>
    public const int Plaetze = 24;

    /// <summary>
    /// Die ersten acht liegen auf dem Hauptschirm; das neunte Feld dort ist
    /// „Mehr". Drei mal drei lässt jeder Taste Platz für Logo und Namen.
    /// </summary>
    public const int HauptPlaetze = 8;

    private readonly Ablage _ablage = new();
    private readonly Abspieler _abspieler = new();
    private readonly Radiobrowser _katalog = new();
    private readonly Logos _logos = new();
    private readonly Cover _cover = new();

    /// <summary>Der Titel, der gerade angezeigt wird -- so, wie der Sender ihn meldet.</summary>
    private string _titelGezeigt = "";

    /// <summary>Das Bild zum laufenden Lied, oder null. Auch im Vollbild gebraucht.</summary>
    private ImageSource? _coverBild;

    /// <summary>Die Adresse des Covers -- das Handy lädt es selbst von dort.</summary>
    private string _coverAdresse = "";

    private readonly Zugang _zugang;
    private readonly Dienst _dienst;
    private string _fernFehler = "";
    private readonly DispatcherTimer _netzuhr = new() { Interval = TimeSpan.FromSeconds(20) };
    private readonly DispatcherTimer _uhr = new() { Interval = TimeSpan.FromSeconds(10) };

    // Die Lautstärke wird erst gespeichert, wenn sie kurz ruht. Beim
    // Festhalten von + oder beim Ziehen am Balken kämen sonst zehn und mehr
    // Schreibvorgänge je Sekunde zusammen -- für eine Zahl, von der nur die
    // letzte zählt.
    private readonly DispatcherTimer _speicheruhr = new() { Interval = TimeSpan.FromMilliseconds(600) };

    private List<Sender> _sender;
    private readonly Einstellungen _einstellungen;

    /// <summary>
    /// Was gerade läuft -- auch dann, wenn es auf keiner Taste liegt (ein Sender
    /// aus der Suche, nur angehört). Dann ist `Platz` 0.
    /// </summary>
    private Sender? _laufender;

    private bool _lautstaerkeVonUns;
    private bool _laeuft;

    public Hauptfenster()
    {
        InitializeComponent();

        _sender = _ablage.SenderLesen();
        _einstellungen = _ablage.EinstellungenLesen();

        _zugang = new Zugang(() => _einstellungen.FernPin, () => _einstellungen.FernSchluessel);
        _dienst = new Dienst(this, _zugang);
        if (_einstellungen.FernAn) _ = FernStarten();

        _abspieler.Lautstaerke = _einstellungen.Lautstaerke;
        _abspieler.Ausgang(_einstellungen.Ausgang, "", "");
        // Der Abspieler meldet aus seinem eigenen Faden. Alles, was am Fenster
        // hängt, muss zurück in den Oberflächenfaden -- sonst wirft WPF.
        _abspieler.StandGeaendert += stand => Dispatcher.Invoke(() => TonstandZeigen(stand));

        DarstellungAnwenden();
        TastenZeichnen();
        _ = HomepagesNachholen();
        MedienAnmelden();
        LautstaerkeZeigen(_einstellungen.Lautstaerke);
        LaufendesZeigen();
        UhrZeigen();
        NetzZeigen();
        TonSymbolZeigen();

        _netzuhr.Tick += (_, _) => { NetzZeigen(); TonSymbolZeigen(); };
        _netzuhr.Start();
        _uhr.Tick += (_, _) =>
        {
            UhrZeigen();
            DarstellungAnwenden();
            if (Uhrebene.Visibility == Visibility.Visible) WortuhrZeichnen();
            JetztUhr.Text = DateTime.Now.ToString("HH:mm");
        };
        KeyDown += (_, e) =>
        {
            if (e.Key != System.Windows.Input.Key.Escape) return;
            // Die Rückfrage zuerst: Sie liegt über allem, und Esc heißt dort
            // „doch nicht schließen".
            if (Schliessebene.Visibility == Visibility.Visible) SchliessenAbbrechen(this, e);
            else if (Ueberebene.Visibility == Visibility.Visible) UeberSchliessen(this, e);
            else if (Uhrebene.Visibility == Visibility.Visible) WortuhrSchliessen(this, e);
            else if (Jetztebene.Visibility == Visibility.Visible) JetztSchliessen(this, e);
            else if (Senderebene.Visibility == Visibility.Visible) SenderSchliessen(this, e);
            else if (Einstellungsebene.Visibility == Visibility.Visible) EinstellungenSchliessen(this, e);
            else if (Tonebene.Visibility == Visibility.Visible) TonSchliessen(this, e);
        };
        _uhr.Start();
        _speicheruhr.Tick += (_, _) =>
        {
            _speicheruhr.Stop();
            _ablage.EinstellungenSchreiben(_einstellungen);
        };

        // Weiterhören, wo man aufgehört hat: Das Notebook klappt man zu und
        // wieder auf, und dann soll dasselbe spielen wie vorher.
        if (_einstellungen.LetzterPlatz > 0)
        {
            var letzter = _sender.FirstOrDefault(s => s.Platz == _einstellungen.LetzterPlatz);
            if (letzter is not null) Spielen(letzter);
        }
    }

    // ================================================== Stationstasten

    private void TastenZeichnen()
    {
        Tastenfeld.Children.Clear();
        for (var platz = 1; platz <= HauptPlaetze; platz++)
        {
            Tastenfeld.Children.Add(TasteBauen(platz, klein: false, ziehbar: false));
        }
        Tastenfeld.Children.Add(MehrTaste());

        // Ist das Blatt offen, zeigt es dieselben Sender -- neu zeichnen, sonst
        // stünde dort noch die alte Reihenfolge oder die alte Laufanzeige.
        if (Senderebene.Visibility == Visibility.Visible) SenderfeldZeichnen();
    }

    private void SenderfeldZeichnen()
    {
        Senderfeld.Children.Clear();
        for (var platz = 1; platz <= Plaetze; platz++)
        {
            Senderfeld.Children.Add(TasteBauen(platz, klein: true, ziehbar: true));
        }
    }

    /// <summary>Eine Stationstaste -- groß für den Hauptschirm, klein für das Blatt.</summary>
    private Button TasteBauen(int platz, bool klein, bool ziehbar)
    {
        var sender = _sender.FirstOrDefault(s => s.Platz == platz);
        var aktiv = sender is not null && _laeuft && _laufender?.Platz == platz;
        var stil = sender is null ? "StationstasteFrei" : aktiv ? "StationstasteAktiv" : "Stationstaste";
        var taste = new Button
        {
            Style = (Style)FindResource(stil),
            Content = Tasteninhalt(platz, sender, aktiv, klein),
            Tag = platz,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        if (klein) taste.Margin = new Thickness(6);
        taste.Click += TasteGedrueckt;
        // Rechtsklick nimmt den Sender von der Taste. Am Touchscreen ist das
        // der lange Druck: Windows macht aus Gedrückthalten von sich aus
        // einen Rechtsklick.
        var hier = platz;
        taste.MouseRightButtonUp += (_, _) => TasteRaeumen(hier);
        if (!ziehbar) return taste;

        taste.AllowDrop = true;
        taste.PreviewMouseLeftButtonDown += (_, e) =>
        {
            _druckpunkt = e.GetPosition(this);
            _druckplatz = hier;
        };
        taste.PreviewMouseMove += (o, e) => ZiehenBeginnen((Button)o, e, hier);
        taste.DragEnter += (o, _) => ZielMarkieren((Button)o, an: true);
        taste.DragLeave += (o, _) => ZielMarkieren((Button)o, an: false);
        taste.DragOver += (_, e) =>
        {
            e.Effects = e.Data.GetDataPresent(Ziehformat) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        };
        taste.Drop += (o, e) =>
        {
            ZielMarkieren((Button)o, an: false);
            Abgelegt(e, hier);
        };
        return taste;
    }

    /// <summary>
    /// Das neunte Feld: „Mehr". Es zeigt, wie viele Sender dahinterliegen --
    /// sonst weiß man nicht, ob sich das Öffnen lohnt.
    /// </summary>
    private Button MehrTaste()
    {
        var punkte = new System.Windows.Controls.Primitives.UniformGrid { Rows = 3, Columns = 3, Width = 44, Height = 44, HorizontalAlignment = HorizontalAlignment.Center };
        for (var i = 0; i < 9; i++)
        {
            punkte.Children.Add(new Border
            {
                Margin = new Thickness(2.5),
                CornerRadius = new CornerRadius(3),
                Background = (Brush)FindResource(i < 8 ? "Tinte2" : "Akzent"),
            });
        }
        var weitere = _sender.Count(s => s.Platz > HauptPlaetze);
        var inhalt = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        inhalt.Children.Add(punkte);
        inhalt.Children.Add(new TextBlock
        {
            Text = "Mehr",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 12, 0, 0),
        });
        inhalt.Children.Add(new TextBlock
        {
            Text = weitere > 0 ? $"{weitere} weitere · suchen · ordnen" : "Sender suchen · ordnen",
            FontSize = 14,
            Opacity = 0.75,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        var taste = new Button
        {
            Style = (Style)FindResource("Stationstaste"),
            Content = inhalt,
            Background = (Brush)FindResource("Flaeche2"),
        };
        System.Windows.Automation.AutomationProperties.SetName(taste, "Mehr");
        taste.Click += SenderOeffnen;
        return taste;
    }

    private UIElement Tasteninhalt(int platz, Sender? sender, bool aktiv, bool klein)
    {
        var flaeche = new Grid();

        // Die Nummer oben links: „Taste 4" ist, was man sich merkt und sagt.
        // Im Blatt sind die ersten acht farbig -- sie liegen auf dem
        // Hauptschirm.
        var nummer = new TextBlock
        {
            Text = platz.ToString(),
            FontSize = klein ? 13 : 14,
            Opacity = 0.7,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(2, 0, 0, 0),
        };
        // Die Farbe nur setzen, wenn sie abweicht. `Foreground = null` hieße
        // nicht „wie die Taste", sondern unsichtbar -- so verschwanden die
        // Nummern auf dem Hauptschirm im ersten Wurf.
        if (klein && platz <= HauptPlaetze)
        {
            nummer.Foreground = (Brush)FindResource("Akzent");
            nummer.FontWeight = FontWeights.Bold;
            nummer.Opacity = 1;
        }
        flaeche.Children.Add(nummer);

        if (aktiv)
        {
            var laeuft = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
            };
            laeuft.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = (Brush)FindResource("Akzent"), Margin = new Thickness(0, 0, klein ? 0 : 6, 0) });
            if (!klein)
            {
                laeuft.Children.Add(new TextBlock { Text = "LÄUFT", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Akzent") });
            }
            flaeche.Children.Add(laeuft);
        }

        var mitte = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        if (sender is null)
        {
            // Eine freie Taste sagt, was zu tun ist. „Leer" allein lässt ratlos,
            // wie sie voll wird.
            var ring = new Border
            {
                Width = klein ? 34 : 52,
                Height = klein ? 34 : 52,
                CornerRadius = new CornerRadius(klein ? 17 : 26),
                BorderThickness = new Thickness(2),
                BorderBrush = (Brush)FindResource("LinieStark"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new Path { Style = (Style)FindResource("Symbol"), Data = (Geometry)FindResource("IconPlus"), HorizontalAlignment = HorizontalAlignment.Center },
            };
            mitte.Children.Add(ring);
            mitte.Children.Add(new TextBlock { Text = "frei", FontSize = klein ? 14 : 17, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, klein ? 4 : 8, 0, 0) });
            if (!klein)
            {
                mitte.Children.Add(new TextBlock { Text = "Sender suchen", FontSize = 14, Opacity = 0.75, HorizontalAlignment = HorizontalAlignment.Center });
            }
        }
        else
        {
            mitte.Children.Add(klein ? Senderbild(sender, 44, 11, 16) : Senderbild(sender, 72, 16, 24));
            mitte.Children.Add(new TextBlock
            {
                Text = sender.Name,
                FontSize = klein ? 15 : 22,
                FontWeight = aktiv ? FontWeights.Bold : FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                TextWrapping = klein ? TextWrapping.NoWrap : TextWrapping.Wrap,
                MaxHeight = klein ? 22 : 56,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, klein ? 6 : 12, 0, 0),
            });
            var unten = Herkunft(sender, mitCodec: false);
            if (unten.Length > 0 && !klein)
            {
                mitte.Children.Add(new TextBlock
                {
                    Text = unten,
                    FontSize = 14,
                    Opacity = 0.75,
                    TextAlignment = TextAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 2, 0, 0),
                });
            }
        }
        flaeche.Children.Add(mitte);
        return flaeche;
    }

    /// <summary>
    /// Das Bild eines Senders: sein Logo, und bis es da ist -- oder wenn es
    /// keins gibt -- ein Kürzel auf einer Farbe, die zum Namen gehört.
    ///
    /// Das Kürzel steht nicht als Notbehelf da, sondern gleichwertig: Viele
    /// Logos im Verzeichnis fehlen, sind tot oder winzig. Eine Taste ohne Bild
    /// erkennt man schlechter als eine mit Kürzel.
    /// </summary>
    private Border Senderbild(Sender sender, double groesse, double radius, double schrift)
    {
        var kuerzel = new TextBlock
        {
            Text = Monogramm.Kuerzel(sender.Name),
            FontSize = schrift,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var logo = new Image { Stretch = Stretch.Uniform, Visibility = Visibility.Collapsed };
        var rahmen = new Border
        {
            Width = groesse,
            Height = groesse,
            CornerRadius = new CornerRadius(radius),
            Background = new SolidColorBrush(Monogramm.Farbe(sender.Name)),
            HorizontalAlignment = HorizontalAlignment.Center,
            ClipToBounds = true,
            Child = new Grid { Children = { kuerzel, logo } },
        };
        _ = LogoEinsetzen(rahmen, kuerzel, logo, sender);
        return rahmen;
    }

    /// <summary>
    /// Kommt das Logo an, tritt es an die Stelle des Kürzels. Der Grund
    /// darunter ist die Randfarbe des Logos selbst (siehe `Logogrund`).
    /// </summary>
    /// <summary>
    /// Stationstasten aus der Zeit vor Bau 15 kennen ihre Homepage nicht – die
    /// wurde nicht gespeichert. Ohne sie gibt es für Sender ohne Logo im
    /// Verzeichnis keinen Ersatz. Einmal nachfragen, in einer Anfrage für
    /// alle, und merken; danach ist die Liste ergänzt und es wird nicht mehr
    /// gefragt. Ohne Netz bleibt alles, wie es ist, und beim nächsten Start
    /// wird es wieder versucht.
    /// </summary>
    private async Task HomepagesNachholen()
    {
        var offen = _sender.Where(s => s.Homepage.Length == 0 && s.Kennung.Length > 0).ToList();
        if (offen.Count == 0) return;
        List<Treffer> treffer;
        try
        {
            treffer = await _katalog.NachKennung(offen.Select(s => s.Kennung));
        }
        catch (KatalogFehler)
        {
            return;
        }
        var neu = false;
        foreach (var t in treffer.Where(t => t.Homepage.Length > 0))
        {
            foreach (var s in offen.Where(s => s.Kennung == t.Kennung))
            {
                s.Homepage = t.Homepage;
                neu = true;
            }
        }
        if (!neu) return;
        _ablage.SenderSchreiben(_sender);
        TastenZeichnen();
        if (_laufender is not null && offen.Contains(_laufender)) LaufendesZeigen();
    }

    private async Task LogoEinsetzen(Border rahmen, TextBlock kuerzel, Image logo, Sender sender)
    {
        var bild = await _logos.HolenFuer(sender.Logo, sender.Homepage);
        if (bild is null) return;
        logo.Source = bild;
        logo.Margin = new Thickness(rahmen.Width * 0.08);
        logo.Visibility = Visibility.Visible;
        kuerzel.Visibility = Visibility.Collapsed;
        rahmen.Background = Logogrund(bild);
    }

    /// <summary>
    /// Der Grund hinter einem Logo: die Farbe seiner Ecken, wenn sie deckend
    /// sind, sonst Weiß.
    ///
    /// Bis Bau 7 lag jedes Logo mit etwas Abstand auf Weiß. Logos mit eigenem
    /// Hintergrund (80s80s auf Lila, SWR1 auf Gelb) bekamen so einen weißen
    /// Ring, der in den dunklen Themen grell durchschien. Mit der Eckfarbe als
    /// Grund läuft das Logo bis an den Rand weiter. Nur Logos mit
    /// durchsichtigen Ecken brauchen wirklich Weiß: Sie sind fast immer
    /// dunkle Schrift, gemacht für hellen Grund, und verschwänden auf Nacht.
    /// </summary>
    private static Brush Logogrund(BitmapSource bild)
    {
        try
        {
            var bgra = new FormatConvertedBitmap(bild, PixelFormats.Bgra32, null, 0);
            int b = bgra.PixelWidth, h = bgra.PixelHeight;
            if (b < 2 || h < 2) return Brushes.White;
            var punkt = new byte[4];
            int summeR = 0, summeG = 0, summeB = 0;
            foreach (var (x, y) in new[] { (0, 0), (b - 1, 0), (0, h - 1), (b - 1, h - 1) })
            {
                bgra.CopyPixels(new Int32Rect(x, y, 1, 1), punkt, 4, 0);
                if (punkt[3] < 200) return Brushes.White;
                summeB += punkt[0];
                summeG += punkt[1];
                summeR += punkt[2];
            }
            var grund = new SolidColorBrush(Color.FromRgb((byte)(summeR / 4), (byte)(summeG / 4), (byte)(summeB / 4)));
            grund.Freeze();
            return grund;
        }
        catch (Exception fehler) when (fehler is NotSupportedException or InvalidOperationException or ArgumentException)
        {
            return Brushes.White;
        }
    }

    private static string Herkunft(Sender sender, bool mitCodec)
    {
        var teile = new List<string> { sender.Land, sender.Genre };
        if (mitCodec && sender.Codec.Length > 0)
        {
            teile.Add(sender.Bitrate > 0 ? $"{sender.Codec} {sender.Bitrate} kBit/s" : sender.Codec);
        }
        return string.Join(" · ", teile.Where(t => t.Length > 0));
    }

    private void TasteGedrueckt(object absender, RoutedEventArgs e)
    {
        var platz = (int)((Button)absender).Tag;
        var sender = _sender.FirstOrDefault(s => s.Platz == platz);
        if (sender is null)
        {
            SucheOeffnen(platz);
            return;
        }
        // Im Blatt: spielen und zumachen. Man hat gewählt, jetzt will man
        // sehen, was läuft.
        if (Senderebene.Visibility == Visibility.Visible)
        {
            if (!(_laeuft && _laufender?.Platz == platz)) Spielen(sender);
            SenderSchliessen(this, e);
            return;
        }
        // Dieselbe Taste noch einmal: aus. So wie am Autoradio, und es erspart
        // den Weg zum Stopp-Knopf.
        if (_laeuft && _laufender?.Platz == platz) Stoppen(this, e);
        else Spielen(sender);
    }

    // ------------------------------------------------ Blatt „Alle Sender"

    private void SenderOeffnen(object absender, RoutedEventArgs e)
    {
        SenderfeldZeichnen();
        Senderebene.Visibility = Visibility.Visible;
    }

    private void SenderSchliessen(object absender, RoutedEventArgs e) =>
        Senderebene.Visibility = Visibility.Collapsed;

    // ------------------------------------------------ Ziehen und ablegen

    private const string Ziehformat = "REGOradio.Platz";
    private Point _druckpunkt;
    private int _druckplatz;

    /// <summary>
    /// Ein Ziehen beginnt erst nach 24 Punkten Weg -- deutlich mehr als die
    /// Windows-Vorgabe von 4. Am Touchscreen wackelt der Finger beim Tippen,
    /// und sonst würde aus jedem Tipp auf einen Sender ein abgebrochenes
    /// Ziehen statt eines Senderwechsels.
    /// </summary>
    private void ZiehenBeginnen(Button taste, System.Windows.Input.MouseEventArgs e, int platz)
    {
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed || _druckplatz != platz) return;
        if (_sender.All(s => s.Platz != platz)) return;   // freie Tasten zieht man nicht

        var weg = e.GetPosition(this) - _druckpunkt;
        if (Math.Abs(weg.X) < 24 && Math.Abs(weg.Y) < 24) return;

        _druckplatz = 0;
        taste.Opacity = 0.45;
        DragDrop.DoDragDrop(taste, new DataObject(Ziehformat, platz), DragDropEffects.Move);
        taste.Opacity = 1;
    }

    private void ZielMarkieren(Button taste, bool an)
    {
        if (an)
        {
            taste.Background = (Brush)FindResource("AkzentWeich");
            taste.BorderBrush = (Brush)FindResource("Akzent");
        }
        else
        {
            // Zurück auf das, was der Stil sagt -- nicht auf eine gemerkte
            // Farbe, die nach einem Wechsel zu Nacht nicht mehr stimmt.
            taste.ClearValue(BackgroundProperty);
            taste.ClearValue(BorderBrushProperty);
        }
    }

    private void Abgelegt(DragEventArgs e, int nach)
    {
        if (e.Data.GetData(Ziehformat) is not int von) return;
        if (!Tastenbelegung.Tauschen(_sender, von, nach)) return;

        // Die Taste, die beim nächsten Start wieder spielt, wandert mit.
        if (_einstellungen.LetzterPlatz == von) _einstellungen.LetzterPlatz = nach;
        else if (_einstellungen.LetzterPlatz == nach) _einstellungen.LetzterPlatz = von;

        _ablage.SenderSchreiben(_sender);
        _ablage.EinstellungenSchreiben(_einstellungen);
        TastenZeichnen();
        // `_laufender` ist derselbe Sender wie auf der Taste; seine neue
        // Nummer steht damit schon drin -- die Anzeige „LÄUFT · TASTE n"
        // muss nur neu gezeichnet werden.
        LaufendesZeigen();
    }

    private void TasteRaeumen(int platz)
    {
        var sender = _sender.FirstOrDefault(s => s.Platz == platz);
        if (sender is null) return;
        if (MessageBox.Show($"„{sender.Name}“ von Taste {platz} nehmen?", "REGOradio",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        _sender.Remove(sender);
        _ablage.SenderSchreiben(_sender);
        if (_laufender?.Platz == platz) Stoppen(this, new RoutedEventArgs());
        TastenZeichnen();
    }

    // ============================================================ Spielen

    private void Spielen(Sender sender)
    {
        _laufender = sender;
        _zuletzt = sender;
        _einstellungen.LetzterPlatz = sender.Platz;
        _ablage.EinstellungenSchreiben(_einstellungen);
        _abspieler.Spiele(sender.Adresse, sender.Name);
        LaufendesZeigen();
        TastenZeichnen();
    }

    private void Stoppen(object absender, RoutedEventArgs e)
    {
        _abspieler.Stopp();
        _laufender = null;
        _einstellungen.LetzterPlatz = 0;
        _ablage.EinstellungenSchreiben(_einstellungen);
        LaufendesZeigen();
        TastenZeichnen();
    }

    private void TonstandZeigen(Tonstand stand)
    {
        var vorher = _laeuft;
        _laeuft = stand.Laeuft;

        // KEIN RUHEZUSTAND, SOLANGE ETWAS LÄUFT -- sofern gewünscht. Windows
        // zählt Tastendrücke, nicht Töne, und wer Radio hört, drückt nichts.
        // Gesetzt wird das hier, weil diese Methode immer im Oberflächenfaden
        // läuft: Der Wachzustand hängt an dem Faden, der ihn setzt.
        Wachhalter.Setzen(stand.Laeuft && _einstellungen.KeinRuhezustand);

        FehlerZeile.Text = stand.Fehler;
        FehlerZeile.Visibility = stand.Fehler.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        LaufendesZeigen();
        if (stand.Titel != _titelGezeigt)
        {
            _titelGezeigt = stand.Titel;
            TitelZeigen();
        }
        // Nur neu zeichnen, wenn sich am Laufen etwas geändert hat -- sonst
        // flackern die Logos bei jeder Meldung.
        if (vorher != _laeuft) TastenZeichnen();
    }

    private void LaufendesZeigen()
    {
        if (_laufender is null)
        {
            LaeuftZeile.Text = "NICHTS LÄUFT";
            SenderZeile.Text = "Taste antippen";
            MetaZeile.Text = "";
            LaufendesKuerzel.Text = "";
            LaufendesLogo.Source = null;
            LaufendesBild.Background = (Brush)FindResource("Flaeche2");
            TitelZeigen();
            return;
        }

        LaeuftZeile.Text = !_laeuft ? "WIRD VERBUNDEN …"
            : _laufender.Platz > 0 ? $"LÄUFT · TASTE {_laufender.Platz}" : "LÄUFT · ZUM ANHÖREN";
        SenderZeile.Text = _laufender.Name;
        MetaZeile.Text = Herkunft(_laufender, mitCodec: true);
        LaufendesKuerzel.Text = Monogramm.Kuerzel(_laufender.Name);
        LaufendesKuerzel.Visibility = Visibility.Visible;
        LaufendesLogo.Source = null;
        LaufendesBild.Background = new SolidColorBrush(Monogramm.Farbe(_laufender.Name));
        _ = LaufendesLogoLaden(_laufender);
        TitelZeigen();
    }

    private async Task LaufendesLogoLaden(Sender sender)
    {
        var bild = await _logos.HolenFuer(sender.Logo, sender.Homepage);
        // Inzwischen umgeschaltet? Dann gehört das Bild nicht mehr hierher.
        if (bild is null || _laufender != sender) return;
        LaufendesLogo.Source = bild;
        LaufendesKuerzel.Visibility = Visibility.Collapsed;
        LaufendesBild.Background = Logogrund(bild);
    }

    // ======================================================= Lied und Cover

    /// <summary>
    /// Titel und Interpret aus der Meldung des Senders, dazu das Cover.
    ///
    /// Solange der Sender keinen Titel meldet -- viele tun das nie, und bei
    /// HLS gibt es keinen --, steht der Sender an seiner Stelle, mit Logo
    /// statt Cover. Das ist keine Fehlanzeige, sondern alles, was es zu
    /// wissen gibt.
    /// </summary>
    private void TitelZeigen()
    {
        var (interpret, titel) = Cover.Zerlege(_titelGezeigt);
        if (_laufender is null)
        {
            TitelZeile.Text = "";
            InterpretZeile.Text = "";
        }
        else if (_titelGezeigt.Length == 0)
        {
            TitelZeile.Text = _laeuft ? "Kein Titel vom Sender" : "";
            InterpretZeile.Text = "";
        }
        else
        {
            TitelZeile.Text = titel;
            InterpretZeile.Text = interpret;
        }

        JetztSender.Text = _laufender is null ? "" : $"LÄUFT · {_laufender.Name.ToUpperInvariant()}";
        JetztTitel.Text = _titelGezeigt.Length > 0 ? titel : _laufender?.Name ?? "";
        JetztInterpret.Text = _titelGezeigt.Length > 0 ? interpret : "";

        _coverAdresse = "";
        CoverSetzen(null);
        MedienZeigen();
        _ = CoverLaden(_titelGezeigt, _laufender);
    }

    /// <summary>
    /// Erst das Cover zum Lied suchen, sonst das Senderlogo nehmen, sonst das
    /// Kürzel. Kommt die Antwort, nachdem schon das nächste Lied läuft, wird
    /// sie verworfen -- sonst stünde das Cover des vorigen Lieds da.
    /// </summary>
    private async Task CoverLaden(string icyTitel, Sender? sender)
    {
        ImageSource? bild = null;
        var istLogo = false;
        var coverAdresse = "";
        if (icyTitel.Length > 0)
        {
            coverAdresse = await _cover.AdresseSuchen(icyTitel);
            if (coverAdresse.Length > 0) bild = await _logos.Holen(coverAdresse, breite: 600);
        }
        if (bild is null && sender is not null)
        {
            bild = await _logos.HolenFuer(sender.Logo, sender.Homepage, breite: 400);
            istLogo = bild is not null;
        }
        if (icyTitel != _titelGezeigt || sender != _laufender) return;
        _coverAdresse = bild is not null && !istLogo ? coverAdresse : "";
        CoverSetzen(bild, istLogo);
        MedienZeigen();
    }

    /// <summary>Das Bild in die kleine Kachel, ins Vollbild und in den Hintergrund.</summary>
    private void CoverSetzen(ImageSource? bild, bool istLogo = false)
    {
        _coverBild = bild;
        var name = _laufender?.Name ?? "";
        CoverKuerzel.Text = bild is null && name.Length > 0 ? Monogramm.Kuerzel(name) : "";
        JetztKuerzel.Text = CoverKuerzel.Text;

        if (bild is null)
        {
            var farbe = name.Length > 0 ? new SolidColorBrush(Monogramm.Farbe(name)) : (Brush)FindResource("Flaeche2");
            CoverRahmen.Background = farbe;
            JetztCover.Background = name.Length > 0 ? farbe : new SolidColorBrush(Color.FromRgb(0x1F, 0x22, 0x25));
            JetztHintergrund.Source = null;
            return;
        }

        // Ein Logo braucht Luft, und darunter seine eigene Randfarbe
        // (`Logogrund`) -- mit Weiß stand um Logos mit eigenem Hintergrund
        // auch hier ein greller Ring. Ein Cover füllt die Fläche.
        var pinsel = new ImageBrush(bild)
        {
            Stretch = istLogo ? Stretch.Uniform : Stretch.UniformToFill,
            Viewport = istLogo ? new Rect(0.12, 0.12, 0.76, 0.76) : new Rect(0, 0, 1, 1),
        };
        if (istLogo)
        {
            var mitGrund = new DrawingBrush(new DrawingGroup
            {
                Children =
                {
                    new GeometryDrawing(bild is BitmapSource quelle ? Logogrund(quelle) : Brushes.White, null,
                        new RectangleGeometry(new Rect(0, 0, 1, 1))),
                    new GeometryDrawing(pinsel, null, new RectangleGeometry(new Rect(0, 0, 1, 1))),
                },
            });
            CoverRahmen.Background = mitGrund;
            JetztCover.Background = mitGrund;
        }
        else
        {
            CoverRahmen.Background = pinsel;
            JetztCover.Background = pinsel;
        }
        JetztHintergrund.Source = bild;
    }

    // ======================================================== Jetzt läuft

    private void JetztOeffnen(object absender, RoutedEventArgs e)
    {
        // Umschalten statt nur öffnen -- aus demselben Grund wie bei der Wortuhr:
        // Die Bedienhilfen von Windows erreichen den Knopf auch, wenn das
        // Vollbild darüberliegt.
        if (Jetztebene.Visibility == Visibility.Visible)
        {
            JetztSchliessen(absender, e);
            return;
        }
        if (_laufender is null) return;   // nichts läuft -- nichts zu zeigen
        JetztUhr.Text = DateTime.Now.ToString("HH:mm");
        VollbildAn();
        Jetztebene.Visibility = Visibility.Visible;
        Focus();
    }

    private void JetztSchliessen(object absender, RoutedEventArgs e)
    {
        Jetztebene.Visibility = Visibility.Collapsed;
        VollbildAus();
    }

    // ------------------------------------------ Vollbild (Uhr und Lied)

    private WindowState _vorherZustand;
    private WindowStyle _vorherStil;
    private ResizeMode _vorherGroesse;
    private bool _imVollbild;

    /// <summary>
    /// Über die Taskleiste, nicht nur maximiert. Randlos wird ein Fenster erst
    /// über die Taskleiste gezogen, wenn es beim Umschalten auf `Maximized`
    /// bereits randlos ist; deshalb der Umweg über `Normal`. Vorher wird
    /// gemerkt, wie das Fenster war, damit „zurück" auch dorthin führt.
    /// </summary>
    private void VollbildAn()
    {
        if (_imVollbild) return;
        _imVollbild = true;
        // Kein Mauszeiger über Cover und Wortuhr: Er stünde sonst mitten im
        // Bild. OverrideCursor statt Cursor an der Ebene, weil er sofort
        // gilt -- auch wenn das Handy umschaltet und die Maus sich nicht
        // bewegt. Er gilt nur über den Fenstern von REGOradio.
        Mouse.OverrideCursor = Cursors.None;
        TooltipsSchliessen();
        _vorherZustand = WindowState;
        _vorherStil = WindowStyle;
        _vorherGroesse = ResizeMode;
        WindowState = WindowState.Normal;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowState = WindowState.Maximized;
    }

    /// <summary>
    /// Offene Tooltips schließen. Wer mit der Maus auf Uhr oder Cover tippt,
    /// hat den Zeiger über dem Knopf stehen; dessen Tooltip blieb über dem
    /// Vollbild stehen, weil WPF ihn erst schließt, wenn die Maus sich
    /// bewegt. Tooltips leben in eigenen Popup-Fenstern -- die werden hier
    /// durchsucht.
    /// </summary>
    private static void TooltipsSchliessen()
    {
        foreach (var quelle in PresentationSource.CurrentSources.OfType<PresentationSource>().ToList())
        {
            if (quelle.RootVisual is { } wurzel) TooltipSuchen(wurzel);
        }
    }

    private static void TooltipSuchen(DependencyObject knoten)
    {
        if (knoten is ToolTip tipp)
        {
            tipp.IsOpen = false;
            return;
        }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(knoten); i++)
        {
            TooltipSuchen(VisualTreeHelper.GetChild(knoten, i));
        }
    }

    private void VollbildAus()
    {
        if (!_imVollbild) return;
        _imVollbild = false;
        Mouse.OverrideCursor = null;
        WindowState = WindowState.Normal;
        WindowStyle = _vorherStil;
        ResizeMode = _vorherGroesse;
        WindowState = _vorherZustand;
    }

    // ========================================================= Lautstärke

    // Ein Schritt je Tipp. Fünfer-Schritte waren am Abend zu grob: Zwischen
    // „zu leise" und „zu laut" lag oft genau ein Tipp. Wer schnell weit will,
    // hält fest -- die Tasten wiederholen dann von selbst -- oder zieht am
    // Balken.
    private void Leiser(object absender, RoutedEventArgs e) => LautstaerkeSetzen(_abspieler.Lautstaerke - 1);

    private void Lauter(object absender, RoutedEventArgs e) => LautstaerkeSetzen(_abspieler.Lautstaerke + 1);

    private void LautstaerkeGeschoben(object absender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Ohne diese Schranke dreht sich die Anzeige im Kreis: Wir setzen den
        // Balken, der meldet eine Änderung, die wieder die Lautstärke setzt.
        if (_lautstaerkeVonUns || !IsLoaded) return;
        LautstaerkeSetzen((int)Math.Round(e.NewValue), balkenSchonRichtig: true);
    }

    private void LautstaerkeSetzen(int wert, bool balkenSchonRichtig = false)
    {
        // Wer an der Lautstärke dreht, will etwas hören: Stumm ist damit vorbei.
        if (_abspieler.Stumm) StummSetzen(false);

        _abspieler.Lautstaerke = wert;
        _einstellungen.Lautstaerke = _abspieler.Lautstaerke;
        _speicheruhr.Stop();
        _speicheruhr.Start();
        if (!balkenSchonRichtig) LautstaerkeZeigen(_abspieler.Lautstaerke);
        else LautstaerkeZahl.Text = $"{_abspieler.Lautstaerke}";
    }

    private void LautstaerkeZeigen(int wert)
    {
        _lautstaerkeVonUns = true;
        LautstaerkeBalken.Value = wert;
        _lautstaerkeVonUns = false;
        LautstaerkeZahl.Text = $"{wert}";
    }

    private void StummUmschalten(object absender, RoutedEventArgs e) => StummSetzen(!_abspieler.Stumm);

    private void StummSetzen(bool stumm)
    {
        _abspieler.Stumm = stumm;
        StummText.Text = stumm ? "Ton an" : "Stumm";
        LautstaerkeText.Text = stumm ? "Stumm" : "Lautstärke";
        // Die Säule wird mit blass: Eine volle grüne Füllung behauptet Ton,
        // wo keiner ist.
        LautstaerkeZahl.Opacity = stumm ? 0.35 : 1;
        LautstaerkeBalken.Opacity = stumm ? 0.4 : 1;
        StummKnopf.Background = (Brush)FindResource(stumm ? "AkzentWeich" : "Flaeche2");
        StummKnopf.BorderBrush = (Brush)FindResource(stumm ? "Akzent" : "LinieStark");
    }

    // ================================================= Kopfzeile: Netz, Uhr

    private void UhrZeigen() => Uhrzeit.Text = DateTime.Now.ToString("HH:mm");

    private void NetzZeigen()
    {
        var lage = Wlan.Lesen();
        // Nur ein Symbol oben: Den Namen gibt es als Hinweis und für die
        // Bedienhilfen. Ohne Verbindung wird das Symbol rot -- das ist im Hotel
        // die Antwort auf „warum spielt nichts", und sie soll auffallen.
        var text = lage.Verbunden ? $"WLAN: {lage.Anzeige}" : "Kein WLAN";
        WlanKnopf.ToolTip = text;
        System.Windows.Automation.AutomationProperties.SetName(WlanKnopf, text);
        if (lage.Verbunden) WlanSymbol.ClearValue(Shape.StrokeProperty);
        else WlanSymbol.Stroke = (Brush)FindResource("Warnung");
    }

    /// <summary>
    /// Ins WLAN-Menü von Windows -- die Liste der Netze im Infobereich, nicht
    /// die große Einstellungsseite: Dort wählt man ein Netz mit einem Tipp.
    /// Gibt es die Liste nicht (ältere Windows-Fassungen), die Seite.
    /// </summary>
    private void WlanOeffnen(object absender, RoutedEventArgs e)
    {
        if (!WindowsOeffnen("ms-availablenetworks:")) WindowsOeffnen("ms-settings:network-wifi");
    }

    private static bool WindowsOeffnen(string adresse)
    {
        try
        {
            Process.Start(new ProcessStartInfo(adresse) { UseShellExecute = true });
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // ============================================================ Wortuhr

    /// <summary>Die Wortuhr im Vollbild -- über der Taskleiste, nicht nur maximiert.</summary>
    private void WortuhrOeffnen(object absender, RoutedEventArgs e)
    {
        // Schon offen? Dann ist das ein Umschalten. Mit dem Finger kommt man
        // an den Knopf nicht heran, solange die Uhr darüberliegt -- wohl aber
        // die Bedienhilfen von Windows, und die sollen die Uhr auch schließen
        // können.
        if (Uhrebene.Visibility == Visibility.Visible)
        {
            WortuhrSchliessen(absender, e);
            return;
        }
        if (Wortfeld.Children.Count == 0) WortfeldAnlegen();
        WortuhrZeichnen();
        VollbildAn();
        Uhrebene.Visibility = Visibility.Visible;
        Focus();
    }

    private void WortuhrSchliessen(object absender, RoutedEventArgs e)
    {
        Uhrebene.Visibility = Visibility.Collapsed;
        VollbildAus();
    }

    /// <summary>Die 110 Buchstaben einmal anlegen; danach wechselt nur die Farbe.</summary>
    private void WortfeldAnlegen()
    {
        foreach (var zeile in Wortuhr.Feld)
        {
            foreach (var buchstabe in zeile)
            {
                Wortfeld.Children.Add(new TextBlock
                {
                    Text = buchstabe.ToString(),
                    Width = 96,
                    Height = 96,
                    FontSize = 64,
                    FontWeight = FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    Padding = new Thickness(0, 6, 0, 0),
                });
            }
        }
    }

    /// <summary>
    /// Leuchtende Buchstaben hell, die übrigen fast so dunkel wie der Grund --
    /// sichtbar genug, dass man das Raster ahnt, nicht so hell, dass man es
    /// liest. Nachts werden auch die leuchtenden gedämpft: Die Uhr steht im
    /// dunklen Zimmer und soll es nicht erhellen.
    /// </summary>
    private void WortuhrZeichnen()
    {
        var an = Wortuhr.Leuchtet(DateTime.Now);
        var nacht = ((App)Application.Current).IstNacht;
        var hell = new SolidColorBrush(nacht ? Color.FromRgb(0x8A, 0x85, 0x7C) : Color.FromRgb(0xEC, 0xE8, 0xE0));
        var dunkel = new SolidColorBrush(Color.FromRgb(0x1E, 0x1F, 0x21));
        hell.Freeze();
        dunkel.Freeze();

        for (var i = 0; i < Wortfeld.Children.Count; i++)
        {
            var feld = (TextBlock)Wortfeld.Children[i];
            feld.Foreground = an.Contains((i / 11, i % 11)) ? hell : dunkel;
        }
        Uhrebene.ToolTip = Wortuhr.Satz(DateTime.Now);
        System.Windows.Automation.AutomationProperties.SetName(Uhrebene, Wortuhr.Satz(DateTime.Now));
    }

    // ======================================================= Wohin der Ton geht

    /// <summary>
    /// Das Ton-Symbol oben zeigt, wohin der Ton gerade geht: Bluetooth-Symbol
    /// für eine Box, sonst Lautsprecher. Den Gerätenamen gibt es als Hinweis.
    /// </summary>
    private void TonSymbolZeigen()
    {
        var liste = Ausgaenge.Lesen();
        var gewaehlt = liste.FirstOrDefault(a => a.Kennung == _einstellungen.Ausgang)
                       ?? liste.FirstOrDefault(a => a.IstStandard);
        TonSymbol.Data = (Geometry)FindResource(gewaehlt?.Bluetooth == true ? "IconBluetooth" : "IconLautsprecher");
        var text = gewaehlt is null ? "Kein Audiogerät" : $"Ton geht an: {gewaehlt.Anzeige}";
        TonKnopf.ToolTip = text;
    }

    private void TonOeffnen(object absender, RoutedEventArgs e)
    {
        GeraetelisteZeichnen();
        Tonebene.Visibility = Visibility.Visible;
    }

    private void TonSchliessen(object absender, RoutedEventArgs e)
    {
        Tonebene.Visibility = Visibility.Collapsed;
        TonSymbolZeigen();
    }

    private void AusgangWechseln(string kennung)
    {
        _einstellungen.Ausgang = kennung;
        _ablage.EinstellungenSchreiben(_einstellungen);
        _abspieler.Ausgang(kennung, _laeuft ? _laufender?.Adresse ?? "" : "", _laufender?.Name ?? "");
    }

    // ======================================================= Einstellungen

    private void EinstellungenOeffnen(object absender, RoutedEventArgs e)
    {
        DarstellungTag.IsChecked = _einstellungen.Darstellung == "tag";
        DarstellungNacht.IsChecked = _einstellungen.Darstellung == "nacht";
        DarstellungAuto.IsChecked = _einstellungen.Darstellung is not ("tag" or "nacht");
        _themaWirdGezeigt = true;
        ThemaWahl.ItemsSource ??= App.Themen;
        ThemaWahl.SelectedItem = App.Themen.FirstOrDefault(t => t.Schluessel == ((App)Application.Current).Thema);
        _themaWirdGezeigt = false;

        SchliessartZeigen();
        RuhezustandSchalter.IsChecked = _einstellungen.KeinRuhezustand;
        // Nachsehen, nicht erinnern: Der Autostart lässt sich auch im
        // Task-Manager abschalten.
        AutostartSchalter.IsChecked = Autostart.Aktiv;
        FernSchalter.IsChecked = _einstellungen.FernAn;
        FernTafelZeigen();
        HelligkeitZeigen();

        VersionZeile.Text = $"{Bau.Programm} {Bau.Version} · Bau {Bau.Nummer} · {Bau.Copyright}";
        Einstellungsebene.Visibility = Visibility.Visible;
    }

    private void EinstellungenSchliessen(object absender, RoutedEventArgs e)
    {
        Einstellungsebene.Visibility = Visibility.Collapsed;
    }

    // ===================================================== Über REGOradio

    /// <summary>
    /// Das Blatt „Über REGOradio" – aus den Einstellungen und aus dem
    /// Tray-Menü. Kommt es aus dem Tray, kann das Fenster versteckt sein oder
    /// gerade die Wortuhr im Vollbild zeigen; dann erst zurückholen.
    /// </summary>
    public void UeberZeigen()
    {
        if (Uhrebene.Visibility == Visibility.Visible) WortuhrSchliessen(this, new RoutedEventArgs());
        if (Jetztebene.Visibility == Visibility.Visible) JetztSchliessen(this, new RoutedEventArgs());
        UeberOeffnen(this, new RoutedEventArgs());
    }

    private void UeberOeffnen(object absender, RoutedEventArgs e)
    {
        UeberSymbol.Source ??= GroessteFassung(new Uri("pack://application:,,,/symbol.ico"));
        UeberBau.Text = $"Fassung {Bau.Version} · Bau {Bau.Nummer} · {Bau.Stand}";
        UeberAblage.Text = _ablage.Verzeichnis;
        UeberKopf.Background = new LinearGradientBrush(
            ((SolidColorBrush)FindResource("Flaeche")).Color,
            ((SolidColorBrush)FindResource("Grund")).Color,
            new Point(0, 0), new Point(1, 1));
        Ueberebene.Visibility = Visibility.Visible;
    }

    private void UeberSchliessen(object absender, RoutedEventArgs e) =>
        Ueberebene.Visibility = Visibility.Collapsed;

    /// <summary>
    /// Die größte Fassung aus einer .ico. Ein `Image` mit der .ico als Quelle
    /// nähme die erste, und das ist in symbol.ico eine der kleinen – auf 112
    /// Punkte gezogen sähe man die Pixel.
    /// </summary>
    private static BitmapSource? GroessteFassung(Uri quelle)
    {
        try
        {
            var dekoder = BitmapDecoder.Create(quelle, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            return dekoder.Frames.OrderByDescending(f => f.PixelWidth).FirstOrDefault();
        }
        catch (Exception fehler) when (fehler is System.IO.IOException or NotSupportedException or System.IO.FileFormatException)
        {
            return null;
        }
    }

    private void UeberAblageOeffnen(object absender, RoutedEventArgs e)
    {
        // UseShellExecute: Ohne das sucht .NET eine ausführbare Datei, findet
        // einen Ordner und wirft.
        try
        {
            Process.Start(new ProcessStartInfo { FileName = _ablage.Verzeichnis, UseShellExecute = true });
        }
        catch (Exception fehler) when (fehler is Win32Exception or System.IO.IOException)
        {
            UeberAblage.Text = $"Ließ sich nicht öffnen: {fehler.Message}";
        }
    }

    // ======================================================== Helligkeit

    // Anfangs true: Schon beim Aufbau feuert der Regler ValueChanged (der Stil
    // hebt ihn auf sein Minimum), und im ersten Bau hat genau das den
    // Bildschirm auf 5 % gedreht. Erst wer den Stand von Windows gelesen hat,
    // darf ihn auch setzen.
    private bool _helligkeitWirdGezeigt = true;
    private int _helligkeitZiel = -1;
    private bool _helligkeitUnterwegs;

    /// <summary>
    /// Den Regler auf den Stand von Windows bringen. Jedes Mal lesen, nicht
    /// merken: Fn-Tasten und Energiesparen drehen daran, ohne zu fragen.
    /// </summary>
    private async void HelligkeitZeigen()
    {
        _helligkeitWirdGezeigt = true;
        var wert = await Task.Run(Helligkeit.Lesen);
        HelligkeitZeile.Visibility = wert is null ? Visibility.Collapsed : Visibility.Visible;
        if (wert is null) return;
        HelligkeitRegler.Value = Math.Max(wert.Value, Helligkeit.Minimum);
        _helligkeitWirdGezeigt = false;
    }

    private void HelligkeitGeschoben(object absender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_helligkeitWirdGezeigt) return;
        HelligkeitSetzen((int)Math.Round(e.NewValue));
    }

    /// <summary>
    /// Helligkeit setzen, ohne den Regler zu bremsen. WMI braucht pro Aufruf
    /// bis zu 100 ms; beim Schieben kommen aber Dutzende Werte je Sekunde.
    /// Deshalb läuft höchstens ein Aufruf zur Zeit, und danach wird nur der
    /// letzte Wunsch nachgeschoben -- die Zwischenwerte will niemand sehen.
    /// </summary>
    private async void HelligkeitSetzen(int wert)
    {
        _helligkeitZiel = wert;
        if (_helligkeitUnterwegs) return;
        _helligkeitUnterwegs = true;
        try
        {
            while (_helligkeitZiel >= 0)
            {
                var ziel = _helligkeitZiel;
                _helligkeitZiel = -1;
                await Task.Run(() => Helligkeit.Setzen(ziel));
            }
        }
        finally
        {
            _helligkeitUnterwegs = false;
        }
    }

    private void GeraetelisteZeichnen()
    {
        Geraeteliste.Children.Clear();
        var liste = Ausgaenge.Lesen();
        var gewaehlt = liste.FirstOrDefault(a => a.Kennung == _einstellungen.Ausgang)
                       ?? liste.FirstOrDefault(a => a.IstStandard);

        if (liste.Count == 0)
        {
            Geraeteliste.Children.Add(new TextBlock
            {
                Text = "Windows meldet kein Audiogerät.",
                FontSize = 17,
                Margin = new Thickness(0, 0, 0, 12),
            });
            return;
        }

        foreach (var ausgang in liste)
        {
            var symbol = ausgang.Bluetooth ? "IconBluetooth"
                : ausgang.Name.Contains("HDMI", StringComparison.OrdinalIgnoreCase)
                  || ausgang.Name.Contains("Display", StringComparison.OrdinalIgnoreCase) ? "IconBildschirm"
                : "IconLautsprecher";
            var unten = ausgang.Bluetooth ? "Bluetooth · verbunden"
                : ausgang.IstStandard ? "Windows-Standard" : "";

            // Ein Raster, kein waagerechtes StackPanel: Das StackPanel gibt
            // seinen Kindern unbegrenzte Breite, und dann kürzt TextTrimming
            // nichts -- der Gerätename lief rechts aus dem Feld.
            var inhalt = new Grid();
            inhalt.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            inhalt.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inhalt.Children.Add(new Path
            {
                Style = (Style)FindResource("Symbol"),
                Data = (Geometry)FindResource(symbol),
                Margin = new Thickness(0, 0, 14, 0),
            });
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(text, 1);
            text.Children.Add(new TextBlock { Text = ausgang.Name, FontSize = 19, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
            if (unten.Length > 0)
            {
                text.Children.Add(new TextBlock { Text = unten, FontSize = 15, Foreground = (Brush)FindResource("Tinte2") });
            }
            inhalt.Children.Add(text);

            var zeile = new RadioButton
            {
                Style = (Style)FindResource("Geraetezeile"),
                GroupName = "Geraet",
                Content = inhalt,
                Tag = ausgang.Kennung,
                IsChecked = ausgang == gewaehlt,
            };
            zeile.Click += (_, _) =>
            {
                AusgangWechseln(ausgang.Kennung);
                TonSymbolZeigen();
            };
            Geraeteliste.Children.Add(zeile);
        }
    }

    private void BluetoothKoppeln(object absender, RoutedEventArgs e) => WindowsOeffnen("ms-settings:bluetooth");

    private bool _themaWirdGezeigt;

    private void ThemaGewaehlt(object absender, SelectionChangedEventArgs e)
    {
        if (_themaWirdGezeigt || ThemaWahl.SelectedItem is not Themenwahl wahl) return;
        _einstellungen.Thema = wahl.Schluessel;
        _ablage.EinstellungenSchreiben(_einstellungen);
        DarstellungAnwenden();
    }

    private void DarstellungGewaehlt(object absender, RoutedEventArgs e)
    {
        _einstellungen.Darstellung = (string)((RadioButton)absender).Tag;
        _ablage.EinstellungenSchreiben(_einstellungen);
        DarstellungAnwenden();
    }

    /// <summary>
    /// Tag oder Nacht -- fest gewählt oder nach der Uhr. Läuft bei jedem
    /// Uhrtakt mit, damit „Automatisch" um 21 Uhr auch wirklich umschaltet,
    /// ohne dass jemand das Programm neu startet.
    /// </summary>
    private void DarstellungAnwenden()
    {
        var stunde = DateTime.Now.Hour;
        var nacht = _einstellungen.Darstellung switch
        {
            "tag" => false,
            "nacht" => true,
            _ => stunde >= 21 || stunde < 7,
        };
        var app = (App)Application.Current;
        if (app.IstNacht == nacht && app.Thema == _einstellungen.Thema) return;
        app.Farbtafel(_einstellungen.Thema, nacht);
        // Neu zeichnen: Die Tasten tragen Pinsel, die im Code aus der Tafel
        // geholt wurden (Akzent der Laufanzeige, Ring der freien Tasten).
        TastenZeichnen();
        LaufendesZeigen();
    }

    private void VerhaltenGeaendert(object absender, RoutedEventArgs e)
    {
        _einstellungen.KeinRuhezustand = RuhezustandSchalter.IsChecked == true;
        _ablage.EinstellungenSchreiben(_einstellungen);
        Wachhalter.Setzen(_laeuft && _einstellungen.KeinRuhezustand);

        if (ReferenceEquals(absender, AutostartSchalter))
        {
            Autostart.Setzen(AutostartSchalter.IsChecked == true);
            // Was Windows wirklich eingetragen hat, nicht was wir wollten.
            AutostartSchalter.IsChecked = Autostart.Aktiv;
        }
    }

    // ============================================= Handy als Fernbedienung

    private async Task FernStarten()
    {
        // PIN und Schlüssel beim ersten Einschalten anlegen, danach nie
        // wieder von selbst: Sonst wären alle Handys nach jedem Neustart
        // abgemeldet.
        if (_einstellungen.FernPin.Length != 4) _einstellungen.FernPin = Zugang.NeuePin();
        if (_einstellungen.FernSchluessel.Length == 0) _einstellungen.FernSchluessel = Zugang.NeuerSchluessel();
        _ablage.EinstellungenSchreiben(_einstellungen);
        try
        {
            await _dienst.Starten();
            _fernFehler = "";
        }
        catch (Exception fehler)
        {
            // Meist ist der Port belegt -- etwa, weil REGOradio zweimal läuft.
            _fernFehler = fehler.Message;
        }
    }

    private async void FernUmschalten(object absender, RoutedEventArgs e)
    {
        _einstellungen.FernAn = FernSchalter.IsChecked == true;
        _ablage.EinstellungenSchreiben(_einstellungen);
        if (_einstellungen.FernAn)
        {
            await FernStarten();
            // Beim Einschalten gleich nach der Firewall sehen: Ist das
            // Programm gesperrt oder noch nicht freigegeben, die Freigabe
            // anbieten -- mit der Windows-Abfrage. Ein versehentliches
            // „Ablehnen" beim ersten Start lässt sich so jederzeit mit diesem
            // Schalter zurücknehmen.
            if (_dienst.Laeuft && Firewall.Pruefen() != Firewall.Zustand.Frei) Firewall.Einrichten(Dienst.Port);
        }
        else
        {
            await _dienst.Anhalten();
        }
        FernTafelZeigen();
    }

    private void FernFreigabeEinrichten(object absender, RoutedEventArgs e)
    {
        Firewall.Einrichten(Dienst.Port);
        FernTafelZeigen();
    }

    /// <summary>
    /// Eine neue PIN meldet alle Handys ab, die die alte kannten -- das
    /// Anmeldezeichen wird aus der PIN gerechnet. Der Weg, wenn jemand die PIN
    /// gesehen hat, der sie nicht haben soll.
    /// </summary>
    private void FernNeuePin(object absender, RoutedEventArgs e)
    {
        _einstellungen.FernPin = Zugang.NeuePin();
        _ablage.EinstellungenSchreiben(_einstellungen);
        FernTafelZeigen();
    }

    private void FernTafelZeigen()
    {
        if (!_einstellungen.FernAn)
        {
            FernTafel.Visibility = Visibility.Collapsed;
            FernHinweis.Text = "Ist der Schalter an, lässt sich das Radio vom Handy aus bedienen: "
                               + "Sender wählen, Lautstärke, Stumm, Stopp. Das Handy muss im selben WLAN sein.";
            return;
        }

        FernTafel.Visibility = Visibility.Visible;
        var adresse = Adresse.Finden();
        var url = adresse is null ? "" : $"http://{adresse}:{Dienst.Port}/";
        FernQr.Source = url.Length > 0 ? QrBild.Zeichnen(url) : null;
        FernAdresse.Text = url.Length > 0 ? url : "Keine Netzadresse – hängt das Notebook im WLAN?";
        FernPin.Text = _einstellungen.FernPin;
        var firewall = _dienst.Laeuft ? Firewall.Pruefen() : Firewall.Zustand.Frei;
        FernFreigabe.Visibility = firewall == Firewall.Zustand.Frei ? Visibility.Collapsed : Visibility.Visible;
        FernHinweis.Text = !_dienst.Laeuft
            ? $"Der Dienst ließ sich nicht starten. {_fernFehler}"
            : firewall == Firewall.Zustand.Gesperrt
                ? "Die Windows-Firewall sperrt REGOradio – das Handy kommt nicht durch. "
                  + "Mit dem Knopf unten wird das freigegeben (Windows fragt einmal nach)."
                : firewall == Firewall.Zustand.Ungeregelt
                    ? "Für REGOradio gibt es noch keine Freigabe in der Firewall. Mit dem Knopf unten "
                      + "wird sie eingerichtet, nur für die Fernbedienung und für alle Netze."
                    : "QR-Code mit der Handykamera öffnen, dann die PIN eingeben. Das Handy muss im "
                      + "selben WLAN sein.";
    }

    // Was das Handy sieht und tun darf. Die Aufrufe kommen aus dem Webdienst,
    // also aus fremden Fäden -- alles geht über den Oberflächenfaden, sonst
    // wirft WPF beim ersten Zugriff auf ein Steuerelement.

    Fernstand IFernsteuerbar.Stand()
    {
        // Die Helligkeit außerhalb des Oberflächenfadens lesen: WMI braucht
        // etwas, und das Handy fragt alle zwei Sekunden.
        var helligkeit = Helligkeit.Lesen();
        return Dispatcher.Invoke(() => FernstandBauen(helligkeit));
    }

    private Fernstand FernstandBauen(int? helligkeit)
    {
        var (interpret, titel) = Cover.Zerlege(_titelGezeigt);
        return new Fernstand(
            Laeuft: _laeuft,
            Platz: _laufender?.Platz ?? 0,
            Sender: _laufender?.Name ?? "",
            Titel: _titelGezeigt.Length == 0 ? "" : titel,
            Interpret: interpret,
            Cover: _coverAdresse,
            Logo: _laufender is null ? "" : _logos.Adresse(_laufender.Logo, _laufender.Homepage),
            Lautstaerke: _abspieler.Lautstaerke,
            Stumm: _abspieler.Stumm,
            Helligkeit: helligkeit,
            Ansicht: Uhrebene.Visibility == Visibility.Visible ? "uhr"
                : Jetztebene.Visibility == Visibility.Visible ? "cover"
                : "bedienung");
    }

    IReadOnlyList<Fernsender> IFernsteuerbar.Sender() => Dispatcher.Invoke(() =>
        (IReadOnlyList<Fernsender>)_sender
            .OrderBy(s => s.Platz)
            .Select(s => new Fernsender(s.Platz, s.Name, _logos.Adresse(s.Logo, s.Homepage), Herkunft(s, mitCodec: false)))
            .ToList());

    bool IFernsteuerbar.Spielen(int platz) => Dispatcher.Invoke(() =>
    {
        var sender = _sender.FirstOrDefault(s => s.Platz == platz);
        if (sender is null) return false;
        Spielen(sender);
        return true;
    });

    void IFernsteuerbar.Stoppen() => Dispatcher.Invoke(() => Stoppen(this, new RoutedEventArgs()));

    void IFernsteuerbar.Lautstaerke(int wert) => Dispatcher.Invoke(() => LautstaerkeSetzen(wert));

    void IFernsteuerbar.Stumm(bool stumm) => Dispatcher.Invoke(() => StummSetzen(stumm));

    void IFernsteuerbar.Helligkeit(int wert)
    {
        // Gleich hier setzen, im Faden des Webdiensts: Die Antwort an das
        // Handy liest die Helligkeit zurück und soll schon den neuen Wert
        // zeigen, sonst springt dort der Regler kurz zurück.
        Helligkeit.Setzen(wert);
        Dispatcher.Invoke(() => FernHelligkeitZeigen(wert));
    }

    bool IFernsteuerbar.Ansicht(string ansicht) => Dispatcher.Invoke(() =>
    {
        var jetztOffen = Jetztebene.Visibility == Visibility.Visible;
        var uhrOffen = Uhrebene.Visibility == Visibility.Visible;
        switch (ansicht)
        {
            case "bedienung":
                if (uhrOffen) WortuhrSchliessen(this, new RoutedEventArgs());
                if (jetztOffen) JetztSchliessen(this, new RoutedEventArgs());
                return true;
            case "cover":
                if (_laufender is null) return false;
                if (uhrOffen) WortuhrSchliessen(this, new RoutedEventArgs());
                if (!jetztOffen) JetztOeffnen(this, new RoutedEventArgs());
                return true;
            case "uhr":
                if (jetztOffen) JetztSchliessen(this, new RoutedEventArgs());
                if (!uhrOffen) WortuhrOeffnen(this, new RoutedEventArgs());
                return true;
            default:
                return false;
        }
    });

    private void FernHelligkeitZeigen(int wert)
    {
        // Ist das Einstellungsblatt offen, zieht sein Regler mit.
        if (Einstellungsebene.Visibility == Visibility.Visible && HelligkeitZeile.Visibility == Visibility.Visible)
        {
            _helligkeitWirdGezeigt = true;
            HelligkeitRegler.Value = Math.Clamp(wert, Helligkeit.Minimum, 100);
            _helligkeitWirdGezeigt = false;
        }
    }

    // =============================================================== Suche

    private void SucheOeffnen(object absender, RoutedEventArgs e) => SucheOeffnen(FreierPlatz());

    private void SucheOeffnen(int platz)
    {
        var fenster = new Suchfenster(_katalog, _logos, platz) { Owner = this };
        if (fenster.ShowDialog() != true || fenster.Gewaehlt is null) return;

        var treffer = fenster.Gewaehlt;
        if (fenster.AufTasteLegen)
        {
            var ziel = platz > 0 ? platz : FreierPlatz();
            _sender.RemoveAll(s => s.Platz == ziel);
            var neuer = treffer.AlsSender(ziel);
            _sender.Add(neuer);
            _ablage.SenderSchreiben(_sender);
            Spielen(neuer);
        }
        else
        {
            // Nur anhören, nichts behalten -- zwei verschiedene
            // Entscheidungen.
            Spielen(treffer.AlsSender(platz: 0));
        }
    }

    /// <summary>Die erste freie Taste, oder die letzte, wenn alle belegt sind.</summary>
    private int FreierPlatz()
    {
        for (var platz = 1; platz <= Plaetze; platz++)
        {
            if (_sender.All(s => s.Platz != platz)) return platz;
        }
        return Plaetze;
    }

    // ================================================= Fenster und Tray

    /// <summary>Ob <see cref="WirklichSchliessen"/> schon gelaufen ist.</summary>
    private bool _beendet;

    /// <summary>
    /// Zugeklappt heißt nicht unbedingt beendet. Seit Bau 11 wird gefragt –
    /// in den Tray oder ganz beenden –, sofern das nicht abgestellt ist; dann
    /// gilt die gespeicherte Wahl. Siehe <see cref="Schliessregel"/>.
    ///
    /// **Ist schon beendet, wird nicht mehr gefragt.** Das Tray-Menü „Beenden"
    /// und das Herunterfahren von Windows rufen erst <see cref="WirklichSchliessen"/>
    /// und dann <c>Shutdown</c>, und <c>Shutdown</c> schließt dieses Fenster
    /// noch einmal. Bis Bau 10 landete das hier in „im Tray bleiben" und wurde
    /// nur deshalb nicht zum Fehler, weil WPF ein Abbrechen beim Herunterfahren
    /// übergeht. Mit der Rückfrage hätte es gefragt, während das Programm schon
    /// geht.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (_beendet)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;

        switch (Schliessregel.Entscheiden(_einstellungen.SchliessenFragen, _einstellungen.ImTrayBleiben))
        {
            case Schliessart.Fragen:
                SchliessfrageZeigen();
                break;
            case Schliessart.Tray:
                Hide();
                break;
            default:
                Beenden();
                break;
        }
    }

    private void Beenden()
    {
        WirklichSchliessen();
        Application.Current.Shutdown();
    }

    private void SchliessfrageZeigen()
    {
        // Das Blatt liegt in der skalierten Fläche, das Vollbild darüber.
        // Erst heraus aus dem Vollbild, sonst fragt das Programm hinter einem
        // Bild, das niemand wegtippt.
        if (Uhrebene.Visibility == Visibility.Visible) WortuhrSchliessen(this, new RoutedEventArgs());
        if (Jetztebene.Visibility == Visibility.Visible) JetztSchliessen(this, new RoutedEventArgs());

        // Auch aus der Taskleiste lässt sich ein verkleinertes Fenster
        // schließen – dann muss es erst wieder zu sehen sein.
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();

        SchliessHinweis.Text = _laeuft
            ? "Im Tray spielt das Radio weiter – ein Doppelklick auf das Symbol unten rechts holt das Fenster zurück. Beenden hält das Radio an."
            : "Im Tray bleibt REGOradio griffbereit – ein Doppelklick auf das Symbol unten rechts holt das Fenster zurück.";
        SchliessenMerken.IsChecked = false;
        Schliessebene.Visibility = Visibility.Visible;
    }

    private void SchliessenInDenTray(object absender, RoutedEventArgs e) => SchliessenMit(Schliessart.Tray);

    private void SchliessenBeenden(object absender, RoutedEventArgs e) => SchliessenMit(Schliessart.Beenden);

    private void SchliessenAbbrechen(object absender, RoutedEventArgs e) =>
        Schliessebene.Visibility = Visibility.Collapsed;

    private void SchliessenMit(Schliessart wahl)
    {
        Schliessebene.Visibility = Visibility.Collapsed;

        // „Nicht mehr fragen": Die Antwort wird zur Einstellung, und die
        // Auswahl in den Einstellungen zeigt sie – dort lässt sich das Fragen
        // auch wieder einschalten.
        if (SchliessenMerken.IsChecked == true)
        {
            _einstellungen.SchliessenFragen = false;
            _einstellungen.ImTrayBleiben = wahl == Schliessart.Tray;
            _ablage.EinstellungenSchreiben(_einstellungen);
            SchliessartZeigen();
        }

        if (wahl == Schliessart.Tray) Hide();
        else Beenden();
    }

    /// <summary>Die Auswahl in den Einstellungen nach dem gespeicherten Stand setzen.</summary>
    private void SchliessartZeigen()
    {
        var art = Schliessregel.Entscheiden(_einstellungen.SchliessenFragen, _einstellungen.ImTrayBleiben);
        SchliessenFragenWahl.IsChecked = art == Schliessart.Fragen;
        SchliessenTrayWahl.IsChecked = art == Schliessart.Tray;
        SchliessenBeendenWahl.IsChecked = art == Schliessart.Beenden;
    }

    private void SchliessartGewaehlt(object absender, RoutedEventArgs e)
    {
        if (absender is not FrameworkElement { Tag: string tag }
            || !Enum.TryParse<Schliessart>(tag, out var art))
        {
            return;
        }

        _einstellungen.SchliessenFragen = art == Schliessart.Fragen;

        // Bei "Fragen" bleibt die letzte Wahl stehen: Sie ist die Antwort für
        // den Tag, an dem jemand das Fragen wieder abstellt.
        if (art != Schliessart.Fragen) _einstellungen.ImTrayBleiben = art == Schliessart.Tray;

        _ablage.EinstellungenSchreiben(_einstellungen);
    }

    public void WirklichSchliessen()
    {
        // Nur einmal: Tray-Menü, Herunterfahren und Rückfrage führen alle
        // hierher, und der Abspieler verträgt kein zweites Dispose.
        if (_beendet) return;
        _beendet = true;

        // Den Webdienst anhalten, bevor das Programm geht -- nicht auf dem
        // Oberflächenfaden warten, Kestrel braucht ihn dafür nicht.
        Task.Run(() => _dienst.Anhalten()).Wait(TimeSpan.FromSeconds(2));
        // Eine Lautstärke, die noch auf ihre Speicherung wartet, nicht
        // verlieren.
        if (_speicheruhr.IsEnabled)
        {
            _speicheruhr.Stop();
            _ablage.EinstellungenSchreiben(_einstellungen);
        }
        _netzuhr.Stop();
        _uhr.Stop();
        Wachhalter.Setzen(false);
        _abspieler.Dispose();
        // Abmelden, sonst zeigt das Lautstärkefenster noch eine Weile einen
        // Sender, der längst aus ist.
        _medien?.Dispose();
    }
}

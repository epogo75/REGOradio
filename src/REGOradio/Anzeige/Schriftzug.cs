using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace REGOradio.Anzeige;

/// <summary>
/// Der Schriftzug „REGOradio" – <b>„REGO" dünn und gesperrt, „radio" fett im
/// Akzent mit Leuchtschein</b> (Bau 13).
///
/// Gewählt aus drei Vorschlägen: „1 bitte" (Röhre), „der Themenakzent ist
/// super". Das Muster ist der Schriftzug von REGOdj (`Ui/Schriftzug.cs`) –
/// mit einem Unterschied: Dort ist das „dj" fest blau, hier nimmt „radio" den
/// Akzent des gewählten Farbthemas, damit es zu allen sechs passt.
///
/// WPF kennt keine Sperrung; die vier Buchstaben von „REGO" stehen deshalb
/// einzeln, sechs Hundertstel der Schriftgröße auseinander. Der Schein sind
/// zwei Lagen: ein weichgezeichnetes „radio" dahinter für den weiten Hof, ein
/// Schatten ohne Versatz für den engen.
///
/// <b>Farben über Ressourcen, nicht kopiert.</b> Schrift und Akzent hängen per
/// `SetResourceReference` an „Tinte" und „Akzent"; der Schatten bindet seine
/// Farbe an die Schrift des „radio". So folgt alles einem Themenwechsel, ohne
/// dass jemand den Schriftzug neu bauen muss.
/// </summary>
public sealed class Schriftzug : Border
{
    public static readonly DependencyProperty GroesseProperty = DependencyProperty.Register(
        nameof(Groesse), typeof(double), typeof(Schriftzug),
        new FrameworkPropertyMetadata(32.0, (d, _) => ((Schriftzug)d).Bauen()));

    /// <summary>Ob der Schein atmet – aus für das Installerbild und Standbilder.</summary>
    public static readonly DependencyProperty PulsiertProperty = DependencyProperty.Register(
        nameof(Pulsiert), typeof(bool), typeof(Schriftzug),
        new FrameworkPropertyMetadata(true, (d, _) => ((Schriftzug)d).Bauen()));

    // Alle Schriftzüge, damit ein Themenwechsel sie neu bauen kann (Bau 26).
    // Schwach gehalten: ein geschlossenes Blatt soll nicht hier weiterleben.
    private static readonly List<WeakReference<Schriftzug>> Lebende = new();

    public Schriftzug()
    {
        Background = Brushes.Transparent;
        SnapsToDevicePixels = true;
        // Für die Bedienungshilfe ein Wort, nicht neun Buchstaben.
        System.Windows.Automation.AutomationProperties.SetName(this, "REGOradio");
        Lebende.Add(new WeakReference<Schriftzug>(this));
        Bauen();
    }

    /// <summary>Nach einem Themenwechsel: Sperrung und Stärke hängen an der Tafel.</summary>
    public static void Alle()
    {
        Lebende.RemoveAll(w => !w.TryGetTarget(out _));
        foreach (var w in Lebende)
            if (w.TryGetTarget(out var s)) s.Bauen();
    }

    private static T Tafel<T>(string schluessel, T vorgabe) =>
        Application.Current?.TryFindResource(schluessel) is T wert ? wert : vorgabe;

    /// <summary>Die Schriftgröße in Punkten.</summary>
    public double Groesse
    {
        get => (double)GetValue(GroesseProperty);
        set => SetValue(GroesseProperty, value);
    }

    public bool Pulsiert
    {
        get => (bool)GetValue(PulsiertProperty);
        set => SetValue(PulsiertProperty, value);
    }

    private void Bauen()
    {
        var g = Groesse;
        var reihe = new StackPanel { Orientation = Orientation.Horizontal };

        // SCHRIFT, STÄRKE UND FARBE AUS DER TAFEL (Bau 26). Die alten Themen
        // haben „REGO" dünn und gesperrt, „radio" fett im Akzent; das Thema
        // REGO setzt beides kräftig in Outfit, „radio" violett wie auf
        // regotools.de. Kräftige Buchstaben stehen eng - gesperrt sähen sie
        // auseinandergefallen aus.
        var staerke = Tafel("SchriftzugREGO", FontWeights.Light);
        var sperrung = staerke.ToOpenTypeWeight() >= FontWeights.Bold.ToOpenTypeWeight() ? g * 0.005 : g * 0.06;

        foreach (var zeichen in "REGO")
        {
            var buchstabe = new TextBlock
            {
                Text = zeichen.ToString(),
                FontSize = g,
                Margin = new Thickness(0, 0, sperrung, 0),
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            buchstabe.SetResourceReference(TextBlock.FontFamilyProperty, "Schrift");
            buchstabe.SetResourceReference(TextBlock.FontWeightProperty, "SchriftzugREGO");
            buchstabe.SetResourceReference(TextBlock.ForegroundProperty, "Tinte");
            reihe.Children.Add(buchstabe);
        }

        TextBlock Radio()
        {
            var t = new TextBlock
            {
                Text = "radio",
                FontSize = g,
            };
            t.SetResourceReference(TextBlock.FontFamilyProperty, "Schrift");
            t.SetResourceReference(TextBlock.FontWeightProperty, "SchriftzugRadio");
            t.SetResourceReference(TextBlock.ForegroundProperty, "Schriftzugfarbe");
            return t;
        }

        var leuchten = new Grid { VerticalAlignment = VerticalAlignment.Bottom };

        // Der weite Hof – bei kleinen Größen schwächer, sonst verschwimmt das Wort.
        var hof = Radio();
        hof.Effect = new BlurEffect { Radius = Math.Max(2, g * 0.28) };
        hof.Opacity = g >= 40 ? 0.85 : 0.55;
        leuchten.Children.Add(hof);

        var kern = Radio();
        var schein = new DropShadowEffect
        {
            ShadowDepth = 0,
            BlurRadius = Math.Max(3, g * 0.14),
            Opacity = 0.9,
        };
        // Ein Effekt ist kein Element und kennt keine Ressourcen; er nimmt die
        // Farbe deshalb vom „radio" selbst, das am Akzent hängt.
        BindingOperations.SetBinding(schein, DropShadowEffect.ColorProperty,
            new Binding("Foreground.Color") { Source = kern });
        kern.Effect = schein;
        leuchten.Children.Add(kern);

        reihe.Children.Add(leuchten);
        Child = reihe;

        if (!Pulsiert) return;

        // Ein ruhiger Puls wie das „dj" in REGOdj: 1,6 s hin, 1,6 s zurück.
        // Der Hof atmet, das Wort selbst bleibt ruhig lesbar.
        var hell = hof.Opacity;
        hof.BeginAnimation(OpacityProperty, new DoubleAnimation(hell * 0.35, hell, TimeSpan.FromSeconds(1.6))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        });
        schein.BeginAnimation(DropShadowEffect.OpacityProperty, new DoubleAnimation(0.45, 0.95, TimeSpan.FromSeconds(1.6))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        });
    }
}

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

using REGOradio.Modelle;

namespace REGOradio;

/// <summary>
/// Im Feld „Läuft" wischen: nach links der nächste Sender, nach rechts der
/// vorige (Bau 23) – dasselbe wie „Weiter" und „Zurück" auf der
/// Medientaste. Was als Wischen zählt, steht in `Modelle/Wischen.cs`.
///
/// **Nur oben.** Gewischt wird auf Sender und Lied, nicht auf Lautstärke
/// und Stopp darunter: Wer am Lautstärkebalken zieht, will keinen anderen
/// Sender.
///
/// **Der Tipp bleibt ein Tipp.** Das Lied darunter ist ein Knopf (Vollbild).
/// War es ein Wischen, wird sein Klick verschluckt – sonst ginge nach jedem
/// Senderwechsel das Vollbild auf.
/// </summary>
public partial class Hauptfenster
{
    private Point? _wischStart;

    private bool ImWischbereich(object quelle)
    {
        for (var teil = quelle as DependencyObject; teil is not null; teil = Elternteil(teil))
        {
            if (teil == SenderKopf || teil == TitelKnopf) return true;
            if (teil == Laeuftfeld) return false;
        }
        return false;
    }

    // Textstücke (Run) hängen nicht im sichtbaren Baum; der logische hilft weiter.
    private static DependencyObject? Elternteil(DependencyObject teil) =>
        teil is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(teil)
            : LogicalTreeHelper.GetParent(teil);

    private void WischenBeginnt(object absender, MouseButtonEventArgs e)
    {
        _wischStart = ImWischbereich(e.OriginalSource) ? e.GetPosition(Laeuftfeld) : null;
    }

    private void WischenZieht(object absender, MouseEventArgs e)
    {
        if (_wischStart is not { } start || e.LeftButton != MouseButtonState.Pressed) return;
        var weg = e.GetPosition(Laeuftfeld) - start;
        // Das Feld folgt dem Finger, gebremst – man sieht, dass etwas
        // passiert, ohne dass Sender und Lied aus dem Feld rutschen.
        WischVersatz.BeginAnimation(TranslateTransform.XProperty, null);
        WischVersatz.X = Wischen.Waagerecht(weg.X, weg.Y) ? weg.X * 0.35 : 0;
    }

    private void WischenEndet(object absender, MouseButtonEventArgs e)
    {
        if (_wischStart is not { } start) return;
        _wischStart = null;
        var weg = e.GetPosition(Laeuftfeld) - start;

        // Zurückgleiten, ob gewechselt wird oder nicht.
        WischVersatz.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(0, TimeSpan.FromMilliseconds(180)) { EasingFunction = new QuadraticEase() });

        var richtung = Wischen.Deuten(weg.X, weg.Y);
        if (richtung == 0) return;

        // Den Klick des Liedknopfs verschlucken. Der Knopf hält die Maus noch
        // fest; freigeben, sonst bleibt er gedrückt stehen.
        e.Handled = true;
        TitelKnopf.ReleaseMouseCapture();
        Medienbefehl(richtung > 0 ? Ton.Medienbefehl.Weiter : Ton.Medienbefehl.Zurueck);
    }
}

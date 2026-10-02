using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace REGOradio.Anzeige;

/// <summary>
/// Der Rahmen ums Programm im Thema – **auch die Titelleiste**.
///
/// Gewünscht: „bitte das Design wie in REGOdj (Rahmen ums Programm im eigenen
/// Design)". Das Fenster selbst trägt längst das Thema; die Titelleiste aber
/// malte Windows in seinem eigenen Grau, bei Neon Pink um Mitternacht ein
/// heller Balken über dem violettschwarzen Radio.
///
/// Windows 11 lässt Leiste, Schrift und Rand über DWM färben. Das geschieht
/// hier einmal für alle Fenster (Klassen-Handler auf <c>Loaded</c>) und nach
/// jedem Themenwechsel neu (<see cref="Alle"/>). Dasselbe Mittel wie in
/// REGOdj (`Ui/Fensterkleid.cs`), nur mit den Schlüsseln dieser Farbtafeln:
/// `Grund`, `Tinte`, `Linie`.
///
/// Auf Windows 10 greifen die Farbattribute nicht; dort bleibt es beim hellen
/// oder dunklen Modus der Leiste. Mehr bietet Windows 10 nicht an.
/// </summary>
internal static class Fensterkleid
{
    private const int DunklerModus = 20;
    private const int Randfarbe = 34;
    private const int Leistenfarbe = 35;
    private const int Leistenschrift = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr fenster, int attribut, ref int wert, int groesse);

    public static void Einschalten()
    {
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((absender, _) =>
            {
                if (absender is Window fenster) Anziehen(fenster);
            }));
    }

    /// <summary>Alle offenen Fenster neu einkleiden – nach einem Themenwechsel.</summary>
    public static void Alle()
    {
        if (Application.Current is not { } anwendung) return;

        foreach (Window fenster in anwendung.Windows) Anziehen(fenster);
    }

    public static void Anziehen(Window fenster)
    {
        var griff = new WindowInteropHelper(fenster).Handle;

        // Noch kein Fenstergriff: Es kommt beim Laden wieder vorbei.
        if (griff == IntPtr.Zero) return;

        if (Farbe(fenster, "Grund") is not { } grund) return;

        try
        {
            // Dunkle Leiste, wenn der Grund dunkel ist (Nacht), sonst hell
            // (Tag). Das zählt vor allem auf Windows 10, wo nur dieser
            // Schalter greift.
            var hell = (0.299 * grund.R + 0.587 * grund.G + 0.114 * grund.B) / 255 > 0.5;
            var dunkel = hell ? 0 : 1;
            DwmSetWindowAttribute(griff, DunklerModus, ref dunkel, sizeof(int));

            var leiste = Farbref(grund);
            DwmSetWindowAttribute(griff, Leistenfarbe, ref leiste, sizeof(int));

            if (Farbe(fenster, "Tinte") is { } tinte)
            {
                var text = Farbref(tinte);
                DwmSetWindowAttribute(griff, Leistenschrift, ref text, sizeof(int));
            }

            if (Farbe(fenster, "Linie") is { } linie)
            {
                var rand = Farbref(linie);
                DwmSetWindowAttribute(griff, Randfarbe, ref rand, sizeof(int));
            }
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    private static Color? Farbe(FrameworkElement wo, string schluessel) =>
        wo.TryFindResource(schluessel) switch
        {
            SolidColorBrush pinsel => pinsel.Color,
            Color farbe => farbe,
            _ => null,
        };

    /// <summary>COLORREF: 0x00BBGGRR.</summary>
    private static int Farbref(Color farbe) => farbe.R | (farbe.G << 8) | (farbe.B << 16);
}

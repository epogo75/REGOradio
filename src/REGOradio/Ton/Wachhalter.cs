using System.Runtime.InteropServices;

namespace REGOradio.Ton;

/// <summary>
/// Hält den Rechner wach, solange Radio läuft.
///
/// **Warum es das braucht.** Windows zählt Tastatur und Maus, nicht Töne: Ein
/// Notebook, an dem eine halbe Stunde niemand tippt, geht in den Ruhezustand --
/// mitten im Lied. Wer Radio hört, bedient nichts, und genau deshalb passiert es.
///
/// **Nur der Rechner, nicht der Bildschirm.** `ES_SYSTEM_REQUIRED` verhindert
/// das Einschlafen, `ES_DISPLAY_REQUIRED` würde den Schirm dauerhaft anlassen --
/// das wäre falsch: Ein dunkler Schirm ist beim Radiohören erwünscht, gerade
/// abends im Hotelzimmer.
///
/// **Der Zustand hängt am Faden, der ihn setzt.** Das verlangt Windows so.
/// Deshalb wird diese Klasse ausschließlich aus dem Oberflächenfaden bedient;
/// ein Aufruf aus dem Stromfaden würde den Zustand dort setzen und beim Stoppen
/// nicht zurücknehmen.
/// </summary>
public static class Wachhalter
{
    [Flags]
    private enum Zustand : uint
    {
        SystemRequired = 0x00000001,
        Continuous = 0x80000000,
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetThreadExecutionState(Zustand zustand);

    private static bool _an;

    /// <summary>Ob der Ruhezustand gerade verhindert wird.</summary>
    public static bool Aktiv => _an;

    public static void Setzen(bool wachbleiben)
    {
        if (wachbleiben == _an) return;
        // ES_CONTINUOUS allein setzt den Zustand zurück: „ab jetzt gilt wieder
        // das Übliche". Zusammen mit ES_SYSTEM_REQUIRED heißt es: „ab jetzt und
        // bis auf Widerruf nicht einschlafen".
        var ergebnis = SetThreadExecutionState(
            wachbleiben ? Zustand.Continuous | Zustand.SystemRequired : Zustand.Continuous);

        // Null heißt: hat nicht geklappt. Kein Grund, den Ton abzubrechen --
        // dann schläft der Rechner eben irgendwann ein, wie bisher.
        _an = ergebnis != 0 && wachbleiben;
    }
}

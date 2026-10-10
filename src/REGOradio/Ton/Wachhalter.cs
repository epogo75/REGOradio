using System.Runtime.InteropServices;

namespace REGOradio.Ton;

/// <summary>
/// Hält den Rechner wach, solange REGOradio läuft (Einstellung „Kein Ruhezustand“).
///
/// **Warum es das braucht.** Windows zählt Tastatur und Maus, nicht Töne: Ein
/// Notebook, an dem eine halbe Stunde niemand tippt, geht in den Ruhezustand --
/// mitten im Lied. Wer Radio hört, bedient nichts, und genau deshalb passiert es.
///
/// **Bau 30: Energieanforderung statt nur `SetThreadExecutionState`.** Bis Bau 29
/// stand hier allein `ES_SYSTEM_REQUIRED | ES_CONTINUOUS`. Stephan am Dell:
/// „REGOradio hält das Notebook nix aktiv“ - Radio lief, der Bildschirm ging aus,
/// kurz danach war Schluss. Der Dell hat Modern Standby (S0): Dort setzt das
/// Flag nur den Leerlaufzähler zurück, und mit dunklem Schirm geht Windows
/// trotzdem in den Standby und hält Desktop-Programme an - der Strom reißt ab.
/// Was dort hält, ist eine benannte Energieanforderung (`PowerSetRequest`) mit
/// „System“ (nicht einschlafen) und „Ausführung“ (Programm nicht anhalten). Sie
/// hängt nicht am Faden und steht mit unserem Grund in `powercfg /requests` -
/// so lässt sich am Gerät nachsehen, ob sie greift. Das alte Flag bleibt
/// zusätzlich gesetzt; es schadet nicht und hält auf Rechnern mit klassischem
/// Ruhezustand ohnehin.
///
/// **Grenze, die wir nicht verschieben können:** Im Akkubetrieb nimmt Windows
/// auf Modern-Standby-Geräten solche Anforderungen fünf Minuten nach der
/// eingestellten Ruhezeit selbst zurück. Mit Netzteil hält es.
///
/// **Nur der Rechner, nicht der Bildschirm.** Kein „Display“ - ein dunkler
/// Schirm ist beim Radiohören erwünscht, gerade abends im Hotelzimmer.
///
/// **`SetThreadExecutionState` hängt am Faden, der es setzt.** Deshalb wird
/// diese Klasse weiter nur aus dem Oberflächenfaden bedient.
/// </summary>
public static class Wachhalter
{
    private const string Grund = "REGOradio: Radio soll weiterlaufen (Einstellung „Kein Ruhezustand“)";

    [Flags]
    private enum Zustand : uint
    {
        SystemRequired = 0x00000001,
        Continuous = 0x80000000,
    }

    private enum Anforderung
    {
        SystemRequired = 1,
        ExecutionRequired = 3,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Begruendung
    {
        public uint Version;          // POWER_REQUEST_CONTEXT_VERSION = 0
        public uint Flags;            // POWER_REQUEST_CONTEXT_SIMPLE_STRING = 1
        public string Text;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetThreadExecutionState(Zustand zustand);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr PowerCreateRequest(ref Begruendung begruendung);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PowerSetRequest(IntPtr anfrage, Anforderung art);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PowerClearRequest(IntPtr anfrage, Anforderung art);

    private static bool _an;
    private static IntPtr _anfrage;

    /// <summary>Ob der Ruhezustand gerade verhindert wird.</summary>
    public static bool Aktiv => _an;

    public static void Setzen(bool wachbleiben)
    {
        if (wachbleiben == _an) return;

        // Das alte Flag: ES_CONTINUOUS allein setzt zurück, mit ES_SYSTEM_REQUIRED
        // heißt es „ab jetzt und bis auf Widerruf nicht einschlafen“.
        var alt = SetThreadExecutionState(wachbleiben ? Zustand.Continuous | Zustand.SystemRequired : Zustand.Continuous) != 0;

        var neu = false;
        if (wachbleiben)
        {
            if (_anfrage == IntPtr.Zero)
            {
                var b = new Begruendung { Version = 0, Flags = 1, Text = Grund };
                var h = PowerCreateRequest(ref b);
                // INVALID_HANDLE_VALUE ist -1, nicht 0.
                if (h != IntPtr.Zero && h != new IntPtr(-1)) _anfrage = h;
            }
            if (_anfrage != IntPtr.Zero)
            {
                neu = PowerSetRequest(_anfrage, Anforderung.SystemRequired);
                // „Ausführung“ kennt Windows erst ab 8 - schlägt es fehl, hält „System“ allein.
                PowerSetRequest(_anfrage, Anforderung.ExecutionRequired);
            }
        }
        else if (_anfrage != IntPtr.Zero)
        {
            PowerClearRequest(_anfrage, Anforderung.SystemRequired);
            PowerClearRequest(_anfrage, Anforderung.ExecutionRequired);
        }

        // Klappt keins, ist das kein Grund, den Ton abzubrechen - dann schläft
        // der Rechner eben irgendwann ein, wie früher.
        _an = wachbleiben && (alt || neu);
    }
}

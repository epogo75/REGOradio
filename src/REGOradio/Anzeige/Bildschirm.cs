using System.Runtime.InteropServices;

namespace REGOradio.Anzeige;

/// <summary>
/// Den Bildschirm ausschalten (Bau 24) – wenn der Einschlaf-Timer das Radio
/// ausgemacht hat.
///
/// **Warum.** Bis dahin blieb das helle Fenster neben dem Bett an, bis
/// Windows es nach seiner eigenen Frist abdunkelte, je nach Energieoptionen
/// eine Viertelstunde oder nie. Wer einschläft, will es dunkel.
///
/// **Wie.** Dieselbe Nachricht, die Windows selbst schickt, wenn die Frist
/// abläuft (WM_SYSCOMMAND / SC_MONITORPOWER, 2 = aus). Eine Maus- oder
/// Tastenbewegung oder ein Tipp auf den Touchscreen schaltet ihn wieder
/// ein; nichts wird gesperrt, nichts beendet. Gepostet, nicht gesendet:
/// Ein Fenster, das gerade hängt, soll REGOradio nicht mit aufhalten.
/// </summary>
public static class Bildschirm
{
    private const int WmSyscommand = 0x0112;
    private const int ScMonitorpower = 0xF170;
    private const int Aus = 2;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr fenster, int nachricht, IntPtr wparam, IntPtr lparam);

    /// <returns>Ob Windows die Nachricht angenommen hat.</returns>
    public static bool Ausschalten(IntPtr fenster) =>
        fenster != IntPtr.Zero && PostMessage(fenster, WmSyscommand, ScMonitorpower, Aus);
}

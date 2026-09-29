using System.Management;
using System.Runtime.InteropServices;

namespace REGOradio.Anzeige;

/// <summary>
/// Die Helligkeit des eingebauten Bildschirms, über WMI (`root\wmi`,
/// `WmiMonitorBrightness`). Dieselbe Stelle, an der der Schieber im
/// Windows-Info-Center dreht.
///
/// **Das Betriebssystem ist die Wahrheit, nicht die Einstellungsdatei.** Anders
/// als die Lautstärke wird die Helligkeit jedes Mal gelesen: Windows ändert sie
/// selbst (Akku, Umgebungslicht, Funktionstasten), und ein gespeicherter Wert
/// wäre nach dem ersten Tastendruck auf Fn+F5 falsch.
///
/// **Nur eingebaute Bildschirme.** Ein angeschlossener Monitor kennt diese
/// Schnittstelle nicht (das ginge nur über DDC/CI, und das kann längst nicht
/// jeder). Dann liefert `Lesen` null, und der Schieber wird nicht gezeigt.
///
/// WMI braucht gern 50–100 ms. Aufrufer rufen das nicht im Takt jeder
/// Mausbewegung auf, sondern gebündelt.
/// </summary>
public static class Helligkeit
{
    /// <summary>
    /// Nie ganz dunkel: Ein schwarzer Bildschirm am Touch-Notebook ist ohne
    /// Tastatur kaum zurückzuholen, und vom Handy aus schon gar nicht zu sehen.
    /// </summary>
    public const int Minimum = 5;

    /// <summary>Die aktuelle Helligkeit 0–100, oder null, wenn der Bildschirm sie nicht verrät.</summary>
    public static int? Lesen()
    {
        try
        {
            using var suche = new ManagementObjectSearcher(@"root\wmi",
                "SELECT CurrentBrightness FROM WmiMonitorBrightness WHERE Active = TRUE");
            foreach (var eintrag in suche.Get())
            {
                using (eintrag) return Convert.ToInt32(eintrag["CurrentBrightness"]);
            }
        }
        catch (Exception fehler) when (fehler is ManagementException or COMException or UnauthorizedAccessException)
        {
        }
        return null;
    }

    /// <summary>Helligkeit setzen, begrenzt auf <see cref="Minimum"/>–100. Gibt zurück, ob es ging.</summary>
    public static bool Setzen(int wert)
    {
        wert = Math.Clamp(wert, Minimum, 100);
        try
        {
            using var suche = new ManagementObjectSearcher(@"root\wmi",
                "SELECT * FROM WmiMonitorBrightnessMethods WHERE Active = TRUE");
            var gesetzt = false;
            foreach (ManagementObject eintrag in suche.Get())
            {
                using (eintrag)
                {
                    // Zeitspanne 0: sofort und dauerhaft, nicht nur vorübergehend.
                    eintrag.InvokeMethod("WmiSetBrightness", [(uint)0, (byte)wert]);
                    gesetzt = true;
                }
            }
            return gesetzt;
        }
        catch (Exception fehler) when (fehler is ManagementException or COMException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

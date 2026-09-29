using Microsoft.Win32;

namespace REGOradio.Speicher;

/// <summary>
/// Mit Windows starten -- über den Run-Schlüssel des angemeldeten Benutzers.
///
/// **Die Registry ist die Wahrheit, nicht die Einstellungsdatei.** Windows
/// zeigt diesen Eintrag im Task-Manager unter „Autostart" und lässt ihn dort
/// abschalten. Merkte sich das Programm den Schalter selbst, stünde er im
/// Fenster auf „an", während Windows ihn längst ausgeschaltet hat. Deshalb wird
/// bei jedem Öffnen der Einstellungen nachgesehen, nicht erinnert.
///
/// **HKCU, nicht HKLM.** Für den eigenen Benutzer braucht es keine
/// Administratorrechte -- und ein Radio, das für seinen Autostart nach dem
/// Admin-Kennwort fragt, würde niemand einschalten.
/// </summary>
public static class Autostart
{
    private const string Schluessel = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "REGOradio";

    public static bool Aktiv
    {
        get
        {
            using var run = Registry.CurrentUser.OpenSubKey(Schluessel);
            return run?.GetValue(Name) is string;
        }
    }

    public static void Setzen(bool an)
    {
        using var run = Registry.CurrentUser.CreateSubKey(Schluessel);
        if (an)
        {
            // In Anführungszeichen: Der Pfad kann Leerzeichen enthalten
            // („C:\Program Files\…"), und ohne sie startet Windows das
            // Falsche -- oder gar nichts.
            var pfad = Environment.ProcessPath ?? "";
            if (pfad.Length > 0) run.SetValue(Name, $"\"{pfad}\" --tray");
        }
        else
        {
            run.DeleteValue(Name, throwOnMissingValue: false);
        }
    }
}

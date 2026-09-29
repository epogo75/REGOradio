using System.ComponentModel;
using System.Diagnostics;

namespace REGOradio.Fernbedienung;

/// <summary>
/// Darf das Handy das Notebook erreichen? Die Windows-Firewall entscheidet.
///
/// **Die Falle:** Beim ersten Lauschen fragt Windows einmal, ob das Programm
/// ins Netz darf. Wer dort versehentlich „Abbrechen" oder „Ablehnen" drückt,
/// bekommt eine Sperrregel -- und Windows fragt nie wieder. Die Fernbedienung
/// ist dann still tot: Der Dienst läuft, das Handy lädt endlos. Am Notebook
/// passiert, genau so.
///
/// Deshalb sieht REGOradio beim Einschalten selbst nach, und wenn eine Sperre
/// besteht oder keine Freigabe da ist, richtet es sie ein -- mit einer
/// Windows-Abfrage, denn Firewall-Regeln brauchen Administratorrechte.
/// </summary>
public static class Firewall
{
    public enum Zustand { Frei, Gesperrt, Ungeregelt }

    /// <summary>
    /// Die Regeln für diese .exe lesen, über die Firewall-Schnittstelle von
    /// Windows (HNetCfg.FwPolicy2). Lesen braucht keine Rechte.
    ///
    /// Eine Sperre gewinnt, auch wenn daneben eine Freigabe steht -- so wertet
    /// Windows selbst aus.
    /// </summary>
    public static Zustand Pruefen()
    {
        var programm = Environment.ProcessPath ?? "";
        try
        {
            var art = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
            if (art is null) return Zustand.Ungeregelt;
            dynamic regeln = ((dynamic)Activator.CreateInstance(art)!).Rules;
            var frei = false;
            foreach (dynamic regel in regeln)
            {
                string? anwendung = regel.ApplicationName;
                if (anwendung is null || !string.Equals(anwendung, programm, StringComparison.OrdinalIgnoreCase)) continue;
                if (!(bool)regel.Enabled || (int)regel.Direction != 1) continue;   // 1 = eingehend
                if ((int)regel.Action == 0) return Zustand.Gesperrt;             // 0 = sperren
                frei = true;
            }
            return frei ? Zustand.Frei : Zustand.Ungeregelt;
        }
        catch (Exception)
        {
            // Schnittstelle nicht lesbar (etwa durch eine Firmenrichtlinie):
            // lieber „ungeregelt" melden als eine Freigabe behaupten.
            return Zustand.Ungeregelt;
        }
    }

    /// <summary>
    /// Sperren für diese .exe löschen und eine Freigabe NUR für den Port der
    /// Fernbedienung anlegen, in allen Netzarten -- im Hotel-WLAN ist das Netz
    /// „öffentlich". Windows fragt dafür nach (Benutzerkontensteuerung).
    /// </summary>
    /// <returns>Ob es geklappt hat; false auch, wenn die Abfrage abgelehnt wurde.</returns>
    public static bool Einrichten(int port)
    {
        var programm = Environment.ProcessPath ?? "";
        if (programm.Length == 0) return false;
        var befehl =
            $"netsh advfirewall firewall delete rule name=all program=\"{programm}\" & " +
            $"netsh advfirewall firewall add rule name=\"REGOradio Fernbedienung\" dir=in action=allow " +
            $"program=\"{programm}\" protocol=TCP localport={port} profile=any";
        try
        {
            using var prozess = Process.Start(new ProcessStartInfo("cmd.exe", $"/c {befehl}")
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            });
            prozess?.WaitForExit(30_000);
            return Pruefen() == Zustand.Frei;
        }
        catch (Win32Exception)
        {
            return false;   // Abfrage mit „Nein" beantwortet
        }
    }
}

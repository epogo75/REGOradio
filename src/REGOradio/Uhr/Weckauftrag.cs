using System.Diagnostics;
using System.IO;
using System.Security;
using System.Text;

namespace REGOradio.Uhr;

/// <summary>
/// Der Auftrag an die Windows-Aufgabenplanung, den Rechner kurz vor dem
/// Wecker aufzuwecken (Bau 20).
///
/// **Warum.** Ein Programm kann nicht klingeln, solange der Rechner schläft.
/// Eine geplante Aufgabe mit „Zum Ausführen reaktivieren" (WakeToRun) holt
/// ihn eine Minute vorher aus dem Energiesparen. Sie startet
/// `REGOradio.exe --tray`: Läuft REGOradio schon, endet dieser zweite Start
/// sofort und still (siehe Einzelstart) – geweckt hat dann das laufende
/// Programm. Läuft es nicht, startet es im Tray und weckt selbst.
///
/// **Ohne Administrator.** Eine Aufgabe für den eigenen Benutzer darf jeder
/// anlegen. Ob Windows den Rechner dafür wirklich weckt, hängt an den
/// Energieoptionen („Zeitgeber zur Aktivierung zulassen"); im Akkubetrieb
/// ist das oft aus. Das sagt das Blatt dazu.
///
/// Die Aufgabe als XML: Nur so lassen sich WakeToRun und „auch im
/// Akkubetrieb" setzen, `schtasks /Create` mit Schaltern kann das nicht.
/// `Auftrag` ist reine Rechnung, geprüft in `WeckerPruefung`.
/// </summary>
public static class Weckauftrag
{
    public const string Name = "REGOradio Wecker";

    /// <summary>Wie lange vor der Weckzeit der Rechner geweckt wird.</summary>
    public static readonly TimeSpan Vorlauf = TimeSpan.FromMinutes(1);

    public static string Auftrag(string programm, DateTime erster, string tage)
    {
        var start = (erster - Vorlauf).ToString("yyyy-MM-dd'T'HH:mm:ss");
        var plan = tage switch
        {
            "einmal" => $"<TimeTrigger><StartBoundary>{start}</StartBoundary><Enabled>true</Enabled></TimeTrigger>",
            "werktags" => $"<CalendarTrigger><StartBoundary>{start}</StartBoundary><Enabled>true</Enabled>"
                          + "<ScheduleByWeek><WeeksInterval>1</WeeksInterval><DaysOfWeek>"
                          + "<Monday /><Tuesday /><Wednesday /><Thursday /><Friday />"
                          + "</DaysOfWeek></ScheduleByWeek></CalendarTrigger>",
            _ => $"<CalendarTrigger><StartBoundary>{start}</StartBoundary><Enabled>true</Enabled>"
                 + "<ScheduleByDay><DaysInterval>1</DaysInterval></ScheduleByDay></CalendarTrigger>",
        };

        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Description>Weckt den Rechner kurz vor dem Wecker von REGOradio. Wird von REGOradio angelegt und wieder entfernt.</Description>
              </RegistrationInfo>
              <Triggers>{plan}</Triggers>
              <Principals>
                <Principal id="Author">
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>LeastPrivilege</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <StartWhenAvailable>false</StartWhenAvailable>
                <WakeToRun>true</WakeToRun>
                <ExecutionTimeLimit>PT5M</ExecutionTimeLimit>
                <Enabled>true</Enabled>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{SecurityElement.Escape(programm)}</Command>
                  <Arguments>--tray</Arguments>
                </Exec>
              </Actions>
            </Task>
            """;
    }

    /// <summary>Anlegen oder ersetzen. Gibt zurück, ob Windows ihn angenommen hat.</summary>
    public static bool Setzen(DateTime erster, string tage)
    {
        var programm = Environment.ProcessPath;
        if (string.IsNullOrEmpty(programm)) return false;
        var datei = Path.Combine(Path.GetTempPath(), $"regoradio-wecker-{Environment.ProcessId}.xml");
        try
        {
            // UTF-16, wie es im Kopf steht – schtasks liest sonst nichts.
            File.WriteAllText(datei, Auftrag(programm, erster, tage), Encoding.Unicode);
            return Planung($"/Create /TN \"{Name}\" /XML \"{datei}\" /F");
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            try { File.Delete(datei); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    public static bool Entfernen() => Planung($"/Delete /TN \"{Name}\" /F");

    private static bool Planung(string argumente)
    {
        try
        {
            using var prozess = Process.Start(new ProcessStartInfo("schtasks.exe", argumente)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (prozess is null) return false;
            prozess.StandardOutput.ReadToEnd();
            prozess.StandardError.ReadToEnd();
            return prozess.WaitForExit(15_000) && prozess.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}

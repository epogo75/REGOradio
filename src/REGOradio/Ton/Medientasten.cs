using Windows.Media;
using Windows.Storage.Streams;

namespace REGOradio.Ton;

/// <summary>Was eine Medientaste von REGOradio will.</summary>
public enum Medienbefehl { Spielen, Anhalten, Weiter, Zurueck }

/// <summary>
/// REGOradio bei Windows als Wiedergabe angemeldet (Bau 16).
///
/// **Warum.** Der Ton läuft über NAudio und WASAPI, an Windows' eigener
/// Wiedergabe vorbei. Windows wusste deshalb nicht, dass hier etwas spielt:
/// Play/Pause auf der Tastatur und die Knöpfe einer Bluetooth-Box gingen ins
/// Leere, und das Lautstärkefenster zeigte nichts an. Über die
/// SystemMediaTransportControls (SMTC) meldet sich das Programm an; Windows
/// schickt die Tasten dann hierher und zeigt Sender, Titel und Cover im
/// Lautstärkefenster und auf dem Sperrbildschirm.
///
/// **Ein Radio pausiert nicht.** „Pause" heißt hier Stopp – wie die
/// Stationstaste, die man ein zweites Mal drückt (siehe Abspieler: Ein
/// pausierter Abspieler hielte das Audiogerät offen). „Weiter" und „Zurück"
/// gehen zur nächsten belegten Stationstaste.
///
/// **Die Tasten kommen aus einem fremden Faden.** Wer `Gedrueckt` abonniert,
/// muss selbst in den Oberflächenfaden wechseln.
/// </summary>
public sealed class Medientasten : IDisposable
{
    private readonly SystemMediaTransportControls _smtc;
    private string _gezeigt = "";

    public event Action<Medienbefehl>? Gedrueckt;

    private Medientasten(SystemMediaTransportControls smtc)
    {
        _smtc = smtc;
        _smtc.IsEnabled = true;
        _smtc.IsPlayEnabled = true;
        _smtc.IsPauseEnabled = true;
        _smtc.IsStopEnabled = true;
        _smtc.IsNextEnabled = true;
        _smtc.IsPreviousEnabled = true;
        _smtc.ButtonPressed += Taste;
    }

    /// <summary>
    /// Für ein Fenster anmelden. Null, wenn Windows es ablehnt – ältere
    /// Windows-Fassungen, eine abgespeckte Sitzung. Dann gibt es eben keine
    /// Medientasten; das Radio läuft trotzdem.
    /// </summary>
    public static Medientasten? Anmelden(IntPtr fenster)
    {
        try
        {
            return new Medientasten(SystemMediaTransportControlsInterop.GetForWindow(fenster));
        }
        catch (Exception fehler) when (fehler is System.Runtime.InteropServices.COMException
                                                 or UnauthorizedAccessException
                                                 or InvalidCastException
                                                 or PlatformNotSupportedException)
        {
            return null;
        }
    }

    private void Taste(SystemMediaTransportControls absender, SystemMediaTransportControlsButtonPressedEventArgs e)
    {
        Medienbefehl? befehl = e.Button switch
        {
            SystemMediaTransportControlsButton.Play => Medienbefehl.Spielen,
            SystemMediaTransportControlsButton.Pause => Medienbefehl.Anhalten,
            SystemMediaTransportControlsButton.Stop => Medienbefehl.Anhalten,
            SystemMediaTransportControlsButton.Next => Medienbefehl.Weiter,
            SystemMediaTransportControlsButton.Previous => Medienbefehl.Zurueck,
            _ => null,
        };
        if (befehl is { } b) Gedrueckt?.Invoke(b);
    }

    /// <summary>
    /// Was Windows anzeigen soll. Ohne Titel vom Sender steht der Sender als
    /// Titel da – im Lautstärkefenster ist eine leere Zeile schlechter als ein
    /// Name. Gleiches wird nicht noch einmal geschickt: Das Fenster meldet bei
    /// jedem Zustand neu, Windows müsste sonst jedes Mal das Bild neu holen.
    /// </summary>
    public void Zeigen(bool laeuft, string sender, string titel, string interpret, string bildAdresse)
    {
        var schluessel = $"{laeuft}\u001f{sender}\u001f{titel}\u001f{interpret}\u001f{bildAdresse}";
        if (schluessel == _gezeigt) return;
        _gezeigt = schluessel;

        try
        {
            _smtc.PlaybackStatus = laeuft ? MediaPlaybackStatus.Playing
                : sender.Length > 0 ? MediaPlaybackStatus.Changing
                : MediaPlaybackStatus.Stopped;

            var anzeige = _smtc.DisplayUpdater;
            anzeige.ClearAll();
            if (sender.Length > 0)
            {
                anzeige.Type = MediaPlaybackType.Music;
                anzeige.MusicProperties.Title = titel.Length > 0 ? titel : sender;
                anzeige.MusicProperties.Artist = titel.Length > 0 ? interpret : "";
                anzeige.MusicProperties.AlbumTitle = sender;
                anzeige.MusicProperties.AlbumArtist = sender;
                if (Uri.TryCreate(bildAdresse, UriKind.Absolute, out var bild) && bild.Scheme is "http" or "https")
                {
                    anzeige.Thumbnail = RandomAccessStreamReference.CreateFromUri(bild);
                }
            }
            anzeige.Update();
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Windows hat die Anzeige abgelehnt; die Tasten gehen trotzdem.
        }
    }

    public void Dispose()
    {
        _smtc.ButtonPressed -= Taste;
        try
        {
            _smtc.PlaybackStatus = MediaPlaybackStatus.Closed;
            _smtc.IsEnabled = false;
        }
        catch (System.Runtime.InteropServices.COMException) { }
    }
}

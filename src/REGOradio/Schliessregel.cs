namespace REGOradio;

/// <summary>Was passiert, wenn das Fenster zugeht.</summary>
public enum Schliessart
{
    /// <summary>Kurz fragen: in den Tray oder ganz beenden.</summary>
    Fragen,

    /// <summary>Im Tray weiterlaufen, das Radio spielt weiter.</summary>
    Tray,

    /// <summary>Ganz beenden, das Radio verstummt.</summary>
    Beenden,
}

/// <summary>
/// Die Regel fürs Schließen – getrennt vom Fenster, damit sie prüfbar ist.
///
/// **Fragen sticht die gespeicherte Wahl.** `ImTrayBleiben` gab es schon vor
/// der Frage (bis Bau 10 war es die einzige Einstellung). Es bleibt die
/// Antwort für den Fall, dass jemand „Nicht mehr fragen" angekreuzt hat –
/// so kehrt das alte Verhalten zurück, sobald man die Frage abstellt.
/// </summary>
public static class Schliessregel
{
    public static Schliessart Entscheiden(bool fragen, bool imTray) =>
        fragen ? Schliessart.Fragen
        : imTray ? Schliessart.Tray
        : Schliessart.Beenden;
}

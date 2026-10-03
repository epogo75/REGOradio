namespace REGOradio.Modelle;

/// <summary>
/// Was eine Wischbewegung im Feld „Läuft" heißt (Bau 23). Reine Rechnung,
/// geprüft in `WischenPruefung`.
///
/// **Nach links ist weiter.** Wie beim Blättern in Bildern: Der Finger
/// schiebt den jetzigen Sender aus dem Feld, der nächste kommt von rechts.
///
/// **Erst ab 70 Punkten, und deutlich waagerecht.** Ein Tipp wackelt am
/// Touchscreen um ein paar Punkte, und wer nach unten zur Lautstärke will,
/// streift das Feld schräg. Beides darf keinen Sender wechseln – ein
/// Fehlwechsel ist ärgerlicher als ein Wischen, das zweimal nötig ist.
/// </summary>
public static class Wischen
{
    public const double Mindestweg = 70;

    /// <summary>Ab hier folgt das Feld sichtbar dem Finger.</summary>
    public const double Sichtbar = 12;

    /// <returns>+1: nächster Sender, −1: voriger, 0: kein Wischen.</returns>
    public static int Deuten(double dx, double dy)
    {
        if (Math.Abs(dx) < Mindestweg || Math.Abs(dx) < 1.5 * Math.Abs(dy)) return 0;
        return dx < 0 ? 1 : -1;
    }

    /// <summary>Ob die Bewegung schon als Wischen zählt und das Feld folgen soll.</summary>
    public static bool Waagerecht(double dx, double dy) =>
        Math.Abs(dx) >= Sichtbar && Math.Abs(dx) >= 1.5 * Math.Abs(dy);
}

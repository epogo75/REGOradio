namespace REGOradio.Modelle;

/// <summary>
/// Sender auf den Tasten umordnen.
///
/// **Tauschen, nicht einschieben.** Wer „SWR3" von Taste 4 auf Taste 1 zieht,
/// will SWR3 auf der 1 -- und das, was dort lag, nicht verlieren. Einschieben
/// mit Nachrücken würde alle Tasten dazwischen verschieben, und plötzlich
/// liegt auf der 3 nicht mehr, was man dort seit Tagen drückt. Tauschen
/// ändert genau die zwei Tasten, die man angefasst hat.
///
/// Reine Rechnung auf der Liste: geprüft in `TastenbelegungPruefung`, ohne
/// Oberfläche.
/// </summary>
public static class Tastenbelegung
{
    /// <summary>
    /// Den Sender von <paramref name="von"/> auf <paramref name="nach"/> legen.
    /// Liegt dort einer, wandert er auf <paramref name="von"/>; ist die Zieltaste
    /// frei, wird die Ausgangstaste frei.
    /// </summary>
    /// <returns>Ob sich etwas geändert hat.</returns>
    public static bool Tauschen(List<Sender> sender, int von, int nach)
    {
        if (von == nach) return false;
        var bewegt = sender.FirstOrDefault(s => s.Platz == von);
        if (bewegt is null) return false;   // eine freie Taste lässt sich nicht ziehen

        var verdraengt = sender.FirstOrDefault(s => s.Platz == nach);
        bewegt.Platz = nach;
        if (verdraengt is not null) verdraengt.Platz = von;
        return true;
    }

    /// <summary>
    /// Die nächste (<paramref name="schritt"/> = 1) oder vorige (−1) belegte
    /// Taste nach <paramref name="platz"/>, im Kreis – für „Weiter" und
    /// „Zurück" auf der Medientaste (Bau 16). Freie Tasten werden
    /// übersprungen. Ist <paramref name="platz"/> 0 (nichts lief, oder ein
    /// Sender nur zum Anhören), beginnt „Weiter" bei der ersten Taste und
    /// „Zurück" bei der letzten.
    /// </summary>
    public static Sender? Nachbar(IReadOnlyList<Sender> sender, int platz, int schritt)
    {
        var belegt = sender.Where(s => s.Platz > 0).OrderBy(s => s.Platz).ToList();
        if (belegt.Count == 0) return null;
        if (platz <= 0) return schritt > 0 ? belegt[0] : belegt[^1];

        if (schritt > 0) return belegt.FirstOrDefault(s => s.Platz > platz) ?? belegt[0];
        return belegt.LastOrDefault(s => s.Platz < platz) ?? belegt[^1];
    }
}

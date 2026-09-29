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
}

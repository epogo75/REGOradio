namespace REGOradio.Speicher;

/// <summary>Ein Lied, das lief – mit Uhrzeit, Sender und Cover.</summary>
public sealed class Gespielt
{
    public DateTime Zeit { get; set; }
    public string Sender { get; set; } = "";
    public string Interpret { get; set; } = "";
    public string Titel { get; set; } = "";
    /// <summary>Adresse des Covers, sobald die Suche eines fand; sonst leer.</summary>
    public string Cover { get; set; } = "";
}

/// <summary>
/// „Was lief vorhin?" (Bau 18): die letzten Lieder, über Neustarts hinweg.
///
/// **Nur Lieder.** Eingetragen wird nur, was die Form „Interpret - Titel"
/// hat; Werbung, Sendungsnamen und Durchsagen („Kontakt zu SWR3:
/// info@swr3.de") bleiben draußen – dieselbe Regel wie bei der Coversuche.
///
/// **Kein Lied zweimal hintereinander.** Sender melden denselben Titel im
/// Takt neu, und nach einem Neuverbinden noch einmal. Erst ein anderes Lied
/// oder ein anderer Sender ist ein neuer Eintrag.
///
/// Reine Rechnung auf der Liste, geprüft in `VerlaufPruefung`.
/// </summary>
public static class Verlauf
{
    public const int Hoechstens = 50;

    /// <returns>Ob ein Eintrag dazukam.</returns>
    public static bool Eintragen(List<Gespielt> liste, string sender, string interpret, string titel, DateTime jetzt)
    {
        interpret = interpret.Trim();
        titel = titel.Trim();
        if (sender.Length == 0 || interpret.Length == 0 || titel.Length == 0) return false;

        var letzter = liste.Count > 0 ? liste[0] : null;
        if (letzter is not null
            && string.Equals(letzter.Sender, sender, StringComparison.OrdinalIgnoreCase)
            && string.Equals(letzter.Interpret, interpret, StringComparison.OrdinalIgnoreCase)
            && string.Equals(letzter.Titel, titel, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        liste.Insert(0, new Gespielt { Zeit = jetzt, Sender = sender, Interpret = interpret, Titel = titel });
        if (liste.Count > Hoechstens) liste.RemoveRange(Hoechstens, liste.Count - Hoechstens);
        return true;
    }

    /// <summary>
    /// Das Cover kommt erst, wenn die Suche fertig ist – dann gehört es zum
    /// neuesten Eintrag, aber nur, wenn das noch dasselbe Lied ist.
    /// </summary>
    public static bool CoverNachtragen(List<Gespielt> liste, string interpret, string titel, string cover)
    {
        if (cover.Length == 0 || liste.Count == 0) return false;
        var letzter = liste[0];
        if (letzter.Cover.Length > 0
            || !string.Equals(letzter.Interpret, interpret.Trim(), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(letzter.Titel, titel.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
        letzter.Cover = cover;
        return true;
    }

    /// <summary>„14:32" heute, „gestern 22:10", sonst „Mo 3.10. 22:10".</summary>
    public static string Wann(DateTime zeit, DateTime jetzt)
    {
        var uhr = zeit.ToString("HH:mm");
        if (zeit.Date == jetzt.Date) return uhr;
        if (zeit.Date == jetzt.Date.AddDays(-1)) return $"gestern {uhr}";
        var tag = new System.Globalization.CultureInfo("de-DE").DateTimeFormat.GetAbbreviatedDayName(zeit.DayOfWeek);
        return $"{tag} {zeit:d.M.} {uhr}";
    }
}

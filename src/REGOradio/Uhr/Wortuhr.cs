namespace REGOradio.Uhr;

/// <summary>
/// Die Wortuhr
/// (`frontend/src/teile/Uhren.tsx`), Raster und Wortstellen unverändert.
///
/// **Die Wörter stehen fest im Raster, die Uhr leuchtet nur die richtigen
/// auf.** Deshalb ist das Feld eine Konstante und keine Berechnung: So sieht
/// man beim Lesen, was auf dem Schirm steht.
///
/// **Koordinaten sind die Falle.** Eine Spalte daneben, und auf dem Schirm
/// steht „FÜNF ACH". Beim ersten Entwurf war das viermal der
/// Fall; gefunden hat es die Prüfung, nicht das Nachdenken. Sie steht in
/// `WortuhrPruefung`.
///
/// Reine Rechnung, keine Oberfläche: Die Anzeige fragt nur, welche Felder
/// leuchten.
/// </summary>
public static class Wortuhr
{
    /// <summary>Elf Spalten, zehn Zeilen -- die klassische Aufteilung.</summary>
    public static readonly string[] Feld =
    [
        "ESKISTAFÜNF",
        "ZEHNZWANZIG",
        "DREIVIERTEL",
        "VORFUNKNACH",
        "HALBAELFÜNF",
        "EINSXAMZWEI",
        "DREIPMJVIER",
        "SECHSNLACHT",
        "SIEBENZWÖLF",
        "ZEHNEUNKUHR",
    ];

    /// <summary>Ein Wort im Feld: Zeile, erste Spalte, Länge.</summary>
    public readonly record struct Stelle(int Zeile, int Spalte, int Laenge);

    public static readonly Stelle[] EsIst = [new(0, 0, 2), new(0, 3, 3)];

    public static readonly IReadOnlyDictionary<int, Stelle[]> Minuten = new Dictionary<int, Stelle[]>
    {
        [0] = [],
        [5] = [new(0, 7, 4), new(3, 7, 4)],                  // FÜNF NACH
        [10] = [new(1, 0, 4), new(3, 7, 4)],                 // ZEHN NACH
        [15] = [new(2, 4, 7), new(3, 7, 4)],                 // VIERTEL NACH
        [20] = [new(1, 4, 7), new(3, 7, 4)],                 // ZWANZIG NACH
        [25] = [new(0, 7, 4), new(3, 0, 3), new(4, 0, 4)],   // FÜNF VOR HALB
        [30] = [new(4, 0, 4)],                               // HALB
        [35] = [new(0, 7, 4), new(3, 7, 4), new(4, 0, 4)],   // FÜNF NACH HALB
        [40] = [new(1, 4, 7), new(3, 0, 3)],                 // ZWANZIG VOR
        [45] = [new(2, 4, 7), new(3, 0, 3)],                 // VIERTEL VOR
        [50] = [new(1, 0, 4), new(3, 0, 3)],                 // ZEHN VOR
        [55] = [new(0, 7, 4), new(3, 0, 3)],                 // FÜNF VOR
    };

    public static readonly IReadOnlyDictionary<int, Stelle[]> Stunden = new Dictionary<int, Stelle[]>
    {
        [1] = [new(5, 0, 4)],
        [2] = [new(5, 7, 4)],
        [3] = [new(6, 0, 4)],
        [4] = [new(6, 7, 4)],
        [5] = [new(4, 7, 4)],
        [6] = [new(7, 0, 5)],
        [7] = [new(8, 0, 6)],
        [8] = [new(7, 7, 4)],
        [9] = [new(9, 3, 4)],
        [10] = [new(9, 0, 4)],
        [11] = [new(4, 5, 3)],
        [12] = [new(8, 6, 5)],
    };

    /// <summary>„EIN" ohne S -- nur bei „EIN UHR". Der Sonderfall, den jede
    /// Wortuhr hat und den man sonst als Fehler liest.</summary>
    public static readonly Stelle[] EinKurz = [new(5, 0, 3)];

    public static readonly Stelle[] UhrWort = [new(9, 8, 3)];

    /// <summary>Die Wörter, die zu dieser Zeit leuchten.</summary>
    public static IEnumerable<Stelle> Woerter(DateTime zeit)
    {
        var stufe = zeit.Minute / 5 * 5;
        // Ab 25 nach spricht man zur nächsten Stunde hin: „fünf vor halb
        // zwölf" meint elf Uhr fünfundzwanzig.
        var stundeRoh = zeit.Hour % 12 == 0 ? 12 : zeit.Hour % 12;
        var stunde = stufe >= 25 ? stundeRoh % 12 + 1 : stundeRoh;

        foreach (var s in EsIst) yield return s;
        foreach (var s in Minuten[stufe]) yield return s;
        // „Ein Uhr", nicht „Eins Uhr": nur, wenn keine Minutenangabe davorsteht.
        foreach (var s in stunde == 1 && stufe == 0 ? EinKurz : Stunden[stunde]) yield return s;
        if (stufe == 0) foreach (var s in UhrWort) yield return s;
    }

    /// <summary>Welche Felder leuchten, als (Zeile, Spalte).</summary>
    public static HashSet<(int Zeile, int Spalte)> Leuchtet(DateTime zeit)
    {
        var an = new HashSet<(int, int)>();
        foreach (var wort in Woerter(zeit))
        {
            for (var i = 0; i < wort.Laenge; i++) an.Add((wort.Zeile, wort.Spalte + i));
        }
        return an;
    }

    /// <summary>Den Text eines Wortes aus dem Feld lesen.</summary>
    public static string Lesen(Stelle stelle) => Feld[stelle.Zeile].Substring(stelle.Spalte, stelle.Laenge);

    /// <summary>Was die Uhr zu dieser Zeit sagt -- für die Prüfung und für
    /// Bildschirmleser.</summary>
    public static string Satz(DateTime zeit) => string.Join(" ", Woerter(zeit).Select(Lesen));
}

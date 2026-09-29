namespace REGOradio.Uhr;

/// <summary>
/// Die Wortuhr – mit einem eigenen Raster: zwölf Spalten, elf Zeilen, in
/// der letzten Zeile „REGORADIO“ vor dem „UHR“. Das bekannte Raster der
/// Wortuhren aus dem Handel ist deren Gestaltung und gehört nicht hierher.
///
/// **Die Reihenfolge ist die Bedingung:** Was zusammen einen Satz ergibt,
/// muss von links oben nach rechts unten in Lesefolge stehen – ES IST, dann
/// die Minuten, dann VOR/NACH, dann HALB, dann die Stunde, zuletzt UHR.
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
    /// <summary>Zwölf Spalten, elf Zeilen.</summary>
    public static readonly string[] Feld =
    [
        "ESJISTOPFÜNF",
        "ZEHNXZWANZIG",
        "BVIERTELMVOR",
        "NACHLOXSHALB",
        "OEINSBLUZWEI",
        "OKDREIBVIERW",
        "FÜNFZSECHSKA",
        "SIEBENGACHTM",
        "NEUNOPZEHNRD",
        "TELFQZWÖLFUS",
        "REGORADIOUHR",
    ];

    /// <summary>Zeilen und Spalten des Feldes – die Anzeige richtet ihr
    /// Raster danach, statt die Maße noch einmal festzuschreiben.</summary>
    public static int Zeilen => Feld.Length;
    public static int Spalten => Feld[0].Length;

    /// <summary>Ein Wort im Feld: Zeile, erste Spalte, Länge.</summary>
    public readonly record struct Stelle(int Zeile, int Spalte, int Laenge);

    public static readonly Stelle[] EsIst = [new(0, 0, 2), new(0, 3, 3)];

    public static readonly IReadOnlyDictionary<int, Stelle[]> Minuten = new Dictionary<int, Stelle[]>
    {
        [0] = [],
        [5] = [new(0, 8, 4), new(3, 0, 4)],                  // FÜNF NACH
        [10] = [new(1, 0, 4), new(3, 0, 4)],                 // ZEHN NACH
        [15] = [new(2, 1, 7), new(3, 0, 4)],                 // VIERTEL NACH
        [20] = [new(1, 5, 7), new(3, 0, 4)],                 // ZWANZIG NACH
        [25] = [new(0, 8, 4), new(2, 9, 3), new(3, 8, 4)],   // FÜNF VOR HALB
        [30] = [new(3, 8, 4)],                               // HALB
        [35] = [new(0, 8, 4), new(3, 0, 4), new(3, 8, 4)],   // FÜNF NACH HALB
        [40] = [new(1, 5, 7), new(2, 9, 3)],                 // ZWANZIG VOR
        [45] = [new(2, 1, 7), new(2, 9, 3)],                 // VIERTEL VOR
        [50] = [new(1, 0, 4), new(2, 9, 3)],                 // ZEHN VOR
        [55] = [new(0, 8, 4), new(2, 9, 3)],                 // FÜNF VOR
    };

    public static readonly IReadOnlyDictionary<int, Stelle[]> Stunden = new Dictionary<int, Stelle[]>
    {
        [1] = [new(4, 1, 4)],
        [2] = [new(4, 8, 4)],
        [3] = [new(5, 2, 4)],
        [4] = [new(5, 7, 4)],
        [5] = [new(6, 0, 4)],
        [6] = [new(6, 5, 5)],
        [7] = [new(7, 0, 6)],
        [8] = [new(7, 7, 4)],
        [9] = [new(8, 0, 4)],
        [10] = [new(8, 6, 4)],
        [11] = [new(9, 1, 3)],
        [12] = [new(9, 5, 5)],
    };

    /// <summary>„EIN" ohne S -- nur bei „EIN UHR". Der Sonderfall, den jede
    /// Wortuhr hat und den man sonst als Fehler liest.</summary>
    public static readonly Stelle[] EinKurz = [new(4, 1, 3)];

    public static readonly Stelle[] UhrWort = [new(10, 9, 3)];

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

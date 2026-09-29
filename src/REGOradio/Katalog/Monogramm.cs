using System.Windows.Media;

namespace REGOradio.Katalog;

/// <summary>
/// Kürzel und Farbe für einen Sender, solange -- oder wenn -- kein Logo da ist.
///
/// **Die Farbe gehört zum Namen, nicht zum Zufall.** Sie wird aus den
/// Buchstaben gerechnet, deshalb hat „SWR3" morgen dieselbe Farbe wie heute und
/// auf jedem Rechner. `string.GetHashCode` taugt dafür nicht: .NET mischt ihn
/// bei jedem Programmstart neu.
///
/// **Gedeckte Farben mit weißer Schrift**, alle dunkel genug für einen
/// Kontrast über 4,5:1 -- ein Kürzel muss man aus zwei Metern lesen.
/// </summary>
public static class Monogramm
{
    private static readonly Color[] Farben =
    [
        Color.FromRgb(0x2F, 0x5D, 0x8A),
        Color.FromRgb(0x3C, 0x6E, 0x47),
        Color.FromRgb(0x6B, 0x4E, 0x2E),
        Color.FromRgb(0x7A, 0x3B, 0x52),
        Color.FromRgb(0x4E, 0x4A, 0x7A),
        Color.FromRgb(0x8A, 0x5A, 0x1F),
        Color.FromRgb(0x2E, 0x67, 0x70),
        Color.FromRgb(0x6E, 0x3A, 0x3A),
        Color.FromRgb(0x45, 0x55, 0x6B),
        Color.FromRgb(0x5E, 0x55, 0x30),
    ];

    public static Color Farbe(string name)
    {
        var summe = 0;
        foreach (var zeichen in name.Trim().ToUpperInvariant()) summe = unchecked(summe * 31 + zeichen);
        return Farben[(int)((uint)summe % Farben.Length)];
    }

    /// <summary>
    /// Zwei bis drei Zeichen aus dem Namen.
    ///
    /// Die Regel folgt dem, wie Sender sich selbst abkürzen: „Bayern 3" wird
    /// „B3", nicht „BA" -- die Zahl ist der Name. Ein Wort allein gibt seine
    /// ersten beiden Buchstaben, mehrere Wörter ihre Anfänge.
    /// </summary>
    public static string Kuerzel(string name)
    {
        var woerter = name
            .Split([' ', '-', '_', '|', '/', '.'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => char.IsLetterOrDigit(w[0]))
            .ToArray();
        if (woerter.Length == 0) return "?";

        if (woerter.Length == 1)
        {
            var wort = woerter[0];
            // „SWR3", „1LIVE": Buchstabe und Ziffer zusammen sind das Kürzel.
            var ziffer = wort.FirstOrDefault(char.IsDigit);
            if (ziffer != default && char.IsLetter(wort[0])) return $"{char.ToUpperInvariant(wort[0])}{ziffer}";
            return wort.Length >= 2 ? wort[..2].ToUpperInvariant() : wort.ToUpperInvariant();
        }

        // Endet der Name auf eine Zahl, gehört sie ins Kürzel.
        var letztes = woerter[^1];
        if (letztes.All(char.IsDigit)) return $"{char.ToUpperInvariant(woerter[0][0])}{letztes}";

        return string.Concat(woerter.Take(2).Select(w => char.ToUpperInvariant(w[0])));
    }
}

namespace REGOradio;

/// <summary>
/// Version und Baunummer -- die einzige Quelle dafür.
///
/// **Die Baunummer wird bei jeder ausgelieferten Änderung erhöht.** Ohne sie
/// lässt sich am laufenden Programm nicht sagen, ob das, was im Fenster steht,
/// dem entspricht, was gerade gebaut wurde.
///
/// Die Baunummer geht außerdem in den User-Agent, mit dem das Senderverzeichnis
/// befragt wird. Es ist ein freier Dienst und bittet ausdrücklich um eine
/// erkennbare Kennung.
/// </summary>
internal static class Bau
{
    public const string Version = "0.1";
    public const int Nummer = 27;
    public const string Stand = "2026-10-07";

    public const string Programm = "REGOradio";
    public const string Copyright = "© 2026 Stephan Ruf";

    /// <summary>Was wir fremden Diensten über uns sagen.</summary>
    public static string Kennung => $"{Programm}/{Version}.{Nummer}";
}

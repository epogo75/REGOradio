namespace REGOradio.Modelle;

/// <summary>
/// Ein Sender, wie ihn das Programm behält.
///
/// `Kennung` ist die Nummer aus dem Senderverzeichnis. Sie steht dabei, damit
/// eine Station später wiederzufinden ist, wenn sich ihre Adresse ändert -- bei
/// Internetradios passiert genau das, und dann ist die Taste sonst tot.
/// </summary>
public sealed class Sender
{
    public string Kennung { get; set; } = "";
    public string Name { get; set; } = "";
    public string Adresse { get; set; } = "";
    /// <summary>Adresse des Senderlogos, oder leer.</summary>
    public string Logo { get; set; } = "";
    /// <summary>
    /// Die Homepage des Senders, oder leer. Seit Bau 15: Hat der Sender kein
    /// eigenes Logo im Verzeichnis, wird es dort gesucht (`Logos.HolenFuer`).
    /// </summary>
    public string Homepage { get; set; } = "";
    public string Land { get; set; } = "";
    public string Genre { get; set; } = "";
    public string Codec { get; set; } = "";
    public int Bitrate { get; set; }

    /// <summary>
    /// Wie laut der Sender zuletzt gemessen wurde, in LUFS (Bau 21). Damit
    /// klingt er beim nächsten Mal vom ersten Ton an angeglichen.
    /// </summary>
    public double? Lautheit { get; set; }

    /// <summary>
    /// Auf welcher Stationstaste der Sender liegt, von 1 an gezählt. 0 heißt:
    /// auf keiner -- dann steht er nur in der Liste.
    /// </summary>
    public int Platz { get; set; }
}

/// <summary>Was ein Suchlauf im Verzeichnis liefert.</summary>
public sealed class Treffer
{
    public string Kennung { get; init; } = "";
    public string Name { get; init; } = "";
    public string Adresse { get; init; } = "";
    public string Logo { get; init; } = "";
    public string Homepage { get; init; } = "";
    public string Land { get; init; } = "";
    public string Genre { get; init; } = "";
    public string Codec { get; init; } = "";
    public int Bitrate { get; init; }

    public Sender AlsSender(int platz = 0) => new()
    {
        Kennung = Kennung,
        Name = Name,
        Adresse = Adresse,
        Logo = Logo,
        Homepage = Homepage,
        Land = Land,
        Genre = Genre,
        Codec = Codec,
        Bitrate = Bitrate,
        Platz = platz,
    };
}

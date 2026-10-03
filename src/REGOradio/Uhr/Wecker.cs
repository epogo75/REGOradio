namespace REGOradio.Uhr;

/// <summary>
/// Einschlafen und Wecken (Bau 20): die Rechnung, ohne Uhr und Oberfläche.
/// Geprüft in `WeckerPruefung`.
///
/// **Das Weckfenster ist zehn Minuten lang.** Wacht der Rechner aus dem
/// Energiesparen auf, vergehen bis zum ersten Takt des Programms gern ein,
/// zwei Minuten. Ein Wecker, der auf die Sekunde genau prüft, käme dann nie.
/// Dafür wird pro Tag nur einmal geweckt (`zuletzt`).
///
/// **Leise anfangen.** Geweckt wird mit Lautstärke 5, die in zwei Minuten
/// auf die eingestellte steigt – ein Radio, das um 6:45 mit 60 losplärrt,
/// stellt man genau einmal.
///
/// **Leise aufhören.** Beim Einschlafen sinkt die Lautstärke in der letzten
/// Minute auf null, dann wird gestoppt. Ein harter Schnitt weckt wieder.
/// </summary>
public static class Wecker
{
    public const int Fensterminuten = 10;
    public const int Startlautstaerke = 5;
    public static readonly TimeSpan Anstieg = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan Ausklang = TimeSpan.FromMinutes(1);

    public static bool Werktag(DateTime tag) => tag.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    private static bool Gilt(DateTime tag, string tage) => tage != "werktags" || Werktag(tag);

    /// <summary>Ob jetzt geweckt werden muss.</summary>
    public static bool Faellig(DateTime jetzt, int minuten, string tage, DateTime? zuletzt)
    {
        var weckzeit = jetzt.Date.AddMinutes(minuten);
        if (jetzt < weckzeit || jetzt >= weckzeit.AddMinutes(Fensterminuten)) return false;
        if (!Gilt(jetzt, tage)) return false;
        return zuletzt is not { } z || z < weckzeit;
    }

    /// <summary>Wann der Wecker das nächste Mal klingelt, nach <paramref name="jetzt"/>.</summary>
    public static DateTime Naechster(DateTime jetzt, int minuten, string tage)
    {
        for (var tag = 0; tag < 8; tag++)
        {
            var zeit = jetzt.Date.AddDays(tag).AddMinutes(minuten);
            if (zeit > jetzt && Gilt(zeit, tage)) return zeit;
        }
        return jetzt.Date.AddDays(1).AddMinutes(minuten);   // unerreichbar, aber nie ohne Antwort
    }

    /// <summary>Lautstärke beim Wecken, <paramref name="vergangen"/> nach dem Start.</summary>
    public static int Rampe(TimeSpan vergangen, int ziel)
    {
        if (ziel <= Startlautstaerke) return ziel;
        var anteil = Math.Clamp(vergangen / Anstieg, 0, 1);
        return (int)Math.Round(Startlautstaerke + (ziel - Startlautstaerke) * anteil);
    }

    /// <summary>Lautstärke beim Einschlafen, <paramref name="rest"/> vor dem Ende.</summary>
    public static int Ausblenden(TimeSpan rest, int voll)
    {
        if (rest >= Ausklang) return voll;
        if (rest <= TimeSpan.Zero) return 0;
        return (int)Math.Round(voll * (rest / Ausklang));
    }

    /// <summary>„in 7 Std 12 Min", „in 25 Min", „gleich".</summary>
    public static string Abstand(TimeSpan bis)
    {
        if (bis < TimeSpan.FromMinutes(1)) return "gleich";
        var minuten = (int)Math.Ceiling(bis.TotalMinutes);
        var stunden = minuten / 60;
        minuten %= 60;
        return stunden switch
        {
            0 => $"in {minuten} Min",
            _ when minuten == 0 => $"in {stunden} Std",
            _ => $"in {stunden} Std {minuten} Min",
        };
    }

    /// <summary>„Mo 06:45" – der Tag, damit man sieht, dass Mo–Fr das Wochenende auslässt.</summary>
    public static string Wann(DateTime zeit)
    {
        var tag = new System.Globalization.CultureInfo("de-DE").DateTimeFormat.GetAbbreviatedDayName(zeit.DayOfWeek);
        return $"{tag} {zeit:HH:mm}";
    }
}

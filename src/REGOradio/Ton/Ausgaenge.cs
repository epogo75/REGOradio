using NAudio.CoreAudioApi;

namespace REGOradio.Ton;

/// <summary>Wohin der Ton geht.</summary>
public sealed record Ausgang(string Kennung, string Name, bool Bluetooth, bool IstStandard)
{
    /// <summary>Was im Fenster steht.</summary>
    public string Anzeige => Bluetooth ? $"{Name} (Bluetooth)" : Name;
}

/// <summary>
/// Die Audiogeräte von Windows.
///
/// **Bluetooth-Boxen koppelt Windows, nicht dieses Programm.** Eine gekoppelte
/// Box ist ein Gerät wie jedes andere und steht hier in der Liste. Das ist der
/// ganze Unterschied zum Pi, wo dafür BlueZ, A2DP und AVRCP nötig sind -- und
/// der Grund, warum es hier nichts davon gibt.
/// </summary>
public static class Ausgaenge
{
    /// <summary>
    /// Woran eine Bluetooth-Box zu erkennen ist.
    ///
    /// Windows hängt sie über den Aufzähler `BTHENUM` ein, und der steht in den
    /// Eigenschaften des Geräts. Am Namen allein lässt sich das nicht
    /// festmachen: Eine Box heißt „JBL Flip", nicht „Bluetooth" -- und ein
    /// USB-Adapter kann sehr wohl „Bluetooth Audio" heißen.
    /// </summary>
    private static bool IstBluetooth(MMDevice geraet)
    {
        try
        {
            for (var i = 0; i < geraet.Properties.Count; i++)
            {
                var wert = geraet.Properties[i];
                if (wert.Value is string text
                    && (text.StartsWith("BTHENUM", StringComparison.OrdinalIgnoreCase)
                        || text.StartsWith("BTHLE", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
        }
        catch (Exception)
        {
            // Eigenschaften eines Geräts, das gerade verschwindet, sind nicht
            // lesbar. Dann gilt: kein Bluetooth -- die Liste soll trotzdem
            // stehen.
        }
        return false;
    }

    public static List<Ausgang> Lesen()
    {
        using var aufzaehler = new MMDeviceEnumerator();
        string standard = "";
        try
        {
            standard = aufzaehler.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID;
        }
        catch (Exception)
        {
            // Kein Standardgerät: Kommt vor, wenn gar keine Soundkarte aktiv
            // ist. Dann bleibt die Liste leer, und das Fenster sagt es.
        }

        var heraus = new List<Ausgang>();
        foreach (var geraet in aufzaehler.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            heraus.Add(new Ausgang(
                Kennung: geraet.ID,
                Name: geraet.FriendlyName,
                Bluetooth: IstBluetooth(geraet),
                IstStandard: geraet.ID == standard));
        }
        // Die Box zuerst: Wer eine dabei hat, will auf sie hören -- sonst wäre
        // sie nicht gekoppelt.
        return heraus
            .OrderByDescending(a => a.Bluetooth)
            .ThenByDescending(a => a.IstStandard)
            .ThenBy(a => a.Name)
            .ToList();
    }

    /// <summary>
    /// Das Gerät zu einer gemerkten Kennung -- oder das Standardgerät.
    ///
    /// **Kein stilles Ausweichen auf irgendein anderes Gerät.** Ist die Box aus
    /// oder außer Reichweite, gibt es das gemerkte Gerät nicht mehr; dann spielt
    /// das Programm über den Windows-Standard und sagt das. Ton, der plötzlich
    /// aus dem Notebook statt aus der Box kommt, überrascht im Hotelzimmer
    /// unangenehm -- aber stumm bleiben ohne Hinweis ist schlimmer.
    /// </summary>
    public static MMDevice? Geraet(string kennung)
    {
        using var aufzaehler = new MMDeviceEnumerator();
        if (kennung.Length > 0)
        {
            foreach (var geraet in aufzaehler.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                if (geraet.ID == kennung) return geraet;
            }
        }
        try
        {
            return aufzaehler.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch (Exception)
        {
            return null;
        }
    }
}

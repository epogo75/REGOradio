using System.IO;

using REGOradio.Netz;

using Xunit;

namespace REGOradio.Pruefung;

/// <summary>
/// Die Auswertung von `netsh wlan show interfaces`.
///
/// **`netsh_deutsch.txt` ist abgelesen** -- an einem Dell 7320 unter einem
/// deutschen Windows 11, über `chcp 65001` wie im Programm. Netzname und
/// Adressen sind ersetzt, der Aufbau ist echt, samt der Zeile mit mehreren
/// Doppelpunkten und der Leerzeichen hinter „92%".
///
/// **`netsh_englisch.txt` und `netsh_getrennt.txt` sind nachgestellt**, nach
/// dem Aufbau der echten Ausgabe. Sobald Ausgaben von einem englischen System
/// und aus einem getrennten Zustand vorliegen, ersetzen sie diese.
/// </summary>
public class WlanPruefung
{
    private static string Antwort(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "antworten", name));

    [Fact]
    public void DeutschesWindows()
    {
        var lage = Wlan.AusAusgabe(Antwort("netsh_deutsch.txt"));
        Assert.True(lage.Verbunden);
        Assert.Equal("Hotel-Gäste", lage.Netz);
        Assert.Equal(92, lage.Staerke);
    }

    [Fact]
    public void EnglischesWindows()
    {
        var lage = Wlan.AusAusgabe(Antwort("netsh_englisch.txt"));
        Assert.True(lage.Verbunden);
        Assert.Equal("Hotel Alpenblick", lage.Netz);
        Assert.Equal(58, lage.Staerke);
    }

    [Fact]
    public void BssidIstKeinNetzname()
    {
        // „AP BSSID" steht direkt unter „SSID". Wer auf „enthält SSID" prüft,
        // bekommt bei vertauschter Reihenfolge die MAC-Adresse als Netznamen.
        var lage = Wlan.AusAusgabe(Antwort("netsh_deutsch.txt"));
        Assert.DoesNotContain(":", lage.Netz);
    }

    [Fact]
    public void GetrenntIstNichtVerbunden()
    {
        var lage = Wlan.AusAusgabe(Antwort("netsh_getrennt.txt"));
        Assert.False(lage.Verbunden);
        Assert.Equal("", lage.Netz);
        Assert.Equal("Kein WLAN", lage.Anzeige);
    }

    [Fact]
    public void DisconnectedEnthaeltConnectedUndIstTrotzdemAus()
    {
        var lage = Wlan.AusAusgabe("    State                  : disconnected\n");
        Assert.False(lage.Verbunden);
    }

    [Fact]
    public void LeereAusgabe()
    {
        var lage = Wlan.AusAusgabe("");
        Assert.False(lage.Verbunden);
    }

    [Fact]
    public void AnzeigeMitStaerke()
    {
        var lage = Wlan.AusAusgabe(Antwort("netsh_deutsch.txt"));
        Assert.Equal("Hotel-Gäste · 92 %", lage.Anzeige);
    }
}

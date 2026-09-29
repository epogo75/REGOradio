using System.Net.NetworkInformation;

using REGOradio.Fernbedienung;

using Xunit;

namespace REGOradio.Pruefung;

public class ZugangPruefung
{
    private string _pin = "4711";
    private readonly string _schluessel = Zugang.NeuerSchluessel();
    private DateTime _jetzt = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

    private Zugang Neu() => new(() => _pin, () => _schluessel, () => _jetzt);

    [Fact]
    public void RichtigePinMeldetAn()
    {
        Assert.Equal(Zugang.Ergebnis.Angemeldet, Neu().Anmelden("10.0.0.5", "4711"));
    }

    [Theory]
    [InlineData("0000")]
    [InlineData("471")]
    [InlineData("47111")]
    [InlineData("")]
    [InlineData(null)]
    public void FalschePinNicht(string? versuch)
    {
        Assert.Equal(Zugang.Ergebnis.Falsch, Neu().Anmelden("10.0.0.5", versuch));
    }

    [Fact]
    public void NachFuenfFehlernIstEineMinuteRuhe()
    {
        var zugang = Neu();
        for (var i = 0; i < Zugang.Versuche; i++) zugang.Anmelden("10.0.0.5", "0000");
        // Auch die richtige PIN zählt jetzt nicht -- sonst wäre die Sperre
        // beim Durchprobieren wirkungslos.
        Assert.Equal(Zugang.Ergebnis.Gesperrt, zugang.Anmelden("10.0.0.5", "4711"));

        _jetzt += Zugang.Sperre + TimeSpan.FromSeconds(1);
        Assert.Equal(Zugang.Ergebnis.Angemeldet, zugang.Anmelden("10.0.0.5", "4711"));
    }

    [Fact]
    public void SperreGiltJeAbsender()
    {
        var zugang = Neu();
        for (var i = 0; i < Zugang.Versuche; i++) zugang.Anmelden("10.0.0.5", "0000");
        Assert.Equal(Zugang.Ergebnis.Angemeldet, zugang.Anmelden("10.0.0.6", "4711"));
    }

    [Fact]
    public void NeuePinMeldetAlteHandysAb()
    {
        var zugang = Neu();
        var alt = zugang.Zeichen();
        Assert.True(zugang.ZeichenGilt(alt));
        _pin = "1234";
        Assert.False(zugang.ZeichenGilt(alt));
    }

    [Fact]
    public void ZeichenUeberlebtNeustart()
    {
        // Derselbe Schlüssel und dieselbe PIN ergeben dasselbe Zeichen -- das
        // Handy bleibt angemeldet, auch wenn das Programm neu startet.
        Assert.Equal(Neu().Zeichen(), Neu().Zeichen());
    }

    [Fact]
    public void NeuePinHatVierZiffern()
    {
        for (var i = 0; i < 200; i++) Assert.Matches("^[0-9]{4}$", Zugang.NeuePin());
    }
}

public class AdressePruefung
{
    private static Adresse.Kandidat K(string name, string beschreibung, NetworkInterfaceType art, string ip, bool gateway) =>
        new(name, beschreibung, art, ip, gateway);

    [Fact]
    public void WlanVorVirtuellenKarten()
    {
        // So sieht ein Entwicklerrechner mit WSL aus: Die vEthernet-Karte
        // steht oft zuerst in der Liste. Ihre Adresse im QR-Code hieße, dass
        // das Handy endlos lädt.
        var gewaehlt = Adresse.Waehlen(
        [
            K("vEthernet (WSL)", "Hyper-V Virtual Ethernet Adapter", NetworkInterfaceType.Ethernet, "172.28.16.1", false),
            K("WLAN", "Intel(R) Wi-Fi 6 AX201", NetworkInterfaceType.Wireless80211, "192.168.178.42", true),
        ]);
        Assert.Equal("192.168.178.42", gewaehlt);
    }

    [Fact]
    public void KarteMitGatewayGewinnt()
    {
        var gewaehlt = Adresse.Waehlen(
        [
            K("Ethernet", "Realtek", NetworkInterfaceType.Ethernet, "10.0.0.7", false),
            K("WLAN", "Intel", NetworkInterfaceType.Wireless80211, "192.168.10.50", true),
        ]);
        Assert.Equal("192.168.10.50", gewaehlt);
    }

    [Fact]
    public void OhneAdresseVomRouterNichts()
    {
        Assert.Null(Adresse.Waehlen(
        [
            K("WLAN", "Intel", NetworkInterfaceType.Wireless80211, "169.254.12.3", false),
        ]));
    }
}

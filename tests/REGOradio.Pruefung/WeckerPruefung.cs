using REGOradio.Uhr;

using Xunit;

namespace REGOradio.Pruefung;

/// <summary>Einschlafen und Wecken (Bau 20).</summary>
public class WeckerPruefung
{
    private const int SechsFuenfundvierzig = 6 * 60 + 45;
    private static readonly DateTime Freitag = new(2026, 10, 2);   // Fr
    private static readonly DateTime Samstag = new(2026, 10, 3);   // Sa

    [Fact]
    public void FaelligImFenster()
    {
        Assert.False(Wecker.Faellig(Freitag.AddHours(6).AddMinutes(44), SechsFuenfundvierzig, "taeglich", null));
        Assert.True(Wecker.Faellig(Freitag.AddHours(6).AddMinutes(45), SechsFuenfundvierzig, "taeglich", null));
        // Der Rechner brauchte zum Aufwachen drei Minuten: trotzdem.
        Assert.True(Wecker.Faellig(Freitag.AddHours(6).AddMinutes(48), SechsFuenfundvierzig, "taeglich", null));
        Assert.False(Wecker.Faellig(Freitag.AddHours(6).AddMinutes(55), SechsFuenfundvierzig, "taeglich", null));
    }

    [Fact]
    public void NurEinmalAmTag()
    {
        var geweckt = Freitag.AddHours(6).AddMinutes(45).AddSeconds(15);
        Assert.False(Wecker.Faellig(Freitag.AddHours(6).AddMinutes(47), SechsFuenfundvierzig, "taeglich", geweckt));
        // Gestern geweckt zählt heute nicht.
        Assert.True(Wecker.Faellig(Samstag.AddHours(6).AddMinutes(46), SechsFuenfundvierzig, "taeglich", geweckt));
    }

    [Fact]
    public void WerktagsNichtAmWochenende()
    {
        Assert.True(Wecker.Faellig(Freitag.AddHours(6).AddMinutes(45), SechsFuenfundvierzig, "werktags", null));
        Assert.False(Wecker.Faellig(Samstag.AddHours(6).AddMinutes(45), SechsFuenfundvierzig, "werktags", null));
    }

    [Fact]
    public void NaechsterUeberspringtDasWochenende()
    {
        // Freitag 7:00 – der nächste Werktag ist Montag.
        var montag = Wecker.Naechster(Freitag.AddHours(7), SechsFuenfundvierzig, "werktags");
        Assert.Equal(new DateTime(2026, 10, 5, 6, 45, 0), montag);
        Assert.Equal("Mo 06:45", Wecker.Wann(montag));
        // Täglich: Samstag.
        Assert.Equal(Samstag.AddHours(6).AddMinutes(45), Wecker.Naechster(Freitag.AddHours(7), SechsFuenfundvierzig, "taeglich"));
        // Vor der Weckzeit: heute.
        Assert.Equal(Freitag.AddHours(6).AddMinutes(45), Wecker.Naechster(Freitag.AddHours(6), SechsFuenfundvierzig, "einmal"));
    }

    [Fact]
    public void LeiseAnfangen()
    {
        Assert.Equal(5, Wecker.Rampe(TimeSpan.Zero, 45));
        Assert.Equal(25, Wecker.Rampe(TimeSpan.FromMinutes(1), 45));
        Assert.Equal(45, Wecker.Rampe(TimeSpan.FromMinutes(5), 45));
        // Leiser eingestellt als der Anfang: gleich so leise.
        Assert.Equal(3, Wecker.Rampe(TimeSpan.Zero, 3));
    }

    [Fact]
    public void LeiseAufhoeren()
    {
        Assert.Equal(40, Wecker.Ausblenden(TimeSpan.FromMinutes(5), 40));
        Assert.Equal(20, Wecker.Ausblenden(TimeSpan.FromSeconds(30), 40));
        Assert.Equal(0, Wecker.Ausblenden(TimeSpan.Zero, 40));
    }

    [Fact]
    public void Abstaende()
    {
        Assert.Equal("gleich", Wecker.Abstand(TimeSpan.FromSeconds(20)));
        Assert.Equal("in 25 Min", Wecker.Abstand(TimeSpan.FromMinutes(24.5)));
        Assert.Equal("in 7 Std 12 Min", Wecker.Abstand(new TimeSpan(7, 12, 0)));
        Assert.Equal("in 2 Std", Wecker.Abstand(TimeSpan.FromHours(2)));
    }

    [Fact]
    public void AuftragWecktEineMinuteVorher()
    {
        var xml = Weckauftrag.Auftrag(@"C:\Users\x\AppData\Local\Programs\REGOradio\REGOradio.exe",
            new DateTime(2026, 10, 5, 6, 45, 0), "werktags");
        Assert.StartsWith("<?xml", xml);
        Assert.Contains("<StartBoundary>2026-10-05T06:44:00</StartBoundary>", xml);
        Assert.Contains("<WakeToRun>true</WakeToRun>", xml);
        Assert.Contains("<DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>", xml);
        Assert.Contains("<Friday />", xml);
        Assert.DoesNotContain("<Saturday />", xml);
        Assert.Contains("<Arguments>--tray</Arguments>", xml);
        // Lässt sich als XML lesen.
        System.Xml.Linq.XDocument.Parse(xml);
    }

    [Fact]
    public void EinmalIstEinZeitpunkt()
    {
        var xml = Weckauftrag.Auftrag(@"C:\R & D\REGOradio.exe", new DateTime(2026, 10, 3, 0, 0, 0), "einmal");
        Assert.Contains("<TimeTrigger><StartBoundary>2026-10-02T23:59:00</StartBoundary>", xml);
        Assert.Contains(@"C:\R &amp; D\REGOradio.exe", xml);
        System.Xml.Linq.XDocument.Parse(xml);
    }
}

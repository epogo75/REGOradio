using System.IO;
using System.Xml.Linq;

using Xunit;

namespace REGOradio.Pruefung;

/// <summary>
/// Haben alle Farbtafeln dieselben Schlüssel?
///
/// `App.Farbtafel` tauscht nur das Wörterbuch aus; die Stile greifen per
/// `DynamicResource` zu. Fehlt einer Tafel ein Schlüssel, meldet WPF keinen
/// Fehler -- die Fläche bleibt einfach durchsichtig, und das fällt erst auf,
/// wenn jemand genau dieses Thema bei Nacht einschaltet.
/// </summary>
public class ThemenPruefung
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string Themenordner()
    {
        var ordner = new DirectoryInfo(AppContext.BaseDirectory);
        while (ordner is not null && !File.Exists(Path.Combine(ordner.FullName, "REGOradio.sln")))
        {
            ordner = ordner.Parent;
        }
        Assert.NotNull(ordner);
        return Path.Combine(ordner.FullName, "src", "REGOradio", "Stil", "Themen");
    }

    private static SortedSet<string> Schluessel(string datei) =>
        new(XDocument.Load(datei).Root!.Elements()
            .Select(e => (string?)e.Attribute(X + "Key"))
            .OfType<string>());

    [Fact]
    public void JedesThemaHatTagUndNacht()
    {
        var dateien = Directory.GetFiles(Themenordner(), "*.xaml").Select(Path.GetFileNameWithoutExtension).ToHashSet();
        foreach (var thema in new[] { "Standard", "Holiday", "Mitternacht", "Neon", "Neongruen", "Neonblau" })
        {
            Assert.Contains($"{thema}-Tag", dateien);
            Assert.Contains($"{thema}-Nacht", dateien);
        }
    }

    [Fact]
    public void AlleTafelnHabenDieselbenSchluessel()
    {
        var dateien = Directory.GetFiles(Themenordner(), "*.xaml");
        var vorbild = Schluessel(Path.Combine(Themenordner(), "Standard-Tag.xaml"));
        Assert.Contains("Leuchten", vorbild);
        Assert.All(dateien, datei => Assert.Equal(vorbild, Schluessel(datei)));
    }
}

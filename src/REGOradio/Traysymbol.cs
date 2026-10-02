using System.Drawing;
using System.Windows;
using System.Windows.Forms;

using Anwendung = System.Windows.Application;

namespace REGOradio;

/// <summary>
/// Das Symbol im Benachrichtigungsfeld.
///
/// **Warum WinForms in einem WPF-Programm?** Weil Windows für das Tray nur
/// diesen Weg anbietet. Ein Paket dafür einzubinden hieße, eine Abhängigkeit für
/// ein Symbol zu holen; `UseWindowsForms` im Projekt kostet nichts, was hier
/// zählt.
///
/// **Das Symbol ist das des Programms** (`symbol.ico`, gezeichnet von
/// `werkzeug/symbol-zeichnen.ps1`). Windows sucht sich daraus die Größe, die
/// das Benachrichtigungsfeld gerade braucht -- bei 150 % Skalierung 24 statt
/// 16 Pixel. Die kleinen Größen liegen in der Datei als klassische Bitmaps,
/// weil `System.Drawing.Icon` PNG-Einträge nicht lesen kann.
/// </summary>
public sealed class Traysymbol : IDisposable
{
    private readonly NotifyIcon _symbol;
    private readonly Hauptfenster _fenster;

    public Traysymbol(Hauptfenster fenster)
    {
        _fenster = fenster;
        _symbol = new NotifyIcon
        {
            Icon = Zeichnen(),
            Visible = true,
            Text = "REGOradio",
        };

        var menue = new ContextMenuStrip();
        menue.Items.Add("Fenster zeigen", null, (_, _) => Zeigen());
        menue.Items.Add(new ToolStripSeparator());
        menue.Items.Add("Beenden", null, (_, _) => Beenden());
        _symbol.ContextMenuStrip = menue;

        // Doppelklick holt das Fenster zurück -- das erwartet man von einem
        // Tray-Symbol, und es ist der schnellere Weg als über das Menü.
        _symbol.DoubleClick += (_, _) => Zeigen();
    }

    /// <summary>Was im Tray steht, wenn man mit der Maus darauf zeigt.</summary>
    public void Beschriften(string text)
    {
        // Windows schneidet den Text hart bei 63 Zeichen ab und wirft bei
        // längeren in manchen Fassungen sogar. Deshalb hier kürzen.
        _symbol.Text = text.Length <= 63 ? text : text[..60] + "…";
    }

    /// <summary>Das Fenster zurückholen – auch, wenn ein zweiter Start darum bittet.</summary>
    public void Zeigen()
    {
        _fenster.Show();
        if (_fenster.WindowState == WindowState.Minimized) _fenster.WindowState = WindowState.Normal;
        _fenster.Activate();
    }

    private void Beenden()
    {
        _fenster.WirklichSchliessen();
        _symbol.Visible = false;
        Anwendung.Current.Shutdown();
    }

    private static Icon Zeichnen()
    {
        var quelle = Anwendung.GetResourceStream(new Uri("pack://application:,,,/symbol.ico"));
        using var strom = quelle.Stream;
        return new Icon(strom, SystemInformation.SmallIconSize);
    }

    public void Dispose()
    {
        _symbol.Visible = false;
        _symbol.Dispose();
    }
}

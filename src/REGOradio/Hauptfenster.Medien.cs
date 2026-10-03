using System.Windows;
using System.Windows.Interop;

using REGOradio.Katalog;
using REGOradio.Modelle;
using REGOradio.Ton;

namespace REGOradio;

/// <summary>
/// Medientasten, Bluetooth-Knöpfe und die Anzeige im Lautstärkefenster von
/// Windows (Bau 16). Die Anmeldung selbst steht in `Ton/Medientasten.cs`.
/// </summary>
public partial class Hauptfenster
{
    private Medientasten? _medien;

    /// <summary>
    /// Der Sender, der zuletzt lief – auch nach dem Stopp. „Spielen" auf der
    /// Medientaste braucht ihn: `_laufender` ist nach dem Stopp leer, und
    /// `LetzterPlatz` wird beim Stopp absichtlich auf 0 gesetzt.
    /// </summary>
    private Sender? _zuletzt;

    private void MedienAnmelden()
    {
        // EnsureHandle: Mit --tray wird das Fenster nie gezeigt, hat also noch
        // keinen Griff – Windows braucht aber eines, an dem es die Wiedergabe
        // festmacht.
        _medien = Medientasten.Anmelden(new WindowInteropHelper(this).EnsureHandle());
        if (_medien is null) return;
        _medien.Gedrueckt += befehl => Dispatcher.InvokeAsync(() => Medienbefehl(befehl));
        MedienZeigen();
    }

    private void Medienbefehl(Medienbefehl befehl)
    {
        switch (befehl)
        {
            case Ton.Medienbefehl.Spielen:
                if (_laufender is not null) return;
                var wieder = _zuletzt is not null && _sender.Contains(_zuletzt) ? _zuletzt
                    : _zuletzt is not null && _zuletzt.Platz == 0 ? _zuletzt
                    : Tastenbelegung.Nachbar(_sender, 0, 1);
                if (wieder is not null) Spielen(wieder);
                break;
            case Ton.Medienbefehl.Anhalten:
                if (_laufender is not null) Stoppen(this, new RoutedEventArgs());
                break;
            case Ton.Medienbefehl.Weiter:
            case Ton.Medienbefehl.Zurueck:
                var nachbar = Tastenbelegung.Nachbar(_sender, (_laufender ?? _zuletzt)?.Platz ?? 0,
                    befehl == Ton.Medienbefehl.Weiter ? 1 : -1);
                if (nachbar is not null) Spielen(nachbar);
                break;
        }
    }

    /// <summary>
    /// Den Stand an Windows melden. Aufgerufen, wo sich Sender, Titel oder
    /// Bild ändern; doppelte Meldungen fängt `Medientasten.Zeigen` ab.
    /// </summary>
    private void MedienZeigen()
    {
        if (_medien is null) return;
        if (_laufender is null)
        {
            _medien.Zeigen(false, "", "", "", "");
            return;
        }
        var (interpret, titel) = Cover.Zerlege(_titelGezeigt);
        var bild = _coverAdresse.Length > 0 ? _coverAdresse : _logos.Adresse(_laufender.Logo, _laufender.Homepage);
        _medien.Zeigen(_laeuft, _laufender.Name,
            _titelGezeigt.Length > 0 ? titel : "", _titelGezeigt.Length > 0 ? interpret : "", bild);
    }
}

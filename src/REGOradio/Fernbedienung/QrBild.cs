using System.IO;
using System.Windows.Media.Imaging;

using QRCoder;

namespace REGOradio.Fernbedienung;

/// <summary>
/// Der QR-Code, den das Handy abfotografiert: nur die Adresse, ohne PIN.
///
/// **Warum die PIN nicht mit hinein?** Dann wäre der QR-Code selbst der
/// Schlüssel -- und wer ihn einmal abfotografiert hat, bräuchte das Notebook
/// nie wieder zu sehen. So muss man die PIN vom Schirm ablesen, und eine neue
/// PIN sperrt alle Handys aus, die sie nicht kennen.
/// </summary>
public static class QrBild
{
    public static BitmapImage Zeichnen(string adresse)
    {
        using var erzeuger = new QRCodeGenerator();
        using var daten = erzeuger.CreateQrCode(adresse, QRCodeGenerator.ECCLevel.M);
        // Schwarz auf Weiß, auch im Nachtmodus: Handykameras lesen helle
        // Codes auf dunklem Grund oft gar nicht.
        var png = new PngByteQRCode(daten).GetGraphic(12);

        var bild = new BitmapImage();
        bild.BeginInit();
        bild.StreamSource = new MemoryStream(png);
        bild.CacheOption = BitmapCacheOption.OnLoad;
        bild.EndInit();
        bild.Freeze();
        return bild;
    }
}

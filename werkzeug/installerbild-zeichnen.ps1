# Zeichnet die Bilder fuer Begruessungs- und Abschlussseite des Installers
# (installer\willkommen-NNN.bmp) - mit dem Schriftzug von REGOradio.
#
# Aufruf, aus dem Repo-Wurzelverzeichnis:
#   powershell -NoProfile -ExecutionPolicy Bypass -File werkzeug\installerbild-zeichnen.ps1
#   ... -Png <ordner>    legt zusaetzlich PNGs zum Ansehen dort ab
#
# Wie symbol-zeichnen.ps1: Das Skript ist die Quelle, die .bmp sind sein
# Ergebnis, eingecheckt, damit liefern.ps1 ohne diesen Schritt auskommt.
#
# FUENF GROESSEN, EINE JE SKALIERUNG (100, 125, 150, 175, 200 %).
# Bau 13 hatte eines in 150 %, und das war "total grob und verpixelt": NSIS
# streckt das Bild mit LoadImage auf die Groesse des Bildfelds, und das
# rechnet nicht, es wirft Pixel weg oder verdoppelt sie. Dazu war der
# Installer nicht DPI-faehig, Windows zog also das ganze Fenster noch einmal
# unscharf auf. Jetzt meldet sich der Installer als DPI-faehig, und
# REGOradio.nsi (BildWaehlen) nimmt das Bild, das genau in das Feld passt -
# gestreckt wird dann hoechstens um ein Pixel.
#
# SYSTEM.DRAWING, NICHT WPF. Mit WPF waere der Schriftzug derselbe Code wie
# im Programm - aber RenderTargetBitmap liefert auf dem Baurechner aus der
# Shell nur leere Pixel, nicht einmal eine rote Flaeche (geprueft am
# 02.10.2026). Die Masse von Anzeige\Schriftzug.cs stehen deshalb hier noch
# einmal: Sperrung 0,06 der Schriftgroesse, "REGO" Light, "radio" Bold im
# Akzent; der Schein ist weichgezeichnet durch Verkleinern und Vergroessern.
# Wer den Schriftzug aendert, aendert ihn hier mit.

param([string]$Png)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Farben von Standard-Nacht (Stil\Themen\Standard-Nacht.xaml).
function Farbe([string]$hex, [int]$alpha = 255) {
    $c = [System.Drawing.ColorTranslator]::FromHtml($hex)
    [System.Drawing.Color]::FromArgb($alpha, $c.R, $c.G, $c.B)
}
$grund   = Farbe '#0E0F10'
$flaeche = Farbe '#17191A'
$tinte   = Farbe '#ECEAE6'
$tinte2  = Farbe '#A7A39A'
$akzent  = Farbe '#5FA790'

function Leinwand([int]$b, [int]$h) {
    $bild = New-Object System.Drawing.Bitmap $b, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bild)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.TextRenderingHint = 'AntiAlias'
    return $bild, $g
}

# Weichzeichnen durch Verkleinern und wieder Vergroessern. Zweimal, damit
# aus dem Treppchen der Verkleinerung ein runder Hof wird.
function Weich($bild, [double]$teiler) {
    $erg = $bild
    foreach ($runde in 1, 2) {
        $kb = [Math]::Max(1, [int]($bild.Width / $teiler)); $kh = [Math]::Max(1, [int]($bild.Height / $teiler))
        $klein, $gk = Leinwand $kb $kh
        $gk.DrawImage($erg, 0, 0, $kb, $kh); $gk.Dispose()
        $gross, $gg = Leinwand $bild.Width $bild.Height
        $gg.DrawImage($klein, 0, 0, $bild.Width, $bild.Height); $gg.Dispose()
        $klein.Dispose()
        $erg = $gross
    }
    $erg
}

function Deckend($ziel, $quelle) {
    $ziel.DrawImage($quelle, 0, 0, $quelle.Width, $quelle.Height)
}

function Zeichnen([int]$prozent) {
    # Gestaltet ist in 150 %; f rechnet jede Zahl auf die Zielgroesse um.
    $f = $prozent / 150.0
    # Das Bildfeld von MUI2 ist 109 x 193 Dialogeinheiten = 164 x 314 Pixel bei 100 %.
    $breite = [int][Math]::Round(164 * $prozent / 100.0)
    $hoehe  = [int][Math]::Round(314 * $prozent / 100.0)

    $bild, $g = Leinwand $breite $hoehe

    $verlauf = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Rectangle 0, 0, $breite, $hoehe), $flaeche, $grund, 90.0
    $g.FillRectangle($verlauf, 0, 0, $breite, $hoehe)

    # Das Gluehen oben rechts, wie im Blatt "Ueber REGOradio".
    $kreis = New-Object System.Drawing.Drawing2D.GraphicsPath
    $kreis.AddEllipse(($breite - 190 * $f), (-190 * $f), (380 * $f), (380 * $f))
    $glut = New-Object System.Drawing.Drawing2D.PathGradientBrush $kreis
    $glut.CenterColor = Farbe '#5FA790' 70
    $glut.SurroundColors = @((Farbe '#5FA790' 0))
    $g.FillPath($glut, $kreis)

    # ---- Der Schriftzug ------------------------------------------------------
    $gr = 34.0 * $f
    $x0 = 22.0 * $f; $y0 = 54.0 * $f
    $duenn = New-Object System.Drawing.Font 'Segoe UI Light', $gr, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $fett  = New-Object System.Drawing.Font 'Segoe UI', $gr, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $genau = [System.Drawing.StringFormat]::GenericTypographic

    $pinselTinte = New-Object System.Drawing.SolidBrush $tinte
    $x = $x0
    foreach ($zeichen in 'R', 'E', 'G', 'O') {
        $g.DrawString($zeichen, $duenn, $pinselTinte, $x, $y0, $genau)
        $x += $g.MeasureString($zeichen, $duenn, 1000, $genau).Width + $gr * 0.06
    }

    # "radio" erst auf eine eigene Lage, daraus zwei Scheine: weit und eng.
    $lage, $gl = Leinwand $breite $hoehe
    $gl.DrawString('radio', $fett, (New-Object System.Drawing.SolidBrush $akzent), $x, $y0, $genau)
    $gl.Dispose()
    Deckend $g (Weich $lage (6 * $f))
    Deckend $g (Weich $lage (3 * $f))
    Deckend $g $lage

    # ---- Untertitel ------------------------------------------------------------
    $klein = New-Object System.Drawing.Font 'Segoe UI', (17 * $f), ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $ue = [string][char]0xFC
    $g.DrawString("Internetradio`nf${ue}r den Finger", $klein, (New-Object System.Drawing.SolidBrush $tinte2), ($x0 + $f), ($y0 + $gr + 22 * $f))

    # ---- Unten eine Senderskala als leiser Abschluss ----------------------------
    $unten = $hoehe - 70 * $f
    for ($i = 0; $i -le 17; $i++) {
        $sx = (22 + $i * 12) * $f
        $lang = ($i % 4) -eq 0
        $stift = New-Object System.Drawing.Pen (Farbe '#ECEAE6' $(if ($lang) { 115 } else { 64 })), ($(if ($lang) { 2 } else { 1.4 }) * $f)
        $g.DrawLine($stift, $sx, ($unten + $(if ($lang) { 10 } else { 20 }) * $f), $sx, ($unten + 32 * $f))
    }
    $zeigerLage, $gz = Leinwand $breite $hoehe
    $zeigerStift = New-Object System.Drawing.Pen $akzent, (3 * $f)
    $zeigerStift.StartCap = 'Round'; $zeigerStift.EndCap = 'Round'
    $gz.DrawLine($zeigerStift, (166 * $f), $unten, (166 * $f), ($unten + 44 * $f)); $gz.Dispose()
    Deckend $g (Weich $zeigerLage (4 * $f))
    Deckend $g $zeigerLage

    $g.Dispose()

    # NSIS will ein BMP ohne Alphakanal: auf 24 Bit umkopieren.
    $fertig = New-Object System.Drawing.Bitmap $breite, $hoehe, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $gf = [System.Drawing.Graphics]::FromImage($fertig); $gf.DrawImage($bild, 0, 0, $breite, $hoehe); $gf.Dispose()
    $fertig
}

foreach ($prozent in 100, 125, 150, 175, 200) {
    $fertig = Zeichnen $prozent
    $ziel = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\installer\willkommen-$prozent.bmp"))
    $fertig.Save($ziel, [System.Drawing.Imaging.ImageFormat]::Bmp)
    "Gezeichnet: $ziel ($($fertig.Width) x $($fertig.Height))"
    if ($Png) { $fertig.Save((Join-Path $Png "willkommen-$prozent.png"), [System.Drawing.Imaging.ImageFormat]::Png) }
    $fertig.Dispose()
}

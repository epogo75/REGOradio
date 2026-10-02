# Zeichnet das Bild fuer Begruessungs- und Abschlussseite des Installers
# (installer\willkommen.bmp) - mit dem Schriftzug von REGOradio.
#
# Aufruf, aus dem Repo-Wurzelverzeichnis:
#   powershell -NoProfile -ExecutionPolicy Bypass -File werkzeug\installerbild-zeichnen.ps1
#   ... -Png <datei.png>    legt zusaetzlich ein PNG zum Ansehen ab
#
# Wie symbol-zeichnen.ps1: Das Skript ist die Quelle, die .bmp sein Ergebnis,
# eingecheckt, damit liefern.ps1 ohne diesen Schritt auskommt.
#
# SYSTEM.DRAWING, NICHT WPF. Mit WPF waere der Schriftzug derselbe Code wie
# im Programm - aber RenderTargetBitmap liefert auf dem Baurechner aus der
# Shell nur leere Pixel, nicht einmal eine rote Flaeche (geprueft am
# 02.10.2026). Die Masse von Anzeige\Schriftzug.cs stehen deshalb hier noch
# einmal: Sperrung 0,06 der Schriftgroesse, "REGO" Light, "radio" Bold im
# Akzent; der Schein ist weichgezeichnet durch Verkleinern und Vergroessern.
# Wer den Schriftzug aendert, aendert ihn hier mit.
#
# 246 x 471 PIXEL statt der ueblichen 164 x 314. NSIS zieht das Bild auf die
# Groesse der Seite; auf dem Dell mit 150 % Skalierung sind das genau 246 x
# 471. Ein Bild in Grundgroesse waere dort hochgezogen und unscharf.

param([string]$Png)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$breite = 246
$hoehe = 471

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
    $g.TextRenderingHint = 'AntiAliasGridFit'
    return $bild, $g
}

# Weichzeichnen durch Verkleinern und wieder Vergroessern. Zweimal, damit
# aus dem Treppchen der Verkleinerung ein runder Hof wird.
function Weich($bild, [int]$teiler) {
    $erg = $bild
    foreach ($runde in 1, 2) {
        $kb = [Math]::Max(1, [int]($erg.Width / $teiler)); $kh = [Math]::Max(1, [int]($erg.Height / $teiler))
        $klein, $gk = Leinwand $kb $kh
        $gk.DrawImage($erg, 0, 0, $kb, $kh); $gk.Dispose()
        $gross, $gg = Leinwand $bild.Width $bild.Height
        $gg.DrawImage($klein, 0, 0, $bild.Width, $bild.Height); $gg.Dispose()
        $klein.Dispose()
        $erg = $gross
    }
    $erg
}

$bild, $g = Leinwand $breite $hoehe

# Grund: von der Flaeche oben in den Grund unten.
$verlauf = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Rectangle 0, 0, $breite, $hoehe), $flaeche, $grund, 90.0
$g.FillRectangle($verlauf, 0, 0, $breite, $hoehe)

# Das Gluehen oben rechts, wie im Blatt "Ueber REGOradio".
$kreis = New-Object System.Drawing.Drawing2D.GraphicsPath
$kreis.AddEllipse(($breite - 190), -190, 380, 380)
$glut = New-Object System.Drawing.Drawing2D.PathGradientBrush $kreis
$glut.CenterColor = Farbe '#5FA790' 70
$glut.SurroundColors = @((Farbe '#5FA790' 0))
$g.FillPath($glut, $kreis)

# ---- Der Schriftzug ----------------------------------------------------------
$gr = 34.0                       # Schriftgroesse in Pixeln
$x0 = 22.0; $y0 = 54.0
$duenn = New-Object System.Drawing.Font 'Segoe UI Light', $gr, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
$fett  = New-Object System.Drawing.Font 'Segoe UI', $gr, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
$genau = [System.Drawing.StringFormat]::GenericTypographic
$genau.FormatFlags = $genau.FormatFlags -bor [System.Drawing.StringFormatFlags]::MeasureTrailingSpaces

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
$weit = Weich $lage 6
$eng  = Weich $lage 3

function Deckend($ziel, $quelle, [single]$deckung) {
    $m = New-Object System.Drawing.Imaging.ColorMatrix
    $m.Matrix33 = $deckung
    $attr = New-Object System.Drawing.Imaging.ImageAttributes
    $attr.SetColorMatrix($m)
    $ziel.DrawImage($quelle, (New-Object System.Drawing.Rectangle 0, 0, $quelle.Width, $quelle.Height),
        0, 0, $quelle.Width, $quelle.Height, ([System.Drawing.GraphicsUnit]::Pixel), $attr)
}
Deckend $g $weit 1.0
Deckend $g $eng 1.0
Deckend $g $lage 1.0

# ---- Untertitel -------------------------------------------------------------
$klein = New-Object System.Drawing.Font 'Segoe UI', 17, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
$ue = [string][char]0xFC
$g.DrawString("Internetradio`nf${ue}r den Finger", $klein, (New-Object System.Drawing.SolidBrush $tinte2), ($x0 + 1), ($y0 + $gr + 22))

# ---- Unten eine Senderskala als leiser Abschluss -----------------------------
$unten = $hoehe - 70
for ($i = 0; $i -le 17; $i++) {
    $sx = 22 + $i * 12
    $lang = ($i % 4) -eq 0
    $stift = New-Object System.Drawing.Pen (Farbe '#ECEAE6' $(if ($lang) { 115 } else { 64 })), $(if ($lang) { 2 } else { 1.4 })
    $g.DrawLine($stift, $sx, $(if ($lang) { $unten + 10 } else { $unten + 20 }), $sx, $unten + 32)
}
$zeigerLage, $gz = Leinwand $breite $hoehe
$zeigerStift = New-Object System.Drawing.Pen $akzent, 3
$zeigerStift.StartCap = 'Round'; $zeigerStift.EndCap = 'Round'
$gz.DrawLine($zeigerStift, 166, $unten, 166, $unten + 44); $gz.Dispose()
Deckend $g (Weich $zeigerLage 4) 1.0
Deckend $g $zeigerLage 1.0

$g.Dispose()

# NSIS will ein BMP ohne Alphakanal: auf 24 Bit umkopieren.
$fertig = New-Object System.Drawing.Bitmap $breite, $hoehe, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$gf = [System.Drawing.Graphics]::FromImage($fertig); $gf.DrawImage($bild, 0, 0, $breite, $hoehe); $gf.Dispose()

$ziel = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\installer\willkommen.bmp'))
$fertig.Save($ziel, [System.Drawing.Imaging.ImageFormat]::Bmp)
"Gezeichnet: $ziel ($breite x $hoehe)"
if ($Png) { $fertig.Save($Png, [System.Drawing.Imaging.ImageFormat]::Png); "Zum Ansehen: $Png" }

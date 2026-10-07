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
#
# SEIT BAU 28 IM REGO-STIL (Thema "REGO", wie regotools.de): nachtblauer
# Grund mit den vier Leuchtflecken der Webseite, oben die Bausteine, der
# Schriftzug in Outfit ExtraBold - "REGO" hell, "radio" violett, eng statt
# gesperrt -, unten die Senderskala mit dem Zeiger in Minze. Outfit kommt
# ueber eine PrivateFontCollection aus src\REGOradio\Schriften, damit das Bild
# ohne installierte Schrift entsteht.

param([string]$Png)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Farben von Rego-Nacht (Stil\Themen\Rego-Nacht.xaml).
function Farbe([string]$hex, [int]$alpha = 255) {
    $c = [System.Drawing.ColorTranslator]::FromHtml($hex)
    [System.Drawing.Color]::FromArgb($alpha, $c.R, $c.G, $c.B)
}
$grund   = Farbe '#0A0C14'
$flaeche = Farbe '#121729'
$tinte   = Farbe '#F1F3F9'
$tinte2  = Farbe '#A3A9BB'
$akzent  = Farbe '#00D9A3'
$violett = Farbe '#7C5CFF'

$schriften = New-Object System.Drawing.Text.PrivateFontCollection
foreach ($datei in 'Outfit-ExtraBold.ttf', 'Outfit-Regular.ttf') {
    $schriften.AddFontFile([System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\src\REGOradio\Schriften\$datei")))
}
$outfitFett = $schriften.Families | Where-Object { $_.Name -like '*ExtraBold*' } | Select-Object -First 1
$outfit     = $schriften.Families | Where-Object { $_.Name -eq 'Outfit' } | Select-Object -First 1
if (-not $outfitFett -or -not $outfit) { throw "Outfit nicht geladen: $(($schriften.Families | ForEach-Object Name) -join ', ')" }

# Ein weicher Leuchtfleck wie body::before auf regotools.de.
function Fleck($g, [double]$mx, [double]$my, [double]$r, [string]$hex, [int]$alpha) {
    $kreis = New-Object System.Drawing.Drawing2D.GraphicsPath
    $kreis.AddEllipse(($mx - $r), ($my - $r), (2 * $r), (2 * $r))
    $pinsel = New-Object System.Drawing.Drawing2D.PathGradientBrush $kreis
    $pinsel.CenterColor = Farbe $hex $alpha
    $pinsel.SurroundColors = @((Farbe $hex 0))
    $g.FillPath($pinsel, $kreis)
}

function Kachel($g, [double]$x, [double]$y, [double]$k, [double]$r, $farbe) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $k - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $k - $d, $y + $k - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $k - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush $farbe), $p)
}

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

    # Die vier Leuchtflecken der Webseite: Minze oben links, Violett oben
    # rechts, Koralle unten rechts, Gelb unten links.
    Fleck $g (0.20 * $breite) (0.24 * $hoehe) (0.75 * $breite) '#00D9A3' 60
    Fleck $g (0.85 * $breite) (0.30 * $hoehe) (0.80 * $breite) '#7C5CFF' 80
    Fleck $g (0.70 * $breite) (0.86 * $hoehe) (0.70 * $breite) '#FF6B6B' 40
    Fleck $g (0.24 * $breite) (0.80 * $hoehe) (0.65 * $breite) '#FFC93C' 30

    $x0 = 22.0 * $f

    # ---- Die Bausteine (viewBox 48 wie auf regotools.de) ---------------------
    $bk = 44.0 * $f / 48.0
    $bx = $x0; $by = 40.0 * $f
    Kachel $g ($bx + 3 * $bk) ($by + 3 * $bk) (19 * $bk) (6 * $bk) (Farbe '#00D9A3')
    Kachel $g ($bx + 26 * $bk) ($by + 3 * $bk) (19 * $bk) (6 * $bk) (Farbe '#FFC93C')
    Kachel $g ($bx + 3 * $bk) ($by + 26 * $bk) (19 * $bk) (6 * $bk) (Farbe '#FF6B6B')
    $g.FillEllipse((New-Object System.Drawing.SolidBrush (Farbe '#7C5CFF')), ($bx + 26 * $bk), ($by + 26 * $bk), (19 * $bk), (19 * $bk))

    # ---- Der Schriftzug ------------------------------------------------------
    $gr = 33.0 * $f
    $y0 = 104.0 * $f
    $fett  = New-Object System.Drawing.Font $outfitFett, $gr, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $genau = [System.Drawing.StringFormat]::GenericTypographic

    $g.DrawString('REGO', $fett, (New-Object System.Drawing.SolidBrush $tinte), $x0, $y0, $genau)
    $x = $x0 + $g.MeasureString('REGO', $fett, 1000, $genau).Width + $gr * 0.005

    # "radio" erst auf eine eigene Lage, daraus EIN enger Schein. Der weite
    # Hof des alten Bilds machte das kraeftige Violett auf dem dunklen Grund
    # zu einem verschwommenen Fleck.
    $lage, $gl = Leinwand $breite $hoehe
    $gl.TextRenderingHint = 'AntiAlias'
    $gl.DrawString('radio', $fett, (New-Object System.Drawing.SolidBrush $violett), $x, $y0, $genau)
    $gl.Dispose()
    Deckend $g (Weich $lage (2.5 * $f))
    Deckend $g $lage

    # ---- Untertitel ------------------------------------------------------------
    $klein = New-Object System.Drawing.Font $outfit, (17 * $f), ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
    $ue = [string][char]0xFC
    $g.DrawString("Internetradio`nf${ue}r den Finger", $klein, (New-Object System.Drawing.SolidBrush $tinte2), ($x0 + $f), ($y0 + $gr + 22 * $f))

    # ---- Unten eine Senderskala als leiser Abschluss ----------------------------
    $unten = $hoehe - 70 * $f
    for ($i = 0; $i -le 17; $i++) {
        $sx = (22 + $i * 12) * $f
        $lang = ($i % 4) -eq 0
        $stift = New-Object System.Drawing.Pen (Farbe '#F1F3F9' $(if ($lang) { 115 } else { 64 })), ($(if ($lang) { 2 } else { 1.4 }) * $f)
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

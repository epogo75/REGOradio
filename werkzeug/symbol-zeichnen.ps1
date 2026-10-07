# Zeichnet das Programmsymbol von REGOradio und legt es als .ico ab.
#
# Aufruf, aus dem Repo-Wurzelverzeichnis:
#   powershell -NoProfile -ExecutionPolicy Bypass -File werkzeug\symbol-zeichnen.ps1
#
# WARUM EIN SKRIPT UND KEINE GEZEICHNETE DATEI? Eine .ico ist eine Binärdatei,
# die im Repo niemand ansehen oder ändern kann. Dieses Skript ist die Quelle;
# die .ico daneben ist nur sein Ergebnis und wird mit eingecheckt, damit der
# Bau ohne diesen Schritt auskommt.
#
# Der Entwurf stand im Mockup (Tafel „Programmsymbol"): runde Ecken, zwei
# Funkbögen, ein Punkt. Unter 32 Pixeln fällt der äußere Bogen weg -- bei 16
# Pixeln laufen zwei Bögen zu einem grauen Fleck zusammen.
#
# SEIT BAU 28 IM REGO-STIL (Stephan: „das icon und der installer an die neue
# designsprache"): dunkle Kachel wie REGOglt, der Punkt in Minze, der innere
# Bogen violett, der äußere in Koralle - die Farben der Bausteine von
# regotools.de. Vorher grüner Grund mit weißer Zeichnung.
#
# Jede Größe wird einzeln gezeichnet statt aus 256 Pixeln verkleinert:
# Verkleinerte Symbole werden matschig, gerade die Strichstärke.

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$ziel = Join-Path $PSScriptRoot '..\src\REGOradio\symbol.ico'
$akzent = [System.Drawing.Color]::FromArgb(255, 0x16, 0x1B, 0x2E)   # Kachel #161B2E wie REGOglt
$minze = [System.Drawing.Color]::FromArgb(255, 0x00, 0xD9, 0xA3)    # Punkt
$violett = [System.Drawing.Color]::FromArgb(255, 0x7C, 0x5C, 0xFF)  # innerer Bogen
$koralle = [System.Drawing.Color]::FromArgb(255, 0xFF, 0x6B, 0x6B)  # äußerer Bogen

function Bild([int]$groesse) {
    $bild = New-Object System.Drawing.Bitmap $groesse, $groesse
    $g = [System.Drawing.Graphics]::FromImage($bild)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # Alles in Einheiten von 256 gedacht, auf die Zielgröße umgerechnet.
    $m = $groesse / 256.0

    # Grund: Quadrat mit runden Ecken (Radius 56 von 256).
    $r = 56 * $m
    $pfad = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $s = $groesse - 1
    $pfad.AddArc(0, 0, $d, $d, 180, 90)
    $pfad.AddArc($s - $d, 0, $d, $d, 270, 90)
    $pfad.AddArc($s - $d, $s - $d, $d, $d, 0, 90)
    $pfad.AddArc(0, $s - $d, $d, $d, 90, 90)
    $pfad.CloseFigure()
    $pinsel = New-Object System.Drawing.SolidBrush $akzent
    $g.FillPath($pinsel, $pfad)

    # Mittelpunkt der Bögen = der Punkt unten links.
    $mx = 76 * $m
    $my = 180 * $m
    $r1 = 60 * $m     # innerer Bogen
    $pr = 22 * $m     # Punkt
    $strich = 22 * $m

    # EIGENE MASSE BIS 24 PIXEL. Maßstäblich verkleinert ist der innere Bogen
    # bei 16 Pixeln keine vier Pixel weit, und Punkt und Bogen laufen zu einem
    # Fleck zusammen -- so sah der erste Wurf aus. Hier sitzt der Punkt
    # weiter in der Ecke, der Bogen ist weiter, der Strich verhältnismäßig
    # dicker.
    if ($groesse -le 24) {
        $mx = 0.28 * $groesse
        $my = 0.72 * $groesse
        $r1 = 0.44 * $groesse
        $pr = 0.12 * $groesse
        $strich = 0.13 * $groesse
    } elseif ($groesse -le 32) {
        $strich = 3.2
    }
    function Stift($farbe) {
        $p = New-Object System.Drawing.Pen $farbe, $strich
        $p.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $p.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $p
    }

    # Innerer Bogen.
    $g.DrawArc((Stift $violett), $mx - $r1, $my - $r1, 2 * $r1, 2 * $r1, 270, 90)

    # Äußerer Bogen, Radius 112 -- erst ab 32 Pixeln.
    if ($groesse -ge 32) {
        $r2 = 112 * $m
        $g.DrawArc((Stift $koralle), $mx - $r2, $my - $r2, 2 * $r2, 2 * $r2, 270, 90)
    }

    # Der Punkt.
    $punktPinsel = New-Object System.Drawing.SolidBrush $minze
    $g.FillEllipse($punktPinsel, $mx - $pr, $my - $pr, 2 * $pr, 2 * $pr)

    $g.Dispose()
    return $bild
}

# Ein ICO-Eintrag als klassische 32-Bit-Bitmap: Kopf, Bildpunkte von UNTEN
# nach oben (so will es das Format), dann eine leere Transparenzmaske -- die
# Transparenz steckt schon im Alphakanal.
function Dib($bild) {
    $b = $bild.Width
    $h = $bild.Height
    $rahmen = New-Object System.Drawing.Rectangle 0, 0, $b, $h
    $daten = $bild.LockBits($rahmen, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
                            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $zeile = $daten.Stride
    $roh = New-Object byte[] ($zeile * $h)
    [System.Runtime.InteropServices.Marshal]::Copy($daten.Scan0, $roh, 0, $roh.Length)
    $bild.UnlockBits($daten)

    $strom = New-Object System.IO.MemoryStream
    $s = New-Object System.IO.BinaryWriter $strom
    $s.Write([UInt32]40); $s.Write([Int32]$b); $s.Write([Int32](2 * $h))   # Höhe doppelt: Bild + Maske
    $s.Write([UInt16]1); $s.Write([UInt16]32); $s.Write([UInt32]0)
    $s.Write([UInt32]($b * $h * 4)); $s.Write([Int32]0); $s.Write([Int32]0)
    $s.Write([UInt32]0); $s.Write([UInt32]0)
    for ($y = $h - 1; $y -ge 0; $y--) { $s.Write($roh, $y * $zeile, $b * 4) }
    $maskenzeile = [int][math]::Floor(($b + 31) / 32) * 4
    $s.Write((New-Object byte[] ($maskenzeile * $h)))
    $s.Flush()
    return , $strom.ToArray()
}

# KLEINE GRÖSSEN ALS BITMAP, GROSSE ALS PNG. PNG im ICO ist seit Windows Vista
# erlaubt und viel kleiner -- aber nicht jeder Leser kann es: Das alte
# System.Drawing.Icon liest einen PNG-Eintrag als Rohbitmap und zeigt
# Farbrauschen. Genau daran wäre das Tray-Symbol gescheitert. Deshalb bis 64
# Pixel die klassische Form, die jeder versteht; 128 und 256 als PNG, wie es
# Microsoft empfiehlt -- dort wären Bitmaps ein Viertel Megabyte.
$groessen = 16, 20, 24, 32, 40, 48, 64, 128, 256
$pngs = foreach ($groesse in $groessen) {
    $bild = Bild $groesse
    if ($groesse -ge 128) {
        $strom = New-Object System.IO.MemoryStream
        $bild.Save($strom, [System.Drawing.Imaging.ImageFormat]::Png)
        $eintrag = $strom.ToArray()
    } else {
        $eintrag = Dib $bild
    }
    $bild.Dispose()
    , $eintrag
}

$aus = New-Object System.IO.MemoryStream
$schreiber = New-Object System.IO.BinaryWriter $aus
$schreiber.Write([UInt16]0)                 # reserviert
$schreiber.Write([UInt16]1)                 # Typ: Symbol
$schreiber.Write([UInt16]$groessen.Count)   # Anzahl Bilder

$versatz = 6 + 16 * $groessen.Count
for ($i = 0; $i -lt $groessen.Count; $i++) {
    $groesse = $groessen[$i]
    # 256 wird im Verzeichnis als 0 geschrieben -- das Feld hat nur ein Byte.
    $kante = if ($groesse -ge 256) { 0 } else { $groesse }
    $schreiber.Write([byte]$kante)
    $schreiber.Write([byte]$kante)
    $schreiber.Write([byte]0)               # keine Palette
    $schreiber.Write([byte]0)               # reserviert
    $schreiber.Write([UInt16]1)             # Farbebenen
    $schreiber.Write([UInt16]32)            # Bit je Pixel
    $schreiber.Write([UInt32]$pngs[$i].Length)
    $schreiber.Write([UInt32]$versatz)
    $versatz += $pngs[$i].Length
}
foreach ($png in $pngs) { $schreiber.Write($png) }
$schreiber.Flush()

[System.IO.File]::WriteAllBytes((Resolve-Path (Split-Path $ziel)).Path + '\symbol.ico', $aus.ToArray())
# Nur ASCII in der Ausgabe: Windows PowerShell 5.1 liest ein Skript ohne BOM
# als ANSI, und aus einem Umlaut hier würde „Ã¶". Die Kommentare stört das
# nicht, sie werden nie ausgegeben.
Write-Output "Symbol geschrieben: $ziel ($($aus.Length) Bytes, Pixel: $($groessen -join ', '))"

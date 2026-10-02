<#
.SYNOPSIS
  Prueft, baut, packt den Installer und spiegelt auf die NAS - in dieser
  Reihenfolge, und nur, wenn jeder Schritt gelingt.

.DESCRIPTION
  Nach dem Vorbild von REGOdj (liefern.ps1, installer.ps1, spiegeln.ps1 in
  einem). Dort fuehrte das Aufrufen der Einzelschritte von Hand einmal dazu,
  dass ein Installer die Nummer des neuen Baus und den Inhalt des alten trug.
  Deshalb bricht hier die ganze Kette beim ersten Fehler ab.

  EIGENSTAENDIG, ABER NICHT GEBUENDELT. Die Laufzeit liegt als Ordner neben
  der .exe. Eine gebuendelte Einzeldatei entpackt sich beim Start selbst nach
  Temp; genau das hat Defender am 03.09.2026 an anderen REGO-Werkzeugen als
  Behavior:Win32/DefenseEvasion.A!ml geloescht, und Smart App Control blockt
  unsignierte Einzeldateien ohnehin. NSIS dagegen ist ein natives Programm.

  Ergebnis:
      dist\                               das Programm
      dist-installer\REGOradio-Setup-NN.exe
      Z:\REGOradio\installer\             Setup, mit zwei Vorgaengern
      Z:\REGOradio\programm\              der entpackte Stand
      Z:\REGOradio\quellcode\             der Quelltext ohne bin/obj/.git
#>
[CmdletBinding()]
param(
  # Nur bauen und packen, nicht auf die NAS spiegeln.
  [switch]$OhneSpiegel,

  [string]$Ziel = 'Z:\REGOradio',

  # Wie viele aeltere Installer auf der NAS stehen bleiben.
  [int]$Vorgaenger = 2
)

$ErrorActionPreference = 'Stop'
$wurzel = $PSScriptRoot

$dotnetDir = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
if (Test-Path (Join-Path $dotnetDir 'dotnet.exe')) { $env:Path = "$dotnetDir;$env:Path" }

# Auf dem Baurechner ist nur die .NET-10-Laufzeit installiert; die Pruefungen
# (net9) laufen dann auf ihr. Das Programm selbst bringt seine Laufzeit mit.
$env:DOTNET_ROLL_FORWARD = 'Major'

function Abschnitt([string]$name) {
  Write-Host ""
  Write-Host "=== $name" -ForegroundColor Cyan
}

# ---- Baunummer -------------------------------------------------------------
# Aus Version.cs, der einzigen Quelle dafuer. Sie gehoert auf die Datei:
# zwei Installer, die gleich heissen und Verschiedenes tragen, sind schlimmer
# als gar keiner.
$version = [System.IO.File]::ReadAllText((Join-Path $wurzel 'src\REGOradio\Version.cs'))
if ($version -notmatch 'Nummer\s*=\s*(\d+)') { throw "Baunummer in Version.cs nicht gefunden." }
$bau = $Matches[1]
if ($version -notmatch 'Version\s*=\s*"([\d.]+)"') { throw "Version in Version.cs nicht gefunden." }
$fassung = $Matches[1]
Write-Host "REGOradio $fassung, Bau $bau" -ForegroundColor Green

# ---- 1. Pruefungen ---------------------------------------------------------
Abschnitt 'Pruefungen'
& dotnet test (Join-Path $wurzel 'REGOradio.sln') -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Pruefungen fehlgeschlagen - nichts ausgeliefert." }

# ---- 2. Veroeffentlichen ---------------------------------------------------
Abschnitt 'Veroeffentlichen'
$dist = Join-Path $wurzel 'dist'

# dotnet publish ueberschreibt, raeumt aber nicht auf. Eine alte DLL neben
# einer neuen ist die unangenehmste Sorte Fehler.
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }

& dotnet publish (Join-Path $wurzel 'src\REGOradio\REGOradio.csproj') `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=false -p:DebugType=none `
  -o $dist -v q --nologo
if ($LASTEXITCODE -ne 0) { throw "Veroeffentlichen fehlgeschlagen." }

$exe = Join-Path $dist 'REGOradio.exe'
if (-not (Test-Path $exe)) { throw "In dist\ liegt kein REGOradio.exe." }

# ---- 3. Installer ----------------------------------------------------------
Abschnitt 'Installer'

# DAS SKRIPT BRAUCHT EIN BOM. Ohne liest makensis es trotz "Unicode true" als
# ANSI, und aus "läuft" wird "lÃ¤uft" - in REGOdj zweimal passiert.
$nsi = Join-Path $wurzel 'installer\REGOradio.nsi'
$kopf = [System.IO.File]::ReadAllBytes($nsi)
if (-not ($kopf.Length -ge 3 -and $kopf[0] -eq 0xEF -and $kopf[1] -eq 0xBB -and $kopf[2] -eq 0xBF)) {
  throw "installer\REGOradio.nsi hat kein UTF-8-BOM - die Umlaute waeren im Installer kaputt."
}

$nsis = $null
foreach ($k in @("$env:ProgramFiles\NSIS\makensis.exe", "${env:ProgramFiles(x86)}\NSIS\makensis.exe")) {
  if (Test-Path $k) { $nsis = $k; break }
}
if (-not $nsis) { throw "makensis.exe nicht gefunden. NSIS installieren: winget install NSIS.NSIS" }

New-Item -ItemType Directory -Force (Join-Path $wurzel 'dist-installer') | Out-Null
& $nsis /V2 "/DBAUNUMMER=$bau" "/DFASSUNG=$fassung" "/DQUELLE=$dist" $nsi
if ($LASTEXITCODE -ne 0) { throw "makensis fehlgeschlagen." }

$setup = Join-Path $wurzel "dist-installer\REGOradio-Setup-$bau.exe"
Write-Host ("{0}  ({1:N0} MB)" -f $setup, ((Get-Item $setup).Length / 1MB)) -ForegroundColor Green

if ($OhneSpiegel) { Write-Host "`nFertig, ohne Spiegel." -ForegroundColor Green; return }

# ---- 4. Spiegeln -----------------------------------------------------------
Abschnitt 'Spiegeln'
if (-not (Test-Path (Split-Path $Ziel -Qualifier))) {
  throw "$Ziel ist nicht erreichbar - haengt das Netzlaufwerk?"
}
foreach ($teil in 'installer', 'programm', 'quellcode') {
  New-Item -ItemType Directory -Force (Join-Path $Ziel $teil) | Out-Null
}

Copy-Item $setup (Join-Path $Ziel 'installer') -Force
Write-Host "Installer:  REGOradio-Setup-$bau.exe" -ForegroundColor Green

# Sortiert nach der ZAHL im Namen, nicht nach dem Zeitstempel: neu kopieren
# macht eine Datei juenger, ohne sie neuer zu machen.
Get-ChildItem (Join-Path $Ziel 'installer') -File -Filter 'REGOradio-Setup-*.exe' |
  Where-Object { $_.Name -match 'REGOradio-Setup-(\d+)\.exe' } |
  Sort-Object { [int]([regex]::Match($_.Name, '(\d+)\.exe').Groups[1].Value) } -Descending |
  Select-Object -Skip (1 + [Math]::Max(0, $Vorgaenger)) |
  ForEach-Object {
    Remove-Item $_.FullName -Force
    Write-Host "  weggeraeumt: $($_.Name)" -ForegroundColor DarkGray
  }

# Robocopy meldet ueber den Rueckgabewert, was es getan hat: unter 8 ist Erfolg.
& robocopy $dist (Join-Path $Ziel 'programm') /MIR /NFL /NDL /NJH /NJS /NP /R:1 /W:1 | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy nach programm fehlgeschlagen ($LASTEXITCODE)." }
Write-Host "Programm:   $Ziel\programm" -ForegroundColor Green

& robocopy $wurzel (Join-Path $Ziel 'quellcode') /MIR /NFL /NDL /NJH /NJS /NP /R:1 /W:1 `
  /XD bin obj .git .vs dist dist-installer TestResults publish release | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy nach quellcode fehlgeschlagen ($LASTEXITCODE)." }
Write-Host "Quellcode:  $Ziel\quellcode" -ForegroundColor Green

# robocopy hinterlaesst seinen Rueckgabewert; der Aufrufer soll 0 sehen.
$global:LASTEXITCODE = 0
Write-Host "`nBau $bau steht auf $Ziel." -ForegroundColor Cyan

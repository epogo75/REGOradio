; REGOradio — Installationsprogramm (NSIS)
;
; Nach dem Vorbild von REGOdj. Was er tut, steht hier ausdrücklich:
;
;   - er installiert JE BENUTZER, nach %LOCALAPPDATA%\Programs\REGOradio.
;     Kein Administrator, kein Eingriff in Program Files, keine Dienste.
;     Das ist derselbe Ordner, den die README für die alte Einzeldatei
;     vorschlägt: wer sie dort liegen hat, bekommt sie ersetzt, und ein
;     eingeschalteter Autostart zeigt weiter auf die richtige Datei.
;   - er legt zwei Verknüpfungen an, Startmenü und Schreibtisch.
;   - er trägt sich in die Programmliste ein und bringt einen Uninstaller mit.
;   - er RICHTET KEINEN AUTOSTART EIN. Den schaltet man im Programm ein.
;   - er lässt beim Entfernen die Ablage stehen: Stationstasten und
;     Einstellungen unter %APPDATA%\REGOradio.
;
; Gebaut wird er mit liefern.ps1; Baunummer und Quellordner kommen von dort.

Unicode true
SetCompressor /SOLID lzma

!ifndef BAUNUMMER
  !define BAUNUMMER "0"
!endif
!ifndef FASSUNG
  !define FASSUNG "0.1"
!endif
!ifndef QUELLE
  !define QUELLE "..\dist"
!endif

!define NAME    "REGOradio"
!define FIRMA   "Stephan Ruf"
!define SCHLUESSEL "Software\Microsoft\Windows\CurrentVersion\Uninstall\REGOradio"

; Derselbe Name wie in Einzelstart.cs: solange es diese Marke gibt, läuft
; REGOradio -- auch dann, wenn es nur im Tray sitzt und kein Fenster hat.
; Nach dem Fenstertitel zu suchen (wie in REGOdj) fände es dort nicht
; zuverlässig.
!define MARKE "Local\REGOradio-einmal"

Name "${NAME} ${FASSUNG} (Bau ${BAUNUMMER})"
OutFile "..\dist-installer\REGOradio-Setup-${BAUNUMMER}.exe"
BrandingText "${NAME}"

RequestExecutionLevel user
InstallDir "$LOCALAPPDATA\Programs\REGOradio"
InstallDirRegKey HKCU "${SCHLUESSEL}" "InstallLocation"

VIProductVersion "${FASSUNG}.0.${BAUNUMMER}"
VIAddVersionKey "ProductName"     "${NAME}"
VIAddVersionKey "CompanyName"     "${FIRMA}"
VIAddVersionKey "LegalCopyright"  "© 2026 ${FIRMA}"
VIAddVersionKey "FileDescription" "${NAME} einrichten"
VIAddVersionKey "FileVersion"     "${FASSUNG}.0.${BAUNUMMER}"

!include "MUI2.nsh"
!include "FileFunc.nsh"

!define MUI_ABORTWARNING
!define MUI_ICON   "..\src\REGOradio\symbol.ico"
!define MUI_UNICON "..\src\REGOradio\symbol.ico"

; DPI-FÄHIG. Ohne das zieht Windows bei 150 % das ganze Fenster als Bild
; auf, und alles darin wird unscharf – gemeldet für Bau 13 als „total grob
; und verpixelt".
ManifestDPIAware true

; Der Schriftzug links auf Begrüßungs- und Abschlussseite (seit Bau 13).
; Gezeichnet von werkzeug\installerbild-zeichnen.ps1 in fünf Größen;
; BildWaehlen legt vor jeder der beiden Seiten das passende hin. MUI2 streckt
; das Bild sonst mit LoadImage, und das wirft Pixel weg, statt zu rechnen.
!define MUI_WELCOMEFINISHPAGE_BITMAP "willkommen-100.bmp"

!define MUI_WELCOMEPAGE_TITLE "${NAME} einrichten"
!define MUI_WELCOMEPAGE_TEXT  "Internetradio für Windows, mit großen Tasten für den Finger: Sender suchen, auf eine Stationstaste legen, hören. Der Ton geht an jedes Windows-Audiogerät, auch an eine gekoppelte Bluetooth-Box.$\r$\n$\r$\nInstalliert wird für den angemeldeten Benutzer, ohne Administratorrechte. Die .NET-Laufzeit kommt mit; es muss nichts weiter installiert sein.$\r$\n$\r$\nStationstasten und Einstellungen bleiben bei einem Update erhalten."

!define MUI_PAGE_CUSTOMFUNCTION_PRE BildWaehlen
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES

!define MUI_PAGE_CUSTOMFUNCTION_PRE BildWaehlen
!define MUI_FINISHPAGE_RUN "$INSTDIR\REGOradio.exe"
!define MUI_FINISHPAGE_RUN_TEXT "${NAME} jetzt starten"
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "German"

; Das Bild für die Skalierung des Bildschirms. MUI2 hat beim Start schon die
; 100-%-Fassung nach $PLUGINSDIR gelegt; hier wird sie überschrieben, bevor
; die Seite sie lädt. Gewählt wird die nächstkleinere Stufe, damit eher um
; ein Pixel gestreckt als gestaucht wird.
Function BildWaehlen
  System::Call 'user32::GetDpiForWindow(p$HWNDPARENT)i.r0'
  ${If} $0 <= 0
    ; Vor Windows 10: die Auflösung des Bildschirms.
    System::Call 'user32::GetDC(p0)p.r1'
    System::Call 'gdi32::GetDeviceCaps(pr1,i90)i.r0'
    System::Call 'user32::ReleaseDC(p0,pr1)'
  ${EndIf}
  ${If} $0 >= 192
    File "/oname=$PLUGINSDIR\modern-wizard.bmp" "willkommen-200.bmp"
  ${ElseIf} $0 >= 168
    File "/oname=$PLUGINSDIR\modern-wizard.bmp" "willkommen-175.bmp"
  ${ElseIf} $0 >= 144
    File "/oname=$PLUGINSDIR\modern-wizard.bmp" "willkommen-150.bmp"
  ${ElseIf} $0 >= 120
    File "/oname=$PLUGINSDIR\modern-wizard.bmp" "willkommen-125.bmp"
  ${EndIf}
FunctionEnd

; Läuft REGOradio? Ergebnis in $0: 0 = nein. SYNCHRONIZE (0x00100000) reicht,
; um eine fremde Marke zu öffnen.
!macro LaeuftEs
  System::Call 'kernel32::OpenMutexW(i 0x00100000, i 0, w "${MARKE}") p .r0'
  StrCmp $0 0 +2
    System::Call 'kernel32::CloseHandle(p r0)'
!macroend

Section "Programm" Hauptteil
  SectionIn RO

  ; Über eine laufende .exe zu schreiben scheitert mitten im Kopieren und
  ; lässt einen halben Stand zurück.
  pruefen:
  !insertmacro LaeuftEs
  StrCmp $0 0 weiter
    MessageBox MB_ICONEXCLAMATION|MB_RETRYCANCEL \
      "REGOradio läuft gerade, vielleicht nur im Tray.$\r$\n$\r$\nBitte über das Tray-Symbol beenden und dann auf Wiederholen." \
      /SD IDCANCEL IDRETRY pruefen
    Abort "Abgebrochen, solange das Programm läuft."
  weiter:

  SetOutPath "$INSTDIR"
  SetOverwrite on

  ; Die alte Einzeldatei aus der Zeit vor dem Installer ist so groß wie das
  ; ganze Programm und würde sonst neben dem neuen Stand liegen bleiben.
  ; REGOradio.exe selbst wird ohnehin überschrieben.
  Delete "$INSTDIR\REGOradio-x64.exe"
  Delete "$INSTDIR\REGOradio-x64-eigenstaendig.exe"

  ; Der ganze veröffentlichte Ordner. Die Laufzeit liegt darin -- deshalb ist
  ; er groß, und deshalb läuft er ohne installiertes .NET.
  File /r "${QUELLE}\*.*"

  CreateShortCut "$SMPROGRAMS\${NAME}.lnk" "$INSTDIR\REGOradio.exe" "" \
                 "$INSTDIR\REGOradio.exe" 0
  CreateShortCut "$DESKTOP\${NAME}.lnk" "$INSTDIR\REGOradio.exe" "" \
                 "$INSTDIR\REGOradio.exe" 0

  WriteUninstaller "$INSTDIR\Entfernen.exe"

  ; WAR DER AUTOSTART AN, ZEIGT ER JETZT HIERHER. Er kann noch auf die alte
  ; Einzeldatei zeigen, die gerade gelöscht wurde -- dann startete das Radio
  ; nach der nächsten Anmeldung still nicht mehr. Ist er aus, bleibt er aus.
  ReadRegStr $1 HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "REGOradio"
  StrCmp $1 "" +2
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "REGOradio" "$\"$INSTDIR\REGOradio.exe$\" --tray"

  WriteRegStr   HKCU "${SCHLUESSEL}" "DisplayName"     "${NAME}"
  WriteRegStr   HKCU "${SCHLUESSEL}" "DisplayVersion"  "${FASSUNG}.${BAUNUMMER}"
  WriteRegStr   HKCU "${SCHLUESSEL}" "Publisher"       "${FIRMA}"
  WriteRegStr   HKCU "${SCHLUESSEL}" "DisplayIcon"     "$INSTDIR\REGOradio.exe"
  WriteRegStr   HKCU "${SCHLUESSEL}" "InstallLocation" "$INSTDIR"
  WriteRegStr   HKCU "${SCHLUESSEL}" "UninstallString" "$\"$INSTDIR\Entfernen.exe$\""
  WriteRegDWORD HKCU "${SCHLUESSEL}" "NoModify" 1
  WriteRegDWORD HKCU "${SCHLUESSEL}" "NoRepair" 1

  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKCU "${SCHLUESSEL}" "EstimatedSize" "$0"
SectionEnd

Section "Uninstall"
  unpruefen:
  !insertmacro LaeuftEs
  StrCmp $0 0 unweiter
    MessageBox MB_ICONEXCLAMATION|MB_RETRYCANCEL \
      "REGOradio läuft gerade, vielleicht nur im Tray.$\r$\n$\r$\nBitte über das Tray-Symbol beenden und dann auf Wiederholen." \
      /SD IDCANCEL IDRETRY unpruefen
    Abort "Abgebrochen, solange das Programm läuft."
  unweiter:

  Delete "$SMPROGRAMS\${NAME}.lnk"
  Delete "$DESKTOP\${NAME}.lnk"

  ; Der Autostart-Eintrag zeigt nach dem Entfernen ins Leere; Windows meldete
  ; dann bei jeder Anmeldung eine fehlende Datei. Speicher/Autostart.cs legt
  ; ihn unter genau diesem Namen an.
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "REGOradio"

  RMDir /r "$INSTDIR"
  DeleteRegKey HKCU "${SCHLUESSEL}"

  ; Die Firewall-Regel der Handy-Fernbedienung bleibt: sie zu löschen braucht
  ; Administratorrechte, und ohne Programm dahinter lässt sie nichts herein.
  MessageBox MB_ICONINFORMATION|MB_OK \
    "${NAME} wurde entfernt.$\r$\n$\r$\nERHALTEN GEBLIEBEN sind Stationstasten und Einstellungen:$\r$\n$APPDATA\REGOradio" \
    /SD IDOK
SectionEnd

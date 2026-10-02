# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Projekt

REGOradio ist ein Internetradio für Windows, gebaut für ein Notebook mit Touchscreen im Hotelzimmer: acht große Stationstasten plus „Mehr“ mit 24 Plätzen (dort per Ziehen umzuordnen), Sendersuche über radio-browser.info, ein großer Lautstärkeregler, Titel und Cover des laufenden Lieds, Ausgabe auf Notebook-Lautsprecher oder eine gekoppelte Bluetooth-Box, sechs Farbthemen je Tag und Nacht, Bildschirmhelligkeit, Wortuhr und „Jetzt läuft" im Vollbild, Handy als Fernbedienung (QR-Code und PIN), Tray-Symbol.

Code, Kommentare, Bezeichner und Commit-Nachrichten sind **deutsch**. Kommentare begründen Entscheidungen, oft mit dem Fehler, der dazu geführt hat. Diesen Stil beibehalten.

Der Entwurf der Oberfläche liegt als Mockup vor: https://claude.ai/artifact/EaBRh4ML5UR3jZqtnkqdg6 (Hauptschirm Tag/Nacht, Einstellungen, Sendersuche, Programmsymbol). Die WPF-Oberfläche folgt ihm; wer sie grundlegend ändert, gleicht das Mockup mit an.

## Befehle

.NET 9 SDK, WPF (`net9.0-windows`). Aus dem Repo-Wurzelverzeichnis:

```bash
dotnet build REGOradio.sln
dotnet test REGOradio.sln
dotnet test REGOradio.sln --filter "FullyQualifiedName~WlanPruefung"      # eine Klasse
dotnet test REGOradio.sln --filter "FullyQualifiedName~WlanPruefung.GetrenntIstNichtVerbunden"
dotnet run --project src/REGOradio/REGOradio.csproj
powershell -NoProfile -ExecutionPolicy Bypass -File liefern.ps1                # prüfen, bauen, Installer, nach Z:\REGOradio
powershell -NoProfile -ExecutionPolicy Bypass -File liefern.ps1 -OhneSpiegel   # ohne NAS
powershell -NoProfile -ExecutionPolicy Bypass -File werkzeug\symbol-zeichnen.ps1    # symbol.ico neu zeichnen
```

Ausgeliefert wird nur über `liefern.ps1`: eigenständig, aber **nicht** als Einzeldatei nach `dist\`, dann NSIS (`installer\REGOradio.nsi` → `dist-installer\REGOradio-Setup-NN.exe`, NN aus `Version.cs`), dann Spiegel nach `Z:\REGOradio\{installer,programm,quellcode}` mit zwei Vorgängern. Die `.nsi` muss UTF-8 mit BOM bleiben. Der Installer erkennt ein laufendes REGOradio an derselben Mutex-Marke wie `Einzelstart.cs` (`Local\REGOradio-einmal`) – wer die umbenennt, muss beide ändern. „Fertig“ heißt: gepusht und auf `Z:` gespiegelt.

Läuft das Programm noch, scheitert der Bau am gesperrten `REGOradio.exe`; vorher `taskkill /IM REGOradio.exe /F`. Das Fenster schließen reicht nicht, es lebt im Tray weiter.

Auf einem Rechner mit nur der .NET-10-Laufzeit (dem Baurechner) brechen `dotnet test` und der Start mit „framework 9.0.0 not found" ab. Dann vorher `$env:DOTNET_ROLL_FORWARD = 'Major'` setzen; das Projekt bleibt bei .NET 9.

`Bau.Nummer` in `src/REGOradio/Version.cs` wird bei jeder ausgelieferten Änderung erhöht, `Stand` bekommt das Datum. Commit-Titel: `Bau NN: <was sich ändert>`.

## Architektur

**Ton**
- `Ton/Abspieler.cs`: ein einziger Wiedergabeweg. `MediaFoundationReader` (NAudio, also Windows Media Foundation) öffnet die Adresse selbst und dekodiert MP3, AAC, HE-AAC, HLS, FLAC, Ogg, WMA; ausgegeben wird über `WasapiOut` auf das gewählte Gerät. Das Öffnen läuft in einem eigenen Faden. Eine frühere Weiche (MP3 selbst dekodiert, um ICY-Titel zu lesen) wurde bewusst entfernt. `Stumm` ist eine eigene Schranke neben der Lautstärke und wird nicht gespeichert.
- `Ton/Titelwache.cs`: Weil Media Foundation die ICY-Metadaten nicht herausgibt, fragt die Titelwache den Sender alle 10 s über eine eigene, kurze Verbindung nach dem Titel (bis zum ersten Metadatenblock lesen, auflegen) – bewusst keine zweite Dauerverbindung. Sender ohne `icy-metaint` und HLS werden nicht weiter gefragt. Das Lesen hat eine eigene 10-s-Frist, weil `HttpClient.Timeout` nach dem Kopf nicht mehr greift. `Ton/IcyStrom.cs` schneidet die Blöcke heraus und behandelt die Zeichensatzfallen (Latin-1, doppelt kodiertes UTF-8).
- `Ton/Ausgaenge.cs`: Audiogeräte über `MMDeviceEnumerator`. Bluetooth wird am Aufzähler `BTHENUM`/`BTHLE` in den Geräteeigenschaften erkannt, nicht am Namen. Gekoppelt wird in Windows (`ms-settings:bluetooth`), nicht im Programm.
- `Ton/Wachhalter.cs`: `SetThreadExecutionState`, damit das Notebook bei laufendem Radio nicht einschläft (nur System, nicht Bildschirm). Der Zustand hängt am aufrufenden Faden, deshalb nur aus dem Oberflächenfaden setzen (`Hauptfenster.TonstandZeigen`).

**Netz, Katalog, Speicher**
- `Netz/Wlan.cs`: WLAN-Name und Signal über `cmd /c chcp 65001>nul & netsh wlan show interfaces`, als UTF-8 gelesen. Ohne `chcp` käme OEM-Zeichensatz, und Umlaute im Netznamen gingen kaputt. `AusAusgabe` ist eine reine Funktion, geprüft mit Aufzeichnungen unter `tests/REGOradio.Pruefung/antworten/`.
- `Katalog/Radiobrowser.cs`: radio-browser.info mit Spiegelwahl über DNS, User-Agent aus `Bau.Kennung`, `url_resolved` statt .pls/.m3u.
- `Katalog/Logos.cs`: Senderlogos mit Zwischenspeicher unter `%APPDATA%\REGOradio\logos`. Nur echte Bilder (Inhaltstyp `image/*`, kein SVG); viele Logoadressen im Verzeichnis sind tot oder leiten auf eine HTML-Seite um, das ergibt `null`.
- `Katalog/Cover.cs`: Cover zum laufenden Lied über die iTunes-Suche. Gesucht wird nur bei „Interpret - Titel"; alles andere (Durchsagen, Mailadressen des Senders) geht nicht nach draußen. Findet der genaue Begriff nichts, folgt ein zweiter Anlauf ohne Klammerzusatz („(Bandcamp Version)“).
- `Katalog/Monogramm.cs`: Kürzel und Farbe für Sender ohne Logo. Die Farbe wird aus den Buchstaben gerechnet, nicht aus `string.GetHashCode` (der wechselt bei jedem Start).
- `Speicher/Ablage.cs`: `stationen.json` und `einstellungen.json` unter `%APPDATA%\REGOradio`, geschrieben über `.neu`-Datei und Ersetzen. `Speicher/Autostart.cs`: „Mit Windows starten" steht im Run-Schlüssel unter HKCU (mit `--tray`), und **nur dort**; die Einstellungsdatei hat dafür kein Feld, weil der Eintrag auch im Task-Manager abgeschaltet werden kann.

**Handy-Fernbedienung** (`Fernbedienung/`)
- `Dienst.cs`: Kestrel (ASP.NET Core, `FrameworkReference`) lauscht auf Port 8765 an allen Adressen – ohne Administratorrechte, anders als `HttpListener`. Läuft nur, wenn „Handy als Fernbedienung“ an ist (`Einstellungen.FernAn`, Vorgabe aus). Die Seite `Handyseite.html` ist als Ressource in die .exe eingebettet (eine Datei, kein Bauschritt, keine Fremdquellen). Endpunkte: `POST /api/anmelden`, dann mit Cookie `GET /api/stand`, `GET /api/sender`, `POST /api/spielen/{platz}`, `/api/stopp`, `/api/lautstaerke/{wert}`, `/api/stumm/{bool}`, `/api/helligkeit/{wert}`, `/api/ansicht/{bedienung|cover|uhr}` (was der Notebook-Bildschirm zeigt; `cover` ohne laufenden Sender gibt 409). Auf der Handyseite steht der Umschalter oben, die Helligkeit liegt in einem Blatt hinter dem Zahnrad. Im Vollbild (Cover, Uhr) ist der Mauszeiger über `Mouse.OverrideCursor` ausgeblendet, und offene Tooltips werden geschlossen (`TooltipsSchliessen`), sonst blieben sie über dem Vollbild stehen.
- Die Schnittstelle `IFernsteuerbar` ist bewusst schmal (Sender wählen, Lautstärke, Stumm, Stopp, Helligkeit, Ansicht am Notebook) und wird vom `Hauptfenster` erfüllt; jeder Aufruf geht über `Dispatcher.Invoke`, weil er aus einem Kestrel-Faden kommt.
- `Zugang.cs`: vierstellige PIN; nach 5 Fehlversuchen je Absender eine Minute Sperre. Das Handy bekommt als Cookie ein HMAC aus einem gespeicherten Schlüssel und der PIN – übersteht Neustarts, eine neue PIN meldet alle Handys ab.
- `Adresse.cs` wählt die Netzadresse für den QR-Code (Karte mit Gateway, WLAN vor Kabel, keine vEthernet/WSL/Hyper-V). Der QR-Code (`QrBild.cs`, QRCoder, MIT) enthält nur die Adresse, nicht die PIN.
- `Firewall.cs`: Beim ersten Lauschen fragt Windows einmal; wer dort „Ablehnen“ drückt, bekommt eine Sperrregel und wird nie wieder gefragt. Deshalb liest REGOradio beim Einschalten des Schalters die Regeln für die eigene .exe (COM `HNetCfg.FwPolicy2`) und richtet bei Sperre oder fehlender Freigabe per UAC eine Freigabe nur für TCP 8765 in allen Netzarten ein (`netsh`, vorher alle Regeln der .exe löschen). Im Blatt steht dann auch der Knopf „Firewall-Freigabe einrichten“. Regeln hängen am Pfad der .exe: Der Debug-Bau braucht eine eigene.
- Cookies gelten je Adresse: Wer mit `curl` über `127.0.0.1` anmeldet, ist über die LAN-Adresse nicht angemeldet.

**Oberfläche**
- WPF ohne MVVM-Rahmen, Code-Behind. `Hauptfenster.xaml` hat vier Ebenen im selben Fenster: Hauptschirm (Kopfzeile, 3×3 Felder = acht Sender plus „Mehr“, rechts das Feld „Läuft" mit Lied, Cover und Lautstärkesäule), das Blatt „Alle Sender“ (24 Plätze, Suchen, Umordnen), das Blatt „Ton geht an“ und das Einstellungsblatt liegen zusammen in einer **festen Fläche von 1280×720 in einem `Viewbox`** – das Fenster skaliert sie als Ganzes, damit beim Ziehen am Fensterrand nichts gestaucht wird. Darüber, über das ganze Fenster: „Jetzt läuft" (Cover groß, verschwommen als Hintergrund) und die Wortuhr. Beide schalten mit `VollbildAn`/`VollbildAus` randlos über die Taskleiste. Die Sendersuche ist noch ein eigenes Dialogfenster (`Suchfenster`).
- `Uhr/Wortuhr.cs`: Raster und Wortstellen, geprüft Wort für Wort (`WortuhrPruefung`). Koordinaten sind die Falle: Eine Spalte daneben ergibt „FÜNF ACH".
- `Modelle/Tastenbelegung.cs`: Ziehen einer Taste auf eine andere **tauscht** die beiden (nicht einschieben mit Nachrücken). Gezogen wird **nur im Blatt „Alle Sender“**, nicht auf dem Hauptschirm, damit ein Tipp dort nie versehentlich umsortiert. Das Ziehen beginnt erst nach 24 Punkten Weg, sonst würde am Touchscreen jeder Tipp zum Ziehversuch. Langes Drücken ist Rechtsklick und nimmt einen Sender von der Taste.
- Kopfzeile: links nur drei Symboltasten (WLAN, Ton, Einstellungen), rechts die Uhr (Tipp: Wortuhr). WLAN öffnet `ms-availablenetworks:` (Netzliste von Windows), sonst `ms-settings:network-wifi`; ohne Verbindung wird das Symbol rot, der Netzname steht im Hinweis. Ton öffnet das Blatt „Ton geht an“ (Geräteliste, Bluetooth koppeln); sein Symbol zeigt Lautsprecher oder Bluetooth, je nach gewähltem Gerät.
- Farbthemen: `Stil/Themen/<Thema>-Tag.xaml` und `-Nacht.xaml` (Standard, Holiday, Mitternacht, Neon, Neongruen, Neonblau), alle mit denselben Schlüsseln – `ThemenPruefung` wacht darüber. `App.Themen` ist die Liste (Schlüssel = Dateiname), `App.Farbtafel(thema, nacht)` tauscht das erste zusammengeführte Wörterbuch, alle Stile greifen per `DynamicResource` darauf zu. Der Schlüssel `Leuchten` ist ein Effekt für die laufende Taste: `x:Null`, nur in den Neon-Nachttafeln ein Schein in der Akzentfarbe. Gewählt wird in einer Aufklappliste (Stil `Auswahl`). Darstellung „Automatisch" (21–7 Uhr dunkel) prüft der Uhrtakt in `Hauptfenster.DarstellungAnwenden`.
- `Anzeige/Helligkeit.cs`: Bildschirmhelligkeit über WMI (`root\wmi`, `WmiMonitorBrightness`, `System.Management`), nur am eingebauten Bildschirm; sonst fehlt die Zeile. **Gelesen, nie gespeichert** – Windows ändert sie selbst. Nie unter 5 %. Der Regler reagiert erst, wenn der Stand gelesen ist: Beim Aufbau feuert `ValueChanged` schon, und im ersten Bau hat das den Bildschirm auf 5 % gedreht.
- Senderlogos liegen auf ihrer eigenen Eckfarbe (`Hauptfenster.Logogrund`, auch in Cover-Kachel und „Jetzt läuft“), nur Logos mit durchsichtigen Ecken auf Weiß. Vorher gab es um jedes Logo einen weißen Ring, der nachts grell durchschien.
- `Stil/Stile.xaml`: Tastenrundung über die Ressource `Tastenradius`, die ein Stil in `Style.Resources` überschreiben kann. Die Lautstärkesäule (`Lautstaerkebalken`) ist ein senkrechter Slider mit eigener Vorlage: Füllung und Griff sind Rechtecke, die Rundung kommt von einer `VisualBrush`-Maske (`ClipToBounds` schneidet nur eckig). Minimum liegt unten, **ohne** `IsDirectionReversed`. +/− sind Wiederholtasten mit Einzelschritten; gespeichert wird die Lautstärke erst, wenn sie 600 ms ruht.
- Symbole in der Oberfläche sind `Path`-Strichzeichnungen im 24er-Raster (`Window.Resources`), die über den Stil `Symbol` die Schriftfarbe ihrer Taste übernehmen.
- Programmsymbol: `werkzeug/symbol-zeichnen.ps1` zeichnet `src/REGOradio/symbol.ico`. Bis 64 px klassische 32-Bit-Bitmaps, 128/256 als PNG: `System.Drawing.Icon` (und damit das Tray-Symbol) kann PNG-Einträge nicht lesen. Unter 24 px eigene Maße, sonst laufen Punkt und Bogen zusammen.
- Tray: `Traysymbol.cs` über `System.Windows.Forms.NotifyIcon`. `ShutdownMode="OnExplicitShutdown"`: Was beim Schließen des Fensters passiert, entscheidet `Schliessregel` (Bau 11): fragen (Vorgabe), in den Tray oder beenden. Gefragt wird im Blatt `Schliessebene` (letzte Ebene in der skalierten Fläche, vorher wird ein Vollbild verlassen); „Nicht mehr fragen" schreibt `SchliessenFragen = false` und `ImTrayBleiben`. In den Einstellungen sind es drei Felder statt zweier Schalter – die Spalte hat keinen Rollbalken. `WirklichSchliessen` läuft nur einmal (`_beendet`): Tray-Menü, Herunterfahren (`App.OnSessionEnding`) und Rückfrage führen alle dorthin, und `Shutdown` schließt das Fenster danach noch einmal – ohne die Sperre würde dann gefragt.
- „Über REGOradio" (Bau 12) ist das Blatt `Ueberebene` (vor `Schliessebene`), geöffnet aus dem Kopf der Einstellungen und aus dem Tray-Menü (`Hauptfenster.UeberZeigen`). Das Symbol ist die größte Fassung aus `symbol.ico` (`GroessteFassung`; ein `Image` direkt auf die .ico nähme eine kleine). Die Liste „Von anderen" ist die Kurzform von `THIRD-PARTY-NOTICES.md` – beide zusammen ändern. Den Kopfverlauf setzt der Code beim Öffnen, damit er dem aktuellen Thema folgt.
- Nur eine Instanz: `Einzelstart.cs` (benannter Mutex `Local\REGOradio-einmal`, Weckereignis `Local\REGOradio-wecken`). Ein zweiter Start weckt den ersten, der über `Traysymbol.Zeigen` sein Fenster zeigt, und beendet sich; mit `--tray` (Autostart) weckt er nicht. Debug- und Release-Bau sperren sich gegenseitig.
- Rahmen im Thema: `Anzeige/Fensterkleid.cs` färbt Titelleiste, Titelschrift und Rand per DWM aus `Grund`, `Tinte`, `Linie` (Klassen-Handler auf `Window.Loaded`, nach `App.Farbtafel` erneut über `Fensterkleid.Alle()`). Dasselbe Mittel wie REGOdj `Ui/Fensterkleid.cs`. Nur Windows 11; DWM lässt die Farben setzen, aber nicht zurücklesen – prüfen lässt es sich nur mit Blick auf den Bildschirm.

## Feste Regeln

- **Eine Lautstärke**, gespeichert in den Einstellungen, gesetzt am Abspieler (`WasapiOut.Volume`), **nie vom Gerät zurückgelesen**.
- **Beim Senderwechsel stoppen, nie pausieren.** Ein pausierter Abspieler hält das Audiogerät offen.
- **Keine GPL/LGPL-Bibliotheken.** NAudio ist MIT; libVLC ist ausgeschlossen.
- **Prüfungen greifen nicht ins Netz** und fragen keine Hardware. Reine Funktionen werden mit aufgezeichneten Ausgaben geprüft; wer eine Aufzeichnung nachstellt statt abliest, schreibt das in die Prüfklasse.
- **XML-Kommentare** in `.xaml` und `.csproj` dürfen kein `--` enthalten, sonst bricht der Bau mit MC3000 ab. Als Gedankenstrich dort `–` verwenden; in C# ist `--` im Kommentar erlaubt.
- `UseWindowsForms` setzt sonst `System.Windows.Forms` und `System.Drawing` als globale usings und macht `Button`, `MessageBox`, `Application` doppeldeutig. In der `.csproj` per `<Using Remove=… />` entfernt; WinForms nur ausdrücklich in `Traysymbol.cs`.
- In WPF-Projekten ist `System.IO` kein automatisches `using`; bei `File`/`Path` ausdrücklich einbinden. `Path` ist in `.xaml.cs` zudem doppeldeutig mit `System.Windows.Shapes.Path`.
- PowerShell-Skripte unter `werkzeug/` werden von Windows PowerShell 5.1 ohne BOM als ANSI gelesen: Ausgaben dort nur in ASCII.

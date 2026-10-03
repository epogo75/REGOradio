# REGOradio

Internetradio für Windows. Große Tasten, für den Finger gemacht: Sender suchen,
auf eine Stationstaste legen, hören. Der Ton geht an jedes Windows-Audiogerät,
auch an eine gekoppelte Bluetooth-Box.

Das Programm legt sich ins Benachrichtigungsfeld (Tray) und läuft dort weiter,
wenn das Fenster zu ist.

## Stand

Bau 24 läuft auf einem Dell 7320 mit Touchscreen unter Windows 11:

* acht Stationstasten mit Senderlogos, dazu „Mehr“ mit 24 Plätzen, dort per Ziehen umzuordnen
* Sendersuche im Verzeichnis, „Anhören" und „Auf Taste legen" getrennt
* spielt MP3, AAC, HE-AAC, HLS, FLAC und Ogg über Windows selbst
* Titel und Cover des laufenden Lieds, „Jetzt läuft" im Vollbild
* große Lautstärkesäule, Einzelschritte, Stumm
* Kopfzeile mit Schriftzug links und Uhr rechts; die drei Symboltasten stehen links untereinander: WLAN (Sprung ins WLAN-Menü), Ton (Notebook oder Bluetooth-Box), Einstellungen (seit Bau 14)
* Tag, Nacht oder automatisch, dazu sechs Farbthemen (Standard, Holiday, Mitternacht, Neon Pink/Grün/Blau); Bildschirmhelligkeit; Wortuhr im Vollbild
* Tray, Autostart, kein Ruhezustand während Radio läuft
* läuft nur einmal: ein zweiter Start holt das Fenster des laufenden nach vorn (seit Bau 11)
* beim Schließen kurz gefragt: in den Tray oder ganz beenden – oder fest eingestellt (seit Bau 11)
* Titelleiste und Fensterrand im gewählten Farbthema, wie in REGOdj (seit Bau 11, Windows 11)
* „Über REGOradio“ in den Einstellungen und im Tray-Menü: Bau, Ablageordner, was von anderen drinsteckt (seit Bau 12)
* im Feld „Läuft“ wischen: nach links der nächste, nach rechts der vorige Sender (seit Bau 23)
* Hotel-WLAN: verlangt das WLAN eine Anmeldung im Browser, sagt REGOradio das, bietet „WLAN-Anmeldung öffnen“ an und spielt nach der Anmeldung von selbst weiter (seit Bau 22)
* alle Sender gleich laut: die Lautheit jedes Senders wird beim Hören nach EBU R 128 gemessen, am Sender gemerkt und auf −17 LUFS angeglichen (höchstens −12/+6 dB, langsam nachgeführt); abschaltbar im Blatt „Ton geht an“ (seit Bau 21)
* Einschlafen (15–90 Minuten, die letzte Minute wird leiser, danach geht auf Wunsch auch der Bildschirm aus – seit Bau 24) und Wecker (Uhrzeit, täglich/Mo–Fr/einmal, Stationstaste; fängt leise an und wird in zwei Minuten lauter). Schläft der Rechner, weckt ihn eine geplante Aufgabe von Windows eine Minute vorher – soweit die Energieoptionen das zulassen (seit Bau 20)
* Stationstasten sichern und einlesen, mit Logos, als eine Datei `*.regoradio` – unter „Mehr“; beim Einlesen werden die bisherigen Tasten ersetzt und vorher selbst gesichert (seit Bau 19)
* „Was lief vorhin?“: die letzten 50 Lieder mit Uhrzeit, Cover und Sender, auch nach einem Neustart; vierte Taste links (seit Bau 18)
* tote Sender repariert es selbst: reißt der Strom ab, wird einmal neu verbunden; spielt ein Sender gar nicht, sucht REGOradio im Verzeichnis seine neue Adresse und legt sie auf die Taste, sobald sie spielt (seit Bau 17)
* Medientasten der Tastatur und Knöpfe der Bluetooth-Box: Play startet den letzten Sender, Pause/Stopp hält an, Weiter/Zurück gehen zur nächsten Stationstaste; Sender, Titel und Cover im Lautstärkefenster von Windows und auf dem Sperrbildschirm (seit Bau 16)
* Senderlogos auch für Sender, die im Verzeichnis keines haben: aus dem Kopf ihrer Homepage, sonst von DuckDuckGo (seit Bau 15)
* Schriftzug wie bei REGOdj: „REGO“ dünn, „radio“ leuchtend im Akzent des Farbthemas – in der Kopfzeile, in „Über“ und im Installer (seit Bau 13)
* Handy als Fernbedienung: QR-Code abfotografieren, vierstellige PIN, dann Sender, Lautstärke, Stumm und Stopp vom Handy; dazu umschalten, was das Notebook zeigt (Bedienung, Cover, Uhr), und hinter dem Zahnrad die Helligkeit

## Installieren

Seit Bau 11 gibt es einen Installer: **`REGOradio-Setup-NN.exe`**, auf der NAS unter `Z:\REGOradio\installer`. Er installiert je Benutzer nach `%LOCALAPPDATA%\Programs\REGOradio`, ohne Administratorrechte, bringt die .NET-Laufzeit mit und legt Verknüpfungen in Startmenü und auf dem Schreibtisch an. Ein Update ist derselbe Installer noch einmal; läuft REGOradio dabei (auch nur im Tray), bittet er erst ums Beenden.

* Eine alte Einzeldatei (`REGOradio-x64*.exe`) im selben Ordner räumt er weg; ein eingeschalteter Autostart zeigt danach auf die neue Datei.
* Beim Entfernen bleiben Stationstasten und Einstellungen unter `%APPDATA%\REGOradio` stehen; der Autostart-Eintrag geht mit weg.
* Installer und Programm sind nicht signiert, deshalb warnt Windows SmartScreen beim ersten Start („Weitere Informationen“ → „Trotzdem ausführen“).

Die beiden Einzeldateien unter [Releases](https://github.com/epogo75/REGOradio/releases) stammen aus der Zeit davor.

Wer die Handy-Fernbedienung einschaltet, wird von der Windows-Firewall gefragt. Wer dort versehentlich ablehnt, schaltet den Schalter aus und wieder an: REGOradio richtet die Freigabe dann selbst ein.

## Bauen

```
dotnet build REGOradio.sln
dotnet test REGOradio.sln
dotnet publish src/REGOradio/REGOradio.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

Braucht das .NET-9-SDK.

Ausliefern (Prüfungen, eigenständig nach `dist\`, NSIS-Installer nach `dist-installer\`, Spiegel nach `Z:\REGOradio\{installer,programm,quellcode}`), bricht beim ersten Fehler ab:

```
powershell -NoProfile -ExecutionPolicy Bypass -File liefern.ps1               # mit Spiegel
powershell -NoProfile -ExecutionPolicy Bypass -File liefern.ps1 -OhneSpiegel  # nur bauen
```

Braucht NSIS (`winget install NSIS.NSIS`). `installer\REGOradio.nsi` muss UTF-8 **mit** BOM bleiben, sonst kommen die Umlaute im Installer kaputt an; `liefern.ps1` prüft das. Gebündelt wird absichtlich nicht: eine .NET-Einzeldatei entpackt sich beim Start nach Temp, und genau das hat Defender an anderen REGO-Werkzeugen gelöscht.

## Grundsätze

* **Eine Lautstärke.** Die Zahl im Programm ist die Wahrheit, sie wird gesetzt
  und nie vom Gerät zurückgelesen. Wer an der Box selbst leiser dreht, bewegt
  den Regler im Fenster nicht. Sonst gäbe es zwei Fassungen derselben Zahl, und
  eine davon lügt.
* **Beim Senderwechsel wird gestoppt, nicht pausiert.** Ein pausierter
  Abspieler hält das Audiogerät offen.
* **Keine GPL- oder LGPL-Bibliotheken.** Der Ton läuft über
  [NAudio](https://github.com/naudio/NAudio) (MIT).
* **Das Senderverzeichnis** ist [radio-browser.info](https://www.radio-browser.info/),
  ein freier Dienst. Er bittet um eine erkennbare Kennung; die schickt
  `Katalog/Radiobrowser.cs` mit.

## Was Windows übernimmt

**Bluetooth-Boxen koppelt Windows, nicht dieses Programm.** Eine gekoppelte Box
ist ein Audiogerät wie jedes andere und steht in der Ausgangsliste. Deshalb gibt
es hier nichts von dem, was am Pi den halben Aufwand ausmacht — kein BlueZ, kein
A2DP, kein AVRCP. Ist noch keine Box gekoppelt, führt ein Knopf in die
Windows-Einstellungen.

## Lizenz

MIT, siehe [`LICENSE`](LICENSE). Fremde Bestandteile und ihre Lizenzen stehen in
[`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

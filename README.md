# REGOradio

Internetradio für Windows. Große Tasten, für den Finger gemacht: Sender suchen,
auf eine Stationstaste legen, hören. Der Ton geht an jedes Windows-Audiogerät,
auch an eine gekoppelte Bluetooth-Box.

Das Programm legt sich ins Benachrichtigungsfeld (Tray) und läuft dort weiter,
wenn das Fenster zu ist.

## Stand

Bau 10 läuft auf einem Dell 7320 mit Touchscreen unter Windows 11:

* acht Stationstasten mit Senderlogos, dazu „Mehr“ mit 24 Plätzen, dort per Ziehen umzuordnen
* Sendersuche im Verzeichnis, „Anhören" und „Auf Taste legen" getrennt
* spielt MP3, AAC, HE-AAC, HLS, FLAC und Ogg über Windows selbst
* Titel und Cover des laufenden Lieds, „Jetzt läuft" im Vollbild
* große Lautstärkesäule, Einzelschritte, Stumm
* Kopfzeile nur mit Symbolen: WLAN (Sprung ins WLAN-Menü), Ton (Notebook oder Bluetooth-Box), Einstellungen; die Uhr rechts
* Tag, Nacht oder automatisch, dazu sechs Farbthemen (Standard, Holiday, Mitternacht, Neon Pink/Grün/Blau); Bildschirmhelligkeit; Wortuhr im Vollbild
* Tray, Autostart, kein Ruhezustand während Radio läuft
* Handy als Fernbedienung: QR-Code abfotografieren, vierstellige PIN, dann Sender, Lautstärke, Stumm und Stopp vom Handy; dazu umschalten, was das Notebook zeigt (Bedienung, Cover, Uhr), und hinter dem Zahnrad die Helligkeit

## Installieren

Unter [Releases](https://github.com/epogo75/REGOradio/releases) liegen zwei Fassungen:

* **`REGOradio-x64-eigenstaendig.exe`**: läuft ohne Weiteres auf Windows 10/11 (64 Bit), bringt .NET mit und ist entsprechend groß.
* **`REGOradio-x64.exe`**: klein, braucht aber die [.NET-9-Laufzeiten](https://dotnet.microsoft.com/download/dotnet/9.0) „Desktop Runtime“ **und** „ASP.NET Core Runtime“ (x64). Ohne die zweite startet das Programm nicht, weil die Handy-Fernbedienung darauf aufsetzt.

Die .exe irgendwohin legen, zum Beispiel nach `%LOCALAPPDATA%\Programs\REGOradio`, und starten. Eine Installation gibt es nicht; Einstellungen und Sender liegen unter `%APPDATA%\REGOradio`. Die Datei ist nicht signiert, deshalb warnt Windows SmartScreen beim ersten Start („Weitere Informationen“ → „Trotzdem ausführen“).

Wer die Handy-Fernbedienung einschaltet, wird von der Windows-Firewall gefragt. Wer dort versehentlich ablehnt, schaltet den Schalter aus und wieder an: REGOradio richtet die Freigabe dann selbst ein.

## Bauen

```
dotnet build REGOradio.sln
dotnet test REGOradio.sln
dotnet publish src/REGOradio/REGOradio.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

Braucht das .NET-9-SDK. Die eigenständige Fassung entsteht mit `--self-contained true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true`.

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

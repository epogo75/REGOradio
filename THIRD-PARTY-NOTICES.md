# Fremde Bestandteile

REGOradio selbst steht unter der MIT-Lizenz (`LICENSE`). In der ausgelieferten
`REGOradio.exe` stecken außerdem diese Bibliotheken, alle ebenfalls unter MIT:

| Bestandteil | Lizenz | Quelle |
|---|---|---|
| NAudio 2.2.1 (NAudio.Core, .Wasapi, .WinMM, .Asio, .Midi, .WinForms) | MIT, © Mark Heath & Contributors | https://github.com/naudio/NAudio |
| QRCoder 1.6.0 | MIT, © Raffael Herrmann | https://github.com/codebude/QRCoder |
| System.Management, System.CodeDom, Microsoft.Win32.*, System.Drawing.Common, System.Security.* | MIT, © .NET Foundation and Contributors | https://github.com/dotnet/runtime |
| .NET, WPF, ASP.NET Core (Kestrel) | MIT, © .NET Foundation and Contributors | https://github.com/dotnet |

Das Zahnrad-Symbol der Handyseite folgt dem Symbol „settings“ aus
[Feather](https://github.com/feathericons/feather) (MIT, © 2013–2023 Cole Bemis).

Nur für die Prüfungen, nicht in der .exe: xUnit (Apache-2.0),
Microsoft.NET.Test.Sdk (MIT).

## Dienste, die REGOradio zur Laufzeit fragt

- **radio-browser.info** für die Sendersuche und die Senderlogos. Die Logos
  gehören den jeweiligen Sendern; REGOradio liefert keine mit, es lädt sie im
  Betrieb und hält sie nur lokal vor.
- **iTunes Search API** (Apple) für Cover zum laufenden Lied. Auch hier wird
  nichts mitgeliefert.

Die Schrift (Segoe UI) kommt von Windows und ist nicht Teil von REGOradio.

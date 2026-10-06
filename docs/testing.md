# Prüfstand v0.1.0

Stand: 6. Oktober 2026.

## Integrität und Schnittstelle

Die veröffentlichte Plugin-DLL wurde ursprünglich gegen die unveränderte RDCMan-3.12-Assembly gebaut. Sie nutzt `IPlugin`, `IPluginContext`, `IMainForm`, `IUndockedServerForm` und `IServerTree` aus RDCMans MEF-Plugin-Schnittstelle. Die betreffenden Methodenverträge wurden in 3.12 und 3.21 statisch verglichen und sind identisch. Beide Versionen suchen nach `Plugin.*.dll` im Programmverzeichnis.

RDCMan 3.12 enthält .NET 8; die geprüfte 3.21 enthält .NET 10.0.11. Der ergänzende 3.21-Test lief mit deren extrahierter Assembly unter WindowsDesktop .NET 10.0.12. Die bestehende v0.1.0-DLL wurde dabei unverändert geladen.

Es werden keine EXE-, IL- oder Speicherpatches verwendet. Farbwechsel des originalen Serverbaums und der Serverbeschriftungen werden über UI-Farbänderungsereignisse ausgeglichen. Die readonly-Farbfelder der Anwendung werden nicht überschrieben. Der Plugin-Code hat keine `.rdg`-Dateizugriffe und keine Verbindungskommandos.

## Erfolgreiche isolierte Tests

- `DirectoryCatalog("Plugin.*.dll")`, MEF-Export und Plugin-Lifecycle.
- Hell/Dunkel/System einschließlich simulierter automatischer Windows-Farbwechsel.
- Wiederholtes Umschalten und Wiederherstellung von Farben, geerbten Farbwerten, FlatStyle, DrawMode, OwnerDraw und Menü-Renderer.
- Neue Controls/Dialoge und dynamische Untermenüs.
- Ausgleich simulierter Fokus-Farbwechsel und AxHost-Ausschluss.
- Speicherung und Laden der Auswahl über Plugin-XML.
- Shutdown und Entfernung des eigenen Menüs.
- Visuelle Kontrolle von Testbildern.

Die Testfenster sind synthetisch. Die Tests führen keine echten RDCMan-Verbindungen, Serverobjekte oder `.rdg`-Lade-/Speicheroperationen aus. Die Verwendung mit 3.12 und der Start mit 3.21 wurden zusätzlich vom Anwender bestätigt; das ist kein vollständiger RDP-Regressionstest.

## Grenzen

Native Systemdialoge, Tray-Kontext, manche Systemflächen und Titelleisten sind nicht vollständig dunkel bestätigt. Neue Fenster können einen hellen ersten Frame zeigen. Das Projekt bewertet oder repariert keine Versionsprobleme von RDCMan wie Laufwerksumleitung oder Console-Verbindungen.

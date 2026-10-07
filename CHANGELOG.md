# Changelog

## 0.2.1 — 2026-10-07

- Weißes Aufblitzen beim Speichern neuer Einträge in RDCMan 3.12 behoben; vom Anwender bestätigt.
- Bestätigungsdialog während synchroner nativer Zeichenaktualisierungen transparent halten; bei ungültiger Eingabe ursprüngliche Sichtbarkeit wiederherstellen.
- Bestätigung über Maus, Enter und Leertaste vor der WinForms-Verarbeitung erfassen.
- Registerkartenflächen und Rahmen direkt dunkel zeichnen.
- Automatisierte Prüfungen für Validierung, Eingabeverarbeitung und Schließen erweitert. RDP-Verbindungslogik und RDG-Format unverändert.
- 3.21 für diese Version nicht erneut geprüft; helle RDP-Verbindungsflächen nicht behoben.


## 0.2.0 — 2026-10-07

- Dialog-Flackern durch Einblenden erst nach Aufbau und synchronem Neuzeichnen behoben; in 3.12 bestätigt.
- Dunkle Scrollbalken, graue Registerkarten-/Textfeldrahmen und KeeTheme-Ordnersymbole.
- Server-/Verbindungsstatus-Symbole bleiben erhalten; Originaldarstellung wird bei Hell und Shutdown wiederhergestellt.
- Frühe Theme-Auswahl in PreLoad; Tests mit RDCMans eigener Dialogklasse erweitert.
- Gegen RDCMan 3.12 gebaut und geprüft. 3.21 für v0.2.0 noch nicht erneut bestätigt.
## 0.1.0 — 2026-10-06

- Eigenständiges MEF-Theme-Plugin für unveränderte RDCMan-EXEs.
- Hell, Dunkel und Windows-Einstellung mit Wiederherstellung der ursprünglichen Darstellung.
- Dark-Palette für eigene Fenster, Dialoge, Menüs und Controls.
- Isolierte Prüfungen mit 3.12/.NET 8 und derselben DLL mit 3.21/.NET 10.
- Build-Skript, Quellcode und isolierter Testhost veröffentlicht.



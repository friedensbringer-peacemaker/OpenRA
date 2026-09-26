# S2 – Auswahl, Bauleiste und Produktion auf dem XR-Brett

- Zuständig: Codex Sol
- Priorität: P0 nach G1 und S1
- Ausgangsstand bei Vorbereitung: Branch `quest-tabletop-research`, Commit `510f1a0a9081c38795997acdb60b4c224808dd0a`, sauberer Git-Status; vor Arbeitsbeginn aktualisieren
- Abhängigkeit: eine stabil sichtbare und aktualisierte XR-Fläche (G1) sowie ein aktiver KI-Gegner (S1)

## Auftrag

Führe einen vollständigen Spielablauf auf Blitz über die räumliche Ansicht aus: einzelne und mehrere Einheiten auswählen, bewegen/angreifen, Bauleiste öffnen, Gebäude platzieren, Einheit produzieren und das Ergebnis des Gefechts erkennen. Untersuche zuerst, welche OpenRA-Widgets die Android-Spielsession bereits lädt und wohin `QuestInputQueue` die Controllerereignisse leitet. Nutze bestehende `WorldInteractionControllerWidget`-, `ProductionTabsWidget`- und `ProductionPaletteWidget`-Pfade, soweit sie in der tatsächlichen Session verfügbar sind. Ergänze nur die fehlenden UI-/Eingabebrücken.

Erlaubte Änderungen nach Abstimmung mit A1: `QuestGameSession.cs`, `MainActivity.cs`, `QuestInputQueue.cs`, zugehörige Tests und nötige UI-Initialisierung. Keine gleichzeitige Bearbeitung dieser Dateien durch A1. Neue Controllerbelegung nur mit aktualisierter Dokumentation und Gerätetest; K2 erledigt davor lediglich die Bestandskorrektur.

## Abnahme

Ein dokumentierter Quest-Durchlauf enthält Auswahlrahmen, additive Auswahl, Kontextbefehl, Pan/Zoom, Gebäudeplatzierung, Einheitenproduktion und sichtbare Sieg-/Niederlagebedingung gegen die KI. Für jeden Schritt ist festgehalten, ob er funktioniert oder welcher genaue Fehler ihn verhindert. Builds und gezielte lokale Eingabetests bestehen. Ohne den Durchlauf bleibt G2 offen.

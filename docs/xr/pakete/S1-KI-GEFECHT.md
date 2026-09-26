# S1 – Lokales Red-Alert-Gefecht mit KI

- Zuständig: Codex Sol
- Priorität: P0; offline startbar
- Ausgangsstand bei Vorbereitung: Branch `quest-tabletop-research`, Commit `510f1a0a9081c38795997acdb60b4c224808dd0a`, sauberer Git-Status
- Abhängigkeit: keine Geräteverbindung für die Implementierung; G0/G2 für die spätere Quest-Abnahme
- Umsetzungsstand: Host-/Bot-Client, Red-Alert-Bottypprüfung und Aktivierungsprüfung im Code ergänzt; kombinierter Android/XR-Build und Signatur erfolgreich. Die installierte APK meldete auf Quest den aktivierten `normal`-KI-Gegner. Fortlaufende Ticks, sichtbares Gefecht und damit die endgültige Abnahme stehen noch aus ([Teilbefund](../QUEST-TEST-2026-09-26.md)).

## Auftrag

`OpenRA.Quest.Probe/QuestGameSession.cs` erzeugt derzeit zwei Blitz-Slots, aber nur einen lokalen Client. Ergänze einen gültigen KI-Gegner nach den vorhandenen OpenRA-Session-/Bot-Konventionen. Ermittle den geeigneten Bot-Typ aus den Red-Alert-Moddaten und beachte `BotControllerClientIndex`, Team/Spawn/Faction sowie den lokalen `EchoConnection`-Pfad. Erhalte den Android-GL-Thread, den bisherigen Datenimport und die getrennte XR-Brücke.

Erlaubte Änderungen: `QuestGameSession.cs`, kleine für diese Session erforderliche Hilfen in `OpenRA.Quest.Probe/` und gezielte Tests. Andere Engine- oder Mod-Dateien nur mit dokumentierter Begründung. Nicht in `QuestXrBridge.cs`, `XrQuad.cpp` oder Kimi-Dokumentationsdateien arbeiten.

## Abnahme

Ein lokaler Test oder diagnostischer Lauf zeigt zwei unterschiedliche Spieler, davon einen gültigen, tickenden Red-Alert-Bot; keine bloß leere zweite Lobbyposition. Es entstehen keine ungültigen Bot-/Order-Fehler. Der reguläre Android-Build und der kombinierte XR-Build bleiben erfolgreich. Auf Quest ist der Bot erst nach G0 als tatsächlich spielender Gegner zu bestätigen. Berichte den genauen Nachweis und die noch offenen Gerätefragen getrennt.

# Roadmap: OpenRA als eigenständiges Quest-3-Tabletop

Stand: 26. September 2026. Ziel ist zunächst **ein vollständiges lokales Red-Alert-Gefecht gegen eine KI** auf einer räumlichen, mit dem Quest-Controller bedienbaren Fläche. Originale Spieldaten werden vom Nutzer getrennt importiert. Passthrough, mehrere räumliche Fenster und weitere Spiele folgen erst nach diesem Nachweis.

## Gesicherter Ausgangspunkt

- Auf der Quest bestätigt: Android-ARM64-App, Red-Alert-Regeln und Blitz-Karte, OpenRAs GLES-`Renderer` und `WorldRenderer` mit Terrain und Actors sowie der erste Tick einer regulären Welt.
- Gebaut und lokal geprüft, **noch nicht auf der Quest bestätigt**: fortlaufende Spielsession, In-App-ZIP-Import, Touch-Bedienung, kombinierte OpenXR-APK, Quad-Bildfluss und Controller-Actions. Die kombinierte signierte APK liegt lokal unter `../Artifacts/OpenRA-Quest-XR-Combined-untested.apk`.
- Die aktuelle XR-Belegung im Code ist rechter Trigger für Auswahl/Ziehen, A für Kontextbefehl, rechter Griff für Karten-Pan, rechter Stick für Zoom und B für additive Auswahl. Die Bedienbarkeit einschließlich Produktionsmenü ist offen.
- Die jetzige `QuestGameSession` trägt einen lokalen Client ein; für `Multi1` ist noch kein KI-Client eingerichtet. Ein tatsächlich spielbares Gefecht gegen KI ist daher ein eigener Implementierungsschritt.
- Der Nutzer hat den nächsten Quest-Gerätetest auf später verschoben. Die unten genannten Gerätetests sind **Prüf-Gates**, keine bereits ausgeführten Tests.

## Reihenfolge und Abnahmetore

| Tor | Nachweis | Freigabe für den nächsten Schritt |
| --- | --- | --- |
| G0 – Geräte-Baseline | Mit derselben kombinierten APK: separaten Red-Alert-Import, fortlaufende 2D-Partie, XR-Start, sichtbares sich aktualisierendes Quad und Start/Stopp anhand von Headset-Beobachtung und Logcat getrennt dokumentieren. | Erst reale Befunde entscheiden, ob die XR-Lebenszyklus-Korrektur A1 gebraucht wird. Bis zum vom Nutzer gewünschten späteren Test bleibt G0 offen. |
| G1 – stabile XR-Sitzung | Nach XR-Start laufen Simulation und Bildübergabe weiter; Pause/Fortsetzen und erneuter Start enden ohne hängende Eingaben oder verlorene GL-/XR-Ressourcen. | Controller- und Spiellogik auf einer verlässlich laufenden Fläche prüfen. |
| G2 – erstes Gefecht | Auf Blitz sind ein lokaler Spieler und eine aktive KI vorhanden. Auswahl, Mehrfachauswahl, Kontextbefehl, Pan, Zoom, Bauen, Produzieren und Spielende funktionieren über die räumliche Ansicht. | Erst danach Komfortfunktionen priorisieren. |
| G3 – benutzbare Tabletop-Fassung | Spielbrett neu ausrichten und skalieren, UI/Produktion gut lesen und bedienen, Eingaben abbrechen, Session sauber verlassen; relevante Fehlpfade sind im Headset verständlich. | Längere Spieltests und Leistungsprofil. |
| G4 – belastbare Quest-Version | Framezeit, Readback-Kosten, Speicher und Temperatur auf Quest messen; wiederholtes Starten sowie längere Gefechte bestehen. Paketierung und Datenimport sind reproduzierbar. | Optionale MR-/Passthrough-Arbeit und weitere Mods separat bewerten. |

Ein Gate zählt nur mit tatsächlich beobachteten Ergebnissen. Ein erfolgreicher Build oder ein Logeintrag allein belegt weder ein sichtbares XR-Brett noch Spielbarkeit. Das vorhandene [`smoke-quest-xr.sh`](../../OpenRA.Quest.XrProbe/scripts/smoke-quest-xr.sh) sammelt bei einem späteren Test Belege; es wurde noch nicht auf der Quest ausgeführt.

## Vorbereitete Umsetzungspakete

| Reihenfolge | Paket | Zuständigkeit | Startbedingung und konkretes Ergebnis |
| --- | --- | --- | --- |
| Jetzt | [S1 – lokales KI-Gefecht](pakete/S1-KI-GEFECHT.md) | Codex Sol | Bestehende Session so vervollständigen, dass ein gültiger Red-Alert-Bot teilnimmt; lokal bauen und gezielt testen. Geräteabnahme bleibt G2. |
| Jetzt | K1 – Geräteprotokoll und Belegmatrix | Kimi | Enges Review ohne Produktivänderung; späteren G0-Test und erwartete/fehlende Nachweise präzisieren. Auftrag in `ai-handoffs/2026-09-26-quest-testprotokoll/`. |
| Jetzt | K2 – Steuerungsdokumentation | Kimi | Die bisherige Entwurfsbelegung mit dem gebauten Code abgleichen und die Dokumentation korrigieren, ohne Eingabecode zu ändern. Auftrag in `ai-handoffs/2026-09-26-steuerungsdoku/`. |
| Nach G0 | [A1 – XR-/Android-Lebenszyklus](pakete/A1-XR-LEBENSZYKLUS.md) | Codex Astra | Den durch Gerätedaten belegten Fehler im Zusammenspiel von Android-GL, OpenXR-Session und Frame-Bridge beheben; G1 nachweisen. |
| Nach G1 und S1 | [S2 – Spiel-UI und Produktionspfad](pakete/S2-SPIEL-UI.md) | Codex Sol | Reale Auswahl- und Produktionsabläufe auf dem Brett vervollständigen und für G2 testen. |
| Nach G2 mit Messdaten | [A2 – Bildpfad und Leistung](pakete/A2-BILDPFAD.md) | Codex Astra | Den gemessenen Engpass im Readback/Kopierpfad beseitigen; G4 erneut messen. |

S1, K1 und K2 können in getrennten Dateibereichen vorbereitet oder bearbeitet werden. A1 und S2 verändern voraussichtlich dieselben Android-/XR-Einstiegspunkte und laufen deshalb **nacheinander**. A2 beginnt erst nach Messung; eine Optimierung auf Verdacht wäre kein belastbarer Fortschritt. Für G3 wird nach G2 anhand der beobachteten Bedienprobleme ein eigenes, engeres Paket formuliert. Weder die Sol-/Astra-Pakete noch die neuen Kimi-Aufträge sind mit dieser Roadmap bereits gestartet.

## Entscheidungspunkte

1. Wenn die Android-GL-Oberfläche beim immersiven XR-Start pausiert, zuerst A1 bearbeiten. Wenn sie weiterläuft, nur nachgewiesene Lebenszyklus- oder Restart-Fehler ändern.
2. Wenn Auswahl und Kontextbefehle funktionieren, aber Bauleiste/Produktion fehlen, S2 auf OpenRAs bestehende Widgets und Eingabepfade ausrichten; keine eigene RTS-Befehlslogik neben OpenRA bauen.
3. Für A2 zuerst messen: aktuelle Bildübergabe ist auf ungefähr 15 Hz begrenzt und verwendet GPU-Readback. 72 Hz ist ein späteres Ziel, kein heutiger Leistungswert.
4. Passthrough, Diorama, Multiplayer und weitere Spiele aus dem [XR-Backlog](BACKLOG.md) sind Erweiterungen nach dem ersten vollständigen Gefecht. Die GPL-Lizenz des Codes erlaubt keine Mitlieferung geschützter Original-Spieldaten.

# Roadmap: OpenRA als eigenständiges Quest-3-Tabletop

Stand: 26. September 2026. Ziel ist zunächst **ein vollständiges lokales Red-Alert-Gefecht gegen eine KI** auf einer räumlichen, mit dem Quest-Controller bedienbaren Fläche. Originale Spieldaten werden vom Nutzer getrennt importiert. Passthrough, mehrere räumliche Fenster und weitere Spiele folgen erst nach diesem Nachweis.

## Gesicherter Ausgangspunkt

- Auf der Quest bestätigt: Android-ARM64-App, Red-Alert-Regeln und Blitz-Karte, OpenRAs GLES-`Renderer` und `WorldRenderer` sowie eine fortlaufende lokale Spielsession mit KI und steigenden Simulationsticks. Die kombinierte signierte APK liegt lokal unter `../Artifacts/OpenRA-Tabletop-XR-Quest3-Preview.apk`.
- Gebaut und lokal geprüft, **noch nicht als bedienbarer XR-Ablauf auf der Quest bestätigt**: In-App-ZIP-Import, Touch-Bedienung, räumlicher Quad-Bildfluss und Controller-Actions.
- Die aktuelle XR-Belegung im Code ist rechter Trigger für Auswahl/Ziehen, A für Kontextbefehl, rechter Griff für Karten-Pan, rechter Stick für Zoom und B für additive Auswahl. Die Bedienbarkeit einschließlich Produktionsmenü ist offen.
- `QuestGameSession` richtet nun einen lokalen Host und den Red-Alert-Bot `normal` in `Multi1` ein und prüft bei der Welterstellung seine Aktivierung und Feindbeziehung. Die kombinierte APK wurde auf Quest installiert; die Session meldete dort den aktivierten KI-Gegner. Fortlaufende Ticks und ein tatsächlich spielendes Gefecht sind noch nicht bestätigt ([Teilbefund](QUEST-TEST-2026-09-26.md)).
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
| Code gebaut; Quest-Abnahme offen | [S1 – lokales KI-Gefecht](pakete/S1-KI-GEFECHT.md) | Codex | Host-/Bot-Client und Aktivierungsprüfung ergänzt; kombinierte APK gebaut. G2 bleibt bis zum Gerätetest offen. |
| Erledigt durch Codex | [K1 – Geräteprotokoll und Belegmatrix](QUEST-TESTPROTOKOLL.md) | Codex | G0-Ablauf und erwartete/fehlende Nachweise dokumentiert; Kimi wurde nicht beauftragt. |
| Erledigt durch Codex | [K2 – Steuerungsdokumentation](CONTROLS.md) | Codex | Entwurfsbelegung an den gebauten Code angeglichen; Kimi wurde nicht beauftragt. |
| Nach G0 | [A1 – XR-/Android-Lebenszyklus](pakete/A1-XR-LEBENSZYKLUS.md) | Codex Astra | Den durch Gerätedaten belegten Fehler im Zusammenspiel von Android-GL, OpenXR-Session und Frame-Bridge beheben; G1 nachweisen. |
| Nach G1 und S1 | [S2 – Spiel-UI und Produktionspfad](pakete/S2-SPIEL-UI.md) | Codex Sol | Reale Auswahl- und Produktionsabläufe auf dem Brett vervollständigen und für G2 testen. |
| Nach G2 mit Messdaten | [A2 – Bildpfad und Leistung](pakete/A2-BILDPFAD.md) | Codex Astra | Den gemessenen Engpass im Readback/Kopierpfad beseitigen; G4 erneut messen. |

Der S1-Code und die beiden kleinen Dokumentationspunkte wurden nach dem Plan durch Codex bearbeitet. A1 und S2 verändern voraussichtlich dieselben Android-/XR-Einstiegspunkte und laufen deshalb **nacheinander**. A2 beginnt erst nach Messung; eine Optimierung auf Verdacht wäre kein belastbarer Fortschritt. Für G3 wird nach G2 anhand der beobachteten Bedienprobleme ein eigenes, engeres Paket formuliert. A1, S2 und A2 wurden noch nicht begonnen; die vorbereiteten Kimi-Aufträge wurden nicht übergeben.

## Entscheidungspunkte

1. Wenn die Android-GL-Oberfläche beim immersiven XR-Start pausiert, zuerst A1 bearbeiten. Wenn sie weiterläuft, nur nachgewiesene Lebenszyklus- oder Restart-Fehler ändern.
2. Wenn Auswahl und Kontextbefehle funktionieren, aber Bauleiste/Produktion fehlen, S2 auf OpenRAs bestehende Widgets und Eingabepfade ausrichten; keine eigene RTS-Befehlslogik neben OpenRA bauen.
3. Für A2 zuerst messen: aktuelle Bildübergabe ist auf ungefähr 15 Hz begrenzt und verwendet GPU-Readback. 72 Hz ist ein späteres Ziel, kein heutiger Leistungswert.
4. Passthrough, Diorama, Multiplayer und weitere Spiele aus dem [XR-Backlog](BACKLOG.md) sind Erweiterungen nach dem ersten vollständigen Gefecht. Die GPL-Lizenz des Codes erlaubt keine Mitlieferung geschützter Original-Spieldaten.

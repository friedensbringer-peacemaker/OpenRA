# Backlog xr-openra

Stand: 27.09.2026 · Projektstand geschätzt **15–20 %** bis zu einer runden, spielbaren Quest-Fassung.
Versionshistorie: [CHANGELOG.md](CHANGELOG.md) · Konzepte/Ideen: [docs/xr/VR-MENUE-KONZEPT.md](docs/xr/VR-MENUE-KONZEPT.md) ·
Abnahmetore G0–G4: [docs/xr/ROADMAP-QUEST-TABLETOP.md](docs/xr/ROADMAP-QUEST-TABLETOP.md) ·
Weitere Spielideen (andere Spiele): [docs/xr/BACKLOG.md](docs/xr/BACKLOG.md).

Status-Stufen: Offen → Implementiert (gebaut) → Im Headset bestätigt. Prioritäten: P0 blockiert
Spielen, P1 wichtig, P2 später.

## Als Nächstes

| ID | Prio | Status | Aufgabe und Abnahmekriterium |
| --- | --- | --- | --- |
| TEST-001 | P0 | Offen | 0.2.8 im Headset prüfen: Ton hörbar, Ladekarte statt drei Punkten, Name unten rechts, Brett aufrecht und mittig, Brille ab/auf ohne Abbruch. |
| UX-001 | P1 | Implementiert | VR-Reiter im OpenRA-Einstellungsmenü (Original-Optik) mit Strahl-Einstellungen; Werte bleiben nach Neustart erhalten. |
| UX-002 | P1 | Implementiert | Spielbrett: Abstand (0,8–3 m), Breite (1–3,2 m), Höhe (±50 cm), „Jetzt zentrieren“; Strahl trifft nach Änderung weiter genau. |
| UX-003 | P1 | Implementiert | VR-Menü (linke Menütaste) in OpenRA-Optik: weiterspielen, entfalten, Spielmenü, VR-Einstellungen, zentrieren, Strahl an/aus; Strahl und Stick/A/B bedienbar. |
| UX-006 | P1 | Implementiert | Kontrollgruppen-Tasten 1–5: Tippen wählt, 3 s Halten speichert/überschreibt (Countdown, „Saved“), leere Auswahl überschreibt nicht. Prüfen: Treffsicherheit mit Strahl, Halten ohne Abrutschen, keine Überdeckung wichtiger UI. |
| HAPTIC-001 | P2 | Offen | Controller-Vibration (18 ms, 0,25) für Klick und „Gruppe gespeichert“; Hook `VrRuntime.Haptic` ist vorbereitet, native OpenXR-Haptik fehlt. |
| NAME-001 | P1 | Rückfrage | Einheitlicher Anzeigename: Code nutzt inzwischen `xr.openra`, Ladekarte/Startbild `xr-openra`. Nutzer entscheidet. |
| FLOW-001 | P0 | Offen | Normaler Start über OpenRAs Hauptmenü statt fester Blitz-Partie: Gefecht (Karte, Fraktion, KI wählen), Einstellungen, Beenden. |
| INPUT-001 | P1 | Offen | Tastaturersatz: VR-Tastatur für Textfelder (Speichername, Chat) und Hotkey-Leiste (Gruppen 1–0, Stopp, Wegpunkt). |

## Danach

| ID | Prio | Status | Aufgabe und Abnahmekriterium |
| --- | --- | --- | --- |
| PERF-001 | P1 | Offen | Bildweg ohne GPU-Readback: OpenRA direkt in die OpenXR-Swapchain rendern. Ziel ≥ 30 Bilder/s auf der Fläche (heute ~12–13). |
| UX-004 | P1 | Offen | Brett am Rahmen greifen und verschieben (Griff), Linkshänder-Option, Glättung des Zeigers (τ 25/55 ms), Klickstabilisierung 85 ms. |
| UX-005 | P2 | Offen | Tischansicht (Neigung 30–75°, 40 cm unter Blickhöhe) als Umschaltung; Zylinderfläche optional. |
| FLOW-002 | P1 | Offen | Speichern/Laden, Spielende-Dialog, Rückkehr ins Hauptmenü, Kampagnen-Missionen. |
| AUDIO-001 | P2 | Offen | Musik: Import aus eigener Originalquelle bequemer (Datei-Dialog in der App), Hinweis wenn keine Musik vorhanden. |
| PASS-001 | P2 | Offen | Passthrough als Hintergrund (0–100 %), wie bei XR-Settlers. |
| I18N-001 | P2 | Offen | Deutsche Oberfläche: OpenRA-Fluent-Übersetzung `de` für Menüs und VR-Reiter (heute Englisch wie das Original). |
| STAB-002 | P1 | Teilweise (0.2.10) | Laden in Etappen je Frame mit Zeitmessung; nächster Schritt anhand der Messwerte: lange Etappen weiter teilen oder in Hintergrund-Thread verlagern. Ursprünglich: Partie nicht mehr am Stück auf dem GL-Thread laden (8–10 s blockierend). Beleg 27.09. 23:02: Horizon schloss die App im Standby (`remove-root-task`), `OnDestroy` kam wegen blockiertem UI-/GL-Thread nicht durch → `am_kill … destroyTimeout` nach 10 s. Gleiche Wurzel wie der ANR aus 0.2.6. Ziel: Laden asynchron, Lebenszyklus-Ereignisse jederzeit < 1 s. |
| STAB-001 | P1 | Offen | Kontextverlust nach Pause trotz `PreserveEGLContextOnPause`: Partie asynchron neu laden statt ANR-Risiko. |
| REL-001 | P2 | Offen | Hinweis „nicht mit OpenRA/EA verbunden“, Lizenzhinweise in App und README; Release-Signatur. |

## Arbeitsregeln

- Engine-Änderungen klein halten (Upstream-Merges). VR-Code in eigenen Dateien; bisher nötig:
  `Sound.SetAllSoundsPaused`, Settings-Reiter-Eintrag in `mods/common/chrome/settings.yaml`.
- Keine Originalspieldaten in Repo oder APK. Musik/Inhalte nur per Import durch den Spieler.
- Jede Bedienänderung in CHANGELOG, BACKLOG und `docs/xr/CONTROLS.md` nachziehen.

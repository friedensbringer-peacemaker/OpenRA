# Backlog xr.openra

Stand: 27.09.2026 · Projektstand geschätzt **15–20 %** bis zu einer runden, spielbaren Quest-Fassung.
Versionshistorie: [CHANGELOG.md](CHANGELOG.md) · Konzepte/Ideen: [docs/xr/VR-MENUE-KONZEPT.md](docs/xr/VR-MENUE-KONZEPT.md) ·
Abnahmetore G0–G4: [docs/xr/ROADMAP-QUEST-TABLETOP.md](docs/xr/ROADMAP-QUEST-TABLETOP.md) ·
Weitere Spielideen (andere Spiele): [docs/xr/BACKLOG.md](docs/xr/BACKLOG.md).

Status-Stufen: Offen → Implementiert (gebaut) → Im Headset bestätigt. Prioritäten: P0 blockiert
Spielen, P1 wichtig, P2 später.

## Als Nächstes

| ID | Prio | Status | Aufgabe und Abnahmekriterium |
| --- | --- | --- | --- |
| TEST-001 | P0 | Offen | 0.3.1 im Headset prüfen: Passthrough-Modi, Ton hörbar, Ladekarte statt drei Punkten, Name unten rechts, Brett aufrecht und mittig, Brille ab/auf ohne Abbruch, VR-Menü (linke Menütaste), VR-Reiter, Kontrollgruppen 1–5. Ladeschritt-Zeiten aus dem Log für STAB-002 auswerten. |
| UX-001 | P1 | Implementiert | VR-Reiter im OpenRA-Einstellungsmenü (Original-Optik) mit Strahl-Einstellungen; Werte bleiben nach Neustart erhalten. |
| UX-002 | P1 | Implementiert | Spielbrett: Abstand (0,8–3 m), Breite (1–3,2 m), Höhe (±50 cm), „Jetzt zentrieren“; Strahl trifft nach Änderung weiter genau. |
| UX-003 | P1 | Implementiert | VR-Menü (linke Menütaste) in OpenRA-Optik: weiterspielen, entfalten, Spielmenü, VR-Einstellungen, zentrieren, Strahl an/aus; Strahl und Stick/A/B bedienbar. |
| UX-006 | P1 | Implementiert | Kontrollgruppen-Tasten 1–5: Tippen wählt, 3 s Halten speichert/überschreibt (Countdown, „Saved“), leere Auswahl überschreibt nicht. Prüfen: Treffsicherheit mit Strahl, Halten ohne Abrutschen, keine Überdeckung wichtiger UI. |
| HAPTIC-001 | P2 | Offen | Controller-Vibration (18 ms, 0,25) für Klick und „Gruppe gespeichert“; Hook `VrRuntime.Haptic` ist vorbereitet, native OpenXR-Haptik fehlt. |
| NAME-001 | P1 | Erledigt | Anzeigename = App-ID mit Punkten: überall `xr.openra` (Quest-Menü, Ladekarte, Startbild, Doku). Branch `xr-openra` und APK-Dateiname bleiben. |
| FLOW-001 | P0 | Implementiert (0.4.0) | Normaler Start über OpenRAs Hauptmenü: Gefecht-Lobby (Karte, Fraktion, KI), Missionen, Einstellungen, Beenden. Prüfen: Ladezeit bis Menü (Shellmap-Schritt < 5 s?), Lobby mit Strahl bedienbar (Dropdowns, Slots), Partie starten/beenden, Rückkehr ins Menü, Exit schließt die App, Bildrate im Menü. Falls Shellmap zu schwer: statischer Menühintergrund (PauseShellmap/leichte Karte). |
| INPUT-001 | P1 | Teilweise (0.4.2) | VR-Tastatur für Textfelder implementiert (automatisch bei Fokus). Prüfen: Tippen mit Strahl, Umlaute darstellbar, Enter sendet Chat, Tastatur verdeckt das Feld nicht. Offen: Hotkey-Leiste (Stopp, Wegpunkt, Angriffsbewegung). |

## Danach

| ID | Prio | Status | Aufgabe und Abnahmekriterium |
| --- | --- | --- | --- |
| PERF-001 | P1 | Implementiert (0.4.1) | Asynchroner nativer Readback (2 PBOs) + Upload nur bei neuem Spielbild + GPU-Blit je XR-Frame. Ziel ≥ 30 Bilder/s (vorher ~12–13); im Log messen. Nächste Stufe bei Bedarf: gemeinsamer EGL-Kontext und direkter GPU-Blit ohne CPU-Kopie, Spielrate > 30. |
| UX-004 | P1 | Offen | Brett am Rahmen greifen und verschieben (Griff), Linkshänder-Option, Glättung des Zeigers (τ 25/55 ms), Klickstabilisierung 85 ms. |
| UX-005 | P2 | Offen | Tischansicht (Neigung 30–75°, 40 cm unter Blickhöhe) als Umschaltung; Zylinderfläche optional. |
| FLOW-002 | P1 | Offen | Speichern/Laden, Spielende-Dialog, Rückkehr ins Hauptmenü, Kampagnen-Missionen. |
| AUDIO-001 | P2 | Offen | Musik: Import aus eigener Originalquelle bequemer (Datei-Dialog in der App), Hinweis wenn keine Musik vorhanden. |
| PASS-001 | P1 | Implementiert | Passthrough-Modi Aus / Raum als Hintergrund / Hintergrund + unerkundet durchsichtig, Raum-Sichtbarkeit 0–100 %, Look Farbe/Graustufen/abgedunkelt; im VR-Reiter und VR-Menü. Prüfen: Start/Stopp ohne Ruckler, Einstellungen bleiben nach Neustart. |
| PASS-002 | P1 | Implementiert | Unerkundete Karte (schwarzer Shroud) und Bereich außerhalb der Karte zeigen den Raum; erkundete Gebiete füllen sich. Prüfen: keine Löcher in Einheiten/dunklem Gelände, Seitenleiste/Menüs deckend, weiche Shroud-Ränder sehen gut aus, Leistung (Maske je Frame, 160×100 Blöcke). |
| I18N-001 | P2 | Offen | Deutsche Oberfläche: OpenRA-Fluent-Übersetzung `de` für Menüs und VR-Reiter (heute Englisch wie das Original). |
| STAB-002 | P1 | Teilweise (0.2.10) | Laden in Etappen je Frame mit Zeitmessung. Messung 28.09.: längste Etappen Karte 2,3 s, Abschluss 1,8 s, Kartenregeln 1,3 s, Moddaten 1,2 s – alle unter der 5-s-ANR-Grenze. Nächster Schritt nur bei Bedarf: diese weiter teilen oder in Hintergrund-Thread verlagern. Ursprünglich: Partie nicht mehr am Stück auf dem GL-Thread laden (8–10 s blockierend). Beleg 27.09. 23:02: Horizon schloss die App im Standby (`remove-root-task`), `OnDestroy` kam wegen blockiertem UI-/GL-Thread nicht durch → `am_kill … destroyTimeout` nach 10 s. Gleiche Wurzel wie der ANR aus 0.2.6. Ziel: Laden asynchron, Lebenszyklus-Ereignisse jederzeit < 1 s. |
| STAB-003 | P0 | Implementiert (0.4.4) | Lua 5.1 für Android (`liblua51.so`), sonst kein Hauptmenü/keine Missionen. Prüfen: Hauptmenü mit Hintergrundkarte erscheint, eine Mission startet. |
| STAB-001 | P1 | Offen | Kontextverlust nach Pause trotz `PreserveEGLContextOnPause`: Partie asynchron neu laden statt ANR-Risiko. |
| NAME-002 | P2 | Recherche | Quest-Systemleiste zeigt unten rechts „App-Name nicht verfügbar“ bei selbst installierten Apps, auch mit korrektem Label (Befund auch bei xr.settlers25). Vermutlich Horizon-OS-Eigenheit für Apps ohne Store-Eintrag. Prüfen, ob Metadaten (z. B. Store-/Plattform-Manifesteinträge) das beheben; bis dahin zeigen Ladekarte und Startbild den Namen. |
| REL-001 | P2 | Offen | Hinweis „nicht mit OpenRA/EA verbunden“, Lizenzhinweise in App und README; Release-Signatur. |

## Arbeitsregeln

- Engine-Änderungen klein halten (Upstream-Merges). VR-Code in eigenen Dateien; bisher nötig:
  `Sound.SetAllSoundsPaused`, Settings-Reiter-Eintrag in `mods/common/chrome/settings.yaml`,
  Gruppenleiste in `mods/ra/chrome/ingame-player.yaml`, zwei Layout-Zeilen je `mod.yaml` (ra, d2k, ts),
  `Game` als `partial` + `CreateInputHandler` in `Game.cs` (eingebetteter Start in `Game.Embedded.cs`).
- Keine Originalspieldaten in Repo oder APK. Musik/Inhalte nur per Import durch den Spieler.
- Jede Bedienänderung in CHANGELOG, BACKLOG und `docs/xr/CONTROLS.md` nachziehen.

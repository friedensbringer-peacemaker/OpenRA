# Updatelog / Changelog

Dieses Protokoll beschreibt den Quest-Port **xr-openra** (App-ID `xr.openra`), nicht die
Upstream-Version von OpenRA. Geplante Arbeit: [BACKLOG.md](BACKLOG.md). Ideen und Konzepte:
[docs/xr/VR-MENUE-KONZEPT.md](docs/xr/VR-MENUE-KONZEPT.md). Bauanleitung:
[docs/xr/BUILD-QUEST.md](docs/xr/BUILD-QUEST.md).

„Gebaut“ heißt kompiliert und auf Manifest/Signatur geprüft. „Im Headset bestätigt“ steht nur
dort, wo es tatsächlich beobachtet wurde.

## Unveröffentlicht

Noch nichts.

## 0.2.9-preview — 2026-09-27

- Eigener **VR-Reiter im OpenRA-Einstellungsmenü** (Original-Optik: Checkboxen, Schieberegler,
  Auswahllisten): Controllerstrahl an/aus, Stärke, Farbe, Zielpunkt; Spielbrett-Abstand, -Breite
  und -Höhe; „Jetzt zentrieren“. Gespeichert in OpenRAs `settings.yaml` (Abschnitt `Vr`).
  Auf dem Desktop bleibt der Reiter unsichtbar. (BACKLOG UX-001, UX-002)
- Spielbrettgröße und -position sind zur Laufzeit einstellbar; Strahltreffer rechnen mit der
  eingestellten Größe.

Gebaut (`versionCode` 11); OpenRAs YAML-Prüfung (`--check-yaml`) für ra, d2k und ts ohne Fehler.
Am 27.09. um 23:02 auf Quest installiert. Per Log bestätigt: Ladekarte startet nach 5,5 s
(„XR-Start mit Ladebildschirm“), „Audio: OpenAL-Soft-Ausgabe aktiv“. Headset lag im Standby;
Horizon schloss die App, dabei `destroyTimeout` (BACKLOG STAB-002). VR-Reiter, Ton und Brett noch
nicht im Headset gesehen/gehört.

## 0.2.8-preview — 2026-09-27

- Ton über OpenAL Soft 1.24.3 (OpenSL ES) statt stummem Platzhalter; Pause beim Absetzen der Brille.
- XR-Fläche startet schon während des Ladens mit Ladekarte (Spielname, ~30 s Hinweis, Fortschritt,
  Kurzbedienung, Version unten rechts); Horizon-OS-Startbild `vr_splash.png`.
- Spielbrett startet aufrecht vor der Blickrichtung (nur Yaw, keine Neigung/Kippung); Recenter
  (langer Meta-Druck) stellt es neu vor den Blick.
- `quest/import-music.sh` spielt eine eigene `scores.mix` ein.

Gebaut (`versionCode` 10). Nicht installiert (Quest nicht verbunden), nicht im Headset bestätigt.

## 0.2.7-preview — 2026-09-27

- ANR nach Brille ab/auf behoben: `PreserveEGLContextOnPause`, die Partie wird nicht mehr neu geladen.

Per ADB bestätigt (Standby → Aufwecken: Partie lief von Tick 137 bis 390 weiter, XR-Bilder wieder
aktiv, kein ANR). Im getragenen Headset noch nicht bestätigt.

## 0.2.6-preview — 2026-09-27

- Neues Projekt/Repository **XR-OpenRA**, App-ID `xr.openra` (vorher
  `com.friedensbringer.openra.questprobe`).
- Ein-Befehl-Build für Windows/macOS/Linux (`Quest-Build.cmd`, `quest/all.sh`): lädt Werkzeuge
  nach `.toolchains/`, das Red-Alert-Quick-Install-Paket (SHA-1 geprüft), baut und installiert.

Auf Windows 10 gebaut und auf Quest 3 installiert; Start und Laden der Partie per Log bestätigt.

## Vor dem Umzug (0.1–0.2.6, Branch `quest-tabletop-research`)

Machbarkeitsprobe, OpenRA-Renderer und `WorldRenderer` auf Quest, lokale Partie mit KI auf der Karte
Blitz, OpenXR-Quad mit Controllerstrahl, Schnellmenü, Bauhof-Doppelklick. Details in
[docs/xr/](docs/xr/README.md) und `OpenRA.Quest.XrProbe/README.md`.

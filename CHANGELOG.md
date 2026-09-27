# Updatelog / Changelog

Dieses Protokoll beschreibt den Quest-Port **xr.openra** (App-ID `xr.openra`), nicht die
Upstream-Version von OpenRA. Geplante Arbeit: [BACKLOG.md](BACKLOG.md). Ideen und Konzepte:
[docs/xr/VR-MENUE-KONZEPT.md](docs/xr/VR-MENUE-KONZEPT.md). Bauanleitung:
[docs/xr/BUILD-QUEST.md](docs/xr/BUILD-QUEST.md).

„Gebaut“ heißt kompiliert und auf Manifest/Signatur geprüft. „Im Headset bestätigt“ steht nur
dort, wo es tatsächlich beobachtet wurde.

## Unveröffentlicht

Noch nichts.

## 0.4.0-preview — 2026-09-28

- **Normaler Start über OpenRAs Hauptmenü** statt fester Blitz-Partie (FLOW-001): Hintergrundkarte
  (Shellmap), Hauptmenü, **Gefecht** mit Lobby (Karte, Fraktion, Farbe, Anzahl und Stärke der KI-Gegner,
  Spieloptionen) über einen lokalen Server, **Missionen/Kampagne**, Einstellungen, Beenden. Nach einer
  Partie geht es zurück ins Menü; „Exit“ im Hauptmenü schließt die App.
- Neuer eingebetteter Startpfad in der Engine (`OpenRA.Game/Game.Embedded.cs`): `InitializeEmbedded` (Start
  in Etappen) und `EmbeddedFrame` (eine Iteration der OpenRA-Hauptschleife pro GL-Frame, Bildrate 30).
  Die Quest-App nutzt ihn über `QuestMenuHost`; der Bauhof-Doppelklick bleibt in echten Partien aktiv.
- Alle 141 Red-Alert-Karten (12 MB) sind in der APK. Mod-Dateien werden nur nach Installation/Update
  in den App-Speicher kopiert, nicht mehr bei jedem Start.
- Headset-Voreinstellungen: kein Versions-/News-/Discord-/NAT-Zugriff, keine Erststart-Dialoge
  (die eine Tastatur bräuchten), Maus-Modus „Modern“, Software-Cursor.

Gebaut (`versionCode` 15); Desktop-Build und OpenRA-YAML-Prüfung (ra, d2k, ts) ohne Fehler/Warnungen.
Nicht installiert, nicht im Headset bestätigt. Offene Risiken: Laden der Shellmap ist ein einzelner
Ladeschritt (Dauer unbekannt, ANR-Grenze 5 s), laufende Shellmap-Schlacht im Menü kostet Leistung.

## 0.3.1-preview — 2026-09-28

- Anzeigename einheitlich `xr.openra` (= App-ID) im Quest-Menü, auf Ladekarte, Startbild und in
  der Doku (NAME-001).
- **Passthrough** (Raumansicht der Quest, `XR_FB_passthrough`) mit drei Modi im VR-Reiter
  (Abschnitt „Passthrough (Room View)“) und als Umschalter im VR-Menü („Room: Off / Background /
  See-Through“): **Aus**, **Raum als Hintergrund**, **Hintergrund + unerkundete Karte durchsichtig**.
  Dazu Raum-Sichtbarkeit 0–100 % und Raum-Look Farbe / Graustufen / abgedunkelt. Fehlt Passthrough
  auf dem Gerät, bleibt es schwarz und der Reiter zeigt einen Hinweis. (PASS-001, PASS-002)
- **Unerkundete Karte durchsichtig:** Bei Modus 3 wird pro 8×8-Pixel-Block geprüft, ob das Kartenfeld
  für den Spieler unerkundet ist (`Shroud.IsExplored`, auch Bereich außerhalb der Karte). Dort werden
  (fast) schwarze Pixel transparent; sobald ein Gebiet erkundet ist, füllt es sich mit der Karte.
  Seitenleiste, Befehlsleiste, Gruppentasten, Chat und offene Menüs bleiben immer deckend.

Gebaut (`versionCode` 14); OpenRA-YAML-Prüfung für ra, d2k und ts ohne Fehler/Warnungen; native
Bibliothek ohne Warnungen. Am 28.09. um 00:18 auf Quest installiert. Per Log bestätigt: Ladekarte
nach 6,6 s, Audio aktiv, „Passthrough verfügbar: 1“, Brett aufrecht platziert, Partie nach 15,0 s.
Ladeschritte: Renderer 7 ms, Moddaten 1224, Schriften/Ton 559, Karte 2277, Kartenliste 93,
Kartenregeln 1310, Welt 748, Weltdarstellung 27, Abschluss 1826 ms (längster Block 2,3 s statt
vorher ~9 s am Stück). Passthrough-Modi und VR-Menü noch nicht im Headset gesehen.

## 0.3.0-preview — 2026-09-27

- **VR-Menü in OpenRA-Optik** (linke Menütaste) ersetzt das selbst gezeichnete Android-Menü:
  Weiterspielen, Entfalten (F), Spielmenü, VR-Einstellungen (öffnet direkt den VR-Reiter),
  Brett zentrieren, Controllerstrahl an/aus. Bedienbar per Strahl oder Stick/A/B. (UX-003)
- **Kontrollgruppen-Tasten 1–5** oben über dem Spielfeld: kurz tippen = Gruppe auswählen,
  3 s halten = aktuelle Auswahl speichern/überschreiben (Countdown „Save 3…“, dann „Saved“;
  leere Auswahl überschreibt nichts). Anzeige der Einheitenzahl je Gruppe. Nur mit XR sichtbar. (UX-006)
- Strahl-Einstellungen haben nur noch eine Quelle (`VrSettings`); Android speichert eine Kopie
  nur für den Ladebildschirm.

## 0.2.10-preview — 2026-09-27

- Partie wird in Etappen geladen (Renderer, Moddaten, Schriften/Ton, Karte, Kartenliste,
  Kartenregeln, Welt, Weltdarstellung), jede Etappe in einem eigenen Frame mit Zeitmessung im
  Log („Ladeschritt …: … ms“). Android-Lebenszyklus-Ereignisse warten nicht mehr auf den ganzen
  Ladevorgang. (STAB-002)

0.2.10 und 0.3.0: gebaut (`versionCode` 13); OpenRA-YAML-Prüfung für ra, d2k und ts ohne Fehler
und ohne Warnungen. Auf Wunsch des Nutzers **nicht installiert**; nicht im Headset bestätigt.

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

# Konzept: VR-Menü und Ideen

Stand 27.09.2026. Umsetzung und Status: [BACKLOG.md](../../BACKLOG.md).

## Entscheidung: VR-Einstellungen als Reiter im Originalmenü

Wie bei XR-Settlers bekommen die VR-Einstellungen einen eigenen Unterpunkt im Spielmenü statt
einer fremd aussehenden Zusatzebene.

- OpenRAs Einstellungen sind datengetrieben: `mods/common/chrome/settings.yaml` listet die Reiter
  (`Panels:`), jeder Reiter ist eine Layout-Datei (`settings-*.yaml`) plus eine `ChromeLogic`-Klasse,
  die sich per `RegisterSettingsPanel` anmeldet.
- Neu: `VR_PANEL` → `mods/common/chrome/settings-vr.yaml` + `VrSettingsLogic`
  (`OpenRA.Mods.Common/Widgets/Logic/Settings/`). Dadurch gleiche Rahmen, Schriften, Regler und
  Auswahllisten wie im Original.
- Werte liegen in einem eigenen Einstellungsmodul `VrSettings` (`[YamlNode("Vr")]`, gleicher
  Mechanismus wie `SoundSettings`/`AutoSave`) und damit in OpenRAs `settings.yaml`.
- Auf dem Desktop meldet sich der Reiter nicht an (`VrRuntime.Available` ist dort `false`) und
  bleibt unsichtbar. Die Quest-App setzt die Laufzeit-Hooks (Übernehmen an OpenXR, Zentrieren).
- Chrome-Layouts werden in OpenRA nicht zusammengeführt (doppelte Schlüssel sind ein Fehler).
  Deshalb ist der Eintrag in `settings.yaml` und das Einbinden von `settings-vr.yaml` in
  `ra`, `d2k` und `ts` nötig; das ist die einzige geteilte Stelle.
- Beschriftungen zunächst Englisch wie das restliche OpenRA-Menü (Fluent `chrome.ftl`);
  deutsche Übersetzung als eigener Punkt (I18N-001).

### Inhalt des VR-Reiters

| Abschnitt | Einstellung | Bereich / Werte | Standard |
| --- | --- | --- | --- |
| Controller ray | Show ray | an/aus | an |
| | Ray thickness | Thin / Medium / Thick | Medium |
| | Ray color | White / Warm yellow / Blue / Grey | White |
| | Target marker | Dot / Ring / Cross | Ring |
| Game board | Distance | 0,8–3,0 m | 1,4 m |
| | Width | 1,0–3,2 m (Höhe folgt 16:10) | 1,6 m |
| | Height offset | −50 … +50 cm | 0 |
| | Recenter now | Button: Brett neu vor den Blick | – |
| Passthrough (Room View) | Mode | Off / Room as Background / Background + Unexplored See-Through | Off |
| | Room Look | Color / Grayscale / Dimmed | Color |
| | Room Visibility | 0–100 % | 100 % |

Später: Zeigerhand, Glättung, Haptik, Tischansicht, Bildrate/Auflösung.

**Unerkundet durchsichtig (0.3.1):** Die XR-Fläche wird mit Alpha übergeben. Pro 8×8-Block prüft
`QuestGameSession.TryComputeSeeThroughMask`, ob das Feld unter der Blockmitte für den Spieler
unerkundet ist; dort werden fast schwarze Pixel (alle Kanäle ≤ 10) transparent und Passthrough
scheint durch. UI-Bereiche (Seitenleiste als ganzer Streifen, Befehls-/Haltungsleiste,
Spezialfähigkeiten, Gruppentasten, Chat) und offene Fenster bleiben deckend.

### Schnellmenü (linke Menütaste)

Heute mit Android-`Canvas` gezeichnet („OPENRA · QUEST“). Ziel (UX-003): als OpenRA-Widget in
Original-Optik, wenige große Einträge: Weiterspielen · Entfalten · Spielmenü · VR-Einstellungen
(öffnet direkt den VR-Reiter) · Jetzt zentrieren. Bis dahin liest das Canvas-Menü dieselben
`VrSettings`, damit beide Wege dieselben Werte zeigen.

## Rechtlicher Rahmen (Recherche 27.09.2026)

- OpenRA-Code: GPLv3 → Fork und Änderungen erlaubt; Quellcode jeder verteilten APK öffentlich
  halten (Repo ist öffentlich).
- Originaldaten: bleiben Eigentum von EA; OpenRA bietet die Freeware-Pakete im Rahmen der
  C&C-Modding-Richtlinien an. Nie in APK/Repo, nur Import durch den Spieler, nicht kommerziell.
- Name/Marke: Die OpenRA-Legal-Seite regelt Forknamen nicht. „xr.openra“ als erkennbarer Fork,
  kein offizielles Logo, Hinweis „nicht mit OpenRA oder EA verbunden“ (REL-001).
- Upstream-Haltung: Mobile/Touch-Ports wurden mehrfach abgelehnt (UI auf Maus+Tastatur
  ausgelegt, z. B. Issues #19206, #8217); proprietäre Module werden nicht angenommen.
  → eigener Fork, Engine-Eingriffe minimal, VR-Logik in eigenen Dateien.
- Andere Projekte: keine VR-Portierung von OpenRA gefunden; alte Android-Forks
  (tarek369/OpenRA, bbit-git/OpenRA) nur als Referenz für Android-Details; „pocketra“ ist ein
  Godot-Neubau, nicht wiederverwendbar.

## Ideensammlung

- Tabletop-Modus mit Passthrough: Schlachtfeld liegt auf dem echten Tisch (Mixed Reality).
- Minimap als eigenes kleines Panel am linken Controller („Armbanduhr“).
- Bauleiste/Produktion als separates, greifbares Panel neben dem Brett (Vorsicht: Erfahrung aus
  Settlers mit separaten Menüebenen, siehe `xr-quest-port`-Skill – nur mit relativer Größe).
- Haptik bei Einheiten-Bestätigung, Angriff auf eigene Basis, Bau fertig.
- Sprachbefehle („Alle Panzer auswählen“) als spätere Option.
- Lokaler Mehrspieler zwischen zwei Quests im selben Raum (OpenRA-Netzwerkcode vorhanden).
- Weitere OpenRA-Mods (Tiberian Dawn, Dune 2000) nach Rechteprüfung; siehe `docs/xr/BACKLOG.md`.

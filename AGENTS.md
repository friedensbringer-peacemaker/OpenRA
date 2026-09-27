# XR-OpenRA – Arbeitskontext für Coding-Agenten

Ziel: OpenRA (Red Alert) als Tabletop-XR-App auf der Meta Quest 3.
Android-Paket / App-ID: **`xr.openra`**, Anzeigename **`xr-openra`** (vorher `com.friedensbringer.openra.questprobe`).

- Repo: Fork `friedensbringer-peacemaker/XR-OpenRA`, Arbeitsbranch `xr-openra`
  (basiert auf `quest-tabletop-research`). `upstream` = `OpenRA/OpenRA`, Branch `bleed`.
- Quest-Code: `OpenRA.Quest.Probe/` (.NET-Android-App, `net10.0-android`),
  `OpenRA.Quest.XrProbe/` (nativer OpenXR-Host + Java-Brücke).
  Engine-Anpassungen möglichst klein halten, damit Upstream-Merges machbar bleiben.
- Doku, Roadmap, Testprotokolle: `docs/xr/` (Einstieg `docs/xr/README.md`,
  `ROADMAP-QUEST-TABLETOP.md`). Alte Testprotokolle nennen noch den alten Paketnamen – das ist historisch korrekt.
- Keine Originalspieldaten (Red-Alert-`.mix`, ISOs), Signierschlüssel, APKs oder Zugangsdaten committen.
  Lokale Artefakte gehören nach `Artifacts/` (gitignored).
- Behauptete Headset-Tests müssen tatsächlich durchgeführt sein.

## Bauen / Installieren

Einzige maßgebliche Anleitung: [docs/xr/BUILD-QUEST.md](docs/xr/BUILD-QUEST.md).
`bash quest/all.sh` (Windows: `Quest-Build.cmd`) lädt Werkzeuge nach `.toolchains/`,
das Red-Alert-Quickinstall-Paket nach `Artifacts/content/`, baut
`Artifacts/xr-openra-quest3.apk` und installiert per ADB. Versionen zentral in `quest/lib.sh`.
Neue Build-Schritte immer in diese Skripte und in BUILD-QUEST.md eintragen, nicht nur lokal ausführen.

## Planung und Verlauf

- [CHANGELOG.md](CHANGELOG.md): Updatelog je Version (gebaut vs. im Headset bestätigt trennen).
- [BACKLOG.md](BACKLOG.md): offene Aufgaben mit IDs (TEST-, UX-, FLOW-, PERF- …) und Abnahmekriterien.
- [docs/xr/VR-MENUE-KONZEPT.md](docs/xr/VR-MENUE-KONZEPT.md): VR-Reiter im Originalmenü, Rechtsrahmen, Ideen.
- Testaufnahmen auswerten: Skill `xr-bug-intake`, Berichte nach `docs/bugs/`.

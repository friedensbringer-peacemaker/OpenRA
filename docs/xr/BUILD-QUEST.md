# xr-openra bauen und auf die Quest 3 installieren

Diese Anleitung ist für jede Person gedacht, die das Projekt frisch klont. Alle
Werkzeuge und Daten lädt das Projekt selbst. Admin-Rechte oder ein vorher
installiertes Android Studio sind nicht nötig.

| | |
|---|---|
| App-ID (Android-Paket) | `xr.openra` |
| Name im Quest-Menü | `xr-openra` |
| Ergebnis | `Artifacts/xr-openra-quest3.apk` |
| Werkzeuge | `.toolchains/` im Checkout (gitignored, ca. 6 GB) |
| Red-Alert-Daten | `Artifacts/content/ra-quickinstall.zip` (gitignored, 13 MB) |

## Voraussetzungen

- **Windows 10/11:** [Git für Windows](https://git-scm.com/download/win). Es bringt
  Git Bash mit, in dem alle Skripte laufen.
- **macOS / Linux:** `git`, `curl`, `unzip`, `bash`.
- Eine Meta Quest 3 mit aktiviertem **Entwicklermodus** (Meta-Horizon-App →
  Gerät → Entwicklermodus) und ein USB-C-Datenkabel.
- Etwa 7 GB freier Speicher und Internet für den ersten Lauf.

## Schnellstart

```sh
git clone https://github.com/friedensbringer-peacemaker/XR-OpenRA.git
cd XR-OpenRA
```

Windows: `Quest-Build.cmd` doppelklicken. macOS/Linux:

```sh
bash quest/all.sh
```

Der erste Lauf dauert je nach Leitung 15 bis 40 Minuten, spätere Läufe nur wenige
Minuten. Bereits Geladenes wird übersprungen. Beim ersten USB-Kontakt fragt die Quest
im Headset nach **„USB-Debugging zulassen“**; das bestätigen (am besten „Immer
zulassen“).

`bash quest/all.sh --no-install` (bzw. `Quest-Build.cmd --no-install`) baut nur die
APK, ohne ein Headset anzusprechen.

## Was die Skripte tun

| Schritt | Skript | Inhalt |
|---|---|---|
| 1 | `quest/setup-toolchains.sh` | Lädt nach `.toolchains/`: Microsoft OpenJDK 17, .NET-10-SDK (`dotnet-install`) mit Android-Workload, Android-cmdline-tools und per `sdkmanager` `platform-tools` (adb), `platforms;android-36`, `build-tools;36.0.0`, `ndk;27.0.12077973`, `cmake;3.22.1` (inkl. Ninja). Danach Khronos-OpenXR-SDK 1.1.58 und Android-Loader (Commit bzw. SHA-1 geprüft). Die Android-SDK-Lizenzen werden dabei akzeptiert. |
| 2 | `quest/fetch-ra-content.sh` | Lädt das Red-Alert-„Quick Install“-Paket über OpenRAs offizielle [Mirrorliste](https://www.openra.net/packages/ra-quickinstall-mirrors.txt) und prüft die SHA-1 aus `mods/ra-content/installer/downloads.yaml`. Das ist dasselbe Paket, das auch der Desktop-OpenRA-Installer anbietet. |
| 3 | `quest/build.sh` | Baut die native OpenXR-Brücke (NDK + CMake) und die .NET-Android-App, prüft Manifest, Bibliotheken und Signatur und kopiert das Ergebnis nach `Artifacts/xr-openra-quest3.apk`. |
| 4 | `quest/install.sh` | Installiert die APK per `adb install -r`, spielt die Red-Alert-Daten per `run-as` in den privaten App-Speicher (`files/Content/ra/v2`) und startet die App. Vorhandene Daten bleiben erhalten; `REIMPORT=1` erzwingt einen neuen Import. Deinstalliert nichts. |

Alle Versionen stehen zentral in `quest/lib.sh`. Pfade lassen sich mit
`OPENRA_TOOLCHAINS`, `OPENRA_ARTIFACTS` und `ANDROID_SERIAL` (bei mehreren
Geräten) überschreiben.

## Im Headset

Nach dem Start lädt die App die lokale Red-Alert-Partie (etwa 10–15 s) und öffnet
dann automatisch die XR-Spielfläche. Bedienung: siehe
[OpenXR-Probe-README](../../OpenRA.Quest.XrProbe/README.md#quest-bedienung-ab-024-preview)
und [Steuerungskonzept](CONTROLS.md). Die Controller müssen verbunden und
aktualisiert sein, sonst fängt Horizon OS den Start mit einem Systemdialog ab.

Logs und Screenshots für Fehlerberichte:

```sh
bash OpenRA.Quest.XrProbe/scripts/smoke-quest-xr.sh capture Artifacts/quest-capture
```

(`ADB_BIN=.toolchains/android-sdk/platform-tools/adb` voranstellen, falls `adb` nicht im `PATH` ist.)

## Rechtliches

OpenRA steht unter GPLv3. Die Red-Alert-Originaldaten gehören EA. Sie werden nur
lokal geladen und landen weder im Git-Repository noch in der APK. Eine gebaute
APK mit importierten Daten nicht weitergeben. Siehe [OpenRA Legal](https://www.openra.net/legal/).

## Fehlerbehebung

- **App startet nach `install.sh` nicht / nur Controller-Dialog:** Liegt die Quest
  ungenutzt (schlafend) oder sind die Controller inaktiv, fängt Horizon OS den
  Start mit `LaunchCheckControllerRequiredDialogActivity` ab. Headset aufsetzen,
  Controller aufwecken und `xr-openra` in der App-Bibliothek unter
  **„Unbekannte Quellen“** starten. Die Installation selbst ist dann bereits fertig.
- **„Keine Quest per ADB gefunden“:** Kabel prüfen (Datenkabel!), Headset aufsetzen
  und die USB-Debugging-Abfrage bestätigen, dann `quest/install.sh` erneut starten.
- **Alte Testversion `com.friedensbringer.openra.questprobe`:** Sie ist eine eigene
  App mit eigenem Datenordner und kann parallel installiert bleiben oder in den
  Quest-Einstellungen unter „Apps“ entfernt werden.
- **Build nach Werkzeug-Update kaputt:** `.toolchains/` löschen und `quest/all.sh`
  neu starten. Die Downloads werden dann frisch geholt.

# Android-ARM64-Probe

`OpenRA.Quest.Probe` ist ein Android-Diagnoseprojekt im Fork. Es bindet `OpenRA.Game`, `OpenRA.Mods.Common`, `OpenRA.Mods.Cnc` und `OpenRA.Platforms.Default` ein, richtet OpenRAs Android-Dateipfade und Einstellungen ein und prüft `TabletopPointer.TryMapRay`. Die APK enthält die im OpenRA-Quellrepo liegenden Mod-Dateien aus `mods/common` und `mods/ra`, die GLSL-Shader, OpenRAs globale MIX-Dateinamen-Datenbank sowie die Testkarte `blitz.oramap`, aber keine separaten originalen EA-MIX-Dateien. Über OpenRAs `InstalledMods`, `ModData`, `Ruleset` und `Map` lädt die Probe echte Red-Alert-Regeln und die Karte. Sie prüft Terrain, Sprites, acht Schriften und UI-Komposition zunächst einzeln. Mit separat importierten Originaldaten initialisiert sie anschließend die Editor-Welt und eine reguläre Spielwelt mit lokalem Spieler. Der aktuelle Quellstand versucht nach den Diagnoseframes eine fortlaufende lokale Spielsession auf derselben GLES-Oberfläche zu starten und leitet Berührungen als Auswahl- und Kontextklicks an OpenRAs `IInputHandler` weiter. Diese Integration ist kompiliert, aber noch nicht auf der Quest geprüft. Eine bestätigte bedienbare Partie oder OpenXR-Sitzung bietet die Probe noch nicht.

Am 25. September 2026 wurde mit .NET 10 und der offiziellen Android-Workload ein signiertes Debug-APK für `android-arm64` gebaut und per ADB auf einer Quest 3 gestartet. Der Geräte-Log meldet **307 Akteure und 89 Waffen** aus den Red-Alert-Regeln sowie die Karte **„Blitz“, 98×98, Tileset SNOW**. Danach bleibt die App geöffnet und zeigt eine aus OpenRAs Terrain-Typen erzeugte Karte. Sie speichert `terrain-preview.png`, `gles-terrain-preview.png` und `openra-terrain-preview.png` sowie `openra-renderer-ui-preview.png` und `openra-renderer-world-preview.png` im app-internen Dateiverzeichnis. Die letzten beiden Bilder stammen aus einem echten OpenRA-`Renderer`-Frame: Das UI-Bild zeigt mit `SpriteFont` geschriebenen Text und das echte Modicon, das Weltbild die Gelände-Farbkarte und einen synthetischen gelben Sprite. Mit getrennt installierten Red-Alert-Daten speichert die Probe zusätzlich `openra-authentic-terrain-preview.png`: **247 originale Karten-Tiles und drei Panzer-Sprites** wurden dabei auf der Quest mit OpenRAs Sprite-Renderer und den Originalpaletten gezeichnet. Die drei Panzer stammen inzwischen aus `Map.Sequences.LoadSprites()` und `GetSequence("1tnk", "idle")`, also dem regulären OpenRA-Animationspfad. Die Bilder wurden ausgelesen und visuell geprüft. Die APK und Originaldaten werden nicht im Git-Repository eingecheckt.

Der Quest-Treiber meldet **OpenGL ES 3.2** und alle vier vom OpenRA-GLES-Pfad abgefragten Erweiterungen. Die Probe löst **74 von 74** im GLES-Profil genutzten OpenRA-GL-Funktionen direkt aus `libGLESv3.so` beziehungsweise über `eglGetProcAddress` auf. OpenRAs vorhandener `OpenGL`-Wrapper initialisiert damit ohne SDL als Profil `Embedded`. Ein Upload/Readback mit OpenRAs `Texture` lieferte **0 abweichende Bytes**, der `combined`-Shader kompiliert und verknüpft, und ein mit OpenRAs Puffer- und Framebufferklassen gezeichneter Testquad ergab den erwarteten Pixel. Die Gelände-Farbkarte wurde danach aus **19.208 Dreiecken** mit denselben OpenRA-Grafikklassen gerendert. Ein diagnostischer `IPlatformWindow`-/`IGraphicsContext`-Adapter erlaubt OpenRAs `Renderer.BeginWorld`, `BeginUI` und `EndFrame` auf der Android-eigenen GLES-Oberfläche. Auf der Quest zeichnet inzwischen auch der vollständige `WorldRenderer` mit Terrain- und Actor-Traits sowie Schattierung und Paletten die Karte: Der Editor-Frame zeigt Schnee, Straßen, Bäume und ein Gebäude aus `blitz.oramap`. Die reguläre Welt initialisiert vier `Player`-Objekte und erreicht `World.LoadComplete`, `PostLoadComplete` und den ersten Tick. Eingaben, fortlaufende Simulation und XR-Ausgabe fehlen noch.

Der erste Gerätebuild stürzte vor dem Managed-Code mit `UnsatisfiedLinkError` für `MainActivity.n_onCreate` ab. Eine unveränderte .NET-Android-Vorlagen-App lief auf derselben Quest. Ursache war eine globale OpenRA-Build-Vorgabe, die `Optimize=true` für die Probe beibehielt und keine debuggable App erzeugte. Die Probe setzt deshalb für `Debug` ausdrücklich `Optimize=false`, `DebugSymbols=true` und `DebugType=portable`; außerdem bettet sie alle Assemblies in die APK ein und deaktiviert Fast Deployment. `ObjectCreator` lädt auf Android die eingebetteten Mod-Assemblies per Namen, wenn keine einzelne DLL-Datei vorhanden ist. Vor `ModData` müssen wie beim Desktopstart `Game.Settings` und die OpenRA-Log-Kanäle initialisiert werden. Der Fehler ist behoben; der spätere Gerätetest erreichte auch den ersten regulären Simulationstick. Eine steuerbare Spielschleife und OpenXR fehlen weiter.

## Reproduzierbarer Build

Benötigt werden das .NET 10 SDK, `dotnet workload install android`, Android-SDK und JDK. Die [offizielle .NET-Android-Anleitung](https://learn.microsoft.com/en-us/dotnet/android/getting-started/installation/dependencies) beschreibt das Ziel `InstallAndroidDependencies`. Im folgenden Beispiel liegen die Toolchains außerhalb des Git-Checkouts im übergeordneten XR-Arbeitsbereich:

```sh
cd OpenRA
ANDROID_SDK_ROOT="$PWD/../.toolchains/android-sdk"
JAVA_HOME="$PWD/../.toolchains/jdk"
ANDROID_SETTINGS_DIR="$PWD/../.toolchains/android-settings"

dotnet build OpenRA.Quest.Probe/OpenRA.Quest.Probe.csproj \
  -t:InstallAndroidDependencies -f net10.0-android \
  -p:AndroidSdkDirectory="$ANDROID_SDK_ROOT" \
  -p:JavaSdkDirectory="$JAVA_HOME" \
  -p:AcceptAndroidSDKLicenses=True

dotnet build OpenRA.Quest.Probe/OpenRA.Quest.Probe.csproj \
  -p:AndroidSdkDirectory="$ANDROID_SDK_ROOT" \
  -p:JavaSdkDirectory="$JAVA_HOME" \
  -p:AppSettingsDirectory="$ANDROID_SETTINGS_DIR" \
  -p:AndroidPackageFormat=apk
```

Die Debug-APK liegt danach unter `OpenRA.Quest.Probe/bin/Debug/net10.0-android/android-arm64/` und trägt `-Signed.apk` im Namen. `AppSettingsDirectory` hält den Debug-Signaturschlüssel außerhalb des macOS-Benutzerprofils. Für eine echte Veröffentlichung wäre ein eigener Release-Schlüssel nötig. Der am 26. September kompilierte Stand mit `QuestGameSession` hat noch keinen Quest-Gerätetest; ein erfolgreicher Build belegt hier nur die Android-Kompilierbarkeit.

Der Geräte-Smoke-Test kann mit `adb install -r <Signed.apk>`, `adb shell monkey -p com.friedensbringer.openra.questprobe -c android.intent.category.LAUNCHER 1` und `adb logcat -d -s OpenRA.Quest.Probe:I` wiederholt werden. Im Log müssen `Red-Alert-Regeln: 307 Akteure, 89 Waffen.`, `Karte: Blitz, 98x98, Tileset SNOW.`, `OpenRA-GL-Binding:`, `OpenRA-Textur-Roundtrip:`, `OpenRA-Combined-Shader:`, `OpenRA-Combined-Draw:`, `OpenRA-Kartenbild:`, `OpenRA-Renderer-UI:` und `OpenRA-Renderer-Welt:` erscheinen. Mit importierten Originaldaten folgen `OpenRA Editor World.LoadComplete`, `OpenRA-Editor-Renderer`, `OpenRA Regular World.LoadComplete` und `OpenRA Regular:` mit einer Tickzahl. Die direkte Anzeige, der Mehrtick-Pfad, die fortlaufende `QuestGameSession` und die Touch-Eingaben sind kompiliert, aber nach dem ADB-Verbindungsverlust noch nicht auf der Quest geprüft. Für die Session wären `Fortlaufende lokale OpenRA-Spielsession initialisiert.` und fortgesetzte Frames ohne Fehlermeldung zu erwarten. Die Android-Tasten unter der Spielfläche schalten Touch-Ereignisse zwischen linkem Auswahlklick und rechtem Kontextbefehl um; auch dieser Bedienpfad ist noch ungetestet. Wenn die Quest schläft, wird die Android-Grafikoberfläche nicht gerendert; `adb shell input keyevent KEYCODE_WAKEUP` weckt sie für diesen Test.

## Originaldaten für den lokalen Gerätetest

OpenRAs [Content-Installer-Konfiguration](../../mods/ra-content/installer/downloads.yaml) verweist für `quickinstall` auf ein Archiv mit SHA-1 `44241f68e69db9511db82cf83c174737ccda300b`. Das Archiv kann über die von OpenRA angegebene [Mirrorliste](https://www.openra.net/packages/ra-quickinstall-mirrors.txt) bezogen werden. Nach Installation der **Debug-APK** importiert das getestete Skript die geprüften Inhalte in deren privaten App-Speicher:

```sh
ADB_BIN=/pfad/zum/adb OpenRA.Quest.Probe/scripts/import-ra-content.sh /pfad/zu/ra-quickinstall.zip
```

Das Skript prüft die SHA-1, überträgt das Archiv temporär per ADB, entpackt es mit `run-as` nach `files/Content/ra/v2` und entfernt die temporäre Kopie. `run-as` funktioniert nur mit einer debugfähigen App. Eine installierbare Endnutzer-Version braucht einen eigenen Inhaltsimport in der App; bloßes Sideloading der APK liefert noch keine Spieldaten. Die heruntergeladenen Dateien werden weder in die APK noch in Git übernommen. OpenRA beschreibt die Rechte an diesen Originaldaten getrennt von der GPL-Engine auf seiner [Legal-Seite](https://www.openra.net/legal/).

Erst mit der zusätzlich in die APK aufgenommenen Datei `global mix database.dat` konnte OpenRAs MIX-Lader auch Namen wie `snow.pal` in den importierten Archiven auflösen. Der anschließende Gerätetest öffnete `snow.pal`, `temperat.pal`, die Schnee-Tilebilder und `1tnk.shp` erfolgreich. Ein späterer Gerätetest startete zusätzlich den vollständigen `WorldRenderer` und speicherte `openra-game-world-preview.png` aus der Editor-Welt sowie `openra-regular-world-preview.png` aus der regulären Spielwelt. Der erste reguläre Frame war wegen des unerforschten Kartenbereichs schwarz; die Kamera wurde auf den Spielerstartpunkt korrigiert, dessen Gerätetest steht noch aus.

OpenRAs Desktop-Grafikcode verwendet bereits GLES-Shader, beschafft Funktionszeiger jedoch normalerweise über `SDL_GL_GetProcAddress`. Ein neuer Überladungspunkt in `OpenGL.Initialize` akzeptiert einen fremden Funktions- und Erweiterungsresolver; der Desktopaufruf nutzt weiterhin SDL. Auch die vorhandenen OpenRA-NuGet-Pakete für SDL2, FreeType und OpenAL enthalten in diesem Checkout keine Android-ARM64-Bibliotheken. Der diagnostische Android-Adapter nutzt OpenRAs Grafikklassen und `Renderer`, überlässt EGL-Kontext und Präsentation aber `GLSurfaceView`. `AndroidFont` rastert die von OpenRA übergebenen TTF-Daten mit Androids `Typeface`, `Paint` und `Canvas` in `FontGlyph`-Alphadaten. OpenRAs normale `Renderer.InitializeFonts`-Routine konnte damit alle acht Red-Alert-Schriften auf der Quest anlegen und die Schrift `Regular` sichtbar zeichnen. Der Welt-Diagnosepfad nutzt einen stummen `DummySoundEngine`; Ton, Cursor und Eingaben sind noch nicht implementiert.

## Nächste technische Schritte

1. Den regulären Weltlauf mit lokalem Spieler am Startpunkt sichtbar machen und mehrere Simulationsticks samt echten Einheiten prüfen.
2. Den Android-Plattformadapter um Eingaben und kontinuierliche Darstellung erweitern; Auswahl und einen Bewegungsbefehl über OpenRAs vorhandene Order-Pipeline nachweisen.
3. Die Welttextur auf eine stereoskopische Brettfläche legen, Controllerposen an `TabletopPointer` anschließen und die [Bedienung](CONTROLS.md) im Spiel testen.

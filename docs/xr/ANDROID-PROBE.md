# Android-ARM64-Probe

`OpenRA.Quest.Probe` ist ein kleines Android-Projekt im Fork. Es bindet `OpenRA.Game`, `OpenRA.Mods.Common`, `OpenRA.Mods.Cnc` und `OpenRA.Platforms.Default` ein, richtet OpenRAs Android-Dateipfade und Einstellungen ein und prüft `TabletopPointer.TryMapRay`. Die APK enthält die im OpenRA-Quellrepo liegenden Mod-Dateien aus `mods/common` und `mods/ra`, die GLSL-Shader sowie die kleine Testkarte `blitz.oramap`, aber keine separaten originalen EA-MIX-Dateien. Über OpenRAs `InstalledMods`, `ModData`, `Ruleset` und `Map` lädt die Probe echte Red-Alert-Regeln und die Karte. Sie zeigt die Geländetyp-Farben in einer normalen 2D-Android-Ansicht, in einem einfachen GLES-Renderer und über OpenRAs `Renderer` mit Weltpuffer und UI-Komposition. Ein selbst erzeugter Markierungs-Sprite prüft den Welt-Sprite-Pfad ohne fremde Spieldaten. OpenRAs `Renderer.InitializeFonts` lädt alle acht Red-Alert-Schriften über den Android-Fontadapter; Text wird mit `SpriteFont` gezeichnet. Das im Mod enthaltene `ra|icon.png` wird über OpenRAs Dateisystem, PNG-Decoder, SheetBuilder und UI-Sprite-Renderer sichtbar ausgegeben. Die Probe startet weder ein Gefecht noch eine OpenXR-Sitzung; echte Terrain-Tiles und Einheitensprites fehlen weiterhin.

Am 25. September 2026 wurde mit .NET 10 und der offiziellen Android-Workload ein signiertes Debug-APK für `android-arm64` gebaut und per ADB auf einer Quest 3 gestartet. Der Geräte-Log meldet **307 Akteure und 89 Waffen** aus den Red-Alert-Regeln sowie die Karte **„Blitz“, 98×98, Tileset SNOW**. Danach bleibt die App geöffnet und zeigt eine aus OpenRAs Terrain-Typen erzeugte Karte. Sie speichert `terrain-preview.png`, `gles-terrain-preview.png` und `openra-terrain-preview.png` sowie `openra-renderer-ui-preview.png` und `openra-renderer-world-preview.png` im app-internen Dateiverzeichnis. Die letzten beiden Bilder stammen aus einem echten OpenRA-`Renderer`-Frame: Das UI-Bild zeigt mit `SpriteFont` geschriebenen Text und das echte Modicon, das Weltbild die Gelände-Farbkarte und einen synthetischen gelben Sprite. Die Bilder wurden ausgelesen und visuell geprüft. Die APK wird bewusst nicht im Git-Repository eingecheckt.

Der Quest-Treiber meldet **OpenGL ES 3.2** und alle vier vom OpenRA-GLES-Pfad abgefragten Erweiterungen. Die Probe löst **74 von 74** im GLES-Profil genutzten OpenRA-GL-Funktionen direkt aus `libGLESv3.so` beziehungsweise über `eglGetProcAddress` auf. OpenRAs vorhandener `OpenGL`-Wrapper initialisiert damit ohne SDL als Profil `Embedded`. Ein Upload/Readback mit OpenRAs `Texture` lieferte **0 abweichende Bytes**, der `combined`-Shader kompiliert und verknüpft, und ein mit OpenRAs Puffer- und Framebufferklassen gezeichneter Testquad ergab den erwarteten Pixel. Die Gelände-Farbkarte wurde danach aus **19.208 Dreiecken** mit denselben OpenRA-Grafikklassen gerendert. Ein diagnostischer `IPlatformWindow`-/`IGraphicsContext`-Adapter erlaubt nun OpenRAs `Renderer.BeginWorld`, `BeginUI` und `EndFrame` auf der Android-eigenen GLES-Oberfläche. Die Welt-zu-UI-Komposition und `WorldRgbaSpriteRenderer` wurden auf dem Gerät sichtbar geprüft. Das beweist noch nicht, dass `WorldRenderer` mit echten Tiles oder ein Spiel ohne weitere Änderungen startet.

Der erste Gerätebuild stürzte vor dem Managed-Code mit `UnsatisfiedLinkError` für `MainActivity.n_onCreate` ab. Eine unveränderte .NET-Android-Vorlagen-App lief auf derselben Quest. Ursache war eine globale OpenRA-Build-Vorgabe, die `Optimize=true` für die Probe beibehielt und keine debuggable App erzeugte. Die Probe setzt deshalb für `Debug` ausdrücklich `Optimize=false`, `DebugSymbols=true` und `DebugType=portable`; außerdem bettet sie alle Assemblies in die APK ein und deaktiviert Fast Deployment. `ObjectCreator` lädt auf Android die eingebetteten Mod-Assemblies per Namen, wenn keine einzelne DLL-Datei vorhanden ist. Vor `ModData` müssen wie beim Desktopstart `Game.Settings` und die OpenRA-Log-Kanäle initialisiert werden. Dies ist ein erfolgreicher Daten-/Karten-Smoke-Test, noch kein Spiel- oder OpenXR-Test.

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

Die Debug-APK liegt danach unter `OpenRA.Quest.Probe/bin/Debug/net10.0-android/android-arm64/` und trägt `-Signed.apk` im Namen. `AppSettingsDirectory` hält den Debug-Signaturschlüssel außerhalb des macOS-Benutzerprofils. Für eine echte Veröffentlichung wäre ein eigener Release-Schlüssel nötig.

Der Geräte-Smoke-Test kann mit `adb install -r <Signed.apk>`, `adb shell monkey -p com.friedensbringer.openra.questprobe -c android.intent.category.LAUNCHER 1` und `adb logcat -d -s OpenRA.Quest.Probe:I` wiederholt werden. Im Log müssen `Red-Alert-Regeln: 307 Akteure, 89 Waffen.`, `Karte: Blitz, 98x98, Tileset SNOW.`, `OpenRA-GL-Binding:`, `OpenRA-Textur-Roundtrip:`, `OpenRA-Combined-Shader:`, `OpenRA-Combined-Draw:`, `OpenRA-Kartenbild:`, `OpenRA-Renderer-UI:` und `OpenRA-Renderer-Welt:` erscheinen. Wenn die Quest schläft, wird die Android-Grafikoberfläche nicht gerendert; `adb shell input keyevent KEYCODE_WAKEUP` weckt sie für diesen Test.

OpenRAs Desktop-Grafikcode verwendet bereits GLES-Shader, beschafft Funktionszeiger jedoch normalerweise über `SDL_GL_GetProcAddress`. Ein neuer Überladungspunkt in `OpenGL.Initialize` akzeptiert einen fremden Funktions- und Erweiterungsresolver; der Desktopaufruf nutzt weiterhin SDL. Auch die vorhandenen OpenRA-NuGet-Pakete für SDL2, FreeType und OpenAL enthalten in diesem Checkout keine Android-ARM64-Bibliotheken. Der diagnostische Android-Adapter nutzt OpenRAs Grafikklassen und `Renderer`, überlässt EGL-Kontext und Präsentation aber `GLSurfaceView`. `AndroidFont` rastert die von OpenRA übergebenen TTF-Daten mit Androids `Typeface`, `Paint` und `Canvas` in `FontGlyph`-Alphadaten. OpenRAs normale `Renderer.InitializeFonts`-Routine konnte damit alle acht Red-Alert-Schriften auf der Quest anlegen und die Schrift `Regular` sichtbar zeichnen. Ton, Cursor und Eingaben sind noch nicht implementiert. Für den eigentlichen Spielstart müssen diese Pfade sowie der vollständige `WorldRenderer` geprüft werden.

## Nächste technische Schritte

1. Den diagnostischen Android-Plattformadapter um Eingaben erweitern; danach eine Karte mit `WorldRenderer` und echten Terrain-/Einheitensprites ausgeben. Der bisherige Welt-Sprite ist nur eine selbst erzeugte Testmarkierung; die UI-Schrift verwendet bereits OpenRAs echte Fontdatei.
2. Eine kleine Spielsimulation mit der geladenen Karte starten und mindestens Auswahl und einen Bewegungsbefehl nachweisen.
3. Die Welttextur auf eine stereoskopische Brettfläche legen, Controllerposen an `TabletopPointer` anschließen und die [Bedienung](CONTROLS.md) im Spiel testen.

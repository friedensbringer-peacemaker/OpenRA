# Android-ARM64-Probe

`OpenRA.Quest.Probe` ist ein kleines Android-Projekt im Fork. Es bindet `OpenRA.Game`, `OpenRA.Mods.Common` und `OpenRA.Mods.Cnc` ein, richtet OpenRAs Android-Dateipfade und Einstellungen ein und prüft `TabletopPointer.TryMapRay`. Die APK enthält die im OpenRA-Quellrepo liegenden Mod-Dateien aus `mods/common` und `mods/ra` sowie die kleine Testkarte `blitz.oramap`, aber keine separaten originalen EA-MIX-Dateien. Über OpenRAs `InstalledMods`, `ModData`, `Ruleset` und `Map` lädt die Probe echte Red-Alert-Regeln und die Karte. Sie zeigt die Geländetyp-Farben als normale 2D-Android-Ansicht und schreibt das Ergebnis ins Android-Log. Sie startet weder ein Gefecht noch eine OpenXR-Sitzung.

Am 25. September 2026 wurde mit .NET 10 und der offiziellen Android-Workload ein signiertes Debug-APK für `android-arm64` gebaut und per ADB auf einer Quest 3 gestartet. Der aktuelle Geräte-Log meldet **307 Akteure und 89 Waffen** aus den Red-Alert-Regeln sowie die Karte **„Blitz“, 98×98, Tileset SNOW**. Danach bleibt die App geöffnet und zeigt eine aus OpenRAs Terrain-Typen erzeugte Karte. Die App speichert dieselbe Ansicht als `terrain-preview.png` im app-internen Dateiverzeichnis; sie wurde vom Gerät ausgelesen und visuell geprüft. Die APK wird bewusst nicht im Git-Repository eingecheckt.

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

Der Geräte-Smoke-Test kann mit `adb install -r <Signed.apk>`, `adb shell monkey -p com.friedensbringer.openra.questprobe -c android.intent.category.LAUNCHER 1` und `adb logcat -d -s OpenRA.Quest.Probe:I` wiederholt werden. Im Log müssen `Red-Alert-Regeln: 307 Akteure, 89 Waffen.`, `Karte: Blitz, 98x98, Tileset SNOW.` und `Red-Alert-Regeln und Testkarte funktionieren auf Android.` erscheinen.

## Nächste technische Schritte

1. Die derzeitige SDL/OpenGL-Plattformschicht für Android/OpenXR ergänzen und eine Karte mit OpenRAs echtem Renderer ausgeben. Die aktuelle Gelände-Farbansicht ist nur ein Diagnosebild ohne Sprites.
2. Eine kleine Spielsimulation mit der geladenen Karte starten und mindestens Auswahl und einen Bewegungsbefehl nachweisen.
3. Die Welttextur auf eine stereoskopische Brettfläche legen, Controllerposen an `TabletopPointer` anschließen und die [Bedienung](CONTROLS.md) im Spiel testen.

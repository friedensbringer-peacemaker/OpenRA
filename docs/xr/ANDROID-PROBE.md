# Android-ARM64-Probe

`OpenRA.Quest.Probe` ist ein kleines Android-Projekt im Fork. Es bindet die tatsächlichen Bibliotheken `OpenRA.Game`, `OpenRA.Mods.Common` und `OpenRA.Mods.Cnc` ein, prüft Android als Plattform, richtet das app-interne OpenRA-Datenverzeichnis ein, ruft `TabletopPointer.TryMapRay` auf und lädt das mitgelieferte freie Red-Alert-`mod.yaml` durch OpenRAs `InstalledMods`-/`Manifest`-Pfad. Danach löst `ObjectCreator` zwei Mod-Typen aus den im APK eingebetteten Assemblies auf. Ein erfolgreicher Start zeigt einen Status in einer normalen 2D-Android-Ansicht und schreibt ihn ins Android-Log. Es lädt noch keine Spielregeln, Karten oder Originaldaten und startet keine OpenXR-Sitzung.

Am 25. September 2026 wurde mit .NET 10 und der offiziellen Android-Workload ein signiertes Debug-APK für `android-arm64` gebaut. `apksigner verify` meldete gültige v2- und v3-Signaturen. Die APK wurde per ADB auf einer Quest 3 installiert und gestartet. Der erste Test wies Engine, Speicher, Projektion und Modmanifest nach. Ein zweiter Build mit direkten Projektverweisen auf beide Mod-Bibliotheken meldete auf demselben Gerät: `OpenRA.Game geladen. Android-Speicher, Tabletop, Red-Alert-Modmanifest und Mod-Assemblies funktionieren.` Das APK enthält keine ursprünglichen C&C-Spieldaten und ist bewusst nicht im Git-Repository eingecheckt.

Der erste Gerätebuild stürzte vor dem Managed-Code mit `UnsatisfiedLinkError` für `MainActivity.n_onCreate` ab. Eine unveränderte .NET-Android-Vorlagen-App lief auf derselben Quest. Ursache war eine globale OpenRA-Build-Vorgabe, die `Optimize=true` für die Probe beibehielt und keine debuggable App erzeugte. Die Probe setzt deshalb für `Debug` ausdrücklich `Optimize=false`, `DebugSymbols=true` und `DebugType=portable`; außerdem bettet sie alle Assemblies in die APK ein und deaktiviert Fast Deployment. `ObjectCreator` lädt auf Android die eingebetteten Mod-Assemblies per Namen, wenn keine einzelne DLL-Datei vorhanden ist. Dies ist ein erfolgreicher Android-/Bibliotheks-Smoke-Test, noch kein Spiel- oder OpenXR-Test.

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

Der Geräte-Smoke-Test kann mit `adb install -r <Signed.apk>`, `adb shell monkey -p com.friedensbringer.openra.questprobe -c android.intent.category.LAUNCHER 1` und `adb logcat -d -s OpenRA.Quest.Probe:I` wiederholt werden. Im Log muss die oben genannte Erfolgsmeldung einschließlich `Mod-Assemblies funktionieren` erscheinen.

## Nächste technische Schritte

1. Über das bereits geprüfte Modmanifest hinaus OpenRAs freie Regeldateien und eine Testkarte im Android-Startpfad laden, ohne geschützte Originaldaten in die APK aufzunehmen.
2. Die derzeitige SDL/OpenGL-Plattformschicht für Android/OpenXR ergänzen und die vorhandene Welttextur auf eine stereoskopische Brettfläche ausgeben.
3. Controllerposen an `TabletopPointer` anschließen und die [Bedienung](CONTROLS.md) im Spiel testen.

# Android-ARM64-Probe

`OpenRA.Quest.Probe` ist ein kleines Android-Projekt im Fork. Es bindet die tatsächliche `OpenRA.Game`-Bibliothek ein und ruft `TabletopPointer.TryMapRay` auf. Ein erfolgreicher Start zeigt einen Status in einer normalen 2D-Android-Ansicht. Es lädt noch keine OpenRA-Spielregeln oder Karten und startet keine OpenXR-Sitzung.

Am 25. September 2026 wurde mit .NET 10 und der offiziellen Android-Workload ein signiertes Debug-APK für `android-arm64` gebaut. `apksigner verify` meldete gültige v2- und v3-Signaturen. Auf dem Build-Rechner war kein Gerät per ADB verbunden; ein Start auf Quest 3 wurde daher noch nicht bestätigt. Das APK enthält keine ursprünglichen C&C-Spieldaten und ist bewusst nicht im Git-Repository eingecheckt.

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

## Nächste technische Schritte

1. Die signierte Probe auf einer verbundenen Quest 3 starten und den Status sowie Android-Logs prüfen.
2. OpenRAs Mod-Dateien und Regel-/Kartenladen in einem Android-Startpfad testen, ohne geschützte Originaldaten in die APK aufzunehmen.
3. Die derzeitige SDL/OpenGL-Plattformschicht für Android/OpenXR ergänzen und die vorhandene Welttextur auf eine stereoskopische Brettfläche ausgeben.
4. Controllerposen an `TabletopPointer` anschließen und die [Bedienung](CONTROLS.md) im Spiel testen.

# Nativer OpenXR-Quad-Test für Quest

Dieser getrennte Android-ARM64-Baustein prüft den offiziellen Khronos-OpenXR-Loader, die benötigten Android-/OpenGL-ES-Erweiterungen und das Headset-System. Er baut außerdem eine OpenXR-Session mit einer OpenGL-ES-Swapchain und zeigt ein farbiges 1,2 × 0,6 m großes Test-Quad 1,4 m vor der Startposition an. Das ist noch keine OpenRA-Spieloberfläche. Die bestehende `OpenRA.Quest.Probe` bleibt bis zur Geräteprüfung unverändert.

Der Quellcode baut gegen den offiziellen [Khronos OpenXR SDK Release 1.1.58](https://github.com/KhronosGroup/OpenXR-SDK/releases/tag/release-1.1.58) und den [Android-Loader 1.1.58](https://central.sonatype.com/artifact/org.khronos.openxr/openxr_loader_for_android/1.1.58). `prepare-openxr.sh` prüft SDK-Commit und Loader-SHA-1. SDK, AAR, extrahierte `.so` und Build-Ausgabe bleiben im lokalen `.toolchains`-Ordner außerhalb des Git-Repositories. Für den Build wird Android-NDK 27.0.12077973 benötigt.

```sh
OpenRA.Quest.XrProbe/scripts/prepare-openxr.sh
OpenRA.Quest.XrProbe/scripts/build-native.sh
```

Das Ergebnis ist `../.toolchains/openra-xr-probe-build/libopenra_xr_probe.so`. `java/com/friedensbringer/openra/xr/XrProbe.java` ist der Android-JNI-Einstieg. Die Java-Methode `inspect(Activity)` gibt einen kurzen Text mit der gefundenen OpenXR-Runtime oder der fehlerhaften API-Stufe zurück.

Die unabhängige Diagnose-APK ist inzwischen paketiert; sie kann ohne .NET-Android-Workload gebaut werden:

```sh
OpenRA.Quest.XrProbe/scripts/build-apk.sh
```

Standardausgabe: `../Artifacts/OpenRA-XR-Quad-Probe-untested.apk`. Die APK enthält die Java-Activity, beide ARM64-Bibliotheken, OpenXR-Manifestrechte und die immersive Intent-Kategorie. Die Activity startet nach `onResume` die OpenXR-Session auf einem eigenen Thread. Die Session wartet auf `READY`, zeichnet das Muster in erworbene Swapchain-Bilder und gibt sie als `XrCompositionLayerQuad` über `xrEndFrame` ab. Der Build prüft die APK-Signatur. **Der Code ist nur kompiliert, nicht auf der Quest ausgeführt.** Ob die Testfläche dort sichtbar ist, bleibt offen. Die bestehende OpenRA-2D-App wird von dieser Diagnose-APK weder ersetzt noch verändert.

# Nativer OpenXR-Quad-Test für Quest

Dieser getrennte Android-ARM64-Baustein prüft den offiziellen Khronos-OpenXR-Loader, die benötigten Android-/OpenGL-ES-Erweiterungen und das Headset-System. Er baut außerdem eine OpenXR-Session mit einer OpenGL-ES-Swapchain und platziert ein 1,2 × 0,6 m großes Quad einmalig 1,4 m vor der ersten gültigen Blickpose. Das ist noch keine OpenRA-Spieloberfläche. Die bestehende `OpenRA.Quest.Probe` bleibt bis zur Geräteprüfung unverändert.

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

Standardausgabe: `../Artifacts/OpenRA-XR-Quad-Probe-untested.apk`. Die APK enthält die Java-Activity, beide ARM64-Bibliotheken, OpenXR-Manifestrechte und die immersive Intent-Kategorie. Die Activity erzeugt ein RGBA-Testbild, übergibt es an `XrProbe.submitFrame(byte[])` und startet nach `onResume` die OpenXR-Session auf einem eigenen Thread. Die Session wartet auf `READY`, kopiert den aktuellen Bildstand in erworbene Swapchain-Bilder und gibt sie als `XrCompositionLayerQuad` über `xrEndFrame` ab. Ohne übergebenes Bild zeichnet sie das native Testmuster. Eine OpenXR-Action für die Zielpose des rechten Touch-Controllers projiziert dessen Strahl auf das Quad. Ein gelbes Quadrat zeigt den Trefferpunkt; beim Trigger wird es rot, bei der A-Taste blau. Bewegungen und Auswahl-Trigger werden als `POINTER_MOVE`, `POINTER_DOWN` und `POINTER_UP` in 1024 × 512-Koordinaten ab der linken oberen Bildecke an einen optionalen Java-Listener gemeldet. Die A-Taste erzeugt `POINTER_CONTEXT_DOWN` und `POINTER_CONTEXT_UP` für spätere Kontextbefehle. Bei Verlust des Treffers oder Sitzungsende werden gehaltene Tasten freigegeben. Noch wird kein Spielbefehl ausgelöst. Der Build prüft die APK-Signatur. **Der Code ist nur kompiliert, nicht auf der Quest ausgeführt.** Ob die Testfläche und der Controllercursor dort funktionieren, bleibt offen. Die bestehende OpenRA-2D-App wird von dieser Diagnose-APK weder ersetzt noch verändert.

`submitFrame` akzeptiert genau 1024 × 512 RGBA8-Pixel, zeilenweise ab der unteren linken Bildecke. Die JNI-Methode kopiert den Puffer in einen unveränderlichen Schnappschuss; der XR-Thread lädt ihn außerhalb des Übergabe-Locks in das jeweilige Swapchain-Bild. Der Pointer-Listener wird auf dem XR-Thread aufgerufen. Die optionale .NET-Brücke reiht seine Ereignisse für OpenRAs Spielthread ein und überträgt fertig komponierte OpenRA-Frames mit vorläufiger 15-Hz-Grenze. Die Auflösung ist noch fest; der GPU-Readback-Pfad ist auf Quest nicht gemessen. Für lokale Geometrie- und Eingabe-Übergangstests ohne Quest:

```sh
OpenRA.Quest.XrProbe/scripts/test-geometry.sh
```

Die .NET-Android-App kann optional **OpenRA und die OpenXR-Brücke in einer immersiven APK** bauen:

```sh
OpenRA.Quest.XrProbe/scripts/build-dotnet-package.sh
```

Das Skript prüft die ARM64-Bibliotheken, das OpenXR-Manifestrecht, die immersive Intent-Kategorie und die Signatur der kombinierten Debug-APK. Standardausgabe: `../Artifacts/OpenRA-Quest-XR-Combined-untested.apk`. Die Activity zeigt zunächst OpenRAs Android-Ansicht. Nach dem separaten Import der Red-Alert-Daten startet „XR-Fläche starten (Experiment)“ die native Session; OpenRAs Welt-/UI-Bild wird in das Quad-Format umgewandelt, und Controllerereignisse werden auf die Spieloberfläche zurückgerechnet. **Dieser kombinierte Ablauf wurde noch nicht auf Quest ausgeführt.** Ein mögliches Pausieren der Android-GL-Oberfläche beim Start der XR-Session sowie Bildrate, Latenz und Bedienung müssen am Gerät geprüft werden. Für den .NET-Build müssen die NuGet-Abhängigkeiten zuvor restauriert sein; das Skript verwendet `--no-restore`.

# Nativer OpenXR-Quad-Test für Quest

Dieser Android-ARM64-Baustein prüft den offiziellen Khronos-OpenXR-Loader, die benötigten Android-/OpenGL-ES-Erweiterungen und das Headset-System. Er baut außerdem eine OpenXR-Session mit einer OpenGL-ES-Swapchain und platziert ein 1,6 × 0,8 m großes Quad einmalig 1,2 m vor der ersten gültigen Blickpose. Die eigenständige Diagnose-APK zeigt ein Testbild; die kombinierte APK bindet die Fläche experimentell an OpenRAs Spielansicht an. Die kombinierte APK zeigte auf Quest 3 erstmals einen OpenRA-Frame im XR-Modus. Der erste sichtbare Versuch war wegen einer 4080 × 1866 Pixel großen Spielfläche auf dem 1024 × 512-Pixel-Brett zu klein. Version 0.2.1-preview rendert das Spiel mit 1280 × 640 Pixeln und vergrößert das Brett; diese Korrektur ist installiert, aber wegen des Quest-Dialogs „Controller erforderlich“ noch nicht erneut im Headset beurteilt. Die Controllerbedienung bleibt ungeprüft. Die eigenständige Diagnose-APK wurde bislang nur gebaut.

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

`submitFrame` akzeptiert genau 1024 × 512 RGBA8-Pixel, zeilenweise ab der unteren linken Bildecke. Die JNI-Methode kopiert den Puffer in einen unveränderlichen Schnappschuss; der XR-Thread lädt ihn außerhalb des Übergabe-Locks in das jeweilige Swapchain-Bild. Der Pointer-Listener wird auf dem XR-Thread aufgerufen. Die optionale .NET-Brücke reiht seine Ereignisse für OpenRAs Spielthread ein und überträgt fertig komponierte OpenRA-Frames mit vorläufiger 15-Hz-Grenze. Das OpenRA-Backend liest dafür nur den sichtbaren Teil der möglicherweise größeren Framebuffer-Textur; der 1024 × 512-Zielpuffer wird wiederverwendet. Die Auflösung ist noch fest und der synchrone GPU-Readback auf Quest nicht gemessen. In der kombinierten App wählt der rechte Trigger aus, A erteilt Kontextbefehle, gedrückter Griff zieht die Karte, der rechte Stick zoomt und gehaltenes B aktiviert additive Auswahl. Diese Belegung ist gebaut, aber am Controller noch nicht geprüft. Für lokale Geometrie- und Eingabe-Übergangstests ohne Quest:

```sh
OpenRA.Quest.XrProbe/scripts/test-geometry.sh
```

Die .NET-Android-App kann optional **OpenRA und die OpenXR-Brücke in einer immersiven APK** bauen:

```sh
OpenRA.Quest.XrProbe/scripts/build-dotnet-package.sh
```

Das Skript prüft die ARM64-Bibliotheken, das OpenXR-Manifestrecht, die immersive Intent-Kategorie und die Signatur der kombinierten Debug-APK. Standardausgabe: `../Artifacts/OpenRA-Tabletop-XR-Quest3-Preview.apk`. Die Activity zeigt zunächst OpenRAs Android-Ansicht. Nach dem separaten Import der Red-Alert-Daten startet die native XR-Session automatisch, sobald die lokale Red-Alert-Partie geladen ist und die Activity aktiv ist. Die Schaltfläche „XR-Fläche erneut starten (Experiment)“ steht oben im ersten Fenster für einen weiteren Versuch. OpenRAs Welt-/UI-Bild wird in das Quad-Format umgewandelt, und Controllerereignisse werden auf die Spieloberfläche zurückgerechnet. Die lokale OpenRA-Partie und die XR-Bildübergabe liefen auf Quest 3; die Lesbarkeit der korrigierten Auflösung, Bildrate, Latenz und Bedienung müssen noch im Headset geprüft werden. Das Build-Skript restauriert die .NET-Abhängigkeiten vor dem Paketbau.

Für den späteren Gerätetest installiert `scripts/smoke-quest-xr.sh install ../Artifacts/OpenRA-Tabletop-XR-Quest3-Preview.apk` die kombinierte APK und startet sie. Nach dem manuellen Import eigener Red-Alert-Daten und dem automatischen Startversuch der XR-Fläche sichert `scripts/smoke-quest-xr.sh capture` Logcat, Activity-Zustand, System-Screenshot und vorhandene OpenRA-Diagnosebilder. Installation und Log-Capture wurden auf einer verbundenen Quest 3 geprüft; ein erstes XR-Bild ist sichtbar, die Größenkorrektur muss noch im Headset geprüft werden. Die sichtbare Quad-Fläche muss zusätzlich im Headset beurteilt werden; ein Android-System-Screenshot muss sie nicht zwingend enthalten.

Beim Test muss die Quest wach und die App sichtbar sein. Ein ADB-Startbefehl kann auf einer schlafenden Quest ohne Activity-Startmeldung enden; in diesem Fall liefert das private `files/quest-diagnostics.log` keine neuen Einträge. Die lokale Partie war bei einem früheren Gerätetest nach 11–14 Sekunden geladen, ohne dass die damalige manuelle XR-Starttaste betätigt wurde. Der Autostart öffnete am 26.09.2026 eine sichtbare XR-Fläche; die anfänglich zu kleine Darstellung führte zur festen Spielauflösung 1280 × 640.

Die Quest-Test-APK ab Version `0.2-preview` verwendet die von Meta dokumentierten Activity-Einstellungen für immersive Anwendungen (Querformat, Vollbild, `singleTask`, Konfigurationswechsel und VR-Kategorie) und Android-Zielversion 32. Der native XR-Thread protokolliert Sitzungszustände und beendet einen ersten Startversuch mit einer Fehlermeldung, wenn 30 Sekunden lang kein `READY` eintrifft. Der Build prüft die entscheidenden Manifest-Felder direkt in der APK. Ob diese Korrekturen den im Headset sichtbaren Startfehler beheben, muss am wachen Gerät geprüft werden.

Beim ADB-Gerätetest am 26.09.2026 fing Horizon OS einen Start bereits **vor** `MainActivity.OnCreate` mit `LaunchCheckControllerRequiredDialogActivity` ab. Beide Controller waren laut Systemlog getrennt und als `update-required` markiert; der Fenstermanager meldete anschließend einen Platzierungs-Timeout. In diesem Zustand entstehen keine neuen `quest-diagnostics.log`-Einträge. Für den nächsten Headset-Test müssen die Controller verbunden, ein eventuelles Controller-Update abgeschlossen und der Systemdialog geschlossen sein. Dieser Systemzustand lässt sich nicht durch Änderungen am OpenRA-Ladecode beheben.

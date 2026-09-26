# Quest-3-Test der kombinierten APK: Teilbefund vom 26. September 2026

Dieser Test wurde auf einer per USB/ADB verbundenen Quest 3 begonnen. Die signierte kombinierte APK wurde als Update installiert; App-Daten wurden nicht gelöscht. **Das XR-Brett und die Controller wurden noch nicht getestet**, weil das Headset während des Tests einschlief und keine Beobachtung im Headset vorlag.

- Git-Stand der APK: `48eebc0` (`quest-tabletop-research`)
- APK: `Artifacts/OpenRA-Quest-XR-Combined-untested.apk` im übergeordneten Projektordner
- SHA-256: `60b50247cd9e7830cf687fc197e296a570846f612276a9ad55e90431b1042b44`
- Lokale Belege, nicht im Git-Fork: `Artifacts/Quest-Test-2026-09-26/` mit `xr-logcat.txt`, Activity-Zustand und OpenRA-Diagnosebildern. `quest-screen.png` hat 0 Byte; das Headset meldete zu diesem Zeitpunkt `Asleep`, die genaue Ursache des leeren System-Screenshots wurde nicht weiter geprüft.

| G0-Schritt | Beobachtung | Wertung |
| --- | --- | --- |
| Installation und Daten | `adb install -r` meldete `Success`. Die benötigten Red-Alert-Archive waren bereits im privaten App-Speicher. Die App meldete 307 Akteure, 89 Waffen und Blitz (98 × 98). | Installation/Daten/Regeln: **Pass**. |
| Android-Grafik | Nach ADB-Wakeup meldete die Quest GLES 3.2, 74 aufgelöste OpenRA-GL-Funktionen, korrekten Textur-/Framebuffer-Readback, acht Schriften sowie einen gezeichneten regulären OpenRA-Weltframe. Das gespeicherte `openra-regular-world-preview.png` zeigt den sichtbaren Startbereich mit Einheit. | Diagnose-Renderer: **Pass**. |
| Lokale Partie mit KI | Der alte Diagnosepfad schloss 30 Simulationsticks mit 120 Akteuren ab. Die neue fortlaufende `QuestGameSession` meldete `KI-Gegner normal auf Startfeld 20,37 aktiviert.` | Session-Initialisierung und Bot-Aktivierung: **Pass**. Fortlaufende Ticks: **unklar**, da vor dem Einschlafen keine wiederholte `OpenRA-Simulation`-Zeile erfasst wurde. |
| OpenXR-Brett und Controller | Keine `OpenXR-Session läuft`-, Quad- oder `XR-Bildübergabe`-Logs. Der XR-Startknopf wurde nicht betätigt. | **Nicht getestet**. |
| Pause/Fortsetzen | `Activity.OnResume` und danach `Activity.OnPause` wurden protokolliert; `dumpsys power` meldete anschließend `Asleep`. | Ein normaler Schlafübergang ist sichtbar; Wiederaufnahme der laufenden Partie und XR-Ressourcen sind **unklar**. |

Der nächste Test beginnt mit aufgesetzter/wacher Quest: unten die laufende Partie und steigende `OpenRA-Simulation`-Ticks prüfen, dann „XR-Fläche starten (Experiment)“ betätigen und Brett/Controller im Headset nach dem [G0-Protokoll](QUEST-TESTPROTOKOLL.md) beurteilen. Ein Logtreffer allein zählt nicht als sichtbares oder bedienbares XR-Brett.

## Späterer Headset-Test und Größenkorrektur

Am 26.09.2026 um 19:29 startete Version `0.2-preview` auf der Quest. Die Diagnose meldete eine lokale Partie bei `4080x1866`, eine initialisierte OpenXR-Session und um 19:30:07 die erste Übergabe eines OpenRA-Frames an das 1024 × 512-Pixel-Quad. Zwei Quest-Screenshots von 19:30:04 und 19:30:18 zeigen zunächst den Ladebildschirm und dann eine sehr kleine, über die schwarze Fläche verteilte OpenRA-Ansicht. Ein Absturz ist dabei nicht belegt; um 19:30:42 wurde die Activity normal pausiert und beendet.

Die Ursache für die winzige Darstellung ist die direkte Übernahme der über 4K breiten Android-Spielfläche als OpenRA-Fenster und die anschließende Verkleinerung auf das XR-Brett. `0.2.1-preview` fordert daher für die GL-Spielfläche `1280x640` an, rechnet die Touch-Eingaben passend um und setzt das Quad auf 1,6 × 0,8 m in 1,2 m Abstand. Die signierte APK wurde mit `adb install -r` installiert; der Build hatte keine Fehler. Ein anschließender ADB-Start wurde von Horizon OS vor `MainActivity.OnCreate` durch `LaunchCheckControllerRequiredDialogActivity` gestoppt. Die neue Rendergröße und Lesbarkeit sind deshalb **noch nicht am Gerät bestätigt**. Sobald die Controller verbunden und der Systemdialog geschlossen sind, muss `quest-diagnostics.log` `Android-Renderfläche: 1280x640` und `XR-Bildübergabe: … Quelle 1280x640` zeigen; danach einen Quest-Screenshot und die Bedienbarkeit beurteilen.

Nach dem Siedler-Abgleich wurde die zu flache 1280 × 640-Fassung durch `0.2.2-preview` mit **1280 × 800** ersetzt: OpenRAs `WorldViewportSizes.MinEffectiveResolution` verlangt mindestens 1024 × 720, und XR-Textur und Spiel-Framebuffer sind nun pixelgleich. Die Tafel misst 1,6 × 1,0 m bei 1,4 m Abstand. Der Controller-Strahlschnitt verwendet dieselben Maße. Diese Version benötigt noch einen Headset-Sichttest.

## Screenshot-Prüfung der installierten 0.2.2-preview um 20:06

Der Quest-Screenshot `com.friedensbringer.openra.questprobe-20260926-200654.jpg` und das App-Protokoll bestätigen eine `1280x800`-Renderfläche, eine gestartete lokale Partie und die Übergabe eines gleich großen Frames an OpenXR. Die anfängliche Einschätzung, das Bild sei aufrecht, war falsch. Die Red-Alert-Minimap liegt laut `mods/ra/chrome/ingame-player.yaml` bei `Y: 40` und damit **oberhalb** des Produktionsmenüs (`Y: 300`). Im Quest-Screenshot liegt sie darunter; auch Hammer und Sichel sind gegenüber `mods/ra/uibits/sidebar.png` vertikal gespiegelt. Dies belegt eine vertikale Umkehr der gesamten XR-Spieloberfläche. Die Geldanzeige allein war dafür kein verlässlicher Orientierungstest. OpenRA spiegelt den Zwischen-Framebuffer beim Zeichnen in das reguläre 2D-Fenster vertikal; die XR-Brücke hatte diesen Zwischenschritt übersprungen. Version `0.2.3-preview` kehrt deshalb die Quellzeilen bei der XR-Umwandlung um. Der native XR-Cursor bleibt im OpenGL-Koordinatensystem mit Ursprung unten links. Ein neuer Headset-Screenshot muss das Ergebnis bestätigen.

Der große schwarze Bereich entspricht der unerforschten Karte um den kleinen sichtbaren Startbereich; er ist kein Hinweis auf eine umgedrehte Textur. Die obere Menüleiste ist im Aufnahmeausschnitt teilweise abgeschnitten. Das Brett ist beim ersten gültigen Headset-Blick fest im Raum platziert und folgt späteren Kopfbewegungen nicht. Ob die Leiste wegen Kopfbewegung, anfänglicher Brettplatzierung oder Komfortgröße aus dem Sichtfeld geriet, lässt sich aus einem Standbild allein nicht sicher entscheiden. Rezentrierung und eine besser sichtbare Startansicht sind dafür die nächsten UI-Arbeiten.

## Korrektur-APK 0.2.3-preview

Die APK wurde nach der Zeilenkorrektur gebaut, signiert und mit `adb install -r` auf der Quest 3 installiert; die vorhandenen Red-Alert-Daten blieben erhalten. SHA-256: `5b196bde478812e542a6ce72dc279f5a34a1ccbc778c55bcbf26aaed5f7879f0`. Drei gezielte .NET-Konvertierungstests und die nativen Geometrie-/Pointertests bestanden. Die Quest meldete beim letzten ADB-Check `Asleep`; ein neuer Sichttest des XR-Quads ist deshalb noch offen. Erwartung: Minimap oberhalb des Produktionsmenüs, Hammer und Sichel wie in `mods/ra/uibits/sidebar.png`, Cursor am Controller-Treffpunkt.

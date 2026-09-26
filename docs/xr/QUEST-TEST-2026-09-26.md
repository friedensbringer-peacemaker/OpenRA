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

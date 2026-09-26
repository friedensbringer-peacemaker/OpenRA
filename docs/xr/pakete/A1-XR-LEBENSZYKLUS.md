# A1 – OpenXR- und Android-GL-Lebenszyklus stabilisieren

- Zuständig: Codex Astra
- Priorität: P0 nach G0
- Ausgangsstand bei Vorbereitung: Branch `quest-tabletop-research`, Commit `510f1a0a9081c38795997acdb60b4c224808dd0a`, sauberer Git-Status
- Startbedingung: G0 liefert Logcat und Headset-Befund zum XR-Start, zum laufenden Bild und zu Pause/Fortsetzen

## Auftrag

Prüfe den tatsächlichen Android-Activity-, `GLSurfaceView`- und OpenXR-Session-Lebenszyklus anhand der G0-Belege. Behebe den engsten bestätigten Fehler, der die gleichzeitige Simulation, wiederholten XR-Start oder sauberes Stoppen verhindert. Relevante Einstiegspunkte sind `OpenRA.Quest.Probe/MainActivity.cs`, `QuestXrBridge.cs`, `GlesProbeRenderer.cs` sowie `OpenRA.Quest.XrProbe/native/XrQuad.cpp`. Session-Tokens, `STOPPING`, JNI-Callback-Freigabe und Kontextbesitz müssen konsistent bleiben.

Vor G0 ist eine lesende Code-/Risikoanalyse möglich; ein Umbau des EGL-/XR-Pfads ohne Gerätebefund gehört nicht zur Implementierung. S1 arbeitet nur an der lokalen Spielsession. A1 und S2 dürfen nicht gleichzeitig dieselben Android-Einstiegspunkte ändern.

## Abnahme

Für den behobenen Fehler sind Auslöser, Vorher-/Nachher-Log, Codeänderung und passender Test belegt. In der kombinierten App bleiben Simulation und XR-Bild bei Start, Pause/Fortsetzen, Stop und erneutem Start aktiv beziehungsweise werden sauber freigegeben. Native Geometrie-/Pointertests und kombinierter Build bestehen; G1 wird erst nach Headset- und Logcat-Prüfung als erfüllt markiert.

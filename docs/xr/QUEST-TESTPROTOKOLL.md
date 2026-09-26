# Quest 3: Protokoll für den ersten kombinierten OpenRA-/OpenXR-Test

Stand: 26. September 2026. **Teilweise ausgeführt:** Installation, Android-Grafik und Initialisierung der KI-Session sind auf Quest belegt; [Teilbefund](QUEST-TEST-2026-09-26.md). Der XR-Start und die fortlaufende Bedienung stehen aus. Dieses Protokoll trennt die 2D-Spielsession, den XR-Bildpfad und die Controllerbedienung, damit ein Fehler einer Stufe zugeordnet werden kann. Die APK ist ein Experiment, keine bestätigte spielbare Quest-Version.

## Vorbereitung am späteren Testtag

1. Quest verbinden und aufwecken; `adb get-state` muss `device` melden. APK-Pfad, `git rev-parse HEAD` und SHA-256 der getesteten APK notieren. Ein neuer Build braucht ein neues Protokoll.
2. Nur selbst rechtmäßig vorhandene Red-Alert-Daten als OpenRA-Quickinstall-ZIP bereithalten. Die APK enthält diese Originaldaten nicht. Falls sie in der App bereits importiert sind, im Protokoll „bereits vorhanden“ vermerken.
3. Die kombinierte APK installieren und starten: `OpenRA.Quest.XrProbe/scripts/smoke-quest-xr.sh install ../Artifacts/OpenRA-Quest-XR-Combined-untested.apk` aus dem Repository. Das Skript verwendet `adb install -r`, stoppt die App und startet sie neu; bestehende private App-Daten werden dabei normalerweise beibehalten.
4. Nach jedem relevanten Fehlversuch und am Ende `OpenRA.Quest.XrProbe/scripts/smoke-quest-xr.sh capture /tmp/openra-quest-xr-g0` ausführen. Für mehrere Versuche je einen neuen Ausgabeordner wählen. Zusätzlich die Sicht im Headset und die eigenen Controlleraktionen handschriftlich notieren; ein Android-System-Screenshot muss das immersive Quad nicht enthalten.

## G0-Belegmatrix

| Schritt | Durchführung und erwartete Beobachtung | Prüfe im Beleg | Wertung |
| --- | --- | --- | --- |
| 1. App/Daten | App startet; Red-Alert-ZIP nötigenfalls über „Red-Alert-ZIP auswählen“ importieren. Danach ist „Red-Alert-Daten vorhanden“ zu sehen. | `xr-logcat.txt`: Regeln/Karte oder konkreter Importfehler; Screenshot der Android-Ansicht. | **Pass** nur bei erfolgreichem Import bzw. nachweislich vorhandenen Daten; sonst **Fail** mit Fehlermeldung. |
| 2. 2D-Spiel | Die untere OpenRA-Fläche zeigt eine fortlaufende Partie. Einige Sekunden warten und eine sichtbare Veränderung/Touch-Auswahl versuchen. | `Fortlaufende lokale OpenRA-Spielsession initialisiert. KI-Gegner ... aktiviert.` und wiederholte `OpenRA-Simulation: Tick ...` mit steigender Tickzahl sowie sichtbarer Weltframe. | **Pass** bei steigenden Ticks und sichtbarer fortlaufender Partie; **unklar**, wenn nur ein Startlog oder Standbild vorliegt. |
| 3. XR-Start/Bild | „XR-Fläche starten (Experiment)“ wählen. Im Headset ein räumliches Brett mit OpenRA-Bild suchen; Kopf bewegen und beobachten, ob es im Raum bleibt und sich das Spielbild aktualisiert. | `OpenXR-Session läuft`, `Quad relativ zur ersten gültigen Blickpose platziert`, wiederholte `XR-Bildübergabe: ... Frames` und weiterhin steigende `OpenRA-Simulation`-Ticks; Headset-Beobachtung zusätzlich zum Screenshot. | **Pass** nur bei sichtbarem, aktualisiertem Quad und fortlaufender Simulation; bei vorhandenem Log ohne sichtbares Brett **unklar/Fail** mit genauer Beobachtung. |
| 4. Controller | Mit rechtem Trigger eine Einheit auswählen und einen Auswahlrahmen ziehen; B für additive Auswahl halten; A auf freien Boden für Kontextbefehl; Griff ziehen für Pan; rechten Stick für Zoom bewegen. Jede Wirkung getrennt notieren. | `OpenRA.XrProbe`-Pointerereignisse zeigen Übergänge. Sichtbare Auswahl, Bewegung und Kartenausschnitt sind entscheidend; Logs allein belegen keine Spielwirkung. | Pro Aktion **Pass/Fail/unklar**. Bei fehlender Produktions-UI offen lassen statt Spielbarkeit zu behaupten. |
| 5. Lebenszyklus | Quest-Systemmenü öffnen, App pausieren und zurückkehren; danach prüfen, ob Bild und Steuerung weiterlaufen. Falls die XR-Session über das System endet, erneut starten. | `Activity.OnPause/OnResume`, Session-Fehler oder `STOPPING`/Startlogs, Headset-Bild und festhängende Tasten. | **Pass** nur ohne eingefrorenes Bild, Geisterklicks oder Absturz. Ein erneuter Start ist nur wertbar, wenn die Session vorher wirklich endete. |

Bei einem **Fail** die letzte funktionierende Stufe benennen und den vollständigen Ausgabeordner behalten. `activity-state.txt` hilft bei Activity-/Fokusfragen; die zwei OpenRA-Diagnose-PNGs zeigen nur die gespeicherten Android-Frames. Im Log nach `AndroidRuntime`, `OpenRA.Quest.Probe`, `OpenRA.XrProbe` und `XR-Bildübergabe` suchen.

## Auszufüllender Testvermerk

```text
Datum/Uhrzeit und Quest-Systemversion:
Commit / APK-SHA-256:
Red-Alert-Daten: neu importiert | bereits vorhanden | Import fehlgeschlagen
Schritt 1 (App/Daten): Pass | Fail | unklar; Beobachtung:
Schritt 2 (2D-Spiel/KI): Pass | Fail | unklar; Beobachtung:
Schritt 3 (XR-Bild): Pass | Fail | unklar; Beobachtung:
Schritt 4 (Trigger / B / A / Griff / Stick): je Pass | Fail | unklar; Beobachtung:
Schritt 5 (Pause/Fortsetzen/erneuter Start): Pass | Fail | unklar | nicht möglich; Beobachtung:
ADB-Ausgabeordner und Headset-Beobachtung:
Erster Fehler und nächster engster Fix:
```

## Grenzen des vorhandenen Capture-Skripts

[`smoke-quest-xr.sh`](../../OpenRA.Quest.XrProbe/scripts/smoke-quest-xr.sh) sammelt Logcat für die beiden App-Tags und `AndroidRuntime`, Activity-Zustand, System-Screenshot und verfügbare Diagnosebilder. Es erfasst weder die immersive Sicht zuverlässig noch kontinuierliche Framezeiten, Speichernutzung oder Temperatur. Der Tester muss die Headset-Beobachtung ergänzen; G4 braucht eine eigene Leistungsmessung. Vor G0 sollte das Skript keine automatischen Pass-Meldungen aus bloßen Logtreffern ableiten.

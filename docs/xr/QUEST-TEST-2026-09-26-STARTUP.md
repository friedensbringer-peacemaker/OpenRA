# Quest 3: Start- und ANR-Test am 26. September 2026

Die vorherige kombinierte APK zeigte zweimal „OpenRA Probe reagiert nicht“: zuerst beim synchronen Pausieren der Grafikfläche während langer Renderer-Diagnosen, dann beim ersten Fensteraufbau nach einem rund sechs Sekunden langen Laden von Dateien und Regeln auf dem UI-Thread. Beides waren echte Android-ANR-Ereignisse im Log.

Die aktualisierte App zeigt sofort „OpenRA wird geladen …“, lädt die OpenRA-Daten im Hintergrund und überspringt auf dem normalen Spielpfad die aufwendigen Diagnosebilder. Ein Grafik-Pausewunsch wird erst nach dem aktuellen Renderdurchgang verarbeitet. Die signierte APK wurde mit `adb install -r` installiert; die vorhandenen Red-Alert-Daten blieben erhalten.

- Test-APK: `../Artifacts/OpenRA-Quest-XR-Startup-Fix-untested.apk`
- SHA-256: `803452238dc4ef7f6015dfed8a1bee370941cb9060850af218d4e355eae1d12b`
- Gerät: Meta Quest 3, USB-ADB, Paket `com.friedensbringer.openra.questprobe`
- Start um 14:06:49: `Activity.OnResume`; um 14:06:55 waren 307 Akteure, 89 Waffen und die Karte Blitz (98 × 98) geladen.
- Um 14:07:03 meldete die App die fortlaufende lokale Partie mit KI-Gegner. Um 14:07:08 und 14:07:13 folgten Simulationstick 125 und 244.
- Für diesen Start enthielt der geprüfte Logabschnitt keine neue ANR-Meldung; der App-Prozess lief weiter. Die Android-Oberfläche meldete „Red-Alert-Partie läuft“ und zeigte die Tasten für Auswahl, Befehl, Kartenbewegung, Zoom, Mehrfachauswahl und „XR-Fläche starten (Experiment)“.

**Grenze des Tests:** Die laufende Simulation und die Bedienelemente sind technisch bestätigt. Ein sichtbares, räumliches OpenXR-Brett und Controller-Eingaben im Headset sind weiterhin nicht bestätigt. Der ADB-Tipp auf die XR-Taste lieferte keinen verlässlich zuordenbaren App-Status; kurz darauf war eine andere Quest-App im Vordergrund. Ein Test mit aufgesetztem Headset muss die tatsächliche XR-Darstellung und Bedienung prüfen. Für den ersten App-Start sind bis zur lokalen Partie etwa 15 Sekunden Ladezeit zu erwarten; währenddessen sollte die Ladeanzeige sichtbar bleiben.

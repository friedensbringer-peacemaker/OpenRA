# OpenRA Tabletop XR: Quest-3-Projekt

Dieser Zweig entwickelt einen eigenständigen OpenRA-Port auf Meta Quest 3. Eine spielbare Quest-Version existiert noch nicht; eine signierte Android-ARM64-Diagnose-APK wurde auf Quest 3 erfolgreich gestartet. Die Desktop-Spielbasis stammt aus dem [Upstream-Projekt](https://github.com/OpenRA/OpenRA).

- [Machbarkeitsprüfung](MACHBARKEIT-OPENRA-QUEST.md)
- [Quellenliste der Prüfung](sources.json)
- [XR-Backlog](BACKLOG.md) mit weiteren, eigenständigen Spieleideen
- [Quest-Eingabekonzept](CONTROLS.md) für Auswahl, Befehle und Bauleiste
- [Android-ARM64-Probe](ANDROID-PROBE.md) mit Buildanleitung und Grenzen des Starttests
- [OpenXR-Anschluss](OPENXR-UMSETZUNG.md) mit dem nächsten räumlichen Meilenstein und Gerätetests
- [Roadmap und Umsetzungspakete](ROADMAP-QUEST-TABLETOP.md) für das erste lokale Red-Alert-Gefecht auf Quest 3
- [Quest-Testprotokoll](QUEST-TESTPROTOKOLL.md) für den späteren kombinierten Gerätecheck
- [Quest-Teilbefund vom 26. September 2026](QUEST-TEST-2026-09-26.md) zu Installation, Grafik und KI-Session
- [Start- und ANR-Test](QUEST-TEST-2026-09-26-STARTUP.md) mit bestätigten fortlaufenden Simulationsticks
- [Neuinstallation und Diagnose-Log](QUEST-FRESH-INSTALL-2026-09-26.md) mit Ladezeit und wiederholtem App-Start

Der nächste große Meilenstein ist ein auf Quest 3 bestätigter Spielablauf auf einer räumlichen Fläche. OpenRAs vollständiger `WorldRenderer` hat die Blitz-Karte auf Quest 3 gezeichnet. Die lokale Spielsession mit KI-Client lief auf Quest nachweislich bis mindestens Tick 244. Ein räumliches XR-Brett und Controllerbedienung sind weiterhin nicht im Headset bestätigt. Die experimentelle OpenXR-Quad-Brücke ist gebaut, aber auf dem Gerät noch nicht gestartet.

Der erste implementierte Baustein ist `OpenRA.Game/Input/TabletopPointer.cs`: Er berechnet aus einem Controllerstrahl die logische Pixelposition auf einem rechteckigen Spielbrett und gibt Bewegung, Klicks und Ziehen über OpenRAs `IInputHandler` weiter. Die Auflösung muss zur angezeigten OpenRA-Ansicht passen. Die zugehörigen Tests liegen in `OpenRA.Test/OpenRA.Game/TabletopPointerTest.cs`. Die Android-Probe lädt auf Quest 3 die echten Red-Alert-Regeln und eine Testkarte. Ein diagnostischer Android-Plattformadapter startet OpenRAs `Renderer`; dessen Weltpuffer, UI-Komposition, Sprite- und Schriftpfad wurden auf dem Gerät geprüft. Nach getrenntem Import der Originaldaten konnte die Quest 247 echte Schnee-Tiles und drei Panzer-Sprites zeichnen. Der vollständige `WorldRenderer` gab anschließend die Editor-Welt mit Terrain und Map-Actors aus; die reguläre Welt startete mit lokalem Spieler und einem Tick. Eine fortlaufende lokale Partie wurde auf dem Gerät gestartet; die Wirkung der Touch- und XR-Steuerung braucht noch einen Sichttest im Headset. Ein In-App-Importer für das separate Red-Alert-Quickinstall-ZIP wurde auf dem Entwicklungsrechner geprüft; sein Quest-Test steht noch aus. Die optionale OpenXR-Brücke liest das komponierte OpenRA-Bild, überträgt es an ein Quad und reicht Controllerereignisse an OpenRAs Eingabequeue weiter. Sie ist bisher nur gebaut und lokal getestet, nicht auf Quest ausgeführt. Die [Android-Probe](ANDROID-PROBE.md) beschreibt den getesteten Debug-Import.

OpenRA steht unter GPLv3 oder später; der Fork behält die ursprüngliche Lizenzdatei `COPYING` bei. Rechte an ursprünglichen Spieldaten von EA sind separat zu klären. Bis dahin werden hier keine solchen Daten oder APKs mit diesen Inhalten bereitgestellt. Siehe [OpenRA-Lizenzhinweise](https://www.openra.net/legal/).

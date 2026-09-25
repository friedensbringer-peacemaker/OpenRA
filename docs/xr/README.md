# OpenRA XR: Quest-3-Tabletop

Dieser Zweig entwickelt einen eigenständigen OpenRA-Port auf Meta Quest 3. Eine spielbare Quest-Version existiert noch nicht; eine signierte Android-ARM64-Diagnose-APK wurde auf Quest 3 erfolgreich gestartet. Die Desktop-Spielbasis stammt aus dem [Upstream-Projekt](https://github.com/OpenRA/OpenRA).

- [Machbarkeitsprüfung](MACHBARKEIT-OPENRA-QUEST.md)
- [Quellenliste der Prüfung](sources.json)
- [XR-Backlog](BACKLOG.md) mit weiteren, eigenständigen Spieleideen
- [Quest-Eingabekonzept](CONTROLS.md) für Auswahl, Befehle und Bauleiste
- [Android-ARM64-Probe](ANDROID-PROBE.md) mit Buildanleitung und Grenzen des Starttests

Der nächste große Meilenstein ist eine Android-ARM64-App, die die geladenen Karten-Tiles und Einheiten mit OpenRAs `WorldRenderer` auf der Quest zeichnet und ein kleines Gefecht simuliert. Danach sollen Welttextur, räumliches Brett und Controller-Eingabe über OpenXR folgen.

Der erste implementierte Baustein ist `OpenRA.Game/Input/TabletopPointer.cs`: Er berechnet aus einem Controllerstrahl die logische Pixelposition auf einem rechteckigen Spielbrett und gibt Bewegung, Klicks und Ziehen über OpenRAs `IInputHandler` weiter. Die Auflösung muss zur angezeigten OpenRA-Ansicht passen. Die zugehörigen Tests liegen in `OpenRA.Test/OpenRA.Game/TabletopPointerTest.cs`. Die Android-Probe lädt auf Quest 3 die echten Red-Alert-Regeln und eine Testkarte. Ein diagnostischer Android-Plattformadapter startet OpenRAs `Renderer`; dessen Weltpuffer, UI-Komposition, Sprite- und Schriftpfad wurden auf dem Gerät geprüft. Nach getrenntem Import der Originaldaten konnte die Quest außerdem 247 echte Schnee-Tiles und drei Panzer-Sprites mit OpenRAs Paletten und Sprite-Renderer zeichnen. Ein OpenXR-Adapter, OpenRAs vollständiger `WorldRenderer`, Spielsimulation und ein Endnutzer-Inhaltsimport fehlen noch. Die [Android-Probe](ANDROID-PROBE.md) beschreibt den getesteten Debug-Import.

OpenRA steht unter GPLv3 oder später; der Fork behält die ursprüngliche Lizenzdatei `COPYING` bei. Rechte an ursprünglichen Spieldaten von EA sind separat zu klären. Bis dahin werden hier keine solchen Daten oder APKs mit diesen Inhalten bereitgestellt. Siehe [OpenRA-Lizenzhinweise](https://www.openra.net/legal/).

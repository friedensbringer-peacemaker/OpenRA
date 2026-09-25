# OpenRA XR: Quest-3-Tabletop

Dieser Zweig hält die frühe Planung für einen eigenständigen OpenRA-Port auf Meta Quest 3 fest. Eine spielbare Quest-Version oder APK existiert hier noch nicht. Die bisherigen OpenRA-Desktop-Funktionen stammen unverändert aus dem [Upstream-Projekt](https://github.com/OpenRA/OpenRA).

- [Machbarkeitsprüfung](MACHBARKEIT-OPENRA-QUEST.md)
- [Quellenliste der Prüfung](sources.json)
- [XR-Backlog](BACKLOG.md) mit weiteren, eigenständigen Spieleideen

Als erster technischer Meilenstein ist eine Android-ARM64-App vorgesehen, die OpenRA samt Regeln und Karte auf der Quest lädt und mit OpenGL ES darstellt. Danach sollen Welttextur, räumliches Brett und Controller-Eingabe über OpenXR folgen.

OpenRA steht unter GPLv3 oder später; der Fork behält die ursprüngliche Lizenzdatei `COPYING` bei. Rechte an ursprünglichen Spieldaten von EA sind separat zu klären. Bis dahin werden hier keine solchen Daten oder APKs mit diesen Inhalten bereitgestellt. Siehe [OpenRA-Lizenzhinweise](https://www.openra.net/legal/).

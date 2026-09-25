# XR-Strategieprojekte – Backlog

## Aktuell: OpenRA für Meta Quest 3

- **Ziel:** Eigenständige Quest-3-App; Installation als APK über SideQuest.
- **Darstellung:** Tabletop, möglichst mit optionalem Mixed Reality/Passthrough.
- **Technische Richtung:** OpenRA-Fork mit Android- und OpenXR-Unterstützung.
- **Erster vorgeschlagener Umfang:** Alarmstufe Rot, Originalgrafik auf einem räumlichen Spielbrett, Quest-Controller und Gefecht gegen KI.
- **Status:** Quellcodebasierte Machbarkeitsprüfung und ein getesteter Controllerstrahl-zu-Spielbrett-Eingabebaustein liegen im Fork vor. Android-/OpenXR-App und Messungen auf der Quest stehen aus.
- **Ausarbeitung:** [Machbarkeitsprüfung](MACHBARKEIT-OPENRA-QUEST.md).
- **Quellen:** [OpenRA](https://github.com/OpenRA/OpenRA), [OpenXR](https://www.khronos.org/openxr/), [Generals XR als Referenz](https://github.com/Cesarus85/Generals-Zero-Hour-XR).

## Später: Schlacht um Mittelerde als Quest-Tabletop

Erfasst am 25. September 2026 auf Wunsch des Nutzers.

| ID | Spiel | Status |
| --- | --- | --- |
| XR-BFME-01 | The Lord of the Rings: The Battle for Middle-earth (2004) / Die Schlacht um Mittelerde | Idee; technische Machbarkeit noch nicht geprüft |
| XR-BFME-02 | The Lord of the Rings: The Battle for Middle-earth II (2006) / Die Schlacht um Mittelerde II | Idee; technische Machbarkeit noch nicht geprüft |
| XR-BFME-03 | Erweiterung: The Rise of the Witch-king / Aufstieg des Hexenkönigs | Idee; zusammen mit Teil II prüfen |

**Wunschbild:** Armeen, Festungen und Schlachtfelder als steuerbare Miniaturen auf einem virtuellen Tisch, idealerweise eigenständig auf Meta Quest 3 und per SideQuest installierbar; Mixed Reality/Passthrough nach Möglichkeit.

**Spätere Recherche:** Verfügbare Engine-/Reimplementierungsprojekte, Stand der Unterstützung dieser konkreten Spiele, Zugang zu Grafik und Spielsimulation, Android-ARM64-Portierbarkeit, OpenXR-Einbindung, Bedienung, Geräteperformance und benötigte Original-Spieldateien untersuchen.

**Abgrenzung:** Als eigene Vorhaben führen. Aus der Generals-XR-Portierung lässt sich keine funktionierende Unterstützung dieser Spiele ableiten. Kein Umsetzungstermin und keine Zusage der Machbarkeit.

**Priorität:** Nach dem OpenRA-Standalone-Prototyp erneut bewerten.

## Später: Siedler 2.5 / Return to the Roots auf Quest 3

Erfasst am 25. September 2026 auf Wunsch des Nutzers.

- **Vorlage:** [Return to the Roots (Siedler 2.5)](https://www.siedler25.org/index.php?com=dynamic&mod=1&lang=de), eine freie Neuimplementierung und Erweiterung von „Die Siedler II“.
- **Wunschbild:** Eigenständig auf Meta Quest 3 spielbar, idealerweise als steuerbares Tabletop mit optionalem Passthrough; Installation als APK über SideQuest.
- **Status:** Idee; technische Machbarkeit, Lizenz- und Spieldatenlage sowie Aufwand sind noch nicht geprüft.
- **Nächster Prüfschritt:** Quellcode und Abhängigkeiten auf Android-ARM64-Portierung, XR-Rendering, Eingabe, Quest-Leistung und benötigte Original-Spieldaten untersuchen. Erst danach Umfang und Reihenfolge festlegen.
- **Abgrenzung:** Eigenes Vorhaben; der OpenRA-Fork ist dafür keine gemeinsame Codebasis.

## Später: OpenTTD auf Quest 3

Erfasst am 25. September 2026 auf Wunsch des Nutzers.

- **Vorlage:** [OpenTTD](https://www.openttd.org), die quelloffene Transport- und Wirtschaftssimulation.
- **Wunschbild:** Eigenständig auf Meta Quest 3 spielbar, vorzugsweise als bedienbare Tabletop-Welt mit optionalem Passthrough; Installation als APK über SideQuest.
- **Status:** Idee; technische Machbarkeit, Lizenz- und Inhaltslage sowie Aufwand sind noch nicht geprüft.
- **Nächster Prüfschritt:** Vorhandene Android-Unterstützung und Quellcodebasis, Grafik- und Eingabesystem, OpenXR-Einbindung, Lesbarkeit der Oberfläche und Leistung auf Quest 3 untersuchen.
- **Abgrenzung:** Eigenes Vorhaben neben OpenRA und Return to the Roots.

## Später: OpenXcom auf Quest 3

Erfasst am 25. September 2026 auf Wunsch des Nutzers.

- **Vorlage:** [OpenXcom](https://github.com/OpenXcom/OpenXcom) für „UFO: Enemy Unknown“ und „X-COM: Terror From the Deep“; [Projektseite](https://openxcom.org).
- **Wunschbild:** Eigenständig auf Meta Quest 3 spielbar, mit räumlicher Tabletop-Darstellung der taktischen Gefechte und gut bedienbaren Strategie- und Basismenüs; Installation als APK über SideQuest.
- **Status:** Idee; Quest-Machbarkeit und Umfang sind noch nicht geprüft.
- **Rechte und Inhalte:** Der OpenXcom-Code steht laut Projekt unter GPL. Zum Spielen benötigt er Ressourcen aus den Originalspielen; deren Rechte werden durch die Code-Lizenz nicht übertragen.
- **Nächster Prüfschritt:** Android-ARM64-Unterstützung, C++/SDL-Rendering, OpenXR-Anbindung, Eingabe für taktische Gefechte und Menüs, Leistung sowie legalen Import der Originaldaten untersuchen.
- **Abgrenzung:** Eigenes Vorhaben neben den anderen Spielen im Backlog.

## Später: Stratagus für Warcraft II und StarCraft auf Quest 3

Erfasst am 25. September 2026 auf Wunsch des Nutzers.

| ID | Basis | Status |
| --- | --- | --- |
| XR-STRATAGUS-01 | [Wargus](https://stratagus.com) mit der [Stratagus-Engine](https://stratagus.com/stratagus.html) für Warcraft II | Quest-Machbarkeit noch nicht geprüft |
| XR-STRATAGUS-02 | [Stargus](https://stratagus.com/stargus.html) mit derselben Engine für StarCraft | Laut Projektseite noch Pre-Alpha; Umfang vor einer Quest-Portierung gesondert bewerten |

- **Recherchehinweis zu Warcraft II:** Die [Open-Source-Game-Clones-Übersicht](https://osgameclones.com/warcraft-ii/) nennt Wargus und [Dark Oberon](https://dark-oberon.sourceforge.io/). Dark Oberon beschreibt sich als eigenständiges, Warcraft-II-ähnliches Spiel mit eigener Grafik; es ist kein Ersatz für Wargus, wenn das Originalspiel auf der Quest laufen soll.
- **Portierungsreferenz:** [PeonPad](https://github.com/chrissotraidis/peonpad) bringt Stratagus/Wargus laut Projekt-README als native ARM64-App auf das iPad und trennt die vom Nutzer bereitgestellten Warcraft-II-Daten vom GPL-Code. SDL2, Touch-Bedienung, Lebenszyklus und Datenprüfung sind für die Planung interessant. Die Apple-/Metal-Integration ist jedoch kein fertiger Android-/OpenXR-Port; Übertragbarkeit und Lizenzbedingungen des Codes sind vor Wiederverwendung zu prüfen.
- **Wunschbild:** Eigenständig auf Meta Quest 3 spielbare Echtzeitstrategie als Tabletop, mit Quest-Controllern und nach Möglichkeit optionalem Passthrough; Installation als APK über SideQuest.
- **Rechte und Inhalte:** Wargus benötigt eine Kopie des ursprünglichen Warcraft II, Stargus ursprüngliche StarCraft-Daten. Die Rechte an diesen Spieldaten sind getrennt vom Quellcode zu prüfen; kein ungeklärtes Mitliefern in einer APK.
- **Nächster Prüfschritt:** Aktuellen Code und Lizenzen von Stratagus, Wargus und Stargus prüfen; Android-ARM64-Portierung, OpenXR-Darstellung und Eingabe sowie einen rechtssicheren Datenimport untersuchen. Für Stargus zunächst die noch fehlenden Spielfunktionen erfassen.
- **Abgrenzung:** Eigenes Vorhaben; keine gemeinsame Codebasis mit dem OpenRA-Fork voraussetzen.

## Später: openage / Age of Empires auf Quest 3

Erfasst am 25. September 2026 auf Wunsch des Nutzers.

- **Vorlage:** [SFTtech/openage](https://github.com/SFTtech/openage), eine freie Neuimplementierung der Genie-Engine für Age of Empires I und II.
- **Wunschbild:** Eigenständig auf Meta Quest 3 spielbare Age-of-Empires-Gefechte als Tabletop mit Quest-Controllern, optionalem Passthrough und Installation als APK über SideQuest.
- **Status:** Langfristige Idee. Das Projekt bezeichnet sein Gameplay im aktuellen README selbst noch als weitgehend nicht funktionsfähig; eine Quest-Portierung setzt deshalb zuerst eine spielbare Desktop-Basis voraus.
- **Rechte und Inhalte:** openage steht laut Projekt unter GPLv3 oder später, liefert die originalen Grafik- und Tondaten aber nicht mit. Für diese Inhalte werden Daten aus rechtmäßig vorhandenen Originalspielen benötigt.
- **Nächster Prüfschritt:** Fortschritt der Spielsimulation verfolgen und nach einem spielbaren Stand Android-ARM64-Tauglichkeit von C++-Engine, Python/Cython, Qt-Oberfläche, OpenGL-Renderer, Datenkonvertierung und OpenXR-Tabletop-Bedienung prüfen.
- **Abgrenzung:** Eigenes Vorhaben, unabhängig vom OpenRA-Fork.

## Später: 0 A.D. auf Quest 3

Erfasst am 25. September 2026 auf Wunsch des Nutzers.

- **Vorlage:** [0 A.D.](https://play0ad.com/), ein eigenständiges historisches Echtzeitstrategiespiel von Wildfire Games. Release 28 ist laut Projektseite eine spielbare Veröffentlichung ohne bisherigen Alpha-Zusatz.
- **Wunschbild:** Eigenständig auf Meta Quest 3 als räumliches Tabletop mit steuerbaren Armeen, Gebäuden und Gelände; optionales Passthrough und Installation als APK über SideQuest.
- **Status:** Quest-Machbarkeit und Geräteperformance noch nicht geprüft. Das bereits spielbare Desktop-Spiel ist eine andere Ausgangslage als die noch unvollständige Spielsimulation von openage.
- **Rechte und Inhalte:** Die Projektseite nennt GPLv2 für den Code und CC BY-SA 3.0 für die Grafik. Vor einer Quest-Veröffentlichung sind die konkreten Lizenzhinweise und Drittinhalte des gewählten Quellstands zu prüfen.
- **Nächster Prüfschritt:** Quellcode, Abhängigkeiten und Renderpfad auf Android-ARM64 und OpenXR untersuchen; 3D-Darstellung, Controller-Bedienung, Wärmeentwicklung und Bildrate auf einer Quest 3 messen.
- **Abgrenzung:** Eigenständiges Spiel und separates Portierungsprojekt, keine Erweiterung des OpenRA-Forks.

## Weitere Kandidaten aus der Strategiespiele-Sichtung

Erfasst am 25. September 2026 nach Sichtung der [Strategiespiele-Liste von My Abandonware](https://www.myabandonware.com/browse/genre/strategy-6/popular/). Diese Liste ist eine Ideensammlung und keine Lizenz- oder Bezugsfreigabe.

| Spielidee | Mögliche technische Basis | Erste Einschätzung |
| --- | --- | --- |
| Dune II | [Dune Legacy](https://www.dunelegacy.com/) | Besonders interessant: Das Projekt bietet bereits eine Android-ARM64-Version. Quest- und XR-Eignung gesondert prüfen; ursprüngliche Spieldaten nicht mitliefern. |
| Black & White (2001) | [openblack](https://github.com/openblack/openblack) | Freie Neuimplementierung mit experimenteller Android-Arbeit; benötigt Originaldaten. Spielbarkeit und Quest-Leistung prüfen. Black & White 2 ist damit nicht automatisch abgedeckt. |
| Civilization-artige Strategie | [Freeciv](https://www.freeciv.org/) | Eigenständiges freies Spiel und möglicher Tabletop-Kandidat; kein Port des ursprünglichen Civilization. |

## Später: Star Trek Armada I und II auf Quest 3

Erfasst am 25. September 2026 auf Wunsch des Nutzers. Der Nutzer besitzt nach eigener Aussage beide GOG-Versionen.

- **Spiele:** [Star Trek: Armada](https://www.myabandonware.com/game/star-trek-armada-bcg) und [Star Trek: Armada II](https://www.myabandonware.com/game/star-trek-armada-ii-bch).
- **Technischer Ansatz:** [STA64](https://www.fleetops.net/forums/technical/sta64-building-a-native-64-bit-future-for-star-trek-armada-14072?p=207960) als mögliche saubere Neuimplementierung für Armada I/II prüfen. Das Projekt bezeichnet seinen Stand noch als frühe, nicht spielbare Alpha; Android/OpenXR sind nicht belegt.
- **Wunschbild:** Raumschiffe und Basen als räumlich steuerbares Tabletop auf Quest 3.
- **Rechte und Inhalte:** Rechtmäßig vorhandene GOG-Dateien des Nutzers könnten als Eingabe dienen. Der Besitz erlaubt nicht automatisch die Weitergabe dieser Daten in einer APK; Code-, Marken- und Inhaltsrechte getrennt prüfen.
- **Nächster Prüfschritt:** Spielbarkeit und Datenimport von STA64, Android-ARM64-Portierung, OpenXR-Rendering und Bedienung prüfen.

## Später: Need for Speed Underground 2 auf Quest 3

Erfasst am 25. September 2026 auf Wunsch des Nutzers.

- **Vorlage:** [Need for Speed: Underground 2](https://www.myabandonware.com/game/need-for-speed-underground-2-ega).
- **Mögliche Basis:** [OpenUG2](https://github.com/whoismept/OpenUG2) ist ein früher Neuimplementierungsprototyp für Originaldaten; daraus folgt noch keine vollständige Android- oder XR-Version.
- **Wunschbild:** Standalone-Rennspiel auf Quest 3; Cockpit-/Fahrperspektive und Komfortregeln getrennt von den Strategie-Tabletops entwerfen.
- **Status:** Langfristige Idee; Engine-Reife, Rechte, benötigte Originaldaten und Quest-Leistung nicht abschließend geprüft.

## Später: Bullfrog-Klassiker auf Quest 3

Erfasst am 25. September 2026 auf Wunsch des Nutzers.

| Spiel | Mögliche Basis und Stand |
| --- | --- |
| [Theme Park](https://www.myabandonware.com/game/theme-park-24y) | Geeignete freie Neuimplementierung noch zu ermitteln; vorerst reine Idee. |
| [Theme Hospital](https://www.myabandonware.com/game/theme-hospital-2ek) | [CorsixTH](https://github.com/CorsixTH/CorsixTH) als freie Engine; ein [Android-Port](https://github.com/alanwoolley/CorsixTH-Android) existiert, Quest-/XR-Eignung offen. Benötigt originale Spieldaten. |
| [Dungeon Keeper](https://www.myabandonware.com/game/dungeon-keeper-d74) und [Gold](https://www.myabandonware.com/game/dungeon-keeper-gold-edition-d5i) | [KeeperFX](https://github.com/dkfans/keeperfx) als freie Engine/Fan-Erweiterung; benötigt originale Spieldaten. Android/OpenXR prüfen. |
| [Dungeon Keeper II](https://www.myabandonware.com/game/dungeon-keeper-2-cu5) | [OpenKeeper](https://github.com/tonihele/OpenKeeper) als Neuimplementierung; benötigt originale Spieldaten. Ein [experimenteller Android-Zweig](https://github.com/DifferentNet/OpenKeeper/blob/android-port/ANDROID.md) ist ein Rechercheansatz, noch keine Quest-Zusage. |

**Gemeinsamer nächster Schritt:** Spielbarkeit der jeweiligen Engine, Code-Lizenz, Datenimport aus rechtmäßig vorhandenen Spielen sowie Android-ARM64-, OpenXR- und Controller-Eignung einzeln prüfen. Die My-Abandonware-Einträge begründen keine Erlaubnis, Originaldaten weiterzugeben.

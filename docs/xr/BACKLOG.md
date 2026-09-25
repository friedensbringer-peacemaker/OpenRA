# XR-Strategieprojekte – Backlog

## Aktuell: OpenRA für Meta Quest 3

- **Ziel:** Eigenständige Quest-3-App; Installation als APK über SideQuest.
- **Darstellung:** Tabletop, möglichst mit optionalem Mixed Reality/Passthrough.
- **Technische Richtung:** OpenRA-Fork mit Android- und OpenXR-Unterstützung.
- **Erster vorgeschlagener Umfang:** Alarmstufe Rot, Originalgrafik auf einem räumlichen Spielbrett, Quest-Controller und Gefecht gegen KI.
- **Status:** Quellcodebasierte Machbarkeitsprüfung liegt vor; Implementierung und Messungen auf der Quest stehen aus.
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

# Quest-Tabletop: Eingabekonzept

Stand: 25. September 2026. Dies ist ein Entwurf für den OpenRA-Fork, keine Beschreibung einer bereits spielbaren Quest-App. [Tiberian Dawn for Android and Meta Quest](https://github.com/Cesarus85/Tiberian-Dawn-for-Android-and-Meta-Quest) ist die nächste Referenz für ein klassisches 2D-RTS mit räumlichem Spiel-, Bau- und Radarfenster sowie getrenntem Import eigener Spieldaten. [Generals: Zero Hour XR](https://github.com/Cesarus85/Generals-Zero-Hour-XR) zeigt zusätzlich eine eigenständige stereoskopische Quest-Tabletop-Umsetzung mit Controller-Auswahl, Auswahlrahmen und Kontextbefehlen. [Gonzorros GeneralsVR](https://github.com/Gonzorro/GeneralsVR) und dessen [VR-Steuerung](https://github.com/Gonzorro/GeneralsVR/blob/feature/openxr-vr/VR-CONTROLS.md) liefern Ideen für Laser-Feedback, Hand-HUD, Befehlsrad und Steuergruppen; GeneralsVR benötigt Quest Link und einen PC. Die konkrete Zuordnung unten orientiert sich an OpenRAs vorhandenen Eingaben.

## Erste bedienbare Stufe

Zuerst wird die vollständige OpenRA-Spielansicht auf eine rechteckige Fläche projiziert. Ein Controllerstrahl liefert die OpenRA-Bildschirmposition; die vorhandene `IInputHandler`-Schnittstelle erhält Mausbewegung und Tastenereignisse. `TabletopPointer` implementiert diesen Teil bereits unabhängig von einem XR-Laufzeitsystem. Die Fläche kann später in Spielfeld, Bauleiste und weitere räumliche Fenster aufgeteilt werden; dann braucht jede Fläche eigene Trefferprüfung und Eingabefokus.

| Quest-Eingabe (Vorschlag) | OpenRA-Eingabe | Ergebnis |
| --- | --- | --- |
| Rechter Controller zeigt auf das Brett | `MouseInputEvent.Move` | Cursor, Hover und Zielvorschau |
| Rechter Trigger kurz | linke Maustaste drücken/loslassen | Einzelne Einheit auswählen oder UI-Schaltfläche betätigen |
| Rechter Trigger halten und Strahl ziehen | linke Maustaste halten/bewegen/loslassen | Auswahlrahmen für mehrere Einheiten |
| Linker Grip während Auswahl | `Modifiers.Shift` | Weitere Einheiten zur Auswahl hinzufügen |
| Rechter Grip kurz über dem Brett | rechte Maustaste drücken/loslassen | Kontextabhängigen Befehl an gewählter Position ausführen |
| Linker Stick | `Viewport.Scroll` über einen XR-Adapter | Karte schwenken, unabhängig vom Auswahlstrahl |
| Rechter Stick vertikal | `Viewport.AdjustZoom` über einen XR-Adapter | Kartenausschnitt vergrößern/verkleinern |

OpenRAs `WorldInteractionControllerWidget` verarbeitet Linksklick, Auswahlrahmen und Rechtsklick bereits. `SelectionUtils` kombiniert eine Auswahl mit Shift. Befehle kommen über `World.OrderGenerator`, sodass Bewegung, Angriff und andere Aktionen vom Ziel und aktuellen Modus abhängen. Die Bauleiste verwendet `ProductionTabsWidget` und `ProductionPaletteWidget`; ein Gebäudeklick aktiviert unter anderem `PlaceBuildingOrderGenerator`. Diese Wege sollen benutzt werden, bevor eigene XR-Befehlslogik entsteht.

Die Tabelle setzt OpenRAs voreingestellten Mausmodus **Modern** voraus, bei dem die rechte Maustaste die Aktions-/Befehlstaste ist. Der XR-Adapter muss einen geänderten Mausmodus berücksichtigen oder für seine Steuerung eine feste, klar erklärte Zuordnung anbieten. Mehrfachklicks zur Auswahl aller gleichartigen Einheiten sind im ersten `TabletopPointer`-Baustein noch nicht umgesetzt.

Die getrennte Android-2D-Probe kann inzwischen einen Doppeltipp als OpenRA-Mehrfachklick weitergeben. Das ist noch keine OpenXR-Controllerbindung; `TabletopPointer` und der native XR-Quad-Test sind derzeit nicht miteinander verbunden.

## Räumliche Bedienung nach der ersten Stufe

- **Baufenster:** Die vorhandenen Produktionstabs und -symbole in ein vergrößerbares, getrenntes Fenster übertragen. Für Gebäudeplatzierung bleibt der Zielcursor auf dem Spielbrett sichtbar.
- **Radarfenster:** Die bestehende Minimap später als eigenes Fenster ausgeben; dabei dieselben Kartenpositionen und Befehle wie in der Desktop-Ansicht verwenden. Tiberian Dawns Quest-Port ist dafür eine passende Bedienreferenz.
- **Befehlsfenster:** Vorhandene Aktionen aus `CommandBarLogic` wie Angriff-Bewegung, Bewachen, Reparieren und Verkaufen als gut lesbare Schaltflächen anbieten. Ein radiales Menü ist eine mögliche spätere Alternative, aber kein Ersatz für die bestehenden `OrderGenerator`-Modi.
- **Laser-Feedback:** Für Auswahl, Bewegung, Angriff und Spezialaktionen unterschiedliche Zielzustände direkt am Strahl zeigen, wie es GeneralsVR vormacht. Die Farbe muss von OpenRAs tatsächlichem Befehl am Ziel kommen, damit die Vorschau keine falsche Aktion ankündigt.
- **Auswahl und Gruppen:** Selektion deutlich hervorheben; Mehrfachauswahl durch Ziehen und additive Auswahl unterstützen. Steuergruppen erst nach funktionierender Basisbedienung mit XR-Schaltflächen versehen.
- **Fokus und Abbruch:** Ein Triggerereignis gehört genau einer getroffenen Fläche. Verlässt ein Strahl das Brett während eines Ziehens, wird die gedrückte Taste am letzten gültigen Punkt losgelassen. Ein Abbruchknopf für Platzierungs- und Befehlsmodi braucht eine eigene Zuordnung.
- **Komfort:** Brettgröße, Höhe und Abstand einstellbar machen; Texte und Trefferflächen im Headset prüfen. Passthrough und Ablage auf einer realen Oberfläche sind spätere Stufen.

## Offene Tests auf der Quest

Treffgenauigkeit bei kleinen Einheiten und Bausymbolen, unbeabsichtigte Befehle beim Schwenken, Auswahlrahmen bei bewegtem Kopf, Erreichbarkeit des Baufensters, Lesbarkeit und Framerate sind erst mit einer Android/OpenXR-App sinnvoll zu bewerten. Die bisherige automatisierte Prüfung deckt nur die Strahlprojektion und Weiterleitung der Mausereignisse ab.

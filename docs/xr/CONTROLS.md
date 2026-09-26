# Quest-Tabletop: aktuelle Eingabe und spätere Bedienung

Stand: 26. September 2026. Die Belegung unten ist in der kombinierten Android/OpenXR-APK **implementiert und lokal gebaut, aber auf Quest noch nicht als Spielsteuerung getestet**. Sie beschreibt die aktuelle rechte Touch-Hand; der linke Controller hat in diesem Prototyp keine OpenXR-Action. Das Brett zeigt zunächst die vollständige OpenRA-Ansicht mit Welt und UI auf einem Quad. Die native Strahlprojektion in [`XrQuad.cpp`](../../OpenRA.Quest.XrProbe/native/XrQuad.cpp) führt über [`QuestXrBridge`](../../OpenRA.Quest.Probe/QuestXrBridge.cs) und [`QuestInputQueue`](../../OpenRA.Quest.Probe/QuestInputQueue.cs) zu OpenRAs Maus-/Modifier-Eingaben. `TabletopPointer` bleibt ein separat getesteter Geometriebaustein und ist noch nicht der aktive XR-Eingabepfad.

## Gebaute Zuordnung

| Rechter Touch-Controller | In OpenRA eingespeist | Gedachte Aktion |
| --- | --- | --- |
| Zielstrahl trifft das Brett | Mausbewegung an die getroffene Bildposition | Cursor/Zeigen |
| Trigger drücken, halten und loslassen | linke Maustaste | Klick, Einheitenauswahl oder Auswahlrahmen |
| A drücken und loslassen | rechte Maustaste | Kontextbefehl am Ziel |
| Griff drücken und Strahl bewegen | mittlere Maustaste halten und ziehen | Karte verschieben |
| B halten | `Modifiers.Shift` | Auswahl erweitern |
| Stick nach oben/unten | wiederholte Scroll-Ereignisse | Kartenzoom |

Die OpenXR-Bindings und Grenzwerte stehen in `XrQuad.cpp` (`xrSuggestInteractionProfileBindings`, `xrGetActionState*`); [`PointerTransitions.h`](../../OpenRA.Quest.XrProbe/native/PointerTransitions.h) erzeugt aus gehaltenen Tasten saubere Down-/Up-Übergänge und löst eine gehaltene Taste beim Verlassen des Bretts. `QuestXrBridge.PointerForwarder` setzt die Ereignisse in Mausbuttons, Scrollen und Shift um. Für die aktuelle Android-Spielsession erzwingt [`QuestGameSession.cs`](../../OpenRA.Quest.Probe/QuestGameSession.cs) OpenRAs Mausmodus **Modern**. Der Stick sendet Scroll-Ereignisse, keinen direkten Aufruf von `Viewport.AdjustZoom`; die tatsächliche Wirkung ist am Gerät zu prüfen.

Auf der getrennten Android-2D-Oberfläche wählt man per Bildschirmtasten zwischen Auswahl, Befehl und Kartenbewegung; „Mehrfach“ setzt Shift und „Karte +/−“ scrollt. Diese Touch-Bedienung ist kein Nachweis für die Controllerbedienung. Die Touch-Fläche nimmt während einer laufenden XR-Session keine Eingaben an; ihr letzter gehaltener Kontakt wird beim XR-Start beendet.

## Noch offen

- Ob der native Strahl kleine Einheiten und UI-Symbole zuverlässig trifft, Auswahlrahmen korrekt auslöst und A tatsächlich einen Bewegungs-/Angriffsbefehl erteilt, muss auf Quest beobachtet werden.
- Das Produktionsmenü ist in der aktuellen Quest-Spielsession noch nicht als vollständiger XR-Spielablauf nachgewiesen. Bauen und Produzieren sind Teil des [S2-Pakets](pakete/S2-SPIEL-UI.md).
- Ein Controllerknopf zum Abbrechen einer Gebäudeplatzierung oder eines Befehlsmodus ist noch nicht gebunden. Ebenso fehlen freies Neupositionieren/Skalieren des Bretts und Steuergruppen.
- Ein zweites räumliches Fenster für Bauleiste oder Radar ist ein späterer Ausbau. Jede Fläche braucht dann eigenen Eingabefokus und eine passende Strahl-zu-Pixel-Abbildung.

Für die spätere Bedienungsprüfung gelten die Schritte im [Quest-Testprotokoll](QUEST-TESTPROTOKOLL.md). Die Projekte [Tiberian Dawn for Android and Meta Quest](https://github.com/Cesarus85/Tiberian-Dawn-for-Android-and-Meta-Quest), [Generals: Zero Hour XR](https://github.com/Cesarus85/Generals-Zero-Hour-XR) und [GeneralsVR](https://github.com/Gonzorro/GeneralsVR/blob/feature/openxr-vr/VR-CONTROLS.md) bleiben Referenzen für räumliche UI, Laser-Feedback und spätere Bedienideen; ihre Implementierungen belegen keine funktionierende OpenRA-Quest-Steuerung.

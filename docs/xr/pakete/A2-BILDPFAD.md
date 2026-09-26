# A2 – Gemessenen XR-Bildpfad optimieren

- Zuständig: Codex Astra
- Priorität: P1 nach G2
- Ausgangsstand bei Vorbereitung: Branch `quest-tabletop-research`, Commit `510f1a0a9081c38795997acdb60b4c224808dd0a`, sauberer Git-Status; vor Arbeitsbeginn aktualisieren
- Startbedingung: G2 plus Quest-Messwerte für Framezeit, Readback/Kopie, Speicher und Temperatur

## Auftrag

Profile die Kette `Renderer.ReadScreenPixelsBgra` → `QuestXrBridge.PublishFrame` → `XrFrameConverter`/JNI → `XrQuad.cpp`-Swapchain. Der bestehende Puffer ist wiederverwendet, die Übergabe auf ungefähr 15 Hz begrenzt; der synchrone GPU-Readback ist nicht auf Quest gemessen. Wähle nur anhand der Messung den Engpass und implementiere die kleinste wirksame Änderung. Schütze Bildformat, sichtbaren Framebuffer-Bereich, atomare Größenpublikation und Eingabeabbildung gegen Regressionen.

Erlaubte Änderungen: die gemessenen Teile des Render-/Frame-Pfads und gezielte Tests; keine Spielregeln oder Steuerungsneubelegung. Dokumentiere vorab Vergleichsszenario und Messmethode, damit Vorher/Nachher vergleichbar sind.

## Abnahme

Vorher-/Nachher-Messungen auf derselben Quest und Szene zeigen den Effekt der Änderung und mögliche thermische Kosten. Das Quad bleibt lesbar und die Eingabe deckungsgleich. Native Tests, Frame-Konvertierungstests und kombinierter Build bestehen. 72 Hz ist kein Abnahmekriterium ohne vorherige Machbarkeitsmessung.

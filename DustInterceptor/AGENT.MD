<!-- AGENT.MD -->

# RockHopper – Agent Plan (minimal prototype)

## Ziel
Neue Spielidee als schneller Prototyp mit minimalen Änderungen:
- Statt Triebwerk-Fliegen: Spieler springt durch Abstoßen von Asteroiden (Impulse).
- Keine UI (keine Myra Panels, kein Shop Screen).
- Impulsvektor dreht sich mit der Rotationsrate des Asteroiden (neue Eigenschaft).
- Stärke wird mit LT/RT gesteuert, mit **A** wird ausgelöst.
- Spieler startet **gedockt** auf einem Asteroiden.

## Ausgangslage (Code-Check – relevante Stellen)
- `OrbitSandboxGame.cs`:
  - Flight/Mining Mode, Mining-UI via `MiningUi` (Myra).
  - Impuls-Aim im Flight-Mode: **Left Stick**; Auslösen: **X**.
  - Zoom: LT/RT.
- `WorldSim.cs`:
  - Docking/Undocking, Mining Transfer, Impuls/Cooldown, `ShipForward`/`ShipRotation`.
- `models/Asteroid.cs`:
  - Asteroid Daten (Rotation/Spin aktuell nicht vorhanden).

---

## Milestone
**MVP:** Start gedockt → Impulsrichtung rotiert mit Asteroid → Stärke per Trigger → A springt → Ship fliegt → kann wieder andocken.

---

## Tickets (in sinnvoller Reihenfolge)

### RH0 – Repository/Build Safety ✅ DONE
**Goal:** Sicherstellen, dass wir vor Änderungen reproduzierbar bauen können.

**Ergebnis:**
- Build: ✅ grün (net8.0, MonoGame DesktopGL).
- Content Pipeline: ✅ vorhanden (Asteroid.fx, BackgroundGrid.fx, Planet.fx, Ship.xnb, UIfont.spritefont).
- Projektstruktur: 22 Source-Dateien, sauber aufgeteilt in `models/`, `services/`, `config/`, `enums/`.
- Keine Compile-Fehler, keine fehlenden Assets.

---

### RH1 – Mining/Shop UI deaktivieren ("keine UI") ✅ DONE
**Status:** ✅ DONE

**Goal:** Keine Myra UI mehr; Docking darf existieren, aber ohne Screen/Shop.

**Ergebnis:**
- `OrbitSandboxGame.cs`:
  - `using Myra` / `using Myra.Graphics2D.UI` entfernt.
  - Felder `_miningUi` und `_hud` entfernt.
  - `LoadContent`: keine MiningUi/Hud Instanziierung mehr.
  - `Update`: HUD-Update-Block entfernt.
  - `UpdateMiningMode` → umbenannt zu `UpdateDockedMode`: nur noch Sim-Update + Undock via B, keine UI-Interaktion.
  - `Draw`: `_hud.Render()` und `_miningUi.Render()` entfernt.
- `MiningUi.cs` und `Hud.cs` bleiben im Projekt (nicht gelöscht), werden aber nicht mehr referenziert.
- Build: ✅ grün.

---

### RH2 – Asteroid Rotation/Spin als Eigenschaft hinzufügen
**Status:** ✅ DONE

**Goal:** Jeder Asteroid hat `Rotation` und `RotationRate` (rad/s).

**Tasks**
- `models/Asteroid.cs`:
  - Felder ergänzen: `float Rotation`, `float RotationRate`.
- `WorldSim`:
  - Beim Spawn: `RotationRate` zufällig initialisieren (klein, +/-, z.B. ±0.5 rad/s).
  - In Update-Schritten: `Rotation += RotationRate * dt`.

**Ergebnis:**
- `models/Asteroid.cs`: `Rotation` (float, rad) und `RotationRate` (float, rad/s) Felder hinzugefügt.
- `WorldSim.SpawnAsteroids`: `Rotation` zufällig [0, 2π), `RotationRate` zufällig ±0.5 rad/s.
- `WorldSim.StepAsteroids`: `Rotation += RotationRate * dt` pro Frame.
- Build: ✅ grün.

**Acceptance**
- Asteroid Rotation ändert sich deterministisch mit der Zeit.

---

### RH3 – Spieler startet gedockt auf Asteroid
**Status:** ✅ DONE

**Goal:** Startzustand ist „Ship docked" auf einem Asteroiden.

**Tasks**
- `WorldSim` Init/Constructor:
  - Nach Asteroiden-Erzeugung: Start-Asteroid festlegen (z.B. nächster zur Ship-Spawn-Position oder erster „Inner Planets"-Asteroid).
  - Ship-Position auf Asteroid-Position setzen, Velocity angleichen.
  - `DockedAsteroidIndex` + `Mode = Mining` setzen.

**Ergebnis:**
- `WorldSim.Initialize`: nach `SpawnAsteroids()` wird `DockToClosestAsteroidOnStart()` aufgerufen.
- Neue Methode `DockToClosestAsteroidOnStart()`: iteriert über alle Asteroiden, findet den nächsten zur Ship-Spawn-Position, setzt Ship-Position/Velocity auf Asteroid, `_mode = GameMode.Mining`, `_dockedAsteroidIndex` auf den Index.
- Ship spawnt bei `SpawnRadius = 200_000` auf +X → nächster Asteroid ist aus dem "Main Belt" (170k–230k).
- Build: ✅ grün.

**Acceptance**
- Direkt nach Start ist Ship „fest" an einem Asteroiden.

---

### RH4 – New Input Mapping: Trigger = Stärke, A = Jump
**Status:** ✅ DONE

**Goal:** Neue Steuerung:
- LT/RT steuern Impulsstärke (0..1).
- A löst Impuls aus.
- Zoom auf Right Stick Y oder fix.

**Tasks**
- `OrbitSandboxGame.Update`:
  - LT/RT nicht mehr für Zoom verwenden.
  - Zoom auf Right Stick Y-Achse legen (oder fix belassen).
- Im Docked-Mode:
  - `strength01 = gp.Triggers.Right` (oder `RT - LT`).
  - `fireImpulse = Pressed(A)`.

**Ergebnis:**
- `OrbitSandboxGame.Update`: Zoom von LT/RT auf Right Stick Y umgelegt (`rs.Y * CameraZoomSpeed * realDt`).
- `UpdateDockedMode`: `strength01 = gp.Triggers.Right`, `fireImpulse = Pressed(A)`.
- Flight-Mode Impulse bleibt auf Left Stick + X (unverändert).
- Build: ✅ grün.

**Acceptance**
- A löst Impuls aus. Stärke reagiert auf Trigger.

---

### RH5 – Impulsrichtung rotiert mit Asteroid-Rotation
**Status:** ✅ DONE

**Goal:** Wenn gedockt: Impulsrichtung = Funktion der Asteroid-Rotation (nicht Left Stick).

**Design (minimal)
- `dir = new Vector2(cos(rot), sin(rot))` vom gedockten Asteroiden.
- `impulse = dir * (strength01 * maxImpulse)`.

**Tasks**
- `OrbitSandboxGame`:
  - Wenn docked: `_impulseAim` aus Asteroid-Rotation + Trigger-Stärke berechnen.
  - Impuls-Vektor weiterhin zeichnen (existierende Draw-Line), als visuelles Feedback.
- `WorldSim`:
  - Sicherstellen: Impuls auslösen while docked → undock + apply impulse.
  - Falls Impuls nur im Flight Mode akzeptiert wird: minimal erweitern.

**Ergebnis:**
- `WorldSim`: Neues Property `DockedAsteroidRotation` gibt die aktuelle Rotation des gedockten Asteroiden zurück.
- `WorldSim`: Neue Methode `JumpFromAsteroid(Vector2 impulse)` – ruft `Undock()` auf und addiert `impulse` auf `_ship.Velocity`.
- `OrbitSandboxGame.UpdateDockedMode`: Berechnet `_impulseAim` aus `DockedAsteroidRotation` + RT-Stärke. A ruft `JumpFromAsteroid(_impulseAim)`. B bleibt als einfaches Undock ohne Impuls.
- `OrbitSandboxGame.Draw`: Impuls-Vektor wird jetzt auch im Docked-Mode gezeichnet (rotiert sichtbar mit Asteroid).
- Build: ✅ grün.

**Acceptance**
- Impuls-Vektor dreht sich sichtbar, wenn Asteroid rotiert.
- A löst Sprung entlang Richtung aus.

---

### RH5.1 – Prediction im Docked Mode anzeigen
**Status:** ✅ DONE

**Goal:** Im docked mode die Prädiktion von Schiff und Target anzeigen.

**Ergebnis:**
- `WorldSim.UpdateMining`: Akzeptiert jetzt `impulseAim` Parameter. Statt Prediction zu clearen, werden `UpdateDockedPrediction(impulseAim)` und `UpdateTargetPredictionWithClosestApproach(impulseAim)` aufgerufen.
- `WorldSim.UpdateDockedPrediction`: Neue Methode – nutzt `impulseAim` direkt als deltaV (Richtung kommt von Asteroid-Rotation, nicht von ShipForward).
- `WorldSim.UpdateTargetPredictionWithClosestApproach`: Erweitert – prüft `_mode == GameMode.Mining` um im Docked-Mode `impulseAim` direkt als deltaV zu verwenden statt `ShipForward * magnitude`.
- `OrbitSandboxGame.UpdateDockedMode`: Gibt `_impulseAim` an `UpdateMining` weiter.
- Draw: Benötigt keine Änderung – `PredictedPath` wird bereits bedingungslos gezeichnet, `TargetPredictedPath` mit AsteroidTracker-Gate.
- Build: ✅ grün.

**Acceptance**
- Predicted path des Schiffs dreht sich mit Impuls-Richtung (Asteroid-Rotation + Trigger-Stärke).
- Target predicted path + closest approach werden auch im Docked-Mode angezeigt (wenn Target selektiert).

---

## Definition of Done (MVP)
- [x] RH0: Build grün
- [x] RH1: Keine UI
- [x] RH2: Asteroiden rotieren
- [x] RH3: Start gedockt
- [x] RH4: Trigger = Stärke, A = Jump
- [x] RH5: Impulsrichtung aus Rotation
- [ ] RH6: Feste Defaults
- [ ] RH7: Cleanup (optional)
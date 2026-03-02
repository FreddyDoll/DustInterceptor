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
  - Asteroid Daten (Rotation/Spin aktuell nicht offensichtlich).

---

## Milestone
**MVP:** Start gedockt → Impulsrichtung rotiert mit Asteroid → Stärke per Trigger → A springt → Ship fliegt → kann wieder andocken.

---

## Tickets (in sinnvoller Reihenfolge)

### RH0 – Repository/Build Safety (keine Feature-Arbeit)
**Goal:** Sicherstellen, dass wir vor Änderungen reproduzierbar bauen können.
- Projekt einmal bauen; grob checken ob Content/Assets vorhanden sind.

**Acceptance**
- Build ist grün.

---

### RH1 – Mining/Shop UI deaktivieren ("keine UI")
**Goal:** Keine Myra UI mehr; Docking darf existieren, aber ohne Screen/Shop.

**Implementation Notes**
- Minimal: `MiningUi` nicht instanziieren/rendern; Mining-Update-Flow nicht nutzen.
- Option A (am kleinsten): `GameMode.Mining` als „Docked“ bestehen lassen, aber ohne UI/Upgrades.
- Option B: Mining-Mode komplett entfernen und Docked-State separat behandeln (wahrscheinlich mehr Eingriff).

**Tasks**
- `OrbitSandboxGame`:
  - Entfernen der `MiningUi`-Abhängigkeit (Init/Load/Draw/UpdateMiningMode).
  - Wenn `WorldSim.Mode == Mining`: keine UI, nur `WorldSim.UpdateMining` (oder eine neue `UpdateDocked` die identisch ist).
- `services/MiningUi.cs`:
  - Vorläufig im Projekt lassen (nicht anfassen), aber nicht mehr verwenden.

**Acceptance**
- Spiel läuft ohne UI Overlay.
- Docking verursacht kein UI aufpoppen.

---

### RH2 – Asteroid Rotation/Spin als Eigenschaft hinzufügen
**Goal:** Jeder Asteroid hat `Rotation` und `RotationRate` (rad/s).

**Tasks**
- `models/Asteroid.cs`:
  - Felder/Properties ergänzen: `Rotation`, `RotationRate`.
- `WorldSim`:
  - Beim Spawn: `RotationRate` zufällig (klein, +/-) initialisieren.
  - In Update-Schritten: `Rotation += RotationRate * dt`.

**Acceptance**
- Asteroid Rotation ändert sich deterministisch mit der Zeit.

---

### RH3 – Spieler startet gedockt auf Asteroid
**Goal:** Startzustand ist „Ship docked“ auf einem Asteroiden.

**Tasks**
- `WorldSim` Init/Constructor:
  - Nach Asteroiden-Erzeugung: Start-Asteroid festlegen.
  - Ship so positionieren, dass Docking konsistent ist (am Rand oder direkt auf Position + Radius).
  - Docked-State setzen (`DockedAsteroidIndex`, `Mode = Mining` falls das der aktuelle Docked-Mechanismus ist).

**Acceptance**
- Direkt nach Start ist Ship „fest“ an einem Asteroiden.

---

### RH4 – New Input Mapping: Trigger = Stärke, A = Jump
**Goal:** Neue Steuerung ohne UI:
- LT/RT steuern Impulsstärke.
- A löst Impuls aus.

**Tasks**
- `OrbitSandboxGame.Update`:
  - LT/RT dürfen nicht mehr Zoom sein.
  - Zoom auf Shoulder (LB/RB) legen ODER Zoom fix lassen (minimal).
- `OrbitSandboxGame` Mode-spezifisch:
  - Im Docked-Mode: `strength01` aus Triggern berechnen.
  - `fireImpulse = Pressed(A)`.

**Acceptance**
- Beim Drücken von A wird (sofern geladen/erlaubt) ein Impuls ausgelöst.
- Stärke reagiert auf Trigger.

---

### RH5 – Impulsrichtung rotiert mit Asteroid-Rotation
**Goal:** Wenn gedockt: Impulsrichtung = Funktion aus Asteroid-Rotation (nicht aus Left Stick).

**Design (minimal, robust)**
- `dir = new Vector2(cos(rot), sin(rot))`.
- Optional 90° Offset, damit es „tangential“ wirkt: `rot + PI/2`.
- Stärke: `impulse = dir * (strength01 * maxImpulse)`.

**Tasks**
- `OrbitSandboxGame`:
  - Wenn docked: `_impulseAim` nicht aus LeftStick, sondern aus Asteroid-Rotation berechnen.
  - Impuls-Vektor weiterhin zeichnen (existierende
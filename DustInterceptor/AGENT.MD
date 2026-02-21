<!-- AGENT.MD -->
# Dust Interceptor – Agent Playbook (MonoGame + Myra)

This repo is intentionally "vibecode-first": fast iteration, minimal dependencies, and code that stays easy to move around.
We are currently past the Orbit Sandbox and have:
- 1000 asteroid ring + gravity + time warp
- docking/mining mode
- spatial hash broadphase
- Myra-based mining UI
- Target selection with orbital path preview

---

## Build + Run
- MonoGame (DesktopGL/Windows) assumed.
- If UI text is missing or errors occur:
  - Ensure you have a SpriteFont in Content (e.g. `UIFont.spritefont`) and content builds successfully.
  - Set Myra to use it if needed (varies by Myra version).

---

## Controls (current)
### Flight
- **Left Stick**: aim impulse direction + strength
- **X**: apply impulse (inaccuracy + cooldown)
- **Y**: cycle camera mode (Locked → Target Selection → Free Pan → Locked)
- **Right Stick**: 
  - *Locked mode*: no effect
  - *Target Selection mode*: move gray cursor reticle (yellow highlight auto-selects closest asteroid)
  - *Free Pan mode*: pan camera
- **A**: (Target Selection mode) select highlighted asteroid as target AND return to ship lock
- **LT/RT**: zoom out/in
- **LB/RB**: decrease/increase time scale (1/2/4/8/16/32/64)

### Mining
- **D-Pad Up/Down**: move menu selection (highlight)
- **A**: transfer materials (currently always transfers; selection is visual only unless refined)
- **B**: undock
- UI is displayed via Myra panel when docked.

---

## Current Features (Status)
### Simulation
- Planet-centered gravity: `a = -mu * rHat / r^2` with min-radius clamp
- Integration: semi-implicit Euler
- Time scales: 1..64 with substeps (max 16)

### World
- 1000 asteroids spawned in ring with near-circular orbit velocity + variation
- Asteroid sizes biased toward small (power distribution)
- Materials: Ice / Iron / Rock (amount scaled by asteroid radius²)
- Ship cargo: Ice / Iron / Rock
- Docking on circle collision; ship "sticks" to asteroid while docked

### Performance
- Broadphase collision via spatial hash grid bucketing
- Still single-threaded, simple data structures

### UI
- Mining dock panel in top-left (Myra)
- Shows docked asteroid materials and ship cargo
- Controller-friendly labels + selection highlight

### Target Selection (Ticket G2)
- Y button cycles through 3 camera modes: Locked to Ship, Target Selection, Free Pan
- In Target Selection mode:
  - Camera stays locked to ship
  - Right stick moves a gray cursor reticle relative to the ship
  - Yellow highlight automatically jumps to closest asteroid (excludes ship)
  - A button selects the highlighted asteroid AND returns to ship lock mode
  - Selected target shows orbital path prediction (same physics as ship prediction)
- Orange highlight ring around selected target
- Target's predicted path rendered in orange

---

## Design Rules (keep the prototype stable)
1. Keep dependencies light. Avoid physics engines unless needed.
2. One "truth" for physics: prediction uses same gravity + integration order as sim.
3. Keep Update deterministic; only random on impulse apply and initial spawn.
4. Avoid per-frame allocations in hot paths.
5. Prefer small, focused extractions over large architecture rewrites.

---

## Next Tickets (Agent Mode – do one at a time)

### Ticket P1 — Cargo capacity should affect ship mass (and therefore acceleration and docking "collisions")
- Problem: Cargo exists and has a capacity limit, but carrying resources does not change ship handling.
- Goal:
  - Add ship mass model: `shipMass = baseShipMass + cargoMassFactor * (shipIce + shipIron + shipRock)`.
  - Make impulse + gravity acceleration use mass appropriately (likely scale impulse acceleration down with mass; gravity can remain mass-independent unless using force-based model).
  - Update docking "collision" to consider mass (e.g., heavier ship has higher impact on the collision/the new direction).
- Notes:
  - Keep it simple and tunable via `GameConfig` constants.
  - Avoid adding allocations; just compute scalar mass each update.

### Ticket U1 — Remove deprecated impulse cooldown / inaccuracy upgrades (cleanup)
- Problem: DESIGN notes mark Impulse Cooldown and Impulse Inaccuracy upgrades as “remove”.
- Goal:
  - Remove `UpgradeType` entries (if present), their definitions in `UpgradeDefinitions`, and any UI exposure.
  - Keep the actual gameplay mechanics (cooldown + inaccuracy) as baseline/tunable values in `GameConfig` unless also intentionally removed.
- Acceptance:
  - Build succeeds; no dead/unused upgrade plumbing.

---

## Agent Guidance (how to work on changes)
- Prefer "move code" over rewriting logic.
- Keep structs/arrays as-is until things are stable.
- After extraction, optionally convert Asteroid/Body to small classes if needed (not now).
- Add small unit-test-like debug helpers only if they help verify refactors (e.g., print counts).

---

## Tuning Notes
- If orbits drift too much at high time scales:
  - increase max substeps (16→32) OR reduce `_baseDt`
- If docking feels too "sticky":
  - adjust pushSpeed on undock or add a short "collision grace" timer
- Target selection cursor:
  - Adjust `CursorSnapRadius` in GameConfig (default 5000 world units)
  - Cursor speed scales inversely with zoom for consistent feel
  - `CursorColor` = gray reticle, `HoveredAsteroidColor` = yellow auto-select highlight

---
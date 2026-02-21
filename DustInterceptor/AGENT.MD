<!-- AGENT.MD -->
# Dust Interceptor – Agent Playbook (MonoGame + Myra)

This repo is intentionally “vibecode-first”: fast iteration, minimal dependencies, and code that stays easy to move around.
We are currently past the Orbit Sandbox and have:
- 1000 asteroid ring + gravity + time warp
- docking/mining mode
- spatial hash broadphase
- Myra-based mining UI

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
- **Y**: lock camera to ship
- **Right Stick**: pan camera (panning unlocks camera)
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
- Docking on circle collision; ship “sticks” to asteroid while docked

### Performance
- Broadphase collision via spatial hash grid bucketing
- Still single-threaded, simple data structures

### UI
- Mining dock panel in top-left (Myra)
- Shows docked asteroid materials and ship cargo
- Controller-friendly labels + selection highlight

---

## Design Rules (keep the prototype stable)
1. Keep dependencies light. Avoid physics engines unless needed.
2. One “truth” for physics: prediction uses same gravity + integration order as sim.
3. Keep Update deterministic; only random on impulse apply and initial spawn.
4. Avoid per-frame allocations in hot paths.
5. Prefer small, focused extractions over large architecture rewrites.

---

## Next Tickets (Agent Mode – do one at a time)

### Ticket U1 — Add first real upgrade (MaxImpulse)
**Goal:** Add an upgrade purchasable in Mining UI.
- Cost in materials (start with Iron)
- Upgrade effect: `MaxImpulse += 200` (or similar)
- UI shows current value and cost
Acceptance:
- Buying reduces cargo and increases max impulse
- Can’t buy if insufficient cargo

### Ticket U2 — Upgrade cooldown reduction
**Goal:** Buy cooldown reduction.
- `ImpulseCooldown = max(0.1, ImpulseCooldown - 0.05)`
- Cost in materials
Acceptance:
- Works with existing cooldown code

### Ticket G1 — Target selection (later)
**Goal:** Select asteroid and display target orbit/path.
- Add selection reticle and A-select logic (flight mode)
Acceptance:
- You can select a target and see its path

---

## Agent Guidance (how to work on changes)
- Prefer “move code” over rewriting logic.
- Keep structs/arrays as-is until things are stable.
- After extraction, optionally convert Asteroid/Body to small classes if needed (not now).
- Add small unit-test-like debug helpers only if they help verify refactors (e.g., print counts).

---

## Tuning Notes
- If orbits drift too much at high time scales:
  - increase max substeps (16→32) OR reduce `_baseDt`
- If docking feels too “sticky”:
  - adjust pushSpeed on undock or add a short “collision grace” timer

---
# Dust Interceptor – Prototype Design (Vibecoding Spec)

## Pitch
A **2D top-down orbital mining** prototype: drift around a planet, plan impulses with an orbit preview, collide with asteroids to dock, mine materials, and upgrade your ship to eventually escape the system.

**Tech:** MonoGame (DesktopGL) · Myra UI · .NET 8

---

## Visual Style
Target vibe: **"Tiny Space Academy"**

### Implemented
- **Asteroids:** Per-asteroid pixel shader with material-based coloring (Ice/Iron/Rock ratios drive color blend); unique seed per asteroid for variation
- **Planet:** Gas giant pixel shader with swirling band colors
- **Background:** Polar grid shader (concentric circles + radial lines) that scales smoothly with zoom
- **Ship:** Texture-based sprite with rotation driven by a PD controller; tinted white in flight, green when docked
- **Trail:** Past trail rendered as a polyline behind the ship
- **Prediction line:** Ship predicted path rendered as a polyline; target predicted path in orange when tracking
- **Closest approach:** Connecting line + marker rings between ship and target at predicted closest approach point
- **Impulse vector:** Charge-up indicator from ship in forward direction; dim while cooling down, bright when ready

### Future Targets
- Ship cargo visualized in size and color (similar to asteroids)
- Fading trail with time-based length
- Patterned prediction line to distinguish from trail and convey time/direction

---

## Gameplay Loop

### Implemented
1. Orbit around planet
2. Use camera modes to find an asteroid (free pan or target selection with tracker upgrade)
3. Aim an impulse and watch predicted path + closest approach indicator
4. Fire impulse → coast (fuel consumed via rocket equation with specific impulse)
5. Collide with asteroid → dock automatically
6. Mine / transfer materials / purchase upgrades at mining dock UI
7. Undock and repeat — depleted asteroids are disabled on undock
8. Drop excess cargo in flight (D-Pad to select material, B to jettison)

### Target
- Grow ship → install upgraded drive + collect fuel → escape system

---

## Random Event System

### Implemented
- Event dialog system that pauses gameplay and blocks input until dismissed
- Events fire based on configurable triggers:
  - **Intro** (start of game): Introduction to the setting and controls
  - **Iron10** (resource threshold: 10 Iron): Hint about upgrades
  - **OneMinute** (60s simulation time): Hint about time compression
- Events are non-repeatable by default
- Trigger types: `OnStartTrigger`, `ResourceThresholdTrigger`, `SimulationTimeTrigger`

### Target
- More events with branching choices (FTL-style)
- Interaction screens at various milestones

---

## Story
- An accident happened to a large ship
- A small crew survived in a docked mining ship, but the systems are destroyed
- Time to scavenge asteroids, upgrade the ship, and eventually escape the system!

---

## Economy

### Resources (Implemented)
- **Ice** — used for navigation upgrades (Prediction, Zoom Range)
- **Iron** — used for propulsion, time control, mining, and tracker upgrades
- **Rock** — used for cargo capacity upgrades
- **Fuel** — consumed on each impulse burn; 250 starting fuel; found on asteroids

### Upgrades (Implemented)
Upgrades are purchased at the mining dock UI using resources. Each upgrade has scaling costs.

| Upgrade | Category | Resource | Description |
|---------|----------|----------|-------------|
| Impulse | Propulsion | Iron | Increases thruster power (additive per level) |
| Cooldown | Propulsion | Iron | Reduces thruster cooldown (multiplicative, max 3 levels) |
| Isp | Propulsion | Fuel | More efficient thrusters — less fuel per impulse (max 6 levels) |
| Time Warp | Time Control | Iron | Unlocks faster time compression (discrete: 1→2→4→...→1024) |
| Mining | Mining | Iron | Increases material transfer rate (additive + multiplicative) |
| Cargo | Mining | Rock | Increases cargo hold size |
| Prediction | Navigation | Ice | Extends trajectory preview (multiplicative) |
| Zoom Range | Navigation | Ice | Allows zooming out further (multiplicative, max 6 levels) |
| Tracker | Navigation | Iron | Unlock: enables target selection mode + asteroid orbit preview |

### Target
- Item shop at each asteroid with random selection (Brotato-style)
- Items give bonus/malus to lower-level stats

---

## Controls (As Implemented)

### Flight Mode
- **Left Stick**: Aim impulse direction + strength (ship rotates toward aim via PD controller)
- **X**: Fire impulse (consumes fuel, applies in ship's forward direction, starts cooldown)
- **Y**: Cycle camera mode:
  - *Locked*: camera follows ship, right stick has no effect
  - *Target Selection* (requires Tracker unlock): right stick moves cursor reticle relative to ship; yellow highlight auto-selects closest asteroid; **A** confirms target and returns to Locked
  - *Free Pan*: right stick pans camera freely
  - Without Tracker upgrade, Y toggles between Locked and Free Pan only
- **D-Pad Left/Right**: Cycle selected drop material (skips Fuel)
- **B**: Drop selected material (10 units per press)
- **LT / RT**: Zoom out / in
- **LB / RB**: Decrease / increase time scale (limited by Time Warp upgrade level)
- **Back**: Exit game

### Mining Mode (Docked)
- **D-Pad Up/Down**: Move menu selection (materials → upgrades → undock)
- **A / D-Pad Left / D-Pad Right**: (on material row) Toggle transfer direction: Load `>>>` → Unload `<<<` → Idle `|`
- **A**: (on upgrade row) Purchase upgrade if affordable
- **B**: Undock
- Transfer bandwidth is split evenly across all active material channels

---

## Entities

### Planet
- Static at origin
- Radius: 1,000 world units
- Gravity parameter `mu`: 1.0×10⁹
- Gas giant shader with configurable base + band colors

### Player Ship ("Mining Ship")
- Radius: 180 world units
- Spawns at 200,000 units from planet on +X axis with tangential circular-orbit velocity
- Base mass: 100 (increases with cargo)
- Rotation: PD controller tracks left-stick aim direction (P=8, D=4, max ω=6 rad/s)
- Impulse: fires in ship's forward direction, fuel consumed via Tsiolkovsky rocket equation
- Impulse cooldown: configurable per upgrade level
- Collision: circle-circle with asteroids triggers docking
- After undocking, collision with the same asteroid is ignored until ship clears a margin

### Asteroids
- Spawned across four configurable belts:
  - **Inner Planets**: 4 large asteroids (10k–60k radius range, 300–500 unit size)
  - **Main Belt**: 10,000 asteroids (170k–230k radius range, 5–60 unit size)
  - **Outer Planets**: 4 very large asteroids (400k–750k radius range, 1k–8k unit size)
  - **Outer Belt**: 1,000 asteroids (1M–2M radius range, 500–1,200 unit size)
- Sizes biased toward small (power distribution: `sizeFactor = rand²`)
- Near-circular orbit velocity with per-belt variation
- Materials: Ice / Iron / Rock / Fuel (amounts scaled by radius², ratios from belt bias config)
- Radius shrinks as materials are mined (`UpdateRadius`)
- Depleted asteroids are disabled on undock
- Rendered via per-asteroid shader with material ratio coloring
- LOD culling: asteroids below a minimum screen pixel size are skipped

---

## Physics & Simulation

### Gravity Model
- Direction: toward planet center
- Magnitude: `mu / r²`
- Implementation: `a = -mu * r̂ / |r|²`
- Minimum radius clamp at 85% planet radius prevents singularities

### Integration
- Semi-implicit Euler:
  - `v += a * dt`
  - `x += v * dt`
- Base timestep: 1/120s
- Time warp: `simDt = baseDt × timeScale`
- Ship rotation sub-stepped per time scale for smooth PD control

### Orbit Prediction
- Predicts future ship path by simulating a copied state forward using the same gravity model
- Preview includes the currently aimed impulse (applied in ship's forward direction before sim)
- Horizon auto-calculated from vis-viva equation (one orbital period), capped by Prediction upgrade
- 2,400 steps, sampled every 12th step for rendering
- Target prediction: same physics, run in parallel with ship prediction
- Closest approach: tracked across all prediction steps between ship and target paths

### Collision
- Broadphase: spatial hash grid (cell size 500 units)
- Narrowphase: circle-circle overlap test
- On collision: momentum-conserving dock (mass-weighted combined velocity)
- Undock ignore: recently undocked asteroid skipped until ship clears margin

---

## Performance
- Spatial hash grid for collision broadphase and asteroid queries
- LOD culling for asteroid rendering based on screen-space size
- Single-threaded, simple data structures
- Fixed 60 FPS target with vsync
- Resolution: 1920×1080 × 2 (scale factor)

---

## Architecture

### Project Structure
```
DustInterceptor/
├── config/          # GameConfig, WorldSimConfig, UpgradeDefinitions, MaterialDefinitions, EventDefinitions
├── enums/           # CameraMode, GameMode, GameEvent, MaterialType, UpgradeType, UpgradeCategory
├── models/          # Body, Asteroid, AsteroidBeltConfig, UpgradeDefinition, MaterialDefinition, EventDefinition
├── services/        # WorldSim, Camera2D, SpatialHashGrid, Hud, MiningUi, EventDialogUi, UpgradeManager, EventManager
├── Content/         # Shaders (BackgroundGrid, Planet, Asteroid), Textures (Ship)
├── OrbitSandboxGame.cs   # Main game class (input, rendering, mode management)
└── Program.cs
```

### Key Systems
- **WorldSim**: All simulation state (planet, ship, asteroids, physics, docking, mining, prediction)
- **UpgradeManager**: Upgrade registration, state tracking, purchase logic (supports additive, multiplicative, discrete, and unlock upgrades)
- **EventManager**: Event registration, trigger evaluation, dialog lifecycle
- **MiningUi**: Myra-based docking panel with material transfer controls and upgrade purchasing
- **Hud**: Flight HUD showing fuel, time scale, drop material, closest approach distance
- **Camera2D**: View matrix generation with position and zoom
- **SpatialHashGrid**: Bucketed spatial indexing for collision queries


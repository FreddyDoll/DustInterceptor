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

### Lower Level Loop
1. Orbit around planet
2. Use camera modes to find an asteroid (free pan or target selection with tracker upgrade)
3. Aim an impulse and watch predicted path + closest approach indicator
4. Fire impulse → coast (fuel consumed via rocket equation with specific impulse)
5. Collide with asteroid → dock automatically
6. Mine / transfer materials / purchase upgrades at mining dock UI
	- Manage your mass and fuel carefully — more cargo means more inertia, but you need resources to upgrade and escape! 
7. Undock and repeat

### Overall progression
- Start after crash
	- slightly higher orbit then main ring. no asteroids visible
	- In the imidiate area there is debris of the carrier
	- movement should feel "linear", no noticable impact of eliptical orbits yet
	- Collext closly scatter debris to unlock game mechanics as tutorial
	  - 2X time warp with usage cost
	  - Asteroid Tracker with closest approach
	  - cooldown mechanic with impulse fuel
	  - higher zoom range -> main belt becomes visible
- Progress inward to main ring

---

## Random Event System

### Implemented
- Event dialog system that pauses gameplay and blocks input until dismissed
- Events fire based on configurable triggers
- Events are non-repeatable by default
- Trigger types: `OnStartTrigger`, `ResourceThresholdTrigger`, `SimulationTimeTrigger`

---

## Story
### Intro
- A carrier with plans for the "genesis" mission to seed new planets has is entering the planet system
- Event: "The plans for "Genesis" ar our only hope. But there are other forces who want us stopped"
- Camera follows the carrier as it enters orbit.
- Event "We are approaching A gas giant around "HR 5191".
- Camera animates to Planet view.
- Event "Swing by coming up"
- Camera animates towards Carrier
- At lowest point the engines fire
- (white blank)
- Come back to small Miner with scattered depris
- -> pass over control to player

### Stage 1: "Scattered Debris"
- Tutorial stage with floating debris from the crash. No asteroids yet, but you can collect debris to unlock mechanics and upgrades.

### Stage 2: "Main Ring"

### Stage 3: "Inner Moons"

### Stage 4: "Outer Moons"

---

## Economy

### Resources (Implemented)
- **Light Exotics** 
  — Upgrade Category: Propulsioon
  - Upgrades: max Impulse, Cooldown, Isp
  -	Fuel for Impulse.
  - density: low
- **Heavy Exotics** 
  - Upgrade Category: Cryo System
  - Upgrades: Max Time Warp, Usage costs.
  -	Fuel for Timewarp.
  - density: high.
- **Metals**
  — Upgrade Category: Structural 
  - Upgrades:Cargo, Mining Speed, Zoom Range, Prediction
  - density: medium.
- **Debris** 
  — Acts as unlock currency for gating
  - density: high.

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
- Gating by events: max level is adjusted by events

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


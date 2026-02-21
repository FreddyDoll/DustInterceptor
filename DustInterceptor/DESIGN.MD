# Dust Interceptor – Prototype Design (Vibecoding Spec)

## Pitch
A **2D top-down orbital mining** prototype: drift around a planet, plan impulses with an orbit preview, collide with asteroids to dock later, mine materials, and upgrade your ship to eventually escape the system.

---

## Visual Style
Target vibe: **“Tiny Space Academy”**
Future Targets:
Pixelshaders
	Asteroids: Generated Texture with uneven outline, colorsheme based on material composition (ice/iron/rock)
	Planet: GasGiant shader with swirling colors
Ship: 
	Visualize current cargo hold in size and color(similar to Asteroid);
TrailLine:
	Fading trail behind ship
	length determined by time
	light in color
prediction line:
	sharpline (screenspace?)
	With pattern to distinguish from trail and give user feedback over time/direction
	

---

## Gameplay Loop (Target)
1. Orbit around planet
2. Pan camera, find an asteroid
3. Aim an impulse and watch predicted path
4. Fire impulse → coast
5. Collide with asteroid → dock (planned)
6. Mine / transfer materials / upgrade (planned)
7. Undock and repeat (planned)

Long-term goal: grow ship → install upgraded drive + collect fuel → escape system.

---

## Controls (As Implemented)
- **Left Stick**: Aim impulse direction + strength
- **X**: Fire impulse (adds inaccuracy, starts cooldown)
- **Y**: Lock camera to ship
- **Right Stick**: Pan camera (panning unlocks camera from ship) -> change camera is always locked on player, right stick gets available with upgrade (cursor to select an asteroid and track)
- **LT**: Zoom out
- **RT**: Zoom in
- **LB**: Decrease time scale
- **RB**: Increase time scale

---

## Entities

### Planet
- Static at origin
- Radius: `_planetRadius`
- Gravity controlled via parameter `mu` (gravitational parameter)

### Player Ship (“Mining Ship”)
Current:
- Circle with `Position`, `Velocity`, `Radius`

Planned:
- Mass/density-driven radius
- Storage: materials (eventually align to Iron/Copper/Carbon/Water)
- Upgrades affecting impulse, cooldown, accuracy, mining limits, etc.

### Asteroids
Current:
- `Position`, `Velocity`, `Radius`
- Material composition: `Ice`, `Iron`, `Rock` (sums to ~1)
- Color derived from composition

Planned:
- Expand material set to match gameplay economy
- Mining tiers / requirements for larger bodies

---

## Physics & Simulation

### Gravity model
Current gravity acceleration:
- Direction: toward planet
- Magnitude: proportional to `mu / r^2`
- Implementation: `a = -mu * rHat / |r|^2`
- Minimum radius clamp prevents singularities near the planet

### Integration
- Semi-implicit Euler:
  - `v += a * dt`
  - `x += v * dt`

### Time scaling
- Discrete scale steps: `1, 2, 4, 8, 16, 32, 64`
- Physics uses **sub-stepping** (up to 16) to maintain stability at high scales

### Orbit prediction
- Predicts future ship path by simulating a copied ship state forward
- Preview includes the currently aimed impulse (added to velocity before sim)
- Draws predicted path as a polyline in world space

---

## Technical Constraints
- C# + **MonoGame**
- Keep dependencies light
- Avoid Box2D unless we truly need complex contacts/constraints
- Target scale: 100_000 asteroids + stable orbits + fast orbit preview

## Upgrades
- MinZoomLevel (allows zooming out further)
- Predictionlength (longer path preview)
- Asteroid predictor (enables path of selected asteroid)
	- Asteroid closest approach
- Mining speed
- Impulse upgrades:
  - Strength
  - Cooldown
  - Accuracy
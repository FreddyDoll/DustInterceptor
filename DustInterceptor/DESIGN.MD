# Dust Interceptor – Prototype Design (Vibecoding Spec)

## Pitch
A **2D top-down orbital mining** prototype: drift around a planet, plan impulses with an orbit preview, collide with asteroids to dock later, mine materials, and upgrade your ship to eventually escape the system.

---

## Visual Style
Target vibe: **"Tiny Space Academy"**
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
	sharpline 
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

grow ship → install upgraded drive + collect fuel → escape system.

## Random Event System
- Think a bit "Faster then light"
- There are interaction screens at certain triggers
  - Start of Game:
	- "Ther was an accident and most of your ship is gone"
	- "We will have to scavage some asteroids to build up again"
	- -> OK
  - After mining 10 iron
	- "If only"
  - After 1 min of game time

## Story
- An accident happened to a large ship
- A small crew survived in a docked mining ship. but the systems are destroyed
- Time to scavenge asteroids, upgrade the ship, and eventually escape the system!

## Economy
- Resources: 
	- Ice, Iron, Rock (eventually more)
-Upgrades:
	- Think "Brotato"
	- Lower Lavel stats (current system)
	- Theres a set of Items that are bought with resources and give you a bonus/malus to Lower Lavel stats
	- Instead of buying directly you get a shop at each asteroid with a random selection of items/upgrades (with some rules to avoid too much bad ones)

-- -

## Controls (As Implemented)
- **Left Stick**: Aim impulse direction + strength
- **X**: Fire impulse (adds inaccuracy, starts cooldown)
- **Y**: Cycle through camera modes:
  - *Locked*: camera follows ship, right stick has no effect
  - *Target Selection*: right stick moves cursor reticle to select asteroid target (auto-highlight closest), A confirms target and returns to Locked
  - *Free Pan*: right stick pans camera, allowing you to look around without moving the ship; pressing Y again returns to Locked
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

### Player Ship ("Mining Ship")
- needs update

### Asteroids
-needs update

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


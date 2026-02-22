using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace DustInterceptor
{
    /// <summary>
    /// Contains all world simulation state and logic:
    /// planet, ship, asteroids, physics, docking, materials, prediction.
    /// </summary>
    public sealed class WorldSim
    {
        // Configuration
        private readonly WorldSimConfig _config;
        private readonly Random _rng = new();

        // State
        private Body _planet;
        private Body _ship;
        private Asteroid[] _asteroids = null!;

        // Cargo
        private float _shipIce;
        private float _shipIron;
        private float _shipRock;

        // Mode
        private GameMode _mode = GameMode.Flight;
        private int _dockedAsteroidIndex = -1;

        // Collision ignore (skip collision with this asteroid until cleared with margin)
        private int _ignoreAsteroidIndex = -1;
        private const float UndockClearanceMargin = 50f;

        // Collision broadphase (fine-grained for collision detection)
        private readonly SpatialHashGrid _spatialHash = new();

        // Trail
        private readonly Queue<Vector2> _shipTrail = new();
        private float _trailSampleTimer;

        // Prediction
        private readonly List<Vector2> _predictedPath = new();
        private float _predictionHorizonSeconds;

        // Target prediction
        private readonly List<Vector2> _targetPredictedPath = new();
        private int _selectedTargetIndex = -1;

        // Impulse state
        private float _impulseCooldownLeft;

        // Mining state
        private float _miningTransferRate;

        public WorldSim(WorldSimConfig config)
        {
            _config = config;
            Initialize();
        }

        /// <summary>
        /// Sets the mining transfer rate (materials per second).
        /// </summary>
        public void SetMiningTransferRate(float rate)
        {
            _miningTransferRate = rate;
        }

        /// <summary>
        /// Sets the prediction horizon in seconds.
        /// </summary>
        public void SetPredictionHorizon(float seconds)
        {
            _predictionHorizonSeconds = seconds;
        }

        private void Initialize()
        {
            // Planet at origin
            _planet = new Body
            {
                Position = Vector2.Zero,
                Velocity = Vector2.Zero,
                Radius = _config.PlanetRadius
            };

            // Ship: start on +X axis at outer edge, with tangential velocity for near-circular orbit
            var startPos = new Vector2(_config.SpawnRadius, 0);
            float vCircular = MathF.Sqrt(_config.Mu / startPos.Length());
            var tangentialDir = new Vector2(0, 1);

            _ship = new Body
            {
                Position = startPos,
                Velocity = tangentialDir * vCircular,
                Radius = _config.ShipRadius,
                Rotation = MathF.PI / 2f, // Start pointing up (in velocity direction)
                AngularVelocity = 0f
            };

            SpawnAsteroids();
        }

        private void SpawnAsteroids()
        {
            int totalCount = _config.AsteroidBelts.Sum(b => b.Count);
            _asteroids = new Asteroid[totalCount];

            int index = 0;
            foreach (var belt in _config.AsteroidBelts)
            {
                for (int i = 0; i < belt.Count; i++)
                {
                    float angle = (float)(_rng.NextDouble() * Math.PI * 2.0);

                    float t = (float)_rng.NextDouble();
                    float radius = MathF.Sqrt(t) * (belt.OuterRadius - belt.InnerRadius) + belt.InnerRadius;

                    Vector2 pos = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;

                    float vCirc = MathF.Sqrt(_config.Mu / radius);
                    float variation = 1f + ((float)_rng.NextDouble() * 2f - 1f) * belt.OrbitVariation;
                    float speed = vCirc * variation;

                    Vector2 tangent = new Vector2(-MathF.Sin(angle), MathF.Cos(angle));
                    Vector2 vel = tangent * speed;

                    float sizeFactor = MathF.Pow((float)_rng.NextDouble(), 2f);
                    float asteroidRadius = belt.RadiusMin + sizeFactor * (belt.RadiusMax - belt.RadiusMin);

                    // Generate materials with belt-specific bias
                    float totalBias = belt.IceBias + belt.IronBias + belt.RockBias;
                    float iceWeight = belt.IceBias / totalBias;
                    float ironWeight = belt.IronBias / totalBias;
                    float rockWeight = belt.RockBias / totalBias;

                    // Random variation around the bias
                    float ice = iceWeight * (0.5f + (float)_rng.NextDouble());
                    float iron = ironWeight * (0.5f + (float)_rng.NextDouble());
                    float rock = rockWeight * (0.5f + (float)_rng.NextDouble());

                    // Normalize
                    float totalMat = ice + iron + rock;
                    ice /= totalMat;
                    iron /= totalMat;
                    rock /= totalMat;

                    float materialScale = asteroidRadius * asteroidRadius * 0.1f;

                    _asteroids[index++] = new Asteroid
                    {
                        Position = pos,
                        Velocity = vel,
                        Radius = asteroidRadius,
                        Ice = ice * materialScale,
                        Iron = iron * materialScale,
                        Rock = rock * materialScale,
                        Disabled = false
                    };
                }
            }
        }

        // ===== Public accessors =====
        public ref Body Planet => ref _planet;
        public ref Body Ship => ref _ship;
        public Asteroid[] Asteroids => _asteroids;
        public IReadOnlyCollection<Vector2> ShipTrail => _shipTrail;
        public IReadOnlyList<Vector2> PredictedPath => _predictedPath;
        public IReadOnlyList<Vector2> TargetPredictedPath => _targetPredictedPath;
        public int SelectedTargetIndex => _selectedTargetIndex;
        public GameMode Mode => _mode;
        public int DockedAsteroidIndex => _dockedAsteroidIndex;

        public float ShipIce => _shipIce;
        public float ShipIron => _shipIron;
        public float ShipRock => _shipRock;

        public float ImpulseCooldownLeft => _impulseCooldownLeft;

        /// <summary>
        /// Current ship mass (base mass + cargo contribution).
        /// </summary>
        public float ShipMass => ComputeShipMass();

        /// <summary>
        /// Gets the ship's current rotation angle in radians.
        /// </summary>
        public float ShipRotation => _ship.Rotation;

        /// <summary>
        /// Gets the ship's forward direction as a unit vector.
        /// </summary>
        public Vector2 ShipForward => new Vector2(MathF.Cos(_ship.Rotation), MathF.Sin(_ship.Rotation));

        // ===== Cargo operations =====

        /// <summary>
        /// Gets the amount of a specific resource type in ship cargo.
        /// </summary>
        public float GetResource(ResourceType resource)
        {
            return resource switch
            {
                ResourceType.Ice => _shipIce,
                ResourceType.Iron => _shipIron,
                ResourceType.Rock => _shipRock,
                _ => 0f
            };
        }

        /// <summary>
        /// Attempts to spend the specified amount of a resource. Returns true if successful.
        /// </summary>
        public bool TrySpendResource(ResourceType resource, float amount)
        {
            return resource switch
            {
                ResourceType.Ice => TrySpendIce(amount),
                ResourceType.Iron => TrySpendIron(amount),
                ResourceType.Rock => TrySpendRock(amount),
                _ => false
            };
        }

        /// <summary>
        /// Attempts to spend the specified amount of ice. Returns true if successful.
        /// </summary>
        public bool TrySpendIce(float amount)
        {
            if (_shipIce >= amount)
            {
                _shipIce -= amount;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Attempts to spend the specified amount of iron. Returns true if successful.
        /// </summary>
        public bool TrySpendIron(float amount)
        {
            if (_shipIron >= amount)
            {
                _shipIron -= amount;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Attempts to spend the specified amount of rock. Returns true if successful.
        /// </summary>
        public bool TrySpendRock(float amount)
        {
            if (_shipRock >= amount)
            {
                _shipRock -= amount;
                return true;
            }
            return false;
        }

        // ===== Update methods =====

        /// <summary>
        /// Updates the simulation in flight mode.
        /// </summary>
        public void UpdateFlight(float realDt, int timeScale, Vector2 impulseAim, bool fireImpulse, float maxImpulse, float impulseCooldown)
        {
            // Check if we've cleared the ignored asteroid
            if (_ignoreAsteroidIndex >= 0 && _ignoreAsteroidIndex < _asteroids.Length)
            {
                ref var ignoredAsteroid = ref _asteroids[_ignoreAsteroidIndex];
                float dist = Vector2.Distance(_ship.Position, ignoredAsteroid.Position);
                float clearanceRadius = _ship.Radius + ignoredAsteroid.Radius + UndockClearanceMargin;

                if (dist > clearanceRadius)
                {
                    _ignoreAsteroidIndex = -1; // Cleared, re-enable collision
                }
            }

            // Cooldown (scales with time warp)
            if (_impulseCooldownLeft > 0f)
                _impulseCooldownLeft = Math.Max(0f, _impulseCooldownLeft - realDt * timeScale);

            // Calculate aim magnitude for rotation logic
            float aimMagnitude = impulseAim.Length();

            //Physics
            float simDt = _config.BaseDt * timeScale;
            //Step asteroids fully for performance
            StepAsteroids(simDt);
            StepBody(ref _ship, simDt);
            // Physics simulation (sub-stepped) - includes rotation controller
            float dtSub = simDt / timeScale;
            for (int i = 0; i < timeScale; i++)
            {
                UpdateShipRotation(dtSub, impulseAim, aimMagnitude);
            }

            // Apply impulse in ship's forward direction (not aim direction)
            if (fireImpulse && _impulseCooldownLeft <= 0f && aimMagnitude > 0.001f)
            {
                // Get ship's forward direction
                Vector2 forward = ShipForward;
                float shipMass = ComputeShipMass();

                var deltaV = forward * aimMagnitude;

                _ship.Velocity += deltaV;
                _impulseCooldownLeft = impulseCooldown;
            }

            // Collision detection
            UpdateSpatialHash();
            CheckShipCollisions();

            // Trail sampling
            _trailSampleTimer += realDt * timeScale;
            while (_trailSampleTimer >= _config.TrailSamplePeriod)
            {
                _trailSampleTimer -= _config.TrailSamplePeriod;
                _shipTrail.Enqueue(_ship.Position);
                while (_shipTrail.Count > _config.ShipTrailMax)
                    _shipTrail.Dequeue();
            }

            // Prediction (using ship's current forward direction for impulse preview)
            UpdatePrediction(impulseAim);

            // Update target prediction
            UpdateTargetPrediction();
        }

        /// <summary>
        /// Updates ship rotation using a PD controller to reach target angle.
        /// </summary>
        private void UpdateShipRotation(float dt, Vector2 impulseAim, float aimMagnitude)
        {
            // Check if aim is above deadzone
            if (aimMagnitude > _config.AimDeadzone * _config.AimDeadzone * 100f) // Compare with squared magnitude threshold
            {
                // Calculate target angle from aim direction
                float targetAngle = MathF.Atan2(impulseAim.Y, impulseAim.X);
                
                // Calculate angle error (shortest path)
                float error = NormalizeAngle(targetAngle - _ship.Rotation);
                
                // PD controller: torque = P * error - D * angularVelocity
                float torque = _config.RotationPGain * error - _config.RotationDGain * _ship.AngularVelocity;
                
                // Apply torque to angular velocity
                _ship.AngularVelocity += torque * dt;
            }
            else
            {
                // No target - apply damping to bring ship to rest
                _ship.AngularVelocity -= _ship.AngularVelocity * _config.AngularDamping * dt;
            }

            // Clamp angular velocity
            _ship.AngularVelocity = Clamp(_ship.AngularVelocity, -_config.MaxAngularVelocity, _config.MaxAngularVelocity);

            // Integrate rotation
            _ship.Rotation += _ship.AngularVelocity * dt;

            // Keep rotation in [-PI, PI] range
            _ship.Rotation = NormalizeAngle(_ship.Rotation);
        }

        /// <summary>
        /// Normalizes an angle to the range [-PI, PI].
        /// </summary>
        private static float NormalizeAngle(float angle)
        {
            while (angle > MathF.PI) angle -= 2f * MathF.PI;
            while (angle < -MathF.PI) angle += 2f * MathF.PI;
            return angle;
        }

        /// <summary>
        /// Updates the simulation in mining mode.
        /// </summary>
        public void UpdateMining(float realDt, int timeScale)
        {
            // Auto-transfer materials (scales with time warp)
            if (_dockedAsteroidIndex >= 0 && _dockedAsteroidIndex < _asteroids.Length)
            {
                ref var asteroid = ref _asteroids[_dockedAsteroidIndex];
                
                // Transfer at rate scaled by time warp
                float transferAmount = _miningTransferRate * realDt * timeScale;
                
                // Calculate how much to transfer from each material type proportionally
                float totalOnAsteroid = asteroid.TotalMaterials;
                if (totalOnAsteroid > 0.001f)
                {
                    float iceRatio = asteroid.Ice / totalOnAsteroid;
                    float ironRatio = asteroid.Iron / totalOnAsteroid;
                    float rockRatio = asteroid.Rock / totalOnAsteroid;

                    // Transfer proportionally, but clamp to available
                    float iceTransfer = Math.Min(asteroid.Ice, transferAmount * iceRatio);
                    float ironTransfer = Math.Min(asteroid.Iron, transferAmount * ironRatio);
                    float rockTransfer = Math.Min(asteroid.Rock, transferAmount * rockRatio);

                    asteroid.Ice -= iceTransfer;
                    asteroid.Iron -= ironTransfer;
                    asteroid.Rock -= rockTransfer;

                    _shipIce += iceTransfer;
                    _shipIron += ironTransfer;
                    _shipRock += rockTransfer;
                }

                // Ship sticks to asteroid
                _ship.Position = asteroid.Position;
                _ship.Velocity = asteroid.Velocity;
            }

            float simDt = _config.BaseDt * timeScale;
            StepAsteroids(simDt);

            // Keep ship stuck to asteroid after physics
            if (_dockedAsteroidIndex >= 0 && _dockedAsteroidIndex < _asteroids.Length)
            {
                ref var asteroid = ref _asteroids[_dockedAsteroidIndex];
                _ship.Position = asteroid.Position;
                _ship.Velocity = asteroid.Velocity;
            }

            // Update spatial hash for rendering culling (asteroids moved)
            UpdateSpatialHash();

            // Trail sampling (keep updating while docked so the past trail remains "alive" in the shop)
            _trailSampleTimer += realDt * timeScale;
            while (_trailSampleTimer >= _config.TrailSamplePeriod)
            {
                _trailSampleTimer -= _config.TrailSamplePeriod;
                _shipTrail.Enqueue(_ship.Position);
                while (_shipTrail.Count > _config.ShipTrailMax)
                    _shipTrail.Dequeue();
            }

            // Clear prediction while docked
            _predictedPath.Clear();
            _targetPredictedPath.Clear();
        }

        /// <summary>
        /// Undocks from the current asteroid and returns to flight mode.
        /// </summary>
        public void Undock()
        {
            _mode = GameMode.Flight;

            // Remember which asteroid to ignore until cleared
            _ignoreAsteroidIndex = _dockedAsteroidIndex;

            if (_dockedAsteroidIndex >= 0)
            {
                ref var asteroid = ref _asteroids[_dockedAsteroidIndex];

                _ship.Velocity = asteroid.Velocity;

                // Check if asteroid is depleted and mark it as disabled
                if (asteroid.IsDepleted)
                {
                    asteroid.Ice = 0f;
                    asteroid.Iron = 0f;
                    asteroid.Rock = 0f;
                    asteroid.Disabled = true;
                }
            }


            _dockedAsteroidIndex = -1;
        }

        /// <summary>
        /// Clears the ship trail (e.g., on dock).
        /// </summary>
        public void ClearTrail()
        {
            _shipTrail.Clear();
        }

        /// <summary>
        /// Sets the selected target asteroid index. -1 means no target.
        /// </summary>
        public void SetSelectedTarget(int asteroidIndex)
        {
            if (asteroidIndex >= 0 && asteroidIndex < _asteroids.Length && !_asteroids[asteroidIndex].Disabled)
            {
                _selectedTargetIndex = asteroidIndex;
            }
            else
            {
                _selectedTargetIndex = -1;
            }
        }

        /// <summary>
        /// Clears the selected target.
        /// </summary>
        public void ClearSelectedTarget()
        {
            _selectedTargetIndex = -1;
            _targetPredictedPath.Clear();
        }

        /// <summary>
        /// Finds the closest asteroid to a given position within a specified radius.
        /// Returns -1 if no asteroid is found.
        /// Excludes asteroids that overlap with the ship position.
        /// </summary>
        public int FindClosestAsteroid(Vector2 position, float maxRadius)
        {
            int closestIndex = -1;
            float closestDistSq = maxRadius * maxRadius;

            // Use spatial hash for efficient query
            foreach (int i in _spatialHash.Query(position, maxRadius))
            {
                if (_asteroids[i].Disabled)
                    continue;

                // Skip asteroids that overlap with the ship (prevents selecting ship as target)
                float distToShip = Vector2.Distance(_asteroids[i].Position, _ship.Position);
                float overlapRadius = _ship.Radius + _asteroids[i].Radius;
                if (distToShip < overlapRadius)
                    continue;

                float distSq = Vector2.DistanceSquared(position, _asteroids[i].Position);
                if (distSq < closestDistSq)
                {
                    closestDistSq = distSq;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }

        // ===== Physics =====

        private void StepBody(ref Body b, float dt)
        {
            Vector2 acc = ComputeGravityAcceleration(b.Position);
            b.Velocity += acc * dt;
            b.Position += b.Velocity * dt;
            // Note: Rotation is updated separately in UpdateShipRotation for the ship
        }

        private void StepAsteroids(float dt)
        {
            for (int i = 0; i < _asteroids.Length; i++)
            {
                // Skip disabled asteroids
                if (_asteroids[i].Disabled)
                    continue;

                Vector2 acc = ComputeGravityAcceleration(_asteroids[i].Position);
                _asteroids[i].Velocity += acc * dt;
                _asteroids[i].Position += _asteroids[i].Velocity * dt;
            }
        }

        private Vector2 ComputeGravityAcceleration(Vector2 position)
        {
            Vector2 r = position - _planet.Position;
            float rLen = r.Length();

            float minR = _planet.Radius * 0.85f;
            if (rLen < minR)
                rLen = minR;

            Vector2 rHat = r / rLen;
            float accMag = _config.Mu / (rLen * rLen);

            return -accMag * rHat;
        }

        private void UpdatePrediction(Vector2 impulseAim)
        {
            _predictedPath.Clear();

            Vector2 pos = _ship.Position;

            // Use same mass-scaled impulse preview as the real impulse.

            // Get ship's forward direction
            Vector2 forward = ShipForward;
            float shipMass = ComputeShipMass();
            var deltaV = forward * impulseAim.Length();

            Vector2 vel = _ship.Velocity + deltaV;

            float horizon = ComputeOneOrbitHorizonSeconds(pos, vel);
            horizon = Math.Min(_predictionHorizonSeconds, horizon);
            if (horizon <= 0.001f)
                return;

            float dt = horizon / _config.PredictSteps;

            for (int i = 0; i <= _config.PredictSteps; i++)
            {
                Vector2 acc = ComputeGravityAcceleration(pos);
                vel += acc * dt;
                pos += vel * dt;

                if (i % _config.PredictSampleEvery == 0)
                    _predictedPath.Add(pos);
            }
        }

        private void UpdateTargetPrediction()
        {
            _targetPredictedPath.Clear();

            if (_selectedTargetIndex < 0 || _selectedTargetIndex >= _asteroids.Length)
                return;

            ref var target = ref _asteroids[_selectedTargetIndex];
            if (target.Disabled)
            {
                _selectedTargetIndex = -1;
                return;
            }

            Vector2 pos = target.Position;
            Vector2 vel = target.Velocity;

            float horizon = ComputeOneOrbitHorizonSeconds(pos, vel);
            horizon = Math.Min(_predictionHorizonSeconds, horizon);
            if (horizon <= 0.001f)
                return;

            float dt = horizon / _config.PredictSteps;

            for (int i = 0; i <= _config.PredictSteps; i++)
            {
                Vector2 acc = ComputeGravityAcceleration(pos);
                vel += acc * dt;
                pos += vel * dt;

                if (i % _config.PredictSampleEvery == 0)
                    _targetPredictedPath.Add(pos);
            }
        }

        /// <summary>
        /// Computes an approximate "one orbit" time horizon at the given state.
        /// Uses orbital period from semi-major axis (works for ellipses):
        /// a = 1 / (2/r - v^2/μ),  T = 2π * sqrt(a^3/μ)
        /// Falls back to circular approximation when not on a bound (elliptic) orbit.
        /// </summary>
        private float ComputeOneOrbitHorizonSeconds(Vector2 position, Vector2 velocity)
        {
            float mu = Math.Max(_config.Mu, 0.001f);

            float r = (position - _planet.Position).Length();
            r = Math.Max(r, _planet.Radius * 0.85f);

            float v2 = velocity.LengthSquared();

            // Vis-viva: v^2 = μ(2/r - 1/a) => 1/a = 2/r - v^2/μ
            float invA = (2f / r) - (v2 / mu);

            // Bound orbit requires a > 0 (invA > 0). If invA <= 0, it's parabolic/hyperbolic.
            if (invA > 1e-9f)
            {
                float a = 1f / invA;

                // Kepler's third law.
                float period = 2f * MathF.PI * MathF.Sqrt((a * a * a) / mu);
                if (float.IsFinite(period) && period > 0.001f)
                    return period;
            }

            // Fallback: local circular orbit period at current radius.
            float circularPeriod = 2f * MathF.PI * MathF.Sqrt((r * r * r) / mu);
            return (float.IsFinite(circularPeriod) && circularPeriod > 0.001f) ? circularPeriod : 0f;
        }

        // ===== Collision =====

        private void UpdateSpatialHash()
        {
            _spatialHash.Clear(_config.SpatialHashCellSize);

            for (int i = 0; i < _asteroids.Length; i++)
            {
                // Skip disabled asteroids - don't add them to spatial hash
                if (_asteroids[i].Disabled)
                    continue;

                _spatialHash.Insert(i, _asteroids[i].Position, _asteroids[i].Radius);
            }
        }

        /// <summary>
        /// Queries asteroids visible in a rectangular area (for rendering culling).
        /// Uses LOD-based spatial hash - when minAsteroidRadius > 0, only queries larger asteroids.
        /// </summary>
        /// <param name="center">Center of query area</param>
        /// <param name="halfWidth">Half-width of visible area in world units</param>
        /// <param name="halfHeight">Half-height of visible area in world units</param>
        /// <param name="minAsteroidRadius">Minimum asteroid radius to return (for LOD culling)</param>
        public IEnumerable<int> QueryVisibleAsteroids(Vector2 center, float halfWidth, float halfHeight, float minAsteroidRadius = 0f)
        {
            for (int i = 0; i < _asteroids.Length; i++)
            {
                var roid = _asteroids[i];
                var left = center.X - halfWidth;
                var right = center.X + halfWidth;
                var top = center.Y - halfHeight;
                var bottom = center.Y + halfHeight;

                // Skip disabled asteroids - don't add them to spatial hash
                if (_asteroids[i].Disabled)
                    continue;

                if (_asteroids[i].Radius < minAsteroidRadius)
                    continue;

                if(roid.Position.X > left && roid.Position.X < right && roid.Position.Y > top && roid.Position.Y < bottom)
                    yield return i;
            }
        }

        private void CheckShipCollisions()
        {
            float queryRadius = _ship.Radius + _config.MaxAsteroidRadius;

            foreach (int asteroidIndex in _spatialHash.Query(_ship.Position, queryRadius))
            {
                // Skip the ignored asteroid (just undocked from)
                if (asteroidIndex == _ignoreAsteroidIndex)
                    continue;

                ref var asteroid = ref _asteroids[asteroidIndex];

                // Skip disabled asteroids (should not be in hash, but double-check)
                if (asteroid.Disabled)
                    continue;

                float distSq = Vector2.DistanceSquared(_ship.Position, asteroid.Position);
                float radiusSum = _ship.Radius + asteroid.Radius;

                if (distSq < radiusSum * radiusSum)
                {
                    DockToAsteroid(asteroidIndex);
                    break;
                }
            }
        }

        private void DockToAsteroid(int asteroidIndex)
        {
            _mode = GameMode.Mining;
            _dockedAsteroidIndex = asteroidIndex;
            _ignoreAsteroidIndex = -1; // Clear ignore when docking to new asteroid

            ref var asteroid = ref _asteroids[asteroidIndex];

            // Calculate perfectly inelastic collision velocity
            // v_final = (m_ship * v_ship + m_asteroid * v_asteroid) / (m_ship + m_asteroid)
            float shipMass = ComputeShipMass();
            float asteroidMass = asteroid.Mass;
            float totalMass = shipMass + asteroidMass;
            if (totalMass <= 0.001f)
                totalMass = 0.001f;

            Vector2 combinedVelocity = (shipMass * _ship.Velocity + asteroidMass * asteroid.Velocity) / totalMass;

            // Apply combined velocity to both bodies
            asteroid.Velocity = combinedVelocity;
            _ship.Velocity = combinedVelocity;
            _ship.Position = asteroid.Position;

            _shipTrail.Clear();
        }

        // ===== Utils =====

        private float ComputeShipMass()
        {
            float cargo = _shipIce + _shipIron + _shipRock;
            float mass = _config.BaseShipMass + cargo;
            return mass <= 0.001f ? 0.001f : mass;
        }

        private static Vector2 RandomUnitVector2(Random rng)
        {
            double a = rng.NextDouble() * Math.PI * 2.0;
            return new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
        }

        private static int ClampInt(int v, int min, int max) =>
            (v < min) ? min : (v > max) ? max : v;

        private static float Clamp(float v, float min, float max) =>
            (v < min) ? min : (v > max) ? max : v;
    }
}

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

        // Collision broadphase
        private readonly SpatialHashGrid _spatialHash = new();

        // Trail
        private readonly Queue<Vector2> _shipTrail = new();
        private float _trailSampleTimer;

        // Prediction
        private readonly List<Vector2> _predictedPath = new();
        private float _predictionHorizonSeconds;

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
                Mass = 1f,
                Density = 1f,
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
                Density = 1f,
                Mass = 1f,
                Radius = _config.ShipRadius
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
        public GameMode Mode => _mode;
        public int DockedAsteroidIndex => _dockedAsteroidIndex;

        public float ShipIce => _shipIce;
        public float ShipIron => _shipIron;
        public float ShipRock => _shipRock;

        public float ImpulseCooldownLeft => _impulseCooldownLeft;

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
        public void UpdateFlight(float realDt, int timeScale, Vector2 impulseAim, bool fireImpulse, float maxImpulse, float inaccuracy, float impulseCooldown)
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

            // Apply impulse
            if (fireImpulse && _impulseCooldownLeft <= 0f && impulseAim.LengthSquared() > 0.001f)
            {
                Vector2 scatter = RandomUnitVector2(_rng) * (inaccuracy * impulseAim.Length());
                _ship.Velocity += impulseAim + scatter;
                _impulseCooldownLeft = impulseCooldown;
            }

            // Physics simulation (sub-stepped)
            float simDt = _config.BaseDt * timeScale;
            int subSteps = ClampInt(timeScale, 1, 16);
            float dtSub = simDt / subSteps;

            for (int i = 0; i < subSteps; i++)
            {
                StepBody(ref _ship, dtSub);
                StepAsteroids(dtSub);
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

            // Prediction
            UpdatePrediction(impulseAim);
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

            // Physics still runs for active asteroids
            float simDt = _config.BaseDt * timeScale;
            int subSteps = ClampInt(timeScale, 1, 16);
            float dtSub = simDt / subSteps;

            for (int i = 0; i < subSteps; i++)
            {
                StepAsteroids(dtSub);
            }

            // Keep ship stuck to asteroid after physics
            if (_dockedAsteroidIndex >= 0 && _dockedAsteroidIndex < _asteroids.Length)
            {
                ref var asteroid = ref _asteroids[_dockedAsteroidIndex];
                _ship.Position = asteroid.Position;
                _ship.Velocity = asteroid.Velocity;
            }

            // Clear prediction while docked
            _predictedPath.Clear();
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

        // ===== Physics =====

        private void StepBody(ref Body b, float dt)
        {
            Vector2 acc = ComputeGravityAcceleration(b.Position);
            b.Velocity += acc * dt;
            b.Position += b.Velocity * dt;
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
            Vector2 vel = _ship.Velocity + impulseAim;

            float dt = _predictionHorizonSeconds / _config.PredictSteps;

            for (int i = 0; i <= _config.PredictSteps; i++)
            {
                Vector2 acc = ComputeGravityAcceleration(pos);
                vel += acc * dt;
                pos += vel * dt;

                if (i % _config.PredictSampleEvery == 0)
                    _predictedPath.Add(pos);
            }
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
            float shipMass = _config.ShipMass;
            float asteroidMass = asteroid.Mass;
            float totalMass = shipMass + asteroidMass;

            Vector2 combinedVelocity = (shipMass * _ship.Velocity + asteroidMass * asteroid.Velocity) / totalMass;

            // Apply combined velocity to both bodies
            asteroid.Velocity = combinedVelocity;
            _ship.Velocity = combinedVelocity;
            _ship.Position = asteroid.Position;

            _shipTrail.Clear();
        }

        // ===== Utils =====

        private static Vector2 RandomUnitVector2(Random rng)
        {
            double a = rng.NextDouble() * Math.PI * 2.0;
            return new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
        }

        private static int ClampInt(int v, int min, int max) =>
            (v < min) ? min : (v > max) ? max : v;
    }
}

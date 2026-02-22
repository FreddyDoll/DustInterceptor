using System.Collections.Generic;

namespace DustInterceptor
{
    /// <summary>
    /// Configuration for WorldSim. Tuning constants.
    /// </summary>
    public sealed class WorldSimConfig
    {
        // Physics
        public float Mu = 1.0e9f;
        public float BaseDt = 1f / 120f;

        // Planet
        public float PlanetRadius = 1000f;

        // Ship
        public float ShipRadius = 180f;
        public float SpawnRadius = 200_000f;

        /// <summary>
        /// Base ship mass with empty cargo.
        /// </summary>
        public float BaseShipMass = 100.0f;

        // === Ship Rotation (PID Controller) ===
        /// <summary>
        /// Proportional gain for rotation PID controller.
        /// Higher values = faster response, but may overshoot.
        /// </summary>
        public float RotationPGain = 8.0f;

        /// <summary>
        /// Derivative gain for rotation PID controller.
        /// Higher values = more damping, reduces overshoot.
        /// </summary>
        public float RotationDGain = 4.0f;

        /// <summary>
        /// Maximum angular velocity in radians per second.
        /// </summary>
        public float MaxAngularVelocity = 6.0f;

        /// <summary>
        /// Deadzone for aim stick. Below this magnitude, ship won't try to rotate.
        /// </summary>
        public float AimDeadzone = 0.15f;

        /// <summary>
        /// Angular damping applied when no target angle (stick in deadzone).
        /// Brings ship to rest when not aiming.
        /// </summary>
        public float AngularDamping = 3.0f;

        // Asteroid Belts
        public AsteroidBeltConfig[] AsteroidBelts =
        [
            new AsteroidBeltConfig
            {
                Name = "Inner Planets",
                Count = 4,
                InnerRadius = 10_000f,
                OuterRadius = 60_000f,
                RadiusMin = 300f,
                RadiusMax = 500f,
                OrbitVariation = 0.002f,
                MaterialBiases = new Dictionary<MaterialType, float>
                {
                    { MaterialType.Ice, 0.1f },
                    { MaterialType.Iron, 0.5f },
                    { MaterialType.Rock, 0.2f },
                    { MaterialType.Fuel, 0.1f }
                }
            },
            new AsteroidBeltConfig
            {
                Name = "Main Belt",
                Count = 10000,
                InnerRadius = 170_000f,
                OuterRadius = 230_000f,
                RadiusMin = 5f,
                RadiusMax = 60f,
                OrbitVariation = 0.01f,
                MaterialBiases = new Dictionary<MaterialType, float>
                {
                    { MaterialType.Ice, 0.33f },
                    { MaterialType.Iron, 0.33f },
                    { MaterialType.Rock, 0.24f },
                    { MaterialType.Fuel, 0.1f }
                }
            },
            new AsteroidBeltConfig
            {
                Name = "Outer Planets",
                Count = 4,
                InnerRadius = 400_000f,
                OuterRadius = 750_000f,
                RadiusMin = 1_000f,
                RadiusMax = 8_000f,
                OrbitVariation = 0.1f,
                MaterialBiases = new Dictionary<MaterialType, float>
                {
                    { MaterialType.Ice, 0.6f },
                    { MaterialType.Iron, 0.1f },
                    { MaterialType.Rock, 0.2f },
                    { MaterialType.Fuel, 0.1f }
                }
            },
            new AsteroidBeltConfig
            {
                Name = "Outer Belt",
                Count = 1000,
                InnerRadius = 1_000_000f,
                OuterRadius = 2_000_000f,
                RadiusMin = 500f,
                RadiusMax = 1200f,
                OrbitVariation = 0.4f,
                MaterialBiases = new Dictionary<MaterialType, float>
                {
                    { MaterialType.Ice, 0.6f },
                    { MaterialType.Iron, 0.1f },
                    { MaterialType.Rock, 0.2f },
                    { MaterialType.Fuel, 0.1f }
                }
            }
        ];

        // Collision
        public float SpatialHashCellSize = 500f;

        // Trail
        public int ShipTrailMax = 2200;
        public float TrailSamplePeriod = 1f / 30f;

        // Prediction
        public int PredictSteps = 2400;
        public int PredictSampleEvery = 12;

        /// <summary>
        /// Gets the maximum asteroid radius across all belts (for collision queries).
        /// </summary>
        public float MaxAsteroidRadius
        {
            get
            {
                float max = 0f;
                foreach (var belt in AsteroidBelts)
                {
                    if (belt.RadiusMax > max)
                        max = belt.RadiusMax;
                }
                return max;
            }
        }
    }
}

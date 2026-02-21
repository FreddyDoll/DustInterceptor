using Microsoft.Xna.Framework;

namespace DustInterceptor
{
    public struct Asteroid
    {
        /// <summary>
        /// Density constant for asteroid mass calculation.
        /// </summary>
        private const float AsteroidDensity = 0.001f;

        public Vector2 Position;
        public Vector2 Velocity;
        public float Radius;

        public float Ice;
        public float Iron;
        public float Rock;

        /// <summary>
        /// Whether this asteroid is disabled (depleted/visited).
        /// Disabled asteroids are not rendered, simulated, or collided with.
        /// </summary>
        public bool Disabled;

        /// <summary>
        /// Total materials remaining on this asteroid.
        /// </summary>
        public readonly float TotalMaterials => Ice + Iron + Rock;

        /// <summary>
        /// Whether this asteroid has been depleted of all materials.
        /// </summary>
        public readonly bool IsDepleted => TotalMaterials < 0.001f;

        /// <summary>
        /// Mass of the asteroid, proportional to volume (radius^3).
        /// Uses a density constant for scaling.
        /// </summary>
        public readonly float Mass => Radius * Radius * Radius * AsteroidDensity;
    }
}
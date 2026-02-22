using System.Collections.Generic;

namespace DustInterceptor
{
    /// <summary>
    /// Configuration for a single circular asteroid belt.
    /// </summary>
    public sealed class AsteroidBeltConfig
    {
        /// <summary>Name of the belt for identification.</summary>
        public string Name { get; init; } = "Belt";

        /// <summary>Number of asteroids in this belt.</summary>
        public int Count { get; init; } = 1000;

        /// <summary>Inner radius of the belt ring.</summary>
        public float InnerRadius { get; init; } = 10_000f;

        /// <summary>Outer radius of the belt ring.</summary>
        public float OuterRadius { get; init; } = 50_000f;

        /// <summary>Minimum asteroid radius.</summary>
        public float RadiusMin { get; init; } = 3f;

        /// <summary>Maximum asteroid radius.</summary>
        public float RadiusMax { get; init; } = 60f;

        /// <summary>Orbit velocity variation (±percentage).</summary>
        public float OrbitVariation { get; init; } = 0.03f;

        /// <summary>
        /// Material biases by type. Keys are MaterialType, values are relative weights (0-1).
        /// Any material not present defaults to 0.
        /// </summary>
        public Dictionary<MaterialType, float> MaterialBiases { get; init; } = new()
        {
            { MaterialType.Ice, 0.33f },
            { MaterialType.Iron, 0.33f },
            { MaterialType.Rock, 0.34f }
        };

        /// <summary>
        /// Gets the material bias for a given type. Returns 0 if not configured.
        /// </summary>
        public float GetMaterialBias(MaterialType type)
        {
            return MaterialBiases.TryGetValue(type, out float bias) ? bias : 0f;
        }
    }
}

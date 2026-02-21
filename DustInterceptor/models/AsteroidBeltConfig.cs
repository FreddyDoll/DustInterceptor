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

        /// <summary>Material bias: ice weight (0-1).</summary>
        public float IceBias { get; init; } = 0.33f;

        /// <summary>Material bias: iron weight (0-1).</summary>
        public float IronBias { get; init; } = 0.33f;

        /// <summary>Material bias: rock weight (0-1).</summary>
        public float RockBias { get; init; } = 0.34f;
    }
}

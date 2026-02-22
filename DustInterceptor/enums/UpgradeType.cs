namespace DustInterceptor
{
    /// <summary>
    /// Identifies different upgrade types in the game.
    /// Each type corresponds to a specific stat or capability.
    /// </summary>
    public enum UpgradeType
    {
        // === Impulse/Thruster ===
        ImpulseStrength,
        ImpulseCooldown,
        SpecificImpulse,

        // === Time Control ===
        MaxTimeScale,

        // === Mining ===
        MiningSpeed,
        CargoCapacity,

        // === Navigation ===
        PredictionLength,
        MinZoomLevel,
        AsteroidTracker,

        // === Future ===
        // FuelEfficiency,
        // ShieldStrength,
        // SensorRange,
    }
}

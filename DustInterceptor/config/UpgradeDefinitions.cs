namespace DustInterceptor
{
    /// <summary>
    /// Factory for creating all upgrade definitions.
    /// Central place to configure all upgrade parameters.
    /// </summary>
    public static class UpgradeDefinitions
    {
        /// <summary>
        /// Creates and registers all upgrade definitions into the manager.
        /// </summary>
        public static void RegisterAll(UpgradeManager manager)
        {
            // === Propulsion Upgrades ===
            manager.Register(new UpgradeDefinition
            {
                Type = UpgradeType.ImpulseStrength,
                Name = "Impulse",
                Description = "Increases thruster power",
                Category = UpgradeCategory.Propulsion,
                CostResource = ResourceType.Iron,
                BaseCost = 20f,
                CostMultiplier = 1.8f,
                BaseValue = 20f,
                ValuePerLevel = 2f
            });

            manager.Register(new UpgradeDefinition
            {
                Type = UpgradeType.ImpulseCooldown,
                Name = "Cooldown",
                Description = "Reduces thruster cooldown",
                Category = UpgradeCategory.Propulsion,
                CostResource = ResourceType.Iron,
                BaseCost = 75f,
                CostMultiplier = 2f,
                BaseValue = 12.00f,
                FactorPerLevel = 0.5f,  
                MaxLevel = 3  
            });

            // === Time Control Upgrades ===
            manager.Register(new UpgradeDefinition
            {
                Type = UpgradeType.MaxTimeScale,
                Name = "Time Warp",
                Description = "Unlocks faster time compression",
                Category = UpgradeCategory.TimeControl,
                CostResource = ResourceType.Iron,
                BaseCost = 50f,
                CostMultiplier = 2f,
                DiscreteValues = [1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024]
            }, startingLevel: 1);

            // === Mining Upgrades ===
            manager.Register(new UpgradeDefinition
            {
                Type = UpgradeType.MiningSpeed,
                Name = "Mining",
                Description = "Increases material transfer rate",
                Category = UpgradeCategory.Mining,
                CostResource = ResourceType.Iron,
                BaseCost = 75f,
                CostMultiplier = 1.5f,
                BaseValue = 10f,
                ValuePerLevel = 2f,
                FactorPerLevel = 1.2f,  
            });

            manager.Register(new UpgradeDefinition
            {
                Type = UpgradeType.CargoCapacity,
                Name = "Cargo",
                Description = "Increases cargo hold size",
                Category = UpgradeCategory.Mining,
                CostResource = ResourceType.Rock,
                BaseCost = 100f,
                CostMultiplier = 2f,
                BaseValue = 1000f,
                ValuePerLevel = 500f
            });

            // === Navigation Upgrades ===
            manager.Register(new UpgradeDefinition
            {
                Type = UpgradeType.PredictionLength,
                Name = "Prediction",
                Description = "Extends trajectory preview",
                Category = UpgradeCategory.Navigation,
                CostResource = ResourceType.Ice,
                BaseCost = 50f,
                CostMultiplier = 1.4f,
                BaseValue = 1000f,  // Seconds of prediction
                FactorPerLevel = 1.1f,
                ValuePerLevel = 1f
            });

            manager.Register(new UpgradeDefinition
            {
                Type = UpgradeType.MinZoomLevel,
                Name = "Zoom Range",
                Description = "Allows zooming out further",
                Category = UpgradeCategory.Navigation,
                CostResource = ResourceType.Ice,
                BaseCost = 40f,
                CostMultiplier = 1.6f,
                BaseValue = 0.8f,   // Start more zoomed in (higher = more restrictive)
                FactorPerLevel = 0.5f, 
                MaxLevel = 6  
            });

            manager.Register(new UpgradeDefinition
            {
                Type = UpgradeType.AsteroidTracker,
                Name = "Tracker",
                Description = "Track asteroid trajectories",
                Category = UpgradeCategory.Navigation,
                CostResource = ResourceType.Iron,
                BaseCost = 1000f,
                CostMultiplier = 1f,  // One-time purchase
                IsUnlock = true,
                MaxLevel = 1
            });
        }
    }
}

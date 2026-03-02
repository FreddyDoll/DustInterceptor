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
                CostResource = MaterialType.LightExotics,
                BaseCost = 20f,
                CostMultiplier = 1.8f,
                BaseValue = 20f,
                ValuePerLevel = 2f,
                MaxLevel = 1,
            });

            manager.Register(new UpgradeDefinition
            {
                Type = UpgradeType.ImpulseCooldown,
                Name = "Cooldown",
                Description = "Reduces thruster cooldown",
                Category = UpgradeCategory.Propulsion,
                CostResource = MaterialType.LightExotics,
                BaseCost = 75f,
                CostMultiplier = 2f,
                BaseValue = 12.00f,
                FactorPerLevel = 0.5f,
                MaxLevel = 1,
            });

            manager.Register(new UpgradeDefinition
            {
                Type = UpgradeType.SpecificImpulse,
                Name = "Isp",
                Description = "More efficient thrusters (less fuel per impulse)",
                Category = UpgradeCategory.Propulsion,
                CostResource = MaterialType.LightExotics,
                BaseCost = 50f,
                CostMultiplier = 1.7f,
                BaseValue = 250f,
                ValuePerLevel = 50f,
                MaxLevel = 1,
            });

            // === Cryo System Upgrades ===
            manager.Register(new UpgradeDefinition
            {
                Type = UpgradeType.MaxTimeScale,
                Name = "Time Warp",
                Description = "Unlocks faster time compression",
                Category = UpgradeCategory.Cryo,
                CostResource = MaterialType.HeavyExotics,
                BaseCost = 50f,
                CostMultiplier = 2f,
                DiscreteValues = [1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024],
                MaxLevel = 1,
            }, startingLevel: 1);
            manager.Register(new UpgradeDefinition
            {
                //not implemented yet
                Type = UpgradeType.CryoUsage,
                Name = "Cryo efficiency",
                Description = "Unlocks faster time compression",
                Category = UpgradeCategory.Cryo,
                CostResource = MaterialType.HeavyExotics,
                //missing
                MaxLevel = 1,
            }, startingLevel: 1);

            // === Structural Upgrades ===
            manager.Register(new UpgradeDefinition
            {
                Type = UpgradeType.MiningSpeed,
                Name = "Mining",
                Description = "Increases material transfer rate",
                Category = UpgradeCategory.Structural,
                CostResource = MaterialType.Metalls,
                BaseCost = 75f,
                CostMultiplier = 1.5f,
                BaseValue = 10f,
                ValuePerLevel = 2f,
                FactorPerLevel = 1.2f,
                MaxLevel = 1,
            });

            manager.Register(new UpgradeDefinition
            {
                Type = UpgradeType.CargoCapacity,
                Name = "Cargo",
                Description = "Increases cargo hold size",
                Category = UpgradeCategory.Structural,
                CostResource = MaterialType.Metalls,
                BaseCost = 100f,
                CostMultiplier = 2f,
                BaseValue = 1000f,
                ValuePerLevel = 500f,
                MaxLevel = 1,
            });
            manager.Register(new UpgradeDefinition
            {
                Type = UpgradeType.PredictionLength,
                Name = "Prediction",
                Description = "Extends trajectory preview",
                Category = UpgradeCategory.Structural,
                CostResource = MaterialType.Metalls,
                BaseCost = 50f,
                CostMultiplier = 1.4f,
                BaseValue = 1000f,  // Seconds of prediction
                FactorPerLevel = 1.1f,
                ValuePerLevel = 1f,
                MaxLevel = 1,
            });
            manager.Register(new UpgradeDefinition
            {
                Type = UpgradeType.MinZoomLevel,
                Name = "Zoom Range",
                Description = "Allows zooming out further",
                Category = UpgradeCategory.Structural,
                CostResource = MaterialType.Metalls,
                BaseCost = 40f,
                CostMultiplier = 1.6f,
                BaseValue = 0.008f,   // Start more zoomed in (higher = more restrictive)
                FactorPerLevel = 0.5f,
                MaxLevel = 1,
            });
        }
    }
}

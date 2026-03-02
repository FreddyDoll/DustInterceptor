using System;

namespace DustInterceptor
{
    /// <summary>
    /// Defines an upgrade's properties: costs, effects, limits.
    /// This is the "blueprint" - immutable configuration data.
    /// </summary>
    public sealed class UpgradeDefinition
    {
        /// <summary>
        /// Unique identifier for this upgrade.
        /// </summary>
        public UpgradeType Type { get; init; }

        /// <summary>
        /// Display name shown in UI.
        /// </summary>
        public string Name { get; init; } = "";

        /// <summary>
        /// Short description of what this upgrade does.
        /// </summary>
        public string Description { get; init; } = "";

        /// <summary>
        /// Category for UI grouping.
        /// </summary>
        public UpgradeCategory Category { get; init; }

        /// <summary>
        /// Material type used to purchase this upgrade.
        /// </summary>
        public MaterialType CostResource { get; init; } = MaterialType.LightExotics;

        /// <summary>
        /// Base cost at level 0 (first purchase).
        /// </summary>
        public float BaseCost { get; init; } = 50f;

        /// <summary>
        /// Cost multiplier per level (exponential growth).
        /// Set to 1.0 for flat cost.
        /// </summary>
        public float CostMultiplier { get; init; } = 2f;

        /// <summary>
        /// Base value of the stat at level 0 (before any upgrades).
        /// </summary>
        public float BaseValue { get; init; }

        /// <summary>
        /// Amount added per upgrade level (additive).
        /// Applied first: BaseValue + (ValuePerLevel * level)
        /// </summary>
        public float ValuePerLevel { get; init; }

        /// <summary>
        /// Multiplier applied per level (multiplicative).
        /// Applied after additive: result * (FactorPerLevel ^ level)
        /// Default is 1.0 (no multiplicative scaling).
        /// </summary>
        public float FactorPerLevel { get; init; } = 1f;

        /// <summary>
        /// Maximum upgrade level. -1 means unlimited.
        /// </summary>
        public int MaxLevel { get; init; } = -1;

        /// <summary>
        /// Whether this upgrade unlocks a feature (boolean) rather than scaling a value.
        /// </summary>
        public bool IsUnlock { get; init; }

        /// <summary>
        /// Optional: specific discrete values per level (overrides BaseValue + ValuePerLevel).
        /// Useful for things like time scale tiers: [2, 4, 8, 16, 32, 64]
        /// </summary>
        public float[]? DiscreteValues { get; init; }

        /// <summary>
        /// Gets the cost to purchase the next level.
        /// </summary>
        public float GetCost(int currentLevel)
        {
            return BaseCost * MathF.Pow(CostMultiplier, currentLevel);
        }

        /// <summary>
        /// Gets the effective value at a given level.
        /// Formula: (BaseValue + ValuePerLevel * level) * (FactorPerLevel ^ level)
        /// </summary>
        public float GetValue(int level)
        {
            if (IsUnlock)
                return level > 0 ? 1f : 0f;

            if (DiscreteValues != null && level >= 0 && level < DiscreteValues.Length)
                return DiscreteValues[level];

            // First: additive scaling
            float value = BaseValue + (ValuePerLevel * level);
            
            // Then: multiplicative scaling (only if FactorPerLevel != 1)
            if (FactorPerLevel != 1f)
                value *= MathF.Pow(FactorPerLevel, level);

            return value;
        }

        /// <summary>
        /// Checks if this upgrade can be purchased (not maxed out).
        /// </summary>
        public bool CanUpgrade(int currentLevel)
        {
            if (MaxLevel >= 0 && currentLevel >= MaxLevel)
                return false;

            if (DiscreteValues != null && currentLevel >= DiscreteValues.Length - 1)
                return false;

            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace DustInterceptor
{
    /// <summary>
    /// Tracks the current state of a single upgrade.
    /// </summary>
    public sealed class UpgradeState
    {
        public UpgradeDefinition Definition { get; }
        public int Level { get; private set; }

        public UpgradeState(UpgradeDefinition definition, int startingLevel = 0)
        {
            Definition = definition;
            Level = startingLevel;
        }

        /// <summary>
        /// Current effective value of this upgrade.
        /// </summary>
        public float Value => Definition.GetValue(Level);

        /// <summary>
        /// Cost to purchase the next level.
        /// </summary>
        public float NextCost => Definition.GetCost(Level);

        /// <summary>
        /// Whether this upgrade can be purchased further.
        /// </summary>
        public bool CanUpgrade => Definition.CanUpgrade(Level);

        /// <summary>
        /// Whether this is a feature unlock that has been purchased.
        /// </summary>
        public bool IsUnlocked => Definition.IsUnlock && Level > 0;

        /// <summary>
        /// Increments the level by 1. Call only after confirming purchase is valid.
        /// </summary>
        public void ApplyUpgrade()
        {
            if (CanUpgrade)
                Level++;
        }
    }

    /// <summary>
    /// Central manager for all upgrades. Handles registration, state, and purchases.
    /// </summary>
    public sealed class UpgradeManager
    {
        private readonly Dictionary<UpgradeType, UpgradeState> _upgrades = new();

        /// <summary>
        /// Registers an upgrade definition. Call during initialization.
        /// </summary>
        public void Register(UpgradeDefinition definition, int startingLevel = 0)
        {
            _upgrades[definition.Type] = new UpgradeState(definition, startingLevel);
        }

        /// <summary>
        /// Gets the state for a specific upgrade type.
        /// </summary>
        public UpgradeState Get(UpgradeType type)
        {
            return _upgrades.TryGetValue(type, out var state) 
                ? state 
                : throw new KeyNotFoundException($"Upgrade {type} not registered");
        }

        /// <summary>
        /// Tries to get an upgrade state, returns null if not found.
        /// </summary>
        public UpgradeState? TryGet(UpgradeType type)
        {
            return _upgrades.TryGetValue(type, out var state) ? state : null;
        }

        /// <summary>
        /// Gets the current value of an upgrade. Returns defaultValue if not found.
        /// </summary>
        public float GetValue(UpgradeType type, float defaultValue = 0f)
        {
            return _upgrades.TryGetValue(type, out var state) ? state.Value : defaultValue;
        }

        /// <summary>
        /// Gets the current level of an upgrade. Returns 0 if not found.
        /// </summary>
        public int GetLevel(UpgradeType type)
        {
            return _upgrades.TryGetValue(type, out var state) ? state.Level : 0;
        }

        /// <summary>
        /// Checks if a feature upgrade has been unlocked.
        /// </summary>
        public bool IsUnlocked(UpgradeType type)
        {
            return _upgrades.TryGetValue(type, out var state) && state.IsUnlocked;
        }

        /// <summary>
        /// Gets all registered upgrades.
        /// </summary>
        public IEnumerable<UpgradeState> GetAll() => _upgrades.Values;

        /// <summary>
        /// Gets all upgrades in a specific category.
        /// </summary>
        public IEnumerable<UpgradeState> GetByCategory(UpgradeCategory category)
        {
            return _upgrades.Values.Where(u => u.Definition.Category == category);
        }

        /// <summary>
        /// Gets all upgrades that can currently be purchased (not maxed).
        /// </summary>
        public IEnumerable<UpgradeState> GetAvailableUpgrades()
        {
            return _upgrades.Values.Where(u => u.CanUpgrade);
        }

        /// <summary>
        /// Attempts to purchase an upgrade. Returns true if successful.
        /// </summary>
        /// <param name="type">The upgrade to purchase</param>
        /// <param name="getResource">Function to get current resource amount</param>
        /// <param name="spendResource">Function to spend resources, returns true if successful</param>
        public bool TryPurchase(UpgradeType type, Func<ResourceType, float> getResource, Func<ResourceType, float, bool> spendResource)
        {
            if (!_upgrades.TryGetValue(type, out var state))
                return false;

            if (!state.CanUpgrade)
                return false;

            float cost = state.NextCost;
            ResourceType resource = state.Definition.CostResource;

            if (getResource(resource) < cost)
                return false;

            if (!spendResource(resource, cost))
                return false;

            state.ApplyUpgrade();
            return true;
        }

        /// <summary>
        /// Checks if an upgrade can be afforded.
        /// </summary>
        public bool CanAfford(UpgradeType type, Func<ResourceType, float> getResource)
        {
            if (!_upgrades.TryGetValue(type, out var state))
                return false;

            if (!state.CanUpgrade)
                return false;

            return getResource(state.Definition.CostResource) >= state.NextCost;
        }

        /// <summary>
        /// Gets upgrade data for UI display.
        /// </summary>
        public UpgradeDisplayData GetDisplayData(UpgradeType type, Func<ResourceType, float> getResource)
        {
            var state = Get(type);
            return new UpgradeDisplayData
            {
                Type = type,
                Name = state.Definition.Name,
                Description = state.Definition.Description,
                Category = state.Definition.Category,
                CurrentLevel = state.Level,
                CurrentValue = state.Value,
                NextCost = state.CanUpgrade ? state.NextCost : 0,
                CostResource = state.Definition.CostResource,
                CanAfford = CanAfford(type, getResource),
                CanUpgrade = state.CanUpgrade,
                IsUnlock = state.Definition.IsUnlock,
                IsUnlocked = state.IsUnlocked
            };
        }
    }

    /// <summary>
    /// Data transfer object for UI display of an upgrade.
    /// </summary>
    public struct UpgradeDisplayData
    {
        public UpgradeType Type;
        public string Name;
        public string Description;
        public UpgradeCategory Category;
        public int CurrentLevel;
        public float CurrentValue;
        public float NextCost;
        public ResourceType CostResource;
        public bool CanAfford;
        public bool CanUpgrade;
        public bool IsUnlock;
        public bool IsUnlocked;

        /// <summary>
        /// Gets a formatted string for the cost display.
        /// </summary>
        public readonly string GetCostString()
        {
            if (!CanUpgrade)
                return "MAXED";
            return $"{NextCost:F0} {CostResource}";
        }

        /// <summary>
        /// Gets a formatted string for the current value.
        /// </summary>
        public readonly string GetValueString(string format = "F0", string? suffix = null)
        {
            if (IsUnlock)
                return IsUnlocked ? "UNLOCKED" : "LOCKED";
            return $"{CurrentValue.ToString(format)}{suffix ?? ""}";
        }
    }
}

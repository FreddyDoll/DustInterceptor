using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace DustInterceptor
{
    /// <summary>
    /// Central registry of all material definitions.
    /// Similar pattern to UpgradeDefinitions: a static class that holds all material blueprints.
    /// </summary>
    public static class MaterialDefinitions
    {
        private static readonly Dictionary<MaterialType, MaterialDefinition> _definitions = new();

        /// <summary>
        /// All registered material definitions.
        /// </summary>
        public static IReadOnlyDictionary<MaterialType, MaterialDefinition> All => _definitions;

        /// <summary>
        /// All material types in registration order.
        /// </summary>
        public static IEnumerable<MaterialType> AllTypes => _definitions.Keys;

        /// <summary>
        /// Number of registered materials.
        /// </summary>
        public static int Count => _definitions.Count;

        /// <summary>
        /// Gets a material definition by type. Throws if not found.
        /// </summary>
        public static MaterialDefinition Get(MaterialType type) => _definitions[type];

        /// <summary>
        /// Tries to get a material definition. Returns null if not found.
        /// </summary>
        public static MaterialDefinition? TryGet(MaterialType type) =>
            _definitions.TryGetValue(type, out var def) ? def : null;

        /// <summary>
        /// Registers all built-in material definitions.
        /// Call once during game initialization.
        /// </summary>
        public static void RegisterAll()
        {
            Register(new MaterialDefinition
            {
                Type = MaterialType.Ice,
                Name = "Ice",
                Color = new Color(200, 230, 255),
                Density = 0.9f
            });

            Register(new MaterialDefinition
            {
                Type = MaterialType.Iron,
                Name = "Iron",
                Color = new Color(90, 90, 100),
                Density = 7.8f
            });

            Register(new MaterialDefinition
            {
                Type = MaterialType.Rock,
                Name = "Rock",
                Color = new Color(140, 110, 80),
                Density = 2.5f
            });
        }

        private static void Register(MaterialDefinition definition)
        {
            _definitions[definition.Type] = definition;
        }
    }
}

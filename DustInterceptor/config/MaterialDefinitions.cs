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
                Type = MaterialType.HeavyExotics,
                Name = "Heavy Exotics",
                Color = new Color(200, 230, 255), //Light Blue
                Density = 0.0004f
            });

            Register(new MaterialDefinition
            {
                Type = MaterialType.LightExotics,
                Name = "Light Exotics",
                Color = new Color(90, 90, 100),  //Light Green
                Density = 0.0001f
            });

            Register(new MaterialDefinition
            {
                Type = MaterialType.Metalls,
                Name = "Metalls",
                Color = new Color(140, 110, 80), //Dark Gray
                Density = 0.0002f
            });

            Register(new MaterialDefinition
            {
                Type = MaterialType.Debris,
                Name = "Debris",
                Color = new Color(255, 200, 80), //light Orange
                Density = 0.0004f
            });
        }

        private static void Register(MaterialDefinition definition)
        {
            _definitions[definition.Type] = definition;
        }
    }
}

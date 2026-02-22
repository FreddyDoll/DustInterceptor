using Microsoft.Xna.Framework;

namespace DustInterceptor
{
    /// <summary>
    /// Defines a material's immutable properties: visual, physical.
    /// This is the "blueprint" - configuration data for a material type.
    /// </summary>
    public sealed class MaterialDefinition
    {
        /// <summary>
        /// Unique identifier for this material.
        /// </summary>
        public MaterialType Type { get; init; }

        /// <summary>
        /// Display name shown in UI.
        /// </summary>
        public string Name { get; init; } = "";

        /// <summary>
        /// Color used for rendering (asteroid shader, UI).
        /// </summary>
        public Color Color { get; init; } = Color.White;

        /// <summary>
        /// Density of this material. Used together with amount to calculate asteroid radius.
        /// Higher density = smaller asteroid for the same amount.
        /// Units: mass per unit volume (arbitrary scale).
        /// </summary>
        public float Density { get; init; } = 1f;
    }
}

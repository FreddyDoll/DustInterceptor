using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace DustInterceptor
{
    public struct Asteroid
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Radius;

        /// <summary>
        /// Current rotation angle in radians.
        /// </summary>
        public float Rotation;

        /// <summary>
        /// Rotation speed in radians per second (positive = counter-clockwise).
        /// </summary>
        public float RotationRate;

        /// <summary>
        /// Material amounts stored by type. Replaces hardcoded Ice/Iron/Rock fields.
        /// </summary>
        public Dictionary<MaterialType, float> Materials;

        /// <summary>
        /// Whether this asteroid is disabled (depleted/visited).
        /// Disabled asteroids are not rendered, simulated, or collided with.
        /// </summary>
        public bool Disabled;

        /// <summary>
        /// Total materials remaining on this asteroid.
        /// </summary>
        public readonly float TotalMaterials
        {
            get
            {
                if (Materials == null) return 0f;
                float total = 0f;
                foreach (var kv in Materials)
                    total += kv.Value;
                return total;
            }
        }

        /// <summary>
        /// Whether this asteroid has been depleted of all materials.
        /// </summary>
        public readonly bool IsDepleted => TotalMaterials < 0.001f;

        /// <summary>
        /// Mass of the asteroid, proportional to total materials.
        /// </summary>
        public readonly float Mass => TotalMaterials;

        /// <summary>
        /// Gets the amount of a specific material on this asteroid.
        /// </summary>
        public readonly float GetMaterial(MaterialType type)
        {
            if (Materials != null && Materials.TryGetValue(type, out float amount))
                return amount;
            return 0f;
        }

        /// <summary>
        /// Sets the amount of a specific material on this asteroid.
        /// </summary>
        public void SetMaterial(MaterialType type, float amount)
        {
            Materials ??= new Dictionary<MaterialType, float>();
            Materials[type] = amount;
        }

        /// <summary>
        /// Calculates the radius based on material amounts and densities.
        /// Uses the formula: volume = sum(amount / density), radius = cbrt(3V / 4π)
        /// </summary>
        public static float CalculateRadius(Dictionary<MaterialType, float> materials)
        {
            if (materials == null || materials.Count == 0)
                return 1f;

            float totalVolume = 0f;
            foreach (var kv in materials)
            {
                var def = MaterialDefinitions.TryGet(kv.Key);
                float density = def?.Density ?? 1f;
                totalVolume += kv.Value / density;
            }

            // radius = cbrt(3V / 4π) — simplified with a scale factor for game feel
            // Using a scale factor to keep asteroids visually similar to before
            return MathF.Max(1f, MathF.Cbrt(totalVolume * 0.75f / MathF.PI));
        }

        /// <summary>
        /// Recalculates and updates the radius based on current materials.
        /// </summary>
        public void UpdateRadius()
        {
            Radius = CalculateRadius(Materials);
        }
    }
}
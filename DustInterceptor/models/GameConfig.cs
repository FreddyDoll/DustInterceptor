using System;
using Microsoft.Xna.Framework;

namespace DustInterceptor
{
    /// <summary>
    /// Configuration for game systems: camera, rendering, upgrades, starting stats.
    /// </summary>
    public sealed class GameConfig
    {
        // === Camera ===
        public float CameraZoomSpeed = 0.9f;
        public float CameraZoomMin = 0.005f;
        public float CameraZoomMax = 0.25f;
        public float CameraZoomDefault = 0.25f;
        public float CameraPanSpeed = 800f;

        // === Trail Rendering ===
        public Color PastTrailColor = new(120, 120, 120, 130);
        public float PastTrailWidth = 75f;
        public Color PredictedTrailColor = new(80, 230, 255, 200);
        public float PredictedTrailWidth = 16f;

        // === Impulse Aim Rendering ===
        public Color ImpulseAimColor = new(255, 0, 0, 220);
        public float ImpulseAimWidth = 28f;
        public float ImpulseAimScale = 20.9f;

        // === World Colors ===
        public Color BackgroundColor = new(8, 10, 18);
        public Color PlanetColor = new(45, 70, 130);
        public Color ShipFlightColor = new(255, 210, 80);
        public Color ShipDockedColor = new(100, 255, 100);
        public Color DockedHighlightColor = new(100, 255, 100, 100);
        public float DockedHighlightPadding = 30f;

        // === Asteroid Colors ===
        public Color AsteroidDepletedColor = new(50, 50, 50);
        public Color AsteroidIceColor = new(200, 230, 255);
        public Color AsteroidIronColor = new(90, 90, 100);
        public Color AsteroidRockColor = new(140, 110, 80);

        // === Ship Starting Stats ===
        public float StartingMaxImpulse = 10f;
        public float ImpulseInaccuracy = 0.05f;
        public float ImpulseCooldown = 0.75f;
        public int StartingMaxTimeScaleIndex = 1;

        // === Mining ===
        /// <summary>
        /// Base materials transferred per second while docked (always runs at 1x speed).
        /// </summary>
        public float BaseMiningTransferRate = 10f;

        // === Upgrade: Impulse (exponential cost growth) ===
        public float BaseImpulseCost = 50f;
        public float ImpulseCostMultiplier = 2f;
        public float UpgradeImpulseAmount = 5f;

        // === Upgrade: Time Scale (exponential cost growth) ===
        public float BaseTimeScaleCost = 100f;
        public float TimeScaleCostMultiplier = 3f;

        // === Upgrade: Mining Speed (exponential cost growth) ===
        public float BaseMiningSpeedCost = 75f;
        public float MiningSpeedCostMultiplier = 2.5f;
        public float UpgradeMiningSpeedAmount = 10f;

        // === Time Scales ===
        public int[] TimeScales = [1, 2, 4, 8, 16, 32, 64];

        /// <summary>
        /// Calculates the cost for the next impulse upgrade.
        /// Cost grows exponentially: 50, 100, 200, 400, 800...
        /// </summary>
        public float GetImpulseUpgradeCost(int upgradeLevel)
        {
            return BaseImpulseCost * MathF.Pow(ImpulseCostMultiplier, upgradeLevel);
        }

        /// <summary>
        /// Calculates the cost for the next time scale upgrade.
        /// Cost grows exponentially: 100, 300, 900, 2700...
        /// </summary>
        public float GetTimeScaleUpgradeCost(int currentMaxScaleIndex)
        {
            int upgradesDone = currentMaxScaleIndex - 1;
            return BaseTimeScaleCost * MathF.Pow(TimeScaleCostMultiplier, upgradesDone);
        }

        /// <summary>
        /// Calculates the cost for the next mining speed upgrade.
        /// Cost grows exponentially: 75, 187, 468, 1172...
        /// </summary>
        public float GetMiningSpeedUpgradeCost(int upgradeLevel)
        {
            return BaseMiningSpeedCost * MathF.Pow(MiningSpeedCostMultiplier, upgradeLevel);
        }

        /// <summary>
        /// Calculates the current mining transfer rate based on upgrade level.
        /// </summary>
        public float GetMiningTransferRate(int upgradeLevel)
        {
            return BaseMiningTransferRate + (UpgradeMiningSpeedAmount * upgradeLevel);
        }
    }
}

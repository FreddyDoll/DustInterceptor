using System;
using Microsoft.Xna.Framework;

namespace DustInterceptor
{
    /// <summary>
    /// Configuration for game systems: camera, rendering, visual settings.
    /// Note: Upgrade configurations are now in UpgradeDefinitions.cs
    /// </summary>
    public sealed class GameConfig
    {
        // === Camera ===
        public float CameraZoomSpeed = 0.9f;
        public float CameraZoomMin = 0.005f;
        public float CameraZoomMax = 2.0f;
        public float CameraZoomDefault = 0.25f;
        public float CameraPanSpeed = 800f;

        // === Target Selection ===
        /// <summary>
        /// Radius in world units for cursor to snap to nearby asteroids.
        /// </summary>
        public float CursorSnapRadius = 5000f;

        /// <summary>
        /// Speed at which cursor moves in world units per second (at zoom 1.0).
        /// </summary>
        public float CursorMoveSpeed = 1200f;

        /// <summary>
        /// Color of the cursor reticle in target selection mode (gray).
        /// </summary>
        public Color CursorColor = new(150, 150, 150, 200);

        /// <summary>
        /// Thickness of the cursor ring.
        /// </summary>
        public float CursorRingThickness = 8f;

        /// <summary>
        /// Size of the cursor ring in world units.
        /// </summary>
        public float CursorRingRadius = 200f;

        /// <summary>
        /// Color of the highlight for the auto-selected closest asteroid (yellow).
        /// </summary>
        public Color HoveredAsteroidColor = new(255, 255, 100, 200);

        /// <summary>
        /// Color of the highlight ring around the currently targeted asteroid.
        /// </summary>
        public Color TargetHighlightColor = new(255, 180, 50, 180);

        /// <summary>
        /// Padding added to asteroid radius for target highlight.
        /// </summary>
        public float TargetHighlightPadding = 40f;

        /// <summary>
        /// Color for the target's predicted orbital path.
        /// </summary>
        public Color TargetPredictedPathColor = new(255, 180, 50, 80);

        /// <summary>
        /// Width of the target predicted path line.
        /// </summary>
        public float TargetPredictedPathWidth = 12f;

        // === Background Grid ===
        public Color GridColor = new(40, 45, 55, 5);
        public float GridCircleSpacing = 100_000f;        // Distance between concentric circles
        public float GridMaxRadius = 2_500_000f;         // Maximum radius for grid circles
        public int GridRadialLineCount = 6;             // Number of radial lines (every 30 degrees = 12 lines)
        public float GridLineWidth = 1000f;                // Thickness of grid lines in world units

        // === Trail Rendering ===
        public Color PastTrailColor = new(20, 20, 20, 20); //alpha does not work
        public float PastTrailWidth = 75f;
        public Color PredictedTrailColor = new(20, 50, 60, 10); //alpha does not work
        public float PredictedTrailWidth = 16f;

        // === Impulse Aim Rendering ===
        public Color ImpulseAimReadyColor = new(255, 80, 80, 180);      // Bright red when ready
        public Color ImpulseAimChargingColor = new(100, 100, 100, 150); // Dim gray while charging
        public float ImpulseAimWidth = 28f;
        public float ImpulseAimScale = 10f;

        // === World Colors ===
        public Color BackgroundColor = new(8, 10, 18);
        public Color ShipFlightColor = new(255, 255, 255);
        public Color ShipDockedColor = new(100, 255, 100);
        public Color DockedHighlightColor = new(100, 255, 100, 100);
        public float DockedHighlightPadding = 30f;

        // === Planet Colors (Gas Giant) ===
        public Color PlanetColor = new(45, 70, 130);          // Base color
        public Color PlanetBandColor1 = new(60, 50, 100);     // Darker band color
        public Color PlanetBandColor2 = new(80, 100, 150);    // Lighter swirl color

        // === Asteroid Colors ===
        /// <summary>
        /// Color for depleted asteroids with no materials remaining.
        /// Material-specific colors are now defined in MaterialDefinitions.
        /// </summary>
        public Color AsteroidDepletedColor = new(50, 50, 50);

        // === Performance / LOD ===
        /// <summary>
        /// Minimum asteroid size in screen pixels to be rendered.
        /// Asteroids smaller than this on screen will be culled.
        /// </summary>
        public float MinAsteroidScreenSize = 0.1f;
    }
}

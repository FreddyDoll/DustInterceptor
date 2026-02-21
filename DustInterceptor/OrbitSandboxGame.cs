using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra;
using Myra.Graphics2D.UI;

namespace DustInterceptor
{
    public sealed partial class OrbitSandboxGame : Game
    {
        private readonly GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch = null!;

        // Textures
        private Texture2D _pixel = null!;
        private Texture2D _circle = null!;

        // Shaders
        private Effect _backgroundGridEffect = null!;
        private float _shaderTime;

        // Camera
        private readonly Camera2D _camera = new();

        // Configuration
        private readonly GameConfig _config = new();

        // Simulation (extracted)
        private WorldSim _world = null!;

        // Upgrades
        private UpgradeManager _upgrades = null!;

        // UI
        private int _resolutionScale = 2;
        private MiningUi _miningUi = null!;
        private Hud _hud = null!;

        // Time scale state
        private int _timeScaleIndex = 0;

        // Input state
        private GamePadState _gpPrev;
        private bool _cameraLocked = true;

        // Impulse state
        private Vector2 _impulseAim;

        public OrbitSandboxGame()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            _graphics.PreferredBackBufferWidth = 1920 * _resolutionScale;
            _graphics.PreferredBackBufferHeight = 1080 * _resolutionScale;
            _graphics.SynchronizeWithVerticalRetrace = true;
            IsFixedTimeStep = true;
            TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);
        }

        protected override void Initialize()
        {
            // Create world simulation with default config
            _world = new WorldSim(new WorldSimConfig());

            // Initialize upgrade system
            _upgrades = new UpgradeManager();
            UpgradeDefinitions.RegisterAll(_upgrades);

            // Initialize upgradeable values from upgrade system
            _world.SetMiningTransferRate(_upgrades.GetValue(UpgradeType.MiningSpeed));
            _world.SetPredictionHorizon(_upgrades.GetValue(UpgradeType.PredictionLength));

            // Camera defaults
            _camera.Zoom = _config.CameraZoomDefault;
            _camera.Position = _world.Ship.Position;
            _cameraLocked = true;

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            _circle = CreateCircleTexture(GraphicsDevice, radiusPx: 128);

            // Load shaders
            _backgroundGridEffect = Content.Load<Effect>("BackgroundGrid");

            // Initialize UI
            _miningUi = new MiningUi(this, _resolutionScale);
            _hud = new Hud(this, _resolutionScale);
        }

        protected override void Update(GameTime gameTime)
        {
            var gp = GamePad.GetState(PlayerIndex.One);

            if (Keyboard.GetState().IsKeyDown(Keys.Escape) ||
                (gp.IsConnected && gp.Buttons.Back == ButtonState.Pressed))
            {
                Exit();
            }

            float realDt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update shader time
            _shaderTime += realDt;

            // Get current max time scale index from upgrades
            int maxTimeScaleIndex = _upgrades.GetLevel(UpgradeType.MaxTimeScale);

            // ----- Input: time scale -----
            if (Pressed(gp.Buttons.RightShoulder, _gpPrev.Buttons.RightShoulder))
                _timeScaleIndex = Math.Min(_timeScaleIndex + 1, maxTimeScaleIndex);
            if (Pressed(gp.Buttons.LeftShoulder, _gpPrev.Buttons.LeftShoulder))
                _timeScaleIndex = Math.Max(_timeScaleIndex - 1, 0);

            // ----- Update HUD -----
            int currentTimeScale = (int)_upgrades.Get(UpgradeType.MaxTimeScale).Definition.GetValue(_timeScaleIndex);
            _hud.Update(realDt, currentTimeScale);

            // ----- Input: camera lock (flight mode only - Y is used for upgrade in mining) -----
            if (_world.Mode == GameMode.Flight && Pressed(gp.Buttons.Y, _gpPrev.Buttons.Y))
                _cameraLocked = true;

            // ----- Input: zoom (LT/RT) with upgradeable min zoom -----
            float minZoom = _upgrades.GetValue(UpgradeType.MinZoomLevel, _config.CameraZoomMin);
            float zoomDelta = (gp.Triggers.Right - gp.Triggers.Left) * _config.CameraZoomSpeed * realDt;
            _camera.Zoom = Clamp(_camera.Zoom * (1f + zoomDelta), minZoom, _config.CameraZoomMax);

            // ----- Input: free pan (right stick) -----
            var rs = gp.ThumbSticks.Right;
            Vector2 pan = new Vector2(rs.X, -rs.Y);
            if (pan.LengthSquared() > 0.01f)
            {
                _cameraLocked = false;
                _camera.Position += pan * (_config.CameraPanSpeed * realDt) / MathF.Max(_camera.Zoom, 0.0001f);
            }

            // If locked, follow ship
            if (_cameraLocked)
                _camera.Position = _world.Ship.Position;

            // Mode-specific update
            if (_world.Mode == GameMode.Flight)
            {
                UpdateFlightMode(gp, realDt);
            }
            else if (_world.Mode == GameMode.Mining)
            {
                UpdateMiningMode(gp, realDt);
            }

            _gpPrev = gp;
            base.Update(gameTime);
        }

        private void UpdateFlightMode(GamePadState gp, float realDt)
        {
            // Get current upgrade values
            float maxImpulse = _upgrades.GetValue(UpgradeType.ImpulseStrength);
            float inaccuracy = _upgrades.GetValue(UpgradeType.ImpulseAccuracy);
            float cooldown = _upgrades.GetValue(UpgradeType.ImpulseCooldown);

            // ----- Input: impulse aim (left stick) -----
            var ls = gp.ThumbSticks.Left;
            Vector2 aim = new Vector2(ls.X, -ls.Y);
            float aimMag = MathF.Min(1f, aim.Length());
            aim = (aimMag > 0.001f) ? Vector2.Normalize(aim) : Vector2.Zero;

            float strength01 = aimMag * aimMag;
            _impulseAim = aim * (strength01 * maxImpulse);

            // ----- Determine if firing impulse -----
            bool fireImpulse = Pressed(gp.Buttons.X, _gpPrev.Buttons.X);

            // ----- Update simulation -----
            int currentTimeScale = (int)_upgrades.Get(UpgradeType.MaxTimeScale).Definition.GetValue(_timeScaleIndex);
            _world.UpdateFlight(realDt, currentTimeScale, _impulseAim, fireImpulse, maxImpulse, 
                inaccuracy, cooldown);

            // Hide mining UI
            _miningUi.Hide();
        }

        private void UpdateMiningMode(GamePadState gp, float realDt)
        {
            // Show mining UI and update data
            if (!_miningUi.IsVisible)
                _miningUi.Show();

            // Helper to get resources for upgrade system
            float GetResource(ResourceType r) => _world.GetResource(r);

            if (_world.DockedAsteroidIndex >= 0 && _world.DockedAsteroidIndex < _world.Asteroids.Length)
            {
                ref var asteroid = ref _world.Asteroids[_world.DockedAsteroidIndex];
                
                // Build upgrade display data list
                var upgradeDataList = new List<UpgradeDisplayData>();
                foreach (var state in _upgrades.GetAvailableUpgrades())
                {
                    upgradeDataList.Add(_upgrades.GetDisplayData(state.Definition.Type, GetResource));
                }

                _miningUi.UpdateData(new MiningUiData
                {
                    AsteroidIce = asteroid.Ice,
                    AsteroidIron = asteroid.Iron,
                    AsteroidRock = asteroid.Rock,
                    ShipIce = _world.ShipIce,
                    ShipIron = _world.ShipIron,
                    ShipRock = _world.ShipRock,
                    CurrentMiningSpeed = _upgrades.GetValue(UpgradeType.MiningSpeed),
                    Upgrades = upgradeDataList
                });
            }

            // Handle UI input
            var (action, upgradeType) = _miningUi.HandleInput(gp, _gpPrev);
            switch (action)
            {
                case MiningAction.PurchaseUpgrade:
                    if (_upgrades.TryPurchase(upgradeType, GetResource, _world.TrySpendResource))
                    {
                        // Update values that depend on upgrades
                        if (upgradeType == UpgradeType.MiningSpeed)
                        {
                            _world.SetMiningTransferRate(_upgrades.GetValue(UpgradeType.MiningSpeed));
                        }
                        else if (upgradeType == UpgradeType.PredictionLength)
                        {
                            _world.SetPredictionHorizon(_upgrades.GetValue(UpgradeType.PredictionLength));
                        }
                    }
                    break;
                case MiningAction.Undock:
                    _world.Undock();
                    _miningUi.Hide();
                    break;
            }

            // ----- Update simulation (mining mode with auto-transfer) -----
            int currentTimeScale = (int)_upgrades.Get(UpgradeType.MaxTimeScale).Definition.GetValue(_timeScaleIndex);
            _world.UpdateMining(realDt, currentTimeScale);

            // Clear impulse aim while docked
            _impulseAim = Vector2.Zero;
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(_config.BackgroundColor);

            // Draw background grid with shader (fullscreen pass)
            DrawBackgroundGridShader();

            _spriteBatch.Begin(
                sortMode: SpriteSortMode.Deferred,
                blendState: BlendState.AlphaBlend,
                samplerState: SamplerState.LinearClamp,
                depthStencilState: DepthStencilState.None,
                rasterizerState: RasterizerState.CullNone,
                effect: null,
                transformMatrix: _camera.GetViewMatrix(GraphicsDevice));

            // Planet
            DrawCircleWorld(_world.Planet.Position, _world.Planet.Radius, _config.PlanetColor);

            // Asteroids
            DrawAsteroids();

            // Past trail
            DrawPath(_world.ShipTrail, _config.PastTrailColor, _config.PastTrailWidth);

            // Predicted path
            DrawPath(_world.PredictedPath, _config.PredictedTrailColor, _config.PredictedTrailWidth);

            // Ship
            Color shipColor = _world.Mode == GameMode.Mining
                ? _config.ShipDockedColor
                : _config.ShipFlightColor;
            DrawCircleWorld(_world.Ship.Position, _world.Ship.Radius, shipColor);

            // Highlight docked asteroid
            if (_world.Mode == GameMode.Mining && _world.DockedAsteroidIndex >= 0)
            {
                ref var asteroid = ref _world.Asteroids[_world.DockedAsteroidIndex];
                DrawCircleWorld(asteroid.Position, asteroid.Radius + _config.DockedHighlightPadding, _config.DockedHighlightColor);
            }

            // Draw impulse vector from ship with cooldown charge-up effect
            if (_world.Mode == GameMode.Flight && _impulseAim.LengthSquared() > 1f)
            {
                float cooldown = _upgrades.GetValue(UpgradeType.ImpulseCooldown);
                float cooldownLeft = _world.ImpulseCooldownLeft;
                
                // Calculate charge progress (0 = just fired, 1 = fully charged)
                float chargeProgress = cooldown > 0.001f 
                    ? 1f - (cooldownLeft / cooldown) 
                    : 1f;
                chargeProgress = Clamp(chargeProgress, 0f, 1f);

                Vector2 fullEnd = _world.Ship.Position + _impulseAim * _config.ImpulseAimScale;
                
                if (chargeProgress >= 1f)
                {
                    // Fully charged - bright color, full length
                    DrawLineWorld(_world.Ship.Position, fullEnd, _config.ImpulseAimReadyColor, _config.ImpulseAimWidth);
                }
                else
                {
                    // Charging - draw dim background line at full length
                    DrawLineWorld(_world.Ship.Position, fullEnd, _config.ImpulseAimChargingColor, _config.ImpulseAimWidth);
                    
                    // Draw charging portion in ready color, length based on progress
                    if (chargeProgress > 0.01f)
                    {
                        Vector2 chargeEnd = _world.Ship.Position + _impulseAim * _config.ImpulseAimScale * chargeProgress;
                        DrawLineWorld(_world.Ship.Position, chargeEnd, _config.ImpulseAimReadyColor, _config.ImpulseAimWidth);
                    }
                }
            }

            _spriteBatch.End();

            // Draw Myra UI on top
            _hud.Render();
            _miningUi.Render();

            base.Draw(gameTime);
        }

        #region Rendering helpers
        
        /// <summary>
        /// Draws the background grid using a pixel shader for smooth rendering at all zoom levels.
        /// </summary>
        private void DrawBackgroundGridShader()
        {
            var vp = GraphicsDevice.Viewport;
            
            // Set shader parameters
            _backgroundGridEffect.Parameters["Time"]?.SetValue(_shaderTime);
            _backgroundGridEffect.Parameters["Resolution"]?.SetValue(new Vector2(vp.Width, vp.Height));
            _backgroundGridEffect.Parameters["CameraPosition"]?.SetValue(_camera.Position);
            _backgroundGridEffect.Parameters["CameraZoom"]?.SetValue(_camera.Zoom);
            _backgroundGridEffect.Parameters["GridSpacing"]?.SetValue(_config.GridCircleSpacing);
            _backgroundGridEffect.Parameters["RadialLineCount"]?.SetValue((float)_config.GridRadialLineCount);
            _backgroundGridEffect.Parameters["GridLineWidth"]?.SetValue(_config.GridLineWidth);

            // Draw fullscreen quad with shader
            _spriteBatch.Begin(
                sortMode: SpriteSortMode.Immediate,
                blendState: BlendState.AlphaBlend,
                samplerState: SamplerState.LinearClamp,
                depthStencilState: DepthStencilState.None,
                rasterizerState: RasterizerState.CullNone,
                effect: _backgroundGridEffect);

            // Draw a fullscreen rectangle
            _spriteBatch.Draw(
                _pixel,
                new Rectangle(0, 0, vp.Width, vp.Height),
                Color.White);

            _spriteBatch.End();
        }

        private void DrawAsteroids()
        {
            for (int i = 0; i < _world.Asteroids.Length; i++)
            {
                ref var a = ref _world.Asteroids[i];
                
                // Skip disabled asteroids
                if (a.Disabled)
                    continue;

                Color color = GetAsteroidColor(ref a);
                DrawCircleWorld(a.Position, a.Radius, color);
            }
        }

        private Color GetAsteroidColor(ref Asteroid a)
        {
            float total = a.Ice + a.Iron + a.Rock;
            if (total < 0.001f)
            {
                return _config.AsteroidDepletedColor;
            }

            float iceRatio = a.Ice / total;
            float ironRatio = a.Iron / total;
            float rockRatio = a.Rock / total;

            float r = iceRatio * _config.AsteroidIceColor.R + ironRatio * _config.AsteroidIronColor.R + rockRatio * _config.AsteroidRockColor.R;
            float g = iceRatio * _config.AsteroidIceColor.G + ironRatio * _config.AsteroidIronColor.G + rockRatio * _config.AsteroidRockColor.G;
            float b = iceRatio * _config.AsteroidIceColor.B + ironRatio * _config.AsteroidIronColor.B + rockRatio * _config.AsteroidRockColor.B;

            return new Color((int)r, (int)g, (int)b);
        }

        private void DrawCircleWorld(Vector2 center, float radiusWorld, Color color)
        {
            var origin = new Vector2(_circle.Width / 2f, _circle.Height / 2f);
            float scale = (radiusWorld * 2f) / _circle.Width;

            _spriteBatch.Draw(
                _circle,
                position: center,
                sourceRectangle: null,
                color: color,
                rotation: 0f,
                origin: origin,
                scale: scale,
                effects: SpriteEffects.None,
                layerDepth: 0f);
        }

        private void DrawPath(IEnumerable<Vector2> points, Color color, float thicknessWorld)
        {
            Vector2? prev = null;
            foreach (var p in points)
            {
                if (prev.HasValue)
                    DrawLineWorld(prev.Value, p, color, thicknessWorld);
                prev = p;
            }
        }

        private void DrawLineWorld(Vector2 a, Vector2 b, Color color, float thicknessWorld)
        {
            Vector2 delta = b - a;
            float len = delta.Length();
            if (len < 0.001f) return;

            float angle = MathF.Atan2(delta.Y, delta.X);

            _spriteBatch.Draw(
                _pixel,
                position: a,
                sourceRectangle: null,
                color: color,
                rotation: angle,
                origin: new Vector2(0, 0.5f),
                scale: new Vector2(len, thicknessWorld),
                effects: SpriteEffects.None,
                layerDepth: 0f);
        }
        #endregion

        #region Texture generation
        private static Texture2D CreateCircleTexture(GraphicsDevice gd, int radiusPx)
        {
            int diameter = radiusPx * 2 + 1;
            var tex = new Texture2D(gd, diameter, diameter);
            var data = new Color[diameter * diameter];

            float r = radiusPx;

            for (int y = 0; y < diameter; y++)
            {
                for (int x = 0; x < diameter; x++)
                {
                    float dx = x - r;
                    float dy = y - r;
                    float d2 = dx * dx + dy * dy;

                    float dist = MathF.Sqrt(d2);
                    float alpha = 1f - SmoothStep(r - 1.5f, r + 0.5f, dist);
                    alpha = Clamp(alpha, 0f, 1f);

                    data[y * diameter + x] = new Color(alpha, alpha, alpha, alpha);
                }
            }

            tex.SetData(data);
            return tex;
        }

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            if (edge0 == edge1) return x < edge0 ? 0f : 1f;
            float t = Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
            return t * t * (3f - 2f * t);
        }
        #endregion

        #region Utils
        private static bool Pressed(ButtonState now, ButtonState prev) =>
            now == ButtonState.Pressed && prev == ButtonState.Released;

        private static float Clamp(float v, float min, float max) =>
            (v < min) ? min : (v > max) ? max : v;
        #endregion
    }
}
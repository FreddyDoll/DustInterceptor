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

        // Camera
        private readonly Camera2D _camera = new();

        // Configuration
        private readonly GameConfig _config = new();

        // Simulation (extracted)
        private WorldSim _world = null!;

        // UI
        private int _resolutionScale = 2;
        private MiningUi _miningUi = null!;
        private Hud _hud = null!;

        // Time scale state
        private int _currentMaxScaleIndex;
        private int _timeScaleIndex = 0;

        // Impulse upgrade state
        private int _impulseUpgradeLevel = 0;

        // Mining speed upgrade state
        private int _miningSpeedUpgradeLevel = 0;

        // Input state
        private GamePadState _gpPrev;
        private bool _cameraLocked = true;

        // Impulse state (upgradeable)
        private Vector2 _impulseAim;
        private float _maxImpulse;

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

            // Initialize mining transfer rate
            _world.SetMiningTransferRate(_config.GetMiningTransferRate(_miningSpeedUpgradeLevel));

            // Initialize upgradeable stats from config
            _maxImpulse = _config.StartingMaxImpulse;
            _currentMaxScaleIndex = _config.StartingMaxTimeScaleIndex;

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

            // ----- Input: time scale -----
            if (Pressed(gp.Buttons.RightShoulder, _gpPrev.Buttons.RightShoulder))
                _timeScaleIndex = Math.Min(_timeScaleIndex + 1, _currentMaxScaleIndex);
            if (Pressed(gp.Buttons.LeftShoulder, _gpPrev.Buttons.LeftShoulder))
                _timeScaleIndex = Math.Max(_timeScaleIndex - 1, 0);

            // ----- Update HUD -----
            _hud.Update(realDt, _config.TimeScales[_timeScaleIndex]);

            // ----- Input: camera lock (flight mode only - Y is used for upgrade in mining) -----
            if (_world.Mode == GameMode.Flight && Pressed(gp.Buttons.Y, _gpPrev.Buttons.Y))
                _cameraLocked = true;

            // ----- Input: zoom (LT/RT) -----
            float zoomDelta = (gp.Triggers.Right - gp.Triggers.Left) * _config.CameraZoomSpeed * realDt;
            _camera.Zoom = Clamp(_camera.Zoom * (1f + zoomDelta), _config.CameraZoomMin, _config.CameraZoomMax);

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
            // ----- Input: impulse aim (left stick) -----
            var ls = gp.ThumbSticks.Left;
            Vector2 aim = new Vector2(ls.X, -ls.Y);
            float aimMag = MathF.Min(1f, aim.Length());
            aim = (aimMag > 0.001f) ? Vector2.Normalize(aim) : Vector2.Zero;

            float strength01 = aimMag * aimMag;
            _impulseAim = aim * (strength01 * _maxImpulse);

            // ----- Determine if firing impulse -----
            bool fireImpulse = Pressed(gp.Buttons.X, _gpPrev.Buttons.X);

            // ----- Update simulation -----
            int timeScale = _config.TimeScales[_timeScaleIndex];
            _world.UpdateFlight(realDt, timeScale, _impulseAim, fireImpulse, _maxImpulse, 
                _config.ImpulseInaccuracy, _config.ImpulseCooldown);

            // Hide mining UI
            _miningUi.Hide();
        }

        private void UpdateMiningMode(GamePadState gp, float realDt)
        {
            // Show mining UI and update data
            if (!_miningUi.IsVisible)
                _miningUi.Show();

            bool isTimeScaleMaxedOut = _currentMaxScaleIndex >= _config.TimeScales.Length - 1;
            float timeScaleCost = _config.GetTimeScaleUpgradeCost(_currentMaxScaleIndex);
            float impulseCost = _config.GetImpulseUpgradeCost(_impulseUpgradeLevel);
            float miningSpeedCost = _config.GetMiningSpeedUpgradeCost(_miningSpeedUpgradeLevel);
            float currentMiningSpeed = _config.GetMiningTransferRate(_miningSpeedUpgradeLevel);

            if (_world.DockedAsteroidIndex >= 0 && _world.DockedAsteroidIndex < _world.Asteroids.Length)
            {
                ref var asteroid = ref _world.Asteroids[_world.DockedAsteroidIndex];
                _miningUi.UpdateData(new MiningUiData
                {
                    AsteroidIce = asteroid.Ice,
                    AsteroidIron = asteroid.Iron,
                    AsteroidRock = asteroid.Rock,
                    ShipIce = _world.ShipIce,
                    ShipIron = _world.ShipIron,
                    ShipRock = _world.ShipRock,
                    CurrentMaxImpulse = _maxImpulse,
                    UpgradeImpulseCost = impulseCost,
                    CanAffordImpulseUpgrade = _world.ShipIron >= impulseCost,
                    CurrentMaxTimeScale = _config.TimeScales[_currentMaxScaleIndex],
                    UpgradeTimeScaleCost = timeScaleCost,
                    CanAffordTimeScaleUpgrade = _world.ShipIron >= timeScaleCost,
                    TimeScaleMaxedOut = isTimeScaleMaxedOut,
                    CurrentMiningSpeed = currentMiningSpeed,
                    UpgradeMiningSpeedCost = miningSpeedCost,
                    CanAffordMiningSpeedUpgrade = _world.ShipIron >= miningSpeedCost
                });
            }

            // Handle UI input
            var action = _miningUi.HandleInput(gp, _gpPrev);
            switch (action)
            {
                case MiningAction.UpgradeImpulse:
                    if (_world.TrySpendIron(impulseCost))
                    {
                        _maxImpulse += _config.UpgradeImpulseAmount;
                        _impulseUpgradeLevel++;
                    }
                    break;
                case MiningAction.UpgradeTimeScale:
                    if (!isTimeScaleMaxedOut && _world.TrySpendIron(timeScaleCost))
                    {
                        _currentMaxScaleIndex++;
                    }
                    break;
                case MiningAction.UpgradeMiningSpeed:
                    if (_world.TrySpendIron(miningSpeedCost))
                    {
                        _miningSpeedUpgradeLevel++;
                        _world.SetMiningTransferRate(_config.GetMiningTransferRate(_miningSpeedUpgradeLevel));
                    }
                    break;
                case MiningAction.Undock:
                    _world.Undock();
                    _miningUi.Hide();
                    break;
            }

            // ----- Update simulation (mining mode with auto-transfer) -----
            int timeScale = _config.TimeScales[_timeScaleIndex];
            bool miningComplete = _world.UpdateMining(realDt, timeScale);

            // Auto-undock when asteroid is depleted
            if (miningComplete)
            {
                _world.Undock();
                _miningUi.Hide();
            }

            // Clear impulse aim while docked
            _impulseAim = Vector2.Zero;
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(_config.BackgroundColor);

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

            // Draw impulse vector from ship
            if (_world.Mode == GameMode.Flight && _impulseAim.LengthSquared() > 1f)
            {
                Vector2 end = _world.Ship.Position + _impulseAim * _config.ImpulseAimScale;
                DrawLineWorld(_world.Ship.Position, end, _config.ImpulseAimColor, _config.ImpulseAimWidth);
            }

            _spriteBatch.End();

            // Draw Myra UI on top
            _hud.Render();
            _miningUi.Render();

            base.Draw(gameTime);
        }

        #region Rendering helpers
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
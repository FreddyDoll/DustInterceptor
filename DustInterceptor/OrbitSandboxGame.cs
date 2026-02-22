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
        private Texture2D _shipTexture = null!;

        // Shaders
        private Effect _backgroundGridEffect = null!;
        private Effect _planetEffect = null!;
        private Effect _asteroidEffect = null!;
        private float _shaderTime;

        // Camera
        private readonly Camera2D _camera = new();

        // Configuration
        private readonly GameConfig _config = new();

        // Simulation (extracted)
        private WorldSim _world = null!;

        // Upgrades
        private UpgradeManager _upgrades = null!;

        // Events
        private EventManager _events = null!;
        private float _simulationTime;

        // UI
        private int _resolutionScale = 2;
        private MiningUi _miningUi = null!;
        private Hud _hud = null!;
        private EventDialogUi _eventDialog = null!;

        // Time scale state
        private int _timeScaleIndex = 0;

        // Input state
        private GamePadState _gpPrev;
        private CameraMode _cameraMode = CameraMode.LockedToShip;

        // Target selection state
        // Cursor offset is relative to ship - this way cursor moves with ship when no input
        private Vector2 _cursorOffset;
        private int _hoveredAsteroidIndex = -1;

        // Impulse state
        private Vector2 _impulseAim;

        // Drop materials (U1)
        private const float DropAmountPerPress = 10f;

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
            // Register material definitions (must be before WorldSim creation)
            MaterialDefinitions.RegisterAll();

            // Create world simulation with default config
            _world = new WorldSim(new WorldSimConfig());

            // Initialize upgrade system
            _upgrades = new UpgradeManager();
            UpgradeDefinitions.RegisterAll(_upgrades);

            // Initialize upgradeable values from upgrade system
            _world.SetMiningTransferRate(_upgrades.GetValue(UpgradeType.MiningSpeed));
            _world.SetPredictionHorizon(_upgrades.GetValue(UpgradeType.PredictionLength));

            // Initialize event system
            _events = new EventManager();
            EventDefinitions.RegisterAll(_events);

            // Camera defaults
            _camera.Zoom = _config.CameraZoomDefault;
            _camera.Position = _world.Ship.Position;
            _cameraMode = CameraMode.LockedToShip;

            // Initialize cursor offset at zero (at ship position)
            _cursorOffset = Vector2.Zero;

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            _circle = CreateCircleTexture(GraphicsDevice, radiusPx: 128);

            // Load textures
            _shipTexture = Content.Load<Texture2D>("Ship");

            // Load shaders
            _backgroundGridEffect = Content.Load<Effect>("BackgroundGrid");
            _planetEffect = Content.Load<Effect>("Planet");
            _asteroidEffect = Content.Load<Effect>("Asteroid");

            // Initialize UI
            _miningUi = new MiningUi(this, _resolutionScale);
            _hud = new Hud(this, _resolutionScale);
            _eventDialog = new EventDialogUi(this, _resolutionScale);
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

            // Update shader time (always ticks, even during events)
            _shaderTime += realDt;

            // ----- Event dialog: blocks all other input while visible -----
            if (_eventDialog.IsVisible)
            {
                if (_eventDialog.HandleInput(gp, _gpPrev))
                {
                    // Player dismissed the dialog
                    _events.Dismiss();
                }

                _gpPrev = gp;
                base.Update(gameTime);
                return; // Skip all sim/flight/mining input while event dialog is up
            }

            // Get current max time scale index from upgrades
            int maxTimeScaleIndex = _upgrades.GetLevel(UpgradeType.MaxTimeScale);

            // ----- Input: time scale -----
            if (Pressed(gp.Buttons.RightShoulder, _gpPrev.Buttons.RightShoulder))
                _timeScaleIndex = Math.Min(_timeScaleIndex + 1, maxTimeScaleIndex);
            if (Pressed(gp.Buttons.LeftShoulder, _gpPrev.Buttons.LeftShoulder))
                _timeScaleIndex = Math.Max(_timeScaleIndex - 1, 0);

            // Compute current time scale for this frame
            int currentTimeScale = (int)_upgrades.Get(UpgradeType.MaxTimeScale).Definition.GetValue(_timeScaleIndex);

            // Accumulate simulation time (scales with time warp)
            _simulationTime += realDt * currentTimeScale;

            // ----- Check for events -----
            var firedEvent = _events.Update(_simulationTime, _world.ShipCargo);
            if (firedEvent != null)
            {
                _eventDialog.Show(firedEvent);
                _gpPrev = gp;
                base.Update(gameTime);
                return; // Pause immediately — dialog will be handled next frame
            }

            // ----- Update HUD -----
            bool hasTracker = _upgrades.IsUnlocked(UpgradeType.AsteroidTracker);
            _hud.Update(
                realDt,
                currentTimeScale,
                fuel: _world.GetResource(MaterialType.Fuel),
                dropMaterial: _world.Mode == GameMode.Flight ? _world.SelectedDropMaterial : null,
                dropAmount: _world.Mode == GameMode.Flight ? _world.GetResource(_world.SelectedDropMaterial) : 0f,
                hasClosestApproach: hasTracker && _world.HasClosestApproach,
                closestApproachDistance: _world.ClosestApproachDistance);

            // Gate: asteroid tracking requires the Asteroid Tracker unlock
            bool hasAsteroidTracker = _upgrades.IsUnlocked(UpgradeType.AsteroidTracker);
            if (!hasAsteroidTracker)
            {
                // Ensure we never show/compute target-selection state without the unlock.
                if (_cameraMode == CameraMode.TargetSelection)
                    _cameraMode = CameraMode.LockedToShip;

                _cursorOffset = Vector2.Zero;
                _hoveredAsteroidIndex = -1;
                _world.ClearSelectedTarget();
            }

            // ----- Input: camera mode toggle (flight mode only - Y is used for upgrade in mining) -----
            if (_world.Mode == GameMode.Flight && Pressed(gp.Buttons.Y, _gpPrev.Buttons.Y))
            {
                if (!hasAsteroidTracker)
                {
                    // Without the tracker, skip Target Selection mode entirely.
                    _cameraMode = _cameraMode == CameraMode.FreePan ? CameraMode.LockedToShip : CameraMode.FreePan;
                }
                else
                {
                    // Cycle through camera modes: LockedToShip -> TargetSelection -> FreePan -> LockedToShip
                    _cameraMode = _cameraMode switch
                    {
                        CameraMode.LockedToShip => CameraMode.TargetSelection,
                        CameraMode.TargetSelection => CameraMode.FreePan,
                        CameraMode.FreePan => CameraMode.LockedToShip,
                        _ => CameraMode.LockedToShip
                    };

                    // When entering target selection mode, reset cursor offset to zero (at ship)
                    if (_cameraMode == CameraMode.TargetSelection)
                    {
                        _cursorOffset = Vector2.Zero;
                        _hoveredAsteroidIndex = -1;
                    }
                }
            }

            // ----- Input: zoom (LT/RT) with upgradeable min zoom -----
            float minZoom = _upgrades.GetValue(UpgradeType.MinZoomLevel, _config.CameraZoomMin);
            float zoomDelta = (gp.Triggers.Right - gp.Triggers.Left) * _config.CameraZoomSpeed * realDt;
            _camera.Zoom = Clamp(_camera.Zoom * (1f + zoomDelta), minZoom, _config.CameraZoomMax);

            // ----- Input: right stick behavior depends on camera mode -----
            var rs = gp.ThumbSticks.Right;
            Vector2 stickInput = new Vector2(rs.X, -rs.Y);

            if (hasAsteroidTracker && _cameraMode == CameraMode.TargetSelection && _world.Mode == GameMode.Flight)
            {
                // In target selection mode, move cursor offset relative to ship
                if (stickInput.LengthSquared() > 0.01f)
                {
                    // Move cursor offset in world space, scaled by zoom
                    float cursorSpeed = _config.CursorMoveSpeed / MathF.Max(_camera.Zoom, 0.0001f);
                    _cursorOffset += stickInput * cursorSpeed * realDt;
                }

                // Calculate cursor world position from ship + offset
                Vector2 cursorWorldPos = _world.Ship.Position + _cursorOffset;

                // Find closest asteroid to cursor position (auto-highlight, yellow selection jumps to it)
                // Use fixed world-space snap radius for consistent performance regardless of zoom
                _hoveredAsteroidIndex = _world.FindClosestAsteroid(cursorWorldPos, _config.CursorSnapRadius);

                // Camera stays locked to ship in target selection mode (G2 change)
                _camera.Position = _world.Ship.Position;

                // Select target with A button - also returns to ship lock mode
                if (Pressed(gp.Buttons.A, _gpPrev.Buttons.A) && _hoveredAsteroidIndex >= 0)
                {
                    _world.SetSelectedTarget(_hoveredAsteroidIndex);
                    _cameraMode = CameraMode.LockedToShip; // Return to locked mode after selection
                }
            }
            else if (_cameraMode == CameraMode.FreePan)
            {
                // In free pan mode, right stick pans camera
                if (stickInput.LengthSquared() > 0.01f)
                {
                    _camera.Position += stickInput * (_config.CameraPanSpeed * realDt) / MathF.Max(_camera.Zoom, 0.0001f);
                }
            }
            else // LockedToShip or not in flight mode
            {
                // If locked, follow ship
                _camera.Position = _world.Ship.Position;
            }

            // Mode-specific update
            if (_world.Mode == GameMode.Flight)
            {
                UpdateFlightMode(gp, realDt);
            }
            else if (_world.Mode == GameMode.Mining)
            {
                UpdateMiningMode(gp, realDt);
                // Reset camera mode when entering mining
                _cameraMode = CameraMode.LockedToShip;
            }

            _gpPrev = gp;
            base.Update(gameTime);
        }

        private void UpdateFlightMode(GamePadState gp, float realDt)
        {
            // U1: cycle drop material with D-pad left/right, drop with B (flight only)
            if (Pressed(gp.DPad.Left, _gpPrev.DPad.Left))
                _world.CycleDropMaterial(-1);
            if (Pressed(gp.DPad.Right, _gpPrev.DPad.Right))
                _world.CycleDropMaterial(+1);

            if (Pressed(gp.Buttons.B, _gpPrev.Buttons.B))
                _world.DropSelectedMaterial(DropAmountPerPress);

            // Get current upgrade values
            float maxImpulse = _upgrades.GetValue(UpgradeType.ImpulseStrength);
            float cooldown = _upgrades.GetValue(UpgradeType.ImpulseCooldown);
            float specificImpulse = _upgrades.GetValue(UpgradeType.SpecificImpulse);

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
                cooldown, specificImpulse);

            // Hide mining UI
            _miningUi.Hide();
        }

        private void UpdateMiningMode(GamePadState gp, float realDt)
        {
            // Show mining UI and update data
            if (!_miningUi.IsVisible)
                _miningUi.Show();

            // Helper to get resources for upgrade system
            float GetResource(MaterialType r) => _world.GetResource(r);

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
                    AsteroidMaterials = asteroid.Materials,
                    ShipCargo = new Dictionary<MaterialType, float>(_world.ShipCargo),
                    CurrentMiningSpeed = _upgrades.GetValue(UpgradeType.MiningSpeed),
                    Upgrades = upgradeDataList,
                    TransferDirections = new Dictionary<MaterialType, int>(_world.TransferDirections)
                });
            }

            // Handle UI input
            var (action, upgradeType, material) = _miningUi.HandleInput(gp, _gpPrev);
            switch (action)
            {
                case MiningAction.ToggleTransfer:
                    if (material.HasValue)
                    {
                        _world.ToggleTransferDirection(material.Value);
                    }
                    break;

                case MiningAction.PurchaseUpgrade:
                    if (upgradeType.HasValue && _upgrades.TryPurchase(upgradeType.Value, GetResource, _world.TrySpendResource))
                    {
                        // Update values that depend on upgrades
                        if (upgradeType.Value == UpgradeType.MiningSpeed)
                        {
                            _world.SetMiningTransferRate(_upgrades.GetValue(UpgradeType.MiningSpeed));
                        }
                        else if (upgradeType.Value == UpgradeType.PredictionLength)
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
                blendState: BlendState.NonPremultiplied,
                samplerState: SamplerState.LinearClamp,
                depthStencilState: DepthStencilState.None,
                rasterizerState: RasterizerState.CullNone,
                effect: null,
                transformMatrix: _camera.GetViewMatrix(GraphicsDevice));

            // Planet (with shader)
            DrawPlanetShader();

            // Asteroids (with shader)
            DrawAsteroidsShader();

            // Past trail
            DrawPath(_world.ShipTrail, _config.PastTrailColor, _config.PastTrailWidth);

            // Predicted path (ship)
            DrawPath(_world.PredictedPath, _config.PredictedTrailColor, _config.PredictedTrailWidth);

            // Target predicted path
            if (_upgrades.IsUnlocked(UpgradeType.AsteroidTracker) && _world.SelectedTargetIndex >= 0)
            {
                DrawPath(_world.TargetPredictedPath, _config.TargetPredictedPathColor, _config.TargetPredictedPathWidth);

                // Draw closest approach indicator
                if (_world.HasClosestApproach)
                {
                    // Draw connecting line between ship and target at closest approach
                    DrawLineWorld(
                        _world.ClosestApproachShipPos,
                        _world.ClosestApproachTargetPos,
                        _config.ClosestApproachLineColor,
                        _config.ClosestApproachLineWidth);

                    // Draw marker rings at both closest approach positions
                    DrawRingWorld(
                        _world.ClosestApproachShipPos,
                        _config.ClosestApproachMarkerRadius,
                        _config.ClosestApproachMarkerColor,
                        _config.ClosestApproachMarkerThickness);

                    DrawRingWorld(
                        _world.ClosestApproachTargetPos,
                        _config.ClosestApproachMarkerRadius,
                        _config.ClosestApproachMarkerColor,
                        _config.ClosestApproachMarkerThickness);
                }
            }

            // Ship (using texture)
            Color shipColor = _world.Mode == GameMode.Mining
                ? _config.ShipDockedColor
                : _config.ShipFlightColor;
            DrawShip(_world.Ship.Position, _world.Ship.Radius, shipColor);

            // Highlight docked asteroid
            if (_world.Mode == GameMode.Mining && _world.DockedAsteroidIndex >= 0)
            {
                ref var asteroid = ref _world.Asteroids[_world.DockedAsteroidIndex];
                DrawCircleWorld(asteroid.Position, asteroid.Radius + _config.DockedHighlightPadding, _config.DockedHighlightColor);
            }

            // Highlight selected target asteroid
            if (_upgrades.IsUnlocked(UpgradeType.AsteroidTracker) && _world.SelectedTargetIndex >= 0 && _world.SelectedTargetIndex < _world.Asteroids.Length)
            {
                ref var target = ref _world.Asteroids[_world.SelectedTargetIndex];
                if (!target.Disabled)
                {
                    DrawRingWorld(target.Position, target.Radius + _config.TargetHighlightPadding, _config.TargetHighlightColor, _config.CursorRingThickness);
                }
            }

            // Draw cursor in target selection mode
            if (_upgrades.IsUnlocked(UpgradeType.AsteroidTracker) && _cameraMode == CameraMode.TargetSelection && _world.Mode == GameMode.Flight)
            {
                // Calculate cursor world position from ship + offset
                Vector2 cursorWorldPos = _world.Ship.Position + _cursorOffset;

                // Draw gray cursor reticle
                float cursorRadius = _config.CursorRingRadius / MathF.Max(_camera.Zoom, 0.0001f);
                DrawRingWorld(cursorWorldPos, cursorRadius, _config.CursorColor, _config.CursorRingThickness / MathF.Max(_camera.Zoom, 0.0001f));

                // Draw yellow highlight on closest asteroid (auto-selection)
                if (_hoveredAsteroidIndex >= 0 && _hoveredAsteroidIndex != _world.SelectedTargetIndex)
                {
                    ref var hovered = ref _world.Asteroids[_hoveredAsteroidIndex];
                    if (!hovered.Disabled)
                    {
                        // Draw yellow highlight for auto-selected closest asteroid
                        DrawRingWorld(hovered.Position, hovered.Radius + _config.TargetHighlightPadding, _config.HoveredAsteroidColor, _config.CursorRingThickness);
                    }
                }
            }

            // Draw impulse vector from ship with cooldown charge-up effect
            // Shows ship's forward direction (actual firing direction) scaled by aim magnitude
            float maxImpulse = _upgrades.GetValue(UpgradeType.ImpulseStrength);
            if (_world.Mode == GameMode.Flight && _impulseAim.LengthSquared() > (maxImpulse * maxImpulse) * 0.0025f)
            {
                float cooldown = _upgrades.GetValue(UpgradeType.ImpulseCooldown);
                float cooldownLeft = _world.ImpulseCooldownLeft;
                
                // Calculate charge progress (0 = just fired, 1 = fully charged)
                float chargeProgress = cooldown > 0.001f 
                    ? 1f - (cooldownLeft / cooldown) 
                    : 1f;
                chargeProgress = Clamp(chargeProgress, 0f, 1f);

                // Use ship's forward direction for the impulse visualization
                // This shows where the impulse will actually be applied
                float aimMagnitude = _impulseAim.Length();
                Vector2 forwardImpulse = _world.ShipForward * aimMagnitude;
                Vector2 fullEnd = _world.Ship.Position + forwardImpulse * _config.ImpulseAimScale;
                
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
                        Vector2 chargeEnd = _world.Ship.Position + forwardImpulse * _config.ImpulseAimScale * chargeProgress;
                        DrawLineWorld(_world.Ship.Position, chargeEnd, _config.ImpulseAimReadyColor, _config.ImpulseAimWidth);
                    }
                }
            }

            _spriteBatch.End();

            // Draw Myra UI on top
            _hud.Render();
            _miningUi.Render();
            _eventDialog.Render();

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

        /// <summary>
        /// Draws the planet using a gas giant shader with swirly bands.
        /// </summary>
        private void DrawPlanetShader()
        {
            _spriteBatch.End();

            // Set planet shader parameters
            _planetEffect.Parameters["Time"]?.SetValue(_shaderTime);
            _planetEffect.Parameters["BaseColor"]?.SetValue(_config.PlanetColor.ToVector3());
            _planetEffect.Parameters["BandColor1"]?.SetValue(_config.PlanetBandColor1.ToVector3());
            _planetEffect.Parameters["BandColor2"]?.SetValue(_config.PlanetBandColor2.ToVector3());

            _spriteBatch.Begin(
                sortMode: SpriteSortMode.Immediate,
                blendState: BlendState.AlphaBlend,
                samplerState: SamplerState.LinearClamp,
                depthStencilState: DepthStencilState.None,
                rasterizerState: RasterizerState.CullNone,
                effect: _planetEffect,
                transformMatrix: _camera.GetViewMatrix(GraphicsDevice));

            DrawCircleWorld(_world.Planet.Position, _world.Planet.Radius, Color.White);

            _spriteBatch.End();

            // Resume normal spritebatch
            _spriteBatch.Begin(
                sortMode: SpriteSortMode.Deferred,
                blendState: BlendState.AlphaBlend,
                samplerState: SamplerState.LinearClamp,
                depthStencilState: DepthStencilState.None,
                rasterizerState: RasterizerState.CullNone,
                effect: null,
                transformMatrix: _camera.GetViewMatrix(GraphicsDevice));
        }

        /// <summary>
        /// Draws all asteroids using the asteroid shader with material-based coloring.
        /// Uses LOD spatial hash for efficient culling based on zoom level.
        /// </summary>
        private void DrawAsteroidsShader()
        {
            _spriteBatch.End();

            // Calculate visible area in world coordinates
            var vp = GraphicsDevice.Viewport;
            float halfWidth = (vp.Width / 2f) / _camera.Zoom;
            float halfHeight = (vp.Height / 2f) / _camera.Zoom;

            // Calculate minimum asteroid radius to render based on screen size threshold
            float minAsteroidRadius = _config.MinAsteroidScreenSize / (2f * _camera.Zoom);

            // Set static asteroid shader parameters from MaterialDefinitions
            var iceDef = MaterialDefinitions.Get(MaterialType.Ice);
            var ironDef = MaterialDefinitions.Get(MaterialType.Iron);
            var rockDef = MaterialDefinitions.Get(MaterialType.Rock);
            _asteroidEffect.Parameters["IceColor"]?.SetValue(iceDef.Color.ToVector3());
            _asteroidEffect.Parameters["IronColor"]?.SetValue(ironDef.Color.ToVector3());
            _asteroidEffect.Parameters["RockColor"]?.SetValue(rockDef.Color.ToVector3());

            _spriteBatch.Begin(
                sortMode: SpriteSortMode.Immediate,
                blendState: BlendState.AlphaBlend,
                samplerState: SamplerState.LinearClamp,
                depthStencilState: DepthStencilState.None,
                rasterizerState: RasterizerState.CullNone,
                effect: _asteroidEffect,
                transformMatrix: _camera.GetViewMatrix(GraphicsDevice));

            // Query visible asteroids using LOD spatial hash - automatically filters by size
            var visibleAsteroids = _world.QueryVisibleAsteroids(_camera.Position, halfWidth, halfHeight, minAsteroidRadius);
            foreach (int i in visibleAsteroids)
            {
                ref var a = ref _world.Asteroids[i];
                
                if (a.Disabled)
                    continue;

                float total = a.TotalMaterials;
                if (total < 0.001f)
                {
                    // Depleted asteroid - draw simple gray
                    _spriteBatch.End();
                    _spriteBatch.Begin(
                        sortMode: SpriteSortMode.Deferred,
                        blendState: BlendState.AlphaBlend,
                        samplerState: SamplerState.LinearClamp,
                        depthStencilState: DepthStencilState.None,
                        rasterizerState: RasterizerState.CullNone,
                        effect: null,
                        transformMatrix: _camera.GetViewMatrix(GraphicsDevice));
                    DrawCircleWorld(a.Position, a.Radius, _config.AsteroidDepletedColor);
                    _spriteBatch.End();
                    _spriteBatch.Begin(
                        sortMode: SpriteSortMode.Immediate,
                        blendState: BlendState.AlphaBlend,
                        samplerState: SamplerState.LinearClamp,
                        depthStencilState: DepthStencilState.None,
                        rasterizerState: RasterizerState.CullNone,
                        effect: _asteroidEffect,
                        transformMatrix: _camera.GetViewMatrix(GraphicsDevice));
                    continue;
                }

                a.UpdateRadius();

                // Set per-asteroid parameters using material dictionary
                _asteroidEffect.Parameters["IceRatio"]?.SetValue(a.GetMaterial(MaterialType.Ice) / total);
                _asteroidEffect.Parameters["IronRatio"]?.SetValue(a.GetMaterial(MaterialType.Iron) / total);
                _asteroidEffect.Parameters["RockRatio"]?.SetValue(a.GetMaterial(MaterialType.Rock) / total);
                _asteroidEffect.Parameters["Seed"]?.SetValue((float)(i * 7.31)); // Unique seed per asteroid

                DrawCircleWorld(a.Position, a.Radius, Color.White);
            }

            _spriteBatch.End();

            // Resume normal spritebatch
            _spriteBatch.Begin(
                sortMode: SpriteSortMode.Deferred,
                blendState: BlendState.AlphaBlend,
                samplerState: SamplerState.LinearClamp,
                depthStencilState: DepthStencilState.None,
                rasterizerState: RasterizerState.CullNone,
                effect: null,
                transformMatrix: _camera.GetViewMatrix(GraphicsDevice));
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

        /// <summary>
        /// Draws a ring (unfilled circle) in world coordinates.
        /// </summary>
        private void DrawRingWorld(Vector2 center, float radiusWorld, Color color, float thickness)
        {
            // Draw ring using line segments
            const int segments = 32;
            float angleStep = MathF.PI * 2f / segments;

            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * angleStep;
                float angle2 = (i + 1) * angleStep;

                Vector2 p1 = center + new Vector2(MathF.Cos(angle1), MathF.Sin(angle1)) * radiusWorld;
                Vector2 p2 = center + new Vector2(MathF.Cos(angle2), MathF.Sin(angle2)) * radiusWorld;

                DrawLineWorld(p1, p2, color, thickness);
            }
        }

        /// <summary>
        /// Draws the ship using its texture, rotated to the ship's actual rotation angle.
        /// </summary>
        private void DrawShip(Vector2 position, float radiusWorld, Color tint)
        {
            var origin = new Vector2(_shipTexture.Width / 2f, _shipTexture.Height / 2f);
            
            // Scale to fit the ship radius (use the larger dimension of the texture)
            float texSize = MathF.Max(_shipTexture.Width, _shipTexture.Height);
            float scale = (radiusWorld * 2f) / texSize;

            // Use the ship's actual rotation from simulation
            float rotation = _world.ShipRotation;
            rotation -= MathF.PI / 2f; // Rotate by 90 degrees to point the ship texture upwards

            _spriteBatch.Draw(
                _shipTexture,
                position: position,
                sourceRectangle: null,
                color: tint,
                rotation: rotation,
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
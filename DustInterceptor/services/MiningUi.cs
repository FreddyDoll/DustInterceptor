using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra;
using Myra.Graphics2D.UI;

namespace DustInterceptor
{
    /// <summary>
    /// Actions that can be triggered from the mining UI.
    /// </summary>
    public enum MiningAction
    {
        None,
        UpgradeImpulse,
        UpgradeTimeScale,
        UpgradeMiningSpeed,
        Undock
    }

    /// <summary>
    /// Data needed to display the mining UI.
    /// </summary>
    public struct MiningUiData
    {
        public float AsteroidIce;
        public float AsteroidIron;
        public float AsteroidRock;
        public float ShipIce;
        public float ShipIron;
        public float ShipRock;

        // Upgrade info
        public float CurrentMaxImpulse;
        public float UpgradeImpulseCost;
        public bool CanAffordImpulseUpgrade;

        // Time scale upgrade info
        public int CurrentMaxTimeScale;
        public float UpgradeTimeScaleCost;
        public bool CanAffordTimeScaleUpgrade;
        public bool TimeScaleMaxedOut;

        // Mining speed upgrade info
        public float CurrentMiningSpeed;
        public float UpgradeMiningSpeedCost;
        public bool CanAffordMiningSpeedUpgrade;
    }

    /// <summary>
    /// Encapsulates Myra UI for the mining/docking screen.
    /// </summary>
    public sealed class MiningUi
    {
        private const int MenuItemCount = 4;

        private readonly Desktop _desktop;
        private readonly Panel _panel;
        private readonly Label _asteroidInfoLabel;
        private readonly Label _shipCargoLabel;
        private readonly Label _upgradeImpulseLabel;
        private readonly Label _upgradeTimeScaleLabel;
        private readonly Label _upgradeMiningSpeedLabel;
        private readonly Label _miningStatusLabel;
        private readonly float _fontScale;

        private int _menuSelection;
        private bool _canAffordImpulseUpgrade;
        private bool _canAffordTimeScaleUpgrade;
        private bool _canAffordMiningSpeedUpgrade;
        private bool _timeScaleMaxedOut;

        public MiningUi(Game game, float uiScale = 1f)
        {
            MyraEnvironment.Game = game;
            _fontScale = uiScale;

            _desktop = new Desktop();

            // Scale helper
            int S(int baseValue) => (int)(baseValue * uiScale);

            // Main mining panel - centered on screen
            _panel = new Panel
            {
                Width = S(550),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new Myra.Graphics2D.Brushes.SolidBrush(new Color(20, 25, 35, 220)),
                Visible = false
            };

            var stack = new VerticalStackPanel
            {
                Spacing = S(16),
                Margin = new Myra.Graphics2D.Thickness(S(15))
            };

            // Title
            var title = new Label
            {
                Text = "== MINING DOCK ==",
                TextColor = new Color(100, 255, 150),
                HorizontalAlignment = HorizontalAlignment.Center,
                Scale = new Vector2(_fontScale)
            };
            stack.Widgets.Add(title);

            // Mining status (auto-transfer indicator)
            _miningStatusLabel = new Label
            {
                Text = "[ TRANSFERRING... ]",
                TextColor = new Color(255, 200, 100),
                HorizontalAlignment = HorizontalAlignment.Center,
                Scale = new Vector2(_fontScale)
            };
            stack.Widgets.Add(_miningStatusLabel);

            // Asteroid info
            _asteroidInfoLabel = new Label
            {
                Text = "Asteroid Materials:",
                TextColor = Color.White,
                Wrap = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                Scale = new Vector2(_fontScale)
            };
            stack.Widgets.Add(_asteroidInfoLabel);

            // Separator
            stack.Widgets.Add(new Label
            {
                Text = "------------",
                TextColor = new Color(80, 80, 100),
                HorizontalAlignment = HorizontalAlignment.Center,
                Scale = new Vector2(_fontScale)
            });

            // Ship cargo
            _shipCargoLabel = new Label
            {
                Text = "Ship Cargo:",
                TextColor = new Color(255, 210, 80),
                Wrap = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                Scale = new Vector2(_fontScale)
            };
            stack.Widgets.Add(_shipCargoLabel);

            // Separator
            stack.Widgets.Add(new Label
            {
                Text = "------------",
                TextColor = new Color(80, 80, 100),
                HorizontalAlignment = HorizontalAlignment.Center,
                Scale = new Vector2(_fontScale)
            });

            // Upgrade info labels
            _upgradeImpulseLabel = new Label
            {
                Text = "Impulse: 10  |  Cost: 50 Iron",
                TextColor = new Color(150, 150, 200),
                HorizontalAlignment = HorizontalAlignment.Center,
                Scale = new Vector2(_fontScale)
            };
            stack.Widgets.Add(_upgradeImpulseLabel);

            _upgradeTimeScaleLabel = new Label
            {
                Text = "Max Warp: x4  |  Cost: 100 Iron",
                TextColor = new Color(150, 150, 200),
                HorizontalAlignment = HorizontalAlignment.Center,
                Scale = new Vector2(_fontScale)
            };
            stack.Widgets.Add(_upgradeTimeScaleLabel);

            _upgradeMiningSpeedLabel = new Label
            {
                Text = "Mining: 10/s  |  Cost: 75 Iron",
                TextColor = new Color(150, 150, 200),
                HorizontalAlignment = HorizontalAlignment.Center,
                Scale = new Vector2(_fontScale)
            };
            stack.Widgets.Add(_upgradeMiningSpeedLabel);

            // Separator
            stack.Widgets.Add(new Label
            {
                Text = "------------",
                TextColor = new Color(80, 80, 100),
                HorizontalAlignment = HorizontalAlignment.Center,
                Scale = new Vector2(_fontScale)
            });

            // Menu options (controller-friendly, highlight based on selection)
            var menuStack = new VerticalStackPanel { Spacing = S(12) };

            var upgradeImpulseBtn = new Label
            {
                Id = "menu_0",
                Text = "> [X] Upgrade Impulse",
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Scale = new Vector2(_fontScale)
            };
            menuStack.Widgets.Add(upgradeImpulseBtn);

            var upgradeTimeScaleBtn = new Label
            {
                Id = "menu_1",
                Text = "  [Y] Upgrade Time Warp",
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Scale = new Vector2(_fontScale)
            };
            menuStack.Widgets.Add(upgradeTimeScaleBtn);

            var upgradeMiningBtn = new Label
            {
                Id = "menu_2",
                Text = "  [A] Upgrade Mining Speed",
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Scale = new Vector2(_fontScale)
            };
            menuStack.Widgets.Add(upgradeMiningBtn);

            var undockBtn = new Label
            {
                Id = "menu_3",
                Text = "  [B] Undock",
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Scale = new Vector2(_fontScale)
            };
            menuStack.Widgets.Add(undockBtn);

            stack.Widgets.Add(menuStack);

            // Controls hint
            stack.Widgets.Add(new Label
            {
                Text = "\nD-Pad Up/Down to select",
                TextColor = new Color(100, 100, 120),
                HorizontalAlignment = HorizontalAlignment.Center,
                Scale = new Vector2(_fontScale)
            });

            _panel.Widgets.Add(stack);
            _desktop.Root = _panel;
        }

        /// <summary>
        /// Shows the mining UI and resets menu selection.
        /// </summary>
        public void Show()
        {
            _panel.Visible = true;
            _menuSelection = 0;
            UpdateMenuHighlight();
        }

        /// <summary>
        /// Hides the mining UI.
        /// </summary>
        public void Hide()
        {
            _panel.Visible = false;
        }

        /// <summary>
        /// Whether the UI is currently visible.
        /// </summary>
        public bool IsVisible => _panel.Visible;

        /// <summary>
        /// Updates the displayed data in the UI.
        /// </summary>
        public void UpdateData(MiningUiData data)
        {
            float asteroidTotal = data.AsteroidIce + data.AsteroidIron + data.AsteroidRock;
            _miningStatusLabel.Text = asteroidTotal > 0.1f 
                ? $"[ TRANSFERRING {data.CurrentMiningSpeed:F0}/s ]" 
                : "[ DEPLETED ]";
            _miningStatusLabel.TextColor = asteroidTotal > 0.1f 
                ? new Color(255, 200, 100) 
                : new Color(150, 150, 150);

            _asteroidInfoLabel.Text = $"Asteroid Materials:\n" +
                $"  Ice:  {data.AsteroidIce:F1}\n" +
                $"  Iron: {data.AsteroidIron:F1}\n" +
                $"  Rock: {data.AsteroidRock:F1}";

            _shipCargoLabel.Text = $"Ship Cargo:\n" +
                $"  Ice:  {data.ShipIce:F1}\n" +
                $"  Iron: {data.ShipIron:F1}\n" +
                $"  Rock: {data.ShipRock:F1}";

            _upgradeImpulseLabel.Text = $"Impulse: {data.CurrentMaxImpulse:F0}  |  Cost: {data.UpgradeImpulseCost:F0} Iron";

            if (data.TimeScaleMaxedOut)
            {
                _upgradeTimeScaleLabel.Text = $"Max Warp: x{data.CurrentMaxTimeScale}  |  MAXED OUT";
                _upgradeTimeScaleLabel.TextColor = new Color(100, 200, 100);
            }
            else
            {
                _upgradeTimeScaleLabel.Text = $"Max Warp: x{data.CurrentMaxTimeScale}  |  Cost: {data.UpgradeTimeScaleCost:F0} Iron";
                _upgradeTimeScaleLabel.TextColor = new Color(150, 150, 200);
            }

            _upgradeMiningSpeedLabel.Text = $"Mining: {data.CurrentMiningSpeed:F0}/s  |  Cost: {data.UpgradeMiningSpeedCost:F0} Iron";

            _canAffordImpulseUpgrade = data.CanAffordImpulseUpgrade;
            _canAffordTimeScaleUpgrade = data.CanAffordTimeScaleUpgrade;
            _canAffordMiningSpeedUpgrade = data.CanAffordMiningSpeedUpgrade;
            _timeScaleMaxedOut = data.TimeScaleMaxedOut;

            UpdateMenuHighlight();
        }

        /// <summary>
        /// Handles input and returns any triggered action.
        /// Call this each frame when the UI is visible.
        /// </summary>
        public MiningAction HandleInput(GamePadState gp, GamePadState gpPrev)
        {
            // Menu navigation (D-Pad)
            if (Pressed(gp.DPad.Up, gpPrev.DPad.Up))
            {
                _menuSelection = (_menuSelection - 1 + MenuItemCount) % MenuItemCount;
                UpdateMenuHighlight();
            }
            if (Pressed(gp.DPad.Down, gpPrev.DPad.Down))
            {
                _menuSelection = (_menuSelection + 1) % MenuItemCount;
                UpdateMenuHighlight();
            }

            // Menu actions
            if (Pressed(gp.Buttons.X, gpPrev.Buttons.X))
                return MiningAction.UpgradeImpulse;

            if (Pressed(gp.Buttons.Y, gpPrev.Buttons.Y))
                return MiningAction.UpgradeTimeScale;

            if (Pressed(gp.Buttons.A, gpPrev.Buttons.A))
                return MiningAction.UpgradeMiningSpeed;

            if (Pressed(gp.Buttons.B, gpPrev.Buttons.B))
                return MiningAction.Undock;

            return MiningAction.None;
        }

        /// <summary>
        /// Renders the UI. Call this after your SpriteBatch.End().
        /// </summary>
        public void Render()
        {
            _desktop.Render();
        }

        private void UpdateMenuHighlight()
        {
            for (int i = 0; i < MenuItemCount; i++)
            {
                var label = _panel.FindChildById($"menu_{i}") as Label;
                if (label != null)
                {
                    bool selected = (i == _menuSelection);
                    string prefix = selected ? "> " : "  ";
                    string suffix = i switch
                    {
                        0 => "[X] Upgrade Impulse",
                        1 => "[Y] Upgrade Time Warp",
                        2 => "[A] Upgrade Mining Speed",
                        3 => "[B] Undock",
                        _ => ""
                    };
                    label.Text = prefix + suffix;

                    // Determine if button should be grayed out
                    bool grayedOut = i switch
                    {
                        0 => !_canAffordImpulseUpgrade,
                        1 => !_canAffordTimeScaleUpgrade || _timeScaleMaxedOut,
                        2 => !_canAffordMiningSpeedUpgrade,
                        _ => false
                    };

                    if (grayedOut)
                    {
                        label.TextColor = new Color(128, 128, 128);
                    }
                    else
                    {
                        label.TextColor = selected ? new Color(100, 255, 150) : Color.White;
                    }
                }
            }
        }

        private static bool Pressed(ButtonState now, ButtonState prev) =>
            now == ButtonState.Pressed && prev == ButtonState.Released;
    }
}

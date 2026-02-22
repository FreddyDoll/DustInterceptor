using System.Collections.Generic;
using System.Linq;
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
        PurchaseUpgrade,
        Undock
    }

    /// <summary>
    /// Data needed to display the mining UI.
    /// </summary>
    public struct MiningUiData
    {
        /// <summary>Materials on the docked asteroid, keyed by MaterialType.</summary>
        public Dictionary<MaterialType, float> AsteroidMaterials;

        /// <summary>Materials in ship cargo, keyed by MaterialType.</summary>
        public Dictionary<MaterialType, float> ShipCargo;

        public float CurrentMiningSpeed;

        // Dynamic upgrade list
        public List<UpgradeDisplayData> Upgrades;
    }

    /// <summary>
    /// Encapsulates Myra UI for the mining/docking screen.
    /// Dynamically generates upgrade buttons from the upgrade system.
    /// </summary>
    public sealed class MiningUi
    {
        private const int MaxUpgradeSlots = 10;

        private readonly Desktop _desktop;
        private readonly Panel _panel;
        private readonly Label _asteroidInfoLabel;
        private readonly Label _shipCargoLabel;
        private readonly Label _miningStatusLabel;
        private readonly VerticalStackPanel _upgradeStack;
        private readonly float _fontScale;

        private int _menuSelection;
        private int _menuItemCount;
        private List<UpgradeDisplayData> _currentUpgrades = new();

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
                Scale = new Vector2(_fontScale),
                Margin = new Myra.Graphics2D.Thickness(0, S(5), 0, S(10))
            };
            stack.Widgets.Add(_miningStatusLabel);

            // Asteroid info
            _asteroidInfoLabel = new Label
            {
                Text = "Asteroid:",
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
                Text = "Ship:",
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

            // Upgrades section header
            stack.Widgets.Add(new Label
            {
                Text = "UPGRADES",
                TextColor = new Color(150, 150, 200),
                HorizontalAlignment = HorizontalAlignment.Center,
                Scale = new Vector2(_fontScale)
            });

            // Dynamic upgrade stack
            _upgradeStack = new VerticalStackPanel { Spacing = S(8) };
            
            // Pre-create upgrade slot labels
            for (int i = 0; i < MaxUpgradeSlots; i++)
            {
                var upgradeLabel = new Label
                {
                    Id = $"upgrade_{i}",
                    Text = "",
                    TextColor = Color.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Scale = new Vector2(_fontScale),
                    Visible = false
                };
                _upgradeStack.Widgets.Add(upgradeLabel);
            }
            stack.Widgets.Add(_upgradeStack);

            // Separator
            stack.Widgets.Add(new Label
            {
                Text = "------------",
                TextColor = new Color(80, 80, 100),
                HorizontalAlignment = HorizontalAlignment.Center,
                Scale = new Vector2(_fontScale)
            });

            // Undock button (always last)
            var undockBtn = new Label
            {
                Id = "undock_btn",
                Text = "  [B] Undock",
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Scale = new Vector2(_fontScale)
            };
            stack.Widgets.Add(undockBtn);

            // Controls hint
            stack.Widgets.Add(new Label
            {
                Text = "\nD-Pad Up/Down to select, A to buy",
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
            // Build asteroid info text dynamically from material definitions
            float asteroidTotal = 0f;
            var asteroidLines = "Asteroid:\n";
            if (data.AsteroidMaterials != null)
            {
                foreach (var matType in MaterialDefinitions.AllTypes)
                {
                    float amount = data.AsteroidMaterials.TryGetValue(matType, out float a) ? a : 0f;
                    var def = MaterialDefinitions.Get(matType);
                    asteroidLines += $"  {def.Name}:  {amount:F1}\n";
                    asteroidTotal += amount;
                }
            }

            _miningStatusLabel.Text = asteroidTotal > 0.1f 
                ? $"[ TRANSFERRING {data.CurrentMiningSpeed:F0}/s ]" 
                : "[ DEPLETED ]";
            _miningStatusLabel.TextColor = asteroidTotal > 0.1f 
                ? new Color(255, 200, 100) 
                : new Color(150, 150, 150);

            _asteroidInfoLabel.Text = asteroidLines.TrimEnd('\n');

            // Build ship cargo text dynamically
            var shipLines = "Ship:\n";
            if (data.ShipCargo != null)
            {
                foreach (var matType in MaterialDefinitions.AllTypes)
                {
                    float amount = data.ShipCargo.TryGetValue(matType, out float a) ? a : 0f;
                    var def = MaterialDefinitions.Get(matType);
                    shipLines += $"  {def.Name}:  {amount:F1}\n";
                }
            }
            _shipCargoLabel.Text = shipLines.TrimEnd('\n');

            // Update upgrade list
            _currentUpgrades = data.Upgrades ?? new List<UpgradeDisplayData>();
            _menuItemCount = _currentUpgrades.Count + 1; // +1 for undock

            // Update upgrade labels
            for (int i = 0; i < MaxUpgradeSlots; i++)
            {
                var label = _upgradeStack.Widgets[i] as Label;
                if (label == null) continue;

                if (i < _currentUpgrades.Count)
                {
                    var upgrade = _currentUpgrades[i];
                    label.Visible = true;
                    
                    string valueDisplay = upgrade.IsUnlock 
                        ? (upgrade.IsUnlocked ? "UNLOCKED" : "LOCKED")
                        : $"{upgrade.CurrentValue:F0}";
                    
                    string costDisplay = upgrade.CanUpgrade 
                        ? $"{upgrade.NextCost:F0} {upgrade.CostResource}"
                        : "MAXED";

                    label.Text = $"{upgrade.Name}: {valueDisplay}  |  {costDisplay}";
                }
                else
                {
                    label.Visible = false;
                }
            }

            // Clamp selection if list shrunk
            if (_menuSelection >= _menuItemCount)
                _menuSelection = _menuItemCount - 1;

            UpdateMenuHighlight();
        }

        /// <summary>
        /// Handles input and returns any triggered action plus the upgrade type if applicable.
        /// Call this each frame when the UI is visible.
        /// </summary>
        public (MiningAction action, UpgradeType upgradeType) HandleInput(GamePadState gp, GamePadState gpPrev)
        {
            // Menu navigation (D-Pad)
            if (Pressed(gp.DPad.Up, gpPrev.DPad.Up))
            {
                _menuSelection = (_menuSelection - 1 + _menuItemCount) % _menuItemCount;
                UpdateMenuHighlight();
            }
            if (Pressed(gp.DPad.Down, gpPrev.DPad.Down))
            {
                _menuSelection = (_menuSelection + 1) % _menuItemCount;
                UpdateMenuHighlight();
            }

            // Purchase upgrade (A button)
            if (Pressed(gp.Buttons.A, gpPrev.Buttons.A))
            {
                if (_menuSelection < _currentUpgrades.Count)
                {
                    var upgrade = _currentUpgrades[_menuSelection];
                    if (upgrade.CanUpgrade && upgrade.CanAfford)
                    {
                        return (MiningAction.PurchaseUpgrade, upgrade.Type);
                    }
                }
            }

            // Undock (B button - always works)
            if (Pressed(gp.Buttons.B, gpPrev.Buttons.B))
                return (MiningAction.Undock, default);

            return (MiningAction.None, default);
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
            // Update upgrade labels
            for (int i = 0; i < _currentUpgrades.Count && i < MaxUpgradeSlots; i++)
            {
                var label = _upgradeStack.Widgets[i] as Label;
                if (label == null) continue;

                var upgrade = _currentUpgrades[i];
                bool selected = (i == _menuSelection);
                string prefix = selected ? "> " : "  ";

                string valueDisplay = upgrade.IsUnlock 
                    ? (upgrade.IsUnlocked ? "UNLOCKED" : "LOCKED")
                    : $"{upgrade.CurrentValue:F0}";
                
                string costDisplay = upgrade.CanUpgrade 
                    ? $"{upgrade.NextCost:F0} {upgrade.CostResource}"
                    : "MAXED";

                label.Text = $"{prefix}{upgrade.Name}: {valueDisplay}  |  {costDisplay}";

                // Color based on affordability
                if (!upgrade.CanUpgrade)
                {
                    label.TextColor = new Color(100, 200, 100); // Green for maxed
                }
                else if (!upgrade.CanAfford)
                {
                    label.TextColor = new Color(128, 128, 128); // Gray for can't afford
                }
                else
                {
                    label.TextColor = selected ? new Color(100, 255, 150) : Color.White;
                }
            }

            // Update undock button
            var undockLabel = _panel.FindChildById("undock_btn") as Label;
            if (undockLabel != null)
            {
                bool undockSelected = (_menuSelection == _menuItemCount - 1);
                undockLabel.Text = (undockSelected ? "> " : "  ") + "[B] Undock";
                undockLabel.TextColor = undockSelected ? new Color(100, 255, 150) : Color.White;
            }
        }

        private static bool Pressed(ButtonState now, ButtonState prev) =>
            now == ButtonState.Pressed && prev == ButtonState.Released;
    }
}

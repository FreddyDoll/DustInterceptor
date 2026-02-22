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
        ToggleTransfer,
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

        public Dictionary<MaterialType, int> TransferDirections; // -1: Unload, 0: None, 1: Load
    }

    /// <summary>
    /// Encapsulates Myra UI for the mining/docking screen.
    /// Dynamically generates upgrade buttons from the upgrade system.
    /// </summary>
    public sealed class MiningUi
    {
        private const int MaxUpgradeSlots = 10;
        private const int MaxMaterialSlots = 10; // Reserve slots for materials

        private readonly Desktop _desktop;
        private readonly Panel _panel;
        private readonly Label _miningStatusLabel;
        private readonly VerticalStackPanel _materialStack; // New stack for materials
        private readonly VerticalStackPanel _upgradeStack;
        private readonly float _fontScale;

        private int _menuSelection;
        private int _menuItemCount;
        private List<UpgradeDisplayData> _currentUpgrades = new();
        private List<MaterialType> _currentMaterials = new(); // Track displayed materials

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
                Text = "[ DOCKED ]",
                TextColor = new Color(255, 200, 100),
                HorizontalAlignment = HorizontalAlignment.Center,
                Scale = new Vector2(_fontScale),
                Margin = new Myra.Graphics2D.Thickness(0, S(5), 0, S(10))
            };
            stack.Widgets.Add(_miningStatusLabel);

            // Column header for materials
            var materialHeaderGrid = new Grid { ColumnSpacing = S(10) };
            materialHeaderGrid.ColumnsProportions.Add(new Proportion(ProportionType.Part));
            materialHeaderGrid.ColumnsProportions.Add(new Proportion(ProportionType.Part));
            materialHeaderGrid.ColumnsProportions.Add(new Proportion(ProportionType.Part));

            materialHeaderGrid.Widgets.Add(new Label { Text = "Asteroid", TextColor = Color.Gray, HorizontalAlignment = HorizontalAlignment.Center, Scale = new Vector2(_fontScale), GridColumn = 0 });
            materialHeaderGrid.Widgets.Add(new Label { Text = "Transfer", TextColor = Color.Gray, HorizontalAlignment = HorizontalAlignment.Center, Scale = new Vector2(_fontScale), GridColumn = 1 });
            materialHeaderGrid.Widgets.Add(new Label { Text = "Ship", TextColor = Color.Gray, HorizontalAlignment = HorizontalAlignment.Center, Scale = new Vector2(_fontScale), GridColumn = 2 });
            stack.Widgets.Add(materialHeaderGrid);

            // Material list stack
            _materialStack = new VerticalStackPanel { Spacing = S(4) };
            for (int i = 0; i < MaxMaterialSlots; i++)
            {
                var label = new Label
                {
                    Id = $"material_{i}",
                    Text = "",
                    TextColor = Color.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Scale = new Vector2(_fontScale),
                    Visible = false
                };
                _materialStack.Widgets.Add(label);
            }
            stack.Widgets.Add(_materialStack);

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
            // Build material list
            _currentMaterials = MaterialDefinitions.AllTypes.ToList();

            bool anyTransferring = false;

            for (int i = 0; i < MaxMaterialSlots; i++)
            {
                var label = _materialStack.Widgets[i] as Label;
                if (label == null) continue;

                if (i < _currentMaterials.Count)
                {
                    var matType = _currentMaterials[i];
                    label.Visible = true;

                    float asteroidAmount = data.AsteroidMaterials != null && data.AsteroidMaterials.TryGetValue(matType, out float a) ? a : 0f;
                    float shipAmount = data.ShipCargo != null && data.ShipCargo.TryGetValue(matType, out float s) ? s : 0f;
                    var def = MaterialDefinitions.Get(matType);

                    int direction = 0;
                    if (data.TransferDirections != null && data.TransferDirections.TryGetValue(matType, out int dir))
                        direction = dir;

                    string arrow = "  |  ";
                    if (direction > 0)
                    {
                        arrow = " >>> ";
                        anyTransferring = true;
                    }
                    else if (direction < 0)
                    {
                        arrow = " <<< ";
                        anyTransferring = true;
                    }

                    label.Text = $"{asteroidAmount,6:F1} {arrow} {shipAmount,6:F1}  {def.Name}";
                }
                else
                {
                    label.Visible = false;
                }
            }

            _miningStatusLabel.Text = anyTransferring
                ? $"[ TRANSFERRING {data.CurrentMiningSpeed:F0}/s ]"
                : "[ DOCKED - IDLE ]";
            _miningStatusLabel.TextColor = anyTransferring
                ? new Color(255, 200, 100)
                : new Color(150, 150, 150);

            // Update upgrade list
            _currentUpgrades = data.Upgrades ?? new List<UpgradeDisplayData>();
            _menuItemCount = _currentMaterials.Count + _currentUpgrades.Count + 1; // Materials + Upgrades + Undock

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
        public (MiningAction action, UpgradeType? upgrade, MaterialType? material) HandleInput(GamePadState gp, GamePadState gpPrev)
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

            // Handle Material Selection (Toggle Direction)
            if (Pressed(gp.Buttons.A, gpPrev.Buttons.A) || Pressed(gp.DPad.Left, gpPrev.DPad.Left) || Pressed(gp.DPad.Right, gpPrev.DPad.Right))
            {
                if (_menuSelection < _currentMaterials.Count)
                {
                    return (MiningAction.ToggleTransfer, null, _currentMaterials[_menuSelection]);
                }
            }

            // Purchase upgrade (A button)
            if (Pressed(gp.Buttons.A, gpPrev.Buttons.A))
            {
                int upgradeIndex = _menuSelection - _currentMaterials.Count;
                if (upgradeIndex >= 0 && upgradeIndex < _currentUpgrades.Count)
                {
                    var upgrade = _currentUpgrades[upgradeIndex];
                    if (upgrade.CanUpgrade && upgrade.CanAfford)
                    {
                        return (MiningAction.PurchaseUpgrade, upgrade.Type, null);
                    }
                }
            }

            // Undock (B button - always works)
            if (Pressed(gp.Buttons.B, gpPrev.Buttons.B))
                return (MiningAction.Undock, null, null);

            return (MiningAction.None, null, null);
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
            // Update material labels
            for (int i = 0; i < _currentMaterials.Count && i < MaxMaterialSlots; i++)
            {
                var label = _materialStack.Widgets[i] as Label;
                if (label == null) continue;

                bool selected = (i == _menuSelection);

                // We just need to update color/prefix, text is updated in UpdateData mostly,
                // but let's refresh prefix
                string currentText = label.Text;
                if (currentText.StartsWith("> ") || currentText.StartsWith("  "))
                    currentText = currentText.Substring(2);

                string prefix = selected ? "> " : "  ";
                label.Text = prefix + currentText;
                label.TextColor = selected ? new Color(100, 255, 150) : Color.White;
            }

            // Update upgrade labels
            for (int i = 0; i < _currentUpgrades.Count && i < MaxUpgradeSlots; i++)
            {
                var label = _upgradeStack.Widgets[i] as Label;
                if (label == null) continue;

                var upgrade = _currentUpgrades[i];
                int menuIndex = _currentMaterials.Count + i;
                bool selected = (menuIndex == _menuSelection);
                string prefix = selected ? "> " : "  ";

                // Reconstruct text to ensure prefix is correct (similar logic to UpdateData but just prefix)
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
                bool selected = (_menuSelection == _menuItemCount - 1);
                undockLabel.Text = (selected ? "> " : "  ") + "[B] Undock";
                undockLabel.TextColor = selected ? new Color(100, 255, 150) : Color.White;
            }
        }

        private static bool Pressed(ButtonState now, ButtonState prev) =>
            now == ButtonState.Pressed && prev == ButtonState.Released;
    }
}

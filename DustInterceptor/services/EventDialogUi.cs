using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra;
using Myra.Graphics2D.UI;

namespace DustInterceptor
{
    /// <summary>
    /// FTL-style modal event dialog. Shows multi-line text with an [A] OK button.
    /// Pattern matches MiningUi: own Desktop, Show/Hide/IsVisible, HandleInput, Render.
    /// </summary>
    public sealed class EventDialogUi
    {
        private readonly Desktop _desktop;
        private readonly Panel _panel;
        private readonly Label _dialogLabel;
        private readonly float _fontScale;

        public EventDialogUi(Game game, float uiScale = 1f)
        {
            MyraEnvironment.Game = game;
            _fontScale = uiScale;

            _desktop = new Desktop();

            int S(int baseValue) => (int)(baseValue * uiScale);

            // Semi-transparent full-screen backdrop to dim the game behind the dialog
            var backdrop = new Panel
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = new Myra.Graphics2D.Brushes.SolidBrush(new Color(0, 0, 0, 140)),
                Visible = false
            };

            // Centered dialog box
            _panel = new Panel
            {
                Width = S(500),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new Myra.Graphics2D.Brushes.SolidBrush(new Color(15, 20, 35, 230)),
            };

            var stack = new VerticalStackPanel
            {
                Spacing = S(16),
                Margin = new Myra.Graphics2D.Thickness(S(20))
            };

            // Title bar
            var title = new Label
            {
                Text = "== INCOMING TRANSMISSION ==",
                TextColor = new Color(100, 200, 255),
                HorizontalAlignment = HorizontalAlignment.Center,
                Scale = new Vector2(_fontScale),
                Margin = new Myra.Graphics2D.Thickness(0, 0, 0, S(20))

            };
            stack.Widgets.Add(title);

            // Dialog text (multi-line)
            _dialogLabel = new Label
            {
                Text = "",
                TextColor = new Color(220, 220, 220),
                HorizontalAlignment = HorizontalAlignment.Center,
                Scale = new Vector2(_fontScale),
                Wrap = true
            };
            stack.Widgets.Add(_dialogLabel);

            // Separator
            stack.Widgets.Add(new Label
            {
                Text = "------------",
                TextColor = new Color(80, 80, 100),
                HorizontalAlignment = HorizontalAlignment.Center,
                Scale = new Vector2(_fontScale)
            });

            // OK button hint
            var okLabel = new Label
            {
                Text = "[A] OK",
                TextColor = new Color(100, 255, 150),
                HorizontalAlignment = HorizontalAlignment.Center,
                Scale = new Vector2(_fontScale)
            };
            stack.Widgets.Add(okLabel);

            _panel.Widgets.Add(stack);
            backdrop.Widgets.Add(_panel);

            // Store backdrop as root so we can toggle visibility on it
            _desktop.Root = backdrop;
        }

        /// <summary>
        /// Shows the dialog with the given event's dialog lines.
        /// </summary>
        public void Show(EventDefinition eventDef)
        {
            _dialogLabel.Text = string.Join("\n\n", eventDef.DialogLines);
            _desktop.Root.Visible = true;
        }

        /// <summary>
        /// Hides the dialog.
        /// </summary>
        public void Hide()
        {
            _desktop.Root.Visible = false;
        }

        /// <summary>
        /// Whether the dialog is currently visible.
        /// </summary>
        public bool IsVisible => _desktop.Root.Visible;

        /// <summary>
        /// Handles gamepad input. Returns true when the player presses A to dismiss.
        /// </summary>
        public bool HandleInput(GamePadState gp, GamePadState gpPrev)
        {
            if (!IsVisible)
                return false;

            if (gp.Buttons.A == ButtonState.Pressed && gpPrev.Buttons.A == ButtonState.Released)
            {
                Hide();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Renders the dialog UI. Call after HUD + MiningUi so it draws on top.
        /// </summary>
        public void Render()
        {
            if (IsVisible)
                _desktop.Render();
        }
    }
}

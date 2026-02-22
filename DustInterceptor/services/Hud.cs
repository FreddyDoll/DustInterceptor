using Microsoft.Xna.Framework;
using Myra;
using Myra.Graphics2D.UI;

namespace DustInterceptor
{
    /// <summary>
    /// Always-visible HUD showing time scale and elapsed game time in top-left corner.
    /// </summary>
    public sealed class Hud
    {
        private readonly Desktop _desktop;
        private readonly Label _timeScaleLabel;
        private readonly Label _gameTimeLabel;
        private readonly Label _fuelLabel;
        private readonly float _fontScale;

        private double _simulationTime; // Total simulation time in seconds (affected by time scale)

        public Hud(Game game, float uiScale = 1f)
        {
            MyraEnvironment.Game = game;
            _fontScale = uiScale;

            _desktop = new Desktop();

            int S(int baseValue) => (int)(baseValue * uiScale);

            // Container panel in top-left
            var panel = new Panel
            {
                Left = S(20),
                Top = S(20),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            var stack = new VerticalStackPanel
            {
                Spacing = S(5)
            };

            _gameTimeLabel = new Label
            {
                Text = "00:00:00",
                TextColor = new Color(180, 180, 180),
                Scale = new Vector2(_fontScale)
            };
            stack.Widgets.Add(_gameTimeLabel);

            _timeScaleLabel = new Label
            {
                Text = "x1",
                TextColor = new Color(200, 200, 200),
                Scale = new Vector2(_fontScale)
            };
            stack.Widgets.Add(_timeScaleLabel);

            _fuelLabel = new Label
            {
                Text = "Fuel: 0",
                TextColor = new Color(255, 220, 140),
                Scale = new Vector2(_fontScale)
            };
            stack.Widgets.Add(_fuelLabel);

            panel.Widgets.Add(stack);
            _desktop.Root = panel;
        }

        /// <summary>
        /// Updates the HUD display.
        /// </summary>
        public void Update(float realDt, int timeScale, float fuel = 0f)
        {
            // Accumulate simulation time
            _simulationTime += realDt * timeScale;

            // Format as HH:MM:SS
            int totalSeconds = (int)_simulationTime;
            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int seconds = totalSeconds % 60;
            _gameTimeLabel.Text = $"{hours:D2}:{minutes:D2}:{seconds:D2}";

            // Time scale display
            _timeScaleLabel.Text = $"x{timeScale}";

            // Fuel display
            _fuelLabel.Text = $"Fuel: {(int)fuel}";

            // Color based on time scale
            _timeScaleLabel.TextColor = timeScale switch
            {
                1 => new Color(150, 150, 150),
                2 => new Color(180, 180, 180),
                4 => new Color(200, 200, 200),
                8 => new Color(220, 220, 150),
                16 => new Color(240, 200, 100),
                32 => new Color(255, 150, 80),
                64 => new Color(255, 100, 100),
                128 => new Color(255, 80, 120),
                256 => new Color(235, 70, 160),
                512 => new Color(205, 70, 205),
                1024 => new Color(170, 90, 255),

                _ => Color.White
            };
        }

        /// <summary>
        /// Renders the HUD.
        /// </summary>
        public void Render()
        {
            _desktop.Render();
        }
    }
}

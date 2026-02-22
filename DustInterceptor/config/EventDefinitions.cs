namespace DustInterceptor
{
    /// <summary>
    /// Static helper that registers the design-doc event definitions into an EventManager.
    /// </summary>
    public static class EventDefinitions
    {
        /// <summary>
        /// Registers all built-in event definitions.
        /// Call once during game initialization.
        /// </summary>
        public static void RegisterAll(EventManager manager)
        {
            // Start-of-game intro
            manager.Register(new EventDefinition
            {
                Id = GameEvent.Intro,
                DialogLines = new[]
                {
                    "Welcome to the Dust Interceptor.",
                    "You are adrift in an asteroid belt orbiting a distant star.",
                    "Mine resources, upgrade your ship, and survive.",
                    "Use the left stick to aim and X to thrust. Good luck."
                },
                Trigger = new OnStartTrigger(),
                Repeatable = false
            });

            // After mining 10 iron
            manager.Register(new EventDefinition
            {
                Id = GameEvent.Iron10,
                DialogLines = new[]
                {
                    "Sensors detect iron reserves in your cargo.",
                    "Iron can be used to upgrade your ship systems.",
                    "Dock with an asteroid and open the upgrade panel to spend it."
                },
                Trigger = new ResourceThresholdTrigger(MaterialType.Iron, 10f),
                Repeatable = false
            });

            // After 1 minute of simulation time
            manager.Register(new EventDefinition
            {
                Id = GameEvent.OneMinute,
                DialogLines = new[]
                {
                    "One minute has passed in this sector.",
                    "Time compression is available — use LB/RB to adjust.",
                    "Higher warp speeds let you cover more ground between asteroids."
                },
                Trigger = new SimulationTimeTrigger(60f),
                Repeatable = false
            });
        }
    }
}

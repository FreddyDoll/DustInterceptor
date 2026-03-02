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
            // Intro sequence at game start
            manager.Register(new EventDefinition
            {
                Id = GameEvent.Intro,
                DialogLines = new[]
                {
                    "--- Dust Interceptor ---",
                    "",
                    "The plans for \"Genesis\" ar our only hope.",
                    "But there are other forces who want us stopped..."
                },
                Trigger = new OnStartTrigger(),
                Repeatable = false
            });
            manager.Register(new EventDefinition
            {
                Id = GameEvent.IntroPlanet,
                DialogLines = new[]
                {
                    "We are approaching A gas giant around \"HR 5191\".",
                },
                Trigger = new SimulationTimeTrigger(10f),
                Repeatable = false
            });
            manager.Register(new EventDefinition
            {
                Id = GameEvent.IntroExplosion,
                DialogLines = new[]
                {
                    "Swing by coming up",
                },
                Trigger = new SimulationTimeTrigger(20f),
                Repeatable = false
            });

            // Stage 1: Scattered Debris
            manager.Register(new EventDefinition
            {
                Id = GameEvent.DebrisTimewarp,
                DialogLines = new[]
                {
                    "some fuel...",
                    "at least something",
                    "---",
                    "Hold 'Left Stick' to select direction and strength",
                    "'X' to fire reactor"
                },
                Trigger = new ResourceThresholdTrigger(MaterialType.Debris, 1f),
                Repeatable = false
            });
            manager.Register(new EventDefinition
            {
                Id = GameEvent.DebrisTimewarp,
                DialogLines = new[]
                {
                    "We found parts of the Cryo system",
                    "We were able to install a basic system",
                    "---",
                    "'RB' to increase timewarp, 'LB' to decrease"
                },
                Trigger = new ResourceThresholdTrigger(MaterialType.Debris, 2f),
                Repeatable = false
            });
            manager.Register(new EventDefinition
            {
                Id = GameEvent.DebrisOrbitPrediction,
                DialogLines = new[]
                {
                    "The nav computer looks to be in workable condition",
                    "we should be able to predict our motion through the system",
                    "We can also control our sensor range",
                    "---",
                    "'LT' to zoom out",
                    "'RT' to zoom in",
                },
                Trigger = new ResourceThresholdTrigger(MaterialType.Debris, 3f),
                Repeatable = false
            });
            manager.Register(new EventDefinition
            {
                Id = GameEvent.DebrisTracker,
                DialogLines = new[]
                {
                    "We have found a radar tracker",
                    "This sould enable us to see paths of objects we are tracking",
                    "---",
                    "'Y' to cycle through Camera Modes",
                    "'Left stick' to move cursor",
                    "Select Object by pressing 'A'"
                },
                Trigger = new ResourceThresholdTrigger(MaterialType.Debris, 4f),
                Repeatable = false
            });
        }
    }
}

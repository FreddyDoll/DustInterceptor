namespace DustInterceptor
{
    /// <summary>
    /// Base class for event trigger conditions.
    /// </summary>
    public abstract class EventTrigger
    {
    }

    /// <summary>
    /// Fires once at game start (first update).
    /// </summary>
    public sealed class OnStartTrigger : EventTrigger
    {
    }

    /// <summary>
    /// Fires when ship cargo for the given material ? threshold.
    /// </summary>
    public sealed class ResourceThresholdTrigger : EventTrigger
    {
        public MaterialType Material { get; }
        public float Threshold { get; }

        public ResourceThresholdTrigger(MaterialType material, float threshold)
        {
            Material = material;
            Threshold = threshold;
        }
    }

    /// <summary>
    /// Fires when total simulation time ? threshold (in seconds).
    /// </summary>
    public sealed class SimulationTimeTrigger : EventTrigger
    {
        public float Seconds { get; }

        public SimulationTimeTrigger(float seconds)
        {
            Seconds = seconds;
        }
    }

    /// <summary>
    /// Defines a single game event: its identity, dialog text, trigger condition, and repeatability.
    /// </summary>
    public sealed class EventDefinition
    {
        /// <summary>Unique identifier for this event.</summary>
        public GameEvent Id { get; init; }

        /// <summary>Lines of dialog shown to the player when the event fires.</summary>
        public string[] DialogLines { get; init; } = [];

        /// <summary>Condition that must be met for the event to fire.</summary>
        public EventTrigger Trigger { get; init; } = new OnStartTrigger();

        /// <summary>If false, the event fires only once and is then permanently dismissed.</summary>
        public bool Repeatable { get; init; } = false;
    }
}

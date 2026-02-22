using System.Collections.Generic;

namespace DustInterceptor
{
    /// <summary>
    /// Central manager for the event system. Evaluates triggers each update and
    /// tracks which non-repeatable events have already fired.
    /// </summary>
    public sealed class EventManager
    {
        private readonly List<EventDefinition> _definitions = new();
        private readonly HashSet<GameEvent> _firedEvents = new();
        private bool _startHandled;

        /// <summary>
        /// The event currently being shown to the player (null = none).
        /// </summary>
        public EventDefinition? ActiveEvent { get; private set; }

        /// <summary>
        /// Registers an event definition. Call during initialization.
        /// </summary>
        public void Register(EventDefinition definition)
        {
            _definitions.Add(definition);
        }

        /// <summary>
        /// Evaluates all triggers and returns the first un-fired event whose condition is met,
        /// or null if no event should fire this frame.
        /// Sets <see cref="ActiveEvent"/> when an event fires.
        /// </summary>
        /// <param name="simulationTime">Total elapsed simulation time in seconds.</param>
        /// <param name="cargo">Current ship cargo amounts keyed by material type.</param>
        /// <returns>The event definition that fired, or null.</returns>
        public EventDefinition? Update(float simulationTime, IReadOnlyDictionary<MaterialType, float> cargo)
        {
            if (ActiveEvent != null)
                return ActiveEvent;

            foreach (var def in _definitions)
            {
                // Skip already-fired non-repeatable events
                if (!def.Repeatable && _firedEvents.Contains(def.Id))
                    continue;

                if (EvaluateTrigger(def.Trigger, simulationTime, cargo))
                {
                    ActiveEvent = def;
                    return def;
                }
            }

            return null;
        }

        /// <summary>
        /// Marks the active event as fired and clears it, resuming normal gameplay.
        /// </summary>
        public void Dismiss()
        {
            if (ActiveEvent != null)
            {
                _firedEvents.Add(ActiveEvent.Id);
                ActiveEvent = null;
            }
        }

        private bool EvaluateTrigger(EventTrigger trigger, float simulationTime, IReadOnlyDictionary<MaterialType, float> cargo)
        {
            switch (trigger)
            {
                case OnStartTrigger:
                    if (!_startHandled)
                    {
                        _startHandled = true;
                        return true;
                    }
                    return false;

                case ResourceThresholdTrigger rt:
                    return cargo.TryGetValue(rt.Material, out float amount) && amount >= rt.Threshold;

                case SimulationTimeTrigger st:
                    return simulationTime >= st.Seconds;

                default:
                    return false;
            }
        }
    }
}

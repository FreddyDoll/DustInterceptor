namespace DustInterceptor
{
    /// <summary>
    /// Camera control modes for flight.
    /// Y button cycles through these modes in flight.
    /// </summary>
    public enum CameraMode
    {
        /// <summary>
        /// Camera is locked to follow the ship.
        /// </summary>
        LockedToShip,

        /// <summary>
        /// Target selection mode - right stick moves cursor to select asteroids.
        /// </summary>
        TargetSelection,

        /// <summary>
        /// Free pan mode - right stick pans camera freely.
        /// </summary>
        FreePan
    }
}
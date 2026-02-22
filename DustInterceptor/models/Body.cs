using Microsoft.Xna.Framework;

namespace DustInterceptor
{
    public struct Body
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Radius;
        
        /// <summary>
        /// Rotation angle in radians. 0 = pointing right (+X), PI/2 = pointing up (+Y).
        /// </summary>
        public float Rotation;
        
        /// <summary>
        /// Angular velocity in radians per second.
        /// </summary>
        public float AngularVelocity;
    }
}
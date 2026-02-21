using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DustInterceptor
{
    sealed class Camera2D
    {
        public Vector2 Position;
        public float Zoom = 1.0f;

        public Matrix GetViewMatrix(GraphicsDevice gd)
        {
            var vp = gd.Viewport;
            var screenCenter = new Vector2(vp.Width * 0.5f, vp.Height * 0.5f);

            return
                Matrix.CreateTranslation(new Vector3(-Position, 0f)) *
                Matrix.CreateScale(Zoom, Zoom, 1f) *
                Matrix.CreateTranslation(new Vector3(screenCenter, 0f));
        }
    }
}
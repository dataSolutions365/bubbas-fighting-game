using UnityEngine;

namespace Bubba.Input
{
    /// Where the on-screen buttons sit, in screen pixels with the origin at
    /// the bottom-left (the same space touches arrive in). The HUD draws
    /// them from these rects so drawing and hit-testing always agree.
    public static class TouchLayout
    {
        private static float Unit => Mathf.Max(Screen.height, 1) * 0.14f;

        private static Rect FromRight(float col, float row)
        {
            float u = Unit;
            float pad = u * 0.2f;
            return new Rect(Screen.width - (col + 1) * (u + pad) - pad, pad + row * (u + pad), u, u);
        }

        // Medium/Hard action buttons, bottom-right cluster.
        public static Rect Light => FromRight(1, 1);
        public static Rect Heavy => FromRight(0, 1);
        public static Rect Block => FromRight(1, 0);
        public static Rect Dodge => FromRight(0, 0);

        // Special ability buttons: above the cluster in Medium, bottom-right in Easy.
        public static Rect Special(int index, ControlScheme scheme)
        {
            float row = scheme == ControlScheme.Easy ? 0 : 2;
            return FromRight(1 - index, row);
        }

        /// The joystick lives anywhere on the left 40% of the screen.
        public static bool InJoystickZone(Vector2 p) => p.x < Screen.width * 0.4f;

        /// Converts a layout rect into OnGUI space (origin top-left).
        public static Rect ToGui(Rect r) => new Rect(r.x, Screen.height - r.y - r.height, r.width, r.height);
    }
}

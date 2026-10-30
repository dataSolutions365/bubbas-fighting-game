using UnityEngine;

namespace Bubba.Input
{
    /// Medium controls: a floating joystick on the left of the screen and
    /// Light / Heavy / Block / Dodge plus two special buttons on the right.
    /// The controller turns joystick direction + Heavy into different moves.
    public class VirtualPadInput
    {
        /// Fraction of screen height the stick can travel from where the thumb landed.
        public float stickRadiusFraction = 0.12f;
        public float deadZone = 0.25f;

        private int stickId = int.MinValue;
        private Vector2 stickOrigin;

        public Vector2 StickOrigin => stickOrigin;
        public Vector2 Stick { get; private set; }
        public bool StickActive => stickId != int.MinValue;

        private bool lightWas, heavyWas, dodgeWas;
        private readonly bool[] specialWas = new bool[2];

        public FighterCommand Read(ControlScheme scheme)
        {
            var cmd = FighterCommand.None;
            var pointers = PointerSource.Active();
            float radius = Screen.height * stickRadiusFraction;

            bool stickFound = false;
            bool light = false, heavy = false, block = false, dodge = false;
            var special = new bool[2];

            foreach (var p in pointers)
            {
                if (p.id == stickId)
                {
                    stickFound = true;
                    Stick = Vector2.ClampMagnitude((p.position - stickOrigin) / radius, 1f);
                    continue;
                }
                if (TouchLayout.Light.Contains(p.position)) { light = true; continue; }
                if (TouchLayout.Heavy.Contains(p.position)) { heavy = true; continue; }
                if (TouchLayout.Block.Contains(p.position)) { block = true; continue; }
                if (TouchLayout.Dodge.Contains(p.position)) { dodge = true; continue; }
                if (TouchLayout.Special(0, scheme).Contains(p.position)) { special[0] = true; continue; }
                if (TouchLayout.Special(1, scheme).Contains(p.position)) { special[1] = true; continue; }
                if (!stickFound && stickId == int.MinValue && TouchLayout.InJoystickZone(p.position))
                {
                    stickId = p.id;
                    stickOrigin = p.position;
                    Stick = Vector2.zero;
                    stickFound = true;
                }
            }

            if (!stickFound)
            {
                stickId = int.MinValue;
                Stick = Vector2.zero;
            }

            cmd.move = Mathf.Abs(Stick.x) > deadZone ? Stick.x : 0f;
            cmd.light = light && !lightWas;
            cmd.heavy = heavy && !heavyWas;
            cmd.dodge = dodge && !dodgeWas;
            cmd.blockHeld = block;
            for (int i = 0; i < special.Length; i++)
            {
                if (special[i] && !specialWas[i]) cmd.special = i;
                specialWas[i] = special[i];
            }
            lightWas = light;
            heavyWas = heavy;
            dodgeWas = dodge;
            return cmd;
        }
    }
}

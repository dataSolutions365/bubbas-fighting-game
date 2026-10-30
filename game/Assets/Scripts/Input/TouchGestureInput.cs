using UnityEngine;

namespace Bubba.Input
{
    /// Easy controls, Injustice style:
    ///   tap                 light attack (tap again quickly to combo)
    ///   swipe toward foe    heavy attack
    ///   swipe away          dodge back
    ///   press and hold      block
    ///   ability buttons     specials
    /// Walking is handled for the player (see FighterController.assistMovement).
    public class TouchGestureInput
    {
        public float tapMaxSeconds = 0.25f;
        public float holdSeconds = 0.2f;
        /// Fraction of screen height a finger must travel to count as a swipe.
        public float swipeFraction = 0.08f;

        private int trackedId = int.MinValue;
        private Vector2 startPos;
        private Vector2 lastPos;
        private float startTime;
        private bool[] specialWasDown = new bool[2];

        public FighterCommand Read(int facingSign)
        {
            var cmd = FighterCommand.None;
            var pointers = PointerSource.Active();

            // Ability buttons are checked first and never start a gesture.
            for (int i = 0; i < specialWasDown.Length; i++)
            {
                bool down = false;
                foreach (var p in pointers)
                {
                    if (TouchLayout.Special(i, ControlScheme.Easy).Contains(p.position)) down = true;
                }
                if (down && !specialWasDown[i]) cmd.special = i;
                specialWasDown[i] = down;
            }

            bool trackedStillDown = false;
            foreach (var p in pointers)
            {
                if (p.id == trackedId)
                {
                    trackedStillDown = true;
                    lastPos = p.position;
                }
            }

            if (trackedId != int.MinValue && !trackedStillDown)
            {
                // Finger lifted: decide what the gesture was.
                Vector2 delta = lastPos - startPos;
                float held = Time.time - startTime;
                float swipe = Screen.height * swipeFraction;
                if (Mathf.Abs(delta.x) > swipe && Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                {
                    bool toward = Mathf.Sign(delta.x) == facingSign;
                    cmd.heavy = toward;
                    cmd.dodge = !toward;
                }
                else if (held <= tapMaxSeconds && delta.magnitude < swipe)
                {
                    cmd.light = true;
                }
                trackedId = int.MinValue;
            }

            if (trackedId == int.MinValue)
            {
                foreach (var p in pointers)
                {
                    if (IsOnSpecialButton(p.position)) continue;
                    trackedId = p.id;
                    startPos = lastPos = p.position;
                    startTime = Time.time;
                    break;
                }
            }
            else
            {
                // Still pressing without moving much: that's a block.
                bool still = (lastPos - startPos).magnitude < Screen.height * swipeFraction * 0.5f;
                cmd.blockHeld = still && Time.time - startTime >= holdSeconds;
            }

            return cmd;
        }

        private static bool IsOnSpecialButton(Vector2 p)
        {
            return TouchLayout.Special(0, ControlScheme.Easy).Contains(p)
                || TouchLayout.Special(1, ControlScheme.Easy).Contains(p);
        }
    }
}

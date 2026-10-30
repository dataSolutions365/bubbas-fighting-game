using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Bubba.Input
{
    /// Keyboard controls for testing in the Unity editor:
    /// A/D or arrows move, J light, K heavy, L (hold) block, Space dodge, 1/2 specials.
    public static class KeyboardReader
    {
        public static FighterCommand Read()
        {
            var cmd = FighterCommand.None;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return cmd;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) cmd.move -= 1f;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) cmd.move += 1f;
            cmd.light = k.jKey.wasPressedThisFrame;
            cmd.heavy = k.kKey.wasPressedThisFrame;
            cmd.blockHeld = k.lKey.isPressed;
            cmd.dodge = k.spaceKey.wasPressedThisFrame;
            if (k.digit1Key.wasPressedThisFrame) cmd.special = 0;
            if (k.digit2Key.wasPressedThisFrame) cmd.special = 1;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow)) cmd.move -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow)) cmd.move += 1f;
            cmd.light = UnityEngine.Input.GetKeyDown(KeyCode.J);
            cmd.heavy = UnityEngine.Input.GetKeyDown(KeyCode.K);
            cmd.blockHeld = UnityEngine.Input.GetKey(KeyCode.L);
            cmd.dodge = UnityEngine.Input.GetKeyDown(KeyCode.Space);
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1)) cmd.special = 0;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2)) cmd.special = 1;
#endif
            return cmd;
        }

        public static FighterCommand Merge(FighterCommand a, FighterCommand b)
        {
            return new FighterCommand
            {
                move = Mathf.Clamp(a.move + b.move, -1f, 1f),
                light = a.light || b.light,
                heavy = a.heavy || b.heavy,
                blockHeld = a.blockHeld || b.blockHeld,
                dodge = a.dodge || b.dodge,
                special = a.special >= 0 ? a.special : b.special,
                specialByMotion = a.specialByMotion || b.specialByMotion,
            };
        }
    }
}

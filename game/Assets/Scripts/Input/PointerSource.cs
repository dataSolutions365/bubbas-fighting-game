using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Bubba.Input
{
    /// Reads touches (or the mouse in the editor) through whichever input
    /// backend the project uses, so the controls work with the old Input
    /// Manager and with the newer Input System package.
    public static class PointerSource
    {
        public struct Pointer
        {
            public int id;
            public Vector2 position;
        }

        private static readonly List<Pointer> Buffer = new List<Pointer>();

        /// All fingers currently on the screen. The mouse counts as id -1 while held.
        public static List<Pointer> Active()
        {
            Buffer.Clear();
#if ENABLE_INPUT_SYSTEM
            if (Touchscreen.current != null)
            {
                foreach (var touch in Touchscreen.current.touches)
                {
                    if (touch.press.isPressed)
                    {
                        Buffer.Add(new Pointer { id = touch.touchId.ReadValue(), position = touch.position.ReadValue() });
                    }
                }
            }
            if (Buffer.Count == 0 && Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                Buffer.Add(new Pointer { id = -1, position = Mouse.current.position.ReadValue() });
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            for (int i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                var touch = UnityEngine.Input.GetTouch(i);
                if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled)
                {
                    Buffer.Add(new Pointer { id = touch.fingerId, position = touch.position });
                }
            }
            if (Buffer.Count == 0 && UnityEngine.Input.touchCount == 0 && UnityEngine.Input.GetMouseButton(0))
            {
                Buffer.Add(new Pointer { id = -1, position = UnityEngine.Input.mousePosition });
            }
#endif
            return Buffer;
        }
    }
}

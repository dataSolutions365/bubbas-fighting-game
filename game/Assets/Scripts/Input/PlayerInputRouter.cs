using Bubba.Combat;
using UnityEngine;

namespace Bubba.Input
{
    /// Put this on the player's fighter. It reads the control scheme the
    /// player picked (Easy or Medium) and, in the editor, the keyboard too.
    public class PlayerInputRouter : MonoBehaviour, IFighterInput
    {
        public const string SchemePrefKey = "controlScheme";

        public ControlScheme scheme = ControlScheme.Easy;
        public bool keyboardInEditor = true;

        private readonly TouchGestureInput gestures = new TouchGestureInput();
        private readonly VirtualPadInput pad = new VirtualPadInput();
        private FighterController fighter;

        public VirtualPadInput Pad => pad;

        private void Awake()
        {
            fighter = GetComponent<FighterController>();
            scheme = (ControlScheme)PlayerPrefs.GetInt(SchemePrefKey, (int)scheme);
        }

        public void SetScheme(ControlScheme value)
        {
            scheme = value;
            PlayerPrefs.SetInt(SchemePrefKey, (int)value);
        }

        public FighterCommand ReadCommand()
        {
            int facing = fighter != null ? fighter.FacingSign : 1;
            FighterCommand cmd = scheme == ControlScheme.Easy ? gestures.Read(facing) : pad.Read(scheme);
            if (keyboardInEditor && Application.isEditor)
            {
                cmd = KeyboardReader.Merge(cmd, KeyboardReader.Read());
            }
            return cmd;
        }
    }
}

namespace Bubba.Input
{
    /// What a fighter wants to do this frame, whoever is controlling it
    /// (touch, virtual pad, keyboard or AI).
    public struct FighterCommand
    {
        /// -1 = toward the left of the screen, +1 = toward the right.
        public float move;
        /// Joystick held toward the opponent (+1), away (-1) or neutral (0).
        /// Set by the controller from `move`; inputs don't fill it in.
        public int towardOpponent;
        public bool light;
        public bool heavy;
        public bool blockHeld;
        public bool dodge;
        /// Which equipped ability to fire this frame, or -1.
        public int special;
        /// True when the special was entered with a motion input (Hard mode).
        public bool specialByMotion;

        public static FighterCommand None => new FighterCommand { special = -1 };
    }

    public enum ControlScheme
    {
        /// Tap, swipe and hold anywhere. Movement is assisted.
        Easy,
        /// Virtual joystick plus attack, heavy, block and dodge buttons.
        /// Direction + heavy gives different moves; well-timed blocks parry.
        Medium,
        /// Medium controls, but specials need a motion on the joystick
        /// and the parry window is tighter. (Planned after the prototype.)
        Hard,
    }

    public interface IFighterInput
    {
        FighterCommand ReadCommand();
    }
}

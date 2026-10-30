using Bubba.Combat;
using Bubba.Input;
using UnityEngine;

namespace Bubba.AI
{
    public enum AiDifficulty { Easy, Medium, Hard }

    /// A computer opponent. It "thinks" every reactionTime seconds and
    /// otherwise repeats its last decision, which is what makes it beatable.
    [RequireComponent(typeof(FighterController))]
    public class SimpleAI : MonoBehaviour, IFighterInput
    {
        public AiDifficulty difficulty = AiDifficulty.Medium;

        private FighterController self;
        private float nextThinkAt;
        private float blockUntil;
        private FighterCommand planned = FighterCommand.None;

        private float ReactionTime => difficulty switch { AiDifficulty.Easy => 0.45f, AiDifficulty.Hard => 0.15f, _ => 0.28f };
        private float DefendChance => difficulty switch { AiDifficulty.Easy => 0.2f, AiDifficulty.Hard => 0.7f, _ => 0.45f };
        private float Aggression => difficulty switch { AiDifficulty.Easy => 0.45f, AiDifficulty.Hard => 0.85f, _ => 0.65f };
        private float SpecialChance => difficulty switch { AiDifficulty.Easy => 0.05f, AiDifficulty.Hard => 0.25f, _ => 0.12f };

        private void Awake()
        {
            self = GetComponent<FighterController>();
        }

        public FighterCommand ReadCommand()
        {
            var foe = self.opponent;
            if (foe == null) return FighterCommand.None;

            var cmd = FighterCommand.None;
            cmd.blockHeld = Time.time < blockUntil;
            cmd.move = planned.move;

            if (Time.time < nextThinkAt) return cmd;
            nextThinkAt = Time.time + ReactionTime * Random.Range(0.8f, 1.25f);
            planned = FighterCommand.None;

            float dist = self.DistanceToOpponent;
            int toward = self.FacingSign;

            // Defend against an attack that is winding up and will reach us.
            if (foe.IsAttackWindingUp && dist < self.stats.heavyRange + 1.5f && Random.value < DefendChance)
            {
                if (Random.value < 0.7f) blockUntil = Time.time + 0.6f;
                else cmd.dodge = true;
                cmd.blockHeld = Time.time < blockUntil;
                return cmd;
            }

            // Use a special when one is ready.
            var abilities = self.Abilities;
            if (abilities != null && Random.value < SpecialChance)
            {
                for (int i = 0; i < abilities.equipped.Count; i++)
                {
                    if (abilities.CooldownRemaining(i) <= 0f)
                    {
                        cmd.special = i;
                        return cmd;
                    }
                }
            }

            if (dist <= self.stats.lightRange)
            {
                if (Random.value < Aggression)
                {
                    if (Random.value < 0.7f) cmd.light = true;
                    else cmd.heavy = true;
                }
                else
                {
                    planned.move = -toward * 0.6f; // back off a little
                }
            }
            else if (dist <= self.stats.heavyRange && Random.value < Aggression * 0.5f)
            {
                cmd.heavy = true;
            }
            else
            {
                planned.move = toward;
            }

            cmd.move = planned.move;
            return cmd;
        }
    }
}

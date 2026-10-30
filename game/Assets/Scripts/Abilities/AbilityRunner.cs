using System.Collections;
using System.Collections.Generic;
using Bubba.Combat;
using UnityEngine;

namespace Bubba.Abilities
{
    /// Holds the two equipped special abilities, their cooldowns, and
    /// applies their effects in a fight.
    [RequireComponent(typeof(FighterController))]
    public class AbilityRunner : MonoBehaviour
    {
        public List<AbilityDefinition> equipped = new List<AbilityDefinition>();

        private readonly List<float> readyAt = new List<float>();
        private FighterController fighter;

        private void Awake()
        {
            fighter = GetComponent<FighterController>();
        }

        public void Equip(IEnumerable<string> abilityIds, AbilityLibrary library)
        {
            equipped.Clear();
            foreach (var id in abilityIds)
            {
                var def = library.Find(id);
                if (def != null) equipped.Add(def);
                else Debug.LogWarning($"No AbilityDefinition for '{id}'.");
            }
            ResetCooldowns();
        }

        public void ResetCooldowns()
        {
            readyAt.Clear();
            for (int i = 0; i < equipped.Count; i++) readyAt.Add(0f);
        }

        public float CooldownRemaining(int index)
        {
            if (index < 0 || index >= readyAt.Count) return 0f;
            return Mathf.Max(0f, readyAt[index] - Time.time);
        }

        public bool TryActivate(int index)
        {
            if (index < 0 || index >= equipped.Count || CooldownRemaining(index) > 0f) return false;
            var a = equipped[index];
            readyAt[index] = Time.time + a.cooldown;
            Apply(a);
            return true;
        }

        private void Apply(AbilityDefinition a)
        {
            switch (a.effect)
            {
                case AbilityEffect.Strike:
                    fighter.StartAttack(new AttackSpec
                    {
                        name = a.displayName,
                        damage = a.damage,
                        range = a.range,
                        windup = a.windup,
                        recovery = 0.35f,
                        stun = 0.6f,
                        knockback = 1f,
                        dash = a.dash,
                    });
                    break;
                case AbilityEffect.Stagger:
                    if (fighter.opponent != null && fighter.DistanceToOpponent <= a.range)
                    {
                        fighter.opponent.Stun(a.duration);
                    }
                    break;
                case AbilityEffect.Armor:
                    StartCoroutine(Timed(a.duration,
                        () => fighter.Health.DamageTakenMultiplier = a.magnitude,
                        () => fighter.Health.DamageTakenMultiplier = 1f));
                    break;
                case AbilityEffect.PowerBuff:
                    StartCoroutine(Timed(a.duration,
                        () => fighter.PowerMultiplier = a.magnitude,
                        () => fighter.PowerMultiplier = 1f));
                    break;
                case AbilityEffect.Haste:
                    StartCoroutine(Timed(a.duration,
                        () => fighter.SpeedMultiplier = a.magnitude,
                        () => fighter.SpeedMultiplier = 1f));
                    break;
            }
        }

        private static IEnumerator Timed(float seconds, System.Action begin, System.Action end)
        {
            begin();
            yield return new WaitForSeconds(seconds);
            end();
        }
    }
}

using UnityEngine;

namespace Bubba.Abilities
{
    public enum AbilityEffect
    {
        /// Dash forward and hit (Pounce, Charge, Maul).
        Strike,
        /// Stun the opponent if they are within range (Roar).
        Stagger,
        /// Take less damage for a while (Thick Hide).
        Armor,
        /// Hit harder for a while (Chest Beat).
        PowerBuff,
        /// Move and attack faster for a while (Frenzy).
        Haste,
    }

    /// How an ability behaves in a fight. Prices, ownership and premium
    /// flags live in the Firestore "abilities" catalog; abilityId links the two.
    [CreateAssetMenu(menuName = "Bubba/Ability", fileName = "Ability")]
    public class AbilityDefinition : ScriptableObject
    {
        public string abilityId = "roar";
        public string displayName = "Roar";
        public AbilityEffect effect = AbilityEffect.Stagger;
        public float cooldown = 12f;

        [Header("Strike / Stagger")]
        public float damage;
        public float range = 2.5f;
        public float dash;
        public float windup = 0.25f;

        [Header("Stagger / buffs")]
        public float duration = 1f;
        [Tooltip("Armor: damage taken multiplier. PowerBuff/Haste: multiplier on power/speed.")]
        public float magnitude = 1f;

        public static AbilityDefinition Create(string id, string name, AbilityEffect effect, float cooldown,
            float damage = 0f, float range = 2.5f, float dash = 0f, float duration = 1f, float magnitude = 1f)
        {
            var a = CreateInstance<AbilityDefinition>();
            a.name = id;
            a.abilityId = id;
            a.displayName = name;
            a.effect = effect;
            a.cooldown = cooldown;
            a.damage = damage;
            a.range = range;
            a.dash = dash;
            a.duration = duration;
            a.magnitude = magnitude;
            return a;
        }
    }
}

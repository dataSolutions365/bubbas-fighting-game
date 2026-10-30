using System.Collections.Generic;
using UnityEngine;

namespace Bubba.Abilities
{
    /// Every ability the game knows how to play, looked up by id.
    [CreateAssetMenu(menuName = "Bubba/Ability Library", fileName = "AbilityLibrary")]
    public class AbilityLibrary : ScriptableObject
    {
        public List<AbilityDefinition> abilities = new List<AbilityDefinition>();

        public AbilityDefinition Find(string abilityId)
        {
            foreach (var a in abilities)
            {
                if (a != null && a.abilityId == abilityId) return a;
            }
            return null;
        }

        /// The prototype set, matching firebase/seed/catalog.json, built in
        /// code so the game runs before any assets are created.
        public static AbilityLibrary CreateDefaults()
        {
            var lib = CreateInstance<AbilityLibrary>();
            lib.abilities.Add(AbilityDefinition.Create("roar", "Roar", AbilityEffect.Stagger, 12f, range: 2.5f, duration: 0.9f));
            lib.abilities.Add(AbilityDefinition.Create("pounce", "Pounce", AbilityEffect.Strike, 8f, damage: 70f, range: 1.8f, dash: 2.5f));
            lib.abilities.Add(AbilityDefinition.Create("charge", "Charge", AbilityEffect.Strike, 10f, damage: 90f, range: 1.8f, dash: 4f));
            lib.abilities.Add(AbilityDefinition.Create("thick-hide", "Thick Hide", AbilityEffect.Armor, 20f, duration: 4f, magnitude: 0.5f));
            lib.abilities.Add(AbilityDefinition.Create("maul", "Maul", AbilityEffect.Strike, 15f, damage: 180f, range: 1.6f, dash: 0.8f));
            lib.abilities.Add(AbilityDefinition.Create("chest-beat", "Chest Beat", AbilityEffect.PowerBuff, 18f, duration: 6f, magnitude: 1.3f));
            lib.abilities.Add(AbilityDefinition.Create("frenzy", "Frenzy", AbilityEffect.Haste, 25f, duration: 5f, magnitude: 1.4f));
            return lib;
        }
    }
}

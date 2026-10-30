using UnityEngine;

namespace Bubba.Combat
{
    /// Tuning numbers for one animal. Create one per fighter with
    /// Assets > Create > Bubba > Fighter Stats. fighterId must match the
    /// document id in the Firestore "fighters" catalog.
    [CreateAssetMenu(menuName = "Bubba/Fighter Stats", fileName = "FighterStats")]
    public class FighterStats : ScriptableObject
    {
        public string fighterId = "bear";
        public string displayName = "Grizzly Bear";

        [Header("Body")]
        public float maxHealth = 1200f;
        public float moveSpeed = 3f;
        [Tooltip("Multiplies every hit this fighter lands.")]
        public float power = 1.3f;

        [Header("Light attack (tap)")]
        public float lightDamage = 30f;
        public float lightRange = 1.6f;
        public float lightWindup = 0.12f;
        public float lightRecovery = 0.18f;

        [Header("Heavy attack (swipe / heavy button)")]
        public float heavyDamage = 75f;
        public float heavyRange = 1.9f;
        public float heavyWindup = 0.35f;
        public float heavyRecovery = 0.4f;

        [Header("Defence")]
        [Tooltip("Share of damage that still gets through a block.")]
        [Range(0f, 1f)] public float blockChip = 0.15f;
        [Tooltip("Seconds after starting a block in which a hit is parried.")]
        public float parryWindow = 0.15f;
        public float dodgeDistance = 2f;
        public float dodgeDuration = 0.3f;
        public float dodgeCooldown = 0.8f;
        public float hitStun = 0.3f;

        [Header("Combos")]
        [Tooltip("Seconds after a light hit in which the next light attack chains.")]
        public float comboWindow = 0.35f;
        public int maxLightChain = 3;
    }
}

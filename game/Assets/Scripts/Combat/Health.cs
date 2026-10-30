using System;
using UnityEngine;

namespace Bubba.Combat
{
    public class Health : MonoBehaviour
    {
        public float Max { get; private set; } = 1000f;
        public float Current { get; private set; } = 1000f;
        public bool IsDead => Current <= 0f;

        /// Multiplier on incoming damage, used by armour abilities (1 = normal).
        public float DamageTakenMultiplier { get; set; } = 1f;

        public event Action<float, float> Changed; // current, max
        public event Action Died;

        public void ResetTo(float max)
        {
            Max = max;
            Current = max;
            DamageTakenMultiplier = 1f;
            Changed?.Invoke(Current, Max);
        }

        public float TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f) return 0f;
            float dealt = Mathf.Min(Current, amount * DamageTakenMultiplier);
            Current -= dealt;
            Changed?.Invoke(Current, Max);
            if (IsDead) Died?.Invoke();
            return dealt;
        }
    }
}

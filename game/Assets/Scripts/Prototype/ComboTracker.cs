using Bubba.Combat;
using UnityEngine;

namespace Bubba.Prototype
{
    /// Counts a fighter's run of clean hits for the HUD's COMBO meter.
    /// The run grows with each unblocked hit on the opponent, and ends when
    /// the fighter is hit, or after a short gap with no hits.
    public class ComboTracker
    {
        public const float Gap = 1.6f;
        public const int MeterHits = 10;

        public int Count { get; private set; }
        public int Best { get; private set; }
        public float LastHitAt { get; private set; } = -99f;

        public ComboTracker(FighterController owner, FighterController opponent)
        {
            opponent.GotHit += (attacker, damage, blocked, parried) =>
            {
                if (attacker != owner || blocked || damage <= 0f) return;
                if (Time.time - LastHitAt > Gap) Count = 0;
                Count++;
                if (Count > Best) Best = Count;
                LastHitAt = Time.time;
            };
            owner.GotHit += (attacker, damage, blocked, parried) =>
            {
                if (!blocked) Count = 0;
            };
        }

        /// The current run, or 0 once the gap has passed.
        public int Live => Time.time - LastHitAt <= Gap ? Count : 0;

        /// 0..1 fill for the meter; drains over the gap so you can see the combo running out.
        public float Fill
        {
            get
            {
                int live = Live;
                if (live == 0) return 0f;
                float drain = 1f - (Time.time - LastHitAt) / Gap * 0.35f;
                return Mathf.Clamp01((float)live / MeterHits) * drain;
            }
        }

        public void Reset()
        {
            Count = 0;
            LastHitAt = -99f;
        }
    }
}

using System;
using System.Collections.Generic;
using Bubba.Abilities;
using Bubba.Input;
using UnityEngine;

namespace Bubba.Combat
{
    /// One attack's numbers, after the move (light, heavy, lunge...) is chosen.
    public struct AttackSpec
    {
        public string name;
        public float damage;
        public float range;
        public float windup;
        public float recovery;
        public float stun;
        public float knockback;
        /// Distance the attacker travels forward during the windup.
        public float dash;
    }

    /// Moves one fighter along the arena floor (the X axis), turns input
    /// into attacks, blocks and dodges, and resolves hits against the
    /// opponent. Works with plain capsules; an Animator is optional.
    [RequireComponent(typeof(Health))]
    public class FighterController : MonoBehaviour
    {
        public enum State { Idle, Attacking, Blocking, Dodging, Stunned, KO }

        public FighterStats stats;
        public FighterController opponent;
        [Tooltip("Walk toward the opponent automatically when there's no move input (Easy controls).")]
        public bool assistMovement;
        [Tooltip("Scales the parry window. Hard controls use a smaller value.")]
        public float parryWindowScale = 1f;
        public float arenaHalfWidth = 8f;
        public float minSeparation = 1f;
        public Animator animator;

        public State CurrentState { get; private set; } = State.Idle;
        public Health Health { get; private set; }
        public AbilityRunner Abilities
        {
            get
            {
                if (abilities == null) abilities = GetComponent<AbilityRunner>();
                return abilities;
            }
        }
        public bool InputEnabled { get; set; }
        public float PowerMultiplier { get; set; } = 1f;
        public float SpeedMultiplier { get; set; } = 1f;
        public int FacingSign => opponent == null || opponent.transform.position.x >= transform.position.x ? 1 : -1;
        public float DistanceToOpponent => opponent == null ? float.MaxValue : Mathf.Abs(opponent.transform.position.x - transform.position.x);
        public bool IsAttackWindingUp => CurrentState == State.Attacking && !attackResolved;

        /// attacker, damage dealt, was blocked, was parried
        public event Action<FighterController, float, bool, bool> GotHit;

        private IFighterInput input;
        private AbilityRunner abilities;
        private float stateUntil;
        private float blockStartedAt;
        private float lastDodgeAt = -99f;
        private float dodgeDirection;
        private AttackSpec pendingAttack;
        private float attackHitsAt;
        private bool attackResolved;
        private bool attackLanded;
        private int lightChain;
        private float lastLightLandedAt = -99f;
        private bool lightBuffered;
        private readonly HashSet<string> animatorParams = new HashSet<string>();

        private void Awake()
        {
            Health = GetComponent<Health>();
            input = GetComponent<IFighterInput>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                foreach (var p in animator.parameters) animatorParams.Add(p.name);
            }
            Health.Died += OnDied;
        }

        public void SetInput(IFighterInput source) => input = source;

        public void ResetForRound(Vector3 position)
        {
            transform.position = position;
            Health.ResetTo(stats.maxHealth);
            PowerMultiplier = 1f;
            SpeedMultiplier = 1f;
            CurrentState = State.Idle;
            lightChain = 0;
            lightBuffered = false;
            if (Abilities != null) Abilities.ResetCooldowns();
            Animate("Reset");
            FaceOpponent();
        }

        private void Update()
        {
            if (CurrentState == State.KO) return;

            FighterCommand cmd = InputEnabled && input != null ? input.ReadCommand() : FighterCommand.None;
            cmd.towardOpponent = Mathf.Abs(cmd.move) < 0.01f ? 0 : (Mathf.Sign(cmd.move) == FacingSign ? 1 : -1);

            switch (CurrentState)
            {
                case State.Attacking:
                    TickAttack(cmd);
                    break;
                case State.Dodging:
                    TickDodge();
                    break;
                case State.Stunned:
                    if (Time.time >= stateUntil) CurrentState = State.Idle;
                    break;
                case State.Blocking:
                    if (!cmd.blockHeld) CurrentState = State.Idle;
                    else if (cmd.special >= 0) TrySpecial(cmd.special);
                    break;
                case State.Idle:
                    TickIdle(cmd);
                    break;
            }

            FaceOpponent();
            ClampPosition();
            if (animatorParams.Contains("Block")) animator.SetBool("Block", CurrentState == State.Blocking);
        }

        private void TickIdle(FighterCommand cmd)
        {
            if (cmd.special >= 0 && TrySpecial(cmd.special)) return;
            if (cmd.dodge && Time.time - lastDodgeAt >= stats.dodgeCooldown) { StartDodge(cmd); return; }
            if (cmd.blockHeld) { StartBlock(); return; }
            if (cmd.heavy) { StartAttack(ChooseHeavy(cmd)); return; }
            if (cmd.light || lightBuffered) { StartLight(); return; }

            float move = cmd.move;
            if (assistMovement && Mathf.Abs(move) < 0.01f && DistanceToOpponent > stats.lightRange * 0.85f)
            {
                move = FacingSign;
            }
            transform.position += Vector3.right * (move * stats.moveSpeed * SpeedMultiplier * Time.deltaTime);
            if (animatorParams.Contains("Speed")) animator.SetFloat("Speed", Mathf.Abs(move));
        }

        // ---------- Attacks ----------

        private AttackSpec ChooseHeavy(FighterCommand cmd)
        {
            var heavy = new AttackSpec
            {
                name = "Heavy",
                damage = stats.heavyDamage,
                range = stats.heavyRange,
                windup = stats.heavyWindup,
                recovery = stats.heavyRecovery,
                stun = stats.hitStun * 1.5f,
                knockback = 0.6f,
            };
            if (cmd.towardOpponent > 0)
            {
                // Forward + heavy: a lunge that covers ground.
                heavy.name = "Lunge";
                heavy.damage *= 0.9f;
                heavy.dash = 1.2f;
                heavy.range += 0.4f;
            }
            else if (cmd.towardOpponent < 0)
            {
                // Back + heavy: slow, short-range, but hits hard and stuns long.
                heavy.name = "Uppercut";
                heavy.damage *= 1.35f;
                heavy.windup += 0.12f;
                heavy.range -= 0.3f;
                heavy.stun = stats.hitStun * 2.5f;
                heavy.knockback = 1.2f;
            }
            return heavy;
        }

        private void StartLight()
        {
            lightBuffered = false;
            bool chaining = Time.time - lastLightLandedAt <= stats.comboWindow && lightChain < stats.maxLightChain;
            lightChain = chaining ? lightChain + 1 : 1;
            StartAttack(new AttackSpec
            {
                name = "Light" + lightChain,
                damage = stats.lightDamage * (1f + 0.15f * (lightChain - 1)),
                range = stats.lightRange,
                windup = stats.lightWindup * (chaining ? 0.8f : 1f),
                recovery = stats.lightRecovery,
                stun = stats.hitStun,
                knockback = lightChain == stats.maxLightChain ? 0.8f : 0.15f,
            });
        }

        /// Also used by abilities that strike (pounce, charge, maul).
        public void StartAttack(AttackSpec spec)
        {
            pendingAttack = spec;
            float speed = Mathf.Max(0.1f, SpeedMultiplier);
            attackHitsAt = Time.time + spec.windup / speed;
            stateUntil = attackHitsAt + spec.recovery / speed;
            attackResolved = false;
            attackLanded = false;
            CurrentState = State.Attacking;
            Animate(spec.name.StartsWith("Light") ? "Light" : spec.name);
        }

        private void TickAttack(FighterCommand cmd)
        {
            if (!attackResolved)
            {
                if (pendingAttack.dash > 0f && pendingAttack.windup > 0f)
                {
                    float step = pendingAttack.dash / pendingAttack.windup * SpeedMultiplier * Time.deltaTime;
                    transform.position += Vector3.right * (FacingSign * step);
                }
                if (Time.time >= attackHitsAt) ResolveAttack();
            }
            else if (cmd.light && attackLanded && pendingAttack.name.StartsWith("Light"))
            {
                lightBuffered = true; // chain into the next light as soon as recovery ends
            }

            if (Time.time >= stateUntil) CurrentState = State.Idle;
        }

        private void ResolveAttack()
        {
            attackResolved = true;
            if (opponent == null || DistanceToOpponent > pendingAttack.range) return;
            var spec = pendingAttack;
            spec.damage *= stats.power * PowerMultiplier;
            attackLanded = opponent.ReceiveHit(this, spec);
            if (attackLanded && spec.name.StartsWith("Light")) lastLightLandedAt = Time.time;
            if (!attackLanded) lightChain = 0;
        }

        /// Returns true if the hit connected (blocked hits count; dodged and parried don't).
        public bool ReceiveHit(FighterController attacker, AttackSpec spec)
        {
            if (CurrentState == State.KO || CurrentState == State.Dodging) return false;

            if (CurrentState == State.Blocking)
            {
                if (Time.time - blockStartedAt <= stats.parryWindow * parryWindowScale)
                {
                    attacker.Stun(0.6f);
                    Animate("Parry");
                    GotHit?.Invoke(attacker, 0f, true, true);
                    return false;
                }
                float chip = Health.TakeDamage(spec.damage * stats.blockChip);
                Push(spec.knockback * 0.3f);
                GotHit?.Invoke(attacker, chip, true, false);
                return true;
            }

            float dealt = Health.TakeDamage(spec.damage);
            if (CurrentState != State.KO)
            {
                Stun(spec.stun);
                Push(spec.knockback);
                Animate("Hit");
            }
            GotHit?.Invoke(attacker, dealt, false, false);
            return true;
        }

        public void Stun(float seconds)
        {
            if (CurrentState == State.KO) return;
            CurrentState = State.Stunned;
            stateUntil = Time.time + seconds;
            lightChain = 0;
            lightBuffered = false;
        }

        private void Push(float distance)
        {
            transform.position += Vector3.right * (-FacingSign * distance);
        }

        // ---------- Defence ----------

        private void StartBlock()
        {
            CurrentState = State.Blocking;
            blockStartedAt = Time.time;
        }

        private void StartDodge(FighterCommand cmd)
        {
            CurrentState = State.Dodging;
            lastDodgeAt = Time.time;
            stateUntil = Time.time + stats.dodgeDuration;
            // Dodge away unless the stick is held toward the opponent (Medium: roll through).
            dodgeDirection = cmd.towardOpponent > 0 ? FacingSign : -FacingSign;
            Animate("Dodge");
        }

        private void TickDodge()
        {
            float speed = stats.dodgeDistance / Mathf.Max(0.05f, stats.dodgeDuration);
            transform.position += Vector3.right * (dodgeDirection * speed * Time.deltaTime);
            if (Time.time >= stateUntil) CurrentState = State.Idle;
        }

        // ---------- Specials ----------

        private bool TrySpecial(int index)
        {
            if (Abilities == null || !Abilities.TryActivate(index)) return false;
            if (CurrentState == State.Blocking) CurrentState = State.Idle;
            return true;
        }

        // ---------- Helpers ----------

        private void OnDied()
        {
            CurrentState = State.KO;
            Animate("KO");
        }

        private void FaceOpponent()
        {
            if (opponent == null) return;
            transform.rotation = Quaternion.Euler(0f, FacingSign > 0 ? 90f : -90f, 0f);
        }

        private void ClampPosition()
        {
            Vector3 p = transform.position;
            p.x = Mathf.Clamp(p.x, -arenaHalfWidth, arenaHalfWidth);
            if (opponent != null)
            {
                float ox = opponent.transform.position.x;
                if (Mathf.Abs(p.x - ox) < minSeparation)
                {
                    p.x = ox - FacingSign * minSeparation;
                }
            }
            transform.position = p;
        }

        private void Animate(string trigger)
        {
            if (animator != null && animatorParams.Contains(trigger)) animator.SetTrigger(trigger);
        }
    }
}

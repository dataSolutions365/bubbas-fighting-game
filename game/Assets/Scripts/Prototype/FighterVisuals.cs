using Bubba.Combat;
using UnityEngine;

namespace Bubba.Prototype
{
    /// Makes the stand-in capsule fighters readable: an arm that shoots out
    /// on each attack, a shield while blocking, flashes and floating numbers
    /// on hits, a squash on dodges, a wobble when stunned and a fall on KO.
    /// Not needed once real animal models with animations are in.
    [RequireComponent(typeof(FighterController))]
    public class FighterVisuals : MonoBehaviour
    {
        public Renderer body;
        public Color baseColor = Color.gray;

        private static readonly Color LightArm = new Color(0.95f, 0.85f, 0.7f);
        private static readonly Color HeavyArm = new Color(1f, 0.55f, 0.1f);
        private static readonly Color SpecialArm = new Color(0.7f, 0.35f, 1f);
        private static readonly Color HitFlash = new Color(1f, 0.25f, 0.2f);
        private static readonly Color BlockColor = new Color(0.3f, 0.6f, 1f);
        private static readonly Color ParryColor = new Color(1f, 0.9f, 0.2f);

        private FighterController fighter;
        private Transform bodyT;
        private Vector3 bodyPos;
        private Vector3 bodyScale;
        private Transform arm;
        private Renderer armRenderer;
        private Transform shield;
        private float attackStartedAt;
        private float flashUntil;
        private Color flashColor;

        private void Awake()
        {
            fighter = GetComponent<FighterController>();
            fighter.AttackStarted += OnAttackStarted;
            fighter.AttackResolved += OnAttackResolved;
            fighter.GotHit += OnGotHit;
        }

        private void Start()
        {
            if (body != null)
            {
                bodyT = body.transform;
                bodyPos = bodyT.localPosition;
                bodyScale = bodyT.localScale;
            }

            arm = MakePart("Attack arm", PrimitiveType.Cube, LightArm).transform;
            armRenderer = arm.GetComponent<Renderer>();
            arm.gameObject.SetActive(false);

            shield = MakePart("Block shield", PrimitiveType.Cube, BlockColor).transform;
            shield.localPosition = new Vector3(0f, 1.1f, 0.7f);
            shield.localScale = new Vector3(1.2f, 1.6f, 0.08f);
            shield.gameObject.SetActive(false);
        }

        private GameObject MakePart(string partName, PrimitiveType type, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = partName;
            go.transform.SetParent(transform, false);
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().material.color = color;
            return go;
        }

        private void Update()
        {
            var state = fighter.CurrentState;
            UpdateArm(state);
            shield.gameObject.SetActive(state == FighterController.State.Blocking);
            UpdateBody(state);
        }

        private void UpdateArm(FighterController.State state)
        {
            if (state != FighterController.State.Attacking)
            {
                arm.gameObject.SetActive(false);
                return;
            }

            var spec = fighter.CurrentAttack;
            bool light = spec.name.StartsWith("Light");
            float thickness = light ? 0.22f : 0.34f;
            float reach = Mathf.Max(0.6f, spec.range - 0.2f);
            float now = Time.time;
            float hit = fighter.AttackHitTime;
            float length;

            if (now < hit)
            {
                // Wind-up: arm pulled in, growing slightly as the hit approaches.
                float p = Mathf.InverseLerp(attackStartedAt, hit, now);
                length = Mathf.Lerp(0.2f, 0.45f, p);
            }
            else
            {
                // Impact, then retract during recovery.
                float q = Mathf.InverseLerp(hit, fighter.AttackEndTime, now);
                length = Mathf.Lerp(reach, 0.3f, q * q);
            }

            arm.gameObject.SetActive(true);
            arm.localScale = new Vector3(thickness, thickness, length);
            arm.localPosition = new Vector3(0f, light ? 1.25f : 1.05f, 0.3f + length / 2f);
        }

        private void UpdateBody(FighterController.State state)
        {
            if (bodyT == null) return;

            Vector3 pos = bodyPos;
            Vector3 scale = bodyScale;
            Quaternion rot = Quaternion.identity;

            switch (state)
            {
                case FighterController.State.Dodging:
                    scale = new Vector3(bodyScale.x * 1.15f, bodyScale.y * 0.6f, bodyScale.z * 1.15f);
                    pos.y = bodyPos.y * 0.6f;
                    break;
                case FighterController.State.Stunned:
                    rot = Quaternion.Euler(-12f + Mathf.Sin(Time.time * 40f) * 6f, 0f, 0f);
                    break;
                case FighterController.State.KO:
                    rot = Quaternion.Euler(-90f, 0f, 0f);
                    pos.y = 0.5f;
                    break;
                case FighterController.State.Attacking:
                    if (Time.time >= fighter.AttackHitTime) rot = Quaternion.Euler(10f, 0f, 0f);
                    break;
            }

            bodyT.localPosition = pos;
            bodyT.localScale = scale;
            bodyT.localRotation = rot;
            body.material.color = Time.time < flashUntil ? flashColor : baseColor;
        }

        private void OnAttackStarted(AttackSpec spec)
        {
            attackStartedAt = Time.time;
            if (armRenderer == null) return;
            bool light = spec.name.StartsWith("Light");
            bool heavy = spec.name == "Heavy" || spec.name == "Lunge" || spec.name == "Uppercut";
            armRenderer.material.color = light ? LightArm : heavy ? HeavyArm : SpecialArm;
            if (!light && spec.name != "Heavy")
            {
                CombatText.Add(Head(), spec.name + "!", heavy ? HeavyArm : SpecialArm, 1.1f);
            }
        }

        private void OnAttackResolved(AttackSpec spec, bool landed)
        {
            if (landed || fighter.opponent == null) return;
            if (fighter.opponent.CurrentState == FighterController.State.Dodging)
            {
                CombatText.Add(fighter.opponent.transform.position + Vector3.up * 2.3f, "Dodged!", Color.white);
            }
            else if (fighter.DistanceToOpponent > spec.range)
            {
                CombatText.Add(Head(), "Miss", new Color(0.8f, 0.8f, 0.8f), 0.8f);
            }
        }

        private void OnGotHit(FighterController attacker, float damage, bool blocked, bool parried)
        {
            if (parried)
            {
                Flash(ParryColor);
                CombatText.Add(Head(), "PARRY!", ParryColor, 1.4f);
            }
            else if (blocked)
            {
                Flash(BlockColor);
                CombatText.Add(Head(), $"Blocked -{Mathf.RoundToInt(damage)}", BlockColor, 0.9f);
            }
            else
            {
                Flash(HitFlash);
                CombatText.Add(Head(), $"-{Mathf.RoundToInt(damage)}", HitFlash, damage >= 60f ? 1.4f : 1.1f);
            }
        }

        private void Flash(Color color)
        {
            flashColor = color;
            flashUntil = Time.time + 0.12f;
        }

        private Vector3 Head() => transform.position + Vector3.up * 2.3f;
    }
}

using Bubba.Combat;
using UnityEngine;

namespace Bubba.Prototype
{
    /// Draws a fighter as the hand-drawn boxing monkey, built from the
    /// cut-out pieces in Resources/Monkey/body-parts. The arms and head turn
    /// around the shoulder and neck pivots, so the monkey can jab, throw a
    /// big punch (swapping to the punch drawing), raise his gloves to block,
    /// duck, wobble when stunned and fall over on a KO.
    ///
    /// The pieces are full-size 660x971 canvases that line up when stacked.
    /// Pixel coordinates below are measured from the top-left of that canvas.
    public class MonkeySpriteRig : MonoBehaviour
    {
        private const float W = 660f, H = 971f;
        // Where the feet touch the ground, as a fraction of the canvas.
        private const float FeetX = 0.55f, FeetY = 0.968f;
        private static readonly Vector2 ShoulderR = new Vector2(446, 300);
        private static readonly Vector2 ShoulderL = new Vector2(288, 300);
        private static readonly Vector2 Neck = new Vector2(362, 250);
        private static readonly Vector2 FaceR = new Vector2(418, 178);
        private static readonly Vector2 FaceL = new Vector2(312, 184);
        // The punch drawing is a separate 657x1199 picture; feet at about (390, 1095).
        private static readonly Vector2 PunchSize = new Vector2(657, 1199);
        private static readonly Vector2 PunchFeet = new Vector2(390, 1095);

        /// Height of the monkey from feet to the top of his head, in world units.
        public float height = 2.3f;

        private FighterController fighter;
        private Transform flip;
        private Transform armR, armL, head, gloveR, gloveL;
        private SpriteRenderer[] guardRenderers;
        private SpriteRenderer punchPose;
        private Color tint = Color.white;
        private float hitAt = -99f;

        private float Ppu => H * FeetY / height;

        /// Builds the rig under the fighter, or returns null if the art isn't in the project.
        public static MonkeySpriteRig TryCreate(FighterController fighter, Color tint)
        {
            var body = Load("Monkey/body-parts/body");
            var headTex = Load("Monkey/body-parts/head");
            var armRTex = Load("Monkey/body-parts/armR");
            var armLTex = Load("Monkey/body-parts/armL");
            var gloveRTex = Load("Monkey/body-parts/gloveR");
            var gloveLTex = Load("Monkey/body-parts/gloveL");
            var punchTex = Load("Monkey/monkey-punch-colored");
            if (body == null || headTex == null || armRTex == null || armLTex == null)
            {
                Debug.LogWarning("Monkey art not found in Resources/Monkey; using the capsule fighter instead.");
                return null;
            }

            var go = new GameObject("Monkey (sprite rig)");
            go.transform.SetParent(fighter.transform, false);
            var rig = go.AddComponent<MonkeySpriteRig>();
            rig.fighter = fighter;
            rig.tint = tint;
            rig.Build(body, headTex, armRTex, armLTex, gloveRTex, gloveLTex, punchTex);
            return rig;
        }

        private static Texture2D Load(string path) => Resources.Load<Texture2D>(path);

        private void Build(Texture2D body, Texture2D headTex, Texture2D armRTex, Texture2D armLTex,
            Texture2D gloveRTex, Texture2D gloveLTex, Texture2D punchTex)
        {
            flip = new GameObject("Flip").transform;
            flip.SetParent(transform, false);

            // Draw order: back arm, body, head, front arm, gloves.
            armL = Part("Arm L", armLTex, new Vector2(W, H), ShoulderL, ShoulderL, 1);
            Part("Body", body, new Vector2(W, H), new Vector2(FeetX * W, FeetY * H), new Vector2(FeetX * W, FeetY * H), 2);
            head = Part("Head", headTex, new Vector2(W, H), Neck, Neck, 3);
            armR = Part("Arm R", armRTex, new Vector2(W, H), ShoulderR, ShoulderR, 4);
            if (gloveLTex != null) gloveL = Part("Glove L", gloveLTex, new Vector2(112, 132), new Vector2(56, 66), FaceL, 6);
            if (gloveRTex != null) gloveR = Part("Glove R", gloveRTex, new Vector2(80, 122), new Vector2(40, 61), FaceR, 6);
            if (gloveL != null) gloveL.gameObject.SetActive(false);
            if (gloveR != null) gloveR.gameObject.SetActive(false);
            guardRenderers = flip.GetComponentsInChildren<SpriteRenderer>(true);

            if (punchTex != null)
            {
                // The punch drawing is taller in pixels; scale it to the same height.
                float ppu = PunchFeet.y / height;
                var t = Part("Punch pose", punchTex, PunchSize, PunchFeet, new Vector2(FeetX * W, FeetY * H), 5, ppu);
                punchPose = t.GetComponentInChildren<SpriteRenderer>();
                t.gameObject.SetActive(false);
            }
            ApplyColor(tint);
        }

        /// Makes one piece: a pivot transform placed at `placeAt` (canvas pixels) that
        /// rotates around the sprite's `pivot` (in the piece's own pixels).
        private Transform Part(string partName, Texture2D tex, Vector2 nativeSize, Vector2 pivot, Vector2 placeAt,
            int order, float ppuOverride = 0f)
        {
            float ppu = ppuOverride > 0f ? ppuOverride : Ppu;
            var holder = new GameObject(partName).transform;
            holder.SetParent(flip, false);
            holder.localPosition = new Vector3((placeAt.x - FeetX * W) / ppu, (FeetY * H - placeAt.y) / ppu, 0f);
            if (ppuOverride > 0f) holder.localPosition = Vector3.zero;

            // Unity may resize textures it imports (e.g. to a power of two), so the sprite
            // is built at whatever size arrived and stretched back to the drawing's shape.
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(pivot.x / nativeSize.x, 1f - pivot.y / nativeSize.y),
                ppu * tex.height / nativeSize.y);
            var child = new GameObject("Sprite");
            child.transform.SetParent(holder, false);
            float aspectFix = nativeSize.x * tex.height / (nativeSize.y * tex.width);
            child.transform.localScale = new Vector3(aspectFix, 1f, 1f);
            var sr = child.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return holder;
        }

        public void OnHit() => hitAt = Time.time;

        /// Called every frame by FighterVisuals.
        public void Pose(FighterController.State state, float attackStartedAt, Color? flash)
        {
            // Always face the camera; mirror to face the opponent (the drawing faces right).
            transform.rotation = Quaternion.identity;
            flip.localScale = new Vector3(fighter.FacingSign, 1f, 1f);

            float t = Time.time;
            float armRAngle = Mathf.Sin(t * 3f) * 3f;
            float armLAngle = -Mathf.Sin(t * 3f) * 3f;
            float headAngle = 0f;
            Vector3 rigScale = Vector3.one;
            float rigAngle = 0f;
            float bob = Mathf.Sin(t * 4f) * 0.02f;
            bool guard = false;
            bool punch = false;

            switch (state)
            {
                case FighterController.State.Attacking:
                    PoseAttack(attackStartedAt, ref armRAngle, ref rigAngle, ref punch);
                    break;
                case FighterController.State.Blocking:
                    guard = true;
                    break;
                case FighterController.State.Dodging:
                    rigScale = new Vector3(1.1f, 0.75f, 1f);
                    rigAngle = 10f;
                    bob = 0f;
                    break;
                case FighterController.State.Stunned:
                    headAngle = Mathf.Sin(t * 30f) * 12f;
                    rigAngle = 6f + Mathf.Sin(t * 18f) * 3f;
                    break;
                case FighterController.State.KO:
                    rigAngle = 80f;
                    bob = 0f;
                    break;
            }

            // Head snaps back for a moment after a hit.
            float sinceHit = t - hitAt;
            if (sinceHit < 0.3f) headAngle += 18f * (1f - sinceHit / 0.3f);

            armR.localRotation = Quaternion.Euler(0f, 0f, armRAngle);
            armL.localRotation = Quaternion.Euler(0f, 0f, armLAngle);
            head.localRotation = Quaternion.Euler(0f, 0f, headAngle);
            flip.localPosition = new Vector3(0f, bob, 0f);
            flip.localRotation = Quaternion.Euler(0f, 0f, rigAngle);
            transform.localScale = rigScale;

            // Gloves-up guard: the arms fold toward the camera, so only the gloves show.
            armR.gameObject.SetActive(!guard && !punch);
            armL.gameObject.SetActive(!guard && !punch);
            if (gloveR != null) gloveR.gameObject.SetActive(guard);
            if (gloveL != null) gloveL.gameObject.SetActive(guard);
            SetGuardBodyVisible(!punch);
            if (punchPose != null) punchPose.transform.parent.gameObject.SetActive(punch);

            ApplyColor(flash.HasValue ? tint * flash.Value : tint);
        }

        private void PoseAttack(float startedAt, ref float armRAngle, ref float rigAngle, ref bool punch)
        {
            var spec = fighter.CurrentAttack;
            float now = Time.time;
            float hit = fighter.AttackHitTime;
            bool light = spec.name.StartsWith("Light");
            bool uppercut = spec.name == "Uppercut";
            // From the shoulder the arm hangs about 60 degrees below level; +62 makes it point at the opponent.
            float reachAngle = uppercut ? 95f : 62f;

            if (now < hit)
            {
                float p = Mathf.InverseLerp(startedAt, hit, now);
                armRAngle = Mathf.Lerp(0f, light ? -12f : -25f, p);
                rigAngle = Mathf.Lerp(0f, light ? 3f : 8f, p); // lean back to load the punch
                return;
            }

            float q = Mathf.InverseLerp(hit, fighter.AttackEndTime, now);
            if (!light && !uppercut && q < 0.6f && punchPose != null)
            {
                punch = true; // big punches use the full punch drawing
                rigAngle = -6f;
                return;
            }
            armRAngle = Mathf.Lerp(reachAngle, 0f, q * q);
            rigAngle = Mathf.Lerp(light ? -4f : -8f, 0f, q);
        }

        private void SetGuardBodyVisible(bool visible)
        {
            foreach (var sr in guardRenderers)
            {
                if (sr == null) continue;
                var holder = sr.transform.parent;
                if (holder == armR || holder == armL || holder == gloveR || holder == gloveL) continue;
                sr.enabled = visible;
            }
        }

        private void ApplyColor(Color c)
        {
            if (guardRenderers != null) foreach (var sr in guardRenderers) if (sr != null) sr.color = c;
            if (punchPose != null) punchPose.color = c;
        }
    }
}

using Bubba.AI;
using Bubba.Backend;
using Bubba.Combat;
using Bubba.Input;
using UnityEngine;

namespace Bubba.Prototype
{
    /// Bare-bones menus, shop and fight HUD drawn with Unity's immediate-mode
    /// GUI, so the prototype needs no UI prefabs. Replace with a proper
    /// Canvas UI once the fighting feels right.
    public class PrototypeHud : MonoBehaviour
    {
        public RoundManager rounds;

        private ControlScheme scheme;
        private AiDifficulty aiDifficulty = AiDifficulty.Medium;
        private bool showShop;
        private Vector2 shopScroll;
        private float u; // one "unit" = 1/720 of the screen height
        private GUIStyle label, small, big, button, panel;

        private static readonly string[] SchemeNames = { "Easy (tap)", "Medium (pad)" };
        private static readonly string[] AiNames = { "Easy", "Medium", "Hard" };

        private void Start()
        {
            scheme = (ControlScheme)Mathf.Clamp(PlayerPrefs.GetInt(PlayerInputRouter.SchemePrefKey, 0), 0, 1);
        }

        private void OnGUI()
        {
            u = Screen.height / 720f;
            BuildStyles();
            var session = GameSession.Instance;

            switch (rounds.CurrentPhase)
            {
                case RoundManager.Phase.Menu:
                    if (showShop) DrawShop(session);
                    else DrawMenu(session);
                    break;
                case RoundManager.Phase.Starting:
                    GUI.Label(Center(600, 80), "Starting match...", big);
                    break;
                default:
                    DrawFight();
                    if (rounds.CurrentPhase == RoundManager.Phase.MatchOver) DrawResult(session);
                    break;
            }
        }

        // ---------- Menu ----------

        private void DrawMenu(GameSession session)
        {
            GUILayout.BeginArea(Center(620, 640), panel);
            GUILayout.Label("Bubba's Fighting Game", big);
            GUILayout.Label(session.Status, small);

            if (session.Ready)
            {
                var profile = session.Profile;
                GUILayout.Label($"Points: {profile.fightPoints}", label);

                GUILayout.Label("Your fighter", small);
                GUILayout.BeginHorizontal();
                foreach (var stats in rounds.roster)
                {
                    bool selected = profile.selectedFighter == stats.fighterId;
                    GUI.enabled = !session.Busy && !selected;
                    if (GUILayout.Button(selected ? $"> {stats.displayName} <" : stats.displayName, button))
                    {
                        _ = session.SelectFighter(stats.fighterId);
                    }
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();

                GUILayout.Label("Controls", small);
                scheme = (ControlScheme)GUILayout.Toolbar((int)scheme, SchemeNames, button);
                GUILayout.Label("Opponent", small);
                aiDifficulty = (AiDifficulty)GUILayout.Toolbar((int)aiDifficulty, AiNames, button);

                GUILayout.Space(12 * u);
                GUI.enabled = !session.Busy;
                if (GUILayout.Button("FIGHT", button, GUILayout.Height(70 * u)))
                {
                    rounds.BeginMatch(scheme, aiDifficulty);
                }
                if (GUILayout.Button("Ability shop", button))
                {
                    showShop = true;
                }
                GUI.enabled = true;
            }
            GUILayout.EndArea();
        }

        private void DrawShop(GameSession session)
        {
            var profile = session.Profile;
            GUILayout.BeginArea(Center(760, 660), panel);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Ability shop    Points: {profile.fightPoints}", big);
            if (GUILayout.Button("Back", button, GUILayout.Width(140 * u))) showShop = false;
            GUILayout.EndHorizontal();
            GUILayout.Label(session.Status, small);
            GUILayout.Label($"Equipped: {string.Join(", ", profile.equippedAbilities)} (max 2)", small);

            shopScroll = GUILayout.BeginScrollView(shopScroll);
            foreach (var a in session.Catalog)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.BeginVertical();
                GUILayout.Label($"{a.name}  ({a.cost} pts){(a.premiumOnly ? "  PREMIUM" : "")}", label);
                GUILayout.Label(a.description, small);
                GUILayout.EndVertical();

                bool owned = profile.ownedAbilities.Contains(a.id);
                bool equipped = profile.equippedAbilities.Contains(a.id);
                GUI.enabled = !session.Busy;
                if (!owned)
                {
                    GUI.enabled &= profile.fightPoints >= a.cost && (!a.premiumOnly || profile.isPremium);
                    if (GUILayout.Button("Buy", button, GUILayout.Width(160 * u))) _ = session.Buy(a.id);
                }
                else if (a.UsableBy(profile.selectedFighter))
                {
                    if (GUILayout.Button(equipped ? "Unequip" : "Equip", button, GUILayout.Width(160 * u)))
                    {
                        _ = session.ToggleEquip(a.id);
                    }
                }
                else
                {
                    GUILayout.Label("Other fighter", small, GUILayout.Width(160 * u));
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ---------- Fight ----------

        private void DrawFight()
        {
            DrawHealth(rounds.player, new Rect(20 * u, 20 * u, Screen.width * 0.38f, 28 * u), false);
            DrawHealth(rounds.enemy, new Rect(Screen.width - Screen.width * 0.38f - 20 * u, 20 * u, Screen.width * 0.38f, 28 * u), true);
            GUI.Label(new Rect(Screen.width / 2f - 60 * u, 12 * u, 120 * u, 44 * u), Mathf.CeilToInt(Mathf.Max(0, rounds.TimeLeft)).ToString(), big);
            GUI.Label(new Rect(Screen.width / 2f - 100 * u, 52 * u, 200 * u, 30 * u), $"{rounds.PlayerRounds} - {rounds.EnemyRounds}", label);
            if (!string.IsNullOrEmpty(rounds.Banner)) GUI.Label(Center(800, 120), rounds.Banner, big);

            if (rounds.CurrentPhase != RoundManager.Phase.Fighting) return;
            var router = rounds.player.GetComponent<PlayerInputRouter>();
            var activeScheme = router != null ? router.scheme : scheme;

            if (activeScheme == ControlScheme.Easy)
            {
                GUI.Label(new Rect(20 * u, Screen.height - 50 * u, Screen.width * 0.6f, 40 * u),
                    "Tap: attack   Swipe toward: heavy   Swipe away: dodge   Hold: block", small);
            }
            else
            {
                DrawButton(TouchLayout.Light, "Light");
                DrawButton(TouchLayout.Heavy, "Heavy");
                DrawButton(TouchLayout.Block, "Block");
                DrawButton(TouchLayout.Dodge, "Dodge");
                if (router != null && router.Pad.StickActive)
                {
                    float r = Screen.height * router.Pad.stickRadiusFraction;
                    Vector2 o = router.Pad.StickOrigin;
                    GUI.Box(new Rect(o.x - r, Screen.height - o.y - r, r * 2, r * 2), "");
                    Vector2 k = o + router.Pad.Stick * r;
                    GUI.Box(new Rect(k.x - r * 0.35f, Screen.height - k.y - r * 0.35f, r * 0.7f, r * 0.7f), "");
                }
                else
                {
                    GUI.Label(new Rect(20 * u, Screen.height - 50 * u, Screen.width * 0.4f, 40 * u),
                        "Touch here and drag to move", small);
                }
            }

            var runner = rounds.player.Abilities;
            for (int i = 0; i < runner.equipped.Count && i < 2; i++)
            {
                float cd = runner.CooldownRemaining(i);
                string text = runner.equipped[i].displayName + (cd > 0f ? $"\n{cd:0.0}s" : "");
                GUI.enabled = cd <= 0f;
                DrawButton(TouchLayout.Special(i, activeScheme), text);
                GUI.enabled = true;
            }
        }

        private void DrawResult(GameSession session)
        {
            GUILayout.BeginArea(Center(560, 300), panel);
            GUILayout.Label(rounds.Banner, big);
            GUILayout.Label(rounds.LastReport == null ? "Saving result..." : session.Status, label);
            if (session.Profile != null) GUILayout.Label($"Points: {session.Profile.fightPoints}", label);
            GUI.enabled = rounds.LastReport != null || !session.Busy;
            if (GUILayout.Button("Continue", button, GUILayout.Height(60 * u))) rounds.ReturnToMenu();
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        // ---------- helpers ----------

        private void DrawHealth(FighterController f, Rect r, bool rightToLeft)
        {
            float frac = f.Health.Max > 0 ? f.Health.Current / f.Health.Max : 0f;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.Lerp(new Color(0.85f, 0.2f, 0.15f), new Color(0.95f, 0.8f, 0.2f), frac);
            float w = r.width * frac;
            GUI.DrawTexture(new Rect(rightToLeft ? r.xMax - w : r.x, r.y, w, r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x, r.yMax, r.width, 30 * u), f.stats != null ? f.stats.displayName : f.name, small);
        }

        private void DrawButton(Rect layoutRect, string text)
        {
            GUI.Box(TouchLayout.ToGui(layoutRect), text, button);
        }

        private Rect Center(float w, float h)
        {
            w = Mathf.Min(w * u, Screen.width - 20);
            h *= u;
            return new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        }

        private void BuildStyles()
        {
            label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(24 * u), wordWrap = true };
            small = new GUIStyle(label) { fontSize = Mathf.RoundToInt(18 * u) };
            big = new GUIStyle(label) { fontSize = Mathf.RoundToInt(40 * u), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(22 * u), wordWrap = true };
            panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(16, 16, 16, 16) };
        }
    }
}

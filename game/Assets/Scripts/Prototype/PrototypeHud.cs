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
        private GUIStyle cardName, lockTag, barName, badgeText, timeLabel, comboLabel;
        private string focusedId;
        private ComboTracker playerCombo, enemyCombo;
        private int comboRound = -1;

        private static readonly string[] SchemeNames = { "Easy (tap)", "Medium (pad)" };
        private static readonly string[] AiNames = { "Easy", "Medium", "Hard" };

        private void Start()
        {
            scheme = (ControlScheme)Mathf.Clamp(PlayerPrefs.GetInt(PlayerInputRouter.SchemePrefKey, 0), 0, 1);
            playerCombo = new ComboTracker(rounds.player, rounds.enemy);
            enemyCombo = new ComboTracker(rounds.enemy, rounds.player);
        }

        private void Update()
        {
            // Each round starts with empty combo meters.
            if (playerCombo != null && rounds.RoundNumber != comboRound)
            {
                comboRound = rounds.RoundNumber;
                playerCombo.Reset();
                enemyCombo.Reset();
            }
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
                    DrawWorldLabels();
                    DrawControlsCard();
                    if (rounds.CurrentPhase == RoundManager.Phase.MatchOver) DrawResult(session);
                    break;
            }
        }

        // ---------- Menu ----------

        private void DrawMenu(GameSession session)
        {
            GUILayout.BeginArea(Center(1180, 690), panel);
            GUILayout.Label("CHOOSE YOUR FIGHTER", big);
            GUILayout.Label(session.Status, small);

            if (session.Ready)
            {
                var profile = session.Profile;
                if (focusedId == null) focusedId = profile.selectedFighter;
                GUILayout.Label($"Points: {profile.fightPoints}", label);

                Rect grid = GUILayoutUtility.GetRect(100, 320 * u, GUILayout.ExpandWidth(true));
                DrawSelectGrid(session, grid);

                var focus = RosterCatalog.Find(focusedId);
                if (focus != null)
                {
                    bool playable = IsPlayable(focus.id);
                    GUILayout.Label($"{focus.name}, {focus.animal}.  Special idea: {focus.abilityIdea}" +
                        (playable ? "" : "\nNeeds a full-body drawing before this fighter can play."), small);
                }

                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();
                GUILayout.Label("Controls", small);
                scheme = (ControlScheme)GUILayout.Toolbar((int)scheme, SchemeNames, button);
                GUILayout.EndVertical();
                GUILayout.Space(16 * u);
                GUILayout.BeginVertical();
                GUILayout.Label("Opponent", small);
                aiDifficulty = (AiDifficulty)GUILayout.Toolbar((int)aiDifficulty, AiNames, button);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();

                GUILayout.Space(10 * u);
                GUILayout.BeginHorizontal();
                GUI.enabled = !session.Busy;
                if (GUILayout.Button("FIGHT", button, GUILayout.Height(64 * u)))
                {
                    rounds.BeginMatch(scheme, aiDifficulty);
                }
                if (GUILayout.Button("Ability shop", button, GUILayout.Height(64 * u), GUILayout.Width(260 * u)))
                {
                    showShop = true;
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }

        /// The 6 x 2 grid of portraits from the drawing. Fighters with art can be
        /// picked; the rest show their notes and a "needs drawing" tag.
        private void DrawSelectGrid(GameSession session, Rect area)
        {
            const int cols = 6;
            var all = RosterCatalog.All;
            int rows = Mathf.CeilToInt(all.Count / (float)cols);
            float gap = 8 * u;
            float cw = (area.width - gap * (cols - 1)) / cols;
            float ch = (area.height - gap * (rows - 1)) / rows;

            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                var r = new Rect(area.x + (i % cols) * (cw + gap), area.y + (i / cols) * (ch + gap), cw, ch);
                bool playable = IsPlayable(e.id);
                bool selected = session.Profile.selectedFighter == e.id;
                bool focused = focusedId == e.id;

                // Card: paper-coloured, with a gold frame on the picked fighter.
                Fill(r, selected ? new Color(1f, 0.8f, 0.2f) : focused ? new Color(0.8f, 0.8f, 0.85f) : new Color(0.25f, 0.25f, 0.28f));
                var inner = new Rect(r.x + 3 * u, r.y + 3 * u, r.width - 6 * u, r.height - 6 * u);
                Fill(inner, new Color(0.96f, 0.93f, 0.86f));

                var tex = RosterCatalog.Portrait(e.id);
                var pic = new Rect(inner.x + 4 * u, inner.y + 4 * u, inner.width - 8 * u, inner.height - 34 * u);
                if (tex != null)
                {
                    GUI.color = playable ? Color.white : new Color(1f, 1f, 1f, 0.6f);
                    GUI.DrawTexture(pic, tex, ScaleMode.ScaleToFit);
                    GUI.color = Color.white;
                }
                GUI.Label(new Rect(inner.x, inner.yMax - 30 * u, inner.width, 28 * u), e.name, cardName);
                if (!playable)
                {
                    var tag = new Rect(inner.x, inner.y, inner.width, 24 * u);
                    Fill(tag, new Color(1f, 1f, 1f, 0.75f));
                    GUI.Label(tag, "needs drawing", lockTag);
                }

                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    focusedId = e.id;
                    if (playable && !selected && !session.Busy) _ = session.SelectFighter(e.id);
                }
            }
        }

        private bool IsPlayable(string id) => rounds.roster.Exists(s => s != null && s.fighterId == id);

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
            DrawTopBar();
            DrawComboMeters();
            if (!string.IsNullOrEmpty(rounds.Banner)) GUI.Label(Center(800, 120), rounds.Banner, big);

            if (rounds.CurrentPhase != RoundManager.Phase.Fighting) return;
            var router = rounds.player.GetComponent<PlayerInputRouter>();
            var activeScheme = router != null ? router.scheme : scheme;

            if (activeScheme != ControlScheme.Easy)
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
                    GUI.Label(new Rect(20 * u, Screen.height - 130 * u, Screen.width * 0.4f, 40 * u),
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

        /// "YOU" / "CPU" over the fighters, plus floating hit numbers.
        private void DrawWorldLabels()
        {
            var cam = Camera.main;
            if (cam == null) return;
            DrawWorldText(cam, rounds.player.transform.position + Vector3.up * 2.6f, "YOU", new Color(1f, 0.85f, 0.4f), 0.8f, 1f);
            DrawWorldText(cam, rounds.enemy.transform.position + Vector3.up * 2.6f, "CPU", new Color(0.7f, 0.8f, 1f), 0.8f, 1f);

            CombatText.Prune();
            foreach (var e in CombatText.Entries)
            {
                float age = (Time.time - e.bornAt) / CombatText.Lifetime;
                DrawWorldText(cam, e.world + Vector3.up * (age * 0.8f), e.text, e.color, e.size, 1f - age * age);
            }
        }

        private void DrawWorldText(Camera cam, Vector3 world, string text, Color color, float size, float alpha)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f) return;
            var style = new GUIStyle(big) { fontSize = Mathf.RoundToInt(30 * u * size) };
            var r = new Rect(sp.x - 150 * u, Screen.height - sp.y - 25 * u, 300 * u, 50 * u);
            var shadow = new Rect(r.x + 2, r.y + 2, r.width, r.height);
            GUI.color = new Color(0f, 0f, 0f, alpha * 0.8f);
            GUI.Label(shadow, text, style);
            GUI.color = new Color(color.r, color.g, color.b, alpha);
            GUI.Label(r, text, style);
            GUI.color = Color.white;
        }

        /// A small always-visible reminder of the controls during a fight.
        private void DrawControlsCard()
        {
            var router = rounds.player.GetComponent<PlayerInputRouter>();
            var activeScheme = router != null ? router.scheme : scheme;
            string text;
            if (Application.isEditor)
            {
                text = "KEYBOARD\nA / D   move\nJ   punch (tap fast to combo)\nK   heavy  (toward + K = lunge, away + K = uppercut)\nhold L   block (just before a hit = PARRY)\nSpace   dodge\n1 / 2   special abilities";
            }
            else if (activeScheme == ControlScheme.Easy)
            {
                text = "Tap: punch   Swipe toward: heavy   Swipe away: dodge\nHold: block   Buttons: specials";
            }
            else
            {
                text = "Left side: drag to move\nForward + Heavy = lunge   Back + Heavy = uppercut\nBlock just before a hit to PARRY";
            }

            var style = new GUIStyle(small) { alignment = TextAnchor.UpperLeft };
            float w = Screen.width * 0.42f;
            float h = style.CalcHeight(new GUIContent(text), w - 20 * u) + 20 * u;
            var r = new Rect(20 * u, 120 * u, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 10 * u, r.y + 10 * u, r.width - 20 * u, r.height - 20 * u), text, style);
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

        /// The fight bar from the sketch: P1 / P2 badges at the ends, long health
        /// bars, and TIME in the middle between two lightning bolts.
        private void DrawTopBar()
        {
            float top = 14 * u, barH = 30 * u, badge = 54 * u, timeW = 150 * u, bolt = 34 * u;
            float side = 14 * u;
            float barW = (Screen.width - timeW - 2 * bolt - 2 * badge - 2 * side - 8 * u) / 2f;
            float cx = Screen.width / 2f;

            var p1Badge = new Rect(side, top - 12 * u, badge, badge);
            var p2Badge = new Rect(Screen.width - side - badge, top - 12 * u, badge, badge);
            var p1Bar = new Rect(p1Badge.xMax + 2 * u, top, barW, barH);
            var p2Bar = new Rect(p2Badge.x - 2 * u - barW, top, barW, barH);

            DrawHealth(rounds.player, p1Bar, false);
            DrawHealth(rounds.enemy, p2Bar, true);
            DrawBadge(p1Badge, "P1", new Color(0.85f, 0.2f, 0.15f));
            DrawBadge(p2Badge, "P2", new Color(0.2f, 0.4f, 0.85f));

            // Names and rounds won under the bars, like "P1  JACKE  ●".
            GUI.Label(new Rect(p1Bar.x, p1Bar.yMax + 2 * u, barW, 28 * u), NameOf(rounds.player), barName);
            var right = new GUIStyle(barName) { alignment = TextAnchor.UpperRight };
            GUI.Label(new Rect(p2Bar.x, p2Bar.yMax + 2 * u, barW, 28 * u), NameOf(rounds.enemy), right);
            DrawRoundPips(new Vector2(p1Bar.xMax, p1Bar.yMax + 8 * u), rounds.PlayerRounds, -1);
            DrawRoundPips(new Vector2(p2Bar.x, p2Bar.yMax + 8 * u), rounds.EnemyRounds, 1);

            // TIME box with a bolt on each side.
            var timeBox = new Rect(cx - timeW / 2f, top - 8 * u, timeW, 72 * u);
            Fill(timeBox, new Color(0f, 0f, 0f, 0.65f));
            GUI.Label(new Rect(timeBox.x, timeBox.y + 2 * u, timeW, 22 * u), "TIME", timeLabel);
            int secs = Mathf.CeilToInt(Mathf.Max(0f, rounds.TimeLeft));
            GUI.Label(new Rect(timeBox.x, timeBox.y + 20 * u, timeW, 50 * u), $"{secs / 60}:{secs % 60:00}", big);
            var boltTex = BoltTexture();
            GUI.DrawTexture(new Rect(timeBox.x - bolt - 2 * u, top - 6 * u, bolt, bolt * 2f), boltTex, ScaleMode.ScaleToFit);
            GUI.DrawTextureWithTexCoords(new Rect(timeBox.xMax + 2 * u, top - 6 * u, bolt, bolt * 2f), boltTex, new Rect(1, 0, -1, 1));
        }

        private void DrawBadge(Rect r, string text, Color color)
        {
            Fill(r, Color.black);
            Fill(new Rect(r.x + 3 * u, r.y + 3 * u, r.width - 6 * u, r.height - 6 * u), color);
            GUI.Label(r, text, badgeText);
        }

        private void DrawRoundPips(Vector2 anchor, int won, int direction)
        {
            float s = 14 * u, gap = 6 * u;
            for (int i = 0; i < rounds.roundsToWin; i++)
            {
                float x = direction < 0 ? anchor.x - (i + 1) * (s + gap) : anchor.x + gap + i * (s + gap);
                var r = new Rect(x, anchor.y, s, s);
                Fill(r, Color.black);
                Fill(new Rect(r.x + 2 * u, r.y + 2 * u, s - 4 * u, s - 4 * u),
                    i < won ? new Color(1f, 0.85f, 0.2f) : new Color(0.3f, 0.3f, 0.3f));
            }
        }

        /// COMBO meters in the bottom corners, kept clear of the touch buttons.
        private void DrawComboMeters()
        {
            if (playerCombo == null) return;
            float w = 250 * u, h = 22 * u, y = Screen.height - 70 * u;
            var clusterLeft = TouchLayout.ToGui(TouchLayout.Light).x; // the button cluster's left edge
            float rightX = Mathf.Min(Screen.width - w - 20 * u, clusterLeft - w - 20 * u);
            DrawCombo(new Rect(20 * u, y, w, h), playerCombo, false);
            DrawCombo(new Rect(rightX, y, w, h), enemyCombo, true);
        }

        private void DrawCombo(Rect r, ComboTracker combo, bool rightToLeft)
        {
            int live = combo.Live;
            var head = new Rect(r.x, r.y - 30 * u, r.width, 28 * u);
            var style = new GUIStyle(comboLabel) { alignment = rightToLeft ? TextAnchor.LowerRight : TextAnchor.LowerLeft };
            GUI.Label(head, live >= 2 ? $"COMBO  x{live}" : "COMBO", style);
            Fill(r, Color.black);
            var inner = new Rect(r.x + 2 * u, r.y + 2 * u, r.width - 4 * u, r.height - 4 * u);
            Fill(inner, new Color(0.2f, 0.2f, 0.22f));
            float w = inner.width * combo.Fill;
            Fill(new Rect(rightToLeft ? inner.xMax - w : inner.x, inner.y, w, inner.height),
                Color.Lerp(new Color(0.3f, 0.7f, 1f), new Color(1f, 0.4f, 0.9f), combo.Fill));
        }

        private string NameOf(FighterController f) =>
            (f.stats != null ? f.stats.displayName : f.name).ToUpperInvariant();

        private void DrawHealth(FighterController f, Rect r, bool rightToLeft)
        {
            float frac = f.Health.Max > 0 ? f.Health.Current / f.Health.Max : 0f;
            Fill(r, Color.black);
            var inner = new Rect(r.x + 3 * u, r.y + 3 * u, r.width - 6 * u, r.height - 6 * u);
            Fill(inner, new Color(0.35f, 0.05f, 0.05f));
            float w = inner.width * frac;
            Fill(new Rect(rightToLeft ? inner.xMax - w : inner.x, inner.y, w, inner.height),
                Color.Lerp(new Color(0.85f, 0.2f, 0.15f), new Color(0.95f, 0.8f, 0.2f), frac));
        }

        private static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static Texture2D bolt;

        /// A yellow lightning bolt, drawn once into a small texture.
        private static Texture2D BoltTexture()
        {
            if (bolt != null) return bolt;
            const int w = 32, h = 64;
            var pts = new[]
            {
                new Vector2(0.62f, 0f), new Vector2(0.12f, 0.56f), new Vector2(0.46f, 0.56f), new Vector2(0.28f, 1f),
                new Vector2(0.9f, 0.4f), new Vector2(0.55f, 0.4f), new Vector2(0.8f, 0f),
            };
            bolt = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // Texture rows start at the bottom; the outline is given top-down.
                var p = new Vector2((x + 0.5f) / w, 1f - (y + 0.5f) / h);
                px[y * w + x] = Inside(p, pts) ? new Color32(255, 214, 40, 255) : new Color32(0, 0, 0, 0);
            }
            bolt.SetPixels32(px);
            bolt.Apply();
            return bolt;
        }

        private static bool Inside(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
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
            cardName = new GUIStyle(label) { fontSize = Mathf.RoundToInt(20 * u), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            cardName.normal.textColor = new Color(0.12f, 0.1f, 0.1f);
            lockTag = new GUIStyle(cardName) { fontSize = Mathf.RoundToInt(16 * u) };
            lockTag.normal.textColor = new Color(0.75f, 0.15f, 0.1f);
            barName = new GUIStyle(label) { fontSize = Mathf.RoundToInt(20 * u), fontStyle = FontStyle.Bold, wordWrap = false };
            badgeText = new GUIStyle(big) { fontSize = Mathf.RoundToInt(24 * u) };
            timeLabel = new GUIStyle(small) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            timeLabel.normal.textColor = new Color(1f, 0.85f, 0.3f);
            comboLabel = new GUIStyle(label) { fontSize = Mathf.RoundToInt(22 * u), fontStyle = FontStyle.Bold, wordWrap = false };
            comboLabel.normal.textColor = new Color(0.6f, 0.85f, 1f);
        }
    }
}

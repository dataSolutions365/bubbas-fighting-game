using Bubba.Abilities;
using Bubba.AI;
using Bubba.Backend;
using Bubba.Combat;
using Bubba.Input;
using UnityEngine;

namespace Bubba.Prototype
{
    /// Builds a playable test arena from nothing: add this one component to
    /// an empty GameObject in an empty scene and press Play. Fighters are
    /// coloured capsules until real animal models replace them.
    public class PrototypeBootstrap : MonoBehaviour
    {
        [Tooltip("Optional. Leave empty to use the built-in bear and gorilla numbers.")]
        public FighterStats[] roster;
        [Tooltip("Optional. Leave empty to use the built-in ability set.")]
        public AbilityLibrary abilityLibrary;
        [Tooltip("Draw the fighters as the hand-drawn monkey (Resources/Monkey) instead of capsules.")]
        public bool useMonkeyArt = true;

        private void Awake()
        {
            if (GameSession.Instance == null) new GameObject("GameSession").AddComponent<GameSession>();

            BuildStage();
            var player = BuildFighter("Player", new Color(0.45f, 0.3f, 0.18f), -2.5f, useMonkeyArt, Color.white);
            // The CPU monkey gets a cool tint so the two are easy to tell apart.
            var enemy = BuildFighter("Enemy", new Color(0.3f, 0.36f, 0.55f), 2.5f, useMonkeyArt, new Color(0.7f, 0.82f, 1f));
            player.opponent = enemy;
            enemy.opponent = player;
            player.SetInput(player.gameObject.AddComponent<PlayerInputRouter>());
            enemy.SetInput(enemy.gameObject.AddComponent<SimpleAI>());

            var rounds = gameObject.AddComponent<RoundManager>();
            rounds.player = player;
            rounds.enemy = enemy;
            rounds.abilityLibrary = abilityLibrary != null ? abilityLibrary : AbilityLibrary.CreateDefaults();
            if (roster != null && roster.Length > 0) rounds.roster.AddRange(roster);
            else
            {
                rounds.roster.Add(DefaultStats("bear", "Grizzly Bear", 1200f, 2.8f, 1.3f));
                rounds.roster.Add(DefaultStats("gorilla", "Silverback Gorilla", 1000f, 3.5f, 1.1f));
            }
            player.stats = rounds.roster[0];
            enemy.stats = rounds.roster[rounds.roster.Count > 1 ? 1 : 0];

            var hud = gameObject.AddComponent<PrototypeHud>();
            hud.rounds = rounds;
        }

        private static void BuildStage()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.transform.SetPositionAndRotation(new Vector3(0f, 2.2f, -9f), Quaternion.Euler(6f, 0f, 0f));
            cam.fieldOfView = 45f;

            if (Object.FindFirstObjectByType<Light>() == null)
            {
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            }

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Arena Floor";
            ground.transform.localScale = new Vector3(2.2f, 1f, 0.6f);
            ground.GetComponent<Renderer>().material.color = new Color(0.42f, 0.5f, 0.32f);
        }

        private static FighterController BuildFighter(string name, Color color, float x, bool monkey, Color monkeyTint)
        {
            var root = new GameObject(name);
            root.transform.position = new Vector3(x, 0f, 0f);
            root.AddComponent<Health>();
            var fighter = root.AddComponent<FighterController>();
            root.AddComponent<AbilityRunner>();

            // Stand-in body: a capsule with a "snout" so you can see which way it faces.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body (replace with model)";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            body.transform.localScale = new Vector3(1f, 1f, 0.8f);
            Object.Destroy(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().material.color = color;

            var snout = GameObject.CreatePrimitive(PrimitiveType.Cube);
            snout.name = "Snout";
            snout.transform.SetParent(body.transform, false);
            snout.transform.localPosition = new Vector3(0f, 0.45f, 0.55f);
            snout.transform.localScale = new Vector3(0.35f, 0.25f, 0.4f);
            Object.Destroy(snout.GetComponent<Collider>());
            snout.GetComponent<Renderer>().material.color = color * 0.7f;

            var visuals = root.AddComponent<FighterVisuals>();
            visuals.body = body.GetComponent<Renderer>();
            visuals.baseColor = color;

            var rig = monkey ? MonkeySpriteRig.TryCreate(fighter, monkeyTint) : null;
            if (rig != null)
            {
                body.SetActive(false);
                visuals.body = null;
                visuals.spriteRig = rig;
            }

            return fighter;
        }

        private static FighterStats DefaultStats(string id, string displayName, float health, float speed, float power)
        {
            var s = ScriptableObject.CreateInstance<FighterStats>();
            s.name = id;
            s.fighterId = id;
            s.displayName = displayName;
            s.maxHealth = health;
            s.moveSpeed = speed;
            s.power = power;
            return s;
        }
    }
}

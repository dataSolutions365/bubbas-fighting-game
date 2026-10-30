using System.Collections.Generic;
using UnityEngine;

namespace Bubba.Prototype
{
    /// Floating words and numbers over the fighters ("-42", "Blocked",
    /// "PARRY!"). Visuals add entries; the HUD draws them.
    public static class CombatText
    {
        public const float Lifetime = 0.9f;

        public struct Entry
        {
            public Vector3 world;
            public string text;
            public Color color;
            public float size;
            public float bornAt;
        }

        public static readonly List<Entry> Entries = new List<Entry>();

        public static void Add(Vector3 world, string text, Color color, float size = 1f)
        {
            // Nudge sideways so quick hits don't stack exactly on top of each other.
            world.x += Random.Range(-0.25f, 0.25f);
            Entries.Add(new Entry { world = world, text = text, color = color, size = size, bornAt = Time.time });
        }

        public static void Prune()
        {
            Entries.RemoveAll(e => Time.time - e.bornAt > Lifetime);
        }
    }
}

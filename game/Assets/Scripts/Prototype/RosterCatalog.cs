using System.Collections.Generic;
using UnityEngine;

namespace Bubba.Prototype
{
    /// The 12 characters from the hand-drawn "Choose Your Fighter" sheet.
    /// Portraits are cut from that drawing into Resources/Roster/<id>.png.
    /// A character becomes playable once it has fight art and stats in the
    /// RoundManager roster (today only Jacke, the boxing monkey).
    public static class RosterCatalog
    {
        public class Entry
        {
            public string id;
            public string name;
            public string animal;
            public string abilityIdea;
        }

        public static readonly List<Entry> All = new List<Entry>
        {
            new Entry { id = "jacke", name = "Jacke", animal = "Boxing monkey", abilityIdea = "Fruit Bowl: eats a snack to heal a little" },
            new Entry { id = "keeteeth", name = "Keeteeth", animal = "Zebra", abilityIdea = "Sound Blast: a music note shockwave" },
            new Entry { id = "esabella", name = "Esabella", animal = "Long-haired fighter", abilityIdea = "Vine Whip: leaves and vines grab the opponent" },
            new Entry { id = "pbnj", name = "PB&J", animal = "Twin ponies", abilityIdea = "Toast Toss: a flying sandwich, two hits" },
            new Entry { id = "phantom-mask", name = "Phantom Mask", animal = "Masked sorcerer", abilityIdea = "Smoke Ball: dark smoke slows the opponent" },
            new Entry { id = "jewly", name = "Jewly", animal = "Bunny", abilityIdea = "Gem Shield: a crystal blocks the next hit" },
            new Entry { id = "tom", name = "Tom", animal = "Cat", abilityIdea = "Nine Lives: gets back up once with a little health" },
            new Entry { id = "caseys", name = "Casey's", animal = "Horned owl", abilityIdea = "Petal Storm: a swirl of flowers pushes back" },
            new Entry { id = "sarena", name = "Sarena", animal = "Maned dragon-horse", abilityIdea = "Wind Slash: a fast claw streak across the screen" },
            new Entry { id = "raven-king", name = "Raven King", animal = "Crowned raven", abilityIdea = "Talon Strike: a diving grab from above" },
            new Entry { id = "iraia", name = "Iraia", animal = "Ram", abilityIdea = "Horn Charge: a running head-butt" },
            new Entry { id = "guardian-dragon", name = "Guardian Dragon", animal = "Winged wolf-dragon", abilityIdea = "Guardian Wings: blocks everything for 2 seconds" },
        };

        private static readonly Dictionary<string, Texture2D> Portraits = new Dictionary<string, Texture2D>();

        public static Entry Find(string id) => All.Find(e => e.id == id);

        /// The portrait cut from the drawing, or null if it isn't in the project.
        public static Texture2D Portrait(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (!Portraits.TryGetValue(id, out var tex))
            {
                tex = Resources.Load<Texture2D>("Roster/" + id);
                Portraits[id] = tex;
            }
            return tex;
        }
    }
}

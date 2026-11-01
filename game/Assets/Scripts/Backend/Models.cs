using System;
using System.Collections.Generic;

namespace Bubba.Backend
{
    public class PlayerProfile
    {
        public string displayName = "Fighter";
        public long fightPoints;
        public bool isPremium;
        public string selectedFighter = "jacke";
        public List<string> equippedAbilities = new List<string>();
        public HashSet<string> ownedAbilities = new HashSet<string>();
    }

    public class CatalogAbility
    {
        public string id;
        public string name;
        public string description;
        public long cost;
        public int tier;
        public string fighterId = "any";
        public bool premiumOnly;

        public bool UsableBy(string fighter) => fighterId == "any" || fighterId == fighter;
    }

    public class MatchReport
    {
        public long pointsAwarded;
        public long fightPoints;
        /// "ok", "daily-cap", "too-short" or "abandoned".
        public string reason;
    }

    public enum MatchResult { Win, Loss, Draw }

    /// A friendly error the game can show, e.g. "Not enough points."
    public class BackendException : Exception
    {
        public BackendException(string message, Exception inner = null) : base(message, inner) { }
    }
}

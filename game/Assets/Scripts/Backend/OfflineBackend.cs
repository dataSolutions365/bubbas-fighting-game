using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Bubba.Backend
{
    /// Stand-in used until Firebase is set up, so the game is playable in
    /// the editor. It mirrors the server rules loosely and keeps everything
    /// in memory. Never ship it: anyone can change local data.
    public class OfflineBackend : IGameBackend
    {
        public string Name => "Offline (testing only)";

        private readonly PlayerProfile profile = new PlayerProfile
        {
            fightPoints = 100,
            equippedAbilities = new List<string> { "roar", "pounce" },
            ownedAbilities = new HashSet<string> { "roar", "pounce" },
        };

        private readonly List<CatalogAbility> catalog = new List<CatalogAbility>
        {
            new CatalogAbility { id = "roar", name = "Roar", cost = 50, tier = 1, description = "Staggers the opponent if they are close." },
            new CatalogAbility { id = "pounce", name = "Pounce", cost = 50, tier = 1, description = "Leap forward and strike from mid range." },
            new CatalogAbility { id = "charge", name = "Charge", cost = 80, tier = 1, description = "Run through the opponent." },
            new CatalogAbility { id = "thick-hide", name = "Thick Hide", cost = 120, tier = 2, description = "Take half damage for 4 seconds." },
            new CatalogAbility { id = "maul", name = "Maul", cost = 150, tier = 2, fighterId = "bear", description = "Bear only. Grab and three heavy swipes." },
            new CatalogAbility { id = "chest-beat", name = "Chest Beat", cost = 150, tier = 2, fighterId = "gorilla", description = "Gorilla only. +30% power for 6 seconds." },
            new CatalogAbility { id = "frenzy", name = "Frenzy", cost = 250, tier = 3, premiumOnly = true, description = "Premium. Attack 40% faster for 5 seconds." },
        };

        private readonly Dictionary<string, float> openMatches = new Dictionary<string, float>();

        public Task SignInAsync() => Task.CompletedTask;

        public Task<PlayerProfile> LoadProfileAsync() => Task.FromResult(profile);

        public Task<List<CatalogAbility>> LoadAbilityCatalogAsync() => Task.FromResult(catalog);

        public Task SetSelectedFighterAsync(string fighterId)
        {
            profile.selectedFighter = fighterId;
            profile.equippedAbilities = profile.equippedAbilities
                .Where(id => catalog.First(a => a.id == id).UsableBy(fighterId)).ToList();
            return Task.CompletedTask;
        }

        public Task<string> StartMatchAsync(string fighterId, string opponentId)
        {
            string id = Guid.NewGuid().ToString("N");
            openMatches[id] = Time.realtimeSinceStartup;
            return Task.FromResult(id);
        }

        public Task<MatchReport> EndMatchAsync(string matchId, MatchResult result)
        {
            if (!openMatches.TryGetValue(matchId, out float started))
            {
                throw new BackendException("That match is already finished.");
            }
            openMatches.Remove(matchId);
            bool tooShort = Time.realtimeSinceStartup - started < 20f;
            long award = tooShort ? 0 : result == MatchResult.Win ? 100 : result == MatchResult.Draw ? 50 : 25;
            profile.fightPoints += award;
            return Task.FromResult(new MatchReport
            {
                pointsAwarded = award,
                fightPoints = profile.fightPoints,
                reason = tooShort ? "too-short" : "ok",
            });
        }

        public Task<long> PurchaseAbilityAsync(string abilityId)
        {
            var a = catalog.FirstOrDefault(x => x.id == abilityId) ?? throw new BackendException("That ability does not exist.");
            if (profile.ownedAbilities.Contains(abilityId)) throw new BackendException("You already own that ability.");
            if (a.premiumOnly && !profile.isPremium) throw new BackendException("That ability needs the premium version.");
            if (profile.fightPoints < a.cost) throw new BackendException("Not enough points.");
            profile.fightPoints -= a.cost;
            profile.ownedAbilities.Add(abilityId);
            return Task.FromResult(profile.fightPoints);
        }

        public Task EquipAbilitiesAsync(IList<string> abilityIds)
        {
            if (abilityIds.Count > 2) throw new BackendException("You can equip at most 2 abilities.");
            foreach (var id in abilityIds)
            {
                if (!profile.ownedAbilities.Contains(id)) throw new BackendException($"You don't own {id}.");
                if (!catalog.First(a => a.id == id).UsableBy(profile.selectedFighter))
                {
                    throw new BackendException($"{id} can't be used by {profile.selectedFighter}.");
                }
            }
            profile.equippedAbilities = abilityIds.ToList();
            return Task.CompletedTask;
        }
    }
}

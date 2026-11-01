// Compiled only after the Firebase Unity SDK is imported and FIREBASE_ENABLED
// is added under Project Settings > Player > Scripting Define Symbols.
// See the README, "Connect Firebase".
#if FIREBASE_ENABLED
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Functions;

namespace Bubba.Backend
{
    public class FirebaseBackend : IGameBackend
    {
        public string Name => "Firebase";

        private FirebaseAuth auth;
        private FirebaseFirestore db;
        private FirebaseFunctions functions;
        private PlayerProfile cached;
        private List<CatalogAbility> catalog;

        private string Uid => auth?.CurrentUser?.UserId ?? throw new BackendException("Not signed in.");

        public async Task SignInAsync()
        {
            var status = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (status != DependencyStatus.Available)
            {
                throw new BackendException($"Firebase is not available on this device ({status}).");
            }
            auth = FirebaseAuth.DefaultInstance;
            db = FirebaseFirestore.DefaultInstance;
            functions = FirebaseFunctions.DefaultInstance;

            if (auth.CurrentUser == null)
            {
                // Anonymous sign-in: play instantly; link Google/Apple later.
                await auth.SignInAnonymouslyAsync();
            }
            await Call("ensurePlayer", new Dictionary<string, object>());
        }

        public async Task<PlayerProfile> LoadProfileAsync()
        {
            var playerDoc = db.Collection("players").Document(Uid);
            var snap = await playerDoc.GetSnapshotAsync();
            if (!snap.Exists) throw new BackendException("Player profile missing.");
            var owned = await playerDoc.Collection("ownedAbilities").GetSnapshotAsync();
            var data = snap.ToDictionary();

            cached = new PlayerProfile
            {
                displayName = Get(data, "displayName", "Fighter"),
                fightPoints = Convert.ToInt64(Get<object>(data, "fightPoints", 0L)),
                isPremium = Get(data, "isPremium", false),
                selectedFighter = Get(data, "selectedFighter", "jacke"),
                equippedAbilities = Get<List<object>>(data, "equippedAbilities", new List<object>())
                    .Select(x => x.ToString()).ToList(),
                ownedAbilities = new HashSet<string>(owned.Documents.Select(d => d.Id)),
            };
            return cached;
        }

        public async Task<List<CatalogAbility>> LoadAbilityCatalogAsync()
        {
            var snap = await db.Collection("abilities").GetSnapshotAsync();
            catalog = new List<CatalogAbility>();
            foreach (var doc in snap.Documents)
            {
                var d = doc.ToDictionary();
                if (!Get(d, "enabled", true)) continue;
                catalog.Add(new CatalogAbility
                {
                    id = doc.Id,
                    name = Get(d, "name", doc.Id),
                    description = Get(d, "description", ""),
                    cost = Convert.ToInt64(Get<object>(d, "cost", 0L)),
                    tier = Convert.ToInt32(Get<object>(d, "tier", 1L)),
                    fighterId = Get(d, "fighterId", "any"),
                    premiumOnly = Get(d, "premiumOnly", false),
                });
            }
            catalog.Sort((a, b) => a.cost.CompareTo(b.cost));
            return catalog;
        }

        public async Task SetSelectedFighterAsync(string fighterId)
        {
            await db.Collection("players").Document(Uid)
                .UpdateAsync(new Dictionary<string, object> { { "selectedFighter", fighterId } });

            // Drop equipped abilities the new fighter can't use.
            if (cached != null && catalog != null)
            {
                var keep = cached.equippedAbilities
                    .Where(id => catalog.Any(a => a.id == id && a.UsableBy(fighterId)))
                    .ToList();
                cached.selectedFighter = fighterId;
                if (keep.Count != cached.equippedAbilities.Count) await EquipAbilitiesAsync(keep);
            }
        }

        public async Task<string> StartMatchAsync(string fighterId, string opponentId)
        {
            var result = await Call("startMatch", new Dictionary<string, object>
            {
                { "fighterId", fighterId },
                { "opponentId", opponentId },
            });
            return result["matchId"].ToString();
        }

        public async Task<MatchReport> EndMatchAsync(string matchId, MatchResult result)
        {
            var r = await Call("endMatch", new Dictionary<string, object>
            {
                { "matchId", matchId },
                { "result", result.ToString().ToLowerInvariant() },
            });
            return new MatchReport
            {
                pointsAwarded = Convert.ToInt64(r["pointsAwarded"]),
                fightPoints = Convert.ToInt64(r["fightPoints"]),
                reason = r["reason"].ToString(),
            };
        }

        public async Task<long> PurchaseAbilityAsync(string abilityId)
        {
            var r = await Call("purchaseAbility", new Dictionary<string, object> { { "abilityId", abilityId } });
            return Convert.ToInt64(r["fightPoints"]);
        }

        public async Task EquipAbilitiesAsync(IList<string> abilityIds)
        {
            await Call("equipAbilities", new Dictionary<string, object> { { "abilityIds", abilityIds.ToList() } });
            if (cached != null) cached.equippedAbilities = abilityIds.ToList();
        }

        // ---------- helpers ----------

        private async Task<Dictionary<string, object>> Call(string name, Dictionary<string, object> data)
        {
            try
            {
                var result = await functions.GetHttpsCallable(name).CallAsync(data);
                var map = new Dictionary<string, object>();
                if (result.Data is IDictionary dict)
                {
                    foreach (DictionaryEntry e in dict) map[e.Key.ToString()] = e.Value;
                }
                return map;
            }
            catch (FunctionsException e)
            {
                // The server's messages are written for players ("Not enough points.").
                throw new BackendException(e.Message, e);
            }
            catch (AggregateException e) when (e.InnerException is FunctionsException fe)
            {
                throw new BackendException(fe.Message, fe);
            }
        }

        private static T Get<T>(IDictionary<string, object> d, string key, T fallback)
        {
            return d.TryGetValue(key, out var v) && v is T t ? t : fallback;
        }
    }
}
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Bubba.Backend
{
    /// Signs in, keeps the player's profile and the ability catalog, and
    /// wraps backend calls so the UI gets a friendly status message.
    public class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        public IGameBackend Backend { get; private set; }
        public PlayerProfile Profile { get; private set; }
        public List<CatalogAbility> Catalog { get; private set; } = new List<CatalogAbility>();
        public bool Ready { get; private set; }
        public bool Busy { get; private set; }
        public string Status { get; private set; } = "Connecting...";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
#if FIREBASE_ENABLED
            Backend = new FirebaseBackend();
#else
            Backend = new OfflineBackend();
#endif
        }

        private async void Start()
        {
            await Run(async () =>
            {
                await Backend.SignInAsync();
                Catalog = await Backend.LoadAbilityCatalogAsync();
                Profile = await Backend.LoadProfileAsync();
                Ready = true;
                return $"Signed in ({Backend.Name}).";
            });
        }

        public CatalogAbility FindAbility(string id) => Catalog.FirstOrDefault(a => a.id == id);

        public Task Buy(string abilityId) => Run(async () =>
        {
            Profile.fightPoints = await Backend.PurchaseAbilityAsync(abilityId);
            Profile.ownedAbilities.Add(abilityId);
            return $"Bought {FindAbility(abilityId)?.name ?? abilityId}.";
        });

        public Task ToggleEquip(string abilityId) => Run(async () =>
        {
            var next = new List<string>(Profile.equippedAbilities);
            if (!next.Remove(abilityId))
            {
                if (next.Count >= 2) next.RemoveAt(0);
                next.Add(abilityId);
            }
            await Backend.EquipAbilitiesAsync(next);
            Profile.equippedAbilities = next;
            return "Loadout saved.";
        });

        public Task SelectFighter(string fighterId) => Run(async () =>
        {
            await Backend.SetSelectedFighterAsync(fighterId);
            Profile = await Backend.LoadProfileAsync();
            return $"Selected {fighterId}.";
        });

        public async Task<string> StartMatch(string fighterId, string opponentId)
        {
            string id = null;
            await Run(async () =>
            {
                id = await Backend.StartMatchAsync(fighterId, opponentId);
                return "Fight!";
            });
            return id;
        }

        public async Task<MatchReport> EndMatch(string matchId, MatchResult result)
        {
            MatchReport report = null;
            await Run(async () =>
            {
                report = await Backend.EndMatchAsync(matchId, result);
                Profile.fightPoints = report.fightPoints;
                return report.reason switch
                {
                    "daily-cap" => $"+{report.pointsAwarded} points (daily limit reached).",
                    "too-short" => "Match too short to earn points.",
                    "abandoned" => "Match took too long to earn points.",
                    _ => $"+{report.pointsAwarded} points!",
                };
            });
            return report;
        }

        private async Task Run(Func<Task<string>> action)
        {
            Busy = true;
            try
            {
                Status = await action();
            }
            catch (BackendException e)
            {
                Status = e.Message;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Status = "Something went wrong. Check the Console.";
            }
            finally
            {
                Busy = false;
            }
        }
    }
}

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bubba.Backend
{
    /// Everything the game asks of the server. Points and ownership only
    /// ever change through these calls, never by writing data directly.
    public interface IGameBackend
    {
        string Name { get; }
        Task SignInAsync();
        Task<PlayerProfile> LoadProfileAsync();
        Task<List<CatalogAbility>> LoadAbilityCatalogAsync();
        Task SetSelectedFighterAsync(string fighterId);
        Task<string> StartMatchAsync(string fighterId, string opponentId);
        Task<MatchReport> EndMatchAsync(string matchId, MatchResult result);
        /// Returns the player's new points balance.
        Task<long> PurchaseAbilityAsync(string abilityId);
        Task EquipAbilitiesAsync(IList<string> abilityIds);
    }
}

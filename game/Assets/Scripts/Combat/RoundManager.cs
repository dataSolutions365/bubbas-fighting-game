using System.Collections.Generic;
using System.Linq;
using Bubba.Abilities;
using Bubba.AI;
using Bubba.Backend;
using Bubba.Input;
using UnityEngine;

namespace Bubba.Combat
{
    /// Runs a best-of-three match: countdown, 3-minute rounds, KO or
    /// time-out, then reports the result to the server for points.
    public class RoundManager : MonoBehaviour
    {
        public enum Phase { Menu, Starting, Countdown, Fighting, RoundOver, MatchOver }

        public FighterController player;
        public FighterController enemy;
        public List<FighterStats> roster = new List<FighterStats>();
        public AbilityLibrary abilityLibrary;
        public float roundSeconds = 180f; // 3:00, as on the fight-screen sketch
        public int roundsToWin = 2;
        public int maxRounds = 5;
        public float startX = 2.5f;

        public Phase CurrentPhase { get; private set; } = Phase.Menu;
        public float TimeLeft { get; private set; }
        public int PlayerRounds { get; private set; }
        public int EnemyRounds { get; private set; }
        public int RoundNumber { get; private set; }
        public string Banner { get; private set; } = "";
        public MatchResult LastResult { get; private set; }
        public MatchReport LastReport { get; private set; }

        private string matchId;
        private float phaseUntil;

        public FighterStats StatsFor(string fighterId) =>
            roster.FirstOrDefault(s => s.fighterId == fighterId) ?? roster.FirstOrDefault();

        public async void BeginMatch(ControlScheme scheme, AiDifficulty aiDifficulty)
        {
            var session = GameSession.Instance;
            if (CurrentPhase != Phase.Menu || session == null || !session.Ready) return;
            CurrentPhase = Phase.Starting;

            var profile = session.Profile;
            player.stats = StatsFor(profile.selectedFighter);
            enemy.stats = roster.FirstOrDefault(s => s.fighterId != player.stats.fighterId) ?? player.stats;

            player.assistMovement = scheme == ControlScheme.Easy;
            player.parryWindowScale = scheme == ControlScheme.Hard ? 0.5f : 1f;
            var router = player.GetComponent<PlayerInputRouter>();
            if (router != null) router.SetScheme(scheme);
            var ai = enemy.GetComponent<SimpleAI>();
            if (ai != null) ai.difficulty = aiDifficulty;

            player.Abilities.Equip(profile.equippedAbilities, abilityLibrary);
            var enemyAbilities = session.Catalog
                .Where(a => a.UsableBy(enemy.stats.fighterId) && !a.premiumOnly)
                .OrderBy(_ => Random.value).Take(2).Select(a => a.id);
            enemy.Abilities.Equip(enemyAbilities, abilityLibrary);

            matchId = await session.StartMatch(player.stats.fighterId, enemy.stats.fighterId);
            if (string.IsNullOrEmpty(matchId))
            {
                CurrentPhase = Phase.Menu;
                return;
            }
            PlayerRounds = EnemyRounds = RoundNumber = 0;
            LastReport = null;
            NextRound();
        }

        public void ReturnToMenu()
        {
            if (CurrentPhase == Phase.MatchOver) CurrentPhase = Phase.Menu;
        }

        private void NextRound()
        {
            RoundNumber++;
            player.ResetForRound(new Vector3(-startX, 0f, 0f));
            enemy.ResetForRound(new Vector3(startX, 0f, 0f));
            SetFightersActive(false);
            TimeLeft = roundSeconds;
            Banner = $"Round {RoundNumber}";
            CurrentPhase = Phase.Countdown;
            phaseUntil = Time.time + 2f;
        }

        private void Update()
        {
            switch (CurrentPhase)
            {
                case Phase.Countdown:
                    if (Time.time >= phaseUntil)
                    {
                        Banner = "Fight!";
                        phaseUntil = Time.time + 1f;
                        CurrentPhase = Phase.Fighting;
                        SetFightersActive(true);
                    }
                    break;
                case Phase.Fighting:
                    if (Time.time >= phaseUntil) Banner = "";
                    TimeLeft -= Time.deltaTime;
                    if (player.Health.IsDead || enemy.Health.IsDead || TimeLeft <= 0f) EndRound();
                    break;
                case Phase.RoundOver:
                    if (Time.time >= phaseUntil)
                    {
                        bool decided = PlayerRounds >= roundsToWin || EnemyRounds >= roundsToWin || RoundNumber >= maxRounds;
                        if (decided) FinishMatch();
                        else NextRound();
                    }
                    break;
            }
        }

        private void EndRound()
        {
            SetFightersActive(false);
            float p = player.Health.Current / player.Health.Max;
            float e = enemy.Health.Current / enemy.Health.Max;
            bool ko = player.Health.IsDead || enemy.Health.IsDead;
            if (Mathf.Approximately(p, e))
            {
                Banner = ko ? "Double KO" : "Time! Draw";
            }
            else if (p > e)
            {
                PlayerRounds++;
                Banner = ko ? "K.O.! You win the round" : "Time! You win the round";
            }
            else
            {
                EnemyRounds++;
                Banner = ko ? "K.O.! Round lost" : "Time! Round lost";
            }
            CurrentPhase = Phase.RoundOver;
            phaseUntil = Time.time + 2.5f;
        }

        private async void FinishMatch()
        {
            CurrentPhase = Phase.MatchOver;
            LastResult = PlayerRounds > EnemyRounds ? MatchResult.Win
                : PlayerRounds < EnemyRounds ? MatchResult.Loss
                : MatchResult.Draw;
            Banner = LastResult == MatchResult.Win ? "You win!" : LastResult == MatchResult.Loss ? "You lose" : "Draw";
            LastReport = await GameSession.Instance.EndMatch(matchId, LastResult);
        }

        private void SetFightersActive(bool active)
        {
            player.InputEnabled = active;
            enemy.InputEnabled = active;
        }
    }
}

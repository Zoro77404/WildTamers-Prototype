using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using WildTamers.Animals;
using WildTamers.Battle;
using WildTamers.Core;

namespace WildTamers.EditorTools.Tests
{
    /// <summary>
    /// Plays 3-vs-1 battles without any visuals, using the same rules, turn order and boss AI as the real battle,
    /// so the balance can be measured (win rate, length, survivors) and guarded by tests.
    /// </summary>
    public static class BattleSimulator
    {
        public class Outcome
        {
            public bool Won;
            public int Rounds;
            public int Survivors;
        }

        public class Summary
        {
            public int Fights;
            public float WinRate;
            public float AverageRounds;
            public float AverageSurvivors;
        }

        /// <summary>A sensible player: uses the skill when it is ready, guards when badly hurt, otherwise attacks.</summary>
        public static BattleAction PlayerPolicy(BattleFighter fighter, IReadOnlyList<BattleFighter> party)
        {
            if (fighter.SkillReady) return BattleAction.Skill;
            var alive = party.Where(f => !f.IsFainted).ToList();
            bool weakest = alive.Count > 1 && alive.OrderBy(f => f.Animal.CurrentHP).First() == fighter;
            if (weakest && fighter.Animal.HPFraction < 0.3f && Random.value < 0.5f) return BattleAction.Defend;
            return BattleAction.Attack;
        }

        public static Outcome Run(IReadOnlyList<AnimalInstance> party, AnimalInstance wild, GameConfig config, int maxRounds = 80)
        {
            var players = party.Select(a => new BattleFighter(a, isPlayer: true)).ToList();
            var boss = new BattleFighter(wild, isPlayer: false);
            var everyone = new List<BattleFighter>(players) { boss };
            bool bossGuardedLast = false;
            int round = 0;

            while (round < maxRounds)
            {
                round++;
                foreach (var fighter in TurnOrder.Build(everyone))
                {
                    if (fighter.IsFainted) continue;
                    fighter.Guarding = false;

                    BattleAction action;
                    BattleFighter target;
                    if (fighter.IsPlayer)
                    {
                        action = PlayerPolicy(fighter, players);
                        target = boss;
                    }
                    else
                    {
                        action = BattleAI.Choose(fighter, config, bossGuardedLast);
                        bossGuardedLast = action == BattleAction.Defend;
                        target = BattleAI.ChooseTarget(players, config);
                    }

                    if (action == BattleAction.Defend) fighter.Guarding = true;
                    else if (target != null)
                    {
                        bool skill = action == BattleAction.Skill && fighter.SkillReady;
                        if (skill) fighter.UseSkill();
                        var hit = BattleRules.RollDamage(fighter, target, skill ? fighter.SkillPower : 1f, config);
                        target.Animal.CurrentHP -= hit.Damage;
                    }
                    fighter.EndTurn();

                    if (boss.IsFainted) return new Outcome { Won = true, Rounds = round, Survivors = players.Count(p => !p.IsFainted) };
                    if (players.All(p => p.IsFainted)) return new Outcome { Won = false, Rounds = round, Survivors = 0 };
                }
            }
            return new Outcome { Won = false, Rounds = round, Survivors = players.Count(p => !p.IsFainted) };
        }

        public static Summary Simulate(AnimalDatabase db, GameConfig config, string[] partyIds, string wildId, int partyLevel, int wildLevel, int fights)
        {
            int wins = 0, rounds = 0, survivors = 0;
            for (int i = 0; i < fights; i++)
            {
                var party = partyIds.Select(id => new AnimalInstance(db.Get(id), partyLevel)).ToList();
                var wild = new AnimalInstance(db.Get(wildId), wildLevel);
                wild.MakeBoss(config);
                var o = Run(party, wild, config);
                if (o.Won) wins++;
                rounds += o.Rounds;
                survivors += o.Survivors;
            }
            return new Summary { Fights = fights, WinRate = wins / (float)fights, AverageRounds = rounds / (float)fights, AverageSurvivors = survivors / (float)fights };
        }

        [MenuItem("Wild Tamers/Debug/Run Balance Simulation")]
        public static void PrintReport()
        {
            var db = AssetDatabase.LoadAssetAtPath<AnimalDatabase>("Assets/_Project/Resources/AnimalDatabase.asset");
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Resources/GameConfig.asset");
            Random.InitState(2024);
            var sb = new StringBuilder("[Balance] 3 starters vs boss (win % / rounds / survivors)\n");
            var starters = db.Starters.Select(s => s.id).ToArray();
            foreach (int gap in new[] { -2, -1, 0, 1, 2 })
            {
                sb.Append($"  boss {gap:+0;-0;±0} lv: ");
                float winSum = 0f, roundSum = 0f, survSum = 0f;
                foreach (var wild in db.Animals)
                {
                    var r = Simulate(db, config, starters, wild.id, 10, 10 + gap, 300);
                    winSum += r.WinRate; roundSum += r.AverageRounds; survSum += r.AverageSurvivors;
                }
                int n = db.Animals.Count;
                sb.AppendLine($"{winSum / n * 100f:0}% / {roundSum / n:0.0} rounds / {survSum / n:0.0} left");
            }
            sb.AppendLine("  same level, every boss vs the starter trio:");
            foreach (var wild in db.Animals)
            {
                var r = Simulate(db, config, starters, wild.id, 10, 10, 400);
                sb.AppendLine($"    {wild.displayName,-16} {r.WinRate * 100f:0}%  {r.AverageRounds:0.0} rounds  {r.AverageSurvivors:0.0} left");
            }
            sb.AppendLine("  same level, random party of 3 vs random boss:");
            float totalWin = 0f; int total = 0;
            var rng = new System.Random(5);
            for (int i = 0; i < 1500; i++)
            {
                var ids = db.Animals.OrderBy(_ => rng.Next()).Take(3).Select(a => a.id).ToArray();
                var wildId = db.Animals[rng.Next(db.Animals.Count)].id;
                var r = Simulate(db, config, ids, wildId, 10, 10, 1);
                totalWin += r.WinRate; total++;
            }
            sb.AppendLine($"    {totalWin / total * 100f:0}% wins over {total} fights");
            Debug.Log(sb.ToString());
        }
    }
}

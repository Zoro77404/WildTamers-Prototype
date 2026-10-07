using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WildTamers.Animals;
using WildTamers.Battle;
using WildTamers.Core;

namespace WildTamers.EditorTools.Tests
{
    /// <summary>Edit-mode checks for the battle math, the 3-vs-1 rules, leveling, the roster and the save format.</summary>
    public class BattleRulesTests
    {
        private static readonly string[] NewSpecies =
            { "camel", "arabian_horse", "falcon", "saluki", "arabian_oryx", "arabian_gazelle", "arabian_wolf", "arabian_fox" };

        private GameConfig config;
        private AnimalDatabase database;

        [SetUp]
        public void SetUp()
        {
            config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Resources/GameConfig.asset");
            database = AssetDatabase.LoadAssetAtPath<AnimalDatabase>("Assets/_Project/Resources/AnimalDatabase.asset");
            Random.InitState(1234);
        }

        [TearDown]
        public void TearDown() => Lang.Loc.SetLanguage(Lang.GameLanguage.English);

        private BattleFighter Fighter(string id, int level, bool player) => new BattleFighter(new AnimalInstance(database.Get(id), level), player);

        private BattleFighter Boss(string id, int level)
        {
            var animal = new AnimalInstance(database.Get(id), level);
            animal.MakeBoss(config);
            return new BattleFighter(animal, isPlayer: false);
        }

        // ---------- Roster ----------

        [Test]
        public void RosterIsTheEightArabianAnimals()
        {
            CollectionAssert.AreEquivalent(NewSpecies, database.Animals.Select(a => a.id).ToArray());
            CollectionAssert.AreEqual(new[] { "camel", "arabian_horse", "falcon" }, database.Starters.Select(a => a.id).ToArray());
            Assert.IsNull(database.Get("bear"));
            Assert.IsNull(database.Get("boar"));
        }

        [Test]
        public void EveryAnimalHasModelDescriptionAndHistory()
        {
            foreach (var a in database.Animals)
            {
                Assert.IsNotNull(a.prefab, a.id + " prefab");
                Assert.IsNotNull(a.prefab.GetComponent<AnimalVisual>(), a.id + " AnimalVisual");
                // Texts live in the "Animals" string table; both languages are checked.
                foreach (var language in new[] { Lang.GameLanguage.English, Lang.GameLanguage.Arabic })
                {
                    Lang.Loc.SetLanguage(language);
                    foreach (var (label, text) in new[] { ("description", a.LocalizedDescription), ("history", a.LocalizedHistory) })
                    {
                        Assert.IsFalse(string.IsNullOrWhiteSpace(text), $"{a.id} {label} ({language})");
                        int sentences = text.Count(c => c == '.');
                        Assert.That(sentences, Is.InRange(2, 3), $"{a.id} {label} ({language}) should be 2–3 sentences");
                        Assert.Less(text.Length, 330, $"{a.id} {label} ({language}) should stay short");
                    }
                }
            }
        }

        // ---------- Damage ----------

        [Test]
        public void DamageIsPositiveAndWithinSpread()
        {
            var wolf = Fighter("arabian_wolf", 5, true);
            var oryx = Fighter("arabian_oryx", 5, false);
            float expected = BattleRules.BaseDamage(wolf, oryx, 1f, config);
            float max = expected * config.damageSpread.y * config.critMultiplier + 1f;
            for (int i = 0; i < 500; i++)
            {
                var hit = BattleRules.RollDamage(wolf, oryx, 1f, config);
                Assert.GreaterOrEqual(hit.Damage, 1);
                Assert.LessOrEqual(hit.Damage, max);
            }
        }

        [Test]
        public void SkillsNeverCritAndCantOneShotAnEvenLevelAnimal()
        {
            var falcon = Fighter("falcon", 5, false);
            var fox = Fighter("arabian_fox", 5, true);
            for (int i = 0; i < 1000; i++) Assert.IsFalse(BattleRules.RollDamage(falcon, fox, falcon.SkillPower, config).Critical);
            float max = BattleRules.BaseDamage(falcon, fox, falcon.SkillPower, config) * config.damageSpread.y;
            Assert.Less(max, fox.Animal.MaxHP);
        }

        [Test]
        public void GuardHalvesDamage()
        {
            var gazelle = Fighter("arabian_gazelle", 10, true);
            var camel = Fighter("camel", 10, false);
            float open = 0f, guarded = 0f;
            for (int i = 0; i < 2000; i++) open += BattleRules.RollDamage(gazelle, camel, 1f, config).Damage;
            camel.Guarding = true;
            for (int i = 0; i < 2000; i++) guarded += BattleRules.RollDamage(gazelle, camel, 1f, config).Damage;
            Assert.AreEqual(config.defendMultiplier, guarded / open, 0.06f);
        }

        [Test]
        public void SkillHitsHarderAndHigherLevelHitsHarder()
        {
            var fox = Fighter("arabian_fox", 5, true);
            var wolf = Fighter("arabian_wolf", 5, false);
            Assert.Greater(BattleRules.BaseDamage(fox, wolf, fox.SkillPower, config), BattleRules.BaseDamage(fox, wolf, 1f, config));
            var strongFox = Fighter("arabian_fox", 9, true);
            Assert.Greater(BattleRules.BaseDamage(strongFox, wolf, 1f, config), BattleRules.BaseDamage(fox, wolf, 1f, config));
        }

        [Test]
        public void NormalHitTakesAFairChunkOfHP()
        {
            // Balance guard: an even-level normal hit should take roughly 8–40% of a normal animal's HP.
            foreach (var a in database.Animals)
            foreach (var d in database.Animals)
            {
                var attacker = Fighter(a.id, 5, true);
                var defender = Fighter(d.id, 5, true);
                float share = BattleRules.BaseDamage(attacker, defender, 1f, config) / defender.Animal.MaxHP;
                Assert.That(share, Is.InRange(0.08f, 0.4f), $"{a.id} vs {d.id}");
            }
        }

        [Test]
        public void SkillCooldownCountsOwnTurns()
        {
            var fox = Fighter("arabian_fox", 5, true);
            int cooldown = fox.Data.skill.cooldownTurns;
            Assert.IsTrue(fox.SkillReady);
            fox.UseSkill();
            fox.EndTurn();
            for (int turn = cooldown; turn > 0; turn--)
            {
                Assert.AreEqual(turn, fox.SkillCooldown);
                Assert.IsFalse(fox.SkillReady);
                fox.EndTurn();
            }
            Assert.IsTrue(fox.SkillReady);
        }

        // ---------- The boss ----------

        [Test]
        public void BossHasAboutTripleHPAndIsABitStronger()
        {
            var normal = new AnimalInstance(database.Get("arabian_wolf"), 8);
            var boss = new AnimalInstance(database.Get("arabian_wolf"), 8);
            boss.MakeBoss(config);
            Assert.IsTrue(boss.IsBoss);
            Assert.AreEqual(normal.MaxHP * config.bossHPMultiplier, boss.MaxHP, 1.5f);
            Assert.AreEqual(boss.MaxHP, boss.CurrentHP);
            Assert.Greater(boss.Attack, normal.Attack);
            Assert.GreaterOrEqual(boss.Defense, normal.Defense);
            Assert.That(config.bossHPMultiplier, Is.InRange(2.5f, 3.5f));

            boss.ClearBoss();
            Assert.IsFalse(boss.IsBoss);
            Assert.AreEqual(normal.MaxHP, boss.MaxHP);
            Assert.LessOrEqual(boss.CurrentHP, boss.MaxHP);
        }

        [Test]
        public void TurnOrderIsBySpeedForAllFourAndSkipsTheFainted()
        {
            var party = new List<BattleFighter> { Fighter("camel", 5, true), Fighter("falcon", 5, true), Fighter("arabian_horse", 5, true) };
            var boss = Boss("arabian_wolf", 5);
            var all = new List<BattleFighter>(party) { boss };
            var order = TurnOrder.Build(all);
            Assert.AreEqual(4, order.Count);
            for (int i = 1; i < order.Count; i++) Assert.GreaterOrEqual(order[i - 1].Animal.Speed, order[i].Animal.Speed);
            Assert.AreSame(party[1], order[0], "falcon is the fastest");
            Assert.AreSame(party[0], order[3], "camel is the slowest");

            party[1].Animal.CurrentHP = 0;
            var after = TurnOrder.Build(all);
            Assert.AreEqual(3, after.Count);
            CollectionAssert.DoesNotContain(after, party[1]);
        }

        [Test]
        public void BossMostlyHitsTheWeakestAnimal()
        {
            var party = new List<BattleFighter> { Fighter("camel", 5, true), Fighter("falcon", 5, true), Fighter("arabian_horse", 5, true) };
            party[2].Animal.CurrentHP = 3;
            int weakest = 0, other = 0;
            for (int i = 0; i < 2000; i++)
            {
                var target = BattleAI.ChooseTarget(party, config);
                if (target == party[2]) weakest++; else other++;
            }
            float share = weakest / 2000f;
            Assert.Greater(share, config.bossFocusWeakest, "weakest is hit more often than the focus chance alone");
            Assert.Greater(other, 0, "the others are still hit sometimes");

            party[2].Animal.CurrentHP = 0;
            for (int i = 0; i < 200; i++) Assert.AreNotSame(party[2], BattleAI.ChooseTarget(party, config), "fainted animals are never targeted");
            party[0].Animal.CurrentHP = 0;
            party[1].Animal.CurrentHP = 0;
            Assert.IsNull(BattleAI.ChooseTarget(party, config));
        }

        [Test]
        public void EscapeFavorsAFasterTeamAndImprovesAfterFails()
        {
            var fast = new List<BattleFighter> { Fighter("falcon", 5, true), Fighter("arabian_gazelle", 5, true), Fighter("saluki", 5, true) };
            var slow = new List<BattleFighter> { Fighter("camel", 5, true), Fighter("arabian_oryx", 5, true), Fighter("camel", 5, true) };
            var boss = Boss("arabian_wolf", 5);
            float fastChance = BattleRules.EscapeChance(fast, boss, 0, config);
            float slowChance = BattleRules.EscapeChance(slow, boss, 0, config);
            Assert.Greater(fastChance, slowChance);
            Assert.Greater(BattleRules.EscapeChance(slow, boss, 2, config), slowChance);
            Assert.That(slowChance, Is.InRange(config.runChanceLimits.x, config.runChanceLimits.y));
        }

        // ---------- Balance ----------

        [Test]
        public void StarterTrioVsSameLevelBossIsAFairFight()
        {
            Random.InitState(99);
            var starters = database.Starters.Select(s => s.id).ToArray();
            float wins = 0f, rounds = 0f;
            int fights = 0;
            foreach (var wild in database.Animals)
            {
                var r = BattleSimulator.Simulate(database, config, starters, wild.id, 10, 10, 250);
                wins += r.WinRate; rounds += r.AverageRounds; fights++;
            }
            float winRate = wins / fights;
            float averageRounds = rounds / fights;
            Assert.That(winRate, Is.InRange(0.6f, 0.9f), $"win rate was {winRate:P0}");
            Assert.That(averageRounds, Is.InRange(4f, 12f), $"fights lasted {averageRounds:0.0} rounds");
        }

        [Test]
        public void ABossFiveLevelsHigherIsMuchHarderButAWeakerOneIsEasy()
        {
            Random.InitState(7);
            var starters = database.Starters.Select(s => s.id).ToArray();
            float Rate(int gap) => database.Animals.Average(w => BattleSimulator.Simulate(database, config, starters, w.id, 10, 10 + gap, 150).WinRate);
            float easy = Rate(-3), even = Rate(0), hard = Rate(3);
            Assert.Greater(easy, even);
            Assert.Greater(even, hard);
        }

        // ---------- Experience ----------

        [Test]
        public void ExperienceLevelsUpAndRaisesStats()
        {
            var fox = new AnimalInstance(database.Get("arabian_fox"), 5);
            var result = fox.AddExperience(config.ExperienceToNext(5) + 1, config);
            Assert.IsTrue(result.LeveledUp);
            Assert.AreEqual(6, fox.Level);
            Assert.AreEqual(1, fox.Experience);
            Assert.GreaterOrEqual(result.NewMaxHP, result.OldMaxHP);
            Assert.Greater(result.NewAttack + result.NewDefense + result.NewSpeed + result.NewMaxHP,
                result.OldAttack + result.OldDefense + result.OldSpeed + result.OldMaxHP);
        }

        [Test]
        public void AFaintedAnimalStillGainsXpButStaysFainted()
        {
            var camel = new AnimalInstance(database.Get("camel"), 5);
            camel.CurrentHP = 0;
            var result = camel.AddExperience(config.ExperienceToNext(5) + 1, config);
            Assert.IsTrue(result.LeveledUp);
            Assert.IsTrue(camel.IsFainted);
        }

        // ---------- Saves ----------

        [Test]
        public void SaveRoundTripKeepsTheTeam()
        {
            var wolf = new AnimalInstance(database.Get("arabian_wolf"), 7);
            wolf.AddExperience(25, config);
            wolf.CurrentHP = 12;
            var fainted = new AnimalInstance(database.Get("camel"), 4);
            fainted.CurrentHP = 0;
            var data = new SaveData();
            data.team.Add(wolf.ToSave());
            data.team.Add(fainted.ToSave());
            data.team.Add(new AnimalSaveData { animalId = "dragon", level = 3 });
            data.lastTeam.Add(wolf.Uid);
            data.seenSpecies.Add("arabian_wolf");

            var loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
            var back = AnimalInstance.FromSave(loaded.team[0], database, config);
            Assert.AreEqual(wolf.Uid, back.Uid);
            Assert.AreEqual(wolf.Level, back.Level);
            Assert.AreEqual(wolf.Experience, back.Experience);
            Assert.AreEqual(12, back.CurrentHP);
            Assert.AreEqual(0, AnimalInstance.FromSave(loaded.team[1], database, config).CurrentHP, "fainted stays fainted");
            Assert.IsNull(AnimalInstance.FromSave(loaded.team[2], database, config), "unknown species are skipped");
            CollectionAssert.AreEqual(new[] { wolf.Uid }, loaded.lastTeam);
            CollectionAssert.AreEqual(new[] { "arabian_wolf" }, loaded.seenSpecies);
        }

        [Test]
        public void ANewGameStartsWithCamelHorseAndFalcon()
        {
            var result = SaveMigration.Apply(new SaveData(), database, config);
            CollectionAssert.AreEqual(new[] { "camel", "arabian_horse", "falcon" }, result.Team.Select(a => a.AnimalId).ToArray());
            Assert.IsTrue(result.Team.All(a => a.Level == config.starterLevel && a.IsFullHP));
            Assert.IsTrue(result.Changed, "a fresh game should be written to disk");
            Assert.AreEqual(0, result.SeenSpecies.Count, "all three cards are still to be shown");
        }

        [Test]
        public void AnOldSaveKeepsItsAnimalsAsNewSpeciesAndGetsTheMissingStarters()
        {
            var old = new SaveData { version = 1, activeIndex = 2 };
            old.team.Add(new AnimalSaveData { uid = "a", animalId = "fox", level = 9, experience = 14, currentHP = 20 });
            old.team.Add(new AnimalSaveData { uid = "b", animalId = "wolf", level = 7, experience = 3, currentHP = 0 });
            old.team.Add(new AnimalSaveData { uid = "c", animalId = "bull", level = 12, experience = 40, currentHP = 55 });
            old.team.Add(new AnimalSaveData { uid = "d", animalId = "stag", level = 6, experience = 0, currentHP = 30 });
            old.team.Add(new AnimalSaveData { uid = "e", animalId = "frog", level = 5, experience = 1, currentHP = 44 });
            old.team.Add(new AnimalSaveData { uid = "f", animalId = "snake", level = 8, experience = 9, currentHP = 38 });
            old.team.Add(new AnimalSaveData { uid = "g", animalId = "dragon", level = 3, currentHP = 10 });

            var result = SaveMigration.Apply(old, database, config);
            var species = result.Team.Select(a => a.AnimalId).ToList();
            Assert.IsTrue(species.All(id => NewSpecies.Contains(id)), "no removed species left: " + string.Join(",", species));
            Assert.AreEqual("arabian_fox", result.Team.First(a => a.Uid == "a").AnimalId);
            Assert.AreEqual("arabian_wolf", result.Team.First(a => a.Uid == "b").AnimalId);
            Assert.AreEqual("camel", result.Team.First(a => a.Uid == "c").AnimalId);

            // Levels, XP and HP survive the conversion.
            var camel = result.Team.First(a => a.Uid == "c");
            Assert.AreEqual(12, camel.Level);
            Assert.AreEqual(40, camel.Experience);
            Assert.AreEqual(55, camel.CurrentHP);
            Assert.IsFalse(result.Team.First(a => a.Uid == "b").IsFainted, "version 1 never kept a fainted animal");

            // Missing starters are added (camel came from the bull, so only the horse and falcon are new).
            Assert.AreEqual(2, result.StartersAdded);
            Assert.IsTrue(species.Contains("arabian_horse") && species.Contains("falcon"));
            Assert.AreEqual(1, species.Count(id => id == "camel"));
            Assert.AreEqual(1, result.Dropped, "the unknown species is dropped");
            Assert.IsTrue(result.Changed);

            // The animal that used to fight alone leads the remembered team (index 2 = the bull, now the camel).
            CollectionAssert.AreEqual(new[] { "c" }, result.LastTeam);
            Assert.AreEqual(0, result.SeenSpecies.Count);
        }

        [Test]
        public void ACurrentSaveIsLeftAlone()
        {
            var current = new SaveData();
            foreach (var id in new[] { "camel", "arabian_horse", "falcon", "saluki" })
                current.team.Add(new AnimalInstance(database.Get(id), 6).ToSave());
            current.lastTeam.AddRange(new[] { current.team[3].uid, "gone", current.team[0].uid });
            current.seenSpecies.AddRange(new[] { "camel", "arabian_horse", "falcon", "saluki" });

            var result = SaveMigration.Apply(current, database, config);
            Assert.AreEqual(0, result.StartersAdded);
            Assert.AreEqual(0, result.Converted);
            CollectionAssert.AreEqual(new[] { current.team[3].uid, current.team[0].uid }, result.LastTeam, "unknown uids are dropped, order kept");
            Assert.AreEqual(4, result.SeenSpecies.Count);
        }

        // ---------- Picking the fight team ----------

        [Test]
        public void TheRememberedTeamComesFirstAndTheRestIsFilledWithTheStrongest()
        {
            var team = new[] { "camel", "arabian_horse", "falcon", "saluki", "arabian_wolf" }.Select((id, i) => new AnimalInstance(database.Get(id), 5 + i)).ToList();
            var party = PartySelection.Default(team, new[] { team[0].Uid, team[1].Uid }, config);
            Assert.AreEqual(3, party.Count);
            Assert.AreSame(team[0], party[0]);
            Assert.AreSame(team[1], party[1]);
            Assert.AreSame(team[4], party[2], "the strongest animal fills the free slot");
        }

        [Test]
        public void WithFewerThanThreeHealthyAnimalsYouFightWithWhatYouHave()
        {
            var team = new[] { "camel", "arabian_horse", "falcon", "saluki" }.Select(id => new AnimalInstance(database.Get(id), 6)).ToList();
            team[0].CurrentHP = 0;                       // fainted
            team[1].CurrentHP = 1;                       // alive but far too hurt
            Assert.AreEqual(2, PartySelection.Capacity(team, config));
            var party = PartySelection.Default(team, new string[0], config);
            CollectionAssert.AreEquivalent(new[] { team[2], team[3] }, party);

            team[2].CurrentHP = 0;
            Assert.AreEqual(1, PartySelection.Capacity(team, config));

            // Nobody is fit: the ones that are still standing fight anyway.
            team[3].CurrentHP = 1;
            var last = PartySelection.Default(team, new string[0], config);
            CollectionAssert.AreEquivalent(new[] { team[1], team[3] }, last);
            Assert.AreEqual(2, PartySelection.Capacity(team, config));
        }
    }
}

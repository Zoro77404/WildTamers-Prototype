using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WildTamers.Animals;
using WildTamers.Battle;
using WildTamers.Core;

namespace WildTamers.EditorTools.Tests
{
    /// <summary>Edit-mode checks for the battle math, leveling and the save format.</summary>
    public class BattleRulesTests
    {
        private GameConfig config;
        private AnimalDatabase database;

        [SetUp]
        public void SetUp()
        {
            config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Resources/GameConfig.asset");
            database = AssetDatabase.LoadAssetAtPath<AnimalDatabase>("Assets/_Project/Resources/AnimalDatabase.asset");
            Random.InitState(1234);
        }

        private BattleFighter Fighter(string id, int level, bool player) => new BattleFighter(new AnimalInstance(database.Get(id), level), player);

        [Test]
        public void DamageIsPositiveAndWithinSpread()
        {
            var wolf = Fighter("wolf", 5, true);
            var frog = Fighter("frog", 5, false);
            float expected = BattleRules.BaseDamage(wolf, frog, 1f, config);
            float max = expected * config.damageSpread.y * config.critMultiplier + 1f;
            for (int i = 0; i < 500; i++)
            {
                var hit = BattleRules.RollDamage(wolf, frog, 1f, config);
                Assert.GreaterOrEqual(hit.Damage, 1);
                Assert.LessOrEqual(hit.Damage, max);
            }
        }

        [Test]
        public void SkillsNeverCrit()
        {
            var stag = Fighter("stag", 5, false);
            var fox = Fighter("fox", 5, true);
            for (int i = 0; i < 1000; i++) Assert.IsFalse(BattleRules.RollDamage(stag, fox, stag.SkillPower, config).Critical);
            // Even the hardest skill roll can't one-shot an even-level starter from full HP.
            float max = BattleRules.BaseDamage(stag, fox, stag.SkillPower, config) * config.damageSpread.y;
            Assert.Less(max, fox.Animal.MaxHP);
        }

        [Test]
        public void GuardHalvesDamage()
        {
            var stag = Fighter("stag", 10, true);
            var bull = Fighter("bull", 10, false);
            float open = 0f, guarded = 0f;
            for (int i = 0; i < 2000; i++) open += BattleRules.RollDamage(stag, bull, 1f, config).Damage;
            bull.Guarding = true;
            for (int i = 0; i < 2000; i++) guarded += BattleRules.RollDamage(stag, bull, 1f, config).Damage;
            Assert.AreEqual(config.defendMultiplier, guarded / open, 0.06f);
        }

        [Test]
        public void SkillHitsHarderAndHigherLevelHitsHarder()
        {
            var fox = Fighter("fox", 5, true);
            var wolf = Fighter("wolf", 5, false);
            Assert.Greater(BattleRules.BaseDamage(fox, wolf, fox.SkillPower, config), BattleRules.BaseDamage(fox, wolf, 1f, config));
            var strongFox = Fighter("fox", 9, true);
            Assert.Greater(BattleRules.BaseDamage(strongFox, wolf, 1f, config), BattleRules.BaseDamage(fox, wolf, 1f, config));
        }

        [Test]
        public void NormalHitTakesAFairChunkOfHP()
        {
            // Balance guard: an even-level normal hit should take roughly 8–40% of the target's HP.
            foreach (var a in database.Animals)
            foreach (var d in database.Animals)
            {
                var attacker = Fighter(a.id, 5, true);
                var defender = Fighter(d.id, 5, false);
                float share = BattleRules.BaseDamage(attacker, defender, 1f, config) / defender.Animal.MaxHP;
                Assert.That(share, Is.InRange(0.08f, 0.4f), $"{a.id} vs {d.id}");
            }
        }

        [Test]
        public void SkillCooldownCountsOwnTurns()
        {
            var fox = Fighter("fox", 5, true);
            int cooldown = fox.Data.skill.cooldownTurns;
            Assert.IsTrue(fox.SkillReady);
            fox.UseSkill();
            fox.EndRound();
            for (int turn = cooldown; turn > 0; turn--)
            {
                Assert.AreEqual(turn, fox.SkillCooldown);
                Assert.IsFalse(fox.SkillReady);
                fox.EndRound();
            }
            Assert.IsTrue(fox.SkillReady);
        }

        [Test]
        public void EscapeFavorsTheFasterAnimalAndImprovesAfterFails()
        {
            var fox = Fighter("fox", 5, true);
            var bull = Fighter("bull", 5, false);
            var bullRunner = Fighter("bull", 5, true);
            var foxFoe = Fighter("fox", 5, false);
            float fast = BattleRules.EscapeChance(fox, bull, config);
            float slow = BattleRules.EscapeChance(bullRunner, foxFoe, config);
            Assert.Greater(fast, slow);
            bullRunner.FailedEscapes = 2;
            Assert.Greater(BattleRules.EscapeChance(bullRunner, foxFoe, config), slow);
        }

        [Test]
        public void ExperienceLevelsUpAndRaisesStats()
        {
            var fox = new AnimalInstance(database.Get("fox"), 5);
            var result = fox.AddExperience(config.ExperienceToNext(5) + 1, config);
            Assert.IsTrue(result.LeveledUp);
            Assert.AreEqual(6, fox.Level);
            Assert.AreEqual(1, fox.Experience);
            Assert.GreaterOrEqual(result.NewMaxHP, result.OldMaxHP);
            Assert.Greater(result.NewAttack + result.NewDefense + result.NewSpeed + result.NewMaxHP,
                result.OldAttack + result.OldDefense + result.OldSpeed + result.OldMaxHP);
        }

        [Test]
        public void SaveRoundTripKeepsTheTeam()
        {
            var wolf = new AnimalInstance(database.Get("wolf"), 7);
            wolf.AddExperience(25, config);
            wolf.CurrentHP = 12;
            var data = new SaveData { activeIndex = 0 };
            data.team.Add(wolf.ToSave());
            data.team.Add(new AnimalSaveData { animalId = "dragon", level = 3 });

            var loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
            var back = AnimalInstance.FromSave(loaded.team[0], database, config);
            Assert.AreEqual(wolf.Uid, back.Uid);
            Assert.AreEqual(wolf.Level, back.Level);
            Assert.AreEqual(wolf.Experience, back.Experience);
            Assert.AreEqual(12, back.CurrentHP);
            Assert.IsNull(AnimalInstance.FromSave(loaded.team[1], database, config), "unknown species are skipped");
        }
    }
}

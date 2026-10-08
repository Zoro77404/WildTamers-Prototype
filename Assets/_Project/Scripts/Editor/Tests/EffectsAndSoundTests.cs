using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WildTamers.Animals;
using WildTamers.Audio;
using WildTamers.Battle;
using WildTamers.Vfx;

namespace WildTamers.EditorTools.Tests
{
    /// <summary>Edit-mode checks for the special-attack effects (baked clips, per-animal settings, timing) and the Sound Library.</summary>
    public class EffectsAndSoundTests
    {
        private static AnimalData[] Animals() =>
            AssetDatabase.FindAssets("t:AnimalData").Select(g => AssetDatabase.LoadAssetAtPath<AnimalData>(AssetDatabase.GUIDToAssetPath(g))).ToArray();

        private static SoundLibrary Library() => Resources.Load<SoundLibrary>(SoundLibrary.ResourcePath);

        [Test]
        public void EveryAnimalHasAPlayableSpecialEffectAndSound()
        {
            var animals = Animals();
            Assert.That(animals.Length, Is.EqualTo(8));
            foreach (var animal in animals)
            {
                var fx = animal.special;
                Assert.IsNotNull(fx, animal.id);
                Assert.IsNotNull(fx.vfx, $"{animal.id} has no Special VFX");
                Assert.IsNotNull(fx.sound, $"{animal.id} has no Special sound");
                Assert.That(fx.vfx.layers.Length, Is.GreaterThan(0), animal.id);
                foreach (var layer in fx.vfx.layers)
                {
                    Assert.IsNotNull(layer.clip, $"{animal.id}: {fx.vfx.name} has an empty layer");
                    Assert.IsTrue(layer.clip.IsValid, $"{layer.clip.name} is not a valid bake");
                }
                Assert.That(fx.scale, Is.GreaterThan(0f), animal.id);
                Assert.That(fx.speed, Is.GreaterThan(0f), animal.id);
            }
        }

        [Test]
        public void FalconUsesTheTornadoAndFireUsesTheWholeSetInOrder()
        {
            var falcon = Animals().First(a => a.id == "falcon");
            Assert.That(falcon.special.vfx.layers.Select(l => l.clip.name), Is.EquivalentTo(new[] { "Tornado" }));

            var fire = VfxBaker.LoadEffect(VfxBaker.Fire);
            var order = fire.layers.OrderBy(l => l.start).Select(l => l.clip.name).ToArray();
            Assert.That(order, Is.EqualTo(new[] { "Fire_Burst", "Fire_Idle", "Fire_End" }));
        }

        [Test]
        public void SpecialEffectHitLinesUpWithTheImpact()
        {
            foreach (var animal in Animals())
            {
                var fx = animal.special;
                float charge = SpecialAttackPlayer.PlanCharge(fx, BattleActor.SkillCharge, BattleActor.SkillLunge, out float spawnAt);
                float impact = charge + BattleActor.SkillLunge;
                Assert.That(spawnAt, Is.GreaterThanOrEqualTo(0f), animal.id);
                Assert.That(charge, Is.InRange(BattleActor.SkillCharge, SpecialAttackPlayer.MaxCharge), animal.id);
                // The effect's own hit moment lands on the impact (shifted only by the animal's delay setting).
                Assert.That(spawnAt + SpecialAttackPlayer.HitLead(fx) - fx.delay, Is.EqualTo(impact).Within(0.001f), animal.id);
            }
        }

        [Test]
        public void DelayShiftsTheEffectWithoutBreakingTheImpact()
        {
            var fx = new SpecialAttackFx { vfx = VfxBaker.LoadEffect(VfxBaker.Spikes), speed = 1f, delay = 0.2f };
            SpecialAttackPlayer.PlanCharge(fx, BattleActor.SkillCharge, BattleActor.SkillLunge, out float later);
            fx.delay = 0f;
            SpecialAttackPlayer.PlanCharge(fx, BattleActor.SkillCharge, BattleActor.SkillLunge, out float onTime);
            Assert.That(later - onTime, Is.EqualTo(0.2f).Within(0.001f));
        }

        [Test]
        public void BakedClipsDecodeInsideTheirBounds()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:VfxClip"))
            {
                var clip = AssetDatabase.LoadAssetAtPath<VfxClip>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsTrue(clip.IsValid, clip.name);
                var mesh = clip.CreateMesh();
                try
                {
                    Assert.That(mesh.subMeshCount, Is.EqualTo(clip.parts.Length), clip.name);
                    Assert.IsTrue(clip.parts.All(p => p.material != null), $"{clip.name} has a part without material");
                    var pos = new Vector3[clip.VertexCount];
                    var col = new Color[clip.VertexCount];
                    var nrm = new Vector3[clip.VertexCount];
                    var bounds = clip.Bounds;
                    bounds.Expand(0.01f);
                    foreach (int frame in new[] { 0, clip.FrameCount / 2, clip.FrameCount - 1 })
                    {
                        clip.DecodeFrame(frame, pos, col, nrm);
                        Assert.IsTrue(pos.All(bounds.Contains), $"{clip.name} frame {frame} is outside its bounds");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(mesh);
                }
            }
        }

        [Test]
        public void SoundLibraryUsesTheProjectSounds()
        {
            var library = Library();
            Assert.IsNotNull(library, "Resources/SoundLibrary is missing");
            Assert.That(library.mainTheme.clip.name, Is.EqualTo("Main theme"));
            Assert.That(library.battleMusic.clip.name, Is.EqualTo("Fighting Song"));
            Assert.That(library.buttonClick.clip.name, Is.EqualTo("ButtonClick"));
            Assert.That(library.footstep.clip.name, Is.EqualTo("FootStep"));
            Assert.That(library.attackSwing.clip.name, Is.EqualTo("Attack_Swing"));
            Assert.That(library.specialCharge.clip.name, Is.EqualTo("Attack_Charged"));
            Assert.That(library.heavyHit.clip.name, Is.EqualTo("Attack_Heavy"));
            Assert.That(library.normalAttacks.Select(s => s.clip.name), Is.EquivalentTo(new[] { "Attack", "Attack_1", "Attack_2", "Attack_3" }));
            Assert.IsTrue(library.normalAttacks.All(s => s.pitchVariance > 0f), "normal attacks need a slight pitch change");
            foreach (var slot in new[] { library.win, library.lose, library.pop, library.guard })
                Assert.IsNotNull(slot.clip);
        }

        [Test]
        public void MusicStreamsAndEffectsLoadUpFront()
        {
            var library = Library();
            foreach (var music in new[] { library.mainTheme, library.battleMusic })
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(music.clip));
                Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.Streaming), music.clip.name);
            }
            foreach (var sfx in new[] { library.buttonClick, library.footstep, library.attackSwing, library.heavyHit })
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sfx.clip));
                Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad), sfx.clip.name);
            }
        }
    }
}

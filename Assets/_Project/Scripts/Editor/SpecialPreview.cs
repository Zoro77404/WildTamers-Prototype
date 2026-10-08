using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WildTamers.Animals;
using WildTamers.Audio;
using WildTamers.Battle;
using WildTamers.Core;
using WildTamers.Vfx;

namespace WildTamers.EditorTools
{
    /// <summary>
    /// "Preview Special" in the AnimalData Inspector: plays an animal's special-attack effect and sounds in the open scene with
    /// the same timing as a battle (charge-up sound → effect → lunge whoosh → impact sound), without starting a fight.
    /// In the Battle scene it uses the real stages (this animal on the middle spot, a stand-in boss on the wild spot);
    /// anywhere else it plays in the middle of the Scene view (its pivot). Works in and out of Play Mode.
    /// </summary>
    public static class SpecialPreview
    {
        private const string BattleScenePath = "Assets/_Project/Scenes/BattleScene.unity";
        private const string TargetPrefab = "Assets/_Project/Prefabs/Animals/ArabianWolf.prefab";

        private struct Cue
        {
            public float Time;
            public System.Action Run;
        }

        private static readonly List<Cue> cues = new List<Cue>();
        private static readonly List<GameObject> temporary = new List<GameObject>();
        private static VfxInstance instance;
        private static double lastTick;
        private static float clock;
        private static float endTime;
        private static bool running;
        private static AudioSource[] voices;
        private static int nextVoice;

        public static bool IsPlaying => running;

        public static void Play(AnimalData data)
        {
            Stop();
            if (data == null) return;
            var fx = data.special;
            var library = Resources.Load<SoundLibrary>(SoundLibrary.ResourcePath);

            // Where: the battle stages if the Battle scene is open, else in front of the Scene view camera.
            Vector3 attackerFeet, targetFeet, targetBody;
            Camera facing;
            float size;
            var controller = Object.FindFirstObjectByType<BattleController>();
            if (controller != null && controller.gameObject.scene.path == BattleScenePath)
            {
                var so = new SerializedObject(controller);
                var wild = so.FindProperty("wildActor").objectReferenceValue as BattleActor;
                var party = so.FindProperty("playerActors");
                var middle = party.arraySize > 1 ? party.GetArrayElementAtIndex(1).objectReferenceValue as BattleActor : null;
                facing = Camera.main;
                attackerFeet = middle != null ? middle.transform.position : Vector3.zero;
                targetFeet = wild != null ? wild.transform.position : Vector3.forward * 8f;
                // Stand-ins (outside Play Mode) so the effect can be judged against animals shown as big as in a fight.
                float attackerHeight = 1.6f, targetHeight = 3.2f;
                if (!Application.isPlaying)
                {
                    attackerHeight = ShowModel(data.prefab, middle);
                    targetHeight = ShowModel(AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefab), wild);
                }
                targetBody = targetFeet + Vector3.up * targetHeight * 0.5f;
                size = SpecialAttackPlayer.SizeFor(fx.spawnAt == VfxAnchor.Attacker ? attackerHeight : targetHeight);
            }
            else
            {
                var view = SceneView.lastActiveSceneView;
                facing = view != null ? view.camera : Camera.main;
                var cam = facing != null ? facing.transform : null;
                var ahead = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
                if (ahead.sqrMagnitude < 0.01f) ahead = Vector3.forward;
                // The point the Scene view orbits around is the middle of what it shows.
                targetFeet = view != null ? view.pivot : (cam != null ? cam.position + ahead * 12f : Vector3.zero);
                attackerFeet = targetFeet - ahead * 6f;
                targetBody = targetFeet + Vector3.up * 1.1f;
                size = 1f;
            }

            // When: the battle's own timing.
            float charge = SpecialAttackPlayer.PlanCharge(fx, BattleActor.SkillCharge, BattleActor.SkillLunge, out float spawnAt);
            float impact = charge + BattleActor.SkillLunge;
            if (library != null) Add(0f, () => PlaySound(library.specialCharge));
            if (fx.vfx != null)
            {
                Add(spawnAt, () =>
                {
                    instance = SpecialAttackPlayer.Spawn(fx, attackerFeet, targetBody, targetFeet, facing, size, manualTick: true);
                    if (instance != null) instance.gameObject.hideFlags = HideFlags.DontSave;
                });
            }
            if (library != null) Add(charge, () => PlaySound(library.attackSwing));
            Add(impact, () =>
            {
                if (fx.sound != null) PlayClip(fx.sound, fx.soundVolume, fx.soundPitch);
                else if (library != null) PlaySound(library.heavyHit);
            });
            endTime = Mathf.Max(impact + 0.6f, spawnAt + SpecialAttackPlayer.Duration(fx) + 0.05f);

            clock = 0f;
            lastTick = EditorApplication.timeSinceStartup;
            running = true;
            EditorApplication.update += Update;
            if (fx.vfx == null) Debug.Log($"[Wild Tamers] {data.name} has no Special VFX set — playing the sounds only.");
        }

        public static void Stop()
        {
            if (running) EditorApplication.update -= Update;
            running = false;
            cues.Clear();
            if (instance != null) instance.Dispose();
            instance = null;
            foreach (var go in temporary) if (go != null) Object.DestroyImmediate(go);
            temporary.Clear();
            if (voices != null)
            {
                foreach (var v in voices) if (v != null) Object.DestroyImmediate(v.gameObject);
                voices = null;
            }
            SceneView.RepaintAll();
        }

        private static void Add(float time, System.Action run) => cues.Add(new Cue { Time = time, Run = run });

        private static void Update()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Min((float)(now - lastTick), 1f / 20f);
            lastTick = now;
            clock += dt;

            foreach (var cue in cues.Where(c => c.Time <= clock).ToList())
            {
                cues.Remove(cue);
                cue.Run();
            }
            if (instance != null) instance.Tick(dt);

            SceneView.RepaintAll();
            if (!Application.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
            if (clock >= endTime && cues.Count == 0) Stop();
        }

        /// <summary>Shows a stand-in model on a battle stage, sized like the stage shows animals; returns its height.</summary>
        private static float ShowModel(GameObject prefab, BattleActor stage)
        {
            if (prefab == null || stage == null) return 2.6f;
            var settings = new SerializedObject(stage);
            float display = settings.FindProperty("displayScale").floatValue;
            float maxHeight = settings.FindProperty("maxHeight").floatValue;
            var go = Object.Instantiate(prefab, stage.transform.position, stage.transform.rotation);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.localScale = Vector3.one * display * stage.transform.lossyScale.y;
            float height = Height(go);
            if (height > maxHeight)
            {
                go.transform.localScale *= maxHeight / height;
                height = maxHeight;
            }
            temporary.Add(go);
            return height;
        }

        private static float Height(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 2.6f;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b.max.y - go.transform.position.y;
        }

        // ---------- Sound (AudioManager in Play Mode, a hidden AudioSource in Edit Mode) ----------

        private static void PlaySound(SoundLibrary.Sound sound)
        {
            if (sound != null) PlayClip(sound.clip, sound.volume, sound.pitch);
        }

        private static void PlayClip(AudioClip clip, float volume, float pitch)
        {
            if (clip == null) return;
            if (Application.isPlaying)
            {
                AudioManager.Instance.PlayClip(clip, volume, pitch, 0f);
                return;
            }
            if (voices == null)
            {
                voices = new AudioSource[4];
                for (int i = 0; i < voices.Length; i++)
                {
                    var go = new GameObject("Special Preview Audio") { hideFlags = HideFlags.HideAndDontSave };
                    voices[i] = go.AddComponent<AudioSource>();
                    voices[i].playOnAwake = false;
                    voices[i].spatialBlend = 0f;
                }
            }
            GameSettings.EnsureLoaded();
            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.clip = clip;
            voice.pitch = pitch;
            voice.volume = Mathf.Clamp01(volume * AudioManager.Loudness(GameSettings.SfxVolume));
            voice.Play();
        }
    }
}

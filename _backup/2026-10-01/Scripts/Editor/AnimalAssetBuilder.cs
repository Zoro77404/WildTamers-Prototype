using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using WildTamers.Animals;
using WildTamers.Core;

namespace WildTamers.EditorTools
{
    /// <summary>
    /// Turns the imported low-poly animal models into game-ready assets:
    /// animation clip copies with proper looping, a shared-state Animator Controller per animal,
    /// a size-normalized prefab with <see cref="AnimalVisual"/>, and the AnimalData / database assets.
    /// Safe to re-run: existing AnimalData stats are kept (only the prefab link is refreshed).
    /// </summary>
    public static class AnimalAssetBuilder
    {
        private const string AnimFolder = "Assets/_Project/Animations";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Animals";
        private const string DataFolder = "Assets/_Project/Data/Animals";
        private const string DatabasePath = "Assets/_Project/Resources/AnimalDatabase.asset";
        private const string ConfigPath = "Assets/_Project/Resources/GameConfig.asset";
        private const string Uaa = "Assets/ThirdParty/Quaternius/UltimateAnimatedAnimals/";
        private const string Eep = "Assets/ThirdParty/Quaternius/EasyEnemyPack/";

        private class Species
        {
            public string Name, Model, Style, Description, Attack, Skill, SkillDescription, Theme;
            public float SkillPower, Size, Weight = 1f;
            public int Cooldown, HP, Atk, Def, Spd;
        }

        private static readonly Species[] Roster =
        {
            new Species { Name = "Fox", Model = Uaa + "Fox.glb", Size = 2.3f, Theme = "#FF8A3D", Weight = 1.2f,
                Style = "Fast & fragile", Description = "A quick, clever fox. It strikes first, but can't take many hits.",
                HP = 32, Atk = 13, Def = 7, Spd = 19, Attack = "Scratch",
                Skill = "Fox Fire", SkillPower = 1.8f, Cooldown = 2, SkillDescription = "A blazing-fast double strike." },
            new Species { Name = "Frog", Model = Eep + "Frog.glb", Size = 2.0f, Theme = "#6BCB3B", Weight = 1.2f,
                Style = "Sturdy & steady", Description = "A bouncy, tough little friend that is hard to knock down.",
                HP = 44, Atk = 11, Def = 14, Spd = 11, Attack = "Tongue Lash",
                Skill = "Big Splash", SkillPower = 2f, Cooldown = 3, SkillDescription = "A belly-flop that soaks the foe." },
            new Species { Name = "Wolf", Model = Uaa + "Wolf.glb", Size = 2.7f, Theme = "#7F95B8", Weight = 1f,
                Style = "Strong hunter", Description = "A fierce pack hunter with a mighty bite.",
                HP = 38, Atk = 16, Def = 9, Spd = 15, Attack = "Bite",
                Skill = "Howling Fang", SkillPower = 2.2f, Cooldown = 3, SkillDescription = "A howl-charged bite." },
            new Species { Name = "Bull", Model = Uaa + "Bull.glb", Size = 3.3f, Theme = "#A47148", Weight = 0.6f,
                Style = "Slow & tanky", Description = "Slow to start, impossible to stop. Shrugs off almost anything.",
                HP = 60, Atk = 15, Def = 16, Spd = 6, Attack = "Headbutt",
                Skill = "Stampede", SkillPower = 2.6f, Cooldown = 4, SkillDescription = "A full-force charge." },
            new Species { Name = "Stag", Model = Uaa + "Stag.glb", Size = 3.1f, Theme = "#D4A373", Weight = 0.8f,
                Style = "Swift glass cannon", Description = "Elegant and swift. Hits very hard but bruises easily.",
                HP = 34, Atk = 18, Def = 7, Spd = 16, Attack = "Antler Jab",
                Skill = "Crown Charge", SkillPower = 2.4f, Cooldown = 3, SkillDescription = "Charges antlers-first." },
            new Species { Name = "Snake", Model = Eep + "Snake.glb", Size = 2.2f, Theme = "#2EC4B6", Weight = 1f,
                Style = "Sneaky burst", Description = "Patient and sneaky. Its venom strike packs a huge punch.",
                HP = 30, Atk = 14, Def = 9, Spd = 13, Attack = "Bite",
                Skill = "Venom Strike", SkillPower = 3f, Cooldown = 4, SkillDescription = "A rare but devastating bite." },
        };

        private static readonly string[] StarterNames = { "Fox", "Frog", "Wolf" };

        // State -> candidate clip names (matched against the part after the last '|').
        private static readonly (string state, bool loop, string[] clips)[] StateClips =
        {
            (AnimalVisual.Idle, true, new[] { "Idle", "Snake_Idle", "Frog_Idle" }),
            (AnimalVisual.Walk, true, new[] { "Walk", "Snake_Walk" }),
            (AnimalVisual.Run, true, new[] { "Gallop" }),
            (AnimalVisual.Attack, false, new[] { "Attack", "Attack_Headbutt", "Snake_Attack", "Frog_Attack" }),
            (AnimalVisual.Hit, false, new[] { "Idle_HitReact_Left" }),
            (AnimalVisual.Death, false, new[] { "Death", "Frog_Death" }),
            (AnimalVisual.Jump, false, new[] { "Jump_ToIdle", "Jump_toIdle", "Snake_Jump", "Frog_Jump" }),
            (AnimalVisual.Eat, true, new[] { "Eating" }),
        };

        [MenuItem("Wild Tamers/Build/Animal Assets")]
        public static void BuildAll()
        {
            foreach (var f in new[] { AnimFolder, PrefabFolder, DataFolder, "Assets/_Project/Resources" })
                Directory.CreateDirectory(f);

            var datas = new List<AnimalData>();
            foreach (var s in Roster)
            {
                var model = AssetDatabase.LoadMainAssetAtPath(s.Model) as GameObject;
                if (model == null)
                {
                    Debug.LogError($"[Wild Tamers] Missing model {s.Model}");
                    continue;
                }
                var clips = BuildClips(s, out var idleClip);
                var controller = BuildController(s, clips);
                var prefab = BuildPrefab(s, model, controller, idleClip);
                datas.Add(BuildData(s, prefab));
            }

            var db = AssetDatabase.LoadAssetAtPath<AnimalDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<AnimalDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }
            var so = new SerializedObject(db);
            SetList(so.FindProperty("animals"), datas);
            SetList(so.FindProperty("starters"), StarterNames.Select(n => datas.FirstOrDefault(d => d.displayName == n)).Where(d => d != null).ToList());
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(db);

            if (AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameConfig>(), ConfigPath);

            AssetDatabase.SaveAssets();
            Debug.Log($"[Wild Tamers] Built {datas.Count} animals.");
        }

        private static Dictionary<string, AnimationClip> BuildClips(Species s, out AnimationClip idle)
        {
            var folder = $"{AnimFolder}/{s.Name}";
            Directory.CreateDirectory(folder);
            var sources = AssetDatabase.LoadAllAssetRepresentationsAtPath(s.Model).OfType<AnimationClip>().ToList();
            var result = new Dictionary<string, AnimationClip>();

            foreach (var (state, loop, candidates) in StateClips)
            {
                AnimationClip src = null;
                foreach (var c in candidates)
                {
                    // Prefer the short-named clip over the 'Armature|' duplicate.
                    src = sources.FirstOrDefault(a => a.name == c) ??
                          sources.FirstOrDefault(a => ShortName(a.name).Equals(c, System.StringComparison.OrdinalIgnoreCase));
                    if (src != null) break;
                }
                if (src == null) continue;

                var path = $"{folder}/{s.Name}_{state}.anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null)
                {
                    clip = Object.Instantiate(src);
                    AssetDatabase.CreateAsset(clip, path);
                }
                else
                {
                    EditorUtility.CopySerialized(src, clip);
                }
                clip.name = $"{s.Name}_{state}";
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = loop;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);
                result[state] = clip;
            }
            result.TryGetValue(AnimalVisual.Idle, out idle);
            return result;
        }

        private static string ShortName(string clipName)
        {
            int i = clipName.LastIndexOf('|');
            return i >= 0 ? clipName.Substring(i + 1) : clipName;
        }

        private static AnimatorController BuildController(Species s, Dictionary<string, AnimationClip> clips)
        {
            var path = $"{AnimFolder}/{s.Name}/{s.Name}.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = controller.layers[0].stateMachine;

            var states = new Dictionary<string, AnimatorState>();
            int i = 0;
            foreach (var (state, _, _) in StateClips)
            {
                if (!clips.TryGetValue(state, out var clip)) continue;
                var st = sm.AddState(state, new Vector3(300f + (i % 2) * 260f, 60f * i, 0f));
                st.motion = clip;
                st.writeDefaultValues = true;
                states[state] = st;
                i++;
            }
            if (states.TryGetValue(AnimalVisual.Idle, out var idle))
            {
                sm.defaultState = idle;
                // One-shot moves return to idle on their own.
                foreach (var oneShot in new[] { AnimalVisual.Attack, AnimalVisual.Hit, AnimalVisual.Jump })
                {
                    if (!states.TryGetValue(oneShot, out var st)) continue;
                    var t = st.AddTransition(idle);
                    t.hasExitTime = true;
                    t.exitTime = 0.92f;
                    t.duration = 0.12f;
                    t.hasFixedDuration = true;
                }
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static GameObject BuildPrefab(Species s, GameObject model, AnimatorController controller, AnimationClip idle)
        {
            var root = new GameObject(s.Name);
            var visual = root.AddComponent<AnimalVisual>();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "Model";
            instance.transform.SetParent(root.transform, false);

            if (idle != null) idle.SampleAnimation(instance, Mathf.Min(0.3f, idle.length * 0.5f));
            var bounds = BakedBounds(instance);
            float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float scale = longest > 0.001f ? s.Size / longest : 1f;
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.localPosition = new Vector3(0f, -bounds.min.y * scale, 0f);

            var animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            foreach (var r in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                r.updateWhenOffscreen = false;
                r.skinnedMotionVectors = false;
            }

            var so = new SerializedObject(visual);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.ApplyModifiedPropertiesWithoutUndo();

            var path = $"{PrefabFolder}/{s.Name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Exact bounds of the posed model (bakes skinned meshes), relative to its root.</summary>
        private static Bounds BakedBounds(GameObject go)
        {
            var root = go.transform;
            bool any = false;
            var b = new Bounds();
            var mesh = new Mesh();
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                // Without "useScale" the skinned result is already in world units (bone matrices carry the scale).
                smr.BakeMesh(mesh, false);
                var m = root.worldToLocalMatrix * Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                foreach (var v in mesh.vertices)
                {
                    var p = m.MultiplyPoint3x4(v);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var m = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var p = m.MultiplyPoint3x4(v);
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            Object.DestroyImmediate(mesh);
            return any ? b : new Bounds(Vector3.up, Vector3.one);
        }

        private static AnimalData BuildData(Species s, GameObject prefab)
        {
            var path = $"{DataFolder}/{s.Name}.asset";
            var data = AssetDatabase.LoadAssetAtPath<AnimalData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<AnimalData>();
                data.id = s.Name.ToLowerInvariant();
                data.displayName = s.Name;
                data.styleLabel = s.Style;
                data.description = s.Description;
                data.themeColor = MaterialLibrary.Hex(s.Theme);
                data.maxHP = s.HP;
                data.attack = s.Atk;
                data.defense = s.Def;
                data.speed = s.Spd;
                data.normalAttackName = s.Attack;
                data.skill = new SkillData { skillName = s.Skill, power = s.SkillPower, cooldownTurns = s.Cooldown, description = s.SkillDescription };
                data.spawnWeight = s.Weight;
                AssetDatabase.CreateAsset(data, path);
            }
            data.prefab = prefab;
            EditorUtility.SetDirty(data);
            return data;
        }

        private static void SetList(SerializedProperty list, List<AnimalData> items)
        {
            list.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }
    }
}

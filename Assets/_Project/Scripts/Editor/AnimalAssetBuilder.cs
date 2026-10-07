using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using WildTamers.Animals;
using WildTamers.Core;
using WildTamers.Map;
using Object = UnityEngine.Object;

namespace WildTamers.EditorTools
{
    /// <summary>
    /// Turns the imported low-poly animal models into game-ready assets:
    /// animation clip copies with proper looping, a shared-state Animator Controller per animal,
    /// a size-normalized prefab with <see cref="AnimalVisual"/> (recolored, with the extras that make each species
    /// readable: camel hump, oryx and gazelle horns, big fox ears), and the AnimalData / database assets.
    /// The roster below is the source of truth: edit it and re-run to refresh every asset.
    /// </summary>
    public static class AnimalAssetBuilder
    {
        private const string AnimFolder = "Assets/_Project/Animations";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Animals";
        private const string DataFolder = "Assets/_Project/Data/Animals";
        private const string MaterialFolder = "Assets/_Project/Materials/Animals";
        private const string MeshFolder = "Assets/_Project/Art/Meshes/Animals";
        private const string DatabasePath = "Assets/_Project/Resources/AnimalDatabase.asset";
        private const string ConfigPath = "Assets/_Project/Resources/GameConfig.asset";
        private const string Uaa = "Assets/ThirdParty/Quaternius/UltimateAnimatedAnimals/";
        private const string Vol2 = "Assets/ThirdParty/Quaternius/AnimalPackVol2/";

        /// <summary>Assets of species that were removed from the game; deleted on every build (exact paths only).</summary>
        private static readonly string[] ObsoleteNames = { "Bull", "Fox", "Frog", "Snake", "Stag", "Wolf" };
        private static readonly string[] ObsoleteModels =
        {
            "Assets/ThirdParty/Quaternius/UltimateAnimatedAnimals/Bull.glb",
            "Assets/ThirdParty/Quaternius/EasyEnemyPack/Frog.glb",
            "Assets/ThirdParty/Quaternius/EasyEnemyPack/Snake.glb",
        };

        private class Species
        {
            public string Id, Name, Model, Theme;
            public float SkillPower, Size, Weight = 1f, Hover, MapScale = 1f;
            /// <summary>Non-uniform tweak of the model's proportions (x = width, y = height, z = length).</summary>
            public Vector3 Stretch = Vector3.one;
            public int Cooldown, HP, Atk, Def, Spd;
            /// <summary>Original material name → new sRGB hex.</summary>
            public Dictionary<string, string> Colors;
            /// <summary>Animator state → candidate clip names (replaces the defaults for that state).</summary>
            public Dictionary<string, string[]> Clips;
            public string[] HideChildren;
            public Action<PosedModel, Species> Customize;
        }

        private static readonly Species[] Roster =
        {
            new Species
            {
                Id = "camel", Name = "Camel", Model = Uaa + "Alpaca.glb", Size = 3.0f, Theme = "#D4A24C", Weight = 0.9f,
                HP = 50, Atk = 12, Def = 12, Spd = 7,
                SkillPower = 2.3f, Cooldown = 4,
                Colors = new Dictionary<string, string>
                {
                    { "Main", "#C99A56" }, { "Main_Light", "#E4C78F" }, { "Main_Dark", "#A57E46" },
                    { "Muzzle", "#7C5E38" }, { "Hooves", "#5E4A34" },
                },
                Clips = new Dictionary<string, string[]> { { AnimalVisual.Attack, new[] { "Attack_Headbutt" } } },
                Stretch = new Vector3(1.05f, 0.9f, 1.12f),
                Customize = AttachCamelHump,
            },
            new Species
            {
                Id = "arabian_horse", Name = "Arabian Horse", Model = Uaa + "WhiteHorse.glb", Size = 3.2f, Theme = "#8FA8C2", Weight = 0.9f,
                HP = 40, Atk = 13, Def = 9, Spd = 17,
                SkillPower = 2.2f, Cooldown = 3,
                Colors = new Dictionary<string, string>
                {
                    { "Main", "#C7CBCC" }, { "Main_Light", "#E8EBEB" }, { "Hair", "#6B7078" },
                    { "Muzzle", "#4A4743" }, { "Hooves", "#3F3B37" },
                },
                Clips = new Dictionary<string, string[]> { { AnimalVisual.Attack, new[] { "Attack_Kick" } } },
            },
            new Species
            {
                Id = "falcon", Name = "Falcon", Model = Vol2 + "Eagle.fbx", Size = 2.5f, Theme = "#C27C3C", Weight = 0.9f, Hover = 0.55f,
                HP = 28, Atk = 16, Def = 6, Spd = 20,
                SkillPower = 2.6f, Cooldown = 3,
                Colors = new Dictionary<string, string>
                {
                    { "Wings", "#70492F" }, { "Head", "#B38D61" }, { "Beak", "#4A4A4A" }, { "Claws", "#E3B84A" },
                },
                Clips = new Dictionary<string, string[]>
                {
                    { AnimalVisual.Idle, new[] { "Flying" } }, { AnimalVisual.Walk, new[] { "Flying" } }, { AnimalVisual.Run, new[] { "Flying" } },
                },
                // Falcon wings are shorter and more pointed than the eagle's.
                Stretch = new Vector3(0.72f, 1f, 1f),
            },
            new Species
            {
                Id = "saluki", Name = "Saluki", Model = Uaa + "Husky.glb", Size = 2.6f, Theme = "#DDAA66", Weight = 1f,
                HP = 34, Atk = 13, Def = 8, Spd = 18,
                SkillPower = 2.2f, Cooldown = 3,
                Colors = new Dictionary<string, string>
                {
                    { "Material", "#D3A363" }, { "Material.001", "#F3E3C3" }, { "Material.006", "#9A7442" },
                },
            },
            new Species
            {
                Id = "arabian_oryx", Name = "Arabian Oryx", Model = Uaa + "Stag.glb", Size = 3.0f, Theme = "#C9A27A", Weight = 0.7f,
                HP = 44, Atk = 14, Def = 12, Spd = 9,
                SkillPower = 2.4f, Cooldown = 4,
                Colors = new Dictionary<string, string>
                {
                    { "Material", "#F4EFE4" }, { "Material.003", "#FFFFFF" }, { "Material.010", "#8A6A50" },
                },
                Clips = new Dictionary<string, string[]> { { AnimalVisual.Attack, new[] { "Attack_Headbutt" } } },
                HideChildren = new[] { "Stag_Horns" },
                Customize = AttachOryxHorns,
            },
            new Species
            {
                Id = "arabian_gazelle", Name = "Arabian Gazelle", Model = Uaa + "Deer.glb", Size = 2.7f, Theme = "#E0A15B", Weight = 1f,
                HP = 30, Atk = 12, Def = 7, Spd = 19,
                SkillPower = 2.2f, Cooldown = 3,
                Colors = new Dictionary<string, string>
                {
                    { "Main", "#CC9C62" }, { "Main_Light", "#F7EEDB" }, { "Main_Dark", "#8C6540" },
                },
                Clips = new Dictionary<string, string[]> { { AnimalVisual.Attack, new[] { "Attack_Kick" } } },
                Customize = AttachGazelleHorns,
            },
            new Species
            {
                Id = "arabian_wolf", Name = "Arabian Wolf", Model = Uaa + "Wolf.glb", Size = 2.7f, Theme = "#A8977C", Weight = 1f,
                HP = 38, Atk = 14, Def = 9, Spd = 15,
                SkillPower = 2.2f, Cooldown = 3,
                Colors = new Dictionary<string, string> { { "Main", "#B8A586" }, { "Main_Light", "#EADFCB" } },
            },
            new Species
            {
                Id = "arabian_fox", Name = "Arabian Fox", Model = Uaa + "Fox.glb", Size = 2.3f, Theme = "#E5A653", Weight = 1.2f,
                HP = 33, Atk = 15, Def = 7, Spd = 17,
                SkillPower = 2.0f, Cooldown = 2,
                Colors = new Dictionary<string, string>
                {
                    { "Main", "#E9CC9B" }, { "Main_Light", "#FFF6E2" }, { "Grey", "#B8A07C" },
                },
                Customize = (pose, s) =>
                {
                    // Big sand-fox ears.
                    foreach (var ear in new[] { "Ear1.L", "Ear1.R" })
                    {
                        var t = pose.Bone(ear);
                        if (t != null) t.localScale = Vector3.one * 1.55f;
                    }
                },
            },
        };

        private static readonly string[] StarterIds = { "camel", "arabian_horse", "falcon" };

        /// <summary>Stat growth per level for every species (balanced with the battle formula in GameConfig).</summary>
        private const float GrowthPerLevel = 0.06f;

        // State -> candidate clip names (matched against the part after the last '|').
        private static readonly (string state, bool loop, string[] clips)[] StateClips =
        {
            (AnimalVisual.Idle, true, new[] { "Idle" }),
            (AnimalVisual.Walk, true, new[] { "Walk" }),
            (AnimalVisual.Run, true, new[] { "Gallop" }),
            (AnimalVisual.Attack, false, new[] { "Attack", "Attack_Headbutt" }),
            (AnimalVisual.Hit, false, new[] { "Idle_HitReact_Left" }),
            (AnimalVisual.Death, false, new[] { "Death" }),
            (AnimalVisual.Jump, false, new[] { "Jump_ToIdle", "Jump_toIdle" }),
            (AnimalVisual.Eat, true, new[] { "Eating" }),
        };

        [MenuItem("Wild Tamers/Build/Animal Assets")]
        public static void BuildAll()
        {
            foreach (var f in new[] { AnimFolder, PrefabFolder, DataFolder, MaterialFolder, MeshFolder, "Assets/_Project/Resources" })
                Directory.CreateDirectory(f);
            DeleteObsolete();

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
            SetList(so.FindProperty("starters"), StarterIds.Select(id => datas.FirstOrDefault(d => d.id == id)).Where(d => d != null).ToList());
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(db);

            if (AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameConfig>(), ConfigPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Wild Tamers] Built {datas.Count} animals.");
        }

        private static void DeleteObsolete()
        {
            foreach (var name in ObsoleteNames)
            {
                bool kept = Roster.Any(r => r.Name == name);
                if (kept) continue;
                AssetDatabase.DeleteAsset($"{PrefabFolder}/{name}.prefab");
                AssetDatabase.DeleteAsset($"{DataFolder}/{name}.asset");
                AssetDatabase.DeleteAsset($"{AnimFolder}/{name}");
            }
            foreach (var model in ObsoleteModels) AssetDatabase.DeleteAsset(model);
        }

        // ------------------------------------------------------------------
        // Animation
        // ------------------------------------------------------------------

        private static Dictionary<string, AnimationClip> BuildClips(Species s, out AnimationClip idle)
        {
            var folder = $"{AnimFolder}/{s.Name.Replace(" ", "")}";
            Directory.CreateDirectory(folder);
            var sources = AssetDatabase.LoadAllAssetRepresentationsAtPath(s.Model).OfType<AnimationClip>().ToList();
            var result = new Dictionary<string, AnimationClip>();

            foreach (var (state, loop, defaults) in StateClips)
            {
                var candidates = s.Clips != null && s.Clips.TryGetValue(state, out var custom) ? custom : defaults;
                AnimationClip src = null;
                foreach (var c in candidates)
                {
                    // Prefer the short-named clip over the 'Armature|' duplicate.
                    src = sources.FirstOrDefault(a => a.name == c) ??
                          sources.FirstOrDefault(a => ShortName(a.name).Equals(c, StringComparison.OrdinalIgnoreCase));
                    if (src != null) break;
                }
                if (src == null) continue;

                var path = $"{folder}/{s.Name.Replace(" ", "")}_{state}.anim";
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
                clip.name = $"{s.Name.Replace(" ", "")}_{state}";
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
            var path = $"{AnimFolder}/{s.Name.Replace(" ", "")}/{s.Name.Replace(" ", "")}.controller";
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

        // ------------------------------------------------------------------
        // Prefab
        // ------------------------------------------------------------------

        private static GameObject BuildPrefab(Species s, GameObject model, AnimatorController controller, AnimationClip idle)
        {
            var fileName = s.Name.Replace(" ", "");
            var root = new GameObject(fileName);
            var visual = root.AddComponent<AnimalVisual>();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "Model";
            instance.transform.SetParent(root.transform, false);

            if (idle != null) idle.SampleAnimation(instance, Mathf.Min(0.3f, idle.length * 0.5f));

            // Recolor, then add the species extras while the model is still at its native size and posed.
            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (s.Colors != null && r is SkinnedMeshRenderer) r.sharedMaterials = Recolor(s, r.sharedMaterials);
            }
            if (s.HideChildren != null)
            {
                foreach (var hide in s.HideChildren)
                {
                    var child = instance.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == hide);
                    if (child != null) child.gameObject.SetActive(false);
                }
            }
            if (s.Customize != null) s.Customize(new PosedModel(instance, fileName), s);
            if (idle != null) idle.SampleAnimation(instance, Mathf.Min(0.3f, idle.length * 0.5f));

            var bounds = BakedBounds(instance);
            var size = Vector3.Scale(bounds.size, s.Stretch);
            float longest = Mathf.Max(size.x, size.y, size.z);
            float scale = longest > 0.001f ? s.Size / longest : 1f;
            instance.transform.localScale = Vector3.Scale(Vector3.one * scale, s.Stretch);
            instance.transform.localPosition = new Vector3(0f, -bounds.min.y * scale * s.Stretch.y + s.Hover, 0f);

            // Not '??': Unity's fake-null for a missing component would skip the AddComponent.
            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
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

            var path = $"{PrefabFolder}/{fileName}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Material[] Recolor(Species s, Material[] source)
        {
            var result = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var src = source[i];
                result[i] = src;
                if (src == null || !s.Colors.TryGetValue(src.name, out var hex)) continue;

                var path = $"{MaterialFolder}/{s.Name.Replace(" ", "")}_{src.name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(src);
                    AssetDatabase.CreateAsset(mat, path);
                }
                else
                {
                    mat.shader = src.shader;
                    mat.CopyPropertiesFromMaterial(src);
                }
                var color = MaterialLibrary.Hex(hex);
                foreach (var prop in new[] { "baseColorFactor", "_BaseColor", "_Color" })
                    if (mat.HasProperty(prop)) mat.SetColor(prop, color);
                // Untextured: drop any baked texture so the flat color shows.
                foreach (var tex in new[] { "baseColorTexture", "_BaseMap", "_MainTex" })
                    if (mat.HasProperty(tex)) mat.SetTexture(tex, null);
                EditorUtility.SetDirty(mat);
                result[i] = mat;
            }
            return result;
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
                if (mf.sharedMesh == null || !mf.gameObject.activeInHierarchy) continue;
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
            var path = $"{DataFolder}/{s.Name.Replace(" ", "")}.asset";
            var data = AssetDatabase.LoadAssetAtPath<AnimalData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<AnimalData>();
                AssetDatabase.CreateAsset(data, path);
            }
            data.id = s.Id;
            data.displayName = s.Name;
            data.themeColor = MaterialLibrary.Hex(s.Theme);
            data.maxHP = s.HP;
            data.attack = s.Atk;
            data.defense = s.Def;
            data.speed = s.Spd;
            data.skill = new SkillData { power = s.SkillPower, cooldownTurns = s.Cooldown };
            data.spawnWeight = s.Weight;
            data.mapScale = s.MapScale;
            data.growthPerLevel = GrowthPerLevel;
            data.prefab = prefab;
            EditorUtility.SetDirty(data);
            return data;
        }

        private static void SetList(SerializedProperty list, List<AnimalData> items)
        {
            list.arraySize = items.Count;
            for (int i = 0; i < items.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        // ------------------------------------------------------------------
        // Species extras (attached to bones so they follow every animation)
        // ------------------------------------------------------------------

        /// <summary>The posed model at its native size: bones by name and the world-space skin vertices.</summary>
        private class PosedModel
        {
            private readonly Transform[] all;
            public readonly GameObject Root;
            public readonly string Name;
            public readonly List<Vector3> Verts = new List<Vector3>();

            public PosedModel(GameObject root, string name)
            {
                Root = root;
                Name = name;
                all = root.GetComponentsInChildren<Transform>(true);
                var mesh = new Mesh();
                foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    smr.BakeMesh(mesh, false);
                    var m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                    foreach (var v in mesh.vertices) Verts.Add(m.MultiplyPoint3x4(v));
                }
                Object.DestroyImmediate(mesh);
            }

            public Transform Bone(string boneName) => all.FirstOrDefault(t => t.name == boneName);

            /// <summary>Shoulder-to-hip distance: a size reference that works for every four-legged model.</summary>
            public float BodyLength()
            {
                var front = Bone("FrontShoulder.L");
                var back = Bone("BackShoulder.L");
                return front != null && back != null ? Mathf.Abs(front.position.z - back.position.z) : 1f;
            }

            /// <summary>Highest skin point over a small patch of the back around a world position.</summary>
            public float BackTop(Vector3 around, float radius)
            {
                float top = float.MinValue;
                foreach (var v in Verts)
                {
                    var d = new Vector2(v.x - around.x, v.z - around.z);
                    if (d.sqrMagnitude < radius * radius) top = Mathf.Max(top, v.y);
                }
                return top == float.MinValue ? around.y : top;
            }
        }

        private static Material AttachmentMaterial(Species s, string part, string hex, float smoothness = 0.1f)
        {
            return MaterialLibrary.Lit($"{MaterialFolder}/{s.Name.Replace(" ", "")}_{part}.mat", MaterialLibrary.Hex(hex), smoothness);
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static GameObject AttachMesh(string name, Mesh mesh, Material material, Transform bone, Vector3 worldPosition)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.position = worldPosition;
            go.transform.SetParent(bone, true);
            return go;
        }

        /// <summary>One big faceted hump on the back (the alpaca body becomes a dromedary).</summary>
        private static void AttachCamelHump(PosedModel pose, Species s)
        {
            var bone = pose.Bone("Torso2") ?? pose.Bone("Back");
            if (bone == null) return;
            float len = pose.BodyLength();
            float top = pose.BackTop(bone.position, len * 0.12f);
            var mb = new MeshBuilder(1);
            mb.Sphere(0, Vector3.zero, new Vector3(len * 0.2f, len * 0.3f, len * 0.3f), 5, 9);
            var mesh = SaveMesh(mb.Build("CamelHump", out _), $"{MeshFolder}/Camel_Hump.asset");
            var color = s.Colors["Main"];
            var center = new Vector3(0f, top - len * 0.02f, bone.position.z + len * 0.04f);
            AttachMesh("Hump", mesh, AttachmentMaterial(s, "Hump", color, 0.1f), bone, center);
            // Camels have small, rounded ears (the alpaca's are tall and pointy).
            foreach (var ear in new[] { "Ear1.L", "Ear1.R" })
            {
                var t = pose.Bone(ear);
                if (t != null) t.localScale = Vector3.one * 0.6f;
            }
        }

        private static void AttachOryxHorns(PosedModel pose, Species s)
        {
            var head = pose.Bone("Head");
            if (head == null) return;
            float len = pose.BodyLength();
            var mesh = new Mesh[2];
            for (int i = 0; i < 2; i++)
                mesh[i] = SaveMesh(BuildHorn("OryxHorn", i == 0 ? -1f : 1f, len * 0.62f, len * 0.026f, 0.42f, 0.12f, 0.1f), $"{MeshFolder}/Oryx_Horn_{(i == 0 ? "L" : "R")}.asset");
            var mat = AttachmentMaterial(s, "Horn", "#3E322C", 0.25f);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                var basePos = head.position + new Vector3(side * len * 0.03f, len * 0.03f, len * 0.055f);
                AttachMesh(i == 0 ? "Horn.L" : "Horn.R", mesh[i], mat, head, basePos);
            }
        }

        private static void AttachGazelleHorns(PosedModel pose, Species s)
        {
            var head = pose.Bone("Head");
            if (head == null) return;
            float len = pose.BodyLength();
            var mesh = new Mesh[2];
            for (int i = 0; i < 2; i++)
                mesh[i] = SaveMesh(BuildHorn("GazelleHorn", i == 0 ? -1f : 1f, len * 0.34f, len * 0.02f, 0.75f, 0.18f, 0.55f), $"{MeshFolder}/Gazelle_Horn_{(i == 0 ? "L" : "R")}.asset");
            var mat = AttachmentMaterial(s, "Horn", "#34302D", 0.25f);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                var basePos = head.position + new Vector3(side * len * 0.028f, len * 0.025f, len * 0.05f);
                AttachMesh(i == 0 ? "Horn.L" : "Horn.R", mesh[i], mat, head, basePos);
            }
        }

        /// <summary>
        /// A tapered, faceted horn growing up from the origin in model space (y up, z forward).
        /// <paramref name="sweep"/> leans it backwards, <paramref name="splay"/> outwards and <paramref name="hook"/> curls the tip.
        /// </summary>
        private static Mesh BuildHorn(string name, float side, float length, float baseRadius, float sweep, float splay, float hook)
        {
            const int segments = 6, sides = 5;
            var centers = new Vector3[segments + 1];
            var radii = new float[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float back = sweep * t + hook * t * t * t;
                centers[i] = new Vector3(side * splay * t * length, t * length * (1f - 0.35f * hook * t), -back * t * length * 0.6f);
                radii[i] = Mathf.Lerp(baseRadius, baseRadius * 0.12f, Mathf.Pow(t, 0.9f));
            }

            var mb = new MeshBuilder(1);
            for (int i = 0; i < segments; i++)
            {
                var axis = (centers[i + 1] - centers[i]).normalized;
                var u = Vector3.Cross(axis, Vector3.right).sqrMagnitude > 0.01f ? Vector3.Cross(axis, Vector3.right).normalized : Vector3.Cross(axis, Vector3.forward).normalized;
                var v = Vector3.Cross(axis, u).normalized;
                for (int k = 0; k < sides; k++)
                {
                    float a0 = k * Mathf.PI * 2f / sides, a1 = (k + 1) * Mathf.PI * 2f / sides;
                    Vector3 Ring(int ring, float a) => centers[ring] + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * radii[ring];
                    // Both windings so the horn is visible from every side whatever the winding convention.
                    mb.Quad(0, Ring(i, a0), Ring(i + 1, a0), Ring(i + 1, a1), Ring(i, a1));
                    mb.Quad(0, Ring(i, a1), Ring(i + 1, a1), Ring(i + 1, a0), Ring(i, a0));
                }
            }
            var mesh = mb.Build(name, out _);
            mesh.name = name + (side < 0f ? "_L" : "_R");
            return mesh;
        }
    }
}

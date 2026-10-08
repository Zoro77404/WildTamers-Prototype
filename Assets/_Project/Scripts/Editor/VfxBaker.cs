using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Formats.Alembic.Importer;
using WildTamers.Animals;
using WildTamers.Vfx;

namespace WildTamers.EditorTools
{
    /// <summary>
    /// Turns the Alembic (.abc) effects in Assets/_Project/VFX into baked <see cref="VfxClip"/>s that play on every platform
    /// (the Alembic package only runs on desktop). Samples every frame at 24 fps, keeps the face sets as parts with their own
    /// material, centers each effect on its impact point and stores 16-bit positions + 8-bit colors/normals.
    /// The .abc files stay untouched; re-run this after replacing one. Materials picked for parts in the Inspector survive a re-bake.
    /// </summary>
    public static class VfxBaker
    {
        public const string BakeFolder = "Assets/_Project/Data/VFX/Baked";
        public const string MaterialFolder = "Assets/_Project/Materials/FX/VFX";
        private const float FrameRate = 24f;

        public enum Look { Toon, ToonShaded, Glow, GlowHot, Soft }

        private struct Source
        {
            public string Abc, Name, PivotPart;
            /// <summary>Lifts the clip (meters) so it lines up with the others of its set (fire burst/end are centered on the flame).</summary>
            public float Lift;
        }

        private static readonly Source[] Sources =
        {
            new Source { Abc = "Assets/_Project/VFX/fire/A_SINGLEBURSTFIRE.abc", Name = "Fire_Burst", Lift = 1.0f },
            new Source { Abc = "Assets/_Project/VFX/fire/A_fireidle.abc", Name = "Fire_Idle" },
            new Source { Abc = "Assets/_Project/VFX/fire/A_fireEnd.abc", Name = "Fire_End", Lift = 1.0f },
            new Source { Abc = "Assets/_Project/VFX/Lroubd Spike Impact/LROUND_SPIKE_IMPACT_VFX.abc", Name = "SpikeImpact", PivotPart = "shockwave_rings" },
            new Source { Abc = "Assets/_Project/VFX/ternedo/Ternedo.abc", Name = "Tornado" },
            new Source { Abc = "Assets/_Project/VFX/Lighting Cloud/LIGHTNING_CLOUD.abc", Name = "LightningCloud", PivotPart = "shock_ring" },
        };

        /// <summary>Face set name → look. Anything not listed is drawn as a flat toon surface.</summary>
        private static readonly Dictionary<string, Look> PartLooks = new Dictionary<string, Look>(StringComparer.OrdinalIgnoreCase)
        {
            // Fire
            { "flame_outer", Look.Toon }, { "flame_core", Look.Toon }, { "flame_tongues", Look.Toon }, { "flame_collapse", Look.Toon },
            { "embers_sparks", Look.Glow }, { "energy_base", Look.Glow }, { "core_glow", Look.GlowHot },
            // Ground spikes
            { "rock_spikes", Look.ToonShaded }, { "floating_rock_debris", Look.ToonShaded }, { "spike_energy_seams", Look.Glow },
            { "shockwave_rings", Look.Glow }, { "ground_energy_fissures", Look.Glow }, { "impact_flash", Look.GlowHot }, { "energy_sparks", Look.Glow },
            // Sand tornado
            { "MAIN_RIBBONS", Look.Toon }, { "INNER_VORTEX", Look.Soft }, { "WISPS", Look.Soft }, { "GOLD_HIGHLIGHTS", Look.Glow },
            { "STREAKS", Look.Glow }, { "PARTICLES", Look.Toon },
            // Lightning cloud
            { "cloud", Look.ToonShaded }, { "debris", Look.ToonShaded }, { "lightning_gold", Look.Toon }, { "lightning_core", Look.GlowHot },
            { "lightning_aura", Look.Glow }, { "ngon", Look.Glow }, { "charge_arcs", Look.GlowHot }, { "charge_aura", Look.Glow },
            { "charge_center", Look.GlowHot }, { "shock_ring", Look.Glow }, { "ring_aura", Look.Glow }, { "impact_rays", Look.Glow }, { "sparks", Look.Glow },
        };

        /// <summary>
        /// "Clip/part" → fade-out window (clip seconds). The .abc files carry no transparency, so parts that should flash and
        /// die away (impact flash, shockwave, glowing cracks) would otherwise stay on screen at full strength.
        /// </summary>
        private static readonly Dictionary<string, Vector2> PartFades = new Dictionary<string, Vector2>
        {
            { "SpikeImpact/impact_flash", new Vector2(0.04f, 0.3f) },
            { "SpikeImpact/shockwave_rings", new Vector2(0.12f, 0.6f) },
            { "SpikeImpact/ground_energy_fissures", new Vector2(0.5f, 0.95f) },
            { "Fire_End/flame_collapse", new Vector2(0.3f, 0.5f) },
            { "Fire_End/core_glow", new Vector2(0.15f, 0.35f) },
        };

        [MenuItem("Wild Tamers/Build/VFX Bakes")]
        public static void BakeAll()
        {
            Directory.CreateDirectory(BakeFolder);
            var looks = BuildMaterials();
            var report = new System.Text.StringBuilder("[Wild Tamers] VFX bakes:\n");
            try
            {
                for (int i = 0; i < Sources.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("Baking VFX", Sources[i].Name, (float)i / Sources.Length);
                    report.AppendLine(Bake(Sources[i], looks));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            BuildEffects();
            AssignSpecials(onlyEmpty: true);
            Debug.Log(report.ToString());
        }

        // ------------------------------------------------------------------
        // Ready-made effects (created once; tune them in the Inspector afterwards)
        // ------------------------------------------------------------------

        public const string EffectFolder = "Assets/_Project/Data/VFX";
        public const string Fire = "VFX_Fire";
        public const string Spikes = "VFX_GroundSpikes";
        public const string Tornado = "VFX_Tornado";
        public const string Lightning = "VFX_LightningCloud";

        public static VfxEffect LoadEffect(string name) => AssetDatabase.LoadAssetAtPath<VfxEffect>($"{EffectFolder}/{name}.asset");

        private static VfxClip Clip(string name) => AssetDatabase.LoadAssetAtPath<VfxClip>($"{BakeFolder}/{name}.asset");

        public static void BuildEffects()
        {
            Directory.CreateDirectory(EffectFolder);

            // Fire set: the burst flares up, the flames burn for a moment, then collapse in a puff of sparks.
            CreateEffect(Fire, hitTime: 0.08f, baseScale: 0.75f, new[]
            {
                new VfxEffect.Layer { clip = Clip("Fire_Burst"), start = 0f, fadeOut = 0.1f },
                new VfxEffect.Layer { clip = Clip("Fire_Idle"), start = 0.2f, hold = 1.0f, fadeIn = 0.14f, fadeOut = 0.12f },
                new VfxEffect.Layer { clip = Clip("Fire_End"), start = 1.1f, fadeOut = 0.12f },
            });
            // Rock spikes burst out of the ground, hold, then sink back.
            CreateEffect(Spikes, hitTime: 0.14f, baseScale: 0.55f, new[]
            {
                new VfxEffect.Layer { clip = Clip("SpikeImpact"), fadeOut = 0.15f },
            });
            // Sand tornado: spins up around the target, whirls, then shrinks away.
            CreateEffect(Tornado, hitTime: 0.3f, baseScale: 0.62f, new[]
            {
                new VfxEffect.Layer { clip = Clip("Tornado"), hold = 1.9f, fadeIn = 0.3f, fadeOut = 0.4f, shrinkOut = true },
            });
            // Storm cloud gathers over the target and the bolt strikes (the slow first second of gathering is skipped).
            CreateEffect(Lightning, hitTime: 1.72f, baseScale: 0.38f, new[]
            {
                new VfxEffect.Layer { clip = Clip("LightningCloud"), clipFrom = 1.2f, clipTo = 4.5f, fadeIn = 0.3f, fadeOut = 0.45f, shrinkOut = true },
            });
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------
        // Default special effect of each animal (only fills animals that have none, so Inspector edits are kept)
        // ------------------------------------------------------------------

        private struct SpecialDefault
        {
            public string Id, Effect, Color;
            public float Strength, Scale, Speed;
            public Vector3 Offset;
        }

        private static readonly SpecialDefault[] Specials =
        {
            // Sandstorm Slam — a heavy stomp: sandstone spikes burst from the ground under the target.
            new SpecialDefault { Id = "camel", Effect = Spikes, Color = "#E0A867", Strength = 0.8f, Scale = 1.05f, Speed = 1f },
            // Spear Charge — horns: pale ivory spikes, narrower and quicker.
            new SpecialDefault { Id = "arabian_oryx", Effect = Spikes, Color = "#F4F1E8", Strength = 0.75f, Scale = 0.85f, Speed = 1.3f },
            // Desert Gallop — "a thundering charge": a storm cloud gathers and a golden bolt strikes.
            new SpecialDefault { Id = "arabian_horse", Effect = Lightning, Color = "#FFFFFF", Strength = 0f, Scale = 1f, Speed = 1.25f },
            // Leap Dash — a quick strike from above: a smaller, faster violet lightning.
            new SpecialDefault { Id = "arabian_gazelle", Effect = Lightning, Color = "#B47CFF", Strength = 0.75f, Scale = 0.85f, Speed = 1.5f },
            // Hunting Dive — the falcon's dive whips up a sand tornado.
            new SpecialDefault { Id = "falcon", Effect = Tornado, Color = "#FFFFFF", Strength = 0f, Scale = 1f, Speed = 1f },
            // Sprint Chase — a burst of speed: a smaller, faster pale wind whirl.
            new SpecialDefault { Id = "saluki", Effect = Tornado, Color = "#BFE3FF", Strength = 0.7f, Scale = 0.75f, Speed = 1.5f },
            // Sand Dash — strikes in a flash: the fire set in its own orange.
            new SpecialDefault { Id = "arabian_fox", Effect = Fire, Color = "#FFFFFF", Strength = 0f, Scale = 1f, Speed = 1.1f },
            // Howling Fang — a howl-charged bite: ghostly blue flames.
            new SpecialDefault { Id = "arabian_wolf", Effect = Fire, Color = "#5FA8FF", Strength = 0.85f, Scale = 1.05f, Speed = 1f },
        };

        [MenuItem("Wild Tamers/Build/Special Effects (fill empty animals)")]
        public static void AssignSpecials() => AssignSpecials(onlyEmpty: true);

        public static void AssignSpecials(bool onlyEmpty)
        {
            var heavy = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Sounds/Attacks/Attack_Heavy.wav");
            foreach (var guid in AssetDatabase.FindAssets("t:AnimalData"))
            {
                var data = AssetDatabase.LoadAssetAtPath<AnimalData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null) continue;
                var def = Array.Find(Specials, s => s.Id == data.id);
                if (def.Id == null) continue;
                if (data.special == null) data.special = new SpecialAttackFx();
                if (onlyEmpty && data.special.vfx != null) continue;
                var fx = data.special;
                fx.vfx = LoadEffect(def.Effect);
                fx.spawnAt = VfxAnchor.Ground;
                fx.offset = def.Offset;
                fx.scale = def.Scale;
                fx.color = MaterialLibrary.Hex(def.Color);
                fx.colorStrength = def.Strength;
                fx.speed = def.Speed;
                fx.delay = 0f;
                if (fx.sound == null) fx.sound = heavy;
                EditorUtility.SetDirty(data);
            }
            AssetDatabase.SaveAssets();
        }

        private static void CreateEffect(string name, float hitTime, float baseScale, VfxEffect.Layer[] layers)
        {
            var path = $"{EffectFolder}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<VfxEffect>(path) != null) return;
            var fx = ScriptableObject.CreateInstance<VfxEffect>();
            fx.layers = layers;
            fx.hitTime = hitTime;
            fx.baseScale = baseScale;
            fx.faceCamera = true;
            AssetDatabase.CreateAsset(fx, path);
        }

        // ------------------------------------------------------------------
        // Materials
        // ------------------------------------------------------------------

        public static Dictionary<Look, Material> BuildMaterials()
        {
            Directory.CreateDirectory(MaterialFolder);
            var shader = Shader.Find("Wild Tamers/VFX Vertex Color");
            var result = new Dictionary<Look, Material>();
            foreach (Look look in Enum.GetValues(typeof(Look)))
            {
                var path = $"{MaterialFolder}/VFX_{look}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, path);
                    // Defaults only on creation, so later Inspector tweaks are kept.
                    switch (look)
                    {
                        case Look.Toon: Set(mat, additive: 0f, zWrite: true, brightness: 1.1f, shade: 0f); break;
                        case Look.ToonShaded: Set(mat, additive: 0f, zWrite: true, brightness: 1.05f, shade: 0.7f); break;
                        case Look.Glow: Set(mat, additive: 1f, zWrite: false, brightness: 1.6f, shade: 0f); break;
                        case Look.GlowHot: Set(mat, additive: 1f, zWrite: false, brightness: 3f, shade: 0f); break;
                        case Look.Soft: Set(mat, additive: 0.3f, zWrite: false, brightness: 1.1f, shade: 0f, opacity: 0.7f, lumaAlpha: 0.35f); break;
                    }
                    EditorUtility.SetDirty(mat);
                }
                result[look] = mat;
            }
            return result;
        }

        private static void Set(Material mat, float additive, bool zWrite, float brightness, float shade, float opacity = 1f, float lumaAlpha = 0f)
        {
            mat.SetFloat("_Additive", additive);
            mat.SetFloat("_ZWrite", zWrite ? 1f : 0f);
            mat.SetFloat("_Brightness", brightness);
            mat.SetFloat("_Shade", shade);
            mat.SetFloat("_Opacity", opacity);
            mat.SetFloat("_LumaAlpha", lumaAlpha);
            mat.SetFloat("_Cull", 0f);
            // Solid parts draw first so glows layer on top of them.
            mat.renderQueue = zWrite ? 2990 : 3000;
        }

        // ------------------------------------------------------------------
        // Bake one file
        // ------------------------------------------------------------------

        private static string Bake(Source source, Dictionary<Look, Material> looks)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source.Abc);
            var clipPath = $"{BakeFolder}/{source.Name}.asset";
            var dataPath = $"{BakeFolder}/{source.Name}.bytes";
            if (prefab == null)
            {
                bool hasBake = File.Exists(dataPath);
                return $"  {source.Name}: source {source.Abc} not found — {(hasBake ? "kept the existing bake" : "NOT BAKED")}.";
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var player = go.GetComponent<AlembicStreamPlayer>();
                var filter = go.GetComponentInChildren<MeshFilter>();
                var custom = go.GetComponentInChildren<AlembicCustomData>();
                if (player == null || filter == null) return $"  {source.Name}: no mesh in {source.Abc}.";
                float unit = 1f / Mathf.Max(1e-6f, player.Settings.ScaleFactor);

                int frames = Mathf.Max(1, Mathf.RoundToInt(player.Duration * FrameRate) + 1);
                var positions = new List<Vector3[]>(frames);
                var colors = new List<Color[]>(frames);
                var normals = new List<Vector3[]>(frames);
                int[][] parts = null;
                int[] firstIndices = null;
                var names = new List<string>();
                int topologyWarnings = 0;

                for (int f = 0; f < frames; f++)
                {
                    player.UpdateImmediately(player.StartTime + Mathf.Min(f / FrameRate, player.Duration));
                    var mesh = filter.sharedMesh;
                    var toRoot = go.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    var v = mesh.vertices;
                    var n = mesh.normals;
                    var c = mesh.colors;
                    for (int i = 0; i < v.Length; i++)
                    {
                        v[i] = toRoot.MultiplyPoint3x4(v[i]) * unit;
                        if (n.Length == v.Length) n[i] = toRoot.MultiplyVector(n[i]).normalized;
                    }
                    if (c.Length != v.Length) { c = new Color[v.Length]; for (int i = 0; i < c.Length; i++) c[i] = Color.white; }
                    if (n.Length != v.Length) n = new Vector3[v.Length];
                    if (positions.Count > 0 && v.Length != positions[0].Length)
                        return $"  {source.Name}: vertex count changes between frames — can't bake this file.";
                    positions.Add(v);
                    colors.Add(c);
                    normals.Add(n);

                    // Face sets: taken from the first frame that has them all (some files drop them on later frames).
                    var all = new List<int>();
                    for (int s = 0; s < mesh.subMeshCount; s++) all.AddRange(mesh.GetIndices(s));
                    if (parts == null || mesh.subMeshCount > parts.Length)
                    {
                        parts = new int[mesh.subMeshCount][];
                        for (int s = 0; s < mesh.subMeshCount; s++) parts[s] = mesh.GetIndices(s);
                        firstIndices = all.ToArray();
                        names = custom != null ? new List<string>(custom.FaceSetNames) : new List<string>();
                    }
                    else if (!SameIndices(all, firstIndices)) topologyWarnings++;
                }

                // Center on the impact point (or the whole effect) on the ground plane.
                var pivotBounds = Measure(positions, parts, names, source.PivotPart);
                var shift = new Vector3(-pivotBounds.center.x, source.Lift, -pivotBounds.center.z);
                foreach (var v in positions)
                    for (int i = 0; i < v.Length; i++) v[i] += shift;

                var bounds = Measure(positions, null, null, null);
                float colorScale = 1e-3f;
                foreach (var c in colors)
                    foreach (var col in c) colorScale = Mathf.Max(colorScale, col.r, col.g, col.b);

                // Loops if the last frame is (nearly) the first one.
                float loopError = 0f;
                var first = positions[0];
                var last = positions[positions.Count - 1];
                for (int i = 0; i < first.Length; i++) loopError = Mathf.Max(loopError, (first[i] - last[i]).magnitude);
                bool loops = loopError < bounds.size.magnitude * 0.02f;

                long bytes = Write(dataPath, positions, colors, normals, parts, bounds, colorScale);
                AssetDatabase.ImportAsset(dataPath);

                var clip = AssetDatabase.LoadAssetAtPath<VfxClip>(clipPath);
                bool isNew = clip == null;
                if (isNew) clip = ScriptableObject.CreateInstance<VfxClip>();
                var old = clip.parts ?? Array.Empty<VfxClip.Part>();
                clip.sourcePath = source.Abc;
                clip.data = AssetDatabase.LoadAssetAtPath<TextAsset>(dataPath);
                clip.parts = new VfxClip.Part[parts.Length];
                for (int p = 0; p < parts.Length; p++)
                {
                    string partName = p < names.Count && !string.IsNullOrEmpty(names[p]) ? names[p] : "other";
                    var kept = Array.Find(old, o => o != null && o.name == partName && o.material != null);
                    if (kept != null)
                    {
                        clip.parts[p] = kept;
                        continue;
                    }
                    var look = PartLooks.TryGetValue(partName, out var l) ? l : partName == "other" ? Look.Glow : Look.Toon;
                    var part = new VfxClip.Part { name = partName, material = looks[look] };
                    if (PartFades.TryGetValue(source.Name + "/" + partName, out var fade))
                    {
                        part.fadeFrom = fade.x;
                        part.fadeTo = fade.y;
                    }
                    clip.parts[p] = part;
                }
                clip.SetInfo(FrameRate, frames, first.Length, bounds, loops);
                // Filled before it is created/saved: a later import in this bake would otherwise reload an empty asset from disk.
                if (isNew) AssetDatabase.CreateAsset(clip, clipPath);
                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssetIfDirty(clip);

                return $"  {source.Name}: {frames} frames, {first.Length} vertices, {parts.Length} parts, {clip.Length:0.00}s, " +
                       $"size {bounds.size.x:0.0}×{bounds.size.y:0.0}×{bounds.size.z:0.0} m, {(loops ? "loops" : "one-shot")}, " +
                       $"{bytes / 1024f / 1024f:0.0} MB" + (topologyWarnings > 0 ? $", {topologyWarnings} frames with different face order" : "");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static bool SameIndices(List<int> a, int[] b)
        {
            if (b == null || a.Count != b.Length) return false;
            for (int i = 0; i < b.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static Bounds Measure(List<Vector3[]> positions, int[][] parts, List<string> names, string part)
        {
            int partIndex = part != null && names != null ? names.IndexOf(part) : -1;
            HashSet<int> only = null;
            if (partIndex >= 0 && parts != null && partIndex < parts.Length) only = new HashSet<int>(parts[partIndex]);
            bool any = false;
            var b = new Bounds();
            foreach (var v in positions)
            {
                for (int i = 0; i < v.Length; i++)
                {
                    if (only != null && !only.Contains(i)) continue;
                    // Skip collapsed (hidden) points sitting exactly at the part's spawn point.
                    if (!any) { b = new Bounds(v[i], Vector3.zero); any = true; }
                    else b.Encapsulate(v[i]);
                }
            }
            if (only != null)
            {
                // Use the frame where the part is biggest (the moment of impact), not the union of its collapsed frames.
                Bounds best = b;
                float bestSize = -1f;
                foreach (var v in positions)
                {
                    bool any2 = false;
                    var fb = new Bounds();
                    foreach (int i in only)
                    {
                        if (!any2) { fb = new Bounds(v[i], Vector3.zero); any2 = true; }
                        else fb.Encapsulate(v[i]);
                    }
                    if (fb.size.sqrMagnitude > bestSize) { bestSize = fb.size.sqrMagnitude; best = fb; }
                }
                return best;
            }
            return b;
        }

        // ------------------------------------------------------------------
        // Binary file (layout read by VfxClip.Parse)
        // ------------------------------------------------------------------

        private static long Write(string path, List<Vector3[]> positions, List<Color[]> colors, List<Vector3[]> normals,
            int[][] parts, Bounds bounds, float colorScale)
        {
            int frames = positions.Count;
            int verts = positions[0].Length;
            var indices = new List<int>();
            var ranges = new List<Vector2Int>();
            foreach (var p in parts)
            {
                ranges.Add(new Vector2Int(indices.Count, p.Length));
                indices.AddRange(p);
            }
            bool wide = verts > 65535;

            int headerSize = 4 * 4 + 4 + 12 + 12 + 4 + 4 + 4 + ranges.Count * 8 + frames * 12;
            int indexBytes = indices.Count * (wide ? 4 : 2);
            int dataStart = Align(headerSize + indexBytes);

            var blocks = new MemoryStream();
            var posOffsets = new int[frames];
            var colOffsets = new int[frames];
            var nrmOffsets = new int[frames];
            byte[] prevPos = null, prevCol = null, prevNrm = null;
            int prevPosOff = 0, prevColOff = 0, prevNrmOff = 0;
            var size = bounds.size;
            var min = bounds.min;
            for (int f = 0; f < frames; f++)
            {
                var pos = new byte[verts * 6];
                var col = new byte[verts * 3];
                var nrm = new byte[verts * 2];
                var v = positions[f];
                var c = colors[f];
                var n = normals[f];
                for (int i = 0; i < verts; i++)
                {
                    WriteU16(pos, i * 6, Quant(v[i].x, min.x, size.x));
                    WriteU16(pos, i * 6 + 2, Quant(v[i].y, min.y, size.y));
                    WriteU16(pos, i * 6 + 4, Quant(v[i].z, min.z, size.z));
                    col[i * 3] = ColorByte(c[i].r, colorScale);
                    col[i * 3 + 1] = ColorByte(c[i].g, colorScale);
                    col[i * 3 + 2] = ColorByte(c[i].b, colorScale);
                    var o = n[i].sqrMagnitude > 1e-8f ? VfxClip.OctEncode(n[i].normalized) : Vector2.zero;
                    nrm[i * 2] = (byte)Mathf.Clamp(Mathf.RoundToInt((o.x * 0.5f + 0.5f) * 255f), 0, 255);
                    nrm[i * 2 + 1] = (byte)Mathf.Clamp(Mathf.RoundToInt((o.y * 0.5f + 0.5f) * 255f), 0, 255);
                }
                posOffsets[f] = Store(blocks, dataStart, pos, ref prevPos, ref prevPosOff);
                colOffsets[f] = Store(blocks, dataStart, col, ref prevCol, ref prevColOff);
                nrmOffsets[f] = Store(blocks, dataStart, nrm, ref prevNrm, ref prevNrmOff);
            }

            using (var file = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(file))
            {
                w.Write(VfxClip.Magic);
                w.Write(VfxClip.Version);
                w.Write(verts);
                w.Write(frames);
                w.Write(FrameRate);
                w.Write(min.x); w.Write(min.y); w.Write(min.z);
                w.Write(size.x); w.Write(size.y); w.Write(size.z);
                w.Write(colorScale);
                w.Write(indices.Count);
                w.Write(ranges.Count);
                foreach (var r in ranges) { w.Write(r.x); w.Write(r.y); }
                foreach (var o in posOffsets) w.Write(o);
                foreach (var o in colOffsets) w.Write(o);
                foreach (var o in nrmOffsets) w.Write(o);
                foreach (var i in indices)
                {
                    if (wide) w.Write(i);
                    else w.Write((ushort)i);
                }
                while (file.Position < dataStart) w.Write((byte)0);
                blocks.Position = 0;
                blocks.CopyTo(file);
                return file.Length;
            }
        }

        /// <summary>Appends a frame's block (4-byte aligned) unless it equals the previous frame's, and returns its file offset.</summary>
        private static int Store(MemoryStream blocks, int dataStart, byte[] block, ref byte[] previous, ref int previousOffset)
        {
            if (previous != null && Equal(previous, block)) return previousOffset;
            while (blocks.Length % 4 != 0) blocks.WriteByte(0);
            int offset = dataStart + (int)blocks.Length;
            blocks.Write(block, 0, block.Length);
            previous = block;
            previousOffset = offset;
            return offset;
        }

        private static bool Equal(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static int Align(int value) => (value + 3) & ~3;

        private static ushort Quant(float value, float min, float size) =>
            size <= 1e-6f ? (ushort)0 : (ushort)Mathf.Clamp(Mathf.RoundToInt((value - min) / size * 65535f), 0, 65535);

        private static byte ColorByte(float value, float scale) =>
            (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(Mathf.Clamp01(value / scale)) * 255f), 0, 255);

        private static void WriteU16(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)(value >> 8);
        }
    }
}

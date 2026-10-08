using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    /// Play-test helper for tuning special attacks in the running Battle scene: puts an animal on a stage, plays its special
    /// exactly like <see cref="BattleController"/> does (same timing helpers, sounds and effect), and saves a strip of game-view
    /// screenshots (UI included) at chosen moments to Temp/Shots. Used through execute_code during development.
    /// </summary>
    public static class VfxShowcase
    {
        public static bool Busy { get; private set; }
        /// <summary>Species shown as the one being hit (null = keep whoever stands there), so shots are comparable.</summary>
        public static string TargetId = "arabian_wolf";
        public static string LastResult { get; private set; }

        /// <summary>
        /// <paramref name="animalId"/> attacks: from the middle party spot at the boss, or (<paramref name="bossAttacks"/>) from the boss spot
        /// at the middle party animal. <paramref name="shots"/> are seconds after the impact (negative = before it).
        /// </summary>
        public static string Run(string animalId, bool bossAttacks, float[] shots, string fileName, int tileWidth = 300)
            => Run(animalId, bossAttacks, shots, fileName, tileWidth, new Rect(0f, 0f, 1f, 1f));

        /// <summary>Same, keeping only <paramref name="crop"/> (normalized screen rect, origin bottom-left) of each screenshot.</summary>
        public static string Run(string animalId, bool bossAttacks, float[] shots, string fileName, int tileWidth, Rect crop)
        {
            if (!Application.isPlaying) return "not playing";
            if (Busy) return "busy";
            var controller = Object.FindFirstObjectByType<BattleController>();
            if (controller == null) return "no battle";
            var so = new SerializedObject(controller);
            var wild = so.FindProperty("wildActor").objectReferenceValue as BattleActor;
            var middle = so.FindProperty("playerActors").GetArrayElementAtIndex(1).objectReferenceValue as BattleActor;
            if (middle == null || !middle.gameObject.activeInHierarchy)
            {
                // One-animal party: it stands on the middle spot anyway; two: use the first used spot.
                var party = so.FindProperty("playerActors");
                for (int i = 0; i < party.arraySize && (middle == null || !middle.gameObject.activeInHierarchy); i++)
                    middle = party.GetArrayElementAtIndex(i).objectReferenceValue as BattleActor;
            }
            var data = GameSession.Instance.Database.Get(animalId);
            if (data == null) return "unknown animal " + animalId;

            var attacker = bossAttacks ? wild : middle;
            var target = bossAttacks ? middle : wild;
            attacker.Spawn(data);
            var targetData = TargetId != null ? GameSession.Instance.Database.Get(TargetId) : null;
            if (targetData != null) target.Spawn(targetData);
            Busy = true;
            LastResult = null;
            var shake = so.FindProperty("cameraShake").objectReferenceValue as Component;
            var camera = shake != null ? shake.GetComponent<Camera>() : Camera.main;
            controller.StartCoroutine(Sequence(controller, attacker, target, data, shots, fileName, tileWidth, crop, camera));
            return "started " + animalId;
        }

        private static readonly Queue<System.Action> queue = new Queue<System.Action>();
        public static readonly List<string> Results = new List<string>();

        /// <summary>Runs <see cref="Run(string,bool,float[],string,int,Rect)"/> for several animals one after another (files "prefix_id").</summary>
        public static string RunAll(string[] ids, bool bossAttacks, float[] shots, string prefix, int tileWidth, Rect crop)
        {
            Results.Clear();
            queue.Clear();
            foreach (var id in ids)
            {
                var animal = id;
                queue.Enqueue(() => Run(animal, bossAttacks, shots, $"{prefix}_{animal}", tileWidth, crop));
            }
            EditorApplication.update -= Pump;
            EditorApplication.update += Pump;
            return $"queued {ids.Length}";
        }

        public static bool QueueDone => queue.Count == 0 && !Busy;

        private static void Pump()
        {
            if (!Application.isPlaying) { EditorApplication.update -= Pump; queue.Clear(); return; }
            if (Busy) return;
            if (LastResult != null && !Results.Contains(LastResult)) Results.Add(LastResult);
            if (queue.Count == 0) { EditorApplication.update -= Pump; return; }
            LastResult = null;
            queue.Dequeue()();
        }

        private static IEnumerator Sequence(BattleController controller, BattleActor attacker, BattleActor target, AnimalData data,
            float[] shots, string fileName, int tileWidth, Rect crop, Camera camera)
        {
            // Let the new model settle into its idle pose.
            for (float t = 0f; t < 0.6f; t += Time.deltaTime) yield return null;

            var fx = data.special;
            float charge = SpecialAttackPlayer.PlanCharge(fx, BattleActor.SkillCharge, BattleActor.SkillLunge, out float spawnAt);
            float impact = charge + BattleActor.SkillLunge;
            // The battle runs on capped game time (UIEase.GameDeltaTime); measure with the same clock so slow frames don't skew the shots.
            float clock = 0f;
            var pending = new List<float>(shots);
            pending.Sort();
            var tiles = new List<Texture2D>();
            var log = new System.Text.StringBuilder($"{data.id}: charge {charge:0.00}s, effect at {spawnAt:0.00}s, impact at {impact:0.00}s, effect {SpecialAttackPlayer.Duration(fx):0.00}s");

            AudioManager.Play(Sfx.Charge);
            bool spawned = false;
            bool attacking = true;
            controller.StartCoroutine(Attack());

            IEnumerator Attack()
            {
                yield return attacker.Attack(target, true, data.themeColor, null, () =>
                {
                    target.TakeHit(attacker.transform.position, false, false);
                    SpecialAttackPlayer.PlayImpactSound(fx);
                    log.Append($" | hit landed at {clock:0.00}s");
                }, charge, () => AudioManager.Play(Sfx.Swing));
                attacking = false;
            }

            while (pending.Count > 0 || attacking)
            {
                float now = clock;
                if (!spawned && now >= spawnAt && fx.vfx != null)
                {
                    spawned = true;
                    var onAnimal = fx.spawnAt == VfxAnchor.Attacker ? attacker : target;
                    var effect = SpecialAttackPlayer.Spawn(fx, attacker.transform.position, target.BodyPoint, target.transform.position, camera,
                        SpecialAttackPlayer.SizeFor(onAnimal.WorldHeight));
                    var hud = new SerializedObject(controller).FindProperty("hud").objectReferenceValue as BattleHUD;
                    if (hud != null && SpecialAttackPlayer.ViewportTop(effect, camera) > hud.WildCardViewportRect.yMin)
                    {
                        hud.DimWildCard(SpecialAttackPlayer.CardDimTime(fx, impact - now));
                        log.Append(" | boss card dimmed");
                    }
                    log.Append($" | effect spawned at {now:0.00}s (size x{SpecialAttackPlayer.SizeFor(onAnimal.WorldHeight):0.00}, on {onAnimal.WorldHeight:0.0} m animal)");
                }
                if (pending.Count > 0 && now >= impact + pending[0])
                {
                    pending.RemoveAt(0);
                    yield return new WaitForEndOfFrame();
                    var shot = ScreenCapture.CaptureScreenshotAsTexture();
                    tiles.Add(Shrink(shot, tileWidth, crop));
                    Object.Destroy(shot);
                    continue;
                }
                yield return null;
                clock += UI.UIEase.GameDeltaTime;
            }

            if (tiles.Count > 0)
            {
                int w = tiles[0].width, h = tiles[0].height;
                var sheet = new Texture2D(w * tiles.Count, h, TextureFormat.RGB24, false);
                for (int i = 0; i < tiles.Count; i++)
                {
                    sheet.SetPixels(i * w, 0, w, h, tiles[i].GetPixels());
                    Object.Destroy(tiles[i]);
                }
                sheet.Apply();
                Directory.CreateDirectory(VfxTestBench.ShotFolder);
                File.WriteAllBytes($"{VfxTestBench.ShotFolder}/{fileName}.png", sheet.EncodeToPNG());
                Object.Destroy(sheet);
            }
            LastResult = log.ToString();
            Busy = false;
        }

        /// <summary>Crops and box-filters on the CPU (a GPU blit would re-apply the color-space conversion and wash the colors out).</summary>
        private static Texture2D Shrink(Texture2D source, int width, Rect crop)
        {
            var src = source.GetPixels32();
            int sw = source.width, sh = source.height;
            int x0 = Mathf.RoundToInt(crop.x * sw), y0 = Mathf.RoundToInt(crop.y * sh);
            int cw = Mathf.RoundToInt(crop.width * sw), ch = Mathf.RoundToInt(crop.height * sh);
            int height = Mathf.Max(1, Mathf.RoundToInt(width * (float)ch / cw));
            var dst = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                int sy0 = y0 + y * ch / height, sy1 = Mathf.Max(sy0 + 1, y0 + (y + 1) * ch / height);
                for (int x = 0; x < width; x++)
                {
                    int sx0 = x0 + x * cw / width, sx1 = Mathf.Max(sx0 + 1, x0 + (x + 1) * cw / width);
                    int r = 0, g = 0, b = 0, n = 0;
                    for (int yy = sy0; yy < sy1 && yy < sh; yy++)
                        for (int xx = sx0; xx < sx1 && xx < sw; xx++)
                        {
                            var c = src[yy * sw + xx];
                            r += c.r; g += c.g; b += c.b; n++;
                        }
                    n = Mathf.Max(1, n);
                    dst[y * width + x] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
                }
            }
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.SetPixels32(dst);
            tex.Apply();
            return tex;
        }
    }
}

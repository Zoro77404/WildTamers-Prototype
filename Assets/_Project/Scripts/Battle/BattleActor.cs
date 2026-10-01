using System;
using System.Collections;
using UnityEngine;
using WildTamers.Animals;
using WildTamers.UI;

namespace WildTamers.Battle
{
    /// <summary>
    /// The 3D animal on one side of the arena. Spawns the model and plays every battle motion:
    /// entrance, attack lunge, hit flash and shake, guard bubble, fainting, joining the team and running away.
    /// Hierarchy: Spot (this, faces the foe) → Motion (lunges, shakes, falls) → Hop (idle bounce) → Model.
    /// </summary>
    public class BattleActor : MonoBehaviour
    {
        [Header("Model")]
        [Tooltip("Display scale of the animal model.")]
        [SerializeField] private float displayScale = 1.7f;
        [Tooltip("Tall animals (antlers, coiled snake) shrink to this height so they stay in frame and clear of the UI.")]
        [SerializeField] private float maxHeight = 3.4f;

        [Header("Parts")]
        [SerializeField] private Transform blobShadow;
        [SerializeField] private Transform guardDome;
        [SerializeField] private Material flashMaterial;
        [SerializeField] private ParticleSystem puffPrefab;

        [Header("Feel")]
        [SerializeField] private float lungeShare = 0.42f;
        [SerializeField] private float skillLungeShare = 0.55f;

        private Transform motion;
        private Transform hop;
        private IdleHop idleHop;
        private AnimalVisual visual;
        private Renderer[] renderers;
        private Material[][] originalMaterials;
        private Vector3 shadowScale = Vector3.one;
        private Vector3 baseScale = Vector3.one;
        private float height = 1.5f;
        private float length = 1.5f;
        private Vector3 domeScale = Vector3.one * 2f;
        private Coroutine flashRoutine;
        private Coroutine shakeRoutine;
        private Coroutine guardRoutine;

        public bool HasModel => visual != null;
        public AnimalVisual Visual => visual;

        /// <summary>Point just above the animal's head (for damage numbers).</summary>
        public Vector3 HeadPoint => motion != null ? motion.position + Vector3.up * (height * transform.lossyScale.y + 0.35f) : transform.position;

        /// <summary>Middle of the body (for hit sparks).</summary>
        public Vector3 BodyPoint => motion != null ? motion.position + Vector3.up * (height * 0.5f * transform.lossyScale.y) : transform.position;

        // ------------------------------------------------------------------
        // Setup
        // ------------------------------------------------------------------

        public void Spawn(AnimalData data)
        {
            if (motion != null) Destroy(motion.gameObject);
            motion = new GameObject("Motion").transform;
            motion.SetParent(transform, false);
            hop = new GameObject("Hop").transform;
            hop.SetParent(motion, false);
            idleHop = hop.gameObject.AddComponent<IdleHop>();

            if (blobShadow != null) shadowScale = blobShadow.localScale;
            if (guardDome != null) guardDome.gameObject.SetActive(false);
            if (data == null || data.prefab == null) return;

            var model = Instantiate(data.prefab, hop);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * displayScale;
            visual = model.GetComponent<AnimalVisual>();
            renderers = model.GetComponentsInChildren<Renderer>();
            originalMaterials = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++) originalMaterials[i] = renderers[i].sharedMaterials;

            if (visual != null) visual.PlayIdle(randomStart: true);
            var b = MeasureLocalBounds(model);
            if (b.size.y > maxHeight)
            {
                model.transform.localScale *= maxHeight / b.size.y;
                b = MeasureLocalBounds(model);
            }
            height = b.size.y;
            length = Mathf.Max(b.size.x, b.size.z);
            domeScale = new Vector3(b.size.x * 0.62f + 0.4f, b.size.y * 0.62f + 0.45f, b.size.z * 0.6f + 0.4f);
            if (guardDome != null)
            {
                // Bubble big enough for the whole animal, centered on its body.
                guardDome.localPosition = new Vector3(b.center.x, b.size.y * 0.42f, b.center.z);
                guardDome.localScale = domeScale;
            }
        }

        /// <summary>Exact bounds of the posed model in this spot's local space (bakes skinned meshes; renderer bounds are padded).</summary>
        private Bounds MeasureLocalBounds(GameObject model)
        {
            var toLocal = transform.worldToLocalMatrix;
            bool any = false;
            var bounds = new Bounds(Vector3.up, Vector3.one);
            var mesh = new Mesh();
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                // Without "useScale" the baked vertices are already scaled (bone matrices carry it).
                smr.BakeMesh(mesh, false);
                var m = toLocal * Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                foreach (var v in mesh.vertices) Encapsulate(ref bounds, ref any, m.MultiplyPoint3x4(v));
            }
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var m = toLocal * mf.transform.localToWorldMatrix;
                foreach (var v in mf.sharedMesh.vertices) Encapsulate(ref bounds, ref any, m.MultiplyPoint3x4(v));
            }
            Destroy(mesh);
            return bounds;
        }

        private static void Encapsulate(ref Bounds bounds, ref bool any, Vector3 point)
        {
            if (!any)
            {
                bounds = new Bounds(point, Vector3.zero);
                any = true;
            }
            else bounds.Encapsulate(point);
        }

        // ------------------------------------------------------------------
        // Entrance / exit
        // ------------------------------------------------------------------

        public IEnumerator Enter(float delay, bool fromAbove)
        {
            var rest = transform.localPosition;
            transform.localScale = Vector3.zero;
            for (float t = 0f; t < delay; t += UIEase.GameDeltaTime) yield return null;
            const float duration = 0.55f;
            for (float t = 0f; t < duration; t += UIEase.GameDeltaTime)
            {
                float p = t / duration;
                float s = UIEase.OutBack(p);
                transform.localScale = new Vector3(s, s, s);
                transform.localPosition = rest + Vector3.up * ((1f - UIEase.OutCubic(p)) * (fromAbove ? 3f : 0f));
                yield return null;
            }
            transform.localScale = Vector3.one;
            transform.localPosition = rest;
            if (idleHop != null) idleHop.HopNow(0.8f);
        }

        // ------------------------------------------------------------------
        // Attacks
        // ------------------------------------------------------------------

        /// <summary>
        /// Lunge at the target. <paramref name="onImpact"/> runs at the moment of contact (apply damage there).
        /// Skills charge up first and leap in an arc.
        /// </summary>
        public IEnumerator Attack(BattleActor target, bool skill, Color skillColor, BattleSparks sparks, Action onImpact)
        {
            if (motion == null) { onImpact?.Invoke(); yield break; }

            var toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            var dirLocal = transform.InverseTransformDirection(toTarget.normalized);
            dirLocal.y = 0f;
            float reach = Mathf.Max(0.5f, distance * (skill ? skillLungeShare : lungeShare) - 0.4f) / Mathf.Max(0.01f, transform.lossyScale.x);

            if (skill)
            {
                // Charge: crouch, sparks swirl at the feet.
                if (sparks != null) sparks.Ring(transform.position + Vector3.up * 0.3f, skillColor, length * 0.6f + 0.6f, 22);
                yield return Squash(0.32f, 0.82f);
            }
            else
            {
                yield return Move(Vector3.zero, -dirLocal * 0.35f, 0.1f, Ease.Out, 0f);
            }

            if (visual != null) visual.Play(AnimalVisual.Attack, 0.06f);
            yield return Move(skill ? Vector3.zero : -dirLocal * 0.35f, dirLocal * reach, skill ? 0.2f : 0.13f, Ease.In, skill ? 1.1f : 0f);

            onImpact?.Invoke();
            for (float t = 0f; t < 0.09f; t += UIEase.GameDeltaTime) yield return null;

            yield return Move(dirLocal * reach, Vector3.zero, 0.32f, Ease.InOut, 0f);
        }

        public void TakeHit(Vector3 fromPosition, bool critical, bool guarded)
        {
            if (motion == null) return;
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(Flash(critical ? 3 : 2));
            if (shakeRoutine != null) StopCoroutine(shakeRoutine);
            var away = transform.position - fromPosition;
            away.y = 0f;
            shakeRoutine = StartCoroutine(Shake(transform.InverseTransformDirection(away.normalized), critical ? 0.4f : guarded ? 0.14f : 0.24f));

            if (guarded && guardDome != null && guardDome.gameObject.activeSelf)
            {
                if (guardRoutine != null) StopCoroutine(guardRoutine);
                guardRoutine = StartCoroutine(GuardPunch());
            }
            if (visual == null || !visual.Play(AnimalVisual.Hit, 0.05f))
            {
                if (idleHop != null) idleHop.HopNow(0.35f);
            }
        }

        // ------------------------------------------------------------------
        // Guard
        // ------------------------------------------------------------------

        public void SetGuard(bool on)
        {
            if (guardDome == null) return;
            if (on == guardDome.gameObject.activeSelf && guardRoutine == null) return;
            if (guardRoutine != null) StopCoroutine(guardRoutine);
            guardRoutine = StartCoroutine(on ? GuardIn() : GuardOut());
        }

        private IEnumerator GuardIn()
        {
            guardDome.gameObject.SetActive(true);
            var full = DomeScale();
            for (float t = 0f; t < 0.3f; t += UIEase.GameDeltaTime)
            {
                guardDome.localScale = full * UIEase.OutBack(t / 0.3f, 2.4f);
                yield return null;
            }
            guardDome.localScale = full;
            guardRoutine = null;
        }

        private IEnumerator GuardOut()
        {
            if (!guardDome.gameObject.activeSelf) { guardRoutine = null; yield break; }
            var start = guardDome.localScale;
            for (float t = 0f; t < 0.18f; t += UIEase.GameDeltaTime)
            {
                float p = t / 0.18f;
                guardDome.localScale = start * (1f + 0.15f * Mathf.Sin(p * Mathf.PI)) * (1f - UIEase.InCubic(p));
                yield return null;
            }
            guardDome.gameObject.SetActive(false);
            guardDome.localScale = DomeScale();
            guardRoutine = null;
        }

        private IEnumerator GuardPunch()
        {
            var full = DomeScale();
            for (float t = 0f; t < 0.25f; t += UIEase.GameDeltaTime)
            {
                float p = t / 0.25f;
                guardDome.localScale = full * (1f + 0.12f * Mathf.Sin(p * Mathf.PI) * (1f - p));
                yield return null;
            }
            guardDome.localScale = full;
            guardRoutine = null;
        }

        private Vector3 DomeScale() => domeScale;

        // ------------------------------------------------------------------
        // Faint / join / run
        // ------------------------------------------------------------------

        public IEnumerator Faint()
        {
            SetGuard(false);
            if (idleHop != null) idleHop.enabled = false;
            if (hop != null) { hop.localPosition = Vector3.zero; hop.localScale = Vector3.one; }

            if (visual != null && visual.Play(AnimalVisual.Death, 0.08f))
            {
                for (float t = 0f; t < 1.05f; t += UIEase.GameDeltaTime) yield return null;
            }
            else
            {
                // No death clip (snake): flop over onto its side with a little bounce.
                var start = motion.localRotation;
                var end = start * Quaternion.Euler(0f, 0f, 88f);
                for (float t = 0f; t < 0.42f; t += UIEase.GameDeltaTime)
                {
                    float p = UIEase.InCubic(t / 0.42f);
                    motion.localRotation = Quaternion.SlerpUnclamped(start, end, p);
                    motion.localPosition = new Vector3(0f, Mathf.Sin(t / 0.42f * Mathf.PI) * 0.4f + p * length * 0.12f, 0f);
                    yield return null;
                }
                for (float t = 0f; t < 0.2f; t += UIEase.GameDeltaTime)
                {
                    float p = t / 0.2f;
                    motion.localRotation = Quaternion.SlerpUnclamped(start, end, 1f + 0.08f * Mathf.Sin(p * Mathf.PI));
                    yield return null;
                }
                motion.localRotation = end;
            }
            // Shadow fades as the animal lies down.
            if (blobShadow != null)
            {
                var from = blobShadow.localScale;
                for (float t = 0f; t < 0.3f; t += UIEase.GameDeltaTime)
                {
                    blobShadow.localScale = Vector3.Lerp(from, shadowScale * 0.75f, t / 0.3f);
                    yield return null;
                }
            }
        }

        /// <summary>The beaten wild animal gets back up, happy, to join the team.</summary>
        public IEnumerator Revive(BattleSparks sparks, Color color)
        {
            Puff();
            if (sparks != null) sparks.Burst(BodyPoint, color, 26, 1.4f);
            motion.localRotation = Quaternion.identity;
            motion.localPosition = Vector3.zero;
            if (blobShadow != null) blobShadow.localScale = shadowScale;
            if (visual != null) visual.PlayIdle(randomStart: false);
            if (idleHop != null)
            {
                idleHop.enabled = true;
                idleHop.Energy = 1.6f;
            }
            for (float t = 0f; t < 0.45f; t += UIEase.GameDeltaTime)
            {
                float s = Mathf.LerpUnclamped(0.4f, 1f, UIEase.OutBack(t / 0.45f, 2.2f));
                motion.localScale = new Vector3(s, s, s);
                yield return null;
            }
            motion.localScale = Vector3.one;
            if (idleHop != null) idleHop.HopNow(1.5f);
            if (visual != null) visual.Play(AnimalVisual.Jump, 0.1f);
        }

        /// <summary>Victory hops.</summary>
        public IEnumerator Celebrate()
        {
            if (idleHop != null)
            {
                idleHop.Energy = 1.8f;
                idleHop.HopNow(1.3f);
            }
            if (visual != null) visual.Play(AnimalVisual.Jump, 0.1f);
            for (float t = 0f; t < 0.6f; t += UIEase.GameDeltaTime) yield return null;
        }

        /// <summary>Turns tail and dashes off (successful escape).</summary>
        public IEnumerator RunAway()
        {
            SetGuard(false);
            yield return Turn(180f, 0.2f);
            if (visual != null && !visual.Play(AnimalVisual.Run, 0.08f)) visual.Play(AnimalVisual.Walk, 0.08f);
            var back = Quaternion.Euler(0f, 180f, 0f) * Vector3.forward;
            for (float t = 0f; t < 0.7f; t += UIEase.GameDeltaTime)
            {
                float p = t / 0.7f;
                motion.localPosition = back * (UIEase.InCubic(p) * 9f);
                float s = 1f - 0.4f * p;
                motion.localScale = new Vector3(s, s, s);
                yield return null;
            }
            motion.gameObject.SetActive(false);
            if (blobShadow != null) blobShadow.gameObject.SetActive(false);
        }

        /// <summary>Tries to flee but trips: half turn, startled hop, back to facing the foe.</summary>
        public IEnumerator Stumble()
        {
            yield return Turn(120f, 0.16f);
            if (idleHop != null) idleHop.HopNow(0.7f);
            for (float t = 0f; t < 0.3f; t += UIEase.GameDeltaTime) yield return null;
            yield return Turn(0f, 0.22f);
        }

        // ------------------------------------------------------------------
        // Building blocks
        // ------------------------------------------------------------------

        private enum Ease { In, Out, InOut }

        private IEnumerator Move(Vector3 from, Vector3 to, float duration, Ease ease, float arcHeight)
        {
            for (float t = 0f; t < duration; t += UIEase.GameDeltaTime)
            {
                float p = t / duration;
                float e = ease == Ease.In ? UIEase.InCubic(p) : ease == Ease.Out ? UIEase.OutCubic(p) : Mathf.SmoothStep(0f, 1f, p);
                motion.localPosition = Vector3.LerpUnclamped(from, to, e) + Vector3.up * (Mathf.Sin(p * Mathf.PI) * arcHeight);
                yield return null;
            }
            motion.localPosition = to;
        }

        private IEnumerator Squash(float duration, float amount)
        {
            for (float t = 0f; t < duration; t += UIEase.GameDeltaTime)
            {
                float p = t / duration;
                float sy = Mathf.Lerp(1f, amount, Mathf.Sin(p * Mathf.PI * 0.5f));
                float sxz = 1f / Mathf.Sqrt(sy);
                motion.localScale = new Vector3(sxz, sy, sxz);
                yield return null;
            }
            motion.localScale = Vector3.one;
        }

        private IEnumerator Turn(float yaw, float duration)
        {
            var start = motion.localRotation;
            var end = Quaternion.Euler(0f, yaw, 0f);
            for (float t = 0f; t < duration; t += UIEase.GameDeltaTime)
            {
                motion.localRotation = Quaternion.Slerp(start, end, UIEase.OutCubic(t / duration));
                yield return null;
            }
            motion.localRotation = end;
        }

        private IEnumerator Shake(Vector3 awayLocal, float amplitude)
        {
            const float duration = 0.34f;
            var squashBase = Vector3.one;
            for (float t = 0f; t < duration; t += UIEase.GameDeltaTime)
            {
                float p = t / duration;
                float damp = 1f - p;
                var side = Vector3.Cross(Vector3.up, awayLocal);
                var jitter = side * (Mathf.Sin(t * 85f) * amplitude * damp) + awayLocal * (Mathf.Sin(p * Mathf.PI) * amplitude * 1.2f);
                motion.localPosition = new Vector3(jitter.x, 0f, jitter.z);
                float sy = 1f - 0.12f * Mathf.Sin(p * Mathf.PI) * damp;
                motion.localScale = new Vector3(1f / Mathf.Sqrt(sy), sy, 1f / Mathf.Sqrt(sy));
                yield return null;
            }
            motion.localPosition = Vector3.zero;
            motion.localScale = squashBase;
            shakeRoutine = null;
        }

        private IEnumerator Flash(int blinks)
        {
            if (renderers == null || flashMaterial == null) yield break;
            for (int i = 0; i < blinks; i++)
            {
                SetFlash(true);
                for (float t = 0f; t < 0.055f; t += UIEase.GameDeltaTime) yield return null;
                SetFlash(false);
                for (float t = 0f; t < 0.05f; t += UIEase.GameDeltaTime) yield return null;
            }
            flashRoutine = null;
        }

        private void SetFlash(bool on)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                if (on)
                {
                    var mats = new Material[originalMaterials[i].Length];
                    for (int m = 0; m < mats.Length; m++) mats[m] = flashMaterial;
                    renderers[i].sharedMaterials = mats;
                }
                else renderers[i].sharedMaterials = originalMaterials[i];
            }
        }

        public void Puff()
        {
            if (puffPrefab == null) return;
            var p = Instantiate(puffPrefab, transform.position + Vector3.up * 0.4f, Quaternion.identity);
            var main = p.main;
            main.stopAction = ParticleSystemStopAction.Destroy;
            p.Play();
        }

        private void OnDisable()
        {
            if (renderers != null && originalMaterials != null) SetFlash(false);
        }
    }
}

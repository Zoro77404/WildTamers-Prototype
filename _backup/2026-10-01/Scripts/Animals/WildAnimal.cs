using System.Collections;
using UnityEngine;
using WildTamers.Core;
using WildTamers.Map;

namespace WildTamers.Animals
{
    /// <summary>
    /// A wild animal standing on the map. Wraps the species model with tap collider, blob shadow,
    /// "in range" ring, pop-in / leave animations and idle life (hops, looking around).
    /// </summary>
    public class WildAnimal : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Transform modelRoot;
        [SerializeField] private IdleHop hop;
        [SerializeField] private Transform blobShadow;
        [SerializeField] private MeshRenderer rangeRing;
        [SerializeField] private CapsuleCollider tapCollider;
        [SerializeField] private ParticleSystem puffPrefab;

        [Header("Feel")]
        [SerializeField] private float popInDuration = 0.5f;
        [SerializeField] private float leaveDuration = 0.35f;
        [SerializeField] private float turnSpeed = 140f;
        [SerializeField] private Color rangeRingColor = new Color(1f, 1f, 1f, 0.95f);

        private AnimalVisual visual;
        private MaterialPropertyBlock block;
        private Transform player;
        private float appear = 1f;
        private float targetYaw;
        private float lookTimer;
        private float ringAlpha;
        private float footprint = 1f;

        public WildSpawnRecord Record { get; private set; }
        public AnimalData Data { get; private set; }
        public int SpawnId => Record != null ? Record.id : -1;
        public int Level => Record != null ? Record.level : 1;
        public bool InRange { get; private set; }
        /// <summary>True while an encounter popup is open for this animal (it won't wander off).</summary>
        public bool IsEngaged { get; set; }
        public bool IsLeaving { get; private set; }

        public void Init(WildSpawnRecord record, AnimalData data, Transform playerTransform, bool popIn)
        {
            Record = record;
            Data = data;
            player = playerTransform;
            block = new MaterialPropertyBlock();
            gameObject.name = $"Wild {data.displayName} Lv{record.level} #{record.id}";
            GameLayers.SetLayerRecursively(gameObject, GameLayers.Animals);

            transform.rotation = Quaternion.Euler(0f, record.yaw, 0f);
            targetYaw = record.yaw;
            lookTimer = Random.Range(1f, 4f);

            if (data.prefab != null)
            {
                var model = Instantiate(data.prefab, modelRoot);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                float scale = data.mapScale * (GameSession.Exists ? GameSession.Instance.Config.wildAnimalScale : 1f);
                model.transform.localScale = Vector3.one * scale;
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                visual = model.GetComponent<AnimalVisual>();
                if (visual != null) visual.PlayIdle(randomStart: true);
            }

            FitToModel();

            if (rangeRing != null)
            {
                rangeRing.GetComponent<MeshFilter>().sharedMesh = RingMesh.Create(0.86f, 1f, 48, withFill: false, "AnimalRing");
                rangeRing.transform.localScale = new Vector3(footprint * 0.75f + 0.6f, 1f, footprint * 0.75f + 0.6f);
                SetRingAlpha(0f);
            }

            if (popIn)
            {
                appear = 0f;
                transform.localScale = Vector3.zero;
                Puff();
            }
        }

        /// <summary>Called by the spawner every frame with whether the player can fight this animal.</summary>
        public void SetInRange(bool inRange)
        {
            if (InRange == inRange) return;
            InRange = inRange;
            if (hop != null) hop.Energy = inRange ? 1.9f : 1f;
            if (inRange) lookTimer = 0f; // turn toward the player right away
        }

        /// <summary>Little startled hop (tapped from too far away).</summary>
        public void ReactTooFar()
        {
            if (hop != null) hop.HopNow(0.55f);
        }

        /// <summary>Excited hop (encounter opened).</summary>
        public void ReactSelected()
        {
            if (hop != null) hop.HopNow(1.35f);
            if (visual != null && visual.HasState(AnimalVisual.Jump) == false) visual.Play(AnimalVisual.Attack, 0.1f);
        }

        /// <summary>Plays the leave animation and destroys the object.</summary>
        public void Leave()
        {
            if (IsLeaving) return;
            IsLeaving = true;
            if (tapCollider != null) tapCollider.enabled = false;
            StartCoroutine(LeaveRoutine());
        }

        private IEnumerator LeaveRoutine()
        {
            Puff();
            var start = transform.localScale;
            for (float t = 0f; t < leaveDuration; t += Time.deltaTime)
            {
                float p = t / leaveDuration;
                // Quick stretch up, then shrink away.
                float s = p < 0.25f ? 1f + p * 0.6f : Mathf.Lerp(1.15f, 0f, (p - 0.25f) / 0.75f);
                transform.localScale = new Vector3(start.x * (2f - Mathf.Max(1f, s)) * Mathf.Min(1f, s), start.y * s, start.z * (2f - Mathf.Max(1f, s)) * Mathf.Min(1f, s));
                yield return null;
            }
            Destroy(gameObject);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (appear < 1f && !IsLeaving)
            {
                appear = Mathf.Min(1f, appear + dt / popInDuration);
                transform.localScale = Vector3.one * EaseOutBack(appear);
            }

            // Look around; face the player when close.
            lookTimer -= dt;
            if (lookTimer <= 0f)
            {
                lookTimer = Random.Range(2.2f, 5f);
                if (InRange && player != null)
                {
                    var to = player.position - transform.position;
                    targetYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                }
                else
                {
                    targetYaw = (Record != null ? Record.yaw : 0f) + Random.Range(-75f, 75f);
                }
            }
            var e = transform.eulerAngles;
            e.y = Mathf.MoveTowardsAngle(e.y, targetYaw, turnSpeed * dt);
            transform.eulerAngles = e;

            // Blob shadow shrinks while airborne.
            if (blobShadow != null && modelRoot != null)
            {
                float lift = Mathf.Clamp01(modelRoot.localPosition.y / 1.2f);
                float s = footprint * Mathf.Lerp(1f, 0.7f, lift);
                blobShadow.localScale = new Vector3(s, s, 1f);
            }

            if (rangeRing != null)
            {
                float target = InRange && !IsLeaving ? 1f : 0f;
                if (!Mathf.Approximately(ringAlpha, target))
                {
                    ringAlpha = Mathf.MoveTowards(ringAlpha, target, dt * 4f);
                    SetRingAlpha(ringAlpha);
                }
                if (ringAlpha > 0f)
                {
                    float pulse = 1f + Mathf.Sin(Time.time * 4f) * 0.05f;
                    var baseScale = footprint * 0.75f + 0.6f;
                    rangeRing.transform.localScale = new Vector3(baseScale * pulse, 1f, baseScale * pulse);
                }
            }
        }

        private void FitToModel()
        {
            var b = visual != null ? visual.GetBounds() : new Bounds(transform.position + Vector3.up, Vector3.one * 2f);
            // Ignore root yaw: measure in local space.
            var localCenter = transform.InverseTransformPoint(b.center);
            float horizontal = Mathf.Max(b.size.x, b.size.z);
            footprint = Mathf.Clamp(horizontal * 0.8f, 0.8f, 6f);

            if (tapCollider != null)
            {
                tapCollider.direction = 1;
                tapCollider.center = new Vector3(0f, Mathf.Max(localCenter.y, b.size.y * 0.5f), 0f);
                tapCollider.height = Mathf.Max(b.size.y * 1.2f, 1f);
                tapCollider.radius = Mathf.Max(horizontal * 0.55f, 0.8f);
            }
            if (blobShadow != null) blobShadow.localScale = new Vector3(footprint, footprint, 1f);
        }

        private void SetRingAlpha(float a)
        {
            if (rangeRing == null || block == null) return;
            var c = rangeRingColor;
            c.a *= a;
            rangeRing.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            rangeRing.SetPropertyBlock(block);
            rangeRing.enabled = a > 0.001f;
        }

        private void Puff()
        {
            if (puffPrefab == null) return;
            var p = Instantiate(puffPrefab, transform.position + Vector3.up * 0.4f, Quaternion.identity);
            var main = p.main;
            main.stopAction = ParticleSystemStopAction.Destroy;
            p.Play();
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}

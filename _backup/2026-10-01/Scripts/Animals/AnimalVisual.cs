using UnityEngine;

namespace WildTamers.Animals
{
    /// <summary>
    /// Sits on the root of every animal model prefab and wraps its Animator.
    /// State names are shared by all animals (generated controllers use the same names).
    /// </summary>
    public class AnimalVisual : MonoBehaviour
    {
        public const string Idle = "Idle";
        public const string Walk = "Walk";
        public const string Run = "Run";
        public const string Attack = "Attack";
        public const string Hit = "Hit";
        public const string Death = "Death";
        public const string Jump = "Jump";
        public const string Eat = "Eat";

        [SerializeField] private Animator animator;

        public Animator Animator => animator;

        private void Reset() => animator = GetComponentInChildren<Animator>();

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        public bool HasState(string state)
        {
            return animator != null && animator.runtimeAnimatorController != null &&
                   animator.HasState(0, Animator.StringToHash(state));
        }

        /// <summary>Starts the idle loop, optionally at a random point so herds don't move in sync.</summary>
        public void PlayIdle(bool randomStart = true)
        {
            if (!HasState(Idle)) return;
            animator.Play(Idle, 0, randomStart ? Random.value : 0f);
            animator.Update(0f);
        }

        /// <summary>Cross-fades to a state; returns false if the model has no such animation.</summary>
        public bool Play(string state, float fadeSeconds = 0.15f)
        {
            if (!HasState(state)) return false;
            animator.CrossFadeInFixedTime(state, fadeSeconds, 0);
            return true;
        }

        /// <summary>World-space bounds of all renderers.</summary>
        public Bounds GetBounds()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(transform.position, Vector3.one);
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }
    }
}

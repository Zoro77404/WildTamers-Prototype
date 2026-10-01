using UnityEngine;

namespace WildTamers.UI
{
    public static class UIEase
    {
        /// <summary>Unscaled frame time, capped so a hitch (scene load, play start) can't skip a UI animation.</summary>
        public static float DeltaTime => Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);

        /// <summary>Scaled frame time (pauses and slow-motion apply), capped the same way; for in-world motion such as battles.</summary>
        public static float GameDeltaTime => Mathf.Min(Time.deltaTime, 1f / 30f);

        public static float OutCubic(float x) => 1f - Mathf.Pow(1f - Mathf.Clamp01(x), 3f);

        public static float OutBack(float x, float overshoot = 1.70158f)
        {
            x = Mathf.Clamp01(x);
            float c3 = overshoot + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + overshoot * Mathf.Pow(x - 1f, 2f);
        }

        public static float InCubic(float x) => Mathf.Pow(Mathf.Clamp01(x), 3f);
    }
}

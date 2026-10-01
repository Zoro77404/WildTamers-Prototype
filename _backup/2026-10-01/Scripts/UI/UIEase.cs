using UnityEngine;

namespace WildTamers.UI
{
    public static class UIEase
    {
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

using UnityEngine;
using WildTamers.Lang;

namespace WildTamers.UI
{
    /// <summary>Sets how much of a bar is filled: from the left in English, from the right in Arabic.</summary>
    public static class BarAnchors
    {
        public static void Fill(RectTransform fill, float amount)
        {
            if (fill == null) return;
            amount = Mathf.Clamp01(amount);
            if (Loc.IsRTL)
            {
                fill.anchorMin = new Vector2(1f - amount, fill.anchorMin.y);
                fill.anchorMax = new Vector2(1f, fill.anchorMax.y);
            }
            else
            {
                fill.anchorMin = new Vector2(0f, fill.anchorMin.y);
                fill.anchorMax = new Vector2(amount, fill.anchorMax.y);
            }
        }
    }
}

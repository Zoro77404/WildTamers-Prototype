using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WildTamers.Lang
{
    /// <summary>Marks a rect (and everything under it) that <see cref="RTLMirror"/> must leave alone, e.g. bars that fill from the right themselves.</summary>
    public class RTLMirrorIgnore : MonoBehaviour { }

    /// <summary>
    /// Flips a layout left ↔ right in Arabic: every child rect swaps sides (anchors, offsets, pivot), horizontal layout groups
    /// reverse and left/right padding and alignment swap. Always works from the layout as it was authored (English),
    /// so switching back and forth never drifts. Text alignment is handled by <see cref="LocalizedText"/>.
    /// </summary>
    public class RTLMirror : MonoBehaviour
    {
        [Tooltip("Also flip this object's own rect (normally only its children are flipped).")]
        [SerializeField] private bool mirrorSelf;

        private struct RectState
        {
            public Vector2 AnchorMin, AnchorMax, Pivot, OffsetMin, OffsetMax;
        }

        private struct GroupState
        {
            public TextAnchor Alignment;
            public bool Reverse;
            public int Left, Right;
        }

        private readonly Dictionary<RectTransform, RectState> rects = new Dictionary<RectTransform, RectState>();
        private readonly Dictionary<HorizontalOrVerticalLayoutGroup, GroupState> groups = new Dictionary<HorizontalOrVerticalLayoutGroup, GroupState>();
        private bool captured;

        private void Awake() => Capture();

        private void OnEnable()
        {
            Capture();
            Loc.LanguageChanged += Apply;
            Apply();
        }

        private void OnDisable() => Loc.LanguageChanged -= Apply;

        private void Capture()
        {
            if (captured) return;
            captured = true;
            Walk((RectTransform)transform, isRoot: true);
        }

        private void Walk(RectTransform rt, bool isRoot)
        {
            if (rt.GetComponent<RTLMirrorIgnore>() != null && !isRoot) return;
            if (!isRoot || mirrorSelf)
            {
                // A child of a layout group is placed by the group, not by its own anchors.
                var parentGroup = rt.parent != null ? rt.parent.GetComponent<LayoutGroup>() : null;
                if (parentGroup == null || isRoot) rects[rt] = Read(rt);
            }
            var group = rt.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (group != null)
                groups[group] = new GroupState { Alignment = group.childAlignment, Reverse = group is HorizontalLayoutGroup && group.reverseArrangement, Left = group.padding.left, Right = group.padding.right };
            // A separate RTLMirror below owns its own subtree.
            foreach (RectTransform child in rt)
            {
                if (child.GetComponent<RTLMirror>() != null) continue;
                Walk(child, isRoot: false);
            }
        }

        private static RectState Read(RectTransform rt) => new RectState
        {
            AnchorMin = rt.anchorMin, AnchorMax = rt.anchorMax, Pivot = rt.pivot, OffsetMin = rt.offsetMin, OffsetMax = rt.offsetMax
        };

        private void Apply()
        {
            Capture();
            bool flip = Loc.IsRTL;
            foreach (var pair in rects)
            {
                var rt = pair.Key;
                if (rt == null) continue;
                var o = pair.Value;
                if (flip)
                {
                    rt.pivot = new Vector2(1f - o.Pivot.x, o.Pivot.y);
                    rt.anchorMin = new Vector2(1f - o.AnchorMax.x, o.AnchorMin.y);
                    rt.anchorMax = new Vector2(1f - o.AnchorMin.x, o.AnchorMax.y);
                    rt.offsetMin = new Vector2(-o.OffsetMax.x, o.OffsetMin.y);
                    rt.offsetMax = new Vector2(-o.OffsetMin.x, o.OffsetMax.y);
                }
                else
                {
                    rt.pivot = o.Pivot;
                    rt.anchorMin = o.AnchorMin;
                    rt.anchorMax = o.AnchorMax;
                    rt.offsetMin = o.OffsetMin;
                    rt.offsetMax = o.OffsetMax;
                }
            }
            foreach (var pair in groups)
            {
                var g = pair.Key;
                if (g == null) continue;
                var o = pair.Value;
                g.childAlignment = flip ? MirrorAnchor(o.Alignment) : o.Alignment;
                if (g is HorizontalLayoutGroup) g.reverseArrangement = flip ? !o.Reverse : o.Reverse;
                g.padding.left = flip ? o.Right : o.Left;
                g.padding.right = flip ? o.Left : o.Right;
                LayoutRebuilder.MarkLayoutForRebuild((RectTransform)g.transform);
            }
        }

        private static TextAnchor MirrorAnchor(TextAnchor a)
        {
            switch (a)
            {
                case TextAnchor.UpperLeft: return TextAnchor.UpperRight;
                case TextAnchor.UpperRight: return TextAnchor.UpperLeft;
                case TextAnchor.MiddleLeft: return TextAnchor.MiddleRight;
                case TextAnchor.MiddleRight: return TextAnchor.MiddleLeft;
                case TextAnchor.LowerLeft: return TextAnchor.LowerRight;
                case TextAnchor.LowerRight: return TextAnchor.LowerLeft;
                default: return a;
            }
        }
    }
}

using System;
using UnityEngine;

namespace WildTamers.Vfx
{
    /// <summary>
    /// A ready-to-use effect made of one or more baked clips ("layers") played in order or on top of each other,
    /// e.g. Fire = burst → idle (held) → end. Drag one into an animal's Special VFX slot.
    /// Sizes are in meters at Scale 1; times are seconds at Speed 1.
    /// </summary>
    [CreateAssetMenu(menuName = "Wild Tamers/VFX/Effect", fileName = "VFX_New")]
    public class VfxEffect : ScriptableObject
    {
        [Serializable]
        public class Layer
        {
            public VfxClip clip;
            [Tooltip("Seconds after the effect starts when this layer starts.")]
            [Min(0f)] public float start;
            [Tooltip("Start this many seconds into the clip (skips its beginning).")]
            [Min(0f)] public float clipFrom;
            [Tooltip("Stop the clip at this time (0 = play to its end).")]
            [Min(0f)] public float clipTo;
            [Tooltip("Keep this layer on screen for this long, looping the clip (0 = play the clip once).")]
            [Min(0f)] public float hold;
            [Tooltip("Playback speed of this layer (multiplied by the animal's speed).")]
            [Min(0.05f)] public float speed = 1f;
            [Tooltip("Position offset in meters (x = right, y = up, z = away from the camera).")]
            public Vector3 offset;
            [Min(0.01f)] public float scale = 1f;
            [Tooltip("Extra squash/stretch per axis (x = width, y = height, z = depth toward the camera). (1, 1, 1) = as made.")]
            public Vector3 stretch = Vector3.one;
            [Tooltip("Seconds to fade and grow in.")]
            [Min(0f)] public float fadeIn;
            [Tooltip("Seconds to fade out at the end of the layer.")]
            [Min(0f)] public float fadeOut = 0.15f;
            [Tooltip("Also shrink while fading out (good for solid shapes that can't just turn see-through).")]
            public bool shrinkOut;

            /// <summary>How long the clip part plays, in clip seconds.</summary>
            public float ClipSpan
            {
                get
                {
                    if (clip == null) return 0f;
                    float to = clipTo > 0f ? Mathf.Min(clipTo, clip.Length) : clip.Length;
                    return Mathf.Max(0f, to - clipFrom);
                }
            }

            /// <summary>Per-axis size of the layer (scale × stretch; an unset stretch counts as 1).</summary>
            public Vector3 Size => scale * (stretch == Vector3.zero ? Vector3.one : stretch);

            /// <summary>Seconds this layer is on screen at speed 1.</summary>
            public float Duration => hold > 0f ? hold : ClipSpan / Mathf.Max(0.05f, speed);
        }

        [Tooltip("Clips that make up the effect.")]
        public Layer[] layers = Array.Empty<Layer>();

        [Header("Timing")]
        [Tooltip("Seconds after the effect starts when it 'hits' (the bolt lands, spikes burst, the flame flares). " +
                 "In battle the effect is started early so this moment lines up with the attack's impact.")]
        [Min(0f)] public float hitTime = 0.1f;

        [Header("Look")]
        [Tooltip("Overall size in battle at Scale 1.")]
        [Min(0.01f)] public float baseScale = 1f;
        [Tooltip("Turn the effect so its front faces the camera (flat effects like fire need this).")]
        public bool faceCamera = true;
        [Tooltip("Lean the effect toward the camera (degrees) around its base. The battle camera looks down, so a lean keeps tall " +
                 "effects (clouds, tornadoes, flames) facing it and below the boss's health card. Keep 0 for effects flat on the ground.")]
        [Range(0f, 45f)] public float leanToCamera;

        /// <summary>Space the effect can fill (meters, before the battle scale), around its base point.</summary>
        public Bounds LocalBounds
        {
            get
            {
                bool any = false;
                var bounds = new Bounds(Vector3.zero, Vector3.zero);
                if (layers == null) return bounds;
                foreach (var layer in layers)
                {
                    if (layer == null || layer.clip == null) continue;
                    var b = layer.clip.Bounds;
                    var size = layer.Size;
                    var lb = new Bounds(layer.offset + Vector3.Scale(b.center, size), Vector3.Scale(b.size, size));
                    if (!any) { bounds = lb; any = true; }
                    else bounds.Encapsulate(lb);
                }
                return bounds;
            }
        }

        /// <summary>Total length at speed 1.</summary>
        public float Duration
        {
            get
            {
                float d = 0f;
                if (layers == null) return d;
                foreach (var layer in layers)
                    if (layer != null && layer.clip != null) d = Mathf.Max(d, layer.start + layer.Duration);
                return d;
            }
        }
    }
}

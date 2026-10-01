using UnityEngine;

namespace WildTamers.Core
{
    /// <summary>Project layers (created by the editor builder).</summary>
    public static class GameLayers
    {
        public const string AnimalsName = "Animals";
        public const string PreviewName = "Preview";

        public static int Animals => LayerMask.NameToLayer(AnimalsName);
        public static int Preview => LayerMask.NameToLayer(PreviewName);

        public static void SetLayerRecursively(GameObject root, int layer)
        {
            if (layer < 0) return;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }
    }
}

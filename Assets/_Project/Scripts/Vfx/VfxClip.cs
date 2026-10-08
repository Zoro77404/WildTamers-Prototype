using System;
using Unity.Collections;
using UnityEngine;

namespace WildTamers.Vfx
{
    /// <summary>
    /// One baked mesh animation (made by Wild Tamers/Build/VFX Bakes from an Alembic .abc file in Assets/_Project/VFX).
    /// The frames live in a compact binary file (<see cref="data"/>): 16-bit positions, 8-bit colors and normals per frame,
    /// so the effect plays on every platform (Alembic itself only runs on desktop). Each "part" is one face set of the
    /// original (flames, sparks, rocks…) drawn with its own material.
    /// </summary>
    [CreateAssetMenu(menuName = "Wild Tamers/VFX/Baked Clip", fileName = "VfxClip")]
    public class VfxClip : ScriptableObject
    {
        public const int Magic = 0x58565457; // "WTVX"
        public const int Version = 1;

        [Serializable]
        public class Part
        {
            [Tooltip("Face set name from the source file.")]
            public string name;
            [Tooltip("How this part is drawn (glow, solid, see-through). Uses the 'Wild Tamers/VFX Vertex Color' shader.")]
            public Material material;
            [Tooltip("Clip time (s) where this part starts fading out. The source files have no transparency, so flashes and rings " +
                     "that should die away are faded here. 0 = never.")]
            [Min(0f)] public float fadeFrom;
            [Tooltip("Clip time (s) where this part is fully gone.")]
            [Min(0f)] public float fadeTo;

            /// <summary>0–1 visibility of this part at a clip time.</summary>
            public float VisibilityAt(float clipTime)
            {
                if (fadeTo <= 0f || fadeTo <= fadeFrom) return fadeTo > 0f && clipTime >= fadeTo ? 0f : 1f;
                return 1f - Mathf.Clamp01((clipTime - fadeFrom) / (fadeTo - fadeFrom));
            }
        }

        [Tooltip("Source .abc file (for re-baking only; not used in the game).")]
        public string sourcePath;
        [Tooltip("Baked frames file. Re-bake with Wild Tamers/Build/VFX Bakes.")]
        public TextAsset data;
        public Part[] parts = Array.Empty<Part>();

        [Header("Info (filled by the bake)")]
        [SerializeField] private float frameRate = 24f;
        [SerializeField] private int frameCount;
        [SerializeField] private int vertexCount;
        [SerializeField] private Bounds bounds;
        [Tooltip("The last frame flows back into the first one, so the clip can loop.")]
        [SerializeField] private bool seamlessLoop;

        public float FrameRate => frameRate;
        public int FrameCount => frameCount;
        public int VertexCount => vertexCount;
        public Bounds Bounds => bounds;
        public bool SeamlessLoop => seamlessLoop;
        /// <summary>Length in seconds (first frame to last frame).</summary>
        public float Length => frameCount > 1 ? (frameCount - 1) / frameRate : 0f;

        // ---------- Decoded header (cached) ----------

        [NonSerialized] private bool parsed;
        [NonSerialized] private Vector3 quantMin, quantSize;
        [NonSerialized] private float colorScale;
        [NonSerialized] private int[] indices;
        [NonSerialized] private Vector2Int[] partRanges;
        [NonSerialized] private int[] posOffsets, colOffsets, nrmOffsets;

        public bool IsValid => data != null && vertexCount > 0 && frameCount > 0 && Parse();

        /// <summary>Called by the baker after the data file changed.</summary>
        public void SetInfo(float rate, int frames, int vertices, Bounds b, bool loops)
        {
            frameRate = rate;
            frameCount = frames;
            vertexCount = vertices;
            bounds = b;
            seamlessLoop = loops;
            parsed = false;
        }

        private void OnValidate() => parsed = false;

        private bool Parse()
        {
            if (parsed) return indices != null;
            parsed = true;
            indices = null;
            if (data == null) return false;
            var bytes = data.GetData<byte>();
            if (bytes.Length < 64) return false;
            var r = new Reader(bytes);
            if (r.Int() != Magic || r.Int() != Version) { Debug.LogError($"[Wild Tamers] {name}: VFX data is not a valid bake. Re-bake it."); return false; }
            int verts = r.Int();
            int frames = r.Int();
            r.Float(); // frame rate (also in the asset)
            quantMin = new Vector3(r.Float(), r.Float(), r.Float());
            quantSize = new Vector3(r.Float(), r.Float(), r.Float());
            colorScale = r.Float();
            int indexCount = r.Int();
            int partCount = r.Int();
            partRanges = new Vector2Int[partCount];
            for (int i = 0; i < partCount; i++) partRanges[i] = new Vector2Int(r.Int(), r.Int());
            posOffsets = new int[frames];
            colOffsets = new int[frames];
            nrmOffsets = new int[frames];
            for (int i = 0; i < frames; i++) posOffsets[i] = r.Int();
            for (int i = 0; i < frames; i++) colOffsets[i] = r.Int();
            for (int i = 0; i < frames; i++) nrmOffsets[i] = r.Int();
            var idx = new int[indexCount];
            bool wide = verts > 65535;
            for (int i = 0; i < indexCount; i++) idx[i] = wide ? r.Int() : r.UShort();
            if (verts != vertexCount || frames != frameCount)
            {
                Debug.LogError($"[Wild Tamers] {name}: VFX data does not match the asset. Re-bake it.");
                return false;
            }
            indices = idx;
            return true;
        }

        /// <summary>Builds a fresh mesh (one submesh per part) showing frame 0. The caller owns and destroys it.</summary>
        public Mesh CreateMesh()
        {
            if (!IsValid) return null;
            var mesh = new Mesh { name = name, indexFormat = vertexCount > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.MarkDynamic();
            var pos = new Vector3[vertexCount];
            var col = new Color[vertexCount];
            var nrm = new Vector3[vertexCount];
            DecodeFrame(0, pos, col, nrm);
            mesh.vertices = pos;
            mesh.colors = col;
            mesh.normals = nrm;
            mesh.subMeshCount = partRanges.Length;
            for (int p = 0; p < partRanges.Length; p++)
            {
                var range = partRanges[p];
                var sub = new int[range.y];
                Array.Copy(indices, range.x, sub, 0, range.y);
                mesh.SetTriangles(sub, p, false);
            }
            mesh.bounds = bounds;
            return mesh;
        }

        /// <summary>Same data offset for both frames = identical data (the bake stores repeated frames once).</summary>
        public bool SamePositions(int a, int b) => posOffsets[a] == posOffsets[b];
        public bool SameColors(int a, int b) => colOffsets[a] == colOffsets[b];

        public void DecodeFrame(int frame, Vector3[] pos, Color[] col, Vector3[] nrm)
        {
            if (!IsValid) return;
            frame = Mathf.Clamp(frame, 0, frameCount - 1);
            var bytes = data.GetData<byte>();
            if (pos != null) DecodePositions(bytes, posOffsets[frame], pos);
            if (col != null) DecodeColors(bytes, colOffsets[frame], col);
            if (nrm != null) DecodeNormals(bytes, nrmOffsets[frame], nrm);
        }

        private void DecodePositions(NativeArray<byte> bytes, int offset, Vector3[] pos)
        {
            const float inv = 1f / 65535f;
            float sx = quantSize.x * inv, sy = quantSize.y * inv, sz = quantSize.z * inv;
            var words = bytes.GetSubArray(offset, vertexCount * 6).Reinterpret<ushort>(1);
            for (int i = 0, w = 0; i < vertexCount; i++, w += 3)
                pos[i] = new Vector3(quantMin.x + words[w] * sx, quantMin.y + words[w + 1] * sy, quantMin.z + words[w + 2] * sz);
        }

        private void DecodeColors(NativeArray<byte> bytes, int offset, Color[] col)
        {
            // Stored as sqrt(color / scale) in 8 bits: keeps dark tones smooth.
            const float inv = 1f / 255f;
            for (int i = 0, o = offset; i < vertexCount; i++, o += 3)
            {
                float r = bytes[o] * inv, g = bytes[o + 1] * inv, b = bytes[o + 2] * inv;
                col[i] = new Color(r * r * colorScale, g * g * colorScale, b * b * colorScale, 1f);
            }
        }

        private void DecodeNormals(NativeArray<byte> bytes, int offset, Vector3[] nrm)
        {
            const float inv = 2f / 255f;
            for (int i = 0, o = offset; i < vertexCount; i++, o += 2)
                nrm[i] = OctDecode(bytes[o] * inv - 1f, bytes[o + 1] * inv - 1f);
        }

        // ---------- Octahedral normal encoding (shared with the baker) ----------

        public static Vector2 OctEncode(Vector3 n)
        {
            n /= Mathf.Abs(n.x) + Mathf.Abs(n.y) + Mathf.Abs(n.z) + 1e-9f;
            if (n.z < 0f)
            {
                float x = (1f - Mathf.Abs(n.y)) * (n.x >= 0f ? 1f : -1f);
                float y = (1f - Mathf.Abs(n.x)) * (n.y >= 0f ? 1f : -1f);
                return new Vector2(x, y);
            }
            return new Vector2(n.x, n.y);
        }

        public static Vector3 OctDecode(float x, float y)
        {
            var n = new Vector3(x, y, 1f - Mathf.Abs(x) - Mathf.Abs(y));
            if (n.z < 0f)
            {
                float ox = n.x;
                n.x = (1f - Mathf.Abs(n.y)) * (ox >= 0f ? 1f : -1f);
                n.y = (1f - Mathf.Abs(ox)) * (n.y >= 0f ? 1f : -1f);
            }
            return n.normalized;
        }

        private struct Reader
        {
            private readonly NativeArray<byte> bytes;
            private int pos;

            public Reader(NativeArray<byte> b)
            {
                bytes = b;
                pos = 0;
            }

            public int Int()
            {
                int v = bytes[pos] | bytes[pos + 1] << 8 | bytes[pos + 2] << 16 | bytes[pos + 3] << 24;
                pos += 4;
                return v;
            }

            public ushort UShort()
            {
                ushort v = (ushort)(bytes[pos] | bytes[pos + 1] << 8);
                pos += 2;
                return v;
            }

            public float Float()
            {
                int v = Int();
                return BitConverter.Int32BitsToSingle(v);
            }
        }
    }
}

using UnityEngine;

namespace WildTamers.Map
{
    /// <summary>Generates flat ring / disc meshes lying on the XZ plane.</summary>
    public static class RingMesh
    {
        /// <summary>
        /// Ring between <paramref name="innerRadius"/> and <paramref name="outerRadius"/> as submesh 0;
        /// when <paramref name="withFill"/>, a disc filling the inside as submesh 1.
        /// </summary>
        public static Mesh Create(float innerRadius, float outerRadius, int segments, bool withFill, string name = "Ring")
        {
            segments = Mathf.Max(3, segments);
            int ringVerts = (segments + 1) * 2;
            int fillVerts = withFill ? segments + 2 : 0;
            var verts = new Vector3[ringVerts + fillVerts];
            var normals = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];

            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts[i * 2] = d * innerRadius;
                verts[i * 2 + 1] = d * outerRadius;
                uvs[i * 2] = new Vector2((float)i / segments, 0f);
                uvs[i * 2 + 1] = new Vector2((float)i / segments, 1f);
            }

            var ringTris = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                int t = i * 6;
                // Clockwise seen from above.
                ringTris[t] = a; ringTris[t + 1] = c; ringTris[t + 2] = b;
                ringTris[t + 3] = b; ringTris[t + 4] = c; ringTris[t + 5] = d;
            }

            int[] fillTris = null;
            if (withFill)
            {
                int center = ringVerts;
                verts[center] = Vector3.zero;
                uvs[center] = new Vector2(0.5f, 0.5f);
                for (int i = 0; i <= segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    verts[center + 1 + i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * innerRadius;
                    uvs[center + 1 + i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.5f + Vector2.one * 0.5f;
                }
                fillTris = new int[segments * 3];
                for (int i = 0; i < segments; i++)
                {
                    fillTris[i * 3] = center;
                    fillTris[i * 3 + 1] = center + 2 + i;
                    fillTris[i * 3 + 2] = center + 1 + i;
                }
            }

            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;

            var mesh = new Mesh { name = name };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.subMeshCount = withFill ? 2 : 1;
            mesh.SetTriangles(ringTris, 0);
            if (withFill) mesh.SetTriangles(fillTris, 1);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

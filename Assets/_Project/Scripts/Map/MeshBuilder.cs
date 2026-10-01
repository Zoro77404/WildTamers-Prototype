using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WildTamers.Map
{
    /// <summary>
    /// Collects flat-shaded low-poly geometry into one mesh with a submesh per material slot.
    /// Every face gets its own vertices so lighting stays faceted.
    /// </summary>
    public class MeshBuilder
    {
        private readonly List<Vector3> vertices = new List<Vector3>(4096);
        private readonly List<Vector3> normals = new List<Vector3>(4096);
        private readonly List<int>[] triangles;

        public MeshBuilder(int materialSlots)
        {
            triangles = new List<int>[materialSlots];
            for (int i = 0; i < materialSlots; i++) triangles[i] = new List<int>();
        }

        public bool IsEmpty => vertices.Count == 0;

        public void Clear()
        {
            vertices.Clear();
            normals.Clear();
            foreach (var t in triangles) t.Clear();
        }

        // ---------- Primitives ----------

        public void Triangle(int slot, Vector3 a, Vector3 b, Vector3 c)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-10f) return;
            n.Normalize();
            int i = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            normals.Add(n); normals.Add(n); normals.Add(n);
            var tri = triangles[slot];
            tri.Add(i); tri.Add(i + 1); tri.Add(i + 2);
        }

        /// <summary>Quad with corners in clockwise order when seen from the front.</summary>
        public void Quad(int slot, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-10f) return;
            n.Normalize();
            int i = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            normals.Add(n); normals.Add(n); normals.Add(n); normals.Add(n);
            var tri = triangles[slot];
            tri.Add(i); tri.Add(i + 1); tri.Add(i + 2);
            tri.Add(i); tri.Add(i + 2); tri.Add(i + 3);
        }

        /// <summary>Flat horizontal rectangle facing up.</summary>
        public void RectXZ(int slot, Rect r, float y)
        {
            if (r.width <= 0f || r.height <= 0f) return;
            Quad(slot,
                new Vector3(r.xMin, y, r.yMin),
                new Vector3(r.xMin, y, r.yMax),
                new Vector3(r.xMax, y, r.yMax),
                new Vector3(r.xMax, y, r.yMin));
        }

        /// <summary>Axis-aligned box from a footprint rect, without a bottom face.</summary>
        public void Box(int slot, Rect footprint, float y0, float y1, int topSlot = -1)
        {
            if (topSlot < 0) topSlot = slot;
            float x0 = footprint.xMin, x1 = footprint.xMax, z0 = footprint.yMin, z1 = footprint.yMax;
            var p000 = new Vector3(x0, y0, z0); var p100 = new Vector3(x1, y0, z0);
            var p001 = new Vector3(x0, y0, z1); var p101 = new Vector3(x1, y0, z1);
            var p010 = new Vector3(x0, y1, z0); var p110 = new Vector3(x1, y1, z0);
            var p011 = new Vector3(x0, y1, z1); var p111 = new Vector3(x1, y1, z1);
            Quad(slot, p000, p010, p110, p100); // south (-z)
            Quad(slot, p101, p111, p011, p001); // north (+z)
            Quad(slot, p001, p011, p010, p000); // west (-x)
            Quad(slot, p100, p110, p111, p101); // east (+x)
            Quad(topSlot, p010, p011, p111, p110); // top
        }

        /// <summary>Gable roof (triangular prism) on a footprint; ridge runs along the longer side.</summary>
        public void GableRoof(int slot, Rect r, float y0, float height, int gableSlot)
        {
            bool alongX = r.width >= r.height;
            if (alongX)
            {
                float zm = r.center.y;
                var a0 = new Vector3(r.xMin, y0, r.yMin); var b0 = new Vector3(r.xMax, y0, r.yMin);
                var a1 = new Vector3(r.xMin, y0, r.yMax); var b1 = new Vector3(r.xMax, y0, r.yMax);
                var ra = new Vector3(r.xMin, y0 + height, zm); var rb = new Vector3(r.xMax, y0 + height, zm);
                Quad(slot, a0, ra, rb, b0);
                Quad(slot, b1, rb, ra, a1);
                Triangle(gableSlot, a1, ra, a0);
                Triangle(gableSlot, b0, rb, b1);
            }
            else
            {
                float xm = r.center.x;
                var a0 = new Vector3(r.xMin, y0, r.yMin); var a1 = new Vector3(r.xMin, y0, r.yMax);
                var b0 = new Vector3(r.xMax, y0, r.yMin); var b1 = new Vector3(r.xMax, y0, r.yMax);
                var ra = new Vector3(xm, y0 + height, r.yMin); var rb = new Vector3(xm, y0 + height, r.yMax);
                Quad(slot, a1, rb, ra, a0);
                Quad(slot, b0, ra, rb, b1);
                Triangle(gableSlot, a0, ra, b0);
                Triangle(gableSlot, b1, rb, a1);
            }
        }

        /// <summary>Vertical prism (cylinder) with flat top.</summary>
        public void Cylinder(int slot, Vector3 baseCenter, float radius, float height, int sides, int topSlot = -1, float topRadius = -1f, float angleOffset = 0f)
        {
            if (topSlot < 0) topSlot = slot;
            if (topRadius < 0f) topRadius = radius;
            float y0 = baseCenter.y, y1 = baseCenter.y + height;
            var top = new Vector3(baseCenter.x, y1, baseCenter.z);
            for (int i = 0; i < sides; i++)
            {
                float a0 = angleOffset + i * Mathf.PI * 2f / sides;
                float a1 = angleOffset + (i + 1) * Mathf.PI * 2f / sides;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                var b0 = new Vector3(baseCenter.x, y0, baseCenter.z) + d0 * radius;
                var b1 = new Vector3(baseCenter.x, y0, baseCenter.z) + d1 * radius;
                var t0 = top + d0 * topRadius;
                var t1 = top + d1 * topRadius;
                Quad(slot, b0, t0, t1, b1);
                if (topRadius > 0.001f) Triangle(topSlot, top, t1, t0);
            }
        }

        /// <summary>Cone pointing up.</summary>
        public void Cone(int slot, Vector3 baseCenter, float radius, float height, int sides, float angleOffset = 0f)
        {
            var apex = baseCenter + Vector3.up * height;
            for (int i = 0; i < sides; i++)
            {
                float a0 = angleOffset + i * Mathf.PI * 2f / sides;
                float a1 = angleOffset + (i + 1) * Mathf.PI * 2f / sides;
                var b0 = baseCenter + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                var b1 = baseCenter + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                Triangle(slot, b0, apex, b1);
                Triangle(slot, b1, baseCenter, b0); // underside, visible from low angles
            }
        }

        /// <summary>Flat disc facing up.</summary>
        public void Disc(int slot, Vector3 center, float radius, int sides, float angleOffset = 0f)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = angleOffset + i * Mathf.PI * 2f / sides;
                float a1 = angleOffset + (i + 1) * Mathf.PI * 2f / sides;
                Triangle(slot, center,
                    center + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius,
                    center + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius);
            }
        }

        private static readonly Vector3[] IcoVerts = BuildIcoVerts();
        private static readonly int[] IcoTris =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
        };

        private static Vector3[] BuildIcoVerts()
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (int i = 0; i < v.Length; i++) v[i] = v[i].normalized;
            return v;
        }

        /// <summary>Low-poly blob (icosahedron) with optional per-vertex jitter; scale stretches it.</summary>
        public void Blob(int slot, Vector3 center, Vector3 scale, float jitter, System.Random rng, float yaw = 0f)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var p = new Vector3[IcoVerts.Length];
            for (int i = 0; i < p.Length; i++)
            {
                float j = 1f + (rng != null ? ((float)rng.NextDouble() * 2f - 1f) * jitter : 0f);
                p[i] = center + rot * Vector3.Scale(IcoVerts[i] * j, scale);
            }
            for (int i = 0; i < IcoTris.Length; i += 3)
                Triangle(slot, p[IcoTris[i]], p[IcoTris[i + 1]], p[IcoTris[i + 2]]);
        }

        /// <summary>Faceted UV sphere (low ring/segment counts give the low-poly look).</summary>
        public void Sphere(int slot, Vector3 center, Vector3 scale, int rings = 6, int segments = 10, Quaternion? rotation = null)
        {
            var rot = rotation ?? Quaternion.identity;
            Vector3 P(int ring, int seg)
            {
                float v = (float)ring / rings * Mathf.PI;          // 0..PI from top
                float u = (float)seg / segments * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Sin(v) * Mathf.Cos(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Sin(u));
                return center + rot * Vector3.Scale(d, scale);
            }

            for (int r = 0; r < rings; r++)
            for (int s = 0; s < segments; s++)
            {
                var a = P(r, s); var b = P(r, s + 1); var c = P(r + 1, s + 1); var d = P(r + 1, s);
                if (r == 0) Triangle(slot, a, c, d);
                else if (r == rings - 1) Triangle(slot, a, b, d);
                else Quad(slot, a, b, c, d);
            }
        }

        // ---------- Output ----------

        /// <summary>Creates the mesh and returns the material slots it actually uses (in submesh order).</summary>
        public Mesh Build(string name, out List<int> usedSlots)
        {
            usedSlots = new List<int>();
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);

            int sub = 0;
            for (int i = 0; i < triangles.Length; i++) if (triangles[i].Count > 0) sub++;
            mesh.subMeshCount = sub;
            sub = 0;
            for (int i = 0; i < triangles.Length; i++)
            {
                if (triangles[i].Count == 0) continue;
                mesh.SetTriangles(triangles[i], sub++, false);
                usedSlots.Add(i);
            }
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

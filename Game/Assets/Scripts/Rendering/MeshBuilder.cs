using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TwelfthStamp
{
    /// <summary>
    /// Small procedural mesh builder. UVs are world-scaled (metres / uvScale) so tiling textures keep a consistent
    /// texel density across props. Normals are explicit; tangents are computed on build for normal mapping.
    /// </summary>
    public class MeshBuilder
    {
        readonly List<Vector3> m_V = new List<Vector3>();
        readonly List<Vector3> m_N = new List<Vector3>();
        readonly List<Vector2> m_UV = new List<Vector2>();
        readonly List<Color> m_C = new List<Color>();
        readonly List<int> m_T = new List<int>();
        public Color VertexColor = Color.white;

        public int VertexCount => m_V.Count;

        public Mesh ToMesh(string name = "Mesh")
        {
            var mesh = new Mesh { name = name };
            if (m_V.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(m_V);
            mesh.SetNormals(m_N);
            mesh.SetUVs(0, m_UV);
            mesh.SetColors(m_C);
            mesh.SetTriangles(m_T, 0);
            mesh.RecalculateBounds();
            if (m_V.Count > 0) mesh.RecalculateTangents();
            return mesh;
        }

        int AddVertex(Vector3 p, Vector3 n, Vector2 uv)
        {
            m_V.Add(p); m_N.Add(n); m_UV.Add(uv); m_C.Add(VertexColor);
            return m_V.Count - 1;
        }

        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            var n = Vector3.Cross(b - a, c - a).normalized;
            int i0 = AddVertex(a, n, ua), i1 = AddVertex(b, n, ub), i2 = AddVertex(c, n, uc);
            m_T.Add(i0); m_T.Add(i1); m_T.Add(i2);
        }

        /// <summary>Quad a-b-c-d in clockwise order when viewed from the front (Unity winding).</summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            var n = Vector3.Cross(b - a, d - a).normalized;
            int i0 = AddVertex(a, n, ua), i1 = AddVertex(b, n, ub), i2 = AddVertex(c, n, uc), i3 = AddVertex(d, n, ud);
            m_T.Add(i0); m_T.Add(i1); m_T.Add(i2);
            m_T.Add(i0); m_T.Add(i2); m_T.Add(i3);
        }

        /// <summary>Quad with UVs derived from its size in metres.</summary>
        public void AddQuadWorldUV(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float uvScale, Vector2 uvOffset = default)
        {
            float w = Vector3.Distance(a, b) / uvScale;
            float h = Vector3.Distance(a, d) / uvScale;
            AddQuad(a, b, c, d,
                uvOffset + new Vector2(0, 0), uvOffset + new Vector2(w, 0),
                uvOffset + new Vector2(w, h), uvOffset + new Vector2(0, h));
        }

        // ------------------------------------------------------------------ primitives
        public void AddBox(Vector3 center, Vector3 size, Quaternion rot, float uvScale = 1f)
        {
            var h = size * 0.5f;
            Vector3 P(float x, float y, float z) => center + rot * new Vector3(x * h.x, y * h.y, z * h.z);
            // Each face: a->b and a->d span the face so that Cross(b-a, d-a) points outward (Unity clockwise front faces).
            AddQuadWorldUV(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), uvScale);      // +Z
            AddQuadWorldUV(P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), uvScale);  // -Z
            AddQuadWorldUV(P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), uvScale);      // +X
            AddQuadWorldUV(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), uvScale);  // -X
            AddQuadWorldUV(P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), P(-1, 1, -1), uvScale);      // +Y
            AddQuadWorldUV(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), uvScale);  // -Y
        }

        public void AddBox(Vector3 center, Vector3 size, float uvScale = 1f) => AddBox(center, size, Quaternion.identity, uvScale);

        /// <summary>Frustum/cylinder along +Y from base centre. radiusTop may be 0 (cone).</summary>
        public void AddFrustum(Vector3 baseCenter, float radiusBottom, float radiusTop, float height, int segments, bool capBottom = true, bool capTop = true, float uvScale = 1f, Quaternion? rotation = null)
        {
            var rot = rotation ?? Quaternion.identity;
            float circ = 2f * Mathf.PI * Mathf.Max(radiusBottom, radiusTop);
            float slant = new Vector2(radiusBottom - radiusTop, height).magnitude;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i / (float)segments * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                Vector3 d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 b0 = baseCenter + rot * (d0 * radiusBottom), b1 = baseCenter + rot * (d1 * radiusBottom);
                Vector3 t0 = baseCenter + rot * (d0 * radiusTop + Vector3.up * height), t1 = baseCenter + rot * (d1 * radiusTop + Vector3.up * height);
                float slope = (radiusBottom - radiusTop) / Mathf.Max(0.0001f, height);
                Vector3 n0 = rot * new Vector3(d0.x, slope, d0.z).normalized;
                Vector3 n1 = rot * new Vector3(d1.x, slope, d1.z).normalized;
                float u0 = i / (float)segments * circ / uvScale, u1 = (i + 1) / (float)segments * circ / uvScale;
                float v1 = slant / uvScale;
                int i0 = AddVertex(b0, n0, new Vector2(u0, 0)), i1 = AddVertex(b1, n1, new Vector2(u1, 0));
                int i2 = AddVertex(t1, n1, new Vector2(u1, v1)), i3 = AddVertex(t0, n0, new Vector2(u0, v1));
                m_T.Add(i0); m_T.Add(i2); m_T.Add(i1);
                m_T.Add(i0); m_T.Add(i3); m_T.Add(i2);
                if (capBottom && radiusBottom > 0.0001f)
                {
                    var c = baseCenter;
                    var n = rot * Vector3.down;
                    int ci = AddVertex(c, n, Vector2.zero);
                    int j0 = AddVertex(b0, n, new Vector2(d0.x, d0.z) * radiusBottom / uvScale);
                    int j1 = AddVertex(b1, n, new Vector2(d1.x, d1.z) * radiusBottom / uvScale);
                    m_T.Add(ci); m_T.Add(j0); m_T.Add(j1);
                }
                if (capTop && radiusTop > 0.0001f)
                {
                    var c = baseCenter + rot * (Vector3.up * height);
                    var n = rot * Vector3.up;
                    int ci = AddVertex(c, n, Vector2.zero);
                    int j0 = AddVertex(t0, n, new Vector2(d0.x, d0.z) * radiusTop / uvScale);
                    int j1 = AddVertex(t1, n, new Vector2(d1.x, d1.z) * radiusTop / uvScale);
                    m_T.Add(ci); m_T.Add(j1); m_T.Add(j0);
                }
            }
        }

        public void AddCylinder(Vector3 baseCenter, float radius, float height, int segments, float uvScale = 1f, Quaternion? rotation = null)
            => AddFrustum(baseCenter, radius, radius, height, segments, true, true, uvScale, rotation);

        /// <summary>Surface of revolution around +Y. profile.x = radius, profile.y = height. Smooth normals.</summary>
        public void AddLathe(Vector3 center, IList<Vector2> profile, int segments, float uvScale = 1f, float angleFrom = 0f, float angleTo = 360f)
        {
            int rows = profile.Count;
            int baseIndex = m_V.Count;
            float total = 0f;
            var vLen = new float[rows];
            for (int r = 1; r < rows; r++) { total += Vector2.Distance(profile[r - 1], profile[r]); vLen[r] = total; }
            for (int s = 0; s <= segments; s++)
            {
                float a = Mathf.Lerp(angleFrom, angleTo, s / (float)segments) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                for (int r = 0; r < rows; r++)
                {
                    var p = profile[r];
                    Vector2 tangent = r == 0 ? profile[1] - profile[0] : r == rows - 1 ? profile[r] - profile[r - 1] : profile[r + 1] - profile[r - 1];
                    var n2 = new Vector2(tangent.y, -tangent.x).normalized; // outward in (radius, height)
                    var n = (dir * n2.x + Vector3.up * n2.y).normalized;
                    AddVertex(center + dir * p.x + Vector3.up * p.y, n, new Vector2(a * Mathf.Max(0.05f, p.x) / uvScale, vLen[r] / uvScale));
                }
            }
            for (int s = 0; s < segments; s++)
                for (int r = 0; r < rows - 1; r++)
                {
                    int i0 = baseIndex + s * rows + r;
                    int i1 = baseIndex + (s + 1) * rows + r;
                    m_T.Add(i0); m_T.Add(i0 + 1); m_T.Add(i1);
                    m_T.Add(i1); m_T.Add(i0 + 1); m_T.Add(i1 + 1);
                }
        }

        public void AddSphere(Vector3 center, float radius, int lon = 16, int lat = 10, float uvScale = 1f, float latFrom = -90f, float latTo = 90f)
        {
            var prof = new List<Vector2>();
            for (int i = 0; i <= lat; i++)
            {
                float t = Mathf.Lerp(latFrom, latTo, i / (float)lat) * Mathf.Deg2Rad;
                prof.Add(new Vector2(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius));
            }
            AddLathe(center, prof, lon, uvScale);
        }

        /// <summary>Prism from a simple polygon (XZ plane, counter-clockwise seen from above), between y0 and y1.</summary>
        public void AddPrism(IList<Vector2> poly, float y0, float y1, float uvScale = 1f, bool caps = true, Vector3 offset = default, Quaternion? rotation = null)
        {
            var rot = rotation ?? Quaternion.identity;
            int n = poly.Count;
            float run = 0;
            for (int i = 0; i < n; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % n];
                float len = Vector2.Distance(a, b);
                Vector3 A0 = offset + rot * new Vector3(a.x, y0, a.y), B0 = offset + rot * new Vector3(b.x, y0, b.y);
                Vector3 A1 = offset + rot * new Vector3(a.x, y1, a.y), B1 = offset + rot * new Vector3(b.x, y1, b.y);
                float u0 = run / uvScale, u1 = (run + len) / uvScale, v = (y1 - y0) / uvScale;
                AddQuad(B0, A0, A1, B1, new Vector2(u1, 0), new Vector2(u0, 0), new Vector2(u0, v), new Vector2(u1, v));
                run += len;
            }
            if (!caps) return;
            var tris = Triangulate(poly);
            for (int t = 0; t < tris.Count; t += 3)
            {
                var a = poly[tris[t]]; var b = poly[tris[t + 1]]; var c = poly[tris[t + 2]];
                Vector3 TA = offset + rot * new Vector3(a.x, y1, a.y), TB = offset + rot * new Vector3(b.x, y1, b.y), TC = offset + rot * new Vector3(c.x, y1, c.y);
                AddTriangle(TA, TC, TB, a / uvScale, c / uvScale, b / uvScale);
                Vector3 BA = offset + rot * new Vector3(a.x, y0, a.y), BB = offset + rot * new Vector3(b.x, y0, b.y), BC = offset + rot * new Vector3(c.x, y0, c.y);
                AddTriangle(BA, BB, BC, a / uvScale, b / uvScale, c / uvScale);
            }
        }

        /// <summary>Flat polygon in a vertical plane (XY), extruded along Z by depth. Used for gates, facades, silhouettes.</summary>
        public void AddExtrudedXY(IList<Vector2> poly, float depth, Vector3 offset, Quaternion rotation, float uvScale = 1f)
        {
            // Build in XZ then rotate so polygon Y becomes world Y.
            var swap = Quaternion.Euler(-90, 0, 0);
            AddPrism(poly, -depth * 0.5f, depth * 0.5f, uvScale, true, offset, rotation * swap);
        }

        /// <summary>Semicircular arch band in the XY plane (opening along local Z).</summary>
        public void AddArch(Vector3 center, float innerRadius, float outerRadius, float depth, int segments, Quaternion rot, float uvScale = 1f)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.PI * i / segments, a1 = Mathf.PI * (i + 1) / segments;
                Vector3 di0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0), di1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0);
                for (int side = -1; side <= 1; side += 2)
                {
                    float z = side * depth * 0.5f;
                    Vector3 o0 = center + rot * (di0 * outerRadius + Vector3.forward * z), o1 = center + rot * (di1 * outerRadius + Vector3.forward * z);
                    Vector3 i0 = center + rot * (di0 * innerRadius + Vector3.forward * z), i1 = center + rot * (di1 * innerRadius + Vector3.forward * z);
                    if (side > 0) AddQuadWorldUV(i0, o0, o1, i1, uvScale); else AddQuadWorldUV(o0, i0, i1, o1, uvScale);
                }
                // outer and inner skins
                Vector3 zf = rot * (Vector3.forward * depth * 0.5f);
                Vector3 O0 = center + rot * (di0 * outerRadius), O1 = center + rot * (di1 * outerRadius);
                Vector3 I0 = center + rot * (di0 * innerRadius), I1 = center + rot * (di1 * innerRadius);
                AddQuadWorldUV(O0 - zf, O1 - zf, O1 + zf, O0 + zf, uvScale);
                AddQuadWorldUV(I0 + zf, I1 + zf, I1 - zf, I0 - zf, uvScale);
            }
        }

        /// <summary>Torus around +Y.</summary>
        public void AddTorus(Vector3 center, float majorRadius, float minorRadius, int majorSeg, int minorSeg, Quaternion rot)
        {
            int baseIndex = m_V.Count;
            for (int i = 0; i <= majorSeg; i++)
            {
                float u = i / (float)majorSeg * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(u), 0, Mathf.Sin(u));
                for (int j = 0; j <= minorSeg; j++)
                {
                    float v = j / (float)minorSeg * Mathf.PI * 2f;
                    var n = dir * Mathf.Cos(v) + Vector3.up * Mathf.Sin(v);
                    var p = dir * majorRadius + n * minorRadius;
                    AddVertex(center + rot * p, rot * n, new Vector2(i / (float)majorSeg * 4f, j / (float)minorSeg));
                }
            }
            int stride = minorSeg + 1;
            for (int i = 0; i < majorSeg; i++)
                for (int j = 0; j < minorSeg; j++)
                {
                    int a = baseIndex + i * stride + j, b = baseIndex + (i + 1) * stride + j;
                    m_T.Add(a); m_T.Add(a + 1); m_T.Add(b);
                    m_T.Add(b); m_T.Add(a + 1); m_T.Add(b + 1);
                }
        }

        /// <summary>A tube following a polyline (round cross-section). Used for glasses frames, rails, wires, fingers.</summary>
        public void AddTube(IList<Vector3> path, float radius, int sides, bool caps = true)
        {
            if (path.Count < 2) return;
            int baseIndex = m_V.Count;
            Vector3 prevUp = Vector3.up;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 fwd = i == 0 ? path[1] - path[0] : i == path.Count - 1 ? path[i] - path[i - 1] : (path[i + 1] - path[i - 1]);
                fwd = fwd.sqrMagnitude < 1e-8f ? Vector3.forward : fwd.normalized;
                Vector3 right = Vector3.Cross(prevUp, fwd);
                if (right.sqrMagnitude < 1e-6f) right = Vector3.Cross(Vector3.right, fwd);
                right.Normalize();
                Vector3 up = Vector3.Cross(fwd, right).normalized;
                prevUp = up;
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    var n = right * Mathf.Cos(a) + up * Mathf.Sin(a);
                    AddVertex(path[i] + n * radius, n, new Vector2(s / (float)sides, i));
                }
            }
            int stride = sides + 1;
            for (int i = 0; i < path.Count - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a = baseIndex + i * stride + s, b = baseIndex + (i + 1) * stride + s;
                    m_T.Add(a); m_T.Add(a + 1); m_T.Add(b);
                    m_T.Add(a + 1); m_T.Add(b + 1); m_T.Add(b);
                }
            if (caps)
            {
                AddDisc(path[0], (path[0] - path[1]).normalized, radius, sides);
                AddDisc(path[path.Count - 1], (path[path.Count - 1] - path[path.Count - 2]).normalized, radius, sides);
            }
        }

        public void AddDisc(Vector3 center, Vector3 normal, float radius, int sides)
        {
            var rot = Quaternion.FromToRotation(Vector3.up, normal);
            int c = AddVertex(center, normal, new Vector2(0.5f, 0.5f));
            int first = m_V.Count;
            for (int s = 0; s <= sides; s++)
            {
                float a = s / (float)sides * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                AddVertex(center + rot * d * radius, normal, new Vector2(0.5f + d.x * 0.5f, 0.5f + d.z * 0.5f));
            }
            for (int s = 0; s < sides; s++) { m_T.Add(c); m_T.Add(first + s + 1); m_T.Add(first + s); }
        }

        /// <summary>Rounded-rectangle plate in XY, thickness along Z. Used for lenses, plaques, controller faces.</summary>
        public void AddRoundedPlate(Vector3 center, float width, float height, float cornerRadius, float thickness, Quaternion rot, int cornerSeg = 5)
        {
            var poly = RoundedRect(width, height, cornerRadius, cornerSeg);
            AddExtrudedXY(poly, thickness, center, rot, Mathf.Max(width, height));
        }

        public static List<Vector2> RoundedRect(float width, float height, float r, int seg)
        {
            r = Mathf.Min(r, Mathf.Min(width, height) * 0.5f);
            var pts = new List<Vector2>();
            Vector2[] centers = { new Vector2(width / 2 - r, height / 2 - r), new Vector2(-width / 2 + r, height / 2 - r), new Vector2(-width / 2 + r, -height / 2 + r), new Vector2(width / 2 - r, -height / 2 + r) };
            for (int c = 0; c < 4; c++)
                for (int i = 0; i <= seg; i++)
                {
                    float a = (c * 90f + i * 90f / seg) * Mathf.Deg2Rad;
                    pts.Add(centers[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
            return pts;
        }

        /// <summary>Ear-clipping triangulation for simple polygons (either winding). Returns indices, CCW.</summary>
        public static List<int> Triangulate(IList<Vector2> poly)
        {
            var result = new List<int>();
            int n = poly.Count;
            if (n < 3) return result;
            var idx = new List<int>();
            float area = 0;
            for (int i = 0; i < n; i++) { var a = poly[i]; var b = poly[(i + 1) % n]; area += a.x * b.y - b.x * a.y; }
            if (area > 0) for (int i = 0; i < n; i++) idx.Add(i); else for (int i = n - 1; i >= 0; i--) idx.Add(i);
            int guard = 0;
            while (idx.Count > 3 && guard++ < 10000)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int i0 = idx[(i + idx.Count - 1) % idx.Count], i1 = idx[i], i2 = idx[(i + 1) % idx.Count];
                    Vector2 a = poly[i0], b = poly[i1], c = poly[i2];
                    if (Cross(b - a, c - b) <= 0) continue;
                    bool inside = false;
                    for (int k = 0; k < idx.Count; k++)
                    {
                        int ik = idx[k];
                        if (ik == i0 || ik == i1 || ik == i2) continue;
                        if (PointInTri(poly[ik], a, b, c)) { inside = true; break; }
                    }
                    if (inside) continue;
                    result.Add(i0); result.Add(i1); result.Add(i2);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            if (idx.Count == 3) { result.Add(idx[0]); result.Add(idx[1]); result.Add(idx[2]); }
            return result;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static bool PointInTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }
    }
}

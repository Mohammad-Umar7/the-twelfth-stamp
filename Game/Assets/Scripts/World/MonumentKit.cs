using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Stone architecture with depth for the exhibition courtyards (Egypt first): a pylon gateway with battered towers,
    /// columns with papyrus capitals, walls with a cavetto cornice and torus edging, and palm silhouettes. Generic museum
    /// architecture: no inscriptions, deities or sacred objects. Front faces local -Z unless noted.
    /// </summary>
    public static class MonumentKit
    {
        /// <summary>A wall run with a rounded torus edge and a flared cavetto cornice on top (batched, one collider).</summary>
        public static GameObject Wall(Transform parent, string name, Vector3 center, Vector3 size, Material stone, Material trim, float uv = 3.5f)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = center;
            var k = new KitBatch();
            k.Box(stone, Vector3.zero, size, uv);
            bool alongX = size.x >= size.z;
            Vector3 Grow(float l, float h, float t) => alongX ? new Vector3(size.x + l, h, size.z + t) : new Vector3(size.x + t, h, size.z + l);
            // Torus moulding band, then the cavetto flare (stepped slabs read as a curve at game distance).
            k.Box(trim, new Vector3(0, size.y / 2 - 0.05f, 0), Grow(0.04f, 0.1f, 0.08f), uv);
            k.Box(trim, new Vector3(0, size.y / 2 + 0.06f, 0), Grow(0.08f, 0.12f, 0.16f), uv);
            k.Box(trim, new Vector3(0, size.y / 2 + 0.18f, 0), Grow(0.12f, 0.12f, 0.3f), uv);
            k.Box(trim, new Vector3(0, size.y / 2 + 0.3f, 0), Grow(0.16f, 0.1f, 0.42f), uv);
            k.Bake(root, name);
            var col = root.gameObject.AddComponent<BoxCollider>();
            col.size = size;
            return root.gameObject;
        }

        /// <summary>A column with a round base, a slightly tapering shaft with shallow flutes, and an open papyrus capital.</summary>
        public static GameObject Column(Transform parent, Vector3 pos, float radius, float height, Material stone, Material trim)
        {
            var root = new GameObject("Column").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            var b = new MeshBuilder();
            b.AddCylinder(Vector3.zero, radius * 1.45f, 0.22f, 20, 2f);                              // base drum
            float shaft = height - 0.22f - 0.85f;
            b.AddFrustum(new Vector3(0, 0.22f, 0), radius, radius * 0.88f, shaft, 20, false, false, 2f);
            for (int i = 0; i < 3; i++)                                                               // binding bands under the capital
                b.AddCylinder(new Vector3(0, 0.22f + shaft - 0.24f + i * 0.08f, 0), radius * 0.92f, 0.04f, 20);
            b.AddFrustum(new Vector3(0, 0.22f + shaft, 0), radius * 0.88f, radius * 1.75f, 0.7f, 20, false, true, 2f); // open capital
            b.AddCylinder(new Vector3(0, height - 0.15f, 0), radius * 1.05f, 0.15f, 4, 2f, Quaternion.Euler(0, 45, 0)); // abacus
            MeshFactory.FromBuilder("ColumnBody", b, stone, root);
            var flutes = new MeshBuilder();
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(a) * radius * 0.96f, 0.22f + shaft * 0.5f, Mathf.Sin(a) * radius * 0.96f);
                flutes.AddBox(p, new Vector3(0.025f, shaft * 0.9f, 0.025f), Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0));
            }
            MeshFactory.FromBuilder("Flutes", flutes, trim, root);
            var col = root.gameObject.AddComponent<CapsuleCollider>();
            col.radius = radius * 1.2f; col.height = height; col.center = new Vector3(0, height / 2, 0);
            return root.gameObject;
        }

        /// <summary>
        /// A pylon gateway: two battered (inward-leaning) towers with cavetto cornices either side of a lintelled gate.
        /// Its silhouette carries the place from across the courtyard. Front faces local -Z.
        /// </summary>
        public static GameObject Pylon(Transform parent, Vector3 pos, float yaw, float towerW, float towerH, float gateW, Material stone, Material trim,
            Material[] paint = null, Material pole = null, Material pennant = null, Material groove = null)
        {
            var root = new GameObject("Pylon").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            root.localRotation = Quaternion.Euler(0, yaw, 0);
            var k = new KitBatch();
            float depth = 2.2f;
            foreach (float side in new[] { -1f, 1f })
            {
                float cx = side * (gateW / 2 + towerW / 2);
                // Battered tower: stacked slabs, each a little narrower (reads as a sloped face).
                int steps = 8;
                for (int i = 0; i < steps; i++)
                {
                    float t0 = i / (float)steps, t1 = (i + 1) / (float)steps;
                    float wv = Mathf.Lerp(towerW, towerW * 0.82f, (t0 + t1) / 2), dv = Mathf.Lerp(depth, depth * 0.8f, (t0 + t1) / 2);
                    k.Box(stone, new Vector3(cx, towerH * (t0 + t1) / 2, 0), new Vector3(wv, towerH / steps + 0.01f, dv), 3.5f);
                }
                k.Box(trim, new Vector3(cx, towerH + 0.12f, 0), new Vector3(towerW * 0.86f, 0.24f, depth * 0.86f), 3.5f);
                k.Box(trim, new Vector3(cx, towerH + 0.36f, 0), new Vector3(towerW * 0.92f, 0.24f, depth * 0.98f), 3.5f);
                // A shallow recessed channel on the face (where a flagstaff would stand), giving the face depth.
                k.Box(trim, new Vector3(cx, towerH * 0.45f, -depth * 0.41f), new Vector3(0.28f, towerH * 0.85f, 0.06f), 3.5f);
                // Torus rolls up the battered outer edges of the face.
                for (int e = -1; e <= 1; e += 2)
                    k.Of(trim).AddTube(new List<Vector3> { new Vector3(cx + e * towerW / 2f, 0f, -depth / 2f), new Vector3(cx + e * towerW * 0.41f, towerH, -depth * 0.4f) }, 0.07f, 8);
                float FaceZ(float t) => -Mathf.Lerp(depth, depth * 0.8f, t) / 2f;
                if (groove != null)
                {
                    // Relief registers: incised bands framing rows of plain panels (decorative; no inscriptions).
                    foreach (float t in new[] { 0.2f, 0.47f, 0.74f })
                    {
                        float wv = Mathf.Lerp(towerW, towerW * 0.82f, t) - 0.5f, z = FaceZ(t);
                        k.Box(groove, new Vector3(cx, towerH * t, z), new Vector3(wv, 0.05f, 0.07f));
                        k.Box(groove, new Vector3(cx, towerH * t + 0.8f, z), new Vector3(wv, 0.035f, 0.07f));
                        int panels = Mathf.Max(2, Mathf.FloorToInt(wv / 0.55f));
                        for (int pi = 0; pi < panels; pi++)
                        {
                            float px = cx - wv / 2f + (pi + 0.5f) * wv / panels;
                            if (Mathf.Abs(px - cx) < 0.3f) continue; // keep the flagstaff channel clear
                            k.Box(groove, new Vector3(px, towerH * t + 0.4f, z), new Vector3(0.22f, 0.52f, 0.07f));
                        }
                    }
                }
                if (paint != null && paint.Length > 0)
                {
                    // Faded painted stripes along the cornice.
                    float cw = towerW * 0.92f, cz = -depth * 0.49f - 0.006f;
                    int stripes = Mathf.FloorToInt(cw / 0.2f);
                    for (int si = 0; si < stripes; si++)
                        k.Box(paint[si % paint.Length], new Vector3(cx - cw / 2f + (si + 0.5f) * cw / stripes, towerH + 0.36f, cz), new Vector3(0.11f, 0.18f, 0.012f));
                }
                if (pole != null)
                {
                    // A cedar flagstaff in the channel, braced to the face, with a long pennant.
                    var foot = new Vector3(cx, 0f, -depth * 0.5f - 0.18f);
                    k.Of(pole).AddCylinder(foot, 0.075f, towerH + 2.4f, 10);
                    foreach (float t in new[] { 0.35f, 0.7f })
                    {
                        float zf = FaceZ(t);
                        k.Box(pole, new Vector3(cx, towerH * t, (zf + foot.z) / 2f), new Vector3(0.2f, 0.12f, Mathf.Abs(foot.z - zf) + 0.04f));
                    }
                    if (pennant != null)
                        k.Box(pennant, new Vector3(cx + 0.21f, towerH + 1.25f, foot.z), new Vector3(0.34f, 2.2f, 0.02f), Quaternion.Euler(0f, 0f, -4f));
                }
            }
            float gateH = towerH * 0.62f;
            k.Box(stone, new Vector3(0, gateH + 0.45f, 0), new Vector3(gateW + 0.4f, 0.9f, depth * 0.7f), 3.5f);   // lintel
            k.Box(trim, new Vector3(0, gateH + 1.0f, 0), new Vector3(gateW + 0.6f, 0.2f, depth * 0.8f), 3.5f);
            if (paint != null && paint.Length > 0)
            {
                int stripes = Mathf.FloorToInt((gateW + 0.6f) / 0.2f);
                for (int si = 0; si < stripes; si++)
                    k.Box(paint[si % paint.Length], new Vector3(-(gateW + 0.6f) / 2f + (si + 0.5f) * (gateW + 0.6f) / stripes, gateH + 1.0f, -depth * 0.4f - 0.006f), new Vector3(0.11f, 0.15f, 0.012f));
            }
            foreach (float side in new[] { -1f, 1f })
                k.Box(stone, new Vector3(side * (gateW / 2 - 0.25f), gateH / 2, 0), new Vector3(0.5f, gateH, depth * 0.7f), 3.5f); // jambs
            k.Bake(root, "Pylon");
            foreach (float side in new[] { -1f, 1f })
            {
                var c = root.gameObject.AddComponent<BoxCollider>();
                c.center = new Vector3(side * (gateW / 2 + towerW / 2), towerH / 2, 0);
                c.size = new Vector3(towerW, towerH, depth);
            }
            return root.gameObject;
        }

        /// <summary>
        /// A date palm: a curved, ringed trunk with a crown shaft, a crown of feather fronds (a drooping rachis with paired
        /// leaflets), optionally a skirt of dead fronds and date clusters. Reads as a palm up close and in silhouette.
        /// </summary>
        public static GameObject Palm(Transform parent, Vector3 pos, float height, int seed, Material trunk, Material frond, Material dead = null, Material dates = null)
        {
            var rnd = new System.Random(seed);
            var root = new GameObject("Palm").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            var path = new List<Vector3>();
            float lean = (float)rnd.NextDouble() * 0.12f + 0.04f;
            float dir = (float)rnd.NextDouble() * Mathf.PI * 2f;
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f;
                path.Add(new Vector3(Mathf.Cos(dir) * lean * height * t * t, height * t, Mathf.Sin(dir) * lean * height * t * t));
            }
            var tb = new MeshBuilder();
            tb.AddTube(path, height * 0.025f, 9);
            for (int i = 1; i < 8; i++) tb.AddCylinder(path[i] - Vector3.up * 0.04f, height * 0.03f, 0.08f, 9);
            var top = path[path.Count - 1];
            tb.AddFrustum(top - Vector3.up * height * 0.06f, height * 0.03f, height * 0.042f, height * 0.07f, 9); // crown shaft
            MeshFactory.FromBuilder("Trunk", tb, trunk, root);

            var fb = new MeshBuilder();
            const int n = 14;
            for (int i = 0; i < n; i++)
            {
                float yaw = i / (float)n * 360f + (float)rnd.NextDouble() * 18f;
                float lift = 18f + (float)rnd.NextDouble() * 30f - (i % 3) * 9f;
                float len = height * (0.3f + (float)rnd.NextDouble() * 0.08f);
                Frond(fb, top + Vector3.up * height * 0.012f, yaw, lift, len, -55f - (float)rnd.NextDouble() * 20f);
            }
            MeshFactory.FromBuilder("Fronds", fb, frond, root);
            if (dead != null)
            {
                var db = new MeshBuilder();
                for (int i = 0; i < 7; i++)
                    Frond(db, top - Vector3.up * height * 0.035f, i * 51f + (float)rnd.NextDouble() * 20f, -48f, height * 0.22f, -86f);
                MeshFactory.FromBuilder("DeadFronds", db, dead, root);
            }
            if (dates != null)
            {
                var cb = new MeshBuilder();
                for (int c = 0; c < 3; c++)
                {
                    float a = (c * 120f + (float)rnd.NextDouble() * 40f) * Mathf.Deg2Rad;
                    var cc = top + new Vector3(Mathf.Cos(a) * height * 0.05f, -height * 0.055f, Mathf.Sin(a) * height * 0.05f);
                    for (int d = 0; d < 9; d++)
                        cb.AddSphere(cc + new Vector3((float)(rnd.NextDouble() - 0.5) * 0.16f, -(float)rnd.NextDouble() * 0.22f, (float)(rnd.NextDouble() - 0.5) * 0.16f), 0.045f, 6, 4);
                }
                MeshFactory.FromBuilder("Dates", cb, dates, root);
            }
            var col = root.gameObject.AddComponent<CapsuleCollider>();
            col.radius = height * 0.04f; col.height = height; col.center = new Vector3(0, height / 2, 0);
            return root.gameObject;
        }

        /// <summary>One feather frond: a rachis arcing from <paramref name="lift"/> degrees up to <paramref name="tipElev"/>
        /// degrees down, with paired leaflets swept down and forward (double-sided triangles).</summary>
        static void Frond(MeshBuilder b, Vector3 start, float yawDeg, float lift, float len, float tipElev)
        {
            var dirH = Quaternion.Euler(0, yawDeg, 0) * Vector3.forward;
            var side = Vector3.Cross(Vector3.up, dirH).normalized;
            const int N = 12;
            var pts = new List<Vector3>(N + 1);
            var p = start;
            float step = len / N;
            for (int i = 0; i <= N; i++)
            {
                pts.Add(p);
                float s = i / (float)N;
                float elev = Mathf.Lerp(lift, tipElev, Mathf.Pow(s, 1.5f)) * Mathf.Deg2Rad;
                p += (dirH * Mathf.Cos(elev) + Vector3.up * Mathf.Sin(elev)) * step;
            }
            b.AddTube(pts, len * 0.01f, 4, false);
            for (int i = 1; i < N; i++)
            {
                float s = i / (float)N;
                var a = pts[i];
                var tan = (pts[i + 1] - a).normalized;
                float leaf = len * (0.05f + 0.2f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, s * 1.05f)));
                var b1 = a + tan * step * 0.45f;
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    var tip = a + (side * sd * 0.8f + Vector3.down * 0.42f + tan * 0.3f).normalized * leaf;
                    b.AddTriangle(a, b1, tip, Vector2.zero, Vector2.right, Vector2.up);
                    b.AddTriangle(a, tip, b1, Vector2.zero, Vector2.up, Vector2.right);
                }
            }
        }

        /// <summary>A plain obelisk on a stepped base: a tapering square shaft and a metal-capped pyramidion. No inscriptions.</summary>
        public static GameObject Obelisk(Transform parent, Vector3 pos, float height, Material stone, Material trim, Material cap)
        {
            var root = new GameObject("Obelisk").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            float w = height * 0.1f, capBase = height - w * 0.9f, ht = w * 0.36f;
            var b = new MeshBuilder();
            b.AddBox(new Vector3(0, 0.15f, 0), new Vector3(w * 2.4f, 0.3f, w * 2.4f), 2f);
            b.AddBox(new Vector3(0, 0.42f, 0), new Vector3(w * 1.8f, 0.24f, w * 1.8f), 2f);
            SquareFrustum(b, 0.54f, capBase, w * 0.5f, ht, 3.5f);
            MeshFactory.FromBuilder("ObeliskBody", b, stone, root);
            var t = new MeshBuilder();
            t.AddBox(new Vector3(0, 0.31f, 0), new Vector3(w * 2.46f, 0.03f, w * 2.46f));
            t.AddBox(new Vector3(0, 0.555f, 0), new Vector3(w * 1.86f, 0.03f, w * 1.86f));
            MeshFactory.FromBuilder("ObeliskTrim", t, trim, root);
            var p = new MeshBuilder();
            var apex = new Vector3(0, height, 0);
            Vector3 C(float x, float z) => new Vector3(x * ht, capBase, z * ht);
            p.AddTriangle(C(-1, 1), C(1, 1), apex, Vector2.zero, Vector2.right, new Vector2(0.5f, 1f));
            p.AddTriangle(C(1, -1), C(-1, -1), apex, Vector2.zero, Vector2.right, new Vector2(0.5f, 1f));
            p.AddTriangle(C(1, 1), C(1, -1), apex, Vector2.zero, Vector2.right, new Vector2(0.5f, 1f));
            p.AddTriangle(C(-1, -1), C(-1, 1), apex, Vector2.zero, Vector2.right, new Vector2(0.5f, 1f));
            MeshFactory.FromBuilder("Pyramidion", p, cap, root);
            var col = root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, height / 2, 0); col.size = new Vector3(w * 1.8f, height, w * 1.8f);
            return root.gameObject;
        }

        /// <summary>A square frustum with flat faces (MeshBuilder's round frustum has smooth normals).</summary>
        static void SquareFrustum(MeshBuilder b, float y0, float y1, float halfBottom, float halfTop, float uv)
        {
            Vector3 B(float x, float z) => new Vector3(x * halfBottom, y0, z * halfBottom);
            Vector3 T(float x, float z) => new Vector3(x * halfTop, y1, z * halfTop);
            b.AddQuadWorldUV(B(-1, 1), B(1, 1), T(1, 1), T(-1, 1), uv);
            b.AddQuadWorldUV(B(1, -1), B(-1, -1), T(-1, -1), T(1, -1), uv);
            b.AddQuadWorldUV(B(1, 1), B(1, -1), T(1, -1), T(1, 1), uv);
            b.AddQuadWorldUV(B(-1, -1), B(-1, 1), T(-1, 1), T(-1, -1), uv);
        }
    }
}

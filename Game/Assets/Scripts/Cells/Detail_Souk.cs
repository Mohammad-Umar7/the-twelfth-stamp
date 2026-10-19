using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    // The souk lane outside the Marrakech shop in the remembered (normal) version: the walk that opens the story.
    // A static shell (paving, terracotta facades with open stall cells and folded shutters, reed / lattice roofs, the
    // closed souk gate behind the start and the horseshoe gate into the shop's alley) plus many small groups (each
    // stall's goods, the arches, lantern clusters, cloth canopies) that rise into place as the player walks
    // (MemoryReveal). Small goods share one palette material so a whole stall is one or two renderers.
    public partial class ShopCell
    {
        /// <summary>Where the memory starts: the far end of the souk lane, facing the shop (local +Z).</summary>
        public const float SoukStartZ = -37f;

        // Layout (cell-local metres, +Z towards the shop). The lane is 4.5 m between stall fronts; it opens into a
        // small square (6 m) at the start, closed behind by the souk gate. Arches stand at z -34, -26 and -18; the
        // horseshoe gate at z -10.85..-10.25 joins the old lane.
        const float SkLane = 2.25f, SkSquare = 3.0f, SkOuter = 4.45f, SkTop = 5.4f, SkBeamY = 4.85f, SkLintel = 2.55f;
        const float SkEndZ = -39.75f, SkSquareZ = -34.3f, SkGateZ0 = -10.85f, SkGateZ1 = -10.25f;
        const float SkRailLane = 1.5f, SkRailSquare = 2.3f;
        static readonly float[] SkArchZ = { -34.0f, -26.0f, -18.0f };

        enum SkKind { Spice, Ceramic, Carpet, Slipper, Basket, Fabric, Metal, Lantern, Leather, Tea, Door, Alley, Fountain }

        /// <summary>One bay of a facade: an open stall cell (or a door, an alley mouth, a wall fountain).</summary>
        struct SkBay
        {
            public int Side;          // -1 left (x < 0), +1 right
            public float Z0, Z1;      // opening along the lane
            public float Half;        // facade line |x|
            public float Depth;       // cell depth behind the facade line
            public float Deck;        // raised floor height inside the cell
            public SkKind Kind;
            public bool Flap;         // upper shutter propped up as an awning (else folding leaves inside)
            public bool Window, WindowLit;
            public float WindowZ;     // centre of the small upstairs window, if any
            public float W => Z1 - Z0;
            public float Zc => (Z0 + Z1) * 0.5f;
        }

        sealed class SkMats
        {
            public Material Plaster, Ochre, Zellige, Timber, Cedar, Reed, Paving, Brass, Carpet;
            public Material[] Cloth;
        }

        // ------------------------------------------------------------------ palette: flat colours on one material
        /// <summary>
        /// Flat colours for goods and small details on one shared material: an 8x8 runtime palette texture (base) and a
        /// matching emission map (black except the lantern-glass cells), so a stall of spices, slippers and pots is one
        /// renderer and lantern glass glows from the same draw.
        /// </summary>
        static class SkPal
        {
            public const int Ink = 0, Iron = 1, WoodDark = 2, Wood = 3, WoodLight = 4, Straw = 5, StrawDark = 6, Leather = 7,
                LeatherDark = 8, Saffron = 9, Paprika = 10, Cumin = 11, Henna = 12, Ochre = 13, Cinnamon = 14, Salt = 15,
                Rose = 16, Indigo = 17, Teal = 18, Cobalt = 19, Madder = 20, Cream = 21, Stucco = 22, Clay = 23,
                ClayLight = 24, GlazeGreen = 25, GlazeBlue = 26, GlazeYellow = 27, White = 28, StoneDark = 29, Olive = 30,
                Plum = 31, Mint = 32, TileGreen = 33, Void = 34, Rust = 35, Sand = 36, Water = 37,
                GlowAmber = 40, GlowHoney = 41, GlowRed = 42, GlowGreen = 43, GlowBlue = 44, GlowWindow = 45;

            static readonly int[] s_Spices = { Saffron, Paprika, Cumin, Henna, Ochre, Cinnamon, Salt, Rose, Mint, Rust };
            public static int Spice(System.Random r) => s_Spices[r.Next(s_Spices.Length)];

            static Material s_Mat;

            public static Vector2 UV(int i) => new Vector2((i % 8 + 0.5f) / 8f, (i / 8 + 0.5f) / 8f);

            public static Material Mat
            {
                get
                {
                    if (s_Mat != null) return s_Mat;
                    var baseCols = new Color[64];
                    var glowCols = new Color[64];
                    for (int i = 0; i < 64; i++) { baseCols[i] = new Color(0.5f, 0.45f, 0.4f); glowCols[i] = Color.black; }
                    void Set(int i, float r, float g, float b) => baseCols[i] = new Color(r, g, b);
                    void Glow(int i, Color b, Color e) { baseCols[i] = b; glowCols[i] = e; }
                    Set(Ink, 0.06f, 0.05f, 0.045f); Set(Iron, 0.13f, 0.12f, 0.11f); Set(WoodDark, 0.24f, 0.15f, 0.09f);
                    Set(Wood, 0.42f, 0.27f, 0.15f); Set(WoodLight, 0.62f, 0.45f, 0.28f); Set(Straw, 0.8f, 0.64f, 0.38f);
                    Set(StrawDark, 0.58f, 0.43f, 0.24f); Set(Leather, 0.58f, 0.34f, 0.17f); Set(LeatherDark, 0.32f, 0.17f, 0.09f);
                    Set(Saffron, 0.93f, 0.63f, 0.12f); Set(Paprika, 0.68f, 0.16f, 0.08f); Set(Cumin, 0.54f, 0.35f, 0.16f);
                    Set(Henna, 0.4f, 0.46f, 0.16f); Set(Ochre, 0.8f, 0.52f, 0.2f); Set(Cinnamon, 0.5f, 0.25f, 0.12f);
                    Set(Salt, 0.9f, 0.88f, 0.82f); Set(Rose, 0.76f, 0.38f, 0.42f); Set(Indigo, 0.15f, 0.19f, 0.42f);
                    Set(Teal, 0.1f, 0.4f, 0.42f); Set(Cobalt, 0.13f, 0.26f, 0.62f); Set(Madder, 0.56f, 0.13f, 0.1f);
                    Set(Cream, 0.9f, 0.85f, 0.72f); Set(Stucco, 0.88f, 0.8f, 0.64f); Set(Clay, 0.68f, 0.34f, 0.2f);
                    Set(ClayLight, 0.8f, 0.52f, 0.35f); Set(GlazeGreen, 0.2f, 0.44f, 0.25f); Set(GlazeBlue, 0.18f, 0.33f, 0.58f);
                    Set(GlazeYellow, 0.87f, 0.68f, 0.2f); Set(White, 0.93f, 0.91f, 0.86f); Set(StoneDark, 0.38f, 0.32f, 0.26f);
                    Set(Olive, 0.36f, 0.37f, 0.15f); Set(Plum, 0.36f, 0.12f, 0.2f); Set(Mint, 0.46f, 0.63f, 0.42f);
                    Set(TileGreen, 0.12f, 0.37f, 0.23f); Set(Void, 0.035f, 0.03f, 0.028f); Set(Rust, 0.6f, 0.3f, 0.14f);
                    Set(Sand, 0.84f, 0.72f, 0.52f); Set(Water, 0.2f, 0.26f, 0.27f);
                    // Lantern glass and the pierced holes: emission = cell x 1.6, so every channel stays within ~0.4-1.6.
                    Glow(GlowAmber, new Color(0.6f, 0.45f, 0.25f), new Color(1f, 0.62f, 0.3f));
                    Glow(GlowHoney, new Color(0.62f, 0.5f, 0.3f), new Color(1f, 0.76f, 0.44f));
                    Glow(GlowRed, new Color(0.5f, 0.16f, 0.1f), new Color(0.8f, 0.26f, 0.14f));
                    Glow(GlowGreen, new Color(0.22f, 0.4f, 0.26f), new Color(0.38f, 0.62f, 0.36f));
                    Glow(GlowBlue, new Color(0.2f, 0.3f, 0.48f), new Color(0.3f, 0.5f, 0.66f));
                    Glow(GlowWindow, new Color(0.5f, 0.36f, 0.2f), new Color(0.55f, 0.36f, 0.18f));
                    var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false) { name = "SoukPalette", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                    tex.SetPixels(baseCols); tex.Apply(false, false);
                    var emi = new Texture2D(8, 8, TextureFormat.RGBA32, false) { name = "SoukPaletteGlow", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                    emi.SetPixels(glowCols); emi.Apply(false, false);
                    s_Mat = new Material(Mats.Template("Lit_Emissive")) { name = "Souk_Palette" };
                    s_Mat.SetColor("_BaseColor", Color.white);
                    s_Mat.SetTexture("_BaseMap", tex);
                    s_Mat.SetTexture("_EmissionMap", emi);
                    s_Mat.SetColor("_EmissionColor", new Color(1.6f, 1.6f, 1.6f));
                    s_Mat.SetFloat("_Smoothness", 0.28f);
                    s_Mat.SetFloat("_Metallic", 0f);
                    return s_Mat;
                }
            }
        }

        // ------------------------------------------------------------------ batching
        /// <summary>One group's geometry: textured parts per material (KitBatch) plus palette parts (one renderer).</summary>
        sealed class SkBatch
        {
            readonly KitBatch m_Kit = new KitBatch();
            readonly Dictionary<int, MeshBuilder> m_Pal = new Dictionary<int, MeshBuilder>();

            public MeshBuilder Of(Material m) => m_Kit.Of(m);
            public MeshBuilder Of(int colour)
            {
                if (!m_Pal.TryGetValue(colour, out var b)) { b = new MeshBuilder(); m_Pal[colour] = b; }
                return b;
            }
            public void Box(Material m, Vector3 c, Vector3 s, float uv = 1f) => m_Kit.Box(m, c, s, uv);
            public void Box(Material m, Vector3 c, Vector3 s, Quaternion r, float uv = 1f) => m_Kit.Box(m, c, s, r, uv);
            public void Box(int colour, Vector3 c, Vector3 s) => Of(colour).AddBox(c, s, Quaternion.identity);
            public void Box(int colour, Vector3 c, Vector3 s, Quaternion r) => Of(colour).AddBox(c, s, r);

            public void Bake(Transform parent, string name)
            {
                m_Kit.Bake(parent, name);
                if (m_Pal.Count == 0) return;
                var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
                foreach (var kv in m_Pal)
                {
                    var m = kv.Value.ToMesh();
                    int b0 = v.Count;
                    var mv = m.vertices;
                    v.AddRange(mv); n.AddRange(m.normals);
                    var c = SkPal.UV(kv.Key);
                    for (int i = 0; i < mv.Length; i++) uv.Add(c);
                    foreach (int i in m.triangles) t.Add(b0 + i);
                    Destroy(m);
                }
                var mesh = new Mesh { name = name + "_Palette" };
                if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
                mesh.RecalculateBounds();
                MeshFactory.Make(name + "_Palette", mesh, SkPal.Mat, parent);
                m_Pal.Clear();
            }
        }

        /// <summary>A group root directly under <paramref name="g"/>, at the group's base.</summary>
        static Transform SkGroup(Transform g, string name, Vector3 pos, float yaw = 0f)
        {
            var t = new GameObject(name).transform;
            t.SetParent(g, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        /// <summary>The memory paints this group in: it waits below the paving (sunk deep enough to hide its tallest part
        /// at the reveal's starting scale) and rises when the player comes within <paramref name="radius"/>.</summary>
        static void SkReveal(Transform root, float radius, float delay)
        {
            float top = 1f;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null) top = Mathf.Max(top, mf.transform.localPosition.y + mf.sharedMesh.bounds.max.y);
            foreach (var l in root.GetComponentsInChildren<Light>()) top = Mathf.Max(top, l.transform.localPosition.y);
            MemoryReveal.Attach(root.gameObject, radius, delay, Mathf.Max(1.4f, top * 0.55f + 0.3f));
        }

        static float SkR(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        // ------------------------------------------------------------------ geometry helpers
        static Vector2 SkUV(Vector3 p, Vector3 n, float uv)
        {
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            if (az >= ax && az >= ay) return new Vector2(p.x, p.y) / uv;
            if (ax >= ay) return new Vector2(p.z, p.y) / uv;
            return new Vector2(p.x, p.z) / uv;
        }

        /// <summary>Quad wound to face <paramref name="n"/>, with planar UVs (continuous across neighbouring quads).</summary>
        static void SkQuad(MeshBuilder b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 n, float uv = 1f)
        {
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p3 - p0), n) < 0f) { var t = p1; p1 = p3; p3 = t; }
            b.AddQuad(p0, p1, p2, p3, SkUV(p0, n, uv), SkUV(p1, n, uv), SkUV(p2, n, uv), SkUV(p3, n, uv));
        }

        static void SkTri(MeshBuilder b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 n, float uv = 1f)
        {
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), n) < 0f) { var t = p1; p1 = p2; p2 = t; }
            b.AddTriangle(p0, p1, p2, SkUV(p0, n, uv), SkUV(p1, n, uv), SkUV(p2, n, uv));
        }

        /// <summary>Angles from <paramref name="a0"/> to <paramref name="a1"/> in about <paramref name="seg"/> steps, with
        /// the rectangle's corner directions inserted so every fan quad ends on a single rectangle edge.</summary>
        static List<float> SkAngles(float a0, float a1, int seg, float hc, float hw, float yb, float yt)
        {
            var list = new List<float>();
            for (int i = 0; i <= seg; i++) list.Add(Mathf.Lerp(a0, a1, i / (float)seg));
            foreach (var c in new[] { Mathf.Atan2(yb - hc, hw), Mathf.Atan2(yt - hc, hw), Mathf.Atan2(yt - hc, -hw), Mathf.Atan2(yb - hc, -hw) + 2f * Mathf.PI })
            {
                float a = c;
                if (a > a1 + 0.0001f) a -= 2f * Mathf.PI;
                if (a > a0 + 0.0001f && a < a1 - 0.0001f) list.Add(a);
            }
            list.Sort();
            return list;
        }

        /// <summary>Where a ray from (0, hc) at angle <paramref name="a"/> leaves the rectangle |x| &lt;= hw, yb..yt.</summary>
        static Vector2 SkRect(float a, float hc, float hw, float yb, float yt)
        {
            float dx = Mathf.Cos(a), dy = Mathf.Sin(a), t = float.MaxValue;
            if (dx > 1e-5f) t = Mathf.Min(t, hw / dx);
            if (dx < -1e-5f) t = Mathf.Min(t, -hw / dx);
            if (dy > 1e-5f) t = Mathf.Min(t, (yt - hc) / dy);
            if (dy < -1e-5f) t = Mathf.Min(t, (yb - hc) / dy);
            return new Vector2(dx * t, hc + dy * t);
        }

        /// <summary>A flat face (plane z, facing <paramref name="n"/>) between a circle of radius r about (0, hc) and the
        /// rectangle |x| &lt;= hw, yb..yt, for angles a0..a1: wall faces around an arch, zellige spandrels.</summary>
        static void SkFan(MeshBuilder b, float r, float hc, float hw, float yb, float yt, float a0, float a1, float z, Vector3 n, float uv, int seg = 20)
        {
            var angles = SkAngles(a0, a1, seg, hc, hw, yb, yt);
            for (int i = 0; i < angles.Count - 1; i++)
            {
                float u0 = angles[i], u1 = angles[i + 1];
                var i0 = new Vector3(Mathf.Cos(u0) * r, hc + Mathf.Sin(u0) * r, z);
                var i1 = new Vector3(Mathf.Cos(u1) * r, hc + Mathf.Sin(u1) * r, z);
                Vector3 o0 = SkRect(u0, hc, hw, yb, yt), o1 = SkRect(u1, hc, hw, yb, yt);
                o0.z = z; o1.z = z;
                if ((o0 - i0).sqrMagnitude < 1e-6f) SkTri(b, i0, i1, o1, n, uv);
                else if ((o1 - i1).sqrMagnitude < 1e-6f) SkTri(b, i0, i1, o0, n, uv);
                else SkQuad(b, i0, i1, o1, o0, n, uv);
            }
        }

        /// <summary>
        /// A wall across the lane (local XY, from z0 to z1) pierced by a horseshoe opening: straight jambs at x = +-a up
        /// to the springing, then an arc of radius r about (0, hc) carried past the half circle. The wall spans
        /// |x| &lt;= hw up to y = top; its outer ends abut the facades and are left open.
        /// </summary>
        static void SkHorseshoeWall(MeshBuilder b, float hw, float top, float a, float r, float hc, float z0, float z1, float uv, int seg = 22)
        {
            float beta = Mathf.Acos(Mathf.Clamp(a / r, -1f, 1f)), ys = hc - r * Mathf.Sin(beta);
            SkFan(b, r, hc, hw, ys, top, -beta, Mathf.PI + beta, z0, Vector3.back, uv, seg);
            SkFan(b, r, hc, hw, ys, top, -beta, Mathf.PI + beta, z1, Vector3.forward, uv, seg);
            // Intrados: the soffit of the arch, facing the opening.
            for (int i = 0; i < seg; i++)
            {
                float u0 = Mathf.Lerp(-beta, Mathf.PI + beta, i / (float)seg), u1 = Mathf.Lerp(-beta, Mathf.PI + beta, (i + 1) / (float)seg);
                var d0 = new Vector3(Mathf.Cos(u0), Mathf.Sin(u0), 0f); var d1 = new Vector3(Mathf.Cos(u1), Mathf.Sin(u1), 0f);
                var c = new Vector3(0f, hc, 0f);
                SkQuad(b, c + d0 * r + Vector3.forward * z0, c + d1 * r + Vector3.forward * z0, c + d1 * r + Vector3.forward * z1, c + d0 * r + Vector3.forward * z1, -(d0 + d1).normalized, uv);
            }
            // Piers below the springing: front, back and inner faces.
            foreach (float s in new[] { -1f, 1f })
            {
                float xi = s * a, xo = s * hw;
                SkQuad(b, new Vector3(xi, 0f, z0), new Vector3(xo, 0f, z0), new Vector3(xo, ys, z0), new Vector3(xi, ys, z0), Vector3.back, uv);
                SkQuad(b, new Vector3(xi, 0f, z1), new Vector3(xo, 0f, z1), new Vector3(xo, ys, z1), new Vector3(xi, ys, z1), Vector3.forward, uv);
                SkQuad(b, new Vector3(xi, 0f, z0), new Vector3(xi, 0f, z1), new Vector3(xi, ys, z1), new Vector3(xi, ys, z0), new Vector3(-s, 0f, 0f), uv);
            }
            // Top of the wall (seen from the open roof bays).
            SkQuad(b, new Vector3(-hw, top, z0), new Vector3(hw, top, z0), new Vector3(hw, top, z1), new Vector3(-hw, top, z1), Vector3.up, uv);
        }

        /// <summary>A raised band following an arc (annulus sector r0..r1 about (0, hc), angles a0..a1) on the face at
        /// z, standing proud by <paramref name="dz"/> (negative = towards -Z): an arch's carved ring or its outline.</summary>
        static void SkArcBand(MeshBuilder b, float r0, float r1, float hc, float a0, float a1, float z, float dz, int seg = 22, float uv = 1f)
        {
            var n = dz < 0f ? Vector3.back : Vector3.forward;
            float zf = z + dz;
            Vector3 P(float r, float a, float zz) => new Vector3(Mathf.Cos(a) * r, hc + Mathf.Sin(a) * r, zz);
            for (int i = 0; i < seg; i++)
            {
                float u0 = Mathf.Lerp(a0, a1, i / (float)seg), u1 = Mathf.Lerp(a0, a1, (i + 1) / (float)seg);
                SkQuad(b, P(r0, u0, zf), P(r1, u0, zf), P(r1, u1, zf), P(r0, u1, zf), n, uv);
                var dout = new Vector3(Mathf.Cos((u0 + u1) / 2f), Mathf.Sin((u0 + u1) / 2f), 0f);
                SkQuad(b, P(r1, u0, z), P(r1, u1, z), P(r1, u1, zf), P(r1, u0, zf), dout, uv);
                SkQuad(b, P(r0, u0, z), P(r0, u1, z), P(r0, u1, zf), P(r0, u0, zf), -dout, uv);
            }
            var e0 = new Vector3(Mathf.Sin(a0), -Mathf.Cos(a0), 0f);
            SkQuad(b, P(r0, a0, z), P(r1, a0, z), P(r1, a0, zf), P(r0, a0, zf), e0, uv);
            var e1 = new Vector3(-Mathf.Sin(a1), Mathf.Cos(a1), 0f);
            SkQuad(b, P(r0, a1, z), P(r1, a1, z), P(r1, a1, zf), P(r0, a1, zf), e1, uv);
        }

        /// <summary>A flat horseshoe-shaped panel (the opening's own outline) on the plane z: a closed gate's doors.</summary>
        static void SkHorseshoeFill(MeshBuilder b, float a, float r, float hc, float z, Vector3 n, float uv, int seg = 20)
        {
            float beta = Mathf.Acos(Mathf.Clamp(a / r, -1f, 1f)), ys = hc - r * Mathf.Sin(beta);
            var c = new Vector3(0f, hc, z);
            for (int i = 0; i < seg; i++)
            {
                float u0 = Mathf.Lerp(-beta, Mathf.PI + beta, i / (float)seg), u1 = Mathf.Lerp(-beta, Mathf.PI + beta, (i + 1) / (float)seg);
                SkTri(b, c, c + new Vector3(Mathf.Cos(u0), Mathf.Sin(u0), 0f) * r, c + new Vector3(Mathf.Cos(u1), Mathf.Sin(u1), 0f) * r, n, uv);
            }
            SkTri(b, c, new Vector3(a, ys, z), new Vector3(-a, ys, z), n, uv);
            SkQuad(b, new Vector3(-a, 0f, z), new Vector3(a, 0f, z), new Vector3(a, ys, z), new Vector3(-a, ys, z), n, uv);
        }

        /// <summary>Springing height of a horseshoe opening with jambs at +-a and arc radius r about (0, hc).</summary>
        static float SkSpring(float a, float r, float hc) => hc - r * Mathf.Sin(Mathf.Acos(Mathf.Clamp(a / r, -1f, 1f)));

        // ------------------------------------------------------------------ build
        /// <summary>Builds the souk lane from the old lane end (z -10.3) back to z -40 (see the class comment above).</summary>
        void BuildSouk(Transform g)
        {
            var m = new SkMats
            {
                // Plaster, zellige, paving, timber, reed and brass match the old lane's (ShopCell.Build) exactly, so the join
                // at z -10.3 has no seam. Linear colour: textured materials keep pale tints and let the texture carry it.
                Plaster = Mats.Lit(new Color(1f, 0.86f, 0.76f), 0.2f, 0f, "plaster_terracotta"),
                Zellige = Mats.Lit(Color.white, 0.6f, 0f, "tile_zellige"),
                Paving = Mats.Lit(new Color(0.7f, 0.6f, 0.5f), 0.3f, 0f, "plaza_stone"),
                Timber = Mats.Lit(new Color(0.92f, 0.84f, 0.76f), 0.35f, 0f, "wood_dark"),
                Reed = Mats.Lit(new Color(1f, 0.95f, 0.82f), 0.15f, 0f, "wood_oak_floor"),
                Brass = Mats.Lit(new Color(0.84f, 0.64f, 0.34f), 0.68f, 0.9f),
                Ochre = Mats.Lit(new Color(1f, 0.85f, 0.68f), 0.2f, 0f, "plaster_warm"),
                Carpet = Mats.Lit(Color.white, 0.15f, 0f, "carpet_rug"),
                Cloth = new[]
                {
                    // Canopy cloth glows faintly, like dusk light through fabric (kept under KitBatch's no-shadow threshold).
                    Mats.Lit(new Color(0.95f, 0.45f, 0.32f), 0.2f, 0f, "fabric_linen", true, new Color(0.19f, 0.09f, 0.064f)),
                    Mats.Lit(new Color(1f, 0.8f, 0.42f), 0.2f, 0f, "fabric_linen", true, new Color(0.19f, 0.152f, 0.08f)),
                    Mats.Lit(new Color(0.5f, 0.58f, 0.85f), 0.2f, 0f, "fabric_linen", true, new Color(0.1f, 0.116f, 0.17f)),
                },
            };
            m.Cedar = m.Reed; // doors, shutters, lintels and decks: the same warm planking as the reed mats (one renderer)
            var bays = SkLayout();

            // The static shell: paving, facades and cells, roofs, both gates (one renderer per material).
            var shellRoot = new GameObject("SoukShell").transform;
            shellRoot.SetParent(g, false);
            var shell = new SkBatch();
            SkFloor(shell, m);
            foreach (var bay in bays) SkCell(shell, m, bay);
            SkFacades(shell, m, bays);
            SkEndGate(shell, m);
            SkLaneGate(shell, m);
            SkRoofs(shell, m);
            shell.Bake(shellRoot, "SoukShell");
            SkColliders(shellRoot);

            // What the memory paints in: every stall's goods, the arches, lantern clusters and canopies.
            int n = 0;
            foreach (var bay in bays)
            {
                if (bay.Kind == SkKind.Door || bay.Kind == SkKind.Alley || bay.Kind == SkKind.Fountain) continue;
                SkStall(g, m, bay, 101 + n * 37, n);
                n++;
            }
            for (int i = 0; i < SkArchZ.Length; i++) SkArch(g, m, SkArchZ[i], i);
            SkLanterns(g, m);
            SkCanopies(g, m);
        }

        /// <summary>The bays along both facades, far end first. Arch piers and the square's corner fill the gaps.</summary>
        static List<SkBay> SkLayout()
        {
            var list = new List<SkBay>();
            void Add(int side, float z0, float z1, float half, float depth, SkKind kind, bool flap)
            {
                float deck = kind == SkKind.Carpet || kind == SkKind.Basket ? 0.14f : kind == SkKind.Ceramic || kind == SkKind.Metal || kind == SkKind.Tea ? 0.3f : 0.42f;
                if (kind == SkKind.Door || kind == SkKind.Alley || kind == SkKind.Fountain) deck = 0f;
                int i = list.Count;
                bool window = kind != SkKind.Fountain && kind != SkKind.Carpet && (half > SkLane || (i / 2 + (side > 0 ? 1 : 0)) % 2 == 0 || kind == SkKind.Door);
                float wz = (z0 + z1) / 2f + (z1 - z0 > 1.6f ? ((i * 37) % 7 - 3) * 0.09f : 0f);
                list.Add(new SkBay { Side = side, Z0 = z0, Z1 = z1, Half = half, Depth = depth, Deck = deck, Kind = kind, Flap = flap, Window = window, WindowZ = wz, WindowLit = i == 7 || i == 12 });
            }
            // The square: a wide carpet shop on the left, the big spice stall on the right (the first thing you see).
            Add(-1, -38.85f, -35.25f, SkSquare, 1.15f, SkKind.Carpet, false);
            Add(1, -38.85f, -35.25f, SkSquare, 1.15f, SkKind.Spice, true);
            // The lane: three bays between each pair of arches.
            var spans = new[]
            {
                (-33.35f, -31.35f), (-30.95f, -28.95f), (-28.55f, -26.65f),
                (-25.35f, -23.35f), (-22.95f, -20.95f), (-20.55f, -18.65f),
                (-17.35f, -15.35f), (-14.95f, -12.95f), (-12.55f, -11.2f),
            };
            var left = new[] { SkKind.Spice, SkKind.Lantern, SkKind.Door, SkKind.Ceramic, SkKind.Slipper, SkKind.Basket, SkKind.Fabric, SkKind.Metal, SkKind.Tea };
            var leftFlap = new[] { true, false, false, true, false, false, false, true, false };
            var right = new[] { SkKind.Leather, SkKind.Metal, SkKind.Alley, SkKind.Lantern, SkKind.Spice, SkKind.Carpet, SkKind.Ceramic, SkKind.Slipper, SkKind.Fountain };
            var rightFlap = new[] { true, false, false, false, true, false, false, true, false };
            for (int i = 0; i < spans.Length; i++)
            {
                Add(-1, spans[i].Item1, spans[i].Item2, SkLane, 1.75f, left[i], leftFlap[i]);
                Add(1, spans[i].Item1, spans[i].Item2, SkLane, 1.75f, right[i], rightFlap[i]);
            }
            return list;
        }

        /// <summary>Paving (top at y = 0, flush with the old lane at z -10.25), worn flagstones, the gutter, thresholds.</summary>
        static void SkFloor(SkBatch k, SkMats m)
        {
            k.Box(m.Paving, new Vector3(0f, -0.1f, (SkEndZ + SkSquareZ) / 2f), new Vector3(SkSquare * 2f, 0.2f, SkSquareZ - SkEndZ));
            k.Box(m.Paving, new Vector3(0f, -0.1f, (SkSquareZ + SkGateZ1) / 2f), new Vector3(SkLane * 2f, 0.2f, SkGateZ1 - SkSquareZ));
            // Uneven paving: old flagstones lifted a centimetre or two and turned a little.
            var rnd = new System.Random(4242);
            for (int i = 0; i < 48; i++)
            {
                float z = SkR(rnd, SkEndZ + 0.4f, SkGateZ0 - 0.3f);
                float half = z < SkSquareZ ? SkSquare - 0.35f : SkLane - 0.3f;
                float x = SkR(rnd, -half, half);
                if (Mathf.Abs(x) < 0.3f) x = Mathf.Sign(x + 0.0001f) * (0.3f + SkR(rnd, 0f, 0.3f)); // the gutter stays clear
                var size = new Vector3(SkR(rnd, 0.35f, 0.8f), SkR(rnd, 0.012f, 0.026f), SkR(rnd, 0.3f, 0.7f));
                k.Box(m.Paving, new Vector3(x, size.y * 0.5f - 0.004f, z), size, Quaternion.Euler(SkR(rnd, -1.2f, 1.2f), SkR(rnd, -8f, 8f), SkR(rnd, -1.2f, 1.2f)), 1.15f);
            }
            // A dark stone gutter down the middle, and a worn threshold under the gate to the shop's alley.
            for (float z = SkEndZ + 0.25f; z < SkGateZ0 - 0.3f; z += 0.62f)
                k.Box(SkPal.StoneDark, new Vector3(0f, 0.004f, z + 0.29f), new Vector3(0.22f, 0.012f, 0.58f), Quaternion.Euler(0f, SkR(rnd, -2f, 2f), 0f));
            k.Box(SkPal.StoneDark, new Vector3(0f, 0.007f, (SkGateZ0 + SkGateZ1) / 2f), new Vector3(3.0f, 0.014f, 0.6f));
        }

        /// <summary>
        /// Walkable paving (Floor layer) only along the path; low invisible rails keep the player out of the goods laid in
        /// front of the stalls (VR teleports into the goods fall through and are refused); one backstop per facade and
        /// the two gate walls. Decoration has no colliders.
        /// </summary>
        static void SkColliders(Transform root)
        {
            void Col(string name, Vector3 c, Vector3 s, int layer)
            {
                var go = new GameObject(name) { layer = layer };
                go.transform.SetParent(root, false);
                go.transform.localPosition = c;
                go.AddComponent<BoxCollider>().size = s;
                if (layer == Layers.Floor) go.AddComponent<Surface>().Kind = "stone";
            }
            float sqZ = (SkEndZ + SkSquareZ) / 2f, sqL = SkSquareZ - SkEndZ;
            float railZ = (SkSquareZ + SkGateZ0) / 2f, railL = SkGateZ0 - SkSquareZ;
            Col("SoukWalkSquare", new Vector3(0f, -0.1f, sqZ), new Vector3(SkRailSquare * 2f, 0.2f, sqL), Layers.Floor);
            Col("SoukWalkLane", new Vector3(0f, -0.1f, (SkSquareZ + SkGateZ1) / 2f), new Vector3(SkRailLane * 2f, 0.2f, SkGateZ1 - SkSquareZ), Layers.Floor);
            foreach (float s in new[] { -1f, 1f })
            {
                Col("SoukRail", new Vector3(s * (SkRailLane + 0.05f), 0.45f, railZ), new Vector3(0.1f, 0.9f, railL), Layers.Default);
                Col("SoukRail", new Vector3(s * (SkRailSquare + 0.05f), 0.45f, sqZ), new Vector3(0.1f, 0.9f, sqL), Layers.Default);
                Col("SoukRail", new Vector3(s * (SkRailLane + SkRailSquare + 0.1f) / 2f, 0.45f, SkSquareZ), new Vector3(SkRailSquare - SkRailLane + 0.1f, 0.9f, 0.1f), Layers.Default);
                Col("SoukFacade", new Vector3(s * (SkLane + SkOuter) / 2f, SkTop / 2f, railZ), new Vector3(SkOuter - SkLane, SkTop, railL), Layers.Default);
                Col("SoukFacade", new Vector3(s * (SkSquare + SkOuter) / 2f, SkTop / 2f, sqZ), new Vector3(SkOuter - SkSquare, SkTop, sqL), Layers.Default);
                Col("SoukGateWall", new Vector3(s * (1.5f + SkOuter) / 2f, SkTop / 2f, (SkGateZ0 + SkGateZ1) / 2f), new Vector3(SkOuter - 1.5f, SkTop, SkGateZ1 - SkGateZ0), Layers.Default);
            }
            Col("SoukEndWall", new Vector3(0f, SkTop / 2f, SkEndZ - 0.15f), new Vector3(SkOuter * 2f, SkTop, 0.3f), Layers.Default);
        }

        /// <summary>
        /// Both facades as solid masonry: piers between the bays (full depth), the wall above every opening (its underside
        /// is the cell's ceiling), the cells' back walls; zellige dados on the piers, small grilled windows upstairs (two
        /// lit), a cornice. The square is lime-washed ochre, the lane terracotta like the old lane.
        /// </summary>
        static void SkFacades(SkBatch k, SkMats m, List<SkBay> bays)
        {
            foreach (int s in new[] { -1, 1 })
            {
                var side = bays.FindAll(b => b.Side == s);
                side.Sort((p, q) => p.Z0.CompareTo(q.Z0));
                // Solid piers: the gaps between openings, split where the square meets the lane.
                var solids = new List<Vector2>();
                float z = SkEndZ;
                foreach (var b in side) { if (b.Z0 > z + 0.001f) solids.Add(new Vector2(z, b.Z0)); z = b.Z1; }
                solids.Add(new Vector2(z, SkGateZ0));
                foreach (var sd in solids)
                {
                    var parts = sd.x < SkSquareZ && sd.y > SkSquareZ ? new[] { new Vector2(sd.x, SkSquareZ), new Vector2(SkSquareZ, sd.y) } : new[] { sd };
                    foreach (var p in parts)
                    {
                        float half = p.y <= SkSquareZ + 0.001f ? SkSquare : SkLane;
                        var mat = half > SkLane ? m.Ochre : m.Plaster;
                        float len = p.y - p.x, zc = (p.x + p.y) / 2f;
                        k.Box(mat, new Vector3(s * (half + SkOuter) / 2f, SkTop / 2f, zc), new Vector3(SkOuter - half, SkTop, len), 1.4f);
                        k.Box(m.Zellige, new Vector3(s * (half - 0.012f), 0.45f, zc), new Vector3(0.024f, 0.9f, len - 0.04f), 0.6f);
                        k.Box(SkPal.Stucco, new Vector3(s * (half - 0.02f), 0.92f, zc), new Vector3(0.04f, 0.05f, len - 0.02f));
                    }
                }
                foreach (var b in side)
                {
                    var mat = b.Half > SkLane ? m.Ochre : m.Plaster;
                    float zc = b.Zc, w = b.W;
                    // The wall above the opening, and the cell's back wall (a door bay is filled solid to the lintel).
                    k.Box(mat, new Vector3(s * (b.Half + SkOuter) / 2f, (SkLintel + SkTop) / 2f, zc), new Vector3(SkOuter - b.Half, SkTop - SkLintel, w), 1.4f);
                    float back = b.Kind == SkKind.Door ? b.Half : b.Half + b.Depth;
                    k.Box(mat, new Vector3(s * (back + SkOuter) / 2f, SkLintel / 2f, zc), new Vector3(SkOuter - back, SkLintel, w), 1.4f);
                    // A small window upstairs behind a turned-wood grille; two of them lit (someone is home).
                    if (b.Window)
                    {
                        float ww = 0.62f, wh = 0.84f, wy = 3.65f, wz = b.WindowZ, x = s * b.Half;
                        bool lit = b.WindowLit;
                        k.Box(lit ? SkPal.GlowWindow : SkPal.Void, new Vector3(x - s * 0.004f, wy, wz), new Vector3(0.01f, wh, ww));
                        for (int i = 1; i < 5; i++) k.Box(SkPal.WoodDark, new Vector3(x - s * 0.025f, wy, wz - ww / 2f + i * ww / 5f), new Vector3(0.025f, wh, 0.022f));
                        for (int i = 1; i < 6; i++) k.Box(SkPal.WoodDark, new Vector3(x - s * 0.03f, wy - wh / 2f + i * wh / 6f, wz), new Vector3(0.022f, 0.022f, ww));
                        k.Box(m.Cedar, new Vector3(x - s * 0.04f, wy - wh / 2f - 0.04f, wz), new Vector3(0.1f, 0.06f, ww + 0.2f));
                        k.Box(m.Cedar, new Vector3(x - s * 0.05f, wy + wh / 2f + 0.05f, wz), new Vector3(0.12f, 0.08f, ww + 0.24f));
                        k.Box(m.Cedar, new Vector3(x - s * 0.02f, wy, wz - ww / 2f - 0.04f), new Vector3(0.05f, wh + 0.1f, 0.06f));
                        k.Box(m.Cedar, new Vector3(x - s * 0.02f, wy, wz + ww / 2f + 0.04f), new Vector3(0.05f, wh + 0.1f, 0.06f));
                    }
                }
                // A plaster cornice along the top of each facade (seen from the open bays of the roof).
                k.Box(SkPal.Stucco, new Vector3(s * (SkSquare - 0.06f), SkTop - 0.12f, (SkEndZ + SkSquareZ) / 2f), new Vector3(0.14f, 0.12f, SkSquareZ - SkEndZ));
                k.Box(SkPal.Stucco, new Vector3(s * (SkLane - 0.06f), SkTop - 0.12f, (SkSquareZ + SkGateZ0) / 2f), new Vector3(0.14f, 0.12f, SkGateZ0 - SkSquareZ));
            }
        }

        /// <summary>The static part of one bay: an open cell (deck, lime-washed lining, plank ceiling, lintel, hanging
        /// rail and shutters folded open), or a carved door, a covered alley mouth, a wall fountain.</summary>
        static void SkCell(SkBatch k, SkMats m, SkBay b)
        {
            var q = Quaternion.Euler(0f, b.Side > 0 ? 90f : -90f, 0f);
            var o = new Vector3(b.Side * b.Half, 0f, b.Zc);
            float w = b.W, D = b.Depth, deck = b.Deck;
            var rnd = new System.Random((int)(b.Zc * 100f) + b.Side * 7);
            Vector3 P(float u, float y, float d) => o + q * new Vector3(u, y, d);
            void Bx(Material mat, float u, float y, float d, float su, float sy, float sd, float uv = 1f) => k.Box(mat, P(u, y, d), new Vector3(su, sy, sd), q, uv);
            void Bp(int col, float u, float y, float d, float su, float sy, float sd) => k.Box(col, P(u, y, d), new Vector3(su, sy, sd), q);
            void Rod(int col, Vector3 a, Vector3 c, float t)
            {
                var pa = P(a.x, a.y, a.z); var pc = P(c.x, c.y, c.z);
                k.Box(col, (pa + pc) / 2f, new Vector3(t, t, Vector3.Distance(pa, pc)), Quaternion.LookRotation(pc - pa));
            }

            if (b.Kind == SkKind.Door) { SkDoor(k, m, P, q, 0f, 1.2f, 2.3f, true); return; }

            // Lime-washed lining and a plank ceiling make each cell read as a small room.
            Bx(m.Ochre, 0f, (deck + SkLintel) / 2f, D - 0.01f, w, SkLintel - deck, 0.02f, 1.6f);
            foreach (float e in new[] { -1f, 1f }) Bx(m.Ochre, e * (w / 2f - 0.01f), (deck + SkLintel) / 2f, D / 2f, 0.02f, SkLintel - deck, D, 1.6f);
            Bx(m.Timber, 0f, SkLintel - 0.025f, D / 2f, w, 0.05f, D);
            for (float d = 0.35f; d < D - 0.1f; d += 0.55f) Bx(m.Timber, 0f, SkLintel - 0.08f, d, w, 0.07f, 0.09f);
            // The lintel beam, proud of the facade.
            Bx(m.Cedar, 0f, SkLintel + 0.09f, -0.03f, w + 0.36f, 0.18f, 0.2f);

            if (b.Kind == SkKind.Alley || b.Kind == SkKind.Fountain)
            {
                k.Box(m.Paving, P(0f, -0.1f, D / 2f), new Vector3(w, 0.2f, D), q, 1f);
                if (b.Kind == SkKind.Alley)
                {
                    // A covered passage to a house door; a little lamp burns under the ceiling.
                    SkDoor(k, m, (u, y, d) => P(u, y, d + D - 0.02f), q, 0f, Mathf.Min(1.1f, w - 0.5f), 1.95f, false);
                    Rod(SkPal.Iron, new Vector3(0f, SkLintel - 0.05f, D * 0.5f), new Vector3(0f, SkLintel - 0.45f, D * 0.5f), 0.012f);
                    k.Of(SkPal.Iron).AddLathe(P(0f, SkLintel - 0.6f, D * 0.5f), new[] { new Vector2(0.0f, 0f), new Vector2(0.07f, 0.02f), new Vector2(0.07f, 0.03f), new Vector2(0.0f, 0.03f) }, 8);
                    k.Of(SkPal.GlowHoney).AddCylinder(P(0f, SkLintel - 0.58f, D * 0.5f), 0.055f, 0.11f, 8);
                    k.Of(SkPal.Iron).AddFrustum(P(0f, SkLintel - 0.47f, D * 0.5f), 0.075f, 0.01f, 0.06f, 8);
                }
                else
                {
                    // A wall fountain: zellige panel, stone basin, an iron spout, a carved stucco hood.
                    Bx(m.Zellige, 0f, 1.15f, D - 0.03f, w - 0.1f, 2.3f, 0.02f, 0.6f);
                    Bx(m.Zellige, 0f, 0.27f, D - 0.32f, w - 0.1f, 0.54f, 0.56f, 0.6f);
                    foreach (float e in new[] { -1f, 1f })
                    {
                        Bp(SkPal.StoneDark, e * (w / 2f - 0.06f), 0.565f, D - 0.32f, 0.1f, 0.05f, 0.62f);
                        Bp(SkPal.StoneDark, 0f, 0.565f, D - 0.32f + e * 0.28f, w - 0.04f, 0.05f, 0.08f);
                    }
                    Bp(SkPal.Water, 0f, 0.52f, D - 0.32f, w - 0.2f, 0.02f, 0.5f);
                    Rod(SkPal.Iron, new Vector3(0f, 1.0f, D - 0.03f), new Vector3(0f, 0.98f, D - 0.22f), 0.03f);
                    Rod(SkPal.Iron, new Vector3(0f, 0.98f, D - 0.22f), new Vector3(0f, 0.9f, D - 0.24f), 0.025f);
                    Bp(SkPal.Stucco, 0f, 1.95f, D - 0.05f, w - 0.16f, 0.1f, 0.06f);
                    Bp(SkPal.Madder, 0f, 1.88f, D - 0.06f, w - 0.3f, 0.03f, 0.05f);
                    float dz = 0.3f;
                    k.Box(SkPal.TileGreen, P(0f, SkLintel + 0.32f, -dz), new Vector3(w + 0.2f, 0.05f, 0.7f), q * Quaternion.Euler(-24f, 0f, 0f));
                    for (float u = -w / 2f; u <= w / 2f + 0.01f; u += w / 4f) Bp(SkPal.WoodDark, u, SkLintel + 0.2f, -0.15f, 0.07f, 0.09f, 0.3f);
                }
                return;
            }

            // Deck: plaster box with plank top; a zellige or plank front.
            Bx(m.Plaster, 0f, (deck - 0.2f) / 2f, (0.06f + D) / 2f, w, deck + 0.2f, D - 0.06f, 1.4f);
            Bx(m.Cedar, 0f, deck + 0.01f, (0.06f + D) / 2f, w, 0.02f, D - 0.06f);
            if (deck > 0.2f) Bx(m.Zellige, 0f, deck / 2f, 0.05f, w, deck, 0.02f, 0.6f);
            else Bx(m.Cedar, 0f, deck / 2f, 0.05f, w, deck, 0.03f);
            // The iron rail under the lintel that the goods hang from.
            Bp(SkPal.Iron, 0f, SkLintel - 0.16f, 0.12f, w - 0.06f, 0.025f, 0.025f);

            int[] paints = { SkPal.Teal, SkPal.GlazeGreen, SkPal.Cobalt, SkPal.Madder, SkPal.Ochre };
            int paint = paints[rnd.Next(paints.Length)];
            if (b.Flap)
            {
                // The upper shutter is propped up as an awning on two iron stays.
                var tilt = q * Quaternion.Euler(22f, 0f, 0f);
                var dir = new Vector3(0f, Mathf.Sin(22f * Mathf.Deg2Rad), -Mathf.Cos(22f * Mathf.Deg2Rad));
                var hinge = new Vector3(0f, SkLintel + 0.18f, -0.13f);
                var c = hinge + dir * 0.46f;
                k.Box(m.Cedar, P(c.x, c.y, c.z), new Vector3(w + 0.1f, 0.035f, 0.92f), tilt);
                for (int i = 0; i < 3; i++)
                {
                    var bc = hinge + dir * (0.12f + i * 0.34f) + new Vector3(0f, 0.03f, 0f);
                    k.Box(m.Cedar, P(bc.x, bc.y, bc.z), new Vector3(w + 0.1f, 0.03f, 0.06f), tilt);
                }
                k.Box(paint, P(c.x, c.y - 0.02f, c.z), new Vector3(w - 0.1f, 0.01f, 0.8f), tilt);
                var tip = hinge + dir * 0.9f;
                foreach (float e in new[] { -1f, 1f })
                    Rod(SkPal.Iron, new Vector3(e * (w / 2f + 0.02f), tip.y - 0.02f, tip.z), new Vector3(e * (w / 2f + 0.08f), 1.95f, -0.03f), 0.02f);
            }
            else
            {
                // Folding leaves pushed back against the cell's side walls (two per side for a wide opening).
                int leaves = w / 2f > D - 0.25f ? 2 : 1;
                float L = Mathf.Min((w / 2f) / leaves, D - 0.2f), h = SkLintel - deck - 0.1f, yc = deck + 0.04f + h / 2f;
                foreach (float e in new[] { -1f, 1f })
                    for (int l = 0; l < leaves; l++)
                    {
                        float u = e * (w / 2f - 0.045f - l * 0.06f), dc = 0.1f + L / 2f + l * 0.03f;
                        Bx(m.Cedar, u, yc, dc, 0.05f, h, L);
                        foreach (float py in new[] { yc - h * 0.24f, yc + h * 0.22f })
                            Bp(paint, u - e * 0.03f, py, dc, 0.012f, h * 0.38f, L - 0.14f);
                        foreach (float py in new[] { yc - h * 0.38f, yc + h * 0.38f })
                            Bp(SkPal.Iron, u - e * 0.032f, py, dc - L * 0.3f, 0.012f, 0.03f, L * 0.4f);
                    }
            }
        }

        /// <summary>A studded cedar double door in a zellige surround under a small green-tiled hood, a lamp beside it.
        /// <paramref name="P"/> maps (along, up, depth into the wall) to cell space; the door face is at depth 0.</summary>
        static void SkDoor(SkBatch k, SkMats m, System.Func<float, float, float, Vector3> P, Quaternion q, float u0, float dw, float dh, bool lamp)
        {
            void Bx(Material mat, float u, float y, float d, float su, float sy, float sd, float uv = 1f) => k.Box(mat, P(u, y, d), new Vector3(su, sy, sd), q, uv);
            void Bp(int col, float u, float y, float d, float su, float sy, float sd) => k.Box(col, P(u, y, d), new Vector3(su, sy, sd), q);
            Bx(m.Zellige, u0, (dh + 0.45f) / 2f, -0.01f, dw + 0.5f, dh + 0.45f, 0.02f, 0.6f);
            Bp(SkPal.Stucco, u0, dh + 0.08f, -0.03f, dw + 0.3f, 0.16f, 0.05f);
            foreach (float e in new[] { -1f, 1f }) Bp(SkPal.Stucco, u0 + e * (dw / 2f + 0.07f), dh / 2f, -0.03f, 0.14f, dh, 0.05f);
            Bx(m.Cedar, u0, dh / 2f, -0.035f, dw, dh, 0.05f);
            Bp(SkPal.Ink, u0, dh / 2f, -0.062f, 0.012f, dh - 0.04f, 0.006f);
            // Raised panels, iron studs, ring knockers.
            foreach (float e in new[] { -1f, 1f })
                foreach (float py in new[] { dh * 0.28f, dh * 0.7f })
                    Bx(m.Cedar, u0 + e * dw / 4f, py, -0.07f, dw / 2f - 0.16f, dh * 0.3f, 0.03f);
            for (float y = 0.2f; y < dh - 0.1f; y += 0.3f)
                for (float u = -dw / 2f + 0.07f; u < dw / 2f - 0.05f; u += dw / 6f)
                    Bp(SkPal.Iron, u0 + u, y, -0.066f, 0.03f, 0.03f, 0.02f);
            foreach (float e in new[] { -1f, 1f })
                k.Of(SkPal.Iron).AddTorus(P(u0 + e * 0.16f, dh * 0.5f, -0.09f), 0.055f, 0.009f, 12, 4, q * Quaternion.Euler(90f, 0f, 0f));
            // Hood: green glazed tiles on carved corbels.
            k.Box(SkPal.TileGreen, P(u0, dh + 0.42f, -0.24f), new Vector3(dw + 0.7f, 0.05f, 0.56f), q * Quaternion.Euler(-26f, 0f, 0f));
            for (float u = -dw / 2f - 0.25f; u <= dw / 2f + 0.26f; u += (dw + 0.5f) / 6f)
                k.Box(SkPal.TileGreen, P(u0 + u, dh + 0.45f, -0.25f), new Vector3(0.05f, 0.04f, 0.56f), q * Quaternion.Euler(-26f, 0f, 0f));
            foreach (float e in new[] { -1f, 1f }) Bp(SkPal.WoodDark, u0 + e * (dw / 2f + 0.15f), dh + 0.25f, -0.15f, 0.08f, 0.12f, 0.3f);
            if (lamp) SkWallLamp(k, P(u0 + dw / 2f + 0.42f, dh - 0.05f, 0f), P(u0 + dw / 2f + 0.42f, dh - 0.05f, -1f) - P(u0 + dw / 2f + 0.42f, dh - 0.05f, 0f));
        }

        /// <summary>An iron wall bracket with a small glowing lantern hanging from it (no light of its own).</summary>
        static void SkWallLamp(SkBatch k, Vector3 wall, Vector3 outward)
        {
            outward.y = 0f; outward.Normalize();
            var tip = wall + outward * 0.26f;
            k.Box(SkPal.Iron, wall + outward * 0.13f, new Vector3(0.022f, 0.022f, 0.26f), Quaternion.LookRotation(outward));
            k.Box(SkPal.Iron, wall + outward * 0.08f - Vector3.up * 0.09f, new Vector3(0.016f, 0.016f, 0.24f), Quaternion.LookRotation(outward + Vector3.up * 1.1f));
            var top = tip - Vector3.up * 0.06f;
            k.Box(SkPal.Iron, tip - Vector3.up * 0.03f, new Vector3(0.012f, 0.06f, 0.012f));
            k.Of(SkPal.Iron).AddFrustum(top - Vector3.up * 0.07f, 0.075f, 0.015f, 0.07f, 8);
            k.Of(SkPal.GlowAmber).AddCylinder(top - Vector3.up * 0.3f, 0.05f, 0.23f, 8);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2f + Mathf.PI / 4f;
                k.Box(SkPal.Iron, top - Vector3.up * 0.185f + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.056f, new Vector3(0.013f, 0.23f, 0.013f));
            }
            k.Of(SkPal.Iron).AddCylinder(top - Vector3.up * 0.33f, 0.062f, 0.035f, 8);
            k.Of(SkPal.Iron).AddFrustum(top - Vector3.up * 0.39f, 0.01f, 0.05f, 0.06f, 8);
        }

        /// <summary>The souk gate behind the start, shut for the night: a horseshoe arch with a carved stucco ring and
        /// red outline, zellige spandrels in an alfiz frame, studded cedar doors, a green-tiled hood, two wall lamps.</summary>
        static void SkEndGate(SkBatch k, SkMats m)
        {
            float z = SkEndZ;
            k.Box(m.Ochre, new Vector3(0f, SkTop / 2f, z - 0.15f), new Vector3(SkOuter * 2f, SkTop, 0.3f), 1.4f);
            const float a = 1.3f, r = 1.48f, hc = 2.55f;
            float beta = Mathf.Acos(a / r), ys = SkSpring(a, r, hc), apex = hc + r;
            // The doors fill the horseshoe; a split, iron straps, studs, ring knockers and a wicket door.
            SkHorseshoeFill(k.Of(m.Cedar), a, r, hc, z + 0.05f, Vector3.forward, 1f);
            k.Box(SkPal.Ink, new Vector3(0f, apex / 2f, z + 0.055f), new Vector3(0.014f, apex - 0.06f, 0.01f));
            foreach (float y in new[] { 0.55f, 1.75f })
                foreach (float e in new[] { -1f, 1f })
                    k.Box(SkPal.Iron, new Vector3(e * (a / 2f + 0.02f), y, z + 0.06f), new Vector3(a - 0.1f, 0.05f, 0.016f));
            for (float y = 0.28f; y < apex - 0.15f; y += 0.26f)
                for (float x = -a + 0.13f; x < a - 0.1f; x += 0.22f)
                {
                    bool inside = y < ys ? Mathf.Abs(x) < a - 0.1f : x * x + (y - hc) * (y - hc) < (r - 0.14f) * (r - 0.14f);
                    if (inside && Mathf.Abs(x) > 0.05f) k.Box(SkPal.Iron, new Vector3(x, y, z + 0.066f), new Vector3(0.034f, 0.034f, 0.024f));
                }
            foreach (float e in new[] { -1f, 1f })
                k.Of(SkPal.Iron).AddTorus(new Vector3(e * 0.22f, 1.32f, z + 0.095f), 0.06f, 0.01f, 12, 4, Quaternion.Euler(90f, 0f, 0f));
            foreach (var p in new[] { new Vector4(0.6f, 0.06f, 0.03f, 1.5f), new Vector4(0.6f, 1.53f, 0.03f, 0.03f) })
                k.Box(SkPal.Ink, new Vector3(0.6f, p.y + (p.w > 1f ? p.w / 2f - 0.03f : 0f), z + 0.058f), p.w > 1f ? new Vector3(0.03f, p.w, 0.008f) : new Vector3(0.62f, 0.03f, 0.008f));
            k.Box(SkPal.Ink, new Vector3(0.91f, 0.81f, z + 0.058f), new Vector3(0.03f, 1.5f, 0.008f));
            // Frame: carved stucco ring and jambs, red outline and imposts, zellige spandrels, the alfiz.
            SkArcBand(k.Of(SkPal.Stucco), r, r + 0.2f, hc, -beta, Mathf.PI + beta, z, 0.07f);
            SkArcBand(k.Of(SkPal.Madder), r + 0.2f, r + 0.25f, hc, -beta, Mathf.PI + beta, z, 0.075f);
            float a0 = Mathf.Asin((ys - hc) / (r + 0.25f));
            SkFan(k.Of(m.Zellige), r + 0.25f, hc, 2.05f, ys, 4.5f, a0, Mathf.PI - a0, z + 0.012f, Vector3.forward, 0.6f);
            foreach (float e in new[] { -1f, 1f })
            {
                k.Box(SkPal.Stucco, new Vector3(e * (a + 0.1f), ys / 2f, z + 0.035f), new Vector3(0.2f, ys, 0.07f));
                k.Box(SkPal.Madder, new Vector3(e * (a + 0.12f), ys, z + 0.045f), new Vector3(0.32f, 0.08f, 0.09f));
                k.Box(SkPal.Madder, new Vector3(e * 2.1f, (ys - 0.15f + 4.6f) / 2f, z + 0.04f), new Vector3(0.1f, 4.6f - ys + 0.15f, 0.08f));
                k.Box(m.Zellige, new Vector3(e * (a + 0.2f + SkSquare) / 2f, 0.5f, z + 0.012f), new Vector3(SkSquare - a - 0.2f, 1.0f, 0.024f), 0.6f);
                k.Box(SkPal.Stucco, new Vector3(e * (a + 0.2f + SkSquare) / 2f, 1.02f, z + 0.025f), new Vector3(SkSquare - a - 0.2f, 0.05f, 0.05f));
                SkWallLamp(k, new Vector3(e * 2.6f, 2.55f, z), Vector3.forward);
            }
            k.Box(SkPal.Madder, new Vector3(0f, 4.55f, z + 0.04f), new Vector3(4.3f, 0.1f, 0.08f));
            // A carved frieze and a hood of green glazed tiles on corbels.
            k.Box(SkPal.Stucco, new Vector3(0f, 4.76f, z + 0.03f), new Vector3(4.4f, 0.2f, 0.06f));
            for (int i = 0; i < 22; i++)
                k.Box(SkPal.WoodDark, new Vector3(-2.1f + i * 0.2f, 4.76f, z + 0.062f), new Vector3(0.08f, 0.08f, 0.01f), Quaternion.Euler(0f, 0f, 45f));
            k.Box(SkPal.TileGreen, new Vector3(0f, 5.08f, z + 0.33f), new Vector3(4.7f, 0.05f, 0.72f), Quaternion.Euler(24f, 0f, 0f));
            for (int i = 0; i <= 16; i++)
                k.Box(SkPal.TileGreen, new Vector3(-2.3f + i * 4.6f / 16f, 5.11f, z + 0.33f), new Vector3(0.06f, 0.05f, 0.72f), Quaternion.Euler(24f, 0f, 0f));
            for (int i = 0; i < 6; i++)
                k.Box(SkPal.WoodDark, new Vector3(-2.0f + i * 0.8f, 4.93f, z + 0.17f), new Vector3(0.1f, 0.14f, 0.34f));
            k.Box(SkPal.StoneDark, new Vector3(0f, 0.05f, z + 0.2f), new Vector3(2.9f, 0.1f, 0.4f));
        }

        /// <summary>The horseshoe gate into the shop's alley, where the souk meets the old lane: terracotta like the lane
        /// walls, the opening 3 m wide, the old lane's reed roof and the lit shop sign seen through it.</summary>
        static void SkLaneGate(SkBatch k, SkMats m)
        {
            const float a = 1.5f, r = 1.7f, hc = 2.6f;
            float beta = Mathf.Acos(a / r), ys = SkSpring(a, r, hc), z0 = SkGateZ0, z1 = SkGateZ1;
            SkHorseshoeWall(k.Of(m.Plaster), SkLane, SkTop, a, r, hc, z0, z1, 1.4f);
            foreach (float s in new[] { -1f, 1f })
                k.Box(m.Plaster, new Vector3(s * (SkLane + SkOuter) / 2f, SkTop / 2f, (z0 + z1) / 2f), new Vector3(SkOuter - SkLane, SkTop, z1 - z0), 1.4f);
            // Souk face: carved ring with a red outline, zellige spandrels in an alfiz, zellige on the piers.
            SkArcBand(k.Of(SkPal.Stucco), r, r + 0.15f, hc, -beta, Mathf.PI + beta, z0, -0.04f);
            SkArcBand(k.Of(SkPal.Madder), r + 0.15f, r + 0.19f, hc, -beta, Mathf.PI + beta, z0, -0.045f);
            float a0 = Mathf.Asin((ys - hc) / (r + 0.19f));
            SkFan(k.Of(m.Zellige), r + 0.19f, hc, 1.96f, ys, 4.56f, a0, Mathf.PI - a0, z0 - 0.012f, Vector3.back, 0.6f);
            k.Box(SkPal.Madder, new Vector3(0f, 4.61f, z0 - 0.035f), new Vector3(4.14f, 0.1f, 0.07f));
            foreach (float s in new[] { -1f, 1f })
            {
                k.Box(SkPal.Madder, new Vector3(s * 2.02f, (ys - 0.15f + 4.66f) / 2f, z0 - 0.035f), new Vector3(0.1f, 4.66f - ys + 0.15f, 0.07f));
                k.Box(m.Zellige, new Vector3(s * (a + SkLane) / 2f, 0.45f, z0 - 0.012f), new Vector3(SkLane - a, 0.9f, 0.024f), 0.6f);
                k.Box(SkPal.Stucco, new Vector3(s * (a + 0.08f), ys / 2f, z0 - 0.02f), new Vector3(0.16f, ys, 0.04f));
                k.Box(SkPal.Madder, new Vector3(s * (a + 0.1f), ys, z0 - 0.04f), new Vector3(0.28f, 0.08f, 0.08f));
            }
            k.Box(SkPal.Stucco, new Vector3(0f, 4.86f, z0 - 0.03f), new Vector3(4.4f, 0.16f, 0.06f));
            // Lane face: the same ring, plainer.
            SkArcBand(k.Of(SkPal.Stucco), r, r + 0.15f, hc, -beta, Mathf.PI + beta, z1, 0.04f);
        }

        /// <summary>
        /// Roofs: the square and the middle bay are open to the evening sky (timber beams carry canopies and lanterns);
        /// the first bay is a reed mat on beams like the old lane, with a few torn gaps; the last a slatted lattice.
        /// </summary>
        static void SkRoofs(SkBatch k, SkMats m)
        {
            var rnd = new System.Random(919);
            foreach (float z in new[] { -38.5f, -36.0f })
                k.Box(m.Timber, new Vector3(0f, 5.0f, z), new Vector3(SkSquare * 2f + 0.3f, 0.16f, 0.16f));
            // Bay A: reeds on beams, edge timbers, a few strips missing and one torn patch.
            float a0 = -33.7f, a1 = -26.3f;
            for (float z = a0 + 0.35f; z < a1 - 0.2f; z += 1.05f)
                k.Box(m.Timber, new Vector3(0f, SkBeamY, z), new Vector3(SkLane * 2f + 0.3f, 0.16f, 0.16f));
            foreach (float s in new[] { -1f, 1f })
                k.Box(m.Timber, new Vector3(s * (SkLane - 0.06f), SkBeamY + 0.05f, (a0 + a1) / 2f), new Vector3(0.1f, 0.2f, a1 - a0));
            for (int i = 0; i < 26; i++)
            {
                if (rnd.NextDouble() < 0.1) continue;
                float x = -2.12f + i * 0.17f;
                var rot = Quaternion.Euler(0f, 0f, (i % 3 - 1) * 4f);
                if (i >= 9 && i <= 14)
                {
                    // Torn: the mat parts over z -29.6..-28.4 and the sky shows through.
                    float g0 = -29.6f + SkR(rnd, -0.25f, 0.25f), g1 = -28.4f + SkR(rnd, -0.25f, 0.25f);
                    k.Box(m.Reed, new Vector3(x, SkBeamY + 0.12f, (a0 + g0) / 2f), new Vector3(0.11f, 0.05f, g0 - a0), rot);
                    k.Box(m.Reed, new Vector3(x, SkBeamY + 0.12f, (g1 + a1) / 2f), new Vector3(0.11f, 0.05f, a1 - g1), rot);
                }
                else k.Box(m.Reed, new Vector3(x, SkBeamY + 0.12f, (a0 + a1) / 2f), new Vector3(0.11f, 0.05f, a1 - a0 - 0.05f), rot);
            }
            // Bay B: open, three beams.
            foreach (float z in new[] { -25.1f, -22.6f, -20.1f })
                k.Box(m.Timber, new Vector3(0f, SkBeamY, z), new Vector3(SkLane * 2f + 0.3f, 0.16f, 0.16f));
            // Bay C: a lattice of reed slats on joists and beams (stripes of evening light).
            float c0 = -17.7f, c1 = SkGateZ0;
            for (float z = c0 + 0.3f; z < c1 - 0.2f; z += 1.35f)
                k.Box(m.Timber, new Vector3(0f, SkBeamY, z), new Vector3(SkLane * 2f + 0.3f, 0.16f, 0.16f));
            foreach (float x in new[] { -2.17f, -1.1f, 0f, 1.1f, 2.17f })
                k.Box(m.Timber, new Vector3(x, SkBeamY + 0.12f, (c0 + c1) / 2f), new Vector3(0.08f, 0.08f, c1 - c0));
            for (float z = c0 + 0.1f; z < c1 - 0.05f; z += 0.2f)
                k.Box(m.Reed, new Vector3(0f, SkBeamY + 0.175f, z), new Vector3(SkLane * 2f - 0.06f, 0.025f, 0.09f), Quaternion.Euler(0f, SkR(rnd, -1.5f, 1.5f), 0f));
        }

        // ------------------------------------------------------------------ arches
        /// <summary>A horseshoe arch across the lane, rising as one piece: ochre plaster, a carved stucco ring with a red
        /// outline, zellige spandrels in an alfiz, zellige on the piers and a carved frieze, on both faces.</summary>
        void SkArch(Transform g, SkMats m, float z, int index)
        {
            var root = SkGroup(g, "SoukArch" + index, new Vector3(0f, 0f, z));
            var k = new SkBatch();
            const float a = 1.65f, r = 1.85f, hc = 2.75f, d = 0.6f;
            float beta = Mathf.Acos(a / r), ys = SkSpring(a, r, hc);
            SkHorseshoeWall(k.Of(m.Ochre), SkLane, SkTop, a, r, hc, -d / 2f, d / 2f, 1.4f);
            float a0 = Mathf.Asin((ys - hc) / (r + 0.18f));
            foreach (float f in new[] { -1f, 1f })
            {
                float zf = f * d / 2f;
                SkArcBand(k.Of(SkPal.Stucco), r, r + 0.15f, hc, -beta, Mathf.PI + beta, zf, f * 0.035f);
                SkArcBand(k.Of(SkPal.Madder), r + 0.15f, r + 0.18f, hc, -beta, Mathf.PI + beta, zf, f * 0.04f);
                SkFan(k.Of(m.Zellige), r + 0.18f, hc, 2.07f, ys, 4.86f, a0, Mathf.PI - a0, zf + f * 0.012f, new Vector3(0f, 0f, f), 0.6f);
                k.Box(SkPal.Madder, new Vector3(0f, 4.91f, zf + f * 0.03f), new Vector3(4.34f, 0.1f, 0.06f));
                foreach (float s in new[] { -1f, 1f })
                {
                    k.Box(SkPal.Madder, new Vector3(s * 2.12f, (ys - 0.15f + 4.96f) / 2f, zf + f * 0.03f), new Vector3(0.1f, 4.96f - ys + 0.15f, 0.06f));
                    k.Box(m.Zellige, new Vector3(s * (a + SkLane) / 2f, 0.45f, zf + f * 0.012f), new Vector3(SkLane - a, 0.9f, 0.024f), 0.6f);
                    k.Box(SkPal.Stucco, new Vector3(s * (a + SkLane) / 2f, 0.93f, zf + f * 0.02f), new Vector3(SkLane - a, 0.05f, 0.04f));
                    k.Box(SkPal.Stucco, new Vector3(s * (a + 0.08f), (0.96f + ys) / 2f, zf + f * 0.02f), new Vector3(0.16f, ys - 0.96f, 0.04f));
                    k.Box(SkPal.Madder, new Vector3(s * (a + 0.1f), ys, zf + f * 0.04f), new Vector3(0.28f, 0.08f, 0.08f));
                }
                k.Box(SkPal.Stucco, new Vector3(0f, 5.15f, zf + f * 0.03f), new Vector3(4.44f, 0.2f, 0.06f));
                for (int i = 0; i < 21; i++)
                    k.Box(SkPal.WoodDark, new Vector3(-2.0f + i * 0.2f, 5.15f, zf + f * 0.062f), new Vector3(0.08f, 0.08f, 0.01f), Quaternion.Euler(0f, 0f, 45f));
            }
            k.Bake(root, "SoukArch");
            SkReveal(root, 10.5f, 0f);
        }

        // ------------------------------------------------------------------ lanterns
        static void SkRod(MeshBuilder b, Vector3 a, Vector3 c, float t)
        {
            var d = c - a;
            if (d.sqrMagnitude < 1e-8f) return;
            b.AddBox((a + c) * 0.5f, new Vector3(t, t, d.magnitude), Quaternion.LookRotation(d.normalized, Mathf.Abs(d.normalized.y) > 0.98f ? Vector3.forward : Vector3.up));
        }

        static float SkProfileR(Vector2[] prof, float y)
        {
            for (int i = 1; i < prof.Length; i++)
                if (y <= prof[i].y && prof[i].y > prof[i - 1].y) return Mathf.Lerp(prof[i - 1].x, prof[i].x, Mathf.InverseLerp(prof[i - 1].y, prof[i].y, y));
            return prof[prof.Length - 1].x;
        }

        /// <summary>
        /// A pierced brass lantern hanging from <paramref name="top"/>: a bulb-shaped metal body whose holes glow (small
        /// emissive tiles set flush on the facets in staggered rows, so the light is always broken up by metal), a cap,
        /// a hanging ring and a drop finial.
        /// </summary>
        static void SkPierced(SkBatch k, SkMats m, Vector3 top, float r, float h, int glow, bool tall = false)
        {
            float y0 = top.y - h;
            var prof = tall
                ? new[] { new Vector2(0.01f, 0f), new Vector2(r * 0.8f, h * 0.04f), new Vector2(r, h * 0.1f), new Vector2(r, h * 0.62f), new Vector2(r * 0.85f, h * 0.7f), new Vector2(r * 0.4f, h * 0.86f), new Vector2(r * 0.18f, h * 0.93f), new Vector2(0.01f, h * 0.95f) }
                : new[] { new Vector2(0.01f, 0f), new Vector2(r * 0.45f, h * 0.07f), new Vector2(r * 0.9f, h * 0.24f), new Vector2(r, h * 0.42f), new Vector2(r * 0.92f, h * 0.6f), new Vector2(r * 0.6f, h * 0.77f), new Vector2(r * 0.32f, h * 0.87f), new Vector2(r * 0.3f, h * 0.91f), new Vector2(0.01f, h * 0.92f) };
            const int seg = 12;
            var c = new Vector3(top.x, y0, top.z);
            k.Of(m.Brass).AddLathe(c, prof, seg);
            k.Of(m.Brass).AddFrustum(c + Vector3.up * h * 0.9f, r * 0.2f, 0.008f, h * 0.1f, 8);
            k.Of(m.Brass).AddSphere(c - Vector3.up * 0.015f, Mathf.Max(0.012f, r * 0.12f), 8, 5);
            k.Of(SkPal.Ink).AddTorus(new Vector3(top.x, top.y + 0.01f, top.z), 0.018f, 0.004f, 8, 3, Quaternion.Euler(90f, 0f, 0f));
            // Glowing holes: one or two per facet, staggered row to row, flush on the flat facets.
            var gb = k.Of(glow);
            float[] rows = tall ? new[] { 0.16f, 0.26f, 0.36f, 0.46f, 0.56f } : new[] { 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f };
            float facet = Mathf.Cos(Mathf.PI / seg);
            for (int ri = 0; ri < rows.Length; ri++)
            {
                float y = h * rows[ri], rr = SkProfileR(prof, y);
                if (rr < 0.03f) continue;
                int per = rr > 0.11f ? 2 : 1;
                float s = Mathf.Min(0.02f, rr * 0.18f) * (ri % 2 == 0 ? 1f : 0.75f);
                for (int j = 0; j < seg; j++)
                    for (int q = 0; q < per; q++)
                    {
                        float off = per == 1 ? 0.5f : (q + 0.5f) / per;
                        if (ri % 2 == 1) off = per == 1 ? 0f : off + 0.25f;
                        float ang = (j + off) * 2f * Mathf.PI / seg, mid = (j + 0.5f) * 2f * Mathf.PI / seg;
                        var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                        var nrm = new Vector3(Mathf.Cos(mid), 0f, Mathf.Sin(mid));
                        float dist = rr * facet / Mathf.Max(0.5f, Mathf.Cos(ang - mid)) + 0.003f;
                        var p = c + Vector3.up * y + dir * dist;
                        var side = Vector3.Cross(Vector3.up, nrm) * s;
                        var up = Vector3.up * s * (tall ? 2.2f : 1.25f);
                        SkQuad(gb, p - side - up, p + side - up, p + side + up, p - side + up, nrm);
                    }
            }
        }

        /// <summary>A six-sided lantern of coloured glass in a brass frame, a domed cap and a drop finial.</summary>
        static void SkGlassLantern(SkBatch k, SkMats m, Vector3 top, float r, float h, int glow)
        {
            var br = k.Of(m.Brass);
            float y0 = top.y - h, gb = y0 + h * 0.2f, gt = y0 + h * 0.72f;
            var c = new Vector3(top.x, 0f, top.z);
            for (int i = 0; i < 6; i++)
            {
                float a0 = i * Mathf.PI / 3f, a1 = (i + 1) * Mathf.PI / 3f;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)); var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 b0 = c + d0 * r * 0.82f + Vector3.up * gb, b1 = c + d1 * r * 0.82f + Vector3.up * gb;
                Vector3 t0 = c + d0 * r + Vector3.up * gt, t1 = c + d1 * r + Vector3.up * gt;
                SkQuad(k.Of(glow), b0, b1, t1, t0, ((d0 + d1) * 0.5f).normalized);
                SkRod(br, b0, t0, 0.014f);
                var mb = (b0 + b1) * 0.5f; var mt = (t0 + t1) * 0.5f;
                SkRod(br, Vector3.Lerp(mb, mt, 0.5f) - (d1 - d0) * r * 0.42f, Vector3.Lerp(mb, mt, 0.5f) + (d1 - d0) * r * 0.42f, 0.008f);
            }
            br.AddCylinder(new Vector3(top.x, gb - 0.025f, top.z), r * 0.86f, 0.03f, 6);
            br.AddCylinder(new Vector3(top.x, gt - 0.005f, top.z), r * 1.05f, 0.03f, 6);
            br.AddFrustum(new Vector3(top.x, y0 + 0.025f, top.z), r * 0.18f, r * 0.84f, gb - y0 - 0.05f, 6);
            br.AddSphere(new Vector3(top.x, y0 + 0.012f, top.z), Mathf.Max(0.012f, r * 0.13f), 6, 4);
            br.AddLathe(new Vector3(top.x, gt + 0.025f, top.z), new[] { new Vector2(r * 1.06f, 0f), new Vector2(r * 0.9f, h * 0.08f), new Vector2(r * 0.55f, h * 0.16f), new Vector2(r * 0.2f, h * 0.22f), new Vector2(0.01f, h * 0.24f) }, 6);
            k.Of(SkPal.Ink).AddTorus(new Vector3(top.x, top.y + 0.01f, top.z), 0.018f, 0.004f, 8, 3, Quaternion.Euler(90f, 0f, 0f));
        }

        /// <summary>Lantern clusters along the lane, each with the souk's only point lights (six in all, no shadows).</summary>
        void SkLanterns(Transform g, SkMats m)
        {
            // (x, z, hook height, lit, intensity, lantern count)
            SkCluster(g, m, new Vector3(0.4f, 4.92f, -36.0f), true, 2.0f, 7, 0);
            SkCluster(g, m, new Vector3(-0.35f, SkBeamY - 0.08f, -31.25f), true, 1.8f, 5, 1);
            SkCluster(g, m, new Vector3(0.45f, SkBeamY - 0.08f, -29.15f), false, 0f, 3, 2);
            SkCluster(g, m, new Vector3(0.35f, SkBeamY - 0.08f, -27.05f), true, 1.8f, 6, 3);
            SkCluster(g, m, new Vector3(-0.3f, SkBeamY - 0.08f, -22.6f), true, 1.9f, 7, 4);
            SkCluster(g, m, new Vector3(0.35f, SkBeamY - 0.08f, -16.05f), true, 1.8f, 5, 5);
            SkCluster(g, m, new Vector3(-0.4f, SkBeamY - 0.08f, -14.7f), false, 0f, 3, 6);
            SkCluster(g, m, new Vector3(-0.3f, SkBeamY - 0.08f, -12.0f), true, 1.7f, 5, 7);
        }

        /// <summary>One cluster: a big lantern in the middle and smaller ones around it at different drops, on chains
        /// from a single hook; the light (if any) sits among them and fades up with the reveal.</summary>
        void SkCluster(Transform g, SkMats m, Vector3 hookXYZ, bool lit, float intensity, int count, int index)
        {
            var root = SkGroup(g, "SoukLanterns" + index, new Vector3(hookXYZ.x, 0f, hookXYZ.z));
            var k = new SkBatch();
            var rnd = new System.Random(500 + index * 13);
            var hook = new Vector3(0f, hookXYZ.y, 0f);
            k.Box(SkPal.Ink, hook + Vector3.up * 0.02f, new Vector3(0.16f, 0.04f, 0.1f));
            int[] glass = { SkPal.GlowAmber, SkPal.GlowRed, SkPal.GlowGreen, SkPal.GlowHoney, SkPal.GlowBlue };
            for (int i = 0; i < count; i++)
            {
                bool centre = i == 0;
                float ang = (i - 1) * 2f * Mathf.PI / Mathf.Max(1, count - 1) + SkR(rnd, -0.3f, 0.3f);
                float rad = centre ? 0f : SkR(rnd, 0.3f, 0.48f);
                var top = new Vector3(Mathf.Cos(ang) * rad, centre ? 3.55f : SkR(rnd, 3.05f, 3.9f), Mathf.Sin(ang) * rad);
                if (!lit && centre) top.y = 3.4f;
                SkRod(k.Of(SkPal.Ink), hook + new Vector3(top.x, 0f, top.z) * 0.15f, top + Vector3.up * 0.02f, 0.011f);
                int type = centre ? 0 : rnd.Next(3);
                if (type == 0) SkPierced(k, m, top, centre ? 0.19f : SkR(rnd, 0.1f, 0.14f), centre ? 0.62f : SkR(rnd, 0.32f, 0.42f), SkPal.GlowAmber);
                else if (type == 1) SkGlassLantern(k, m, top, SkR(rnd, 0.09f, 0.13f), SkR(rnd, 0.32f, 0.42f), glass[rnd.Next(glass.Length)]);
                else SkPierced(k, m, top, SkR(rnd, 0.07f, 0.09f), SkR(rnd, 0.34f, 0.44f), SkPal.GlowHoney, true);
            }
            k.Bake(root, "SoukLanterns");
            if (lit) AddPointLight(root, new Vector3(0f, 3.15f, 0f), new Color(1f, 0.69f, 0.4f), intensity, 6.5f);
            SkReveal(root, 10f, 0.25f + (index % 2) * 0.1f);
        }

        // ------------------------------------------------------------------ stalls
        /// <summary>Stall-local frame: x along the opening (-W/2..W/2), y up, z into the cell (0 = facade line, D = back
        /// wall). Goods spill onto the paving between Front (the path's edge) and 0. Unless the stall has a propped flap,
        /// folded leaves cover the front metre of each side wall.</summary>
        struct SkFrame { public float W, D, Deck, Front, WinX; public bool Flap, Window; }

        // Lathe profiles, normalised (x by radius, y by height); closed ones go back down inside so nothing reads hollow.
        static readonly Vector2[] s_Bowl = { new Vector2(0f, 0f), new Vector2(0.55f, 0f), new Vector2(0.85f, 0.35f), new Vector2(1f, 0.85f), new Vector2(1f, 1f), new Vector2(0.9f, 1f), new Vector2(0.78f, 0.55f), new Vector2(0f, 0.3f) };
        static readonly Vector2[] s_Pot = { new Vector2(0f, 0f), new Vector2(0.6f, 0f), new Vector2(0.95f, 0.3f), new Vector2(1f, 0.5f), new Vector2(0.8f, 0.75f), new Vector2(0.45f, 0.88f), new Vector2(0.42f, 0.96f), new Vector2(0.52f, 1f), new Vector2(0.45f, 1.01f), new Vector2(0.36f, 0.95f), new Vector2(0f, 0.9f) };
        static readonly Vector2[] s_Sack = { new Vector2(0f, 0f), new Vector2(0.75f, 0f), new Vector2(0.95f, 0.18f), new Vector2(1f, 0.55f), new Vector2(0.95f, 0.8f), new Vector2(1.06f, 0.86f), new Vector2(1.1f, 0.95f), new Vector2(1.02f, 1f) };
        static readonly Vector2[] s_Mound = { new Vector2(1.02f, 0.97f), new Vector2(0.72f, 1.1f), new Vector2(0.36f, 1.18f), new Vector2(0f, 1.21f) };
        static readonly Vector2[] s_Pouf = { new Vector2(0f, 0f), new Vector2(0.75f, 0f), new Vector2(0.97f, 0.18f), new Vector2(1f, 0.5f), new Vector2(0.97f, 0.82f), new Vector2(0.75f, 1f), new Vector2(0f, 1f) };
        static readonly Vector2[] s_Basket = { new Vector2(0f, 0f), new Vector2(0.6f, 0f), new Vector2(0.85f, 0.2f), new Vector2(1f, 0.85f), new Vector2(1.04f, 1f), new Vector2(0.95f, 1f), new Vector2(0.91f, 0.86f), new Vector2(0.77f, 0.24f), new Vector2(0f, 0.2f) };
        static readonly Vector2[] s_TagBase = { new Vector2(0f, 0f), new Vector2(0.7f, 0f), new Vector2(0.95f, 0.5f), new Vector2(1f, 1f), new Vector2(0.9f, 1f), new Vector2(0f, 0.7f) };
        static readonly Vector2[] s_TagLid = { new Vector2(0.92f, 0f), new Vector2(0.72f, 0.25f), new Vector2(0.42f, 0.55f), new Vector2(0.2f, 0.8f), new Vector2(0.16f, 0.86f), new Vector2(0.27f, 0.92f), new Vector2(0f, 1f) };
        static readonly Vector2[] s_Teapot = { new Vector2(0f, 0f), new Vector2(0.62f, 0f), new Vector2(0.94f, 0.25f), new Vector2(1f, 0.5f), new Vector2(0.75f, 0.78f), new Vector2(0.42f, 0.88f), new Vector2(0.38f, 0.96f), new Vector2(0f, 1f) };

        static void SkLathe(MeshBuilder b, Vector3 p, Vector2[] prof, float r, float h, int seg)
        {
            var pts = new Vector2[prof.Length];
            for (int i = 0; i < prof.Length; i++) pts[i] = new Vector2(prof[i].x * r, prof[i].y * h);
            b.AddLathe(p, pts, seg);
        }

        static void SkTagine(SkBatch k, int baseCol, int lidCol, Vector3 p, float r)
        {
            SkLathe(k.Of(baseCol), p, s_TagBase, r, r * 0.26f, 12);
            SkLathe(k.Of(lidCol), p + Vector3.up * r * 0.2f, s_TagLid, r * 0.93f, r * 1.45f, 12);
        }

        /// <summary>A babouche: sole, upper, pointed toe and the crushed-down heel; <paramref name="p"/> is the sole's
        /// underside, the toe points along the rotation's +Z.</summary>
        static void SkSlipper(SkBatch k, int col, Vector3 p, Quaternion r)
        {
            var b = k.Of(col);
            b.AddBox(p + r * new Vector3(0f, 0.009f, 0f), new Vector3(0.088f, 0.018f, 0.26f), r);
            b.AddBox(p + r * new Vector3(0f, 0.042f, 0.045f), new Vector3(0.082f, 0.05f, 0.15f), r);
            b.AddBox(p + r * new Vector3(0f, 0.03f, 0.14f), new Vector3(0.05f, 0.028f, 0.05f), r);
            b.AddBox(p + r * new Vector3(0f, 0.023f, -0.085f), new Vector3(0.084f, 0.01f, 0.085f), r);
        }

        static void SkTeapot(SkBatch k, Material metal, Vector3 p, float s)
        {
            var b = k.Of(metal);
            SkLathe(b, p, s_Teapot, 0.075f * s, 0.15f * s, 10);
            b.AddSphere(p + Vector3.up * 0.155f * s, 0.014f * s, 6, 4);
            b.AddTube(new List<Vector3> { p + new Vector3(0.06f, 0.05f, 0f) * s, p + new Vector3(0.1f, 0.09f, 0f) * s, p + new Vector3(0.14f, 0.15f, 0f) * s }, 0.009f * s, 5);
            b.AddTube(new List<Vector3> { p + new Vector3(-0.065f, 0.11f, 0f) * s, p + new Vector3(-0.11f, 0.1f, 0f) * s, p + new Vector3(-0.115f, 0.06f, 0f) * s, p + new Vector3(-0.07f, 0.035f, 0f) * s }, 0.007f * s, 5, false);
        }

        /// <summary>A flat-woven kilim (folded or laid out): a ground colour with bands across it.</summary>
        static void SkKilim(SkBatch k, Vector3 c, Vector3 size, Quaternion r, System.Random rnd)
        {
            int[] grounds = { SkPal.Madder, SkPal.Indigo, SkPal.Saffron, SkPal.Cream, SkPal.Teal };
            int[] bands = { SkPal.Cream, SkPal.Ink, SkPal.Saffron, SkPal.Madder, SkPal.Indigo, SkPal.GlazeGreen };
            int ground = grounds[rnd.Next(grounds.Length)];
            k.Box(ground, c, size, r);
            int n = rnd.Next(3, 6);
            for (int i = 0; i < n; i++)
            {
                int band = bands[rnd.Next(bands.Length)];
                if (band == ground) band = SkPal.Cream;
                float t = (i + 0.5f) / n - 0.5f;
                k.Box(band, c + r * new Vector3(0f, 0f, t * size.z * 0.9f), new Vector3(size.x + 0.004f, size.y + 0.004f, size.z * SkR(rnd, 0.04f, 0.09f)), r);
            }
        }

        /// <summary>Boards (with two uprights) against the back wall of a cell, at the given heights.</summary>
        static void SkShelves(SkBatch k, SkFrame f, float[] ys, float depth)
        {
            float back = f.D - 0.03f, w = f.W - 0.12f;
            foreach (float y in ys) k.Box(SkPal.Wood, new Vector3(0f, y, back - depth / 2f), new Vector3(w, 0.03f, depth));
            foreach (float e in new[] { -1f, 1f })
                k.Box(SkPal.WoodDark, new Vector3(e * (w / 2f - 0.02f), (f.Deck + ys[ys.Length - 1]) / 2f, back - depth / 2f), new Vector3(0.04f, ys[ys.Length - 1] - f.Deck, depth));
        }

        /// <summary>A low wooden stool (the stallholder's, left where he sat).</summary>
        static void SkStool(SkBatch k, Vector3 p)
        {
            k.Of(SkPal.Wood).AddCylinder(p + Vector3.up * 0.27f, 0.15f, 0.035f, 10);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2f * Mathf.PI / 3f;
                var foot = p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.13f;
                SkRod(k.Of(SkPal.WoodDark), foot, p + Vector3.up * 0.27f + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.08f, 0.028f);
            }
        }

        // ------------------------------------------------------------------ the wall above each stall
        /// <summary>Hanging places along the wall above the opening (stall-local x), keeping <paramref name="clear"/>
        /// metres from the upstairs window's centre.</summary>
        static List<float> SkUpperSlots(SkFrame f, float spacing, float clear)
        {
            var xs = new List<float>();
            float half = f.W / 2f + 0.05f;
            void Fill(float a, float b)
            {
                float len = b - a;
                if (len < spacing * 0.6f) return;
                int n = Mathf.Max(1, Mathf.FloorToInt(len / spacing + 0.01f));
                for (int i = 0; i < n; i++) xs.Add(a + (i + 0.5f) * len / n);
            }
            if (!f.Window) Fill(-half, half);
            else { Fill(-half, f.WinX - clear); Fill(f.WinX + clear, half); }
            return xs;
        }

        /// <summary>A plate (or brass tray) hung flat on a wall facing the lane, painted in rings and dots.</summary>
        static void SkWallPlate(SkBatch k, Vector3 c, float r, int[] s)
        {
            k.Of(s[0]).AddDisc(c, Vector3.back, r, 14);
            k.Of(s[1]).AddDisc(c - new Vector3(0f, 0f, 0.004f), Vector3.back, r * 0.72f, 14);
            k.Of(s[2]).AddDisc(c - new Vector3(0f, 0f, 0.008f), Vector3.back, r * 0.32f, 10);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                k.Box(s[2], c + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * r * 0.86f - new Vector3(0f, 0f, 0.005f), new Vector3(0.024f, 0.024f, 0.006f));
            }
        }

        /// <summary>What a stall shows on the wall above its opening, up to the roof: plates, strings of slippers, brass
        /// trays, lanterns on brackets, bags, kilims, straw hats, herbs. Stays clear of the upstairs window.</summary>
        static void SkUpper(SkBatch k, SkMats m, SkFrame f, SkKind kind, System.Random rnd)
        {
            int[][] glaze =
            {
                new[] { SkPal.GlazeBlue, SkPal.White, SkPal.Cobalt }, new[] { SkPal.GlazeGreen, SkPal.Cream, SkPal.GlazeYellow },
                new[] { SkPal.Clay, SkPal.Cream, SkPal.Madder }, new[] { SkPal.GlazeYellow, SkPal.Cobalt, SkPal.White },
            };
            int[] cloth = { SkPal.Madder, SkPal.Indigo, SkPal.Saffron, SkPal.Teal, SkPal.Cream, SkPal.Plum, SkPal.Rose };
            switch (kind)
            {
                case SkKind.Ceramic:
                    foreach (float y in new[] { 3.3f, 3.95f })
                        foreach (float x in SkUpperSlots(f, 0.5f, 0.66f))
                            SkWallPlate(k, new Vector3(x + SkR(rnd, -0.04f, 0.04f), y + SkR(rnd, -0.04f, 0.04f), -0.025f), SkR(rnd, 0.15f, 0.2f), glaze[rnd.Next(glaze.Length)]);
                    break;
                case SkKind.Metal:
                    foreach (float y in new[] { 3.3f, 3.95f })
                        foreach (float x in SkUpperSlots(f, 0.55f, 0.7f))
                        {
                            float rr = SkR(rnd, 0.17f, 0.24f);
                            var c = new Vector3(x, y + SkR(rnd, -0.05f, 0.05f), -0.012f);
                            k.Of(m.Brass).AddCylinder(c, rr, 0.012f, 16, 1f, Quaternion.Euler(-90f, 0f, 0f));
                            k.Of(SkPal.WoodDark).AddDisc(c - new Vector3(0f, 0f, 0.0135f), Vector3.back, rr * 0.22f, 10);
                        }
                    break;
                case SkKind.Slipper:
                {
                    int[] cols = { SkPal.Saffron, SkPal.Madder, SkPal.White, SkPal.Cobalt, SkPal.GlazeGreen, SkPal.Rose };
                    foreach (float x in SkUpperSlots(f, 0.2f, 0.52f))
                    {
                        int c = cols[rnd.Next(cols.Length)];
                        SkRod(k.Of(SkPal.StrawDark), new Vector3(x, 4.5f, -0.04f), new Vector3(x, 3.05f, -0.04f), 0.006f);
                        for (int i = 0; i < 4; i++) SkSlipper(k, c, new Vector3(x, 4.25f - i * 0.33f, -0.03f), Quaternion.Euler(90f, 180f, 0f));
                    }
                    break;
                }
                case SkKind.Lantern:
                    foreach (float x in SkUpperSlots(f, 0.85f, 0.55f))
                    {
                        var wall = new Vector3(x, 3.85f, 0f);
                        SkRod(k.Of(SkPal.Iron), wall, wall + new Vector3(0f, 0f, -0.42f), 0.022f);
                        SkRod(k.Of(SkPal.Iron), wall + new Vector3(0f, -0.22f, 0f), wall + new Vector3(0f, 0f, -0.3f), 0.016f);
                        var top = wall + new Vector3(0f, -SkR(rnd, 0.12f, 0.3f), -0.4f);
                        SkRod(k.Of(SkPal.Ink), wall + new Vector3(0f, 0f, -0.4f), top + Vector3.up * 0.02f, 0.008f);
                        if (rnd.NextDouble() < 0.5) SkPierced(k, m, top, SkR(rnd, 0.1f, 0.13f), SkR(rnd, 0.34f, 0.42f), SkPal.GlowAmber);
                        else SkGlassLantern(k, m, top, SkR(rnd, 0.1f, 0.12f), SkR(rnd, 0.32f, 0.4f), rnd.NextDouble() < 0.5 ? SkPal.GlowRed : SkPal.GlowGreen);
                    }
                    break;
                case SkKind.Leather:
                    foreach (float y in new[] { 3.15f, 3.8f })
                        foreach (float x in SkUpperSlots(f, 0.42f, 0.6f))
                            SkBag(k, s_Hides[rnd.Next(s_Hides.Length)], new Vector3(x, y, -0.09f), SkR(rnd, 0.9f, 1.15f), true);
                    break;
                case SkKind.Fabric:
                {
                    var xs = SkUpperSlots(f, 0.42f, 0.62f);
                    if (xs.Count == 0) break;
                    SkRod(k.Of(SkPal.Iron), new Vector3(-f.W / 2f - 0.05f, 4.45f, -0.16f), new Vector3(f.W / 2f + 0.05f, 4.45f, -0.16f), 0.02f);
                    foreach (float x in xs)
                    {
                        float len = SkR(rnd, 1.1f, 1.5f);
                        k.Box(cloth[rnd.Next(cloth.Length)], new Vector3(x, 4.45f - len / 2f, -0.16f), new Vector3(0.38f, len, 0.008f), Quaternion.Euler(SkR(rnd, -2f, 2f), SkR(rnd, -6f, 6f), 0f));
                        k.Box(cloth[rnd.Next(cloth.Length)], new Vector3(x, 4.45f - len + 0.06f, -0.165f), new Vector3(0.385f, 0.05f, 0.009f));
                    }
                    break;
                }
                case SkKind.Basket:
                    foreach (float y in new[] { 3.3f, 3.95f })
                        foreach (float x in SkUpperSlots(f, 0.5f, 0.66f))
                        {
                            var c = new Vector3(x, y, -0.02f);
                            var face = Quaternion.Euler(-90f, 0f, 0f);
                            if (rnd.NextDouble() < 0.5)
                            {
                                k.Of(SkPal.Straw).AddCylinder(c, SkR(rnd, 0.18f, 0.22f), 0.012f, 14, 1f, face);
                                k.Of(SkPal.StrawDark).AddFrustum(c - new Vector3(0f, 0f, 0.012f), 0.1f, 0.08f, 0.09f, 12, false, true, 1f, face);
                                k.Of(rnd.NextDouble() < 0.5 ? SkPal.Madder : SkPal.Indigo).AddCylinder(c - new Vector3(0f, 0f, 0.012f), 0.102f, 0.02f, 12, 1f, face);
                            }
                            else
                            {
                                float tr = SkR(rnd, 0.17f, 0.22f);
                                k.Of(SkPal.Straw).AddDisc(c, Vector3.back, tr, 14);
                                k.Of(SkPal.StrawDark).AddDisc(c - new Vector3(0f, 0f, 0.004f), Vector3.back, tr * 0.68f, 14);
                                k.Of(SkPal.Teal).AddDisc(c - new Vector3(0f, 0f, 0.008f), Vector3.back, tr * 0.4f, 12);
                            }
                        }
                    break;
                case SkKind.Spice:
                    foreach (float x in SkUpperSlots(f, 0.34f, 0.5f))
                    {
                        float len = SkR(rnd, 0.45f, 0.8f);
                        var top = new Vector3(x, 4.45f, -0.12f);
                        SkRod(k.Of(SkPal.StrawDark), top, top - Vector3.up * len, 0.006f);
                        if (rnd.NextDouble() < 0.5)
                            for (float y = 0.06f; y < len; y += 0.045f)
                                k.Box(SkPal.Paprika, top - Vector3.up * y, new Vector3(0.022f, 0.06f, 0.022f), Quaternion.Euler(0f, 0f, SkR(rnd, -25f, 25f)));
                        else
                            for (float y = 0.08f; y < len; y += 0.09f)
                                k.Of(SkPal.Cream).AddSphere(top - Vector3.up * y, 0.035f, 6, 4);
                    }
                    break;
            }
        }

        /// <summary>One stall's goods as a group root at the cell's base, rising as the player approaches.</summary>
        void SkStall(Transform g, SkMats m, SkBay b, int seed, int index)
        {
            var root = SkGroup(g, "SoukStall" + index + "_" + b.Kind, new Vector3(b.Side * b.Half, 0f, b.Zc), b.Side > 0 ? 90f : -90f);
            var k = new SkBatch();
            var rnd = new System.Random(seed);
            float rail = b.Half > SkLane ? SkRailSquare : SkRailLane;
            var f = new SkFrame { W = b.W, D = b.Depth, Deck = b.Deck, Front = -(b.Half - rail) + 0.06f, Flap = b.Flap, Window = b.Window, WinX = b.Side * (b.Zc - b.WindowZ) };
            switch (b.Kind)
            {
                case SkKind.Spice: SkSpices(k, f, rnd); break;
                case SkKind.Ceramic: SkCeramics(k, f, rnd); break;
                case SkKind.Carpet: SkCarpets(k, m, f, rnd); break;
                case SkKind.Slipper: SkSlippers(k, f, rnd); break;
                case SkKind.Basket: SkBaskets(k, f, rnd); break;
                case SkKind.Fabric: SkFabrics(k, f, rnd); break;
                case SkKind.Metal: SkMetal(k, m, f, rnd); break;
                case SkKind.Lantern: SkLanternShop(k, m, f, rnd); break;
                case SkKind.Leather: SkLeather(k, f, rnd); break;
                case SkKind.Tea: SkTea(k, m, f, rnd); break;
            }
            SkUpper(k, m, f, b.Kind, rnd);
            k.Bake(root, "SoukStall");
            SkReveal(root, b.Side > 0 ? 9.8f : 9.4f, 0.04f + (index % 3) * 0.12f);
        }

        /// <summary>Spices: jars on shelves, rolled-down sacks heaped with buds and pulses, the tiered stand of bowls piled
        /// into cones out front, chillies and herbs hanging from the rail, a balance on the deck.</summary>
        static void SkSpices(SkBatch k, SkFrame f, System.Random rnd)
        {
            float W = f.W, D = f.D, deck = f.Deck, back = D - 0.03f;
            float[] shelf = { deck + 0.8f, deck + 1.2f, deck + 1.6f };
            SkShelves(k, f, shelf, 0.24f);
            foreach (float sy in shelf)
                for (float x = -W / 2f + 0.15f; x < W / 2f - 0.13f; x += 0.14f)
                {
                    if (rnd.NextDouble() < 0.15) continue;
                    float h = SkR(rnd, 0.12f, 0.2f);
                    k.Of(SkPal.Spice(rnd)).AddCylinder(new Vector3(x, sy + 0.015f, back - 0.12f), 0.05f, h, 6);
                    k.Of(SkPal.WoodDark).AddCylinder(new Vector3(x, sy + 0.015f + h, back - 0.12f), 0.045f, 0.022f, 6);
                }
            int n = Mathf.Max(2, Mathf.FloorToInt((W - 0.3f) / 0.44f));
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < n; i++)
                {
                    float x = -((n - 1) * 0.44f) / 2f + i * 0.44f + SkR(rnd, -0.03f, 0.03f);
                    var p = new Vector3(x, deck, 0.36f + row * 0.44f);
                    if (p.z > D - 0.22f || (!f.Flap && Mathf.Abs(x) > W / 2f - 0.32f && p.z < 1.2f) || (row == 0 && i == n - 1)) continue;
                    float r = SkR(rnd, 0.15f, 0.18f), h = SkR(rnd, 0.26f, 0.34f);
                    SkLathe(k.Of(rnd.NextDouble() < 0.5 ? SkPal.Straw : SkPal.StrawDark), p, s_Sack, r, h, 10);
                    SkLathe(k.Of(SkPal.Spice(rnd)), p, s_Mound, r, h, 10);
                }
            // The tiered stand out front.
            float d0 = f.Front + 0.16f, d1 = f.Front + 0.46f;
            k.Box(SkPal.Wood, new Vector3(0f, 0.15f, d0), new Vector3(W - 0.25f, 0.3f, 0.3f));
            k.Box(SkPal.Wood, new Vector3(0f, 0.26f, d1), new Vector3(W - 0.25f, 0.52f, 0.3f));
            k.Box(SkPal.WoodDark, new Vector3(0f, 0.31f, d0 - 0.14f), new Vector3(W - 0.23f, 0.03f, 0.03f));
            int nb = Mathf.Max(1, Mathf.FloorToInt((W - 0.3f) / 0.34f));
            int[] bowls = { SkPal.Clay, SkPal.Cream, SkPal.GlazeGreen, SkPal.Clay, SkPal.GlazeBlue };
            foreach (var (d, y) in new[] { (d0, 0.3f), (d1, 0.52f) })
                for (int i = 0; i < nb; i++)
                {
                    var p = new Vector3(-((nb - 1) * 0.34f) / 2f + i * 0.34f, y, d);
                    SkLathe(k.Of(bowls[rnd.Next(bowls.Length)]), p, s_Bowl, 0.15f, 0.07f, 10);
                    k.Of(SkPal.Spice(rnd)).AddFrustum(p + Vector3.up * 0.06f, 0.145f, 0.012f, SkR(rnd, 0.14f, 0.3f), 10, false, true);
                }
            // Chillies on strings and bunches of herbs from the rail.
            for (float x = -W / 2f + 0.25f; x < W / 2f - 0.2f; x += SkR(rnd, 0.22f, 0.34f))
            {
                float len = SkR(rnd, 0.35f, 0.7f);
                var top = new Vector3(x, SkLintel - 0.17f, 0.12f);
                SkRod(k.Of(SkPal.StrawDark), top, top - Vector3.up * len, 0.006f);
                if (rnd.NextDouble() < 0.55)
                    for (float y = 0.06f; y < len; y += 0.045f)
                        k.Box(SkPal.Paprika, top - Vector3.up * y + new Vector3(SkR(rnd, -0.015f, 0.015f), 0f, 0f), new Vector3(0.022f, 0.06f, 0.022f), Quaternion.Euler(0f, 0f, SkR(rnd, -25f, 25f)));
                else k.Of(rnd.NextDouble() < 0.5 ? SkPal.Henna : SkPal.Olive).AddFrustum(top - Vector3.up * len, 0.07f, 0.015f, len * 0.6f, 6);
            }
            // A balance on the deck's front edge, its pans resting.
            var bp = new Vector3(-((n - 1) * 0.44f) / 2f + (n - 1) * 0.44f, deck, 0.32f);
            k.Box(SkPal.WoodDark, bp + new Vector3(0f, 0.02f, 0f), new Vector3(0.16f, 0.04f, 0.1f));
            k.Box(SkPal.Ochre, bp + new Vector3(0f, 0.2f, 0f), new Vector3(0.016f, 0.34f, 0.016f));
            k.Box(SkPal.Ochre, bp + new Vector3(0f, 0.37f, 0f), new Vector3(0.36f, 0.014f, 0.014f));
            foreach (float e in new[] { -1f, 1f })
            {
                SkRod(k.Of(SkPal.Ink), bp + new Vector3(e * 0.17f, 0.37f, 0f), bp + new Vector3(e * 0.17f, 0.2f, 0f), 0.004f);
                k.Of(SkPal.Ochre).AddCylinder(bp + new Vector3(e * 0.17f, 0.19f, 0f), 0.07f, 0.015f, 10);
            }
            if (D > 1.5f) SkStool(k, new Vector3(-W * 0.2f, deck, D - 0.4f));
        }

        /// <summary>Pottery: painted plates hung in rows, stacks of bowls on a shelf, tagines and jars on the deck, a big
        /// storage jar and a crate of tagines out front.</summary>
        static void SkCeramics(SkBatch k, SkFrame f, System.Random rnd)
        {
            float W = f.W, D = f.D, deck = f.Deck, back = D - 0.03f;
            int[][] sets =
            {
                new[] { SkPal.GlazeBlue, SkPal.White, SkPal.Cobalt }, new[] { SkPal.GlazeGreen, SkPal.Cream, SkPal.GlazeYellow },
                new[] { SkPal.Clay, SkPal.Cream, SkPal.Madder }, new[] { SkPal.GlazeYellow, SkPal.Cobalt, SkPal.White },
                new[] { SkPal.White, SkPal.GlazeBlue, SkPal.GlazeGreen },
            };
            for (int r = 0; r < 3; r++)
                for (float x = -W / 2f + 0.24f + (r % 2) * 0.12f; x < W / 2f - 0.2f; x += 0.36f)
                {
                    var s = sets[rnd.Next(sets.Length)];
                    float pr = SkR(rnd, 0.12f, 0.155f);
                    var c = new Vector3(x, deck + 1.0f + r * 0.38f, back - 0.012f);
                    k.Of(s[0]).AddDisc(c, Vector3.back, pr, 14);
                    k.Of(s[1]).AddDisc(c - new Vector3(0f, 0f, 0.004f), Vector3.back, pr * 0.72f, 14);
                    k.Of(s[2]).AddDisc(c - new Vector3(0f, 0f, 0.008f), Vector3.back, pr * 0.32f, 10);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f;
                        k.Box(s[2], c + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * pr * 0.86f - new Vector3(0f, 0f, 0.005f), new Vector3(0.024f, 0.024f, 0.006f));
                    }
                }
            float sy = deck + 0.62f;
            SkShelves(k, f, new[] { sy }, 0.3f);
            for (float x = -W / 2f + 0.2f; x < W / 2f - 0.15f; x += 0.3f)
            {
                var s = sets[rnd.Next(sets.Length)];
                int stack = rnd.Next(2, 5);
                for (int i = 0; i < stack; i++) SkLathe(k.Of(s[i % 2]), new Vector3(x, sy + 0.015f + i * 0.035f, back - 0.15f), s_Bowl, 0.11f, 0.06f, 10);
            }
            // Two rows on the deck: tagines, nested bowls, jars.
            float lim = f.Flap ? W / 2f - 0.22f : W / 2f - 0.3f;
            foreach (float d in new[] { 0.36f, Mathf.Min(0.8f, D - 0.75f) })
                for (float x = -lim; x <= lim + 0.001f; x += SkR(rnd, 0.3f, 0.36f))
                {
                    var p = new Vector3(x + SkR(rnd, -0.03f, 0.03f), deck, d + SkR(rnd, -0.05f, 0.05f));
                    double roll = rnd.NextDouble();
                    if (roll < 0.45) SkTagine(k, SkPal.Clay, sets[rnd.Next(sets.Length)][0], p, SkR(rnd, 0.12f, 0.15f));
                    else if (roll < 0.75)
                    {
                        var s = sets[rnd.Next(sets.Length)];
                        int st = rnd.Next(3, 6);
                        for (int i = 0; i < st; i++) SkLathe(k.Of(s[i % 2]), p + Vector3.up * i * 0.035f, s_Bowl, 0.12f - i * 0.006f, 0.06f, 10);
                    }
                    else SkLathe(k.Of(sets[rnd.Next(sets.Length)][0]), p, s_Pot, SkR(rnd, 0.08f, 0.11f), SkR(rnd, 0.3f, 0.42f), 10);
                }
            // Out front: a big storage jar, a crate stacked with tagines, two plates leaning on it.
            SkLathe(k.Of(SkPal.Clay), new Vector3(-W * 0.3f, 0f, f.Front + 0.3f), s_Pot, 0.21f, 0.72f, 12);
            var cr = new Vector3(W * 0.16f, 0f, f.Front + 0.32f);
            k.Box(SkPal.Wood, cr + new Vector3(0f, 0.15f, 0f), new Vector3(0.62f, 0.3f, 0.42f));
            for (int i = 0; i < 3; i++) SkTagine(k, SkPal.Clay, sets[i][0], cr + new Vector3((i - 1) * 0.2f, 0.3f, 0f), 0.09f);
            SkTagine(k, SkPal.Clay, sets[3][0], cr + new Vector3(-0.1f, 0.39f, 0f), 0.08f);
            foreach (float e in new[] { -0.12f, 0.14f })
            {
                var s = sets[rnd.Next(sets.Length)];
                var pc = cr + new Vector3(e, 0.15f, -0.27f);
                var rot = Quaternion.Euler(-72f, 0f, 0f);
                k.Of(s[0]).AddCylinder(pc - rot * Vector3.up * 0.006f, 0.15f, 0.012f, 14, 1f, rot);
                k.Of(s[1]).AddDisc(pc + rot * Vector3.up * 0.0075f, rot * Vector3.up, 0.1f, 12);
            }
        }

        /// <summary>Carpets: a big one hung over the lintel on the facade, one on the back wall with a kilim, folded stacks
        /// on the deck, rolled rugs leaning against the piers and a kilim laid on the paving.</summary>
        static void SkCarpets(SkBatch k, SkMats m, SkFrame f, System.Random rnd)
        {
            float W = f.W, D = f.D, deck = f.Deck, back = D - 0.03f;
            float cw = Mathf.Min(W + 0.25f, 3.6f);
            k.Box(m.Carpet, new Vector3(0f, 3.6f, -0.15f), new Vector3(cw, 1.62f, 0.02f), Quaternion.Euler(-4f, 0f, 0f), 1.3f);
            SkRod(k.Of(SkPal.Iron), new Vector3(-cw / 2f - 0.12f, 4.43f, -0.21f), new Vector3(cw / 2f + 0.12f, 4.43f, -0.21f), 0.025f);
            k.Box(SkPal.Cream, new Vector3(0f, 2.775f, -0.093f), new Vector3(cw, 0.05f, 0.012f), Quaternion.Euler(-4f, 0f, 0f));
            k.Box(m.Carpet, new Vector3(-W * 0.12f, deck + 1.2f, back - 0.02f), new Vector3(W * 0.56f, 1.9f, 0.02f), 1.1f);
            SkKilim(k, new Vector3(W * 0.3f, deck + 1.05f, back - 0.03f), new Vector3(W * 0.3f, 0.012f, 1.6f), Quaternion.Euler(-90f, 0f, 0f), rnd);
            // Folded stacks on the deck, carpet and kilim in turn.
            int stacks = W > 3f ? 3 : 1;
            for (int s = 0; s < stacks; s++)
            {
                float x = stacks == 1 ? 0f : (s - 1) * 1.05f, d = Mathf.Min(0.75f, D - 0.4f);
                int layers = rnd.Next(5, 9);
                for (int i = 0; i < layers; i++)
                {
                    var c = new Vector3(x + SkR(rnd, -0.04f, 0.04f), deck + 0.035f + i * 0.07f, d + SkR(rnd, -0.03f, 0.03f));
                    var r = Quaternion.Euler(0f, SkR(rnd, -6f, 6f), 0f);
                    if (i % 2 == 0) k.Box(m.Carpet, c, new Vector3(0.9f, 0.07f, 0.6f), r, 0.9f);
                    else SkKilim(k, c, new Vector3(0.88f, 0.07f, 0.58f), r, rnd);
                }
            }
            // Rolled rugs leaning back against the piers either side of the opening.
            foreach (float e in new[] { -1f, 1f })
                for (int i = 0; i < 2; i++)
                {
                    float len = SkR(rnd, 1.5f, 2.1f), rr = SkR(rnd, 0.075f, 0.11f);
                    float lean = Mathf.Atan2(0.24f - rr, len) * Mathf.Rad2Deg;
                    var b = new Vector3(e * (W / 2f + 0.1f + i * 0.2f), 0f, -0.34f + i * 0.04f);
                    var rot = Quaternion.Euler(lean, 0f, e * SkR(rnd, 1f, 5f));
                    if (i == 0) k.Of(m.Carpet).AddCylinder(b, rr, len, 10, 0.8f, rot);
                    else k.Of(rnd.NextDouble() < 0.5 ? SkPal.Madder : SkPal.Indigo).AddCylinder(b, rr, len, 10, 1f, rot);
                }
            SkKilim(k, new Vector3(0f, 0.006f, f.Front + 0.33f), new Vector3(Mathf.Min(W - 0.4f, 1.6f), 0.012f, 0.5f), Quaternion.identity, rnd);
            SkStool(k, new Vector3(W * 0.3f, deck, 0.4f));
        }

        /// <summary>Babouches: a stepped rack of pairs on the deck (toes to the lane), columns of single colours hung on
        /// the back wall, a bench of pairs out front.</summary>
        static void SkSlippers(SkBatch k, SkFrame f, System.Random rnd)
        {
            float W = f.W, D = f.D, deck = f.Deck, back = D - 0.03f;
            int[] cols = { SkPal.Saffron, SkPal.Madder, SkPal.White, SkPal.Cobalt, SkPal.GlazeGreen, SkPal.Leather, SkPal.Rose, SkPal.Cream, SkPal.Saffron };
            float rw = f.Flap ? W - 0.25f : W - 0.42f;
            for (int s = 0; s < 3; s++)
            {
                float top = deck + 0.1f + s * 0.17f, d = 0.34f + s * 0.27f;
                k.Box(SkPal.Wood, new Vector3(0f, (deck + top) / 2f, d), new Vector3(rw, top - deck, 0.27f));
                for (float x = -rw / 2f + 0.12f; x < rw / 2f - 0.1f; x += 0.23f)
                {
                    int c = cols[rnd.Next(cols.Length)];
                    foreach (float e in new[] { -1f, 1f })
                        SkSlipper(k, c, new Vector3(x + e * 0.048f, top, d), Quaternion.Euler(0f, 180f + e * 4f, 0f));
                }
            }
            var hang = Quaternion.Euler(90f, 180f, 0f);
            int col = 0;
            for (float x = -W / 2f + 0.16f; x < W / 2f - 0.14f; x += 0.125f)
            {
                if (rnd.NextDouble() < 0.3) col++;
                for (int r = 0; r < 4; r++)
                    SkSlipper(k, cols[col % cols.Length], new Vector3(x, deck + 1.85f - r * 0.3f, back - 0.01f), hang);
            }
            k.Box(SkPal.Wood, new Vector3(0f, 0.14f, f.Front + 0.3f), new Vector3(W - 0.3f, 0.28f, 0.34f));
            for (float x = -W / 2f + 0.3f; x < W / 2f - 0.25f; x += 0.26f)
            {
                int c = cols[rnd.Next(cols.Length)];
                foreach (float e in new[] { -1f, 1f })
                    SkSlipper(k, c, new Vector3(x + e * 0.05f, 0.28f, f.Front + 0.3f), Quaternion.Euler(0f, 180f + e * 5f + SkR(rnd, -8f, 8f), 0f));
            }
            SkStool(k, new Vector3(W * 0.25f, deck, D - 0.35f));
        }

        static readonly int[] s_Hides = { SkPal.Leather, SkPal.LeatherDark, SkPal.Madder, SkPal.Saffron, SkPal.Teal, SkPal.Cream, SkPal.Cinnamon };

        /// <summary>A leather pouf with a stitched band round its middle.</summary>
        static void SkPouf(SkBatch k, Vector3 p, System.Random rnd)
        {
            SkLathe(k.Of(s_Hides[rnd.Next(s_Hides.Length)]), p, s_Pouf, 0.23f, 0.22f, 12);
            k.Of(rnd.NextDouble() < 0.5 ? SkPal.Cream : SkPal.Ink).AddCylinder(p + Vector3.up * 0.104f, 0.232f, 0.012f, 12);
        }

        /// <summary>Baskets and poufs: stacks of leather poufs out front, nested baskets and a lidded hamper on the deck,
        /// small baskets hanging from the rail, woven trays on the back wall.</summary>
        static void SkBaskets(SkBatch k, SkFrame f, System.Random rnd)
        {
            float W = f.W, D = f.D, deck = f.Deck, back = D - 0.03f;
            for (int s = 0; s < 3; s++)
            {
                float x = (s - 1) * Mathf.Min(0.6f, W / 3f);
                int h = rnd.Next(1, 4);
                for (int i = 0; i < h; i++) SkPouf(k, new Vector3(x + SkR(rnd, -0.03f, 0.03f), i * 0.215f, f.Front + 0.3f + SkR(rnd, -0.02f, 0.02f)), rnd);
            }
            float d = Mathf.Min(0.75f, D - 0.5f);
            foreach (float e in new[] { -1f, 1f })
            {
                int levels = rnd.Next(3, 5);
                for (int i = 0; i < levels; i++)
                    SkLathe(k.Of(i % 2 == 0 ? SkPal.Straw : SkPal.StrawDark), new Vector3(e * W * 0.22f, deck + i * 0.13f, d), s_Basket, 0.26f - i * 0.035f, 0.2f, 12);
            }
            var hp = new Vector3(0f, deck, D - 0.36f);
            SkLathe(k.Of(SkPal.Straw), hp, s_Basket, 0.2f, 0.55f, 12);
            k.Of(SkPal.StrawDark).AddFrustum(hp + Vector3.up * 0.55f, 0.215f, 0.06f, 0.12f, 12);
            k.Of(SkPal.StrawDark).AddSphere(hp + Vector3.up * 0.69f, 0.03f, 6, 4);
            // Small baskets hung by their handles from the rail.
            for (float x = -W / 2f + 0.3f; x < W / 2f - 0.25f; x += SkR(rnd, 0.32f, 0.42f))
            {
                float drop = SkR(rnd, 0.25f, 0.5f), y = SkLintel - 0.17f - drop;
                SkRod(k.Of(SkPal.StrawDark), new Vector3(x, SkLintel - 0.17f, 0.12f), new Vector3(x, y, 0.12f), 0.006f);
                k.Of(SkPal.StrawDark).AddTorus(new Vector3(x, y - 0.1f, 0.12f), 0.1f, 0.008f, 12, 3, Quaternion.Euler(90f, 0f, 0f));
                SkLathe(k.Of(SkPal.Straw), new Vector3(x, y - 0.22f, 0.12f), s_Basket, 0.13f, 0.12f, 10);
            }
            // Woven trays on the back wall.
            for (int r = 0; r < 2; r++)
                for (float x = -W / 2f + 0.3f + r * 0.2f; x < W / 2f - 0.25f; x += 0.5f)
                {
                    float tr = SkR(rnd, 0.17f, 0.23f);
                    var c = new Vector3(x, deck + 1.2f + r * 0.5f, back - 0.012f);
                    k.Of(SkPal.Straw).AddDisc(c, Vector3.back, tr, 14);
                    k.Of(SkPal.StrawDark).AddDisc(c - new Vector3(0f, 0f, 0.004f), Vector3.back, tr * 0.7f, 14);
                    k.Of(rnd.NextDouble() < 0.5 ? SkPal.Madder : SkPal.Teal).AddDisc(c - new Vector3(0f, 0f, 0.008f), Vector3.back, tr * 0.5f, 12);
                    k.Of(SkPal.Straw).AddDisc(c - new Vector3(0f, 0f, 0.012f), Vector3.back, tr * 0.3f, 10);
                }
            SkStool(k, new Vector3(-W * 0.05f, deck, 0.35f));
        }

        /// <summary>Cloth: shelves of folded fabric, scarves hanging from the rail at both sides of the opening, bolts
        /// standing in a basket and a pile of blankets on a low table out front.</summary>
        static void SkFabrics(SkBatch k, SkFrame f, System.Random rnd)
        {
            float W = f.W, D = f.D, deck = f.Deck, back = D - 0.03f;
            int[] cloth = { SkPal.Madder, SkPal.Indigo, SkPal.Saffron, SkPal.Teal, SkPal.Cream, SkPal.Plum, SkPal.Rose, SkPal.Olive, SkPal.Cobalt, SkPal.White, SkPal.Ochre };
            float[] shelves = { deck + 0.4f, deck + 0.82f, deck + 1.24f, deck + 1.66f };
            SkShelves(k, f, shelves, 0.34f);
            foreach (float sy in shelves)
                for (float x = -W / 2f + 0.24f; x < W / 2f - 0.2f; x += 0.36f)
                {
                    int n = rnd.Next(3, 7);
                    for (int i = 0; i < n; i++)
                        k.Box(cloth[rnd.Next(cloth.Length)], new Vector3(x + SkR(rnd, -0.015f, 0.015f), sy + 0.04f + i * 0.05f, back - 0.17f), new Vector3(0.32f, 0.048f, 0.28f));
                }
            foreach (float e in new[] { -1f, 1f })
                for (float x = W * 0.16f; x < W / 2f - 0.28f; x += SkR(rnd, 0.1f, 0.15f))
                {
                    float len = SkR(rnd, 0.9f, 1.4f);
                    k.Box(cloth[rnd.Next(cloth.Length)], new Vector3(e * x, SkLintel - 0.17f - len / 2f, 0.12f + SkR(rnd, -0.03f, 0.03f)),
                        new Vector3(SkR(rnd, 0.2f, 0.28f), len, 0.006f), Quaternion.Euler(SkR(rnd, -3f, 3f), SkR(rnd, -15f, 15f), SkR(rnd, -2f, 2f)));
                }
            var bk = new Vector3(-W * 0.25f, 0f, f.Front + 0.32f);
            SkLathe(k.Of(SkPal.StrawDark), bk, s_Basket, 0.25f, 0.36f, 12);
            for (int i = 0; i < 7; i++)
            {
                float a = i * 0.9f, rr = i == 0 ? 0f : 0.13f;
                k.Of(cloth[rnd.Next(cloth.Length)]).AddCylinder(bk + new Vector3(Mathf.Cos(a) * rr, 0.06f, Mathf.Sin(a) * rr), 0.055f, SkR(rnd, 0.75f, 1.1f), 8, 1f, Quaternion.Euler(SkR(rnd, -7f, 7f), 0f, SkR(rnd, -7f, 7f)));
            }
            var tb = new Vector3(W * 0.22f, 0f, f.Front + 0.32f);
            k.Box(SkPal.Wood, tb + new Vector3(0f, 0.36f, 0f), new Vector3(0.7f, 0.04f, 0.44f));
            foreach (float ex in new[] { -0.3f, 0.3f }) foreach (float ez in new[] { -0.18f, 0.18f })
                k.Box(SkPal.WoodDark, tb + new Vector3(ex, 0.17f, ez), new Vector3(0.04f, 0.34f, 0.04f));
            for (int i = 0; i < 5; i++)
                k.Box(cloth[rnd.Next(cloth.Length)], tb + new Vector3(SkR(rnd, -0.03f, 0.03f), 0.41f + i * 0.06f, SkR(rnd, -0.02f, 0.02f)), new Vector3(0.6f, 0.058f, 0.38f), Quaternion.Euler(0f, SkR(rnd, -5f, 5f), 0f));
            k.Box(cloth[rnd.Next(cloth.Length)], tb + new Vector3(0f, 0.3f, -0.225f), new Vector3(0.55f, 0.3f, 0.008f));
            SkStool(k, new Vector3(0f, deck, 0.4f));
        }

        /// <summary>Metalwork: engraved brass trays hung on the back wall and leaning out front, teapots and bowls on the
        /// deck, a shelf of small pieces, pierced lanterns lit under the lintel.</summary>
        static void SkMetal(SkBatch k, SkMats m, SkFrame f, System.Random rnd)
        {
            float W = f.W, D = f.D, deck = f.Deck, back = D - 0.03f;
            var face = Quaternion.Euler(-90f, 0f, 0f);
            void Engrave(Vector3 c, Vector3 n, float rr)
            {
                var right = Vector3.Cross(Vector3.up, n).normalized; var up = Vector3.Cross(n, right);
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI / 6f;
                    k.Box(SkPal.WoodDark, c + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * rr * 0.68f + n * 0.002f, new Vector3(0.02f, 0.02f, 0.004f), Quaternion.LookRotation(n));
                }
                k.Of(SkPal.WoodDark).AddDisc(c + n * 0.002f, n, rr * 0.2f, 10);
            }
            for (int r = 0; r < 2; r++)
                for (float x = -W / 2f + 0.3f + r * 0.2f; x < W / 2f - 0.25f; x += 0.48f)
                {
                    float rr = SkR(rnd, 0.16f, 0.22f);
                    var c = new Vector3(x, deck + 1.2f + r * 0.5f, back - 0.005f);
                    k.Of(m.Brass).AddCylinder(c, rr, 0.012f, 16, 1f, face);
                    Engrave(c - new Vector3(0f, 0f, 0.012f), Vector3.back, rr);
                }
            for (int i = 0; i < 3; i++)
            {
                float rr = SkR(rnd, 0.26f, 0.38f), lean = SkR(rnd, 10f, 16f);
                var rot = Quaternion.Euler(-90f + lean, SkR(rnd, -10f, 10f), 0f);
                var n = rot * Vector3.up;
                var c = new Vector3((i - 1) * W * 0.3f, rr * Mathf.Cos(lean * Mathf.Deg2Rad) + 0.01f, -0.06f - i % 2 * 0.1f - rr * Mathf.Sin(lean * Mathf.Deg2Rad));
                k.Of(m.Brass).AddCylinder(c - n * 0.008f, rr, 0.016f, 18, 1f, rot);
                Engrave(c + n * 0.008f, n, rr);
            }
            float sy = deck + 0.72f;
            SkShelves(k, f, new[] { sy }, 0.28f);
            for (float x = -W / 2f + 0.2f; x < W / 2f - 0.15f; x += 0.22f)
            {
                if (rnd.NextDouble() < 0.5) SkTeapot(k, m.Brass, new Vector3(x, sy + 0.015f, back - 0.14f), SkR(rnd, 0.7f, 1f));
                else SkLathe(k.Of(m.Brass), new Vector3(x, sy + 0.015f, back - 0.14f), s_Pot, 0.06f, SkR(rnd, 0.14f, 0.22f), 8);
            }
            for (float x = -W / 2f + 0.25f; x < W / 2f - 0.2f; x += 0.26f)
            {
                if (!f.Flap && Mathf.Abs(x) > W / 2f - 0.32f) continue;
                var p = new Vector3(x, deck, SkR(rnd, 0.3f, 0.5f));
                if (rnd.NextDouble() < 0.6) SkTeapot(k, m.Brass, p, SkR(rnd, 1f, 1.35f));
                else for (int i = 0; i < 3; i++) SkLathe(k.Of(m.Brass), p + Vector3.up * i * 0.03f, s_Bowl, 0.11f - i * 0.012f, 0.06f, 12);
            }
            foreach (float x in new[] { -W * 0.3f, 0f, W * 0.3f })
            {
                var top = new Vector3(x + SkR(rnd, -0.05f, 0.05f), SkR(rnd, 2.22f, 2.36f), -0.05f);
                SkRod(k.Of(SkPal.Ink), new Vector3(top.x, SkLintel, -0.05f), top + Vector3.up * 0.02f, 0.008f);
                SkPierced(k, m, top, SkR(rnd, 0.07f, 0.09f), SkR(rnd, 0.26f, 0.32f), SkPal.GlowAmber);
            }
            SkStool(k, new Vector3(W * 0.1f, deck, D - 0.5f));
        }

        /// <summary>The lantern maker's: lanterns crowded under the rail and the joists, all lit; more standing on the
        /// deck and two tall floor lanterns on the paving out front.</summary>
        static void SkLanternShop(SkBatch k, SkMats m, SkFrame f, System.Random rnd)
        {
            float W = f.W, D = f.D, deck = f.Deck;
            int[] glass = { SkPal.GlowAmber, SkPal.GlowRed, SkPal.GlowGreen, SkPal.GlowHoney, SkPal.GlowBlue };
            float lim = f.Flap ? W / 2f - 0.15f : W / 2f - 0.27f;
            foreach (float d in new[] { 0.12f, 0.9f, 1.45f })
            {
                if (d > D - 0.15f) continue;
                float hookY = d < 0.2f ? SkLintel - 0.17f : SkLintel - 0.115f;
                for (float x = -lim + (d > 0.5f ? 0.15f : 0f); x <= lim; x += SkR(rnd, 0.3f, 0.38f))
                {
                    var top = new Vector3(x, hookY - SkR(rnd, 0.05f, 0.4f), d);
                    SkRod(k.Of(SkPal.Ink), new Vector3(x, hookY, d), top + Vector3.up * 0.02f, 0.008f);
                    if (rnd.NextDouble() < 0.5) SkPierced(k, m, top, SkR(rnd, 0.08f, 0.12f), SkR(rnd, 0.28f, 0.38f), rnd.NextDouble() < 0.7 ? SkPal.GlowAmber : SkPal.GlowHoney);
                    else SkGlassLantern(k, m, top, SkR(rnd, 0.08f, 0.11f), SkR(rnd, 0.28f, 0.36f), glass[rnd.Next(glass.Length)]);
                }
            }
            for (float x = -lim + 0.1f; x <= lim - 0.1f; x += 0.32f)
            {
                float h = SkR(rnd, 0.3f, 0.45f);
                var top = new Vector3(x, deck + 0.02f + h, D - 0.3f);
                if (rnd.NextDouble() < 0.5) SkPierced(k, m, top, SkR(rnd, 0.08f, 0.11f), h, SkPal.GlowAmber, true);
                else SkGlassLantern(k, m, top, SkR(rnd, 0.09f, 0.12f), h, glass[rnd.Next(glass.Length)]);
            }
            foreach (float e in new[] { -1f, 1f })
            {
                float h = SkR(rnd, 0.62f, 0.78f);
                SkPierced(k, m, new Vector3(e * W * 0.28f, h + 0.02f, f.Front + 0.3f), 0.13f, h, SkPal.GlowAmber, true);
            }
            SkGlassLantern(k, m, new Vector3(0f, 0.36f, f.Front + 0.34f), 0.12f, 0.34f, SkPal.GlowRed);
        }

        /// <summary>A leather bag: body, flap, buckle and a strap up to its peg.</summary>
        static void SkBag(SkBatch k, int col, Vector3 p, float s, bool peg)
        {
            k.Box(col, p, new Vector3(0.3f, 0.24f, 0.09f) * s);
            k.Box(col, p + new Vector3(0f, 0.06f, -0.05f) * s, new Vector3(0.3f, 0.13f, 0.01f) * s);
            k.Box(SkPal.Ochre, p + new Vector3(0f, 0f, -0.057f) * s, new Vector3(0.03f, 0.03f, 0.006f) * s);
            var top = p + new Vector3(0f, 0.34f, 0f) * s;
            SkRod(k.Of(col), p + new Vector3(-0.13f, 0.11f, 0f) * s, top, 0.014f);
            SkRod(k.Of(col), p + new Vector3(0.13f, 0.11f, 0f) * s, top, 0.014f);
            if (peg) k.Box(SkPal.Wood, top + new Vector3(0f, 0f, 0.035f), new Vector3(0.025f, 0.025f, 0.08f));
        }

        /// <summary>Leather: bags on pegs and hanging from the rail, belts along a side wall, poufs out front, a pile of
        /// folded hides on the deck.</summary>
        static void SkLeather(SkBatch k, SkFrame f, System.Random rnd)
        {
            float W = f.W, D = f.D, deck = f.Deck, back = D - 0.03f;
            for (int r = 0; r < 2; r++)
                for (float x = -W / 2f + 0.3f + r * 0.15f; x < W / 2f - 0.25f; x += 0.36f)
                    SkBag(k, s_Hides[rnd.Next(s_Hides.Length)], new Vector3(x, deck + 1.0f + r * 0.6f, back - 0.07f), SkR(rnd, 0.8f, 1.15f), true);
            for (int i = 0; i < 3; i++)
                SkBag(k, s_Hides[rnd.Next(s_Hides.Length)], new Vector3((i - 1) * W * 0.3f, SkLintel - 0.55f, 0.12f), 0.9f, false);
            // Belts hang in a row from a bar along one side wall (behind the folded leaves, if any).
            float side = -W / 2f + 0.05f, b0 = f.Flap ? 0.25f : 1.2f;
            k.Box(SkPal.Wood, new Vector3(side, SkLintel - 0.12f, (b0 + D - 0.15f) / 2f), new Vector3(0.03f, 0.03f, D - 0.15f - b0));
            for (float d = b0 + 0.05f; d < D - 0.2f; d += 0.065f)
                k.Box(s_Hides[rnd.Next(s_Hides.Length)], new Vector3(side + 0.01f, SkLintel - 0.55f, d), new Vector3(0.01f, 0.85f, 0.035f));
            // Satchels standing in a row at the front of the deck.
            for (float x = -W / 2f + 0.35f; x < W / 2f - 0.3f; x += 0.34f)
                SkBag(k, s_Hides[rnd.Next(s_Hides.Length)], new Vector3(x, deck + 0.12f, 0.3f), SkR(rnd, 0.85f, 1f), false);
            foreach (float x in new[] { -W * 0.3f, W * 0.28f })
                for (int i = 0, h = rnd.Next(1, 3); i < h; i++) SkPouf(k, new Vector3(x, i * 0.215f, f.Front + 0.3f), rnd);
            for (int i = 0; i < 6; i++)
                k.Box(s_Hides[rnd.Next(s_Hides.Length)], new Vector3(SkR(rnd, -0.04f, 0.04f), deck + 0.015f + i * 0.03f, 0.55f), new Vector3(0.62f, 0.028f, 0.45f), Quaternion.Euler(0f, SkR(rnd, -10f, 10f), 0f));
            SkStool(k, new Vector3(W * 0.3f, deck, D - 0.45f));
        }

        /// <summary>A curled-up cat asleep (a dark shape on a cushion).</summary>
        static void SkCat(SkBatch k, Vector3 p)
        {
            var b = k.Of(SkPal.Ink);
            b.AddLathe(p, new[] { new Vector2(0f, 0f), new Vector2(0.11f, 0.005f), new Vector2(0.15f, 0.04f), new Vector2(0.13f, 0.09f), new Vector2(0.07f, 0.12f), new Vector2(0f, 0.13f) }, 10);
            var head = p + new Vector3(0.12f, 0.06f, -0.07f);
            b.AddSphere(head, 0.055f, 8, 6);
            foreach (float e in new[] { -1f, 1f }) b.AddFrustum(head + new Vector3(e * 0.026f, 0.035f, 0.005f), 0.018f, 0.002f, 0.04f, 4);
            var tail = new List<Vector3>();
            for (int i = 0; i <= 6; i++) { float a = -0.4f + i * 0.4f; tail.Add(p + new Vector3(Mathf.Cos(a) * 0.155f, 0.02f, Mathf.Sin(a) * 0.155f)); }
            b.AddTube(tail, 0.018f, 5);
        }

        /// <summary>The tea seller's corner by the gate: cushions and bolsters on the low deck, a low table with a brass
        /// tray, teapot and painted glasses, the cat asleep, two stools and a brazier with its coals still glowing.</summary>
        static void SkTea(SkBatch k, SkMats m, SkFrame f, System.Random rnd)
        {
            float W = f.W, D = f.D, deck = f.Deck, back = D - 0.03f;
            k.Box(SkPal.Madder, new Vector3(0f, deck + 0.06f, back - 0.3f), new Vector3(W - 0.12f, 0.12f, 0.5f));
            foreach (float z in new[] { back - 0.42f, back - 0.18f }) k.Box(SkPal.Saffron, new Vector3(0f, deck + 0.06f, z), new Vector3(W - 0.11f, 0.122f, 0.03f));
            k.Box(SkPal.Indigo, new Vector3(0f, deck + 0.3f, back - 0.07f), new Vector3(W - 0.16f, 0.36f, 0.12f), Quaternion.Euler(-8f, 0f, 0f));
            k.Of(SkPal.Teal).AddCylinder(new Vector3(-W / 2f + 0.1f, deck + 0.21f, back - 0.3f), 0.09f, W - 0.2f, 10, 1f, Quaternion.Euler(0f, 0f, -90f));
            var t = new Vector3(0f, deck, Mathf.Min(1.05f, D - 0.62f));
            k.Of(SkPal.Wood).AddCylinder(t, 0.12f, 0.26f, 8);
            k.Of(SkPal.Wood).AddCylinder(t + Vector3.up * 0.26f, 0.3f, 0.035f, 14);
            k.Of(m.Brass).AddCylinder(t + Vector3.up * 0.295f, 0.25f, 0.012f, 18);
            SkTeapot(k, m.Brass, t + new Vector3(-0.06f, 0.307f, 0.03f), 1f);
            int[] tint = { SkPal.GlazeGreen, SkPal.Madder, SkPal.Cobalt, SkPal.GlazeGreen, SkPal.Plum };
            for (int i = 0; i < 5; i++)
            {
                float a = 0.5f + i * 0.6f;
                var gp = t + new Vector3(Mathf.Cos(a) * 0.16f, 0.307f, -Mathf.Sin(a) * 0.16f);
                k.Of(tint[i]).AddFrustum(gp, 0.021f, 0.027f, 0.075f, 8);
                k.Of(SkPal.Saffron).AddCylinder(gp + Vector3.up * 0.05f, 0.0275f, 0.009f, 8);
            }
            SkLathe(k.Of(SkPal.White), t + new Vector3(0.13f, 0.307f, 0.1f), s_Bowl, 0.05f, 0.04f, 8);
            SkCat(k, new Vector3(-W * 0.18f, deck + 0.12f, back - 0.3f));
            SkStool(k, new Vector3(-0.36f, 0f, f.Front + 0.32f));
            SkStool(k, new Vector3(0.04f, 0f, f.Front + 0.26f));
            var br = new Vector3(0.44f, 0f, f.Front + 0.34f);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2f * Mathf.PI / 3f;
                SkRod(k.Of(SkPal.Iron), br + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.13f, br + new Vector3(Mathf.Cos(a) * 0.1f, 0.16f, Mathf.Sin(a) * 0.1f), 0.015f);
            }
            k.Of(SkPal.Iron).AddFrustum(br + Vector3.up * 0.15f, 0.1f, 0.15f, 0.12f, 10, true, false);
            for (int i = 0; i < 9; i++)
                k.Box(i % 3 == 0 ? SkPal.GlowRed : SkPal.Ink, br + new Vector3(SkR(rnd, -0.08f, 0.08f), 0.26f, SkR(rnd, -0.08f, 0.08f)), new Vector3(0.05f, 0.03f, 0.04f), Quaternion.Euler(SkR(rnd, -20f, 20f), SkR(rnd, 0f, 90f), 0f));
            SkTeapot(k, m.Brass, br + new Vector3(0f, 0.27f, 0f), 1.3f);
        }

        // ------------------------------------------------------------------ canopies
        void SkCanopies(Transform g, SkMats m)
        {
            SkCanopy(g, m.Cloth[0], -39.45f, -37.25f, SkSquare, 4.95f, 0.5f, 0);
            SkCanopy(g, m.Cloth[1], -25.45f, -23.4f, SkLane, 4.68f, 0.4f, 1);
            SkCanopy(g, m.Cloth[2], -21.8f, -18.7f, SkLane, 4.68f, 0.46f, 2);
        }

        /// <summary>A cloth shade stretched across under the evening sky, sagging in the middle, hemmed, and tied with
        /// rope to iron rings in the facades at its corners and mid-edges.</summary>
        static void SkCanopy(Transform g, Material cloth, float z0, float z1, float half, float y, float sag, int index)
        {
            var root = SkGroup(g, "SoukCanopy" + index, new Vector3(0f, 0f, (z0 + z1) / 2f));
            var k = new SkBatch();
            var b = k.Of(cloth);
            const int nx = 10, nz = 4;
            float len = z1 - z0, w = half - 0.18f;
            Vector3 P(int i, int j)
            {
                float u = i / (float)nx, v = j / (float)nz;
                float drop = sag * 4f * u * (1f - u) * (0.8f + 0.8f * v * (1f - v)) + 0.035f * Mathf.Sin(u * Mathf.PI * 4f) * Mathf.Sin(v * Mathf.PI);
                return new Vector3(Mathf.Lerp(-w, w, u), y - drop, Mathf.Lerp(-len / 2f, len / 2f, v));
            }
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    Vector3 a = P(i, j), c = P(i + 1, j), d = P(i + 1, j + 1), e = P(i, j + 1);
                    var n = Vector3.Cross(e - a, c - a).normalized;
                    SkQuad(b, a, c, d, e, n, 1.2f);
                    SkQuad(b, a, c, d, e, -n, 1.2f);
                }
            foreach (int j in new[] { 0, nz })
                for (int i = 0; i < nx; i++) SkRod(b, P(i, j), P(i + 1, j), 0.03f);
            foreach (int i in new[] { 0, nx })
            {
                SkRod(b, P(i, 0), P(i, nz), 0.035f);
                foreach (int j in new[] { 0, nz / 2, nz })
                {
                    var p = P(i, j);
                    var wall = new Vector3(Mathf.Sign(p.x) * (half - 0.015f), y + 0.14f, p.z);
                    SkRod(b, p, wall, 0.014f);
                    b.AddTorus(wall, 0.03f, 0.007f, 8, 3, Quaternion.Euler(0f, 0f, 90f));
                }
            }
            k.Bake(root, "SoukCanopy");
            SkReveal(root, 9.5f, 0.35f);
        }
    }
}

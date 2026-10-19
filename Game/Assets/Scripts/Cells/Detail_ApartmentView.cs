using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    // The night view from the apartment (playtest: "make all the countries places detailed"). It belongs to the cell: the
    // Corniche far below, a marina channel with two island towers, and a skyline of generic glass towers across the water.
    // Bodies are flat colours on one palette texture (one material per piece); windows, lamps and car lights are
    // vertex-coloured unlit quads; pools and reflections are additive. The Margin copy builds the same city, dead: every
    // window dark glass, the streets unlit, one window left burning. Everything stays inside the 320 m sky dome.
    public partial class ApartmentCell
    {
        const int PaletteSize = 16;
        const int C_Glass = 0, C_GlassBlue = 1, C_Band = 2, C_Ground = 3, C_Road = 4, C_Paving = 5, C_Water = 6, C_Frond = 7,
                  C_Trunk = 8, C_Car = 9, C_CarPale = 10, C_Sand = 11, C_Seawall = 12, C_Stone = 13;
        static readonly Vector3 ViewEye = new Vector3(0f, 1.6f, 0f);
        const float WaterY = -32f, GroundY = -30f;

        static readonly Color Warm = new Color(1f, 0.74f, 0.44f), WarmDim = new Color(0.7f, 0.45f, 0.24f), Cool = new Color(0.74f, 0.84f, 1f),
                              Screen = new Color(0.45f, 0.55f, 0.9f), Haze = new Color(0.55f, 0.36f, 0.28f), Dead = new Color(0.1f, 0.115f, 0.14f),
                              DeadHaze = new Color(0.2f, 0.24f, 0.3f), Beacon = new Color(1f, 0.12f, 0.08f), Sodium = new Color(1f, 0.7f, 0.38f);

        /// <summary>Flat-coloured parts that each sample one texel of a palette texture, so a whole skyline is one material.</summary>
        sealed class Flat
        {
            public readonly MeshBuilder B = new MeshBuilder();
            Vector2 m_Uv;
            public Flat Use(int index) { m_Uv = new Vector2((index + 0.5f) / PaletteSize, 0.5f); return this; }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) => B.AddQuad(a, b, c, d, m_Uv, m_Uv, m_Uv, m_Uv);
            public void Tri(Vector3 a, Vector3 b, Vector3 c) => B.AddTriangle(a, b, c, m_Uv, m_Uv, m_Uv);

            /// <summary>A box without its underside (nothing out there is seen from below).</summary>
            public void Box(Vector3 c, Vector3 size, Quaternion rot)
            {
                var h = size * 0.5f;
                Vector3 P(float x, float y, float z) => c + rot * new Vector3(x * h.x, y * h.y, z * h.z);
                Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1));
                Quad(P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1));
                Quad(P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1));
                Quad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1));
                Quad(P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), P(-1, 1, -1));
            }

            public void Box(Vector3 c, Vector3 size) => Box(c, size, Quaternion.identity);

            /// <summary>An upright prism on a convex polygon (counter-clockwise seen from above), capped on top.</summary>
            public void Prism(IList<Vector2> poly, float y0, float y1, Vector3 offset, Quaternion rot)
            {
                int n = poly.Count;
                Vector3 V(Vector2 p, float y) => offset + rot * new Vector3(p.x, y, p.y);
                for (int i = 0; i < n; i++)
                {
                    var a = poly[i]; var b = poly[(i + 1) % n];
                    Quad(V(b, y0), V(a, y0), V(a, y1), V(b, y1));
                }
                for (int i = 1; i + 1 < n; i++) Tri(V(poly[0], y1), V(poly[i + 1], y1), V(poly[i], y1));
            }

            /// <summary>A quad facing the horizontal direction <paramref name="n"/>, centred on <paramref name="c"/>.</summary>
            public void Face(Vector3 c, Vector3 n, float w, float h)
            {
                var r = Vector3.Cross(Vector3.up, -n).normalized * (w * 0.5f);
                var u = Vector3.up * (h * 0.5f);
                Quad(c + r - u, c - r - u, c - r + u, c + r + u);
            }
        }

        struct Reflection { public Vector3 Foot; public float Width, Low, High; public Color Colour; }

        /// <summary>Vertex colours are not gamma-converted: hand over the sRGB colour the eye should see (alpha stays 1, the
        /// additive glow multiplies by it).</summary>
        static Color Lin(Color c)
        {
            var l = QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
            l.a = 1f;
            return l;
        }

        /// <summary>A colour scaled in brightness only (Unity's Color * float also scales alpha).</summary>
        static Color Rgb(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        /// <summary>A lit (or dead) quad facing <paramref name="n"/>, with 0..1 UVs.</summary>
        static void FaceQuad(MeshBuilder b, Vector3 c, Vector3 n, float w, float h)
        {
            var r = Vector3.Cross(Vector3.up, -n).normalized * (w * 0.5f);
            var u = Vector3.up * (h * 0.5f);
            b.AddQuad(c + r - u, c - r - u, c - r + u, c + r + u, new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1));
        }

        /// <summary>A soft additive quad lying flat, long along <paramref name="along"/> (light pools, reflections, beams).</summary>
        static void GlowQuad(MeshBuilder b, Vector3 c, Vector3 along, float length, float width)
        {
            along.y = 0f; along.Normalize();
            var side = Vector3.Cross(Vector3.up, along).normalized * (width * 0.5f);
            var l = along * (length * 0.5f);
            b.AddQuad(c - side + l, c + side + l, c + side - l, c - side - l, new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0));
        }

        static Texture2D CityPalette(bool dark)
        {
            var t = new Texture2D(PaletteSize, 1, TextureFormat.RGBA32, false)
            {
                name = dark ? "TS_CityPaletteMargin" : "TS_CityPalette", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            var c = new Color[PaletteSize];
            for (int i = 0; i < c.Length; i++) c[i] = new Color(0.2f, 0.2f, 0.2f);
            c[C_Glass] = dark ? new Color(0.085f, 0.095f, 0.12f) : new Color(0.1f, 0.11f, 0.14f);
            c[C_GlassBlue] = dark ? new Color(0.075f, 0.09f, 0.12f) : new Color(0.08f, 0.1f, 0.16f);
            c[C_Band] = dark ? new Color(0.16f, 0.17f, 0.19f) : new Color(0.22f, 0.21f, 0.22f);
            c[C_Ground] = dark ? new Color(0.06f, 0.065f, 0.07f) : new Color(0.075f, 0.07f, 0.07f);
            c[C_Road] = new Color(0.04f, 0.04f, 0.045f);
            c[C_Paving] = dark ? new Color(0.12f, 0.13f, 0.14f) : new Color(0.17f, 0.155f, 0.14f);
            c[C_Water] = dark ? new Color(0.012f, 0.02f, 0.032f) : new Color(0.02f, 0.035f, 0.06f);
            c[C_Frond] = dark ? new Color(0.06f, 0.07f, 0.065f) : new Color(0.06f, 0.085f, 0.05f);
            c[C_Trunk] = dark ? new Color(0.1f, 0.1f, 0.1f) : new Color(0.13f, 0.1f, 0.08f);
            c[C_Car] = new Color(0.05f, 0.05f, 0.06f);
            c[C_CarPale] = dark ? new Color(0.3f, 0.31f, 0.32f) : new Color(0.55f, 0.55f, 0.56f);
            c[C_Sand] = dark ? new Color(0.2f, 0.21f, 0.22f) : new Color(0.34f, 0.28f, 0.22f);
            c[C_Seawall] = dark ? new Color(0.17f, 0.18f, 0.19f) : new Color(0.26f, 0.23f, 0.2f);
            c[C_Stone] = dark ? new Color(0.13f, 0.14f, 0.15f) : new Color(0.24f, 0.2f, 0.17f);
            t.SetPixels(c);
            t.Apply(false, false);
            return t;
        }

        /// <summary>The backdrop shader (self-lit with this cell's sun and ambient) with a lighter fog than the preset's, so
        /// the towers keep their windows instead of fading into flat silhouettes.</summary>
        Material CityMat(Texture2D palette, float fog)
        {
            var m = new Material(Mats.Template("TS_PocketLit")) { name = "CityPalette" };
            m.SetTexture("_BaseMap", palette);
            m.SetColor("_BaseColor", Color.white);
            m.SetColor("_EmissionColor", Color.black);
            Lighting.ConfigurePocket(m);
            m.SetFloat("_FogDensity", fog);
            m.SetFloat("_StencilComp", Mats.StencilAlways);
            m.SetFloat("_UsePocketClip", 0f);
            m.renderQueue = 2000;
            return m;
        }

        static GameObject Piece(string name, MeshBuilder b, Material m, Transform parent)
        {
            var go = MeshFactory.FromBuilder(name, b, m, parent);
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        readonly struct TowerSpec
        {
            public readonly float X, Z, W, D, H, Yaw;
            public readonly int Style;
            public TowerSpec(float x, float z, float w, float d, float h, float yaw, int style) { X = x; Z = z; W = w; D = d; H = h; Yaw = yaw; Style = style; }
        }

        static TowerSpec T(float x, float z, float w, float d, float h, float yaw, int style) => new TowerSpec(x, z, w, d, h, yaw, style);

        // Far skyline in three layers (cell-local metres; the Margin copy lets the layers drift apart in Update). Gaps of low
        // buildings around 20 degrees north of east leave the low evening sun visible between the towers.
        static readonly TowerSpec[][] s_Skyline =
        {
            new[] { T(162f, -128f, 22f, 20f, 70f, 8f, 0), T(170f, -95f, 18f, 18f, 96f, -5f, 1), T(161f, -62f, 26f, 18f, 58f, 12f, 2), T(175f, -35f, 20f, 20f, 112f, 0f, 3),
                    T(166f, -8f, 24f, 22f, 84f, -10f, 0), T(180f, 18f, 18f, 16f, 124f, 4f, 2), T(163f, 38f, 28f, 20f, 52f, 0f, 0), T(170f, 62f, 30f, 22f, 40f, 6f, 0),
                    T(188f, 92f, 20f, 20f, 102f, -8f, 1), T(168f, 118f, 24f, 18f, 76f, 10f, 3) },
            new[] { T(215f, -140f, 26f, 22f, 88f, 0f, 2), T(228f, -108f, 20f, 20f, 120f, 6f, 0), T(212f, -78f, 24f, 24f, 100f, 0f, 3), T(232f, -48f, 22f, 18f, 132f, -4f, 1),
                    T(210f, -20f, 28f, 22f, 92f, 8f, 0), T(236f, 6f, 20f, 20f, 118f, 0f, 2), T(218f, 30f, 24f, 20f, 104f, -6f, 3), T(230f, 58f, 30f, 24f, 45f, 0f, 0),
                    T(214f, 84f, 22f, 22f, 50f, 10f, 0), T(226f, 112f, 22f, 18f, 110f, -3f, 1), T(210f, 138f, 26f, 20f, 80f, 5f, 2) },
            new[] { T(262f, -70f, 26f, 22f, 96f, 0f, 0), T(270f, -38f, 22f, 22f, 108f, 0f, 3), T(258f, -8f, 28f, 24f, 84f, 6f, 1), T(268f, 24f, 22f, 20f, 104f, -5f, 2),
                    T(260f, 52f, 30f, 26f, 60f, 0f, 0), T(272f, 74f, 20f, 20f, 90f, 4f, 0) },
        };

        /// <summary>
        /// The night city beyond the balcony. The near piece (land, Corniche, promenade, channel, marina islands with two
        /// towers, the far bank) is static; the three far skyline layers are <see cref="SkylineLayers"/> (the Margin copy
        /// pulls them apart). Must be built after <see cref="Lighting"/> is set.
        /// </summary>
        void BuildCityView(Transform g)
        {
            bool dark = Corrupted;
            var palette = CityPalette(dark);
            var city = CityMat(palette, dark ? 0.0048f : 0.0042f);
            var lights = Mats.UnlitInstance(Color.white, null, false, 1f);
            lights.name = "CityLights";
            var glow = Mats.UnlitInstance(Color.white, Tex.SoftDot, true, 1f);
            glow.name = "CityGlow";
            glow.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One); // additive: pools of light, reflections
            var rnd = new System.Random(dark ? 1970 : 971);
            var reflections = new List<Reflection>();

            // ---- near: the static piece
            var near = new GameObject("CityNear").transform;
            near.SetParent(g, false);
            var f = new Flat();
            var lit = new MeshBuilder();
            var pools = new MeshBuilder();
            // Our shore: land, the Corniche (two carriageways and a palm median), the promenade, the seawall.
            f.Use(C_Ground).Box(new Vector3(22.75f, GroundY - 0.5f, 0f), new Vector3(34.5f, 1f, 600f));
            f.Use(C_Road).Box(new Vector3(23f, GroundY + 0.01f, 0f), new Vector3(14f, 0.04f, 580f));
            f.Use(C_Paving).Box(new Vector3(23f, GroundY + 0.1f, 0f), new Vector3(0.9f, 0.2f, 580f));
            f.Use(C_Paving).Box(new Vector3(35.8f, GroundY + 0.06f, 0f), new Vector3(8.4f, 0.12f, 580f));
            f.Use(C_Seawall).Box(new Vector3(39.9f, GroundY - 1.1f, 0f), new Vector3(0.6f, 2.4f, 580f));
            // The channel, the far bank (two plates keep every corner inside the sky dome) and the marina islands.
            f.Use(C_Water).Box(new Vector3(95f, WaterY - 0.5f, 0f), new Vector3(110f, 1f, 500f));
            f.Use(C_Ground).Box(new Vector3(203.5f, GroundY - 0.8f, 0f), new Vector3(117f, 1.6f, 240f));
            f.Use(C_Ground).Box(new Vector3(192.5f, GroundY - 0.8f, 0f), new Vector3(95f, 1.6f, 390f));
            f.Use(C_Seawall).Box(new Vector3(145.2f, GroundY - 1.2f, 0f), new Vector3(0.6f, 2.4f, 390f));
            f.Use(C_Ground).Box(new Vector3(88f, GroundY - 0.8f, -45f), new Vector3(50f, 1.6f, 44f));
            f.Use(C_Ground).Box(new Vector3(86f, GroundY - 0.8f, 40f), new Vector3(46f, 1.6f, 46f));

            // Street lamps over both carriageways, with pools of sodium light on the road.
            for (float z = -228f; z <= 228f; z += 24f)
                foreach (float sx in new[] { 16.4f, 29.6f })
                {
                    float arm = sx < 23f ? 1f : -1f;
                    f.Use(C_Trunk).Box(new Vector3(sx, GroundY + 4.5f, z), new Vector3(0.2f, 9f, 0.2f));
                    f.Use(C_Trunk).Box(new Vector3(sx + arm * 0.8f, GroundY + 8.9f, z), new Vector3(1.6f, 0.12f, 0.12f));
                    if (dark) continue;
                    lit.VertexColor = Lin(Sodium);
                    lit.AddBox(new Vector3(sx + arm * 1.5f, GroundY + 8.8f, z), new Vector3(0.6f, 0.14f, 0.3f));
                    pools.VertexColor = Lin(new Color(0.26f, 0.17f, 0.08f));
                    GlowQuad(pools, new Vector3(sx + arm * 2.2f, GroundY + 0.05f, z), Vector3.forward, 11f, 9f);
                }
            // Palms on the median and the promenade (dark silhouettes against the lit road).
            for (float z = -200f; z <= 200f; z += 16f) Palm(f, new Vector3(23f, GroundY + 0.2f, z + 3f), 8.5f + (float)rnd.NextDouble() * 2f, rnd);
            for (float z = -190f; z <= 190f; z += 20f) Palm(f, new Vector3(36.6f, GroundY + 0.12f, z), 9f + (float)rnd.NextDouble() * 2.5f, rnd);
            // Promenade lamps along the seawall, their reflections just beyond it.
            for (float z = -180f; z <= 180f; z += 12f)
            {
                f.Use(C_Trunk).Box(new Vector3(38.7f, GroundY + 2.1f, z), new Vector3(0.12f, 4f, 0.12f));
                if (dark && Mathf.Abs(z - 36f) > 1f) continue; // one lamp still burns on the Margin's promenade
                var c = dark ? new Color(0.5f, 0.58f, 0.66f) : new Color(1f, 0.86f, 0.62f);
                lit.VertexColor = Lin(c);
                lit.AddBox(new Vector3(38.7f, GroundY + 4.2f, z), new Vector3(0.45f, 0.45f, 0.45f));
                pools.VertexColor = Lin(Rgb(c, 0.16f));
                GlowQuad(pools, new Vector3(37.4f, GroundY + 0.14f, z), Vector3.forward, 6f, 5f);
                if (!dark)
                {
                    pools.VertexColor = Lin(Rgb(c, 0.22f));
                    GlowQuad(pools, new Vector3(45f, WaterY + 0.04f, z), Vector3.right, 9f, 1.1f);
                }
            }
            // Evening traffic on the Corniche: dark bodies, white headlights and red tail lights, a wash on the road ahead.
            if (!dark)
            {
                foreach (float lane in new[] { 18.2f, 20.3f, 25.7f, 27.8f })
                {
                    float dir = lane < 23f ? 1f : -1f;
                    for (float z = -190f + (float)rnd.NextDouble() * 20f; z < 190f; z += 18f + (float)rnd.NextDouble() * 34f)
                    {
                        var p = new Vector3(lane, GroundY + 0.75f, z);
                        f.Use(rnd.NextDouble() < 0.3 ? C_CarPale : C_Car).Box(p, new Vector3(1.8f, 1.1f, 4.4f));
                        f.Use(C_Car).Box(p + new Vector3(0f, 0.75f, -dir * 0.3f), new Vector3(1.6f, 0.45f, 2.2f));
                        foreach (float sx in new[] { -0.62f, 0.62f })
                        {
                            lit.VertexColor = Lin(new Color(1f, 0.95f, 0.85f));
                            lit.AddBox(p + new Vector3(sx, 0.05f, dir * 2.2f), new Vector3(0.3f, 0.16f, 0.1f));
                            lit.VertexColor = Lin(new Color(0.85f, 0.08f, 0.05f));
                            lit.AddBox(p + new Vector3(sx, 0.12f, -dir * 2.2f), new Vector3(0.3f, 0.14f, 0.1f));
                        }
                        pools.VertexColor = Lin(new Color(0.16f, 0.15f, 0.12f));
                        GlowQuad(pools, new Vector3(lane, GroundY + 0.06f, z + dir * 6.5f), Vector3.forward, 9f, 2.6f);
                    }
                }
            }
            // The marina: low buildings, dock lamps and a few moored boats with their mast lights.
            MarinaBlock(f, lit, new Vector3(103f, GroundY, -56f), 20f, 14f, 16f, rnd, dark);
            MarinaBlock(f, lit, new Vector3(100f, GroundY, 24f), 14f, 14f, 13f, rnd, dark);
            foreach (var island in new[] { new Vector2(63.3f, -45f), new Vector2(63.3f, 40f) })
                for (float dz = -19f; dz <= 19f; dz += 4.75f)
                {
                    f.Use(C_Trunk).Box(new Vector3(island.x, GroundY + 0.5f, island.y + dz), new Vector3(0.1f, 1f, 0.1f));
                    if (dark) continue;
                    lit.VertexColor = Lin(new Color(1f, 0.82f, 0.56f));
                    lit.AddBox(new Vector3(island.x, GroundY + 1.05f, island.y + dz), new Vector3(0.25f, 0.25f, 0.25f));
                    pools.VertexColor = Lin(new Color(0.18f, 0.12f, 0.07f));
                    GlowQuad(pools, new Vector3(island.x - 3.5f, WaterY + 0.04f, island.y + dz), Vector3.right, 6f, 0.9f);
                }
            foreach (var b in new[] { new Vector3(58f, 0f, -55f), new Vector3(57.5f, 0f, -36f), new Vector3(58.5f, 0f, -26f), new Vector3(58f, 0f, 30f), new Vector3(57f, 0f, 48f), new Vector3(58f, 0f, 57f) })
            {
                var hull = new Vector3(b.x, WaterY + 0.55f, b.z);
                f.Use(C_CarPale).Box(hull, new Vector3(2.6f, 1.1f, 9f));
                f.Use(C_CarPale).Box(hull + new Vector3(0f, 0.95f, -0.8f), new Vector3(2f, 0.8f, 3.4f));
                f.Use(C_Trunk).Box(hull + new Vector3(0f, 4.2f, 0.6f), new Vector3(0.08f, 7f, 0.08f));
                if (dark) continue;
                lit.VertexColor = Lin(new Color(0.95f, 0.95f, 0.9f));
                lit.AddBox(hull + new Vector3(0f, 7.8f, 0.6f), new Vector3(0.2f, 0.2f, 0.2f));
                lit.VertexColor = Lin(WarmDim);
                lit.AddBox(hull + new Vector3(-1.01f, 0.95f, -0.8f), new Vector3(0.02f, 0.3f, 2.4f));
            }
            // The two island towers frame the channel.
            Tower(f, lit, Vector3.zero, T(84f, -40f, 26f, 22f, 105f, 6f, 1), rnd, dark, false, reflections);
            Tower(f, lit, Vector3.zero, T(80f, 44f, 24f, 24f, 124f, -4f, 3), rnd, dark, false, reflections);
            // The far bank: a promenade line of lamps and their reflections along the water.
            if (!dark)
                for (float z = -180f; z <= 180f; z += 9f)
                {
                    lit.VertexColor = Lin(new Color(1f, 0.8f, 0.52f));
                    lit.AddBox(new Vector3(146.4f, GroundY + 1.6f, z), new Vector3(0.5f, 0.5f, 0.5f));
                    pools.VertexColor = Lin(new Color(0.2f, 0.13f, 0.07f));
                    GlowQuad(pools, new Vector3(136f, WaterY + 0.04f, z * 0.94f), new Vector3(146f, 0f, z), 16f, 1.4f);
                }

            // ---- far: the skyline layers
            for (int layer = 0; layer < s_Skyline.Length; layer++)
            {
                var root = new GameObject("SkylineLayer" + layer).transform;
                root.SetParent(g, false);
                root.localPosition = new Vector3(90f + layer * 70f, GroundY, 0f);
                SkylineLayers.Add(root);
                var lf = new Flat();
                var ll = new MeshBuilder();
                var rootPos = root.localPosition;
                var specs = s_Skyline[layer];
                for (int i = 0; i < specs.Length; i++)
                    Tower(lf, ll, rootPos, specs[i], rnd, dark, dark && layer == 0 && i == 4, reflections);
                if (layer == 0)
                {
                    // Low blocks along the far shore, in front of the towers' feet.
                    for (float z = -160f; z <= 160f; z += 21f)
                    {
                        float bw = 13f + (float)rnd.NextDouble() * 6f, bd = 10f + (float)rnd.NextDouble() * 5f, bh = 7f + (float)rnd.NextDouble() * 13f;
                        MarinaBlock(lf, ll, new Vector3(148.5f + bd * 0.5f, GroundY, z) - rootPos, bw, bd, bh, rnd, dark, rootPos);
                    }
                }
                Piece("Skyline" + layer, lf.B, city, root);
                if (ll.VertexCount > 0) Piece("SkylineLights" + layer, ll, lights, root);
            }

            // Reflections of the lit towers in the channel: a soft streak where each tower's lit floors mirror, laid along
            // the line of sight (from the apartment, 33.6 m above the water, a higher light reflects nearer).
            if (!dark)
                foreach (var r in reflections)
                {
                    var flat = new Vector3(r.Foot.x, 0f, r.Foot.z);
                    float d = flat.magnitude, h = ViewEye.y - WaterY;
                    var dir = flat / d;
                    float near0 = d * h / (h + r.High), far0 = d * h / (h + r.Low);
                    near0 = Mathf.Max(near0, 42f / Mathf.Max(0.2f, dir.x));
                    far0 = Mathf.Min(far0, 143f / Mathf.Max(0.2f, dir.x));
                    if (far0 < near0 + 3f) continue;
                    float mid = (near0 + far0) * 0.5f;
                    pools.VertexColor = Lin(r.Colour);
                    GlowQuad(pools, dir * mid + Vector3.up * (WaterY + 0.05f), dir, far0 - near0, Mathf.Max(1.5f, r.Width * mid / d * 0.8f));
                }

            Piece("City", f.B, city, near);
            if (lit.VertexCount > 0) Piece("CityLights", lit, lights, near);
            if (pools.VertexCount > 0) Piece("CityGlow", pools, glow, near);
        }

        /// <summary>
        /// One glass tower, built in its layer's space (ground at y = 0, <paramref name="rootPos"/> = the layer's offset): body,
        /// crown, floor bands and a window grid on the faces the apartment sees (dead glass in the Margin copy, except the one
        /// <paramref name="loneWindow"/>), a red beacon on the tallest.
        /// </summary>
        void Tower(Flat f, MeshBuilder lit, Vector3 rootPos, TowerSpec s, System.Random rnd, bool dark, bool loneWindow, List<Reflection> reflections)
        {
            var rot = Quaternion.Euler(0f, s.Yaw, 0f);
            var pos = new Vector3(s.X, GroundY, s.Z) - rootPos;
            int bodyCol = s.Style == 3 || rnd.NextDouble() < 0.3 ? C_GlassBlue : C_Glass;
            float top = s.Style == 1 ? s.H * 0.78f : s.H;
            var faces = new List<(Vector3 c, Vector3 n, float w)>();
            if (s.Style == 3)
            {
                // Round tower with a ring crown.
                const int sides = 14;
                var poly = new List<Vector2>();
                var ring = new List<Vector2>();
                float r = s.W * 0.5f;
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    poly.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                    ring.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r * 1.05f));
                }
                f.Use(bodyCol).Prism(poly, -20f, s.H, pos, rot);
                f.Use(C_Band).Prism(ring, s.H - 3.5f, s.H + 0.8f, pos, rot);
                for (int i = 0; i < sides; i++)
                {
                    var mid = (poly[i] + poly[(i + 1) % sides]) * 0.5f;
                    faces.Add((pos + rot * new Vector3(mid.x, 0f, mid.y), rot * new Vector3(mid.x, 0f, mid.y).normalized, Vector2.Distance(poly[i], poly[(i + 1) % sides])));
                }
                top = s.H - 4f;
            }
            else
            {
                f.Use(bodyCol).Box(pos + Vector3.up * (top - 20f) * 0.5f, new Vector3(s.W, top + 20f, s.D), rot);
                if (s.Style == 1)
                {
                    // Setbacks: two narrower tiers and a mast.
                    f.Use(bodyCol).Box(pos + Vector3.up * (top + (s.H * 0.92f - top) * 0.5f), new Vector3(s.W * 0.78f, s.H * 0.92f - top, s.D * 0.78f), rot);
                    f.Use(C_Band).Box(pos + Vector3.up * (s.H * 0.96f), new Vector3(s.W * 0.5f, s.H * 0.08f, s.D * 0.5f), rot);
                    f.Use(C_Band).Box(pos + Vector3.up * (s.H + 5f), new Vector3(0.6f, 10f, 0.6f), rot);
                }
                else if (s.Style == 2)
                {
                    // A slanted crown: a wedge rising across the width (its underside is the roof).
                    float rise = s.W * 0.45f, hd = s.D * 0.5f;
                    var w0 = pos + Vector3.up * top;
                    Vector2 A = new Vector2(-s.W * 0.5f, 0f), B = new Vector2(s.W * 0.5f, 0f), C = new Vector2(s.W * 0.5f, rise);
                    Vector3 V(Vector2 p, float z) => w0 + rot * new Vector3(p.x, p.y, z);
                    f.Use(bodyCol).Quad(V(B, hd), V(B, -hd), V(C, -hd), V(C, hd));
                    f.Tri(V(A, hd), V(B, hd), V(C, hd));
                    f.Tri(V(A, -hd), V(C, -hd), V(B, -hd));
                    f.Use(C_Band).Quad(V(A, hd), V(C, hd), V(C, -hd), V(A, -hd));
                }
                else
                {
                    // Flat roof, plant room, a mast on the tall ones.
                    f.Use(C_Band).Box(pos + Vector3.up * (top + 2f), new Vector3(s.W * 0.6f, 4f, s.D * 0.55f), rot);
                    if (s.H > 90f) f.Use(C_Band).Box(pos + Vector3.up * (top + 9f), new Vector3(0.5f, 14f, 0.5f), rot);
                }
                faces.Add((pos + rot * new Vector3(s.W * 0.5f, 0f, 0f), rot * Vector3.right, s.D));
                faces.Add((pos + rot * new Vector3(-s.W * 0.5f, 0f, 0f), rot * Vector3.left, s.D));
                faces.Add((pos + rot * new Vector3(0f, 0f, s.D * 0.5f), rot * Vector3.forward, s.W));
                faces.Add((pos + rot * new Vector3(0f, 0f, -s.D * 0.5f), rot * Vector3.back, s.W));
            }

            // Windows on the faces turned toward the apartment.
            var eye = ViewEye - rootPos;
            bool office = s.Style != 3 && rnd.NextDouble() < 0.4;
            bool crown = !dark && rnd.NextDouble() < 0.4;
            int floors = Mathf.FloorToInt((top - 6f) / 3.4f);
            int lone = loneWindow ? rnd.Next(Mathf.Max(1, floors / 3), Mathf.Max(2, floors * 2 / 3)) : -1;
            bool loneDone = false;
            float litSum = 0f; int litCount = 0;
            foreach (var (fc, n, fw) in faces)
            {
                var toEye = eye - fc; toEye.y = 0f;
                if (Vector3.Dot(n, toEye.normalized) < 0.08f) continue;
                int cols = Mathf.Max(1, Mathf.FloorToInt(fw / 2.5f));
                float pitch = fw / cols, ww = Mathf.Min(1.6f, pitch * 0.68f);
                var right = Vector3.Cross(Vector3.up, -n).normalized;
                float dist = (fc + rootPos - ViewEye).magnitude;
                float haze = 1f - Mathf.Exp(-Mathf.Pow(dist * 0.0024f, 2f));
                for (int fl = 0; fl < floors; fl++)
                {
                    float y = 3.2f + fl * 3.4f;
                    f.Use(C_Band).Face(fc + n * 0.06f + Vector3.up * y, n, fw, 0.34f);
                    double mode = rnd.NextDouble();
                    bool all = mode < 0.1, none = mode >= 0.1 && mode < 0.3;
                    float p = all ? 0.95f : none ? 0.04f : office ? 0.3f : 0.42f;
                    for (int c = 0; c < cols; c++)
                    {
                        var wc = fc + n * 0.1f + right * (-fw * 0.5f + (c + 0.5f) * pitch) + Vector3.up * (y + 1.55f);
                        Color col;
                        if (dark)
                        {
                            bool here = !loneDone && fl == lone && c == cols / 2;
                            if (here) { col = new Color(1f, 0.76f, 0.48f); loneDone = true; }
                            else if (rnd.NextDouble() < 0.45) col = Color.Lerp(Dead, DeadHaze, haze * 0.7f);
                            else continue;
                        }
                        else
                        {
                            if (rnd.NextDouble() >= p) continue;
                            double k = rnd.NextDouble();
                            col = office ? Cool : k < 0.72 ? Warm : k < 0.9 ? WarmDim : Screen;
                            col *= 0.72f + (float)rnd.NextDouble() * 0.36f;
                            col = Color.Lerp(col, Haze, haze);
                            litSum += col.grayscale; litCount++;
                        }
                        lit.VertexColor = Lin(col);
                        FaceQuad(lit, wc, n, ww, 1.9f);
                    }
                }
                if (crown)
                {
                    lit.VertexColor = Lin(Color.Lerp(new Color(0.7f, 0.82f, 1f), Haze, haze));
                    FaceQuad(lit, fc + n * 0.12f + Vector3.up * (top - 1.4f), n, fw * 0.92f, 0.55f);
                }
            }
            if (!dark && s.H > 95f)
            {
                // A red aviation light on the highest point: the mast, the crown ring, the slant's high corner.
                var beacon = s.Style == 0 ? pos + Vector3.up * (s.H + 16.5f) : s.Style == 1 ? pos + Vector3.up * (s.H + 10.5f)
                           : s.Style == 3 ? pos + Vector3.up * (s.H + 1.3f) : pos + rot * new Vector3(s.W * 0.5f - 0.5f, s.H + s.W * 0.45f + 0.5f, 0f);
                lit.VertexColor = Lin(Beacon);
                lit.AddBox(beacon, new Vector3(0.9f, 0.9f, 0.9f));
            }
            if (litCount > 0)
                reflections.Add(new Reflection
                {
                    Foot = pos + rootPos, Width = s.W, Low = 6f, High = top * 0.85f + 2f,
                    Colour = Rgb(office ? Cool : Warm, Mathf.Clamp(0.12f + litSum / litCount * 0.25f, 0.12f, 0.3f))
                });
        }

        /// <summary>A low block (marina, far bank): a flat-roofed box with a few rows of windows on its west face.</summary>
        static void MarinaBlock(Flat f, MeshBuilder lit, Vector3 basePos, float w, float d, float h, System.Random rnd, bool dark, Vector3 rootPos = default)
        {
            f.Use(rnd.NextDouble() < 0.5 ? C_Sand : C_Stone).Box(basePos + Vector3.up * (h - 4f) * 0.5f, new Vector3(d, h + 4f, w));
            f.Use(C_Band).Box(basePos + Vector3.up * (h + 0.4f), new Vector3(d + 0.4f, 0.8f, w + 0.4f));
            if (dark) return;
            var face = basePos + new Vector3(-d * 0.5f - 0.08f, 0f, 0f);
            float haze = 1f - Mathf.Exp(-Mathf.Pow((face + rootPos - ViewEye).magnitude * 0.0024f, 2f));
            for (float y = 2.2f; y < h - 1.5f; y += 3.3f)
                for (float z = -w * 0.5f + 1.4f; z < w * 0.5f - 1f; z += 2.6f)
                {
                    if (rnd.NextDouble() > 0.35) continue;
                    lit.VertexColor = Lin(Color.Lerp(Warm * (0.7f + (float)rnd.NextDouble() * 0.3f), Haze, haze));
                    FaceQuad(lit, face + new Vector3(0f, y, z), Vector3.left, 1.4f, 1.6f);
                }
        }

        /// <summary>A Corniche palm: a slim trunk and a crown of drooping fronds (double-sided kites), silhouette only.</summary>
        static void Palm(Flat f, Vector3 foot, float height, System.Random rnd)
        {
            var top = foot + new Vector3((float)(rnd.NextDouble() - 0.5) * 0.6f, height, (float)(rnd.NextDouble() - 0.5) * 0.6f);
            var mid = (foot + top) * 0.5f;
            f.Use(C_Trunk).Box(mid, new Vector3(0.32f, height, 0.32f), Quaternion.FromToRotation(Vector3.up, top - foot));
            f.Use(C_Frond);
            for (int i = 0; i < 9; i++)
            {
                float a = (i / 9f + (float)rnd.NextDouble() * 0.05f) * Mathf.PI * 2f;
                var dirH = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float len = 2.6f + (float)rnd.NextDouble() * 0.8f;
                var midF = top + dirH * len * 0.55f + Vector3.up * 0.25f;
                var tip = top + dirH * len + Vector3.down * (0.9f + (float)rnd.NextDouble() * 0.6f);
                var side = Vector3.Cross(Vector3.up, dirH) * 0.42f;
                f.Quad(top, midF + side, tip, midF - side);
                f.Quad(top, midF - side, tip, midF + side);
            }
        }
    }
}

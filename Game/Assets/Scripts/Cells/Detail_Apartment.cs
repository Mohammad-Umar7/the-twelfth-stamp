using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    // Place detail for the Abu Dhabi apartment (playtest: "the places look cheap and bare"). Set dressing only, batched per
    // material, no colliders, and nothing on the puzzle spots: the bag and the floor beside it, the table spots, the fridge
    // notes, the three seams, the window glass the reflection lives in, the bathroom doorway. The Margin copy builds the
    // same room and gets the details wrong: blank art and book spines, a frame turned to the wall, things counted twice.
    public partial class ApartmentCell
    {
        /// <summary>A wall face for set dressing: u runs along the wall (to the right when facing it), v up, w out into the room.</summary>
        readonly struct Face
        {
            public readonly Vector3 Origin;
            public readonly Quaternion Rot;
            public Face(Vector3 origin, float yaw) { Origin = origin; Rot = Quaternion.Euler(0f, yaw, 0f); }
            public Vector3 P(float u, float v, float w) => Origin + Rot * new Vector3(u, v, -w);
            public Vector3 Out => Rot * Vector3.back;
        }

        static readonly Face WestWall = new Face(new Vector3(-4.5f, 0f, 0f), -90f);  // u = +z
        static readonly Face NorthWall = new Face(new Vector3(0f, 0f, 4.0f), 0f);    // u = +x
        static readonly Face SouthWall = new Face(new Vector3(0f, 0f, -4.0f), 180f); // u = -x

        /// <summary>The room's dressing materials (the Margin copy drains them; its cloth and paint are one blank grey).</summary>
        sealed class HomeMats
        {
            public Material Walnut, Oak, Trim, Brass, Ceramic, Charcoal, Soil, Marble, Leaf, Shade, Led, Screen, Concrete;
            public Material[] Paints; // linen-textured cloth and paint: 0 madder, 1 navy, 2 ochre, 3 cream, 4 teal, 5 terracotta
            public Material Wall, Wood, Fabric, Metal, Black, Rug, Tile;
            public Material Paint(int i) => Paints[i % Paints.Length];
        }

        static readonly Vector2[] s_LeafShape =
        {
            new Vector2(0f, 0f), new Vector2(0.32f, 0.12f), new Vector2(0.5f, 0.4f), new Vector2(0.42f, 0.72f), new Vector2(0.2f, 0.92f),
            new Vector2(0f, 1f), new Vector2(-0.2f, 0.92f), new Vector2(-0.42f, 0.72f), new Vector2(-0.5f, 0.4f), new Vector2(-0.32f, 0.12f),
        };

        // ------------------------------------------------------------------ small builders
        /// <summary>A box on a wall: centre (u, v, w), size along (u, v, w), turned <paramref name="roll"/> degrees in the wall plane.</summary>
        static void WBox(KitBatch k, Material m, Face f, float u, float v, float w, float su, float sv, float sw, float roll = 0f)
            => k.Box(m, f.P(u, v, w), new Vector3(su, sv, sw), f.Rot * Quaternion.Euler(0f, 0f, roll));

        /// <summary>A box on a wall turned about the vertical (books lying on a shelf, things set down not quite square).</summary>
        static void WBoxY(KitBatch k, Material m, Face f, float u, float v, float w, float su, float sv, float sw, float yaw)
            => k.Box(m, f.P(u, v, w), new Vector3(su, sv, sw), f.Rot * Quaternion.Euler(0f, yaw, 0f));

        /// <summary>A part of a piece hung at <paramref name="roll"/> degrees: its offset (du, dv) from the piece's centre turns with it.</summary>
        static void WBoxR(KitBatch k, Material m, Face f, float cu, float cv, float roll, float du, float dv, float w, float su, float sv, float sw)
        {
            float a = roll * Mathf.Deg2Rad, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            WBox(k, m, f, cu + du * cs - dv * sn, cv + du * sn + dv * cs, w, su, sv, sw, roll);
        }

        static void WDiscR(KitBatch k, Material m, Face f, float cu, float cv, float roll, float du, float dv, float w, float r)
        {
            float a = roll * Mathf.Deg2Rad, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            k.Of(m).AddDisc(f.P(cu + du * cs - dv * sn, cv + du * sn + dv * cs, w), f.Out, r, 28);
        }

        /// <summary>A printed picture (0..1 UVs) facing out of a wall.</summary>
        static void WPrint(KitBatch k, Material m, Face f, float u, float v, float w, float su, float sv)
        {
            var c = f.P(u, v, w);
            var r = f.Rot * Vector3.right * (su * 0.5f);
            var up = Vector3.up * (sv * 0.5f);
            k.Of(m).AddQuad(c + r - up, c - r - up, c - r + up, c + r + up, new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1));
        }

        /// <summary>A picture frame: four rails, <paramref name="border"/> wide, hung at <paramref name="roll"/> degrees.</summary>
        static void Frame(KitBatch k, Material m, Face f, float cu, float cv, float w, float h, float roll, float border = 0.022f, float depth = 0.025f)
        {
            WBoxR(k, m, f, cu, cv, roll, 0f, (h - border) * 0.5f, depth * 0.5f, w, border, depth);
            WBoxR(k, m, f, cu, cv, roll, 0f, -(h - border) * 0.5f, depth * 0.5f, w, border, depth);
            WBoxR(k, m, f, cu, cv, roll, -(w - border) * 0.5f, 0f, depth * 0.5f, border, h - border * 2f, depth);
            WBoxR(k, m, f, cu, cv, roll, (w - border) * 0.5f, 0f, depth * 0.5f, border, h - border * 2f, depth);
        }

        /// <summary>A surface of revolution from (radius, height) pairs.</summary>
        static void Lathe(KitBatch k, Material m, Vector3 foot, int segments, params float[] rh)
        {
            var prof = new List<Vector2>(rh.Length / 2);
            for (int i = 0; i + 1 < rh.Length; i += 2) prof.Add(new Vector2(rh[i], rh[i + 1]));
            k.Of(m).AddLathe(foot, prof, segments);
        }

        /// <summary>An open lampshade with an inside (seen from below).</summary>
        static void Shade(KitBatch k, Material m, Vector3 foot, float rBottom, float rTop, float height)
            => Lathe(k, m, foot, 24, rBottom, 0f, rTop, height, rTop - 0.006f, height, rBottom - 0.006f, 0f);

        /// <summary>A flat leaf (pointed oval, a few millimetres thick) from <paramref name="foot"/> along <paramref name="axis"/>.</summary>
        static void Leaf(MeshBuilder b, Vector3 foot, Vector3 axis, Vector3 normal, float length, float width)
        {
            var poly = new Vector2[s_LeafShape.Length];
            for (int i = 0; i < poly.Length; i++) poly[i] = new Vector2(s_LeafShape[i].x * width, s_LeafShape[i].y * length);
            if (Mathf.Abs(Vector3.Dot(axis.normalized, normal.normalized)) > 0.98f) normal = Vector3.Cross(axis, Vector3.right);
            b.AddExtrudedXY(poly, 0.004f, foot, Quaternion.LookRotation(normal, axis));
        }

        /// <summary>A small double-sided kite leaf (vines, olive, succulents).</summary>
        static void Kite(MeshBuilder b, Vector3 foot, Vector3 axis, float length, float width)
        {
            axis.Normalize();
            var side = Vector3.Cross(axis, Vector3.up);
            if (side.sqrMagnitude < 0.01f) side = Vector3.Cross(axis, Vector3.right);
            side = side.normalized * (width * 0.5f);
            var tip = foot + axis * length;
            var mid = foot + axis * (length * 0.45f);
            b.AddQuad(foot, mid + side, tip, mid - side, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            b.AddQuad(foot, mid - side, tip, mid + side, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
        }

        /// <summary>A plant pot with its soil: a tapered lathe with a lip.</summary>
        static void Pot(KitBatch k, Material pot, Material soil, Vector3 foot, float radius, float height)
        {
            Lathe(k, pot, foot, 18, 0f, 0f, radius * 0.78f, 0f, radius * 0.95f, height * 0.85f, radius, height * 0.88f, radius, height,
                radius * 0.9f, height, radius * 0.86f, height * 0.9f);
            k.Of(soil).AddDisc(foot + Vector3.up * height * 0.9f, Vector3.up, radius * 0.87f, 16);
        }

        /// <summary>A row of books standing on a shelf from u0 to u1 (backs against the wall), with gaps and lying stacks.</summary>
        static void Books(KitBatch k, Face f, float u0, float u1, float v, System.Random rnd, Material[] spines, float wBack = 0.02f)
        {
            float u = u0 + 0.01f;
            while (u < u1 - 0.03f)
            {
                double roll = rnd.NextDouble();
                if (roll < 0.07) { u += 0.04f + (float)rnd.NextDouble() * 0.07f; continue; }
                if (roll < 0.14 && u1 - u > 0.32f)
                {
                    float y = v;
                    int n = 3 + rnd.Next(3);
                    for (int i = 0; i < n; i++)
                    {
                        float t = 0.025f + (float)rnd.NextDouble() * 0.02f, l = 0.22f + (float)rnd.NextDouble() * 0.05f;
                        WBoxY(k, spines[rnd.Next(spines.Length)], f, u + 0.15f, y + t * 0.5f, wBack + 0.1f, l, t, 0.17f + (float)rnd.NextDouble() * 0.04f, (float)(rnd.NextDouble() - 0.5) * 8f);
                        y += t;
                    }
                    u += 0.31f;
                    continue;
                }
                float th = 0.018f + (float)rnd.NextDouble() * 0.034f, h = 0.17f + (float)rnd.NextDouble() * 0.14f, d = 0.14f + (float)rnd.NextDouble() * 0.08f;
                if (u + th > u1 - 0.01f) break;
                WBox(k, spines[rnd.Next(spines.Length)], f, u + th * 0.5f, v + h * 0.5f, wBack + d * 0.5f, th, h, d);
                u += th + 0.0015f;
            }
        }

        // ------------------------------------------------------------------ the room
        /// <summary>
        /// Dresses the apartment: a feature wall (credenza under a gallery of pictures, a built-in bookcase), a slatted media
        /// wall with the TV, the kitchen's backsplash and counter clutter, the living room (a coffee table set with a dallah,
        /// cups and dates, a floor lamp, cushions, a pouf, a rug under the dining table), the entry, curtains and window
        /// board, the ceiling (downlights, slot diffusers, cornice), skirting and casings, and the balcony outside.
        /// </summary>
        void BuildHomeDetail(Transform g, Material wall, Material wood, Material fabric, Material metal, Material black, Material rug, Material tile)
        {
            bool m = Corrupted;
            var k = new KitBatch();
            var rnd = new System.Random(m ? 404 : 2024);
            var h = new HomeMats
            {
                Walnut = Mats.Lit(m ? new Color(0.3f, 0.26f, 0.22f) : new Color(0.42f, 0.28f, 0.18f), 0.45f, 0f, "wood_dark"),
                Oak = Mats.Lit(m ? new Color(0.55f, 0.51f, 0.46f) : new Color(0.74f, 0.57f, 0.4f), 0.4f, 0f, "wood_oak_floor"),
                Trim = Mats.Lit(m ? new Color(0.8f, 0.81f, 0.81f) : new Color(0.9f, 0.88f, 0.84f), 0.35f),
                Brass = Mats.Lit(m ? new Color(0.6f, 0.58f, 0.53f) : new Color(0.8f, 0.62f, 0.33f), 0.65f, 0.9f, "metal_brushed"),
                Ceramic = Mats.Lit(m ? new Color(0.8f, 0.82f, 0.83f) : new Color(0.92f, 0.89f, 0.83f), 0.75f),
                Charcoal = Mats.Lit(new Color(0.1f, 0.1f, 0.105f), 0.4f, 0.3f),
                Soil = Mats.Lit(new Color(0.11f, 0.08f, 0.06f), 0.2f),
                Marble = Mats.Lit(m ? new Color(0.82f, 0.84f, 0.85f) : new Color(0.93f, 0.91f, 0.87f), 0.6f, 0f, "marble_white"),
                Leaf = Mats.Lit(m ? new Color(0.19f, 0.23f, 0.2f) : new Color(0.2f, 0.34f, 0.15f), 0.35f),
                // Lamp shades lit from inside; the LED (downlights, strips, displays). Dark albedo, so the practical lights
                // right beside them cannot blow them out: the emission is the look. The Margin copy's are drained and cold.
                Shade = m ? Mats.Emissive(new Color(0.3f, 0.31f, 0.32f), new Color(0.2f, 0.24f, 0.28f)) : Mats.Emissive(new Color(0.07f, 0.06f, 0.05f), new Color(0.95f, 0.66f, 0.36f)),
                Led = m ? Mats.Emissive(new Color(0.08f, 0.09f, 0.1f), new Color(0.34f, 0.42f, 0.5f)) : Mats.Emissive(new Color(0.08f, 0.075f, 0.07f), new Color(1.05f, 0.82f, 0.5f)),
                // The TV is off at home; in the copy it is on, showing nothing.
                Screen = m ? Mats.Emissive(new Color(0.1f, 0.12f, 0.15f), new Color(0.16f, 0.21f, 0.27f)) : Mats.Lit(new Color(0.02f, 0.02f, 0.025f), 0.95f, 0.3f),
                Concrete = Mats.Lit(m ? new Color(0.5f, 0.52f, 0.54f) : new Color(0.66f, 0.64f, 0.6f), 0.25f, 0f, "concrete_panels"),
                Paints = m ? new[] { Mats.Lit(new Color(0.6f, 0.61f, 0.61f), 0.2f, 0f, "fabric_linen") } : new[]
                {
                    Mats.Lit(new Color(0.5f, 0.13f, 0.1f), 0.2f, 0f, "fabric_linen"), Mats.Lit(new Color(0.13f, 0.17f, 0.3f), 0.2f, 0f, "fabric_linen"),
                    Mats.Lit(new Color(0.76f, 0.55f, 0.24f), 0.2f, 0f, "fabric_linen"), Mats.Lit(new Color(0.88f, 0.83f, 0.72f), 0.2f, 0f, "fabric_linen"),
                    Mats.Lit(new Color(0.13f, 0.37f, 0.38f), 0.2f, 0f, "fabric_linen"), Mats.Lit(new Color(0.7f, 0.37f, 0.25f), 0.2f, 0f, "fabric_linen"),
                },
                Wall = wall, Wood = wood, Fabric = fabric, Metal = metal, Black = black, Rug = rug, Tile = tile,
            };

            FeatureWall(k, h, rnd);
            MediaWall(k, h, rnd);
            KitchenDetail(k, h);
            LivingDetail(k, h, rnd);
            EntryAndSouthWall(k, h, rnd);
            WindowDetail(k, h, rnd);
            CeilingAndTrim(k, h);
            Balcony(g, k, h, rnd);
            k.Bake(g, "HomeDetail");

            // Practical lights, each kept clear of the surfaces nearest it (point lights fall off as 1/d^2): the credenza
            // lamp's wash on the gallery, the media wall (washed from the downlight above it at home; the copy's TV is on
            // and throws a cold glow) and, at home only, the balcony sconce.
            AddPointLight(g, WestWall.P(1.15f, 1.45f, 0.75f), m ? new Color(0.72f, 0.8f, 0.9f) : new Color(1f, 0.76f, 0.5f), m ? 0.55f : 0.9f, 3.2f);
            if (m) AddPointLight(g, NorthWall.P(2.9f, 1.35f, 0.55f), new Color(0.62f, 0.72f, 0.88f), 0.45f, 2.6f);
            else
            {
                AddPointLight(g, NorthWall.P(2.9f, 2.55f, 0.75f), new Color(1f, 0.76f, 0.5f), 0.75f, 3.2f);
                AddPointLight(g, new Vector3(5.3f, 2.1f, 3.1f), new Color(1f, 0.78f, 0.52f), 0.8f, 4.5f);
            }
        }

        /// <summary>The west wall that used to be a blank orange expanse: credenza, gallery, bookcase (u = z).</summary>
        void FeatureWall(KitBatch k, HomeMats h, System.Random rnd)
        {
            var W = WestWall;
            bool m = Corrupted;
            // Credenza (z -0.85..1.55): walnut, a fluted front of four doors with brass pulls, slim black legs.
            const float cu = 0.35f, cl = 2.4f;
            WBox(k, h.Walnut, W, cu, 0.43f, 0.225f, cl, 0.58f, 0.43f);
            WBox(k, h.Walnut, W, cu, 0.735f, 0.235f, cl + 0.02f, 0.03f, 0.47f);
            foreach (float lu in new[] { cu - cl * 0.5f + 0.08f, cu + cl * 0.5f - 0.08f })
                foreach (float lw in new[] { 0.06f, 0.39f })
                    WBox(k, h.Black, W, lu, 0.07f, lw, 0.035f, 0.14f, 0.035f);
            for (int d = 0; d < 4; d++)
            {
                float du = cu - cl * 0.5f + (d + 0.5f) * cl / 4f;
                if (d > 0) WBox(k, h.Black, W, du - cl / 8f, 0.43f, 0.446f, 0.008f, 0.54f, 0.006f);
                for (float ru = du - 0.27f; ru <= du + 0.271f; ru += 0.03f) WBox(k, h.Walnut, W, ru, 0.43f, 0.447f, 0.014f, 0.52f, 0.012f);
                WBox(k, h.Brass, W, du + (d % 2 == 0 ? 0.25f : -0.25f), 0.5f, 0.462f, 0.012f, 0.16f, 0.02f);
            }
            // On it: a lamp with a linen shade (lit), dried pampas in a black vase, books under a brass ball, the old photo.
            var lampFoot = W.P(1.2f, 0.75f, 0.24f);
            Lathe(k, h.Ceramic, lampFoot, 18, 0f, 0f, 0.06f, 0f, 0.085f, 0.05f, 0.095f, 0.12f, 0.075f, 0.2f, 0.035f, 0.25f, 0.016f, 0.29f, 0.012f, 0.36f);
            Shade(k, h.Shade, lampFoot + Vector3.up * 0.33f, 0.17f, 0.12f, 0.22f);
            var vase = W.P(-0.45f, 0.75f, 0.2f);
            Lathe(k, h.Charcoal, vase, 16, 0f, 0f, 0.06f, 0f, 0.085f, 0.08f, 0.08f, 0.2f, 0.045f, 0.32f, 0.04f, 0.38f, 0.046f, 0.4f, 0.04f, 0.4f, 0.034f, 0.36f);
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f + 0.4f;
                var lean = new Vector3(Mathf.Cos(a) * 0.24f, 1f, Mathf.Sin(a) * 0.24f).normalized;
                var stemTop = vase + Vector3.up * 0.36f + lean * (0.42f + (float)rnd.NextDouble() * 0.2f);
                k.Of(h.Oak).AddTube(new List<Vector3> { vase + Vector3.up * 0.3f, stemTop }, 0.004f, 4, false);
                k.Of(h.Paint(3)).AddFrustum(stemTop - lean * 0.02f, 0.03f, 0.004f, 0.24f, 6, true, true, 1f, Quaternion.FromToRotation(Vector3.up, lean));
            }
            float y = 0.75f;
            int[] cover = { 1, 3, 0 };
            for (int i = 0; i < 3; i++)
            {
                float t = 0.035f + i * 0.006f;
                WBoxY(k, h.Paint(cover[i]), W, 0.18f + (i - 1) * 0.012f, y + t * 0.5f, 0.21f, 0.3f - i * 0.03f, t, 0.23f - i * 0.02f, (i - 1) * 6f);
                y += t;
            }
            k.Of(h.Brass).AddSphere(W.P(0.2f, y + 0.045f, 0.21f), 0.045f, 14, 10);
            // The photo from the old shelf, propped on the credenza (blank in the Margin copy).
            WBox(k, h.Black, W, 0.72f, 0.865f, 0.03f, 0.3f, 0.23f, 0.02f);
            WBox(k, h.Trim, W, 0.72f, 0.865f, 0.0405f, 0.26f, 0.19f, 0.002f);
            var photo = Mats.Printed(m ? null : Tex.Art("postcard_1"), m ? new Color(0.85f, 0.85f, 0.83f) : Color.white, 0.2f);
            WPrint(k, photo, W, 0.72f, 0.865f, 0.0418f, 0.21f, 0.13f);

            // Gallery over the credenza. The copy: the canvas and the prints blank, one frame turned to the wall, one askew.
            WBox(k, m ? h.Paint(0) : h.Paint(3), W, 0.35f, 1.62f, 0.0175f, 0.9f, 0.66f, 0.035f);
            if (!m)
            {
                // Dunes under a low sun (the copy left it white).
                WBox(k, h.Paint(2), W, 0.35f, 1.37f, 0.036f, 0.9f, 0.16f, 0.004f);
                WBox(k, h.Paint(5), W, 0.27f, 1.47f, 0.0375f, 0.72f, 0.06f, 0.004f);
                WBox(k, h.Paint(5), W, 0.15f, 1.51f, 0.0378f, 0.42f, 0.04f, 0.004f);
                WBox(k, h.Paint(0), W, 0.53f, 1.52f, 0.039f, 0.3f, 0.05f, 0.004f);
                WBox(k, h.Paint(1), W, 0.35f, 1.585f, 0.036f, 0.9f, 0.006f, 0.003f);
                k.Of(h.Paint(5)).AddDisc(W.P(0.55f, 1.76f, 0.0362f), W.Out, 0.13f, 32);
                k.Of(h.Paint(2)).AddDisc(W.P(0.55f, 1.76f, 0.037f), W.Out, 0.085f, 32);
            }
            // Night Paris in a black frame (the copy kept only the mount).
            Frame(k, h.Black, W, -0.6f, 1.92f, 0.44f, 0.32f, 0f);
            WBox(k, h.Trim, W, -0.6f, 1.92f, 0.012f, 0.4f, 0.28f, 0.004f);
            if (!m) WPrint(k, Mats.Printed(Tex.Art("postcard_2"), Color.white, 0.2f), W, -0.6f, 1.92f, 0.0145f, 0.29f, 0.18f);
            if (!m)
            {
                // Three arches on navy in walnut.
                Frame(k, h.Walnut, W, -0.55f, 1.36f, 0.3f, 0.4f, 0f);
                WBox(k, h.Paint(1), W, -0.55f, 1.36f, 0.012f, 0.26f, 0.36f, 0.004f);
                int[] ac = { 2, 3, 5 };
                for (int i = 0; i < 3; i++)
                {
                    float au = -0.55f + (i - 1) * 0.075f;
                    WBox(k, h.Paint(ac[i]), W, au, 1.31f, 0.0145f, 0.05f, 0.14f, 0.003f);
                    k.Of(h.Paint(ac[i])).AddArch(W.P(au, 1.38f, 0.0145f), 0.001f, 0.025f, 0.003f, 10, W.Rot);
                }
            }
            else
            {
                // Hung the wrong way round: the kraft backing, a hanging wire, slightly askew.
                WBox(k, h.Oak, W, -0.55f, 1.36f, 0.0125f, 0.3f, 0.4f, 0.025f, 3f);
                WBoxR(k, h.Charcoal, W, -0.55f, 1.36f, 3f, -0.065f, 0.115f, 0.0262f, 0.16f, 0.003f, 0.002f);
                WBoxR(k, h.Charcoal, W, -0.55f, 1.36f, 3f, 0.065f, 0.115f, 0.0262f, 0.16f, 0.003f, 0.002f);
            }
            // Three circles in oak (askew and colourless in the copy).
            float tilt = m ? 7f : 0f;
            Frame(k, h.Oak, W, 1.28f, 2.0f, 0.36f, 0.36f, tilt);
            WBoxR(k, h.Paint(3), W, 1.28f, 2.0f, tilt, 0f, 0f, 0.012f, 0.32f, 0.32f, 0.004f);
            WDiscR(k, h.Paint(4), W, 1.28f, 2.0f, tilt, -0.045f, 0.03f, 0.0146f, 0.07f);
            WDiscR(k, h.Paint(2), W, 1.28f, 2.0f, tilt, 0.045f, 0.03f, 0.0152f, 0.07f);
            WDiscR(k, h.Paint(0), W, 1.28f, 2.0f, tilt, 0f, -0.045f, 0.0158f, 0.07f);
            // A line map of a coast with a red dot for home (blank paper in the copy).
            const float mu = 1.24f, mv = 1.45f;
            Frame(k, h.Black, W, mu, mv, 0.44f, 0.32f, 0f);
            WBox(k, h.Paint(3), W, mu, mv, 0.012f, 0.4f, 0.28f, 0.004f);
            if (!m)
            {
                WBox(k, h.Paint(4), W, mu + 0.13f, mv + 0.02f, 0.0145f, 0.13f, 0.22f, 0.002f);
                WBox(k, h.Paint(4), W, mu + 0.04f, mv + 0.1f, 0.0145f, 0.08f, 0.06f, 0.002f);
                for (int i = 0; i < 5; i++) WBox(k, h.Charcoal, W, mu - 0.17f + i * 0.045f, mv - 0.03f, 0.015f, 0.0025f, 0.17f, 0.001f);
                for (int i = 0; i < 4; i++) WBox(k, h.Charcoal, W, mu - 0.075f, mv - 0.105f + i * 0.045f, 0.015f, 0.2f, 0.0025f, 0.001f);
                WBox(k, h.Paint(0), W, mu - 0.04f, mv - 0.06f, 0.0156f, 0.014f, 0.014f, 0.001f);
            }

            // Built-in bookcase (z 1.65..3.85).
            const float b0 = 1.65f, b1 = 3.85f, bd = 0.35f, bh = 2.3f;
            float bc = (b0 + b1) * 0.5f, bw = b1 - b0;
            WBox(k, h.Walnut, W, b0 + 0.015f, bh * 0.5f, bd * 0.5f, 0.03f, bh, bd);
            WBox(k, h.Walnut, W, b1 - 0.015f, bh * 0.5f, bd * 0.5f, 0.03f, bh, bd);
            WBox(k, h.Walnut, W, bc, bh - 0.015f, bd * 0.5f, bw, 0.03f, bd);
            WBox(k, h.Charcoal, W, bc, bh * 0.5f, 0.006f, bw - 0.06f, bh - 0.03f, 0.012f);
            WBox(k, h.Walnut, W, bc, 0.04f, bd - 0.03f, bw - 0.06f, 0.08f, 0.02f);
            WBox(k, h.Walnut, W, bc, bh * 0.5f + 0.045f, bd * 0.5f, 0.025f, bh - 0.12f, bd - 0.02f);
            float[] shelf = { 0.1f, 0.52f, 0.94f, 1.36f, 1.78f };
            foreach (float sy in shelf) WBox(k, h.Walnut, W, bc, sy - 0.0125f, bd * 0.5f, bw - 0.06f, 0.025f, bd - 0.01f);
            var spines = m ? new[] { h.Paint(0) } : new[] { h.Paint(0), h.Paint(1), h.Paint(2), h.Paint(3), h.Paint(4), h.Paint(5), h.Charcoal };
            for (int r = 0; r < shelf.Length; r++)
                for (int bay = 0; bay < 2; bay++)
                {
                    float u0 = bay == 0 ? b0 + 0.03f : bc + 0.0125f, u1 = bay == 0 ? bc - 0.0125f : b1 - 0.03f, sv = shelf[r];
                    int cell = r * 2 + bay;
                    // The copy filled every bay with the same row of blank books.
                    var br = new System.Random(m ? 77 : 100 + cell * 13);
                    if (cell == 9)
                    {
                        // Top right: a trailing plant spilling over the shelf, a few books beside it.
                        Books(k, W, u0 + 0.38f, u1, sv, br, spines);
                        Trailing(k, h, W.P(u0 + 0.2f, sv, 0.24f), W.Out, rnd);
                        continue;
                    }
                    if (m) { Books(k, W, u0, u1, sv, br, spines); continue; }
                    switch (cell)
                    {
                        case 1:
                            // Two woven baskets.
                            for (int i = 0; i < 2; i++)
                            {
                                float basket = u0 + 0.27f + i * 0.5f;
                                WBox(k, h.Paint(2 + i), W, basket, sv + 0.13f, 0.17f, 0.44f, 0.26f, 0.3f);
                                WBox(k, h.Walnut, W, basket, sv + 0.255f, 0.17f, 0.45f, 0.02f, 0.31f);
                            }
                            break;
                        case 3:
                            Books(k, W, u0, u0 + 0.6f, sv, br, spines);
                            Lathe(k, h.Ceramic, W.P(u1 - 0.22f, sv, 0.17f), 18, 0f, 0f, 0.05f, 0f, 0.09f, 0.08f, 0.085f, 0.17f, 0.04f, 0.25f, 0.032f, 0.3f, 0.036f, 0.31f);
                            break;
                        case 4:
                            Books(k, W, u0 + 0.36f, u1, sv, br, spines);
                            Lathe(k, h.Brass, W.P(u0 + 0.17f, sv, 0.17f), 20, 0f, 0f, 0.05f, 0f, 0.1f, 0.04f, 0.12f, 0.07f, 0.115f, 0.072f, 0.095f, 0.045f, 0.045f, 0.012f, 0f, 0.012f);
                            break;
                        case 6:
                            Books(k, W, u0, u1 - 0.3f, sv, br, spines);
                            k.Of(h.Ceramic).AddSphere(W.P(u1 - 0.15f, sv + 0.08f, 0.17f), 0.08f, 16, 12);
                            break;
                        default:
                            Books(k, W, u0, u1, sv, br, spines);
                            break;
                    }
                }
        }

        /// <summary>A pothos in a small pot, its vines spilling forward over the shelf edge and down.</summary>
        static void Trailing(KitBatch k, HomeMats h, Vector3 foot, Vector3 outDir, System.Random rnd)
        {
            Pot(k, h.Ceramic, h.Soil, foot, 0.075f, 0.12f);
            var leaves = k.Of(h.Leaf);
            var side = Vector3.Cross(Vector3.up, outDir).normalized;
            for (int v = 0; v < 6; v++)
            {
                float spread = (v / 5f - 0.5f) * 1.6f;
                var p = foot + Vector3.up * 0.11f + side * (spread * 0.05f);
                var dir = (outDir + side * spread * 0.6f + Vector3.up * 0.25f).normalized;
                var hang = (Vector3.down + outDir * 0.12f + side * spread * 0.18f).normalized;
                int n = 7 + rnd.Next(7);
                var path = new List<Vector3> { p };
                for (int i = 0; i < n; i++)
                {
                    dir = Vector3.Slerp(dir, hang, 0.28f).normalized;
                    p += dir * 0.06f;
                    path.Add(p);
                    var axis = (outDir * 0.55f + side * (i % 2 == 0 ? 0.8f : -0.8f) + Vector3.down * 0.25f).normalized;
                    Kite(leaves, p, axis, 0.055f, 0.042f);
                }
                k.Of(h.Leaf).AddTube(path, 0.003f, 3, false);
            }
        }

        /// <summary>The north wall east of the fridge: oak slats on charcoal, a floating console, the TV, a fig in the corner (u = x).</summary>
        void MediaWall(KitBatch k, HomeMats h, System.Random rnd)
        {
            var N = NorthWall;
            WBox(k, h.Charcoal, N, 2.835f, 1.45f, 0.004f, 2.43f, 2.9f, 0.008f);
            for (float su = 1.66f; su <= 4.02f; su += 0.074f) WBox(k, h.Oak, N, su, 1.45f, 0.019f, 0.034f, 2.9f, 0.022f);
            // Floating walnut console with three doors; a soundbar, a succulent, books and a brass ring on top.
            WBox(k, h.Walnut, N, 2.9f, 0.39f, 0.23f, 1.9f, 0.34f, 0.4f);
            for (int d = 1; d < 3; d++) WBox(k, h.Black, N, 1.95f + d * 1.9f / 3f, 0.39f, 0.432f, 0.006f, 0.3f, 0.004f);
            WBox(k, h.Black, N, 2.9f, 0.535f, 0.432f, 1.86f, 0.008f, 0.004f);
            WBox(k, h.Black, N, 2.9f, 0.595f, 0.13f, 0.9f, 0.07f, 0.1f);
            Succulent(k, h, N.P(2.12f, 0.56f, 0.24f), rnd);
            WBoxY(k, h.Paint(1), N, 3.6f, 0.578f, 0.24f, 0.3f, 0.036f, 0.22f, 4f);
            WBoxY(k, h.Paint(3), N, 3.6f, 0.614f, 0.24f, 0.26f, 0.036f, 0.19f, -3f);
            k.Of(h.Brass).AddTorus(N.P(3.6f, 0.715f, 0.24f), 0.065f, 0.012f, 24, 6, N.Rot * Quaternion.Euler(90f, 0f, 0f));
            // The TV: a black slab and its glass (off at home, a blank cold glow in the copy).
            WBox(k, h.Black, N, 2.9f, 1.425f, 0.05f, 1.5f, 0.85f, 0.04f);
            WBox(k, h.Screen, N, 2.9f, 1.43f, 0.0715f, 1.46f, 0.81f, 0.003f);
            // A fiddle-leaf fig in a woven basket in the corner by the window.
            var foot = new Vector3(3.86f, 0f, 3.08f);
            Lathe(k, h.Paint(2), foot, 18, 0f, 0f, 0.17f, 0f, 0.2f, 0.4f, 0.205f, 0.42f, 0.19f, 0.42f, 0.185f, 0.38f);
            k.Of(h.Soil).AddDisc(foot + Vector3.up * 0.38f, Vector3.up, 0.186f, 16);
            FiddleFig(k, h, foot + Vector3.up * 0.38f, 1.55f, rnd);
        }

        static void Succulent(KitBatch k, HomeMats h, Vector3 foot, System.Random rnd)
        {
            Pot(k, h.Ceramic, h.Soil, foot, 0.055f, 0.08f);
            var b = k.Of(h.Leaf);
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.3f;
                var axis = new Vector3(Mathf.Cos(a), 0.9f + (i % 3) * 0.3f, Mathf.Sin(a));
                Kite(b, foot + Vector3.up * 0.075f, axis, 0.05f + (i % 3) * 0.01f, 0.03f);
            }
        }

        /// <summary>A fiddle-leaf fig: a slim trunk and big paddle leaves spiralling up its top half.</summary>
        static void FiddleFig(KitBatch k, HomeMats h, Vector3 foot, float height, System.Random rnd)
        {
            var top = foot + new Vector3(-0.06f, height, -0.05f);
            k.Of(h.Walnut).AddTube(new List<Vector3> { foot, Vector3.Lerp(foot, top, 0.5f) + new Vector3(0.03f, 0f, 0f), top }, 0.018f, 6, false);
            var b = k.Of(h.Leaf);
            for (int i = 0; i < 26; i++)
            {
                float t = 0.35f + 0.65f * i / 25f;
                var at = Vector3.Lerp(foot, top, t);
                float a = i * 2.4f;
                var outDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var axis = (outDir * 0.75f + Vector3.up * (0.65f - t * 0.5f) + Vector3.down * (float)rnd.NextDouble() * 0.2f).normalized;
                var normal = Vector3.Cross(axis, Vector3.Cross(Vector3.up, outDir)).normalized;
                float size = 0.2f + (float)rnd.NextDouble() * 0.09f;
                Leaf(b, at + outDir * 0.02f, axis, normal, size * 1.3f, size);
            }
        }

        /// <summary>Backsplash, cabinet fronts and handles, the LED under the wall units, bulkheads, a cabinet over the
        /// fridge and the counter's everyday clutter (the run's worktop is at 0.905 m, sink near x -2.3, hob near x -1).</summary>
        void KitchenDetail(KitBatch k, HomeMats h)
        {
            var N = NorthWall;
            WBox(k, h.Marble, N, -1.5f, 1.3525f, 0.006f, 4.02f, 0.895f, 0.012f);
            for (int d = 1; d < 6; d++) WBox(k, h.Black, N, -3.5f + d * 4f / 6f, 2.2f, 0.362f, 0.006f, 0.79f, 0.004f);
            for (int d = 0; d < 6; d++) WBox(k, h.Metal, N, -3.5f + (d + 0.5f) * 4f / 6f, 1.845f, 0.375f, 0.32f, 0.014f, 0.02f);
            foreach (var (a, b) in new[] { (-3.42f, -2.2f), (-2.1f, -0.9f), (-0.8f, 0.42f) }) WBox(k, h.Led, N, (a + b) * 0.5f, 1.794f, 0.3f, b - a, 0.008f, 0.02f);
            WBox(k, h.Wall, N, -1.5f, 2.75f, 0.18f, 4.0f, 0.3f, 0.36f);
            WBox(k, h.Wood, N, 1.1f, 2.3f, 0.31f, 0.85f, 0.6f, 0.62f);
            WBox(k, h.Black, N, 1.1f, 2.3f, 0.622f, 0.006f, 0.58f, 0.004f);
            foreach (float hu in new[] { 1.04f, 1.16f }) WBox(k, h.Metal, N, hu, 2.05f, 0.635f, 0.014f, 0.16f, 0.02f);
            WBox(k, h.Wall, N, 1.1f, 2.75f, 0.31f, 0.85f, 0.3f, 0.62f);
            const float top = 0.905f;
            // Canisters with oak lids, a cutting board against the splash, soap by the sink, a utensil crock, a kettle and
            // an espresso machine at the right-hand end.
            for (int i = 0; i < 3; i++)
            {
                var c = N.P(-3.36f + i * 0.13f, top, 0.13f);
                float ch = 0.2f - i * 0.04f;
                k.Of(h.Ceramic).AddCylinder(c, 0.055f, ch, 16);
                k.Of(h.Oak).AddCylinder(c + Vector3.up * ch, 0.057f, 0.022f, 16);
            }
            k.Box(h.Oak, N.P(-2.86f, top + 0.15f, 0.035f), new Vector3(0.36f, 0.28f, 0.018f), N.Rot * Quaternion.Euler(8f, 0f, 0f));
            var soap = N.P(-1.95f, top, 0.1f);
            k.Of(h.Ceramic).AddCylinder(soap, 0.03f, 0.14f, 12);
            k.Box(h.Charcoal, soap + Vector3.up * 0.16f, new Vector3(0.02f, 0.04f, 0.02f));
            var crock = N.P(-1.62f, top, 0.11f);
            Lathe(k, h.Ceramic, crock, 16, 0f, 0f, 0.055f, 0f, 0.062f, 0.15f, 0.058f, 0.16f, 0.05f, 0.03f, 0f, 0.03f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 1.7f;
                var tip = crock + new Vector3(Mathf.Cos(a) * 0.05f, 0.33f, Mathf.Sin(a) * 0.05f);
                k.Of(i % 2 == 0 ? h.Oak : h.Charcoal).AddTube(new List<Vector3> { crock + Vector3.up * 0.04f, tip }, 0.009f, 5, true);
            }
            var kettle = N.P(-0.36f, top, 0.24f);
            k.Of(h.Charcoal).AddCylinder(kettle, 0.095f, 0.02f, 18);
            Lathe(k, h.Metal, kettle + Vector3.up * 0.02f, 18, 0f, 0f, 0.085f, 0f, 0.09f, 0.08f, 0.078f, 0.18f, 0.05f, 0.2f, 0f, 0.205f);
            k.Of(h.Charcoal).AddTube(new List<Vector3> { kettle + new Vector3(-0.08f, 0.07f, 0f), kettle + new Vector3(-0.13f, 0.12f, 0f), kettle + new Vector3(-0.11f, 0.2f, 0f), kettle + new Vector3(-0.04f, 0.215f, 0f) }, 0.012f, 6, true);
            var espresso = N.P(0.16f, top, 0.26f);
            k.Box(h.Metal, espresso + Vector3.up * 0.16f, new Vector3(0.26f, 0.32f, 0.3f));
            k.Box(h.Charcoal, espresso + new Vector3(0f, 0.325f, 0f), new Vector3(0.26f, 0.012f, 0.3f));
            k.Box(h.Charcoal, espresso + new Vector3(0f, 0.025f, -0.17f), new Vector3(0.2f, 0.03f, 0.08f));
            k.Of(h.Charcoal).AddCylinder(espresso + new Vector3(0f, 0.19f, -0.17f), 0.03f, 0.05f, 12);
            Lathe(k, h.Ceramic, espresso + new Vector3(0f, 0.04f, -0.17f), 14, 0f, 0f, 0.022f, 0f, 0.03f, 0.05f, 0.027f, 0.05f, 0.02f, 0.008f, 0f, 0.008f);
        }

        /// <summary>The living and dining area: coffee table and its coffee set, floor lamp, cushion and throw, a pouf, a
        /// side table, a rug under the dining table, the fruit bowl, the pendant's cord.</summary>
        void LivingDetail(KitBatch k, HomeMats h, System.Random rnd)
        {
            bool m = Corrupted;
            // Coffee table (its collider is the procedural box in Build): an oak top on black legs over a walnut shelf.
            k.Of(h.Oak).AddRoundedPlate(new Vector3(1.2f, 0.3775f, -0.7f), 1.1f, 0.6f, 0.08f, 0.045f, Quaternion.Euler(90f, 0f, 0f));
            foreach (float lx in new[] { 0.73f, 1.67f }) foreach (float lz in new[] { -0.92f, -0.48f }) k.Box(h.Black, new Vector3(lx, 0.178f, lz), new Vector3(0.03f, 0.355f, 0.03f));
            k.Box(h.Walnut, new Vector3(1.2f, 0.12f, -0.7f), new Vector3(1.0f, 0.025f, 0.5f));
            const float top = 0.4f;
            // Gahwa: a brass dallah on a round tray, finjan cups, dates. The copy sets out six cups in a row and no dates.
            var tray = new Vector3(0.98f, top, -0.72f);
            k.Of(h.Brass).AddCylinder(tray, 0.19f, 0.01f, 28);
            k.Of(h.Brass).AddTorus(tray + Vector3.up * 0.01f, 0.19f, 0.006f, 28, 5, Quaternion.identity);
            Dallah(k, h.Brass, tray + new Vector3(-0.04f, 0.01f, -0.03f), 20f);
            if (!m)
            {
                Cup(k, h.Ceramic, tray + new Vector3(0.1f, 0.01f, 0.07f));
                Cup(k, h.Ceramic, tray + new Vector3(0.12f, 0.01f, -0.06f));
                Cup(k, h.Ceramic, new Vector3(1.3f, top, -0.55f));
                var bowl = new Vector3(1.52f, top, -0.53f);
                Lathe(k, h.Ceramic, bowl, 18, 0f, 0f, 0.04f, 0f, 0.07f, 0.025f, 0.08f, 0.045f, 0.077f, 0.047f, 0.066f, 0.026f, 0.035f, 0.008f, 0f, 0.008f);
                for (int i = 0; i < 9; i++)
                {
                    float a = i * 2.3f, r = i == 0 ? 0f : 0.035f + (i % 2) * 0.012f;
                    var p = bowl + new Vector3(Mathf.Cos(a) * r, 0.022f + (i == 0 ? 0.012f : 0f), Mathf.Sin(a) * r);
                    var along = new Vector3(Mathf.Cos(a + 1.2f), 0.1f, Mathf.Sin(a + 1.2f)) * 0.007f;
                    k.Of(h.Soil).AddSphere(p - along, 0.0105f, 6, 4);
                    k.Of(h.Soil).AddSphere(p + along, 0.0105f, 6, 4);
                }
            }
            else
            {
                for (int i = 0; i < 6; i++) Cup(k, h.Ceramic, new Vector3(1.2f + i * 0.07f, top, -0.95f));
                Lathe(k, h.Ceramic, new Vector3(1.52f, top, -0.53f), 18, 0f, 0f, 0.04f, 0f, 0.07f, 0.025f, 0.08f, 0.045f, 0.077f, 0.047f, 0.066f, 0.026f, 0.035f, 0.008f, 0f, 0.008f);
            }
            k.Box(h.Paint(4), new Vector3(1.52f, top + 0.018f, -0.78f), new Vector3(0.3f, 0.036f, 0.23f), Quaternion.Euler(0f, 8f, 0f));
            k.Box(h.Paint(3), new Vector3(1.53f, top + 0.05f, -0.785f), new Vector3(0.26f, 0.028f, 0.2f), Quaternion.Euler(0f, -5f, 0f));
            k.Box(h.Black, new Vector3(0.76f, top + 0.009f, -0.47f), new Vector3(0.045f, 0.018f, 0.17f), Quaternion.Euler(0f, 15f, 0f));

            // The floor lamp the sofa's corner light belongs to.
            var lamp = new Vector3(2.68f, 0f, -2.42f);
            k.Of(h.Black).AddCylinder(lamp, 0.15f, 0.025f, 20);
            k.Of(h.Black).AddCylinder(lamp + Vector3.up * 0.025f, 0.012f, 1.35f, 8);
            Shade(k, h.Shade, lamp + Vector3.up * 1.36f, 0.23f, 0.2f, 0.3f);
            k.Of(h.Led).AddSphere(lamp + Vector3.up * 1.43f, 0.035f, 10, 8);
            // A terracotta cushion against the sofa's middle cushion; a teal throw folded over the east arm.
            k.Of(h.Paint(5)).AddRoundedPlate(new Vector3(1.22f, 0.67f, -2.19f), 0.42f, 0.42f, 0.11f, 0.13f, Quaternion.Euler(-15f, 6f, 0f));
            k.Box(h.Paint(4), new Vector3(2.27f, 0.645f, -2.08f), new Vector3(0.26f, 0.04f, 0.72f));
            k.Box(h.Paint(4), new Vector3(2.395f, 0.46f, -2.08f), new Vector3(0.025f, 0.38f, 0.72f));
            // A leather pouf from Marrakech on the rug; a small side table at the sofa's west end.
            Lathe(k, h.Paint(2), new Vector3(2.02f, 0f, 0.12f), 20, 0f, 0f, 0.24f, 0f, 0.27f, 0.06f, 0.285f, 0.19f, 0.265f, 0.33f, 0.2f, 0.38f, 0f, 0.39f);
            var side = new Vector3(-0.3f, 0f, -2.12f);
            k.Of(h.Black).AddCylinder(side, 0.14f, 0.015f, 18);
            k.Of(h.Black).AddCylinder(side, 0.018f, 0.5f, 8);
            k.Of(h.Oak).AddCylinder(side + Vector3.up * 0.5f, 0.22f, 0.025f, 24);
            k.Box(h.Paint(0), side + new Vector3(0.02f, 0.543f, -0.03f), new Vector3(0.2f, 0.035f, 0.15f), Quaternion.Euler(0f, 20f, 0f));
            Lathe(k, h.Ceramic, side + new Vector3(0.08f, 0.525f, 0.09f), 14, 0f, 0f, 0.035f, 0f, 0.04f, 0.09f, 0.036f, 0.09f, 0.033f, 0.01f, 0f, 0.01f);

            // A rug under the dining table; a bowl of fruit on it (empty in the copy); the pendant's cord and canopy.
            k.Box(h.Rug, new Vector3(-1.6f, 0.004f, 0.435f), new Vector3(2.7f, 0.008f, 2.37f));
            var fruit = new Vector3(-2.12f, 0.765f, 0.66f);
            Lathe(k, h.Ceramic, fruit, 20, 0f, 0f, 0.06f, 0f, 0.12f, 0.05f, 0.135f, 0.075f, 0.13f, 0.077f, 0.115f, 0.05f, 0.05f, 0.012f, 0f, 0.012f);
            if (!m)
                for (int i = 0; i < 5; i++)
                {
                    float a = i * 1.25f, r = i == 4 ? 0f : 0.055f;
                    k.Of(h.Paint(i == 2 ? 2 : 5)).AddSphere(fruit + new Vector3(Mathf.Cos(a) * r, 0.05f + (i == 4 ? 0.04f : 0f), Mathf.Sin(a) * r), 0.038f, 12, 8);
                }
            k.Box(h.Black, new Vector3(-1.6f, 2.685f, 0.4f), new Vector3(0.008f, 0.43f, 0.008f));
            k.Of(h.Black).AddCylinder(new Vector3(-1.6f, 2.875f, 0.4f), 0.055f, 0.025f, 16);
        }

        /// <summary>A brass dallah: a waisted belly, a slim neck and flared mouth, a domed lid with a finial, the long beak
        /// spout rising from the belly and the handle behind. About 26 cm tall; the spout points along +X turned by yaw.</summary>
        static void Dallah(KitBatch k, Material brass, Vector3 p, float yaw)
        {
            var b = k.Of(brass);
            var q = Quaternion.Euler(0f, yaw, 0f);
            Lathe(k, brass, p, 20, 0f, 0f, 0.05f, 0f, 0.062f, 0.018f, 0.064f, 0.04f, 0.056f, 0.066f, 0.04f, 0.085f, 0.024f, 0.105f, 0.021f, 0.13f, 0.026f, 0.15f, 0.04f, 0.168f, 0.043f, 0.174f);
            Lathe(k, brass, p + Vector3.up * 0.174f, 16, 0.04f, 0f, 0.038f, 0.012f, 0.026f, 0.03f, 0.012f, 0.042f, 0.006f, 0.05f, 0.012f, 0.062f, 0.004f, 0.075f, 0f, 0.08f);
            var spout = new List<Vector3>();
            foreach (var s in new[] { new Vector2(0.052f, 0.045f), new Vector2(0.075f, 0.07f), new Vector2(0.088f, 0.1f), new Vector2(0.098f, 0.13f), new Vector2(0.108f, 0.155f), new Vector2(0.122f, 0.172f), new Vector2(0.137f, 0.178f) })
                spout.Add(p + q * new Vector3(s.x, s.y, 0f));
            b.AddTube(spout, 0.007f, 6, true);
            var handle = new List<Vector3>();
            foreach (var s in new[] { new Vector2(-0.054f, 0.07f), new Vector2(-0.075f, 0.085f), new Vector2(-0.088f, 0.11f), new Vector2(-0.085f, 0.14f), new Vector2(-0.07f, 0.16f), new Vector2(-0.042f, 0.168f) })
                handle.Add(p + q * new Vector3(s.x, s.y, 0f));
            b.AddTube(handle, 0.005f, 6, true);
        }

        /// <summary>A small handleless coffee cup (finjan), with an inside.</summary>
        static void Cup(KitBatch k, Material ceramic, Vector3 p)
            => Lathe(k, ceramic, p, 14, 0f, 0f, 0.017f, 0f, 0.021f, 0.008f, 0.025f, 0.03f, 0.026f, 0.036f, 0.024f, 0.036f, 0.021f, 0.012f, 0.012f, 0.006f, 0f, 0.006f);

        /// <summary>The south wall behind the sofa (a colour-field canvas, a clock), the bathroom door's casing, and the entry:
        /// a shoe bench, hooks with a scarf and a tote, slippers, a snake plant, light switches (u = -x on the south wall).</summary>
        void EntryAndSouthWall(KitBatch k, HomeMats h, System.Random rnd)
        {
            bool m = Corrupted;
            var S = SouthWall;
            var W = WestWall;
            // Two bands of colour on terracotta above the sofa (a blank canvas in the copy).
            WBox(k, m ? h.Paint(0) : h.Paint(5), S, -0.4f, 1.62f, 0.02f, 1.3f, 0.85f, 0.04f);
            if (!m)
            {
                WBox(k, h.Paint(0), S, -0.4f, 1.79f, 0.041f, 1.12f, 0.34f, 0.003f);
                WBox(k, h.Paint(2), S, -0.4f, 1.41f, 0.041f, 1.12f, 0.28f, 0.003f);
                WBox(k, h.Paint(3), S, -0.4f, 1.59f, 0.0412f, 1.12f, 0.012f, 0.003f);
            }
            // A wall clock at twenty to eight (no hands in the copy).
            const float ku = 1.9f, kv = 2.2f;
            k.Of(h.Black).AddTorus(S.P(ku, kv, 0.014f), 0.16f, 0.012f, 32, 6, S.Rot * Quaternion.Euler(90f, 0f, 0f));
            k.Of(h.Trim).AddDisc(S.P(ku, kv, 0.012f), S.Out, 0.152f, 32);
            for (int i = 0; i < 12; i++) WBoxR(k, h.Black, S, ku, kv, -i * 30f, 0f, 0.125f, 0.0135f, 0.008f, i % 3 == 0 ? 0.03f : 0.015f, 0.002f);
            if (!m)
            {
                WBoxR(k, h.Black, S, ku, kv, -230f, 0f, 0.035f, 0.016f, 0.01f, 0.075f, 0.003f);
                WBoxR(k, h.Black, S, ku, kv, -240f, 0f, 0.05f, 0.019f, 0.006f, 0.11f, 0.003f);
                k.Of(h.Brass).AddDisc(S.P(ku, kv, 0.021f), S.Out, 0.01f, 12);
            }
            // The bathroom door's casing on the room side (opening x 1.8..3.8).
            WBox(k, h.Trim, S, -1.76f, 1.09f, 0.009f, 0.08f, 2.18f, 0.018f);
            WBox(k, h.Trim, S, -3.84f, 1.09f, 0.009f, 0.08f, 2.18f, 0.018f);
            WBox(k, h.Trim, S, -2.8f, 2.14f, 0.009f, 2.16f, 0.08f, 0.018f);
            // The front door's casing (the leaf hangs at z -3.05..-2.14 on the west wall).
            WBox(k, h.Trim, W, -3.11f, 1.09f, 0.009f, 0.08f, 2.18f, 0.018f);
            WBox(k, h.Trim, W, -2.09f, 1.09f, 0.009f, 0.08f, 2.18f, 0.018f);
            WBox(k, h.Trim, W, -2.6f, 2.14f, 0.009f, 1.1f, 0.08f, 0.018f);
            // Switches by both doors and the air-conditioning panel by the front door.
            WBox(k, h.Trim, W, -1.93f, 1.15f, 0.004f, 0.086f, 0.086f, 0.008f);
            WBox(k, h.Trim, W, -1.93f, 1.15f, 0.01f, 0.03f, 0.05f, 0.006f);
            WBox(k, h.Trim, W, -1.93f, 1.42f, 0.006f, 0.09f, 0.09f, 0.012f);
            WBox(k, h.Led, W, -1.93f, 1.435f, 0.0125f, 0.05f, 0.022f, 0.002f);
            WBox(k, h.Trim, S, -1.6f, 1.15f, 0.004f, 0.086f, 0.086f, 0.008f);
            WBox(k, h.Trim, S, -1.6f, 1.15f, 0.01f, 0.03f, 0.05f, 0.006f);
            // Shoe bench in the corner by the front door (z -3.95..-3.25), shoes underneath, hooks above.
            WBox(k, h.Oak, W, -3.6f, 0.435f, 0.18f, 0.7f, 0.03f, 0.36f);
            WBox(k, h.Walnut, W, -3.935f, 0.21f, 0.17f, 0.03f, 0.42f, 0.34f);
            WBox(k, h.Walnut, W, -3.265f, 0.21f, 0.17f, 0.03f, 0.42f, 0.34f);
            for (int s = 0; s < 4; s++) WBox(k, h.Oak, W, -3.6f, 0.12f, 0.05f + s * 0.085f, 0.64f, 0.018f, 0.06f);
            int[] shoe = { -1, -1, 2, 2 };
            for (int s = 0; s < 4; s++)
            {
                var mat = shoe[s] < 0 ? h.Charcoal : h.Paint(shoe[s]);
                WBoxY(k, mat, W, -3.83f + s * 0.11f + (s / 2) * 0.06f, 0.17f, 0.19f, 0.09f, 0.08f, 0.27f, (s % 2 == 0 ? 3f : -2f));
            }
            WBox(k, h.Walnut, W, -3.6f, 1.72f, 0.01f, 0.62f, 0.07f, 0.02f);
            for (int i = 0; i < 3; i++) k.Box(h.Brass, W.P(-3.82f + i * 0.22f, 1.74f, 0.045f), new Vector3(0.014f, 0.014f, 0.06f), W.Rot * Quaternion.Euler(25f, 0f, 0f));
            WBox(k, h.Paint(0), W, -3.82f, 1.4f, 0.06f, 0.11f, 0.62f, 0.02f, 2f);
            WBox(k, h.Paint(3), W, -3.52f, 1.34f, 0.075f, 0.3f, 0.34f, 0.07f, -3f);
            WBox(k, h.Paint(3), W, -3.6f, 1.58f, 0.06f, 0.012f, 0.3f, 0.01f, 18f);
            foreach (float sz in new[] { -3.62f, -3.49f })
                k.Box(h.Paint(4), new Vector3(-3.95f, 0.015f, sz), new Vector3(0.1f, 0.03f, 0.26f), Quaternion.Euler(0f, sz < -3.55f ? 8f : -4f, 0f));
            // A snake plant by the door (the copy has two, identical).
            SnakePlant(k, h, new Vector3(-3.72f, 0f, -3.72f), new System.Random(31));
            if (m) SnakePlant(k, h, new Vector3(-3.27f, 0f, -3.72f), new System.Random(31));
        }

        static void SnakePlant(KitBatch k, HomeMats h, Vector3 foot, System.Random rnd)
        {
            Lathe(k, h.Charcoal, foot, 18, 0f, 0f, 0.13f, 0f, 0.155f, 0.3f, 0.16f, 0.32f, 0.15f, 0.32f, 0.145f, 0.29f);
            k.Of(h.Soil).AddDisc(foot + Vector3.up * 0.29f, Vector3.up, 0.146f, 16);
            var b = k.Of(h.Leaf);
            for (int i = 0; i < 11; i++)
            {
                float a = i * 2.39f, w = 0.035f + (float)rnd.NextDouble() * 0.018f, l = 0.45f + (float)rnd.NextDouble() * 0.38f;
                var outDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var axis = (Vector3.up + outDir * (0.08f + (float)rnd.NextDouble() * 0.22f)).normalized;
                var blade = new[] { new Vector2(-w, 0f), new Vector2(w, 0f), new Vector2(w * 0.85f, l * 0.65f), new Vector2(0f, l), new Vector2(-w * 0.85f, l * 0.65f) };
                b.AddExtrudedXY(blade, 0.006f, foot + Vector3.up * 0.29f + outDir * 0.05f * (i % 3), Quaternion.LookRotation(Vector3.Cross(axis, Vector3.Cross(Vector3.up, outDir)).normalized, axis));
            }
        }

        /// <summary>Curtains gathered at both ends of the window (clear of the middle, where the reflection lives), the
        /// track, a marble window board with a succulent and a candle lantern (the copy has the succulent twice).</summary>
        void WindowDetail(KitBatch k, HomeMats h, System.Random rnd)
        {
            bool m = Corrupted;
            k.Box(h.Charcoal, new Vector3(4.31f, 2.845f, 0f), new Vector3(0.03f, 0.025f, 8f));
            Curtain(k, h.Paint(3), 4.33f, -3.98f, -3.36f, 9, 35f);
            Curtain(k, h.Fabric, 4.24f, -3.98f, -3.54f, 7, 42f);
            Curtain(k, h.Paint(3), 4.33f, 3.36f, 3.98f, 9, 35f);
            Curtain(k, h.Fabric, 4.24f, 3.54f, 3.98f, 7, 42f);
            k.Box(h.Marble, new Vector3(4.475f, 0.415f, 0f), new Vector3(0.22f, 0.03f, 8f));
            Succulent(k, h, new Vector3(4.47f, 0.43f, 2.6f), new System.Random(8));
            if (m) Succulent(k, h, new Vector3(4.47f, 0.43f, 2.82f), new System.Random(8));
            var lantern = new Vector3(4.47f, 0.43f, -2.9f);
            k.Box(h.Brass, lantern + Vector3.up * 0.005f, new Vector3(0.12f, 0.01f, 0.12f));
            k.Box(h.Brass, lantern + Vector3.up * 0.2f, new Vector3(0.12f, 0.012f, 0.12f));
            foreach (var c in new[] { new Vector2(-0.055f, -0.055f), new Vector2(0.055f, -0.055f), new Vector2(-0.055f, 0.055f), new Vector2(0.055f, 0.055f) })
                k.Box(h.Brass, lantern + new Vector3(c.x, 0.1f, c.y), new Vector3(0.008f, 0.19f, 0.008f));
            k.Of(h.Ceramic).AddCylinder(lantern + Vector3.up * 0.01f, 0.03f, 0.07f, 12);
            if (!m) k.Of(h.Shade).AddSphere(lantern + Vector3.up * 0.095f, 0.008f, 6, 4);
        }

        /// <summary>A gathered curtain hanging from the track: a zig-zag of narrow pleats between z0 and z1 at x.</summary>
        static void Curtain(KitBatch k, Material cloth, float x, float z0, float z1, int pleats, float angle)
        {
            float pw = (z1 - z0) / pleats, len = pw / Mathf.Cos(angle * Mathf.Deg2Rad);
            for (int i = 0; i < pleats; i++)
                k.Box(cloth, new Vector3(x, 1.425f, z0 + (i + 0.5f) * pw), new Vector3(0.012f, 2.79f, len), Quaternion.Euler(0f, i % 2 == 0 ? angle : -angle, 0f));
        }

        /// <summary>Cornice, recessed downlights (some dead in the copy), slot diffusers, a smoke detector, skirting.</summary>
        void CeilingAndTrim(KitBatch k, HomeMats h)
        {
            bool m = Corrupted;
            const float ceil = 2.9f;
            k.Box(h.Trim, new Vector3(-4.475f, ceil - 0.025f, 0f), new Vector3(0.05f, 0.05f, 8f));
            k.Box(h.Trim, new Vector3(-0.05f, ceil - 0.025f, -3.975f), new Vector3(8.9f, 0.05f, 0.05f));
            k.Box(h.Trim, new Vector3(-4.0f, ceil - 0.025f, 3.975f), new Vector3(1.0f, 0.05f, 0.05f));
            var spots = new[]
            {
                new Vector2(-3.0f, -3.3f), new Vector2(-0.8f, -3.3f), new Vector2(1.2f, -3.3f), new Vector2(0.4f, 0.9f), new Vector2(2.4f, 0.9f),
                new Vector2(-3.0f, 2.75f), new Vector2(-1.5f, 2.75f), new Vector2(0.0f, 2.75f), new Vector2(2.9f, 2.75f), new Vector2(-3.8f, -1.0f),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                var p = new Vector3(spots[i].x, ceil, spots[i].y);
                k.Of(h.Trim).AddCylinder(p + Vector3.down * 0.012f, 0.06f, 0.012f, 16);
                bool dead = m && (i == 2 || i == 5 || i == 8);
                k.Of(dead ? h.Charcoal : h.Led).AddDisc(p + Vector3.down * 0.0125f, Vector3.down, 0.042f, 16);
            }
            foreach (var (c, s) in new[] { (new Vector3(3.75f, ceil, 0f), new Vector3(0.12f, 0.008f, 4.8f)), (new Vector3(-1.3f, ceil, -3.62f), new Vector3(3.8f, 0.008f, 0.12f)) })
            {
                k.Box(h.Trim, c + Vector3.down * 0.004f, s);
                k.Box(h.Charcoal, c + Vector3.down * 0.0085f, new Vector3(s.x > 1f ? s.x - 0.08f : 0.045f, 0.002f, s.z > 1f ? s.z - 0.08f : 0.045f));
            }
            k.Of(h.Trim).AddCylinder(new Vector3(1.4f, ceil - 0.03f, -0.2f), 0.055f, 0.03f, 16);
            // Skirting (white), around the doors and fitted furniture.
            var skirt = new (Vector3 c, Vector3 s)[]
            {
                (new Vector3(-4.492f, 0.045f, -3.57f), new Vector3(0.016f, 0.09f, 0.86f)), (new Vector3(-4.492f, 0.045f, -0.2f), new Vector3(0.016f, 0.09f, 3.7f)),
                (new Vector3(-1.39f, 0.045f, -3.992f), new Vector3(6.22f, 0.09f, 0.016f)), (new Vector3(4.14f, 0.045f, -3.992f), new Vector3(0.52f, 0.09f, 0.016f)),
                (new Vector3(-4.0f, 0.045f, 3.992f), new Vector3(1.0f, 0.09f, 0.016f)),
            };
            foreach (var (c, s) in skirt) k.Box(h.Trim, c, s);
        }

        /// <summary>
        /// The balcony beyond the glass (visual only; the window collider keeps everyone in): a tiled slab running on past
        /// the neighbours', the soffit of the floor above, privacy fins, a frameless glass balustrade with a slim rail, two
        /// teak loungers and an olive tree (clear of the middle, where the reflection's corridor stands) and a sconce.
        /// </summary>
        void Balcony(Transform g, KitBatch k, HomeMats h, System.Random rnd)
        {
            bool m = Corrupted;
            k.Box(h.Concrete, new Vector3(5.5f, -0.135f, 0f), new Vector3(1.6f, 0.23f, 24f));
            k.Box(h.Tile, new Vector3(5.48f, -0.01f, 0f), new Vector3(1.52f, 0.02f, 24f));
            // The soffit sits high enough that the low sun still reaches the feature wall; a header closes it to the glass.
            k.Box(h.Concrete, new Vector3(5.5f, 3.28f, 0f), new Vector3(1.6f, 0.26f, 24f));
            k.Box(h.Concrete, new Vector3(4.65f, 3.1f, 0f), new Vector3(0.2f, 0.4f, 24f));
            foreach (float z in new[] { -12.1f, -4.15f, 4.15f, 12.1f }) k.Box(h.Concrete, new Vector3(5.2f, 1.565f, z), new Vector3(1.0f, 3.17f, 0.2f));
            var glass = MeshFactory.Box("BalconyGlass", g, new Vector3(6.22f, 0.51f, 0f), new Vector3(0.012f, 1.02f, 24f), Mats.Glass(new Color(0.7f, 0.8f, 0.82f, 0.1f)), false);
            glass.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            k.Box(h.Metal, new Vector3(6.22f, 1.04f, 0f), new Vector3(0.035f, 0.03f, 24f));
            k.Box(h.Charcoal, new Vector3(6.22f, 0.05f, 0f), new Vector3(0.06f, 0.1f, 24f));
            // Two loungers facing the view (in the copy one has turned to face the window), a side table, an olive tree.
            Lounger(k, h, new Vector3(5.45f, 0f, 2.35f), 0f);
            Lounger(k, h, new Vector3(5.45f, 0f, 3.45f), m ? 180f : 0f);
            k.Of(h.Charcoal).AddCylinder(new Vector3(5.75f, 0f, 2.9f), 0.17f, 0.42f, 18);
            var planter = new Vector3(5.55f, 0f, -3.3f);
            k.Box(h.Charcoal, planter + Vector3.up * 0.27f, new Vector3(0.52f, 0.54f, 0.52f));
            k.Box(h.Soil, planter + Vector3.up * 0.53f, new Vector3(0.46f, 0.02f, 0.46f));
            k.Of(h.Walnut).AddTube(new List<Vector3> { planter + Vector3.up * 0.54f, planter + new Vector3(0.05f, 1.1f, 0.03f), planter + new Vector3(-0.02f, 1.55f, 0.05f) }, 0.03f, 6, false);
            for (int i = 0; i < 9; i++)
            {
                float a = i * 2.1f, r = 0.12f + (i % 3) * 0.1f;
                k.Of(h.Leaf).AddSphere(planter + new Vector3(Mathf.Cos(a) * r, 1.45f + (i % 4) * 0.12f, Mathf.Sin(a) * r), 0.16f + (i % 2) * 0.05f, 9, 7);
            }
            // The sconce on the fin (dark in the copy).
            k.Box(h.Brass, new Vector3(5.15f, 2.15f, 4.03f), new Vector3(0.14f, 0.24f, 0.05f));
            k.Box(m ? h.Charcoal : h.Shade, new Vector3(5.15f, 2.15f, 3.995f), new Vector3(0.1f, 0.18f, 0.02f));
        }

        /// <summary>A teak lounger with cream cushions, facing +X turned by <paramref name="yaw"/> about its centre.</summary>
        static void Lounger(KitBatch k, HomeMats h, Vector3 c, float yaw)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            void B(Material mat, Vector3 o, Vector3 s, Quaternion r) => k.Box(mat, c + q * o, s, q * r);
            B(h.Oak, new Vector3(0f, 0.34f, 0f), new Vector3(0.62f, 0.05f, 0.6f), Quaternion.identity);
            B(h.Paint(3), new Vector3(0.02f, 0.41f, 0f), new Vector3(0.56f, 0.09f, 0.56f), Quaternion.identity);
            B(h.Oak, new Vector3(-0.3f, 0.66f, 0f), new Vector3(0.05f, 0.6f, 0.6f), Quaternion.Euler(0f, 0f, 16f));
            B(h.Paint(3), new Vector3(-0.24f, 0.68f, 0f), new Vector3(0.08f, 0.5f, 0.54f), Quaternion.Euler(0f, 0f, 16f));
            foreach (float z in new[] { -0.29f, 0.29f })
            {
                B(h.Oak, new Vector3(0f, 0.57f, z), new Vector3(0.62f, 0.04f, 0.05f), Quaternion.identity);
                foreach (float x in new[] { -0.27f, 0.27f }) B(h.Oak, new Vector3(x, 0.28f, z), new Vector3(0.045f, 0.56f, 0.045f), Quaternion.identity);
            }
        }
    }
}

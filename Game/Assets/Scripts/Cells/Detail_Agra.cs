using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    // Place detail for Agra (playtest: "make all the countries detailed"). Builders add architecture, set dressing
    // and practical lights around the puzzle objects, batched per material; far vistas are built in Landmarks.cs / Vista.cs.
    public partial class AgraCell
    {
        static readonly float[] s_CypressZ = { -3f, 0.4f, 3.8f, 7.2f, 10.6f, 14f };

        /// <summary>
        /// A Mughal char-bagh at dusk, framed for the domed pavilion at the end of the axis: the reflecting channel in a
        /// moulded marble kerb with a pietra-dura band and a row of fountain jets frozen mid-spray; red sandstone walks
        /// inlaid with a white marble lattice; an inlaid medallion where the player arrives; slender Italian cypresses
        /// (a column of clumped foliage, not stacked cones); clipped hedges; parterres of marigolds, roses and jasmine;
        /// cross-axis paths to the wall pavilions; lantern posts; marble benches; petals on the water and the stone; and
        /// the enclosure (Detail_AgraArch.cs). Purely architectural and horticultural; nothing sacred is placed or used.
        /// </summary>
        void BuildGardenDetail(Transform g, Material marble, Material stone, Material brass)
        {
            var m = new GardenMats
            {
                Marble = marble,
                Sandstone = stone,
                Recess = Mats.Lit(new Color(0.52f, 0.3f, 0.26f), 0.2f, 0f, "sandstone_blocks"),
                Brass = brass,
                Wood = Mats.Lit(new Color(0.42f, 0.24f, 0.14f), 0.4f, 0f, "wood_dark"),
                Carnelian = Mats.Lit(new Color(0.66f, 0.1f, 0.07f), 0.65f),
                Jade = Mats.Lit(new Color(0.1f, 0.45f, 0.32f), 0.65f),
                Lapis = Mats.Lit(new Color(0.1f, 0.22f, 0.62f), 0.65f),
                Onyx = Mats.Lit(new Color(0.05f, 0.05f, 0.06f), 0.7f),
            };
            var solid = new KitBatch(); // casts shadows: trees, hedges, kerbs, benches, lanterns
            var flat = new KitBatch();  // no shadows: inlay, beds, petals, frozen water, the enclosure
            BuildPaving(flat, m);
            BuildChannel(solid, flat, m);
            BuildPlanting(solid, flat, m);
            BuildFurniture(g, solid, flat, m);
            BuildGardenWalls(flat, m);
            solid.Bake(g, "GardenDetail");
            flat.Bake(g, "GardenInlay", false);
        }

        /// <summary>Marble surround of the channel, the inlaid lattice on both walks and the arrival medallion.</summary>
        void BuildPaving(KitBatch k, GardenMats m)
        {
            const float lift = 0.008f;
            foreach (float sx in new[] { -1f, 1f })
            {
                // Flush marble surround (the trace lies over it) edged with an onyx line, a row of inlaid lozenges.
                k.Box(m.Marble, new Vector3(sx * 1.625f, 0.006f, 5f), new Vector3(0.65f, 0.012f, 17.9f), 1.5f);
                k.Box(m.Onyx, new Vector3(sx * 1.93f, 0.007f, 5f), new Vector3(0.035f, 0.014f, 17.9f));
                for (int i = 0; i < 40; i++)
                {
                    float z = -3.7f + i * 0.445f + 0.2f;
                    k.Box(m.Inlay[i % 3], new Vector3(sx * 1.66f, 0.0135f, z), new Vector3(0.07f, 0.003f, 0.07f), Quaternion.Euler(0f, 45f, 0f));
                }
                // The walks: red sandstone panels framed by marble strips, a marble lozenge (with a carnelian heart)
                // in each, joined corner to corner by a lattice of thinner strips.
                const int panels = 13;
                float z0 = -7.6f, l = 22.6f / panels, xin = sx * 1.95f, xout = sx * 4.5f, xc = sx * 3.225f;
                for (int j = 0; j <= panels; j++)
                    k.Box(m.Marble, new Vector3(xc, lift * 0.5f, z0 + j * l), new Vector3(2.55f, lift, 0.07f));
                for (int j = 0; j < panels; j++)
                {
                    float za = z0 + j * l, zb = za + l, zm = (za + zb) * 0.5f;
                    var a = new Vector3(xin, 0f, zm); var b = new Vector3(xc, 0f, zb); var c = new Vector3(xout, 0f, zm); var d = new Vector3(xc, 0f, za);
                    DetailShapes.GroundStrip(k, m.Marble, a, b, 0.045f, lift);
                    DetailShapes.GroundStrip(k, m.Marble, b, c, 0.045f, lift);
                    DetailShapes.GroundStrip(k, m.Marble, c, d, 0.045f, lift);
                    DetailShapes.GroundStrip(k, m.Marble, d, a, 0.045f, lift);
                    k.Box(m.Marble, new Vector3(xc, 0.005f, zm), new Vector3(0.3f, 0.01f, 0.3f), Quaternion.Euler(0f, 45f, 0f));
                    k.Box(j % 2 == 0 ? m.Carnelian : m.Lapis, new Vector3(xc, 0.0115f, zm), new Vector3(0.13f, 0.003f, 0.13f), Quaternion.Euler(0f, 45f, 0f));
                }
                k.Box(m.Marble, new Vector3(sx * 4.43f, lift * 0.5f, 3.7f), new Vector3(0.12f, lift, 22.6f));
                k.Box(m.Onyx, new Vector3(sx * 4.35f, 0.005f, 3.7f), new Vector3(0.03f, 0.01f, 22.6f));
            }
            foreach (float z in new[] { -3.625f, 13.625f })
            {
                k.Box(m.Marble, new Vector3(0f, 0.006f, z), new Vector3(2.6f, 0.012f, 0.65f), 1.5f);
                k.Box(m.Onyx, new Vector3(0f, 0.007f, z + Mathf.Sign(z - 5f) * 0.305f), new Vector3(3.9f, 0.014f, 0.035f));
            }
            // Arrival medallion: concentric inlaid octagons in the forecourt.
            var med = new Vector3(0f, 0f, -5.45f);
            DetailShapes.GroundRing(k, m.Marble, med, 1.5f, 8, 0.1f, lift, 22.5f);
            DetailShapes.GroundRing(k, m.Onyx, med, 1.37f, 8, 0.035f, lift, 22.5f);
            DetailShapes.GroundRing(k, m.Marble, med, 1.0f, 8, 0.05f, lift, 22.5f);
            k.Of(m.Marble).AddPrism(DetailShapes.Regular(0.62f, 8, 22.5f), 0f, 0.01f, 1f, true, med);
            k.Of(m.Lapis).AddPrism(DetailShapes.Regular(0.42f, 8, 22.5f), 0.01f, 0.013f, 1f, true, med);
            k.Of(m.Marble).AddPrism(DetailShapes.Regular(0.2f, 8, 22.5f), 0.013f, 0.016f, 1f, true, med);
            k.Of(m.Carnelian).AddPrism(DetailShapes.Regular(0.09f, 8, 22.5f), 0.016f, 0.019f, 1f, true, med);
            for (int i = 0; i < 8; i++)
            {
                float a = (22.5f + i * 45f) * Mathf.Deg2Rad;
                var p = med + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.19f;
                k.Box(i % 2 == 0 ? m.Jade : m.Carnelian, p + Vector3.up * 0.005f, new Vector3(0.14f, 0.01f, 0.14f), Quaternion.Euler(0f, 45f - i * 45f, 0f));
            }
        }

        /// <summary>
        /// The channel: a moulded marble kerb around the water (the old solid rim keeps only its collider) with an onyx
        /// and stone-inlay band on the coping, and brass jets whose sprays hang in the air, frozen as they open.
        /// </summary>
        void BuildChannel(KitBatch solid, KitBatch flat, GardenMats m)
        {
            foreach (float sx in new[] { -1f, 1f })
            {
                solid.Box(m.Marble, new Vector3(sx * 1.2f, 0.075f, 5f), new Vector3(0.2f, 0.15f, 16.6f), 1.5f);
                solid.Box(m.Marble, new Vector3(sx * 1.2f, 0.17f, 5f), new Vector3(0.27f, 0.04f, 16.67f), 1.5f);
                foreach (float d in new[] { -0.075f, 0.075f })
                    flat.Box(m.Onyx, new Vector3(sx * 1.2f + d, 0.1905f, 5f), new Vector3(0.01f, 0.002f, 16.55f));
                for (int i = 0; i < 52; i++)
                    flat.Box(m.Inlay[i % 3], new Vector3(sx * 1.2f, 0.1905f, -3.3f + 0.32f * (i + 0.5f)), new Vector3(0.065f, 0.002f, 0.065f), Quaternion.Euler(0f, 45f, 0f));
            }
            foreach (float z in new[] { -3.15f, 13.15f })
            {
                solid.Box(m.Marble, new Vector3(0f, 0.075f, z), new Vector3(2.6f, 0.15f, 0.3f), 1.5f);
                solid.Box(m.Marble, new Vector3(0f, 0.17f, z), new Vector3(2.67f, 0.04f, 0.37f), 1.5f);
            }
            var spray = Mats.Glass(new Color(0.82f, 0.92f, 0.97f, 0.42f), 0.96f, new Color(0.16f, 0.2f, 0.24f));
            var rnd = new System.Random(9);
            foreach (float z in new[] { -1.4f, 1.8f, 5.0f, 11.4f }) // never over the reflected figure (z 7.6..9.4)
            {
                var n = new Vector3(0f, 0.1f, z);
                solid.Of(m.Brass).AddCylinder(n - Vector3.up * 0.01f, 0.05f, 0.03f, 10);
                solid.Of(m.Brass).AddFrustum(n + Vector3.up * 0.02f, 0.03f, 0.012f, 0.05f, 8);
                var jb = flat.Of(spray);
                jb.AddFrustum(n + Vector3.up * 0.06f, 0.016f, 0.008f, 0.62f, 6, false, false);
                var top = n + Vector3.up * 0.68f;
                jb.AddSphere(top, 0.02f, 6, 4);
                for (int a = 0; a < 10; a++)
                {
                    float ang = (a * 36f + z * 17f) * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    for (int j = 1; j <= 4; j++)
                    {
                        float t = j / 4f;
                        jb.AddSphere(top + dir * (0.05f + 0.17f * t) + Vector3.up * (0.06f * t - 0.44f * t * t), 0.013f - 0.0018f * j, 5, 3);
                    }
                }
                for (int j = 0; j < 6; j++)
                    jb.AddSphere(n + new Vector3(DetailShapes.Range(rnd, -0.22f, 0.22f), DetailShapes.Range(rnd, 0.05f, 0.3f), DetailShapes.Range(rnd, -0.22f, 0.22f)), 0.008f, 5, 3);
                foreach (float r in new[] { 0.14f, 0.27f })
                    jb.AddTorus(n + Vector3.up * 0.0012f, r, 0.0035f, 18, 3, Quaternion.identity);
            }
        }

        /// <summary>Cypress rows, clipped hedges along the walks, the cross-axis paths and the flower parterres.</summary>
        void BuildPlanting(KitBatch solid, KitBatch flat, GardenMats m)
        {
            var bark = Mats.Lit(new Color(0.85f, 0.75f, 0.65f), 0.2f, 0f, "wood_dark");
            var leaf = Mats.Lit(new Color(0.6f, 0.86f, 0.74f), 0.18f, 0f, "grass_short");
            var leafLit = Mats.Lit(new Color(0.84f, 1f, 0.84f), 0.22f, 0f, "grass_short");
            var hedge = Mats.Lit(new Color(0.92f, 1f, 0.78f), 0.2f, 0f, "grass_short");
            var rnd = new System.Random(77);
            foreach (float z in s_CypressZ)
                foreach (float x in new[] { -5.2f, 5.2f })
                    Cypress(solid, bark, leaf, leafLit, new Vector3(x, -0.02f, z), rnd);

            // Clipped hedges on the lawn side of the walks, opening at each cypress and at the cross axis, and stopping at
            // the last cypress so the way north (the threshold at z 14.35..15) stays open.
            const float hedgeEnd = 13.38f;
            foreach (float sx in new[] { -1f, 1f })
            {
                float start = -7.3f;
                var cuts = new List<Vector2>();
                foreach (float z in s_CypressZ) cuts.Add(new Vector2(z - 0.62f, z + 0.62f));
                cuts.Add(new Vector2(4.75f, 6.25f));
                cuts.Sort((p, q) => p.x.CompareTo(q.x));
                cuts.Add(new Vector2(hedgeEnd, hedgeEnd));
                foreach (var c in cuts)
                {
                    float end = Mathf.Min(c.x, hedgeEnd);
                    if (end - start > 0.6f)
                    {
                        float zc = (start + end) * 0.5f, len = end - start;
                        solid.Box(hedge, new Vector3(sx * 4.8f, 0.14f, zc), new Vector3(0.26f, 0.32f, len), 0.6f);
                        solid.Box(hedge, new Vector3(sx * 4.8f, 0.32f, zc), new Vector3(0.2f, 0.06f, len - 0.04f), 0.6f);
                    }
                    start = Mathf.Max(start, c.y);
                }
                // Cross-axis path to the wall pavilion: sandstone between marble edges, a line of lozenges.
                flat.Box(m.Sandstone, new Vector3(sx * 6.35f, -0.01f, 5.5f), new Vector3(3.7f, 0.02f, 1.4f), 1.6f);
                foreach (float dz in new[] { -0.67f, 0.67f })
                    flat.Box(m.Marble, new Vector3(sx * 6.35f, -0.005f, 5.5f + dz), new Vector3(3.7f, 0.03f, 0.07f));
                for (int i = 0; i < 6; i++)
                    flat.Box(i % 2 == 0 ? m.Marble : m.Lapis, new Vector3(sx * (4.85f + i * 0.6f), 0.003f, 5.5f), new Vector3(0.22f, 0.012f, 0.22f), Quaternion.Euler(0f, 45f, 0f));
            }

            // Parterres between the cypresses: sandstone kerb, a clipped border, soil, mounds of leaves and bands of
            // flowers (marigold at the rim, roses, then jasmine or cosmos at the heart).
            var soil = Mats.Lit(new Color(0.3f, 0.2f, 0.14f), 0.1f, 0f, "sand_ripples");
            var leaves = Mats.Lit(new Color(0.22f, 0.42f, 0.16f), 0.25f);
            var marigold = new[] { Mats.Lit(new Color(1f, 0.52f, 0.06f), 0.3f), Mats.Lit(new Color(1f, 0.74f, 0.12f), 0.3f) };
            var rose = Mats.Lit(new Color(0.8f, 0.06f, 0.12f), 0.35f);
            var heart = new[] { Mats.Lit(new Color(0.96f, 0.94f, 0.88f), 0.3f), Mats.Lit(new Color(0.86f, 0.2f, 0.52f), 0.3f) };
            var frnd = new System.Random(4);
            int bed = 0;
            foreach (float sx in new[] { -1f, 1f })
                foreach (float z in new[] { -4.7f, -1.3f, 2.1f, 8.9f, 12.3f })
                {
                    var c = new Vector3(sx * 6.9f, -0.02f, z);
                    const float w = 1.8f, d = 2.2f;
                    flat.Box(m.Sandstone, c + new Vector3(0f, 0.06f, d * 0.5f - 0.05f), new Vector3(w, 0.12f, 0.1f));
                    flat.Box(m.Sandstone, c + new Vector3(0f, 0.06f, -d * 0.5f + 0.05f), new Vector3(w, 0.12f, 0.1f));
                    flat.Box(m.Sandstone, c + new Vector3(w * 0.5f - 0.05f, 0.06f, 0f), new Vector3(0.1f, 0.12f, d - 0.2f));
                    flat.Box(m.Sandstone, c + new Vector3(-w * 0.5f + 0.05f, 0.06f, 0f), new Vector3(0.1f, 0.12f, d - 0.2f));
                    flat.Box(soil, c + new Vector3(0f, 0.035f, 0f), new Vector3(w - 0.2f, 0.07f, d - 0.2f));
                    solid.Box(hedge, c + new Vector3(0f, 0.13f, d * 0.5f - 0.18f), new Vector3(w - 0.2f, 0.26f, 0.16f), 0.6f);
                    solid.Box(hedge, c + new Vector3(0f, 0.13f, -d * 0.5f + 0.18f), new Vector3(w - 0.2f, 0.26f, 0.16f), 0.6f);
                    solid.Box(hedge, c + new Vector3(w * 0.5f - 0.18f, 0.13f, 0f), new Vector3(0.16f, 0.26f, d - 0.52f), 0.6f);
                    solid.Box(hedge, c + new Vector3(-w * 0.5f + 0.18f, 0.13f, 0f), new Vector3(0.16f, 0.26f, d - 0.52f), 0.6f);
                    for (int i = 0; i < 9; i++)
                        DetailShapes.Ellipsoid(flat.Of(leaves), c + new Vector3(DetailShapes.Range(frnd, -0.5f, 0.5f), 0.1f, DetailShapes.Range(frnd, -0.75f, 0.75f)), DetailShapes.Range(frnd, 0.15f, 0.22f), 0.09f, 6, 3);
                    const int nx = 8, nz = 11;
                    for (int i = 0; i < nx; i++)
                        for (int j = 0; j < nz; j++)
                        {
                            int ring = Mathf.Min(Mathf.Min(i, nx - 1 - i), Mathf.Min(j, nz - 1 - j));
                            var mat = ring == 0 ? marigold[(i + j) % 2] : ring == 1 ? rose : heart[(bed + i) % 2];
                            var p = c + new Vector3(-0.56f + i * 0.16f + DetailShapes.Range(frnd, -0.04f, 0.04f), DetailShapes.Range(frnd, 0.17f, 0.3f), -0.8f + j * 0.16f + DetailShapes.Range(frnd, -0.04f, 0.04f));
                            flat.Of(mat).AddSphere(p, DetailShapes.Range(frnd, 0.038f, 0.055f), 5, 3);
                        }
                    bed++;
                }
        }

        /// <summary>
        /// A slender Italian cypress: a short trunk, a tight column of foliage (tucked at the foot, fullest a quarter of
        /// the way up, a long taper to a rounded tip) and clumps standing proud of it so the outline and the sunlit side
        /// break up like real foliage.
        /// </summary>
        static void Cypress(KitBatch k, Material bark, Material body, Material clump, Vector3 b, System.Random rnd)
        {
            float h = DetailShapes.Range(rnd, 4.7f, 5.6f), R = DetailShapes.Range(rnd, 0.5f, 0.58f), seed = DetailShapes.Range(rnd, 0f, 10f);
            const float y0 = 0.32f;
            float Rad(float t)
            {
                float r = t < 0.25f
                    ? R * Mathf.Lerp(0.55f, 1f, Mathf.SmoothStep(0f, 1f, t / 0.25f))
                    : R * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((t - 0.25f) / 0.75f, 1.7f))) * (1f - 0.5f * (t - 0.25f) / 0.75f);
                return r * (1f + 0.05f * Mathf.Sin(t * 23f + seed));
            }
            float Y(float t) => y0 + 0.08f + t * (h - y0 - 0.08f);
            k.Of(bark).AddFrustum(b, 0.1f, 0.07f, 0.5f, 7);
            var prof = new List<Vector2> { new Vector2(0.001f, y0) };
            const int rows = 16;
            for (int r = 0; r <= rows; r++)
            {
                float t = r / (float)rows;
                prof.Add(new Vector2(Mathf.Max(0.001f, Rad(t)), Y(t)));
            }
            k.Of(body).AddLathe(b, prof, 11, 0.55f);
            const int n = 16;
            for (int c = 0; c < n; c++)
            {
                float t = 0.05f + 0.83f * (c + (float)rnd.NextDouble()) / n;
                float ang = DetailShapes.Range(rnd, 0f, Mathf.PI * 2f), rad = Rad(t);
                float a = Mathf.Max(0.09f, rad * 0.42f);
                var p = b + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (rad * 0.78f) + Vector3.up * Y(t);
                DetailShapes.Ellipsoid(k.Of(clump), p, a, a * 2.1f, 7, 4, 0.55f);
            }
        }

        /// <summary>
        /// Lantern posts along the walks, the gate lanterns at the threshold, the two marble benches, fallen petals. Six of
        /// the lanterns carry real lights (the pairs at z -1.3 and 8.9 and the gate pair); the middle pair only glows.
        /// </summary>
        void BuildFurniture(Transform g, KitBatch solid, KitBatch flat, GardenMats m)
        {
            var glow = Mats.Emissive(new Color(1f, 0.78f, 0.5f), new Color(1.45f, 0.8f, 0.34f));
            foreach (float sx in new[] { -1f, 1f })
                foreach (float z in new[] { -1.3f, 2.1f, 8.9f }) // the cross axis (and the lawn seam) stay open at z 5.5
                {
                    var b = new Vector3(sx * 4.25f, 0f, z);
                    Lantern(solid, m, glow, b);
                    if (z != 2.1f) AddPointLight(g, b + Vector3.up * 1.6f, new Color(1f, 0.64f, 0.34f), 1.5f, 5f);
                }
            BuildThreshold(g, solid, flat, m, glow);
            // Backless marble benches with waisted supports and a small carnelian inlay.
            foreach (var p in new[] { new Vector3(-3.6f, 0f, -2.4f), new Vector3(3.6f, 0f, 11.2f) })
            {
                solid.Box(m.Marble, p + Vector3.up * 0.415f, new Vector3(0.56f, 0.07f, 1.7f), 1.5f);
                solid.Box(m.Marble, p + Vector3.up * 0.365f, new Vector3(0.5f, 0.03f, 1.62f), 1.5f);
                foreach (float dz in new[] { -0.6f, 0.6f })
                {
                    var c = p + new Vector3(0f, 0f, dz);
                    solid.Box(m.Marble, c + Vector3.up * 0.04f, new Vector3(0.5f, 0.08f, 0.22f));
                    solid.Box(m.Marble, c + Vector3.up * 0.19f, new Vector3(0.34f, 0.22f, 0.14f));
                    solid.Box(m.Marble, c + Vector3.up * 0.325f, new Vector3(0.46f, 0.05f, 0.2f));
                    foreach (float side in new[] { -1f, 1f })
                        flat.Box(m.Carnelian, c + new Vector3(side * 0.171f, 0.19f, 0f), new Vector3(0.004f, 0.08f, 0.08f), Quaternion.Euler(45f, 0f, 0f));
                }
            }
            // Petals: afloat on the channel (never on the reflected figure) and fallen along the walks.
            var petal = new[] { Mats.Lit(new Color(0.97f, 0.64f, 0.7f), 0.35f), Mats.Lit(new Color(0.86f, 0.3f, 0.42f), 0.35f) };
            var rnd = new System.Random(31);
            for (int i = 0; i < 90; i++)
            {
                var p = new Vector3(DetailShapes.Range(rnd, -1.02f, 1.02f), 0.1015f, DetailShapes.Range(rnd, -2.9f, 12.9f));
                if (Mathf.Abs(p.z - 8.5f) < 1.1f && Mathf.Abs(p.x - 0.3f) < 0.5f) continue;
                flat.Box(petal[i % 3 == 0 ? 1 : 0], p, new Vector3(0.035f, 0.002f, 0.024f), Quaternion.Euler(0f, DetailShapes.Range(rnd, 0f, 180f), 0f));
            }
            for (int i = 0; i < 150; i++)
            {
                float sx = i % 2 == 0 ? -1f : 1f;
                var p = new Vector3(sx * DetailShapes.Range(rnd, 2.0f, 4.3f), 0.0095f, DetailShapes.Range(rnd, -7f, 14.8f));
                flat.Box(petal[i % 4 == 0 ? 1 : 0], p, new Vector3(0.035f, 0.002f, 0.024f), Quaternion.Euler(0f, DetailShapes.Range(rnd, 0f, 180f), 0f));
            }
        }

        /// <summary>
        /// Where the garden opens onto the long walk to the domed pavilion (the vista's marble walks and channel start at
        /// z = 15, level with this walk): a marble threshold band inlaid across the walk, bridging the gap between the two
        /// channels, and a pair of lit gate lanterns on sandstone piers either side of it, so the way on reads from the
        /// arrival. The piers stand on the lawn edge, off the walk, and keep small colliders.
        /// </summary>
        void BuildThreshold(Transform g, KitBatch solid, KitBatch flat, GardenMats m, Material glow)
        {
            const float z0 = 14.35f, z1 = 15.0f;
            float zm = (z0 + z1) * 0.5f;
            flat.Box(m.Marble, new Vector3(0f, 0.007f, zm), new Vector3(9f, 0.014f, z1 - z0), 1.5f);
            foreach (float z in new[] { z0 + 0.04f, z1 - 0.04f })
                flat.Box(m.Onyx, new Vector3(0f, 0.0145f, z), new Vector3(9f, 0.003f, 0.03f));
            for (int i = 0; i < 15; i++)
                flat.Box(m.Inlay[i % 3], new Vector3(-4.2f + i * 0.6f, 0.0145f, zm), new Vector3(0.14f, 0.003f, 0.14f), Quaternion.Euler(0f, 45f, 0f));
            foreach (float sx in new[] { -1f, 1f })
            {
                var p = new Vector3(sx * 4.9f, 0f, 15.2f);
                solid.Box(m.Sandstone, p + Vector3.up * 0.06f, new Vector3(0.68f, 0.12f, 0.68f));
                solid.Box(m.Sandstone, p + Vector3.up * 0.62f, new Vector3(0.5f, 1.0f, 0.5f), 1.5f);
                solid.Box(m.Marble, p + Vector3.up * 0.42f, new Vector3(0.52f, 0.05f, 0.52f));
                solid.Box(m.Marble, p + Vector3.up * 1.15f, new Vector3(0.6f, 0.06f, 0.6f));
                // An inlaid lozenge on the two faces seen from the garden: toward the walk and toward the arrival.
                var inlay = m.Inlay[sx > 0 ? 0 : 2];
                flat.Box(inlay, p + new Vector3(-sx * 0.252f, 0.78f, 0f), new Vector3(0.005f, 0.12f, 0.12f), Quaternion.Euler(45f, 0f, 0f));
                flat.Box(inlay, p + new Vector3(0f, 0.78f, -0.252f), new Vector3(0.12f, 0.12f, 0.005f), Quaternion.Euler(0f, 0f, 45f));
                Lantern(solid, m, glow, p + Vector3.up * 1.18f);
                AddPointLight(g, p + Vector3.up * 2.78f, new Color(1f, 0.64f, 0.34f), 1.6f, 6f);
                var col = new GameObject("GatePier");
                col.transform.SetParent(g, false);
                col.transform.localPosition = p + Vector3.up * 0.6f;
                col.AddComponent<BoxCollider>().size = new Vector3(0.5f, 1.2f, 0.5f);
            }
        }

        /// <summary>A lantern post: sandstone shaft on a marble-capped base, a brass hexagonal lantern with a cupola.</summary>
        static void Lantern(KitBatch k, GardenMats m, Material glow, Vector3 b)
        {
            k.Box(m.Sandstone, b + Vector3.up * 0.08f, new Vector3(0.32f, 0.16f, 0.32f));
            k.Box(m.Marble, b + Vector3.up * 0.18f, new Vector3(0.26f, 0.04f, 0.26f));
            k.Box(m.Sandstone, b + Vector3.up * 0.79f, new Vector3(0.15f, 1.18f, 0.15f), 1.5f);
            k.Box(m.Marble, b + Vector3.up * 0.9f, new Vector3(0.17f, 0.03f, 0.17f));
            k.Box(m.Marble, b + Vector3.up * 1.41f, new Vector3(0.24f, 0.06f, 0.24f));
            k.Of(m.Brass).AddCylinder(b + Vector3.up * 1.44f, 0.13f, 0.03f, 6);
            k.Of(glow).AddCylinder(b + Vector3.up * 1.47f, 0.1f, 0.24f, 6);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                k.Of(m.Brass).AddCylinder(b + new Vector3(Mathf.Cos(a) * 0.104f, 1.47f, Mathf.Sin(a) * 0.104f), 0.009f, 0.24f, 4);
            }
            k.Of(m.Brass).AddFrustum(b + Vector3.up * 1.71f, 0.15f, 0.035f, 0.11f, 6);
            k.Of(m.Brass).AddSphere(b + Vector3.up * 1.85f, 0.035f, 6, 4);
            k.Of(m.Brass).AddTorus(b + Vector3.up * 1.92f, 0.03f, 0.007f, 8, 4, Quaternion.Euler(90f, 0f, 0f));
        }
    }
}

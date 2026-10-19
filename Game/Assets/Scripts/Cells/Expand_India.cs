using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Agra grows a walled water garden east of the long walk (the expansion pass: "expand the country maps ... more
    /// interactive animative and cool"). A sandstone path leaves the walk's east marble strip through a gap in the cypress
    /// row (z 34.4 / 37.8) to a pointed-arch gateway; inside, a char-bagh in miniature: a lotus basin where the walks
    /// cross, a short rill with brass jets running to a marble chadar under a chhatri on its plinth, cypress rows along
    /// the walls, two parterres with a flowering champa that drops petals over a marble bench, lotus urns, silk pennants,
    /// hanging lanterns and sparrows. A stone valve on the plinth starts the fountains one by one (the set piece). The
    /// garden (x 8.5..21.5, z 26..46) is far from the reflecting pool and its tank; the cell's east bound is opened only
    /// between its walls. Walls cast no shadow, like the main garden's (a low sun would shade the whole garden).
    /// </summary>
    public static class Expand_India
    {
        const float GX0 = 8.5f, GX1 = 21.5f, GZ0 = 26f, GZ1 = 46f;   // wall centre lines (0.5 m thick)
        const float GateZ = 36.1f, AxisX = 15f, WallH = 2.6f, Top = 0.45f;  // gate on the west wall; the plinth's top
        static readonly Color LampColor = new Color(1f, 0.72f, 0.42f);
        static readonly Color Lantern = new Color(1f, 0.64f, 0.34f);

        // Bulbous Mughal dome, radius 1 at the base (as Detail_AgraArch).
        static readonly Vector2[] s_Dome =
        {
            new Vector2(0.98f, 0f), new Vector2(1.07f, 0.1f), new Vector2(1.12f, 0.26f), new Vector2(1.09f, 0.44f), new Vector2(0.98f, 0.62f),
            new Vector2(0.8f, 0.8f), new Vector2(0.56f, 0.97f), new Vector2(0.32f, 1.12f), new Vector2(0.14f, 1.27f), new Vector2(0.05f, 1.38f),
            new Vector2(0.001f, 1.44f),
        };

        sealed class Pal
        {
            public Material Marble, Sandstone, Recess, Brass, Wood, Carnelian, Jade, Lapis, Onyx, Grass, Hedge, Bark, Leaf, LeafLit, Soil, Glow, Wet, Spray, Blossom, BlossomPink, Lotus, LotusLeaf;
            public Material[] Flowers;
            public Material[] Inlay => new[] { Carnelian, Jade, Lapis };
        }

        public static void Build(AgraCell cell, Transform parent)
        {
            var root = new GameObject("Expand_India").transform;
            root.SetParent(cell.transform, false);
            try
            {
                var m = Palette();
                var solid = new KitBatch();  // casts shadows: trees, the chhatri, urns, lanterns, the bench
                var flat = new KitBatch();   // no shadows: walls, paving, beds, inlay, water stone
                Ground(root, flat, m);
                Walls(root, flat, m);
                Gateway(root, flat, solid, m);
                Approach(root, flat, solid, m);
                Basin(root, solid, flat, m);
                Pavilion(root, solid, flat, m);
                Planting(root, solid, flat, m);
                solid.Bake(root, "WaterGarden");
                flat.Bake(root, "WaterGardenStone", false);
                Life(root);
                Lamps(root);
                Postcards(root);
                ShotPoint(root, new Vector3(AxisX, 0f, 26.8f), 0f);
                OpenBounds(cell.transform); // last: if anything above failed, the bounds stay shut
            }
            catch (System.Exception e)
            {
                Debug.LogError("[TS-Expand] Agra expansion failed: " + e);
            }
        }

        static Pal Palette() => new Pal
        {
            // The main garden's own materials (same parameters, so the same cached instances): pale tints on textures.
            Marble = Mats.Lit(new Color(0.92f, 0.9f, 0.88f), 0.6f, 0f, "marble_white"),
            Sandstone = Mats.Lit(new Color(0.9f, 0.55f, 0.55f), 0.25f, 0f, "sandstone_blocks"),
            Recess = Mats.Lit(new Color(0.52f, 0.3f, 0.26f), 0.2f, 0f, "sandstone_blocks"),
            Brass = Mats.Lit(new Color(0.78f, 0.6f, 0.32f), 0.7f, 0.9f, "metal_brushed"),
            Wood = Mats.Lit(new Color(0.42f, 0.24f, 0.14f), 0.4f, 0f, "wood_dark"),
            Carnelian = Mats.Lit(new Color(0.66f, 0.1f, 0.07f), 0.65f),
            Jade = Mats.Lit(new Color(0.1f, 0.45f, 0.32f), 0.65f),
            Lapis = Mats.Lit(new Color(0.1f, 0.22f, 0.62f), 0.65f),
            Onyx = Mats.Lit(new Color(0.05f, 0.05f, 0.06f), 0.7f),
            Grass = Mats.Lit(new Color(0.9f, 1f, 0.72f), 0.15f, 0f, "grass_short"),
            Hedge = Mats.Lit(new Color(0.92f, 1f, 0.78f), 0.2f, 0f, "grass_short"),
            Bark = Mats.Lit(new Color(0.85f, 0.75f, 0.65f), 0.2f, 0f, "wood_dark"),
            Leaf = Mats.Lit(new Color(0.6f, 0.86f, 0.74f), 0.18f, 0f, "grass_short"),
            LeafLit = Mats.Lit(new Color(0.84f, 1f, 0.84f), 0.22f, 0f, "grass_short"),
            Soil = Mats.Lit(new Color(0.3f, 0.2f, 0.14f), 0.1f, 0f, "sand_ripples"),
            Glow = Mats.Emissive(new Color(1f, 0.78f, 0.5f), new Color(1.45f, 0.8f, 0.34f)),
            Wet = Mats.Lit(new Color(0.1f, 0.12f, 0.12f), 0.85f),
            Spray = Mats.Glass(new Color(0.82f, 0.92f, 0.97f, 0.42f), 0.96f, new Color(0.16f, 0.2f, 0.24f)),
            Blossom = Mats.Lit(new Color(0.97f, 0.95f, 0.9f), 0.3f),
            BlossomPink = Mats.Lit(new Color(0.97f, 0.64f, 0.7f), 0.35f),
            Lotus = Mats.Lit(new Color(0.95f, 0.55f, 0.68f), 0.35f),
            LotusLeaf = Mats.Lit(new Color(0.22f, 0.45f, 0.2f), 0.4f),
            Flowers = new[]
            {
                Mats.Lit(new Color(1f, 0.52f, 0.06f), 0.3f), Mats.Lit(new Color(1f, 0.74f, 0.12f), 0.3f), Mats.Lit(new Color(0.8f, 0.06f, 0.12f), 0.35f),
                Mats.Lit(new Color(0.96f, 0.94f, 0.88f), 0.3f), Mats.Lit(new Color(0.86f, 0.2f, 0.52f), 0.3f),
            },
        };

        // ------------------------------------------------------------------ ground, walls and the gate
        static void Ground(Transform root, KitBatch k, Pal m)
        {
            float ix0 = GX0 + 0.25f, ix1 = GX1 - 0.25f, iz0 = GZ0 + 0.25f, iz1 = GZ1 - 0.25f;
            // The cell's lit lawn, 2 cm over the vista's, and walkable.
            var lc = new Vector3((ix0 + ix1) * 0.5f, -0.06f, (iz0 + iz1) * 0.5f);
            var ls = new Vector3(ix1 - ix0, 0.06f, iz1 - iz0);
            k.Box(m.Grass, lc, ls, 1.5f);
            Floor(root, "GardenLawn", lc, ls, "grass");
            // Walks: the cross walk from the gate to the basin and on to the east wall, the main walk south of the basin,
            // and one each side of the rill.
            Walk(root, k, m, GX0 - 0.5f, 13.0f, 35.3f, 36.9f, true); // from under the gate's arch
            Walk(root, k, m, 17.0f, ix1, 35.3f, 36.9f, true);
            Walk(root, k, m, 14.2f, 15.8f, iz0, 34.1f, false);
            Walk(root, k, m, 13.6f, 14.55f, 38.1f, 40.15f, false);
            Walk(root, k, m, 15.45f, 16.4f, 38.1f, 40.15f, false);
        }

        /// <summary>A sandstone walk (top at 0) with marble edges along its length and a line of inlaid lozenges.</summary>
        static void Walk(Transform root, KitBatch k, Pal m, float x0, float x1, float z0, float z1, bool alongX)
        {
            var c = new Vector3((x0 + x1) * 0.5f, -0.02f, (z0 + z1) * 0.5f);
            var s = new Vector3(x1 - x0, 0.04f, z1 - z0);
            k.Box(m.Sandstone, c, s, 1.6f);
            Floor(root, "GardenWalk", c, s, "stone");
            if (alongX)
            {
                foreach (var z in new[] { z0 + 0.04f, z1 - 0.04f }) k.Box(m.Marble, new Vector3(c.x, 0.002f, z), new Vector3(x1 - x0, 0.004f, 0.07f));
                for (float x = x0 + 0.4f; x < x1 - 0.2f; x += 0.6f) k.Box(m.Inlay[Mathf.RoundToInt(x * 10f) % 3], new Vector3(x, 0.003f, c.z), new Vector3(0.16f, 0.006f, 0.16f), Quaternion.Euler(0f, 45f, 0f));
            }
            else
            {
                foreach (var x in new[] { x0 + 0.04f, x1 - 0.04f }) k.Box(m.Marble, new Vector3(x, 0.002f, c.z), new Vector3(0.07f, 0.004f, z1 - z0));
                if (x1 - x0 > 1.2f)
                    for (float z = z0 + 0.4f; z < z1 - 0.2f; z += 0.6f) k.Box(m.Inlay[Mathf.RoundToInt(z * 10f) % 3], new Vector3(c.x, 0.003f, z), new Vector3(0.16f, 0.006f, 0.16f), Quaternion.Euler(0f, 45f, 0f));
            }
        }

        /// <summary>The enclosure: red sandstone walls with a marble dado, coping and merlons, blind niches inside and out.</summary>
        static void Walls(Transform root, KitBatch k, Pal m)
        {
            WallRun(root, k, m, new Vector3(GX0, 0f, GZ0 - 0.25f), new Vector3(GX0, 0f, 34.0f), Vector3.right);
            WallRun(root, k, m, new Vector3(GX0, 0f, 38.2f), new Vector3(GX0, 0f, GZ1 + 0.25f), Vector3.right);
            WallRun(root, k, m, new Vector3(GX1, 0f, GZ0 - 0.25f), new Vector3(GX1, 0f, GZ1 + 0.25f), Vector3.left);
            WallRun(root, k, m, new Vector3(GX0 - 0.25f, 0f, GZ0), new Vector3(GX1 + 0.25f, 0f, GZ0), Vector3.forward);
            WallRun(root, k, m, new Vector3(GX0 - 0.25f, 0f, GZ1), new Vector3(GX1 + 0.25f, 0f, GZ1), Vector3.back);
            foreach (var z in new[] { 28.4f, 31.0f, 41.2f, 43.8f })
            {
                Niche(k, m, new Vector3(GX0 + 0.25f, 0.3f, z), Vector3.right, 1.1f, 1.2f, true);
                Niche(k, m, new Vector3(GX0 - 0.25f, 0.3f, z), Vector3.left, 1.1f, 1.2f, true);
                Niche(k, m, new Vector3(GX1 - 0.25f, 0.3f, z), Vector3.left, 1.1f, 1.2f, true);
            }
            foreach (var x in new[] { 10.8f, 13.3f, 16.7f, 19.2f }) Niche(k, m, new Vector3(x, 0.3f, GZ0 + 0.25f), Vector3.forward, 1.1f, 1.2f, true);
            foreach (var x in new[] { 10.6f, 19.4f }) Niche(k, m, new Vector3(x, 0.3f, GZ1 - 0.25f), Vector3.back, 1.1f, 1.2f, true);
            // The east wall at the end of the cross walk: a jharokha, a little balcony window with a jali, over a bench.
            var j = new Vector3(GX1 - 0.25f, 0f, GateZ);
            k.Box(m.Marble, j + new Vector3(-0.2f, 1.25f, 0f), new Vector3(0.4f, 0.08f, 1.5f));
            k.Box(m.Recess, j + new Vector3(-0.01f, 1.85f, 0f), new Vector3(0.02f, 1.1f, 1.1f));
            for (int r = 0; r < 5; r++)
                for (int c = 0; c < 5; c++)
                    k.Box(m.Marble, j + new Vector3(-0.025f, 1.43f + r * 0.21f, -0.42f + c * 0.21f), new Vector3(0.02f, 0.12f, 0.12f), Quaternion.Euler(45f, 0f, 0f));
            foreach (var dz in new[] { -0.62f, 0.62f }) k.Box(m.Sandstone, j + new Vector3(-0.2f, 1.85f, dz), new Vector3(0.12f, 1.1f, 0.12f));
            k.Box(m.Sandstone, j + new Vector3(-0.32f, 2.45f, 0f), new Vector3(0.62f, 0.05f, 1.6f), Quaternion.Euler(0f, 0f, 15f));
            k.Box(m.Marble, j + new Vector3(-0.3f, 0.42f, 0f), new Vector3(0.5f, 0.06f, 1.4f));
            foreach (var dz in new[] { -0.55f, 0.55f }) k.Box(m.Marble, j + new Vector3(-0.3f, 0.2f, dz), new Vector3(0.36f, 0.4f, 0.16f));
            Wall(root, "Bench", j + new Vector3(-0.3f, 0.22f, 0f), new Vector3(0.5f, 0.44f, 1.4f));
        }

        static void WallRun(Transform root, KitBatch k, Pal m, Vector3 a, Vector3 b, Vector3 inner)
        {
            var d = b - a;
            float len = d.magnitude;
            var dir = d / len;
            var q = Quaternion.LookRotation(dir, Vector3.up);
            var mid = (a + b) * 0.5f;
            k.Box(m.Sandstone, mid + Vector3.up * (WallH * 0.5f - 0.025f), new Vector3(0.5f, WallH + 0.05f, len), q, 2.5f);
            k.Box(m.Marble, mid + inner * 0.27f + Vector3.up * 0.2f, new Vector3(0.04f, 0.44f, len - 0.5f), q);
            k.Box(m.Marble, mid - inner * 0.27f + Vector3.up * 0.2f, new Vector3(0.04f, 0.44f, len), q);
            k.Box(m.Recess, mid + inner * 0.28f + Vector3.up * (WallH - 0.18f), new Vector3(0.06f, 0.1f, len - 0.5f), q);
            k.Box(m.Marble, mid + Vector3.up * (WallH + 0.05f), new Vector3(0.7f, 0.1f, len + 0.2f), q);
            Parapet(k, m, a + Vector3.up * (WallH + 0.1f), b + Vector3.up * (WallH + 0.1f));
            Wall(root, "Bound", mid + Vector3.up * 1.6f, new Vector3(0.5f, 3.2f, len), q);
        }

        /// <summary>
        /// The gateway on the west wall: a pishtaq (x 8..9, z 34..38.2, 4.6 m) round a 2 m pointed arch, marble outlines,
        /// an alfiz frame, roundels and a band of lozenges, merlons, two small chhatris, and the studded doors folded back
        /// inside. A brass lantern hangs in the arch.
        /// </summary>
        static void Gateway(Transform root, KitBatch flat, KitBatch solid, Pal m)
        {
            const float x0 = 8.0f, x1 = 9.0f, za = 34.0f, zb = 38.2f, H = 4.6f, o0 = 35.1f, o1 = 37.1f, spring = 2.4f, lintel = 3.9f;
            float cx = (x0 + x1) * 0.5f;
            flat.Box(m.Sandstone, new Vector3(cx, (H - 0.05f) * 0.5f, (za + o0) * 0.5f), new Vector3(1f, H + 0.05f, o0 - za), 2.5f);
            flat.Box(m.Sandstone, new Vector3(cx, (H - 0.05f) * 0.5f, (o1 + zb) * 0.5f), new Vector3(1f, H + 0.05f, zb - o1), 2.5f);
            flat.Box(m.Sandstone, new Vector3(cx, (lintel + H) * 0.5f, GateZ), new Vector3(1f, H - lintel, o1 - o0), 2.5f);
            // The spandrels: the wall between the arch and the lintel, extruded through the gate's depth.
            const int segs = 9;
            var arch = DetailShapes.PointedArch(o1 - o0, spring, 0.3f, segs);
            float hw = (o1 - o0) * 0.5f;
            var span = new List<Vector2> { new Vector2(hw, lintel), new Vector2(-hw, lintel) };
            for (int i = 1 + 2 * segs; i >= 1; i--) span.Add(arch[i]);
            flat.Of(m.Sandstone).AddExtrudedXY(span, 1f, new Vector3(cx, 0f, GateZ), Quaternion.Euler(0f, 90f, 0f), 2.5f);
            foreach (var (fx, n) in new[] { (x0, Vector3.left), (x1, Vector3.right) })
            {
                var fn = n;
                float faceX = fx;
                Vector3 W(Vector2 v) => new Vector3(faceX, v.y, GateZ - v.x) + fn * 0.02f;
                DetailShapes.Outline(flat, m.Marble, arch, W, fn, 0.14f, 0.05f);
                if (fn.x > 0f) continue;
                // The outer face: alfiz frame, roundels in the spandrels, a band of lozenges.
                foreach (var (w, mat, wd) in new[] { (1.42f, m.Marble, 0.14f), (1.3f, m.Onyx, 0.035f) })
                {
                    DetailShapes.PlaneStrip(flat, mat, W(new Vector2(-w, 0.2f)), W(new Vector2(-w, 4.1f)), fn, wd, 0.04f);
                    DetailShapes.PlaneStrip(flat, mat, W(new Vector2(w, 0.2f)), W(new Vector2(w, 4.1f)), fn, wd, 0.04f);
                    DetailShapes.PlaneStrip(flat, mat, W(new Vector2(-w - wd * 0.5f, 4.1f)), W(new Vector2(w + wd * 0.5f, 4.1f)), fn, wd, 0.04f);
                }
                foreach (var u in new[] { -0.98f, 0.98f })
                {
                    var c = W(new Vector2(u, 3.45f));
                    flat.Of(m.Marble).AddDisc(c, fn, 0.22f, 16);
                    flat.Of(m.Lapis).AddDisc(c + fn * 0.006f, fn, 0.14f, 14);
                    flat.Of(m.Carnelian).AddDisc(c + fn * 0.012f, fn, 0.06f, 10);
                }
                for (int i = 0; i < 9; i++)
                    flat.Box(i % 2 == 0 ? m.Marble : m.Jade, W(new Vector2(-1.2f + i * 0.3f, 4.35f)), new Vector3(0.03f, 0.15f, 0.15f), Quaternion.Euler(45f, 0f, 0f));
            }
            flat.Box(m.Marble, new Vector3(cx, H + 0.05f, (za + zb) * 0.5f), new Vector3(1.15f, 0.1f, zb - za + 0.15f));
            Parapet(flat, m, new Vector3(cx, H + 0.1f, za + 0.9f), new Vector3(cx, H + 0.1f, zb - 0.9f));
            Chhatri(solid, m, new Vector3(cx, H + 0.1f, za + 0.42f), 0.42f);
            Chhatri(solid, m, new Vector3(cx, H + 0.1f, zb - 0.42f), 0.42f);
            // Studded doors folded back against the inside of the gate.
            foreach (var (z0, z1) in new[] { (o0 - 1.0f, o0), (o1, o1 + 1.0f) })
            {
                solid.Box(m.Wood, new Vector3(x1 + 0.04f, 1.3f, (z0 + z1) * 0.5f), new Vector3(0.06f, 2.6f, z1 - z0), 1.2f);
                for (int r = 0; r < 7; r++)
                    for (int c = 0; c < 3; c++)
                        solid.Box(m.Brass, new Vector3(x1 + 0.08f, 0.3f + r * 0.36f, z0 + 0.2f + c * 0.3f), new Vector3(0.03f, 0.05f, 0.05f));
            }
            Wall(root, "Bound", new Vector3(cx, 1.75f, (za + o0) * 0.5f), new Vector3(1f, 3.5f, o0 - za));
            Wall(root, "Bound", new Vector3(cx, 1.75f, (o1 + zb) * 0.5f), new Vector3(1f, 3.5f, zb - o1));

            // A brass lantern hanging in the arch: a real light, swaying a little.
            var pivot = new GameObject("GateLantern").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = new Vector3(cx, 3.64f, GateZ);
            HangingLantern(pivot, m, 0.38f);
            var l = PointLight(pivot, new Vector3(0f, -0.38f - 0.21f, 0f), Lantern, 1.5f, 5.5f);
            Flicker.Add(l, 0.06f, 3f);
            WindSway.Add(pivot, 3.5f, 2.6f);
        }

        /// <summary>The path from the long walk's east strip to the gate, between lantern posts.</summary>
        static void Approach(Transform root, KitBatch flat, KitBatch solid, Pal m)
        {
            Walk(root, flat, m, 4.5f, 8.0f, 35.3f, 36.9f, true);
            foreach (var z in new[] { 34.55f, 37.65f })
            {
                var b = new Vector3(7.5f, -0.05f, z);
                LanternPost(solid, m, b);
                Wall(root, "LanternPost", b + Vector3.up * 0.8f, new Vector3(0.32f, 1.6f, 0.32f));
            }
        }

        // ------------------------------------------------------------------ the basin, the rill and the pavilion
        static void Basin(Transform root, KitBatch solid, KitBatch flat, Pal m)
        {
            // Marble paving where the walks cross, with an onyx border.
            var pc = new Vector3(AxisX, -0.02f, GateZ);
            flat.Box(m.Marble, pc, new Vector3(4f, 0.04f, 4f), 1.5f);
            Floor(root, "BasinPaving", pc, new Vector3(4f, 0.04f, 4f), "stone");
            foreach (var (c, s) in new[] { (new Vector3(AxisX, 0.002f, GateZ - 1.85f), new Vector3(3.7f, 0.004f, 0.035f)), (new Vector3(AxisX, 0.002f, GateZ + 1.85f), new Vector3(3.7f, 0.004f, 0.035f)),
                                          (new Vector3(AxisX - 1.85f, 0.002f, GateZ), new Vector3(0.035f, 0.004f, 3.7f)), (new Vector3(AxisX + 1.85f, 0.002f, GateZ), new Vector3(0.035f, 0.004f, 3.7f)) })
                flat.Box(m.Onyx, c, s);
            // The basin: a marble rim too high to step over, an inlaid coping, dark stone under the water.
            const float half = 1.1f, rimW = 0.2f, rimH = 0.35f;
            foreach (var (c, s) in new[] { (new Vector3(AxisX, rimH * 0.5f, GateZ - half - rimW * 0.5f), new Vector3(2f * half + 2f * rimW, rimH, rimW)),
                                          (new Vector3(AxisX, rimH * 0.5f, GateZ + half + rimW * 0.5f), new Vector3(2f * half + 2f * rimW, rimH, rimW)),
                                          (new Vector3(AxisX - half - rimW * 0.5f, rimH * 0.5f, GateZ), new Vector3(rimW, rimH, 2f * half)),
                                          (new Vector3(AxisX + half + rimW * 0.5f, rimH * 0.5f, GateZ), new Vector3(rimW, rimH, 2f * half)) })
            {
                solid.Box(m.Marble, c, s, 1.5f);
                Wall(root, "BasinRim", c, s);
            }
            for (int i = 0; i < 4; i++)
            {
                var r = Quaternion.Euler(0f, i * 90f, 0f);
                for (int j = 0; j < 7; j++)
                    flat.Box(m.Inlay[(i + j) % 3], new Vector3(AxisX, rimH + 0.001f, GateZ) + r * new Vector3(-0.9f + j * 0.3f, 0f, half + rimW * 0.5f), new Vector3(0.07f, 0.004f, 0.07f), r * Quaternion.Euler(0f, 45f, 0f));
            }
            flat.Box(m.Wet, new Vector3(AxisX, 0.004f, GateZ), new Vector3(2f * half, 0.008f, 2f * half));
            var water = Kit.Water(root, new Vector3(AxisX, 0.3f, GateZ), new Vector2(2f * half, 2f * half), new Color(0.1f, 0.16f, 0.2f), 0.95f);
            water.name = "BasinWater";
            // Lotus pads and two flowers afloat, each bobbing on its own.
            var rnd = new System.Random(36);
            for (int i = 0; i < 8; i++)
            {
                var p = new Vector3(AxisX + DetailShapes.Range(rnd, -0.85f, 0.85f), 0.315f, GateZ + DetailShapes.Range(rnd, -0.85f, 0.85f));
                if (Mathf.Abs(p.x - AxisX) < 0.25f && Mathf.Abs(p.z - GateZ) < 0.25f) continue; // the jet in the middle
                var pad = new GameObject("LotusPad").transform;
                pad.SetParent(root, false);
                pad.localPosition = p;
                pad.localRotation = Quaternion.Euler(0f, DetailShapes.Range(rnd, 0f, 360f), 0f);
                var pb = new MeshBuilder();
                float r = DetailShapes.Range(rnd, 0.12f, 0.2f);
                pb.AddCylinder(Vector3.zero, r, 0.006f, 14);
                MeshFactory.FromBuilder("Pad", pb, m.LotusLeaf, pad).GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (i % 3 == 0)
                {
                    var fb = new MeshBuilder();
                    for (int k = 0; k < 7; k++)
                    {
                        var dir = Quaternion.Euler(0f, k * 51.4f, 0f) * Vector3.forward;
                        DetailShapes.Feather(fb, new Vector3(0f, 0.01f, 0f), (dir + Vector3.up * 1.3f).normalized, dir, 0.09f, 0.05f, dir, 0.25f, 3, 0.3f);
                    }
                    fb.AddSphere(new Vector3(0f, 0.03f, 0f), 0.02f, 6, 4);
                    MeshFactory.FromBuilder("Flower", fb, m.Lotus, pad);
                }
                Bob.Add(pad, 0.006f, DetailShapes.Range(rnd, 3f, 4.5f), 2.5f);
            }
            // Brass lotus urns at the outer corners of the paving.
            foreach (var c in new[] { new Vector3(12.7f, -0.03f, 33.8f), new Vector3(17.3f, -0.03f, 33.8f), new Vector3(12.7f, -0.03f, 38.4f), new Vector3(17.3f, -0.03f, 38.4f) })
            {
                Urn(solid, m, c);
                Wall(root, "Urn", c + Vector3.up * 0.32f, new Vector3(0.5f, 0.64f, 0.5f));
            }
        }

        static void Urn(KitBatch k, Pal m, Vector3 b)
        {
            var prof = new List<Vector2>
            {
                new Vector2(0.001f, 0f), new Vector2(0.16f, 0f), new Vector2(0.17f, 0.05f), new Vector2(0.24f, 0.16f), new Vector2(0.27f, 0.26f),
                new Vector2(0.24f, 0.38f), new Vector2(0.17f, 0.46f), new Vector2(0.2f, 0.52f), new Vector2(0.23f, 0.55f),
            };
            k.Of(m.Brass).AddLathe(b, prof, 16);
            k.Of(m.Wet).AddCylinder(b + Vector3.up * 0.5f, 0.2f, 0.03f, 14);
            for (int i = 0; i < 5; i++)
            {
                var dir = Quaternion.Euler(0f, i * 72f + 20f, 0f) * Vector3.forward;
                DetailShapes.Feather(k.Of(m.LotusLeaf), b + Vector3.up * 0.52f + dir * 0.05f, (dir + Vector3.up * 0.25f).normalized, Vector3.up, 0.32f, 0.2f, Vector3.down, 0.35f, 4, 0.4f);
            }
            for (int i = 0; i < 6; i++)
            {
                var dir = Quaternion.Euler(0f, i * 60f, 0f) * Vector3.forward;
                DetailShapes.Feather(k.Of(m.Lotus), b + Vector3.up * 0.6f, (dir * 0.5f + Vector3.up).normalized, dir, 0.13f, 0.07f, dir, 0.2f, 3, 0.3f);
            }
            k.Of(m.Flowers[1]).AddSphere(b + Vector3.up * 0.63f, 0.03f, 6, 4);
        }

        /// <summary>
        /// The plinth with twin flights, the chadar between them, the rill with its jets, the chhatri with its hanging
        /// lantern, two silk pennants, lantern posts at the front corners, and the stone valve (the set piece).
        /// </summary>
        static void Pavilion(Transform root, KitBatch solid, KitBatch flat, Pal m)
        {
            const float px0 = 12.8f, px1 = 17.2f, pz0 = 41.2f, pz1 = 45.4f;
            var pc = new Vector3((px0 + px1) * 0.5f, (Top - 0.05f) * 0.5f, (pz0 + pz1) * 0.5f);
            var ps = new Vector3(px1 - px0, Top + 0.05f, pz1 - pz0);
            flat.Box(m.Sandstone, new Vector3(pc.x, (Top - 0.06f - 0.05f) * 0.5f, pc.z), new Vector3(ps.x, Top - 0.06f + 0.05f, ps.z), 1.6f);
            flat.Box(m.Marble, new Vector3(pc.x, Top - 0.03f, pc.z), new Vector3(ps.x + 0.06f, 0.06f, ps.z + 0.06f), 1.5f);
            Floor(root, "Plinth", pc, ps, "stone");
            for (int i = 0; i < 13; i++)
                flat.Box(m.Inlay[i % 3], new Vector3(px0 + 0.2f + i * 0.333f, 0.22f, pz0 - 0.005f), new Vector3(0.12f, 0.12f, 0.01f), Quaternion.Euler(0f, 0f, 45f));
            // Twin flights of three 15 cm steps either side of the chadar.
            foreach (var (fx0, fx1) in new[] { (13.0f, 14.4f), (15.6f, 17.0f) })
                for (int t = 0; t < 3; t++)
                {
                    float z0 = 40.15f + t * 0.35f, h = 0.15f * (t + 1);
                    var c = new Vector3((fx0 + fx1) * 0.5f, h * 0.5f - 0.015f, (z0 + pz0) * 0.5f);
                    var s = new Vector3(fx1 - fx0, h + 0.03f, pz0 - z0);
                    flat.Box(m.Sandstone, new Vector3(c.x, (h - 0.03f - 0.03f) * 0.5f, c.z), new Vector3(s.x, h, s.z), 1.5f);
                    flat.Box(m.Marble, new Vector3(c.x, h - 0.015f, z0 + 0.175f), new Vector3(fx1 - fx0 + 0.02f, 0.03f, 0.35f), 1.5f);
                    Floor(root, "PlinthStep", c, s, "stone");
                }
            // The chadar: a carved marble chute from the plinth down into the rill, its water sheet shown when it runs.
            float pitch = Mathf.Atan2(Top - 0.2f, pz0 - 40.15f) * Mathf.Rad2Deg;
            var slope = Quaternion.Euler(-pitch, 0f, 0f);
            var chuteC = new Vector3(AxisX, (Top + 0.2f) * 0.5f - 0.03f, (pz0 + 40.15f) * 0.5f);
            float chuteL = new Vector2(Top - 0.2f, pz0 - 40.15f).magnitude;
            solid.Box(m.Marble, chuteC, new Vector3(0.8f, 0.06f, chuteL + 0.02f), slope);
            for (int i = 0; i < 8; i++)
                for (int j = 0; j < 3; j++)
                    solid.Box(m.Onyx, chuteC + slope * new Vector3(-0.25f + j * 0.25f, 0.031f, -chuteL * 0.5f + 0.08f + i * (chuteL - 0.16f) / 7f), new Vector3(0.1f, 0.006f, 0.1f), slope * Quaternion.Euler(0f, 45f, 0f));
            foreach (var sx in new[] { -0.44f, 0.44f })
                flat.Box(m.Sandstone, new Vector3(AxisX + sx, Top * 0.5f, (pz0 + 40.15f) * 0.5f), new Vector3(0.08f, Top + 0.04f, pz0 - 40.15f));
            Wall(root, "Chadar", new Vector3(AxisX, Top * 0.5f, (pz0 + 40.15f) * 0.5f), new Vector3(0.96f, Top, pz0 - 40.15f));
            var sheet = MeshFactory.Box("ChadarWater", root, chuteC + slope * new Vector3(0f, 0.045f, 0f), new Vector3(0.66f, 0.012f, chuteL), m.Spray, false, 1f, slope);
            sheet.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sheet.SetActive(false);

            // The rill: marble kerbs, dark stone under still water, from the basin's rim to the chadar.
            const float r0 = 37.4f, r1 = 40.15f;
            foreach (var sx in new[] { -0.325f, 0.325f })
                solid.Box(m.Marble, new Vector3(AxisX + sx, 0.1f, (r0 + r1) * 0.5f), new Vector3(0.15f, 0.2f, r1 - r0), 1.5f);
            flat.Box(m.Wet, new Vector3(AxisX, 0.01f, (r0 + r1) * 0.5f), new Vector3(0.5f, 0.02f, r1 - r0));
            var rill = Kit.Water(root, new Vector3(AxisX, 0.14f, (r0 + r1) * 0.5f), new Vector2(0.5f, r1 - r0), new Color(0.1f, 0.16f, 0.2f), 0.95f);
            rill.name = "RillWater";
            Wall(root, "Rill", new Vector3(AxisX, 0.2f, (r0 + r1) * 0.5f), new Vector3(0.8f, 0.4f, r1 - r0));

            // Brass jets: three along the rill, one in the basin; each a stopped particle fountain until the valve turns.
            var jetAt = new[] { new Vector3(AxisX, 0.15f, 39.85f), new Vector3(AxisX, 0.15f, 39.05f), new Vector3(AxisX, 0.15f, 38.25f), new Vector3(AxisX, 0.31f, GateZ) };
            var jets = new ParticleSystem[jetAt.Length];
            for (int i = 0; i < jetAt.Length; i++)
            {
                solid.Of(m.Brass).AddCylinder(jetAt[i] - Vector3.up * 0.02f, 0.04f, 0.025f, 10);
                solid.Of(m.Brass).AddFrustum(jetAt[i], 0.022f, 0.01f, 0.04f, 8);
                bool big = i == jetAt.Length - 1;
                jets[i] = Jet(root, jetAt[i] + Vector3.up * 0.04f, big ? 3.0f : 1.9f, big ? 3.4f : 2.3f);
            }

            // The chhatri on the plinth, column colliders, a hanging lantern (a real light) and two silk pennants.
            var cc = new Vector3(AxisX, Top, 43.3f);
            const float cs = 1.5f;
            Chhatri(solid, m, cc, cs);
            foreach (float cx in new[] { -0.72f, 0.72f })
                foreach (float cz in new[] { -0.72f, 0.72f })
                {
                    var col = new GameObject("ChhatriColumn");
                    col.transform.SetParent(root, false);
                    col.transform.localPosition = cc + new Vector3(cx * cs, 1.2f, cz * cs);
                    var cap = col.AddComponent<CapsuleCollider>();
                    cap.radius = 0.17f;
                    cap.height = 2.4f;
                }
            var lp = new GameObject("PavilionLantern").transform;
            lp.SetParent(root, false);
            lp.localPosition = cc + Vector3.up * 2.8f;
            HangingLantern(lp, m, 0.35f);
            var pl = PointLight(lp, new Vector3(0f, -0.35f - 0.21f, 0f), Lantern, 1.6f, 5f);
            Flicker.Add(pl, 0.06f, 3.2f);
            WindSway.Add(lp, 3f, 2.8f);
            var saffron = Mats.Lit(new Color(1f, 0.62f, 0.15f), 0.3f, 0f, "fabric_linen", false);
            var rose = Mats.Lit(new Color(0.95f, 0.45f, 0.55f), 0.3f, 0f, "fabric_linen", false);
            foreach (var (x, mat) in new[] { (12.95f, saffron), (17.05f, rose) })
            {
                var pb = new Vector3(x, Top, 45.2f);
                solid.Of(m.Brass).AddCylinder(pb, 0.03f, 3.2f, 8);
                solid.Of(m.Brass).AddSphere(pb + Vector3.up * 3.24f, 0.05f, 8, 6);
                ClothFlag.Create(root, "Pennant", pb + new Vector3(0f, 3.12f, -0.03f), 0.9f, 0.48f, mat, 90f, 0.1f);
            }
            foreach (var x in new[] { 12.55f, 17.45f })
            {
                var b = new Vector3(x, -0.03f, 40.9f);
                LanternPost(solid, m, b);
                Wall(root, "LanternPost", b + Vector3.up * 0.8f, new Vector3(0.32f, 1.6f, 0.32f));
            }

            // The stone valve at the head of the chadar: a sandstone post with a brass wheel that turns the water on.
            var vb = new Vector3(AxisX, Top, 41.55f);
            solid.Box(m.Sandstone, vb + Vector3.up * 0.06f, new Vector3(0.42f, 0.12f, 0.42f));
            solid.Box(m.Sandstone, vb + Vector3.up * 0.47f, new Vector3(0.3f, 0.7f, 0.3f), 1.5f);
            solid.Box(m.Marble, vb + Vector3.up * 0.85f, new Vector3(0.36f, 0.06f, 0.36f));
            solid.Box(m.Carnelian, vb + new Vector3(0f, 0.5f, -0.152f), new Vector3(0.1f, 0.1f, 0.006f), Quaternion.Euler(0f, 0f, 45f));
            solid.Of(m.Brass).AddCylinder(vb + Vector3.up * 0.88f, 0.03f, 0.1f, 8);
            Wall(root, "Valve", vb + Vector3.up * 0.45f, new Vector3(0.42f, 0.9f, 0.42f));
            var wheel = new GameObject("ValveWheel").transform;
            wheel.SetParent(root, false);
            wheel.localPosition = vb + Vector3.up * 0.98f;
            var wb = new MeshBuilder();
            wb.AddTorus(Vector3.zero, 0.22f, 0.018f, 24, 5, Quaternion.identity);
            wb.AddCylinder(new Vector3(0f, -0.03f, 0f), 0.04f, 0.06f, 10);
            for (int i = 0; i < 6; i++) wb.AddBox(Quaternion.Euler(0f, i * 60f, 0f) * new Vector3(0.11f, 0f, 0f), new Vector3(0.19f, 0.016f, 0.02f), Quaternion.Euler(0f, i * 60f, 0f));
            for (int i = 0; i < 6; i++) wb.AddSphere(Quaternion.Euler(0f, i * 60f + 30f, 0f) * new Vector3(0.22f, 0f, 0f), 0.026f, 6, 4);
            MeshFactory.FromBuilder("WheelMesh", wb, m.Brass, wheel);
            var use = new GameObject("ValveUse");
            use.transform.SetParent(root, false);
            use.transform.localPosition = vb + Vector3.up * 0.95f;
            var trig = use.AddComponent<SphereCollider>();
            trig.radius = 0.4f;
            trig.isTrigger = true;
            var jetLocal = new Vector3[jetAt.Length];
            for (int i = 0; i < jetAt.Length; i++) jetLocal[i] = jetAt[i];
            use.AddComponent<AgraFountains>().Setup(root, wheel, sheet, jets, jetLocal);
        }

        static ParticleSystem Jet(Transform root, Vector3 at, float v0, float v1)
        {
            var go = new GameObject("Jet");
            go.transform.SetParent(root, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f * v0 / 9.81f, 2f * v1 / 9.81f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(v0, v1);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.045f);
            main.startColor = new Color(0.82f, 0.9f, 0.97f, 0.6f);
            main.gravityModifier = 1f;
            main.maxParticles = 200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = v1 > 3f ? 70f : 45f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 4f; sh.radius = 0.008f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.04f;
            r.lengthScale = 1.1f;
            r.maxParticleSize = 0.1f;
            r.sharedMaterial = Mats.Particle(Tex.SoftDot, false, Color.white, 1.2f, Mats.StencilNotEqual);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        // ------------------------------------------------------------------ planting
        /// <summary>Cypress rows along the side walls, two parterres, the champa tree dropping petals and its marble bench.</summary>
        static void Planting(Transform root, KitBatch solid, KitBatch flat, Pal m)
        {
            var rnd = new System.Random(4646);
            foreach (var x in new[] { 9.55f, 20.45f })
                foreach (var z in new[] { 27.0f, 29.8f, 32.6f, 39.6f, 42.4f, 45.0f })
                {
                    Cypress(solid, m, new Vector3(x, -0.03f, z), rnd);
                    var tc = new GameObject("CypressCollider");
                    tc.transform.SetParent(root, false);
                    tc.transform.localPosition = new Vector3(x, 1.2f, z);
                    tc.AddComponent<CapsuleCollider>().radius = 0.32f;
                }
            Bed(root, solid, flat, m, new Vector3(11.95f, -0.03f, 30.3f), new Vector2(3.1f, 6.2f), Vector3.zero, rnd);
            var tree = new Vector3(18.25f, -0.03f, 30.3f);
            Bed(root, solid, flat, m, new Vector3(18.25f, -0.03f, 30.3f), new Vector2(3.1f, 6.2f), tree, rnd);
            Champa(root, solid, flat, m, tree, rnd);
            // A backless marble bench under the champa, along the main walk.
            var bench = new Vector3(16.2f, -0.03f, 30.3f);
            solid.Box(m.Marble, bench + Vector3.up * 0.415f, new Vector3(0.56f, 0.07f, 1.7f), 1.5f);
            solid.Box(m.Marble, bench + Vector3.up * 0.365f, new Vector3(0.5f, 0.03f, 1.62f), 1.5f);
            foreach (float dz in new[] { -0.6f, 0.6f })
            {
                var c = bench + new Vector3(0f, 0f, dz);
                solid.Box(m.Marble, c + Vector3.up * 0.04f, new Vector3(0.5f, 0.08f, 0.22f));
                solid.Box(m.Marble, c + Vector3.up * 0.19f, new Vector3(0.34f, 0.22f, 0.14f));
                solid.Box(m.Marble, c + Vector3.up * 0.325f, new Vector3(0.46f, 0.05f, 0.2f));
                foreach (float side in new[] { -1f, 1f })
                    flat.Box(m.Carnelian, c + new Vector3(side * 0.171f, 0.19f, 0f), new Vector3(0.004f, 0.08f, 0.08f), Quaternion.Euler(45f, 0f, 0f));
            }
            Wall(root, "Bench", bench + Vector3.up * 0.22f, new Vector3(0.56f, 0.45f, 1.7f));
        }

        /// <summary>A parterre: sandstone kerb, clipped border, soil, leaf mounds and bands of flowers (none at the tree).</summary>
        static void Bed(Transform root, KitBatch solid, KitBatch flat, Pal m, Vector3 c, Vector2 size, Vector3 tree, System.Random rnd)
        {
            float w = size.x, d = size.y;
            flat.Box(m.Sandstone, c + new Vector3(0f, 0.06f, d * 0.5f - 0.05f), new Vector3(w, 0.12f, 0.1f));
            flat.Box(m.Sandstone, c + new Vector3(0f, 0.06f, -d * 0.5f + 0.05f), new Vector3(w, 0.12f, 0.1f));
            flat.Box(m.Sandstone, c + new Vector3(w * 0.5f - 0.05f, 0.06f, 0f), new Vector3(0.1f, 0.12f, d - 0.2f));
            flat.Box(m.Sandstone, c + new Vector3(-w * 0.5f + 0.05f, 0.06f, 0f), new Vector3(0.1f, 0.12f, d - 0.2f));
            flat.Box(m.Soil, c + new Vector3(0f, 0.035f, 0f), new Vector3(w - 0.2f, 0.07f, d - 0.2f));
            solid.Box(m.Hedge, c + new Vector3(0f, 0.15f, d * 0.5f - 0.18f), new Vector3(w - 0.2f, 0.3f, 0.16f), 0.6f);
            solid.Box(m.Hedge, c + new Vector3(0f, 0.15f, -d * 0.5f + 0.18f), new Vector3(w - 0.2f, 0.3f, 0.16f), 0.6f);
            solid.Box(m.Hedge, c + new Vector3(w * 0.5f - 0.18f, 0.15f, 0f), new Vector3(0.16f, 0.3f, d - 0.52f), 0.6f);
            solid.Box(m.Hedge, c + new Vector3(-w * 0.5f + 0.18f, 0.15f, 0f), new Vector3(0.16f, 0.3f, d - 0.52f), 0.6f);
            bool hasTree = tree != Vector3.zero;
            var leaves = Mats.Lit(new Color(0.22f, 0.42f, 0.16f), 0.25f);
            for (int i = 0; i < 18; i++)
            {
                var p = c + new Vector3(DetailShapes.Range(rnd, -w * 0.32f, w * 0.32f), 0.1f, DetailShapes.Range(rnd, -d * 0.38f, d * 0.38f));
                if (hasTree && (p - tree).sqrMagnitude < 0.36f) continue;
                DetailShapes.Ellipsoid(flat.Of(leaves), p, DetailShapes.Range(rnd, 0.15f, 0.22f), 0.09f, 6, 3);
            }
            int nx = Mathf.RoundToInt((w - 0.5f) / 0.16f), nz = Mathf.RoundToInt((d - 0.5f) / 0.16f);
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    var p = c + new Vector3(-(nx - 1) * 0.08f + i * 0.16f + DetailShapes.Range(rnd, -0.04f, 0.04f), DetailShapes.Range(rnd, 0.17f, 0.3f), -(nz - 1) * 0.08f + j * 0.16f + DetailShapes.Range(rnd, -0.04f, 0.04f));
                    if (hasTree && new Vector2(p.x - tree.x, p.z - tree.z).sqrMagnitude < 0.3f) continue;
                    if (rnd.NextDouble() < 0.35) continue;
                    int ring = Mathf.Min(Mathf.Min(i, nx - 1 - i), Mathf.Min(j, nz - 1 - j));
                    var mat = ring == 0 ? m.Flowers[(i + j) % 2] : ring == 1 ? m.Flowers[2] : m.Flowers[3 + (i + j) % 2];
                    flat.Of(mat).AddSphere(p, DetailShapes.Range(rnd, 0.038f, 0.055f), 5, 3);
                }
            Wall(root, "Parterre", c + Vector3.up * 0.22f, new Vector3(w, 0.45f, d));
        }

        /// <summary>A champa (frangipani): a leaning trunk forking into a rounded crown studded with blossom, petals falling.</summary>
        static void Champa(Transform root, KitBatch solid, KitBatch flat, Pal m, Vector3 b, System.Random rnd)
        {
            var path = new List<Vector3> { b, b + new Vector3(0.05f, 0.7f, 0.02f), b + new Vector3(0.18f, 1.4f, -0.05f), b + new Vector3(0.3f, 2.0f, -0.08f) };
            solid.Of(m.Bark).AddTube(path, 0.12f, 8);
            var fork = path[3];
            foreach (var dir in new[] { new Vector3(0.7f, 1f, 0.3f), new Vector3(-0.6f, 1f, 0.2f), new Vector3(0.1f, 1f, -0.7f), new Vector3(-0.2f, 1f, 0.8f) })
            {
                var tip = fork + dir.normalized * 1.0f;
                solid.Of(m.Bark).AddTube(new[] { fork, Vector3.Lerp(fork, tip, 0.5f) + Vector3.up * 0.1f, tip }, 0.07f, 6);
            }
            var crown = fork + Vector3.up * 1.0f;
            for (int i = 0; i < 14; i++)
            {
                float a = i * 2.4f, rr = DetailShapes.Range(rnd, 0.4f, 1.15f);
                var p = crown + new Vector3(Mathf.Cos(a) * rr, DetailShapes.Range(rnd, -0.25f, 0.5f), Mathf.Sin(a) * rr);
                DetailShapes.Ellipsoid(solid.Of(i % 3 == 0 ? m.LeafLit : m.Leaf), p, DetailShapes.Range(rnd, 0.42f, 0.6f), DetailShapes.Range(rnd, 0.28f, 0.38f), 8, 4);
            }
            for (int i = 0; i < 60; i++)
            {
                float a = DetailShapes.Range(rnd, 0f, Mathf.PI * 2f), rr = DetailShapes.Range(rnd, 0.3f, 1.55f);
                var p = crown + new Vector3(Mathf.Cos(a) * rr, DetailShapes.Range(rnd, -0.1f, 0.8f) + (1.55f - rr) * 0.25f, Mathf.Sin(a) * rr);
                solid.Of(i % 4 == 0 ? m.BlossomPink : m.Blossom).AddSphere(p, DetailShapes.Range(rnd, 0.05f, 0.08f), 6, 4);
            }
            var tc = new GameObject("ChampaTrunk");
            tc.transform.SetParent(root, false);
            tc.transform.localPosition = b + new Vector3(0.12f, 1.0f, -0.03f);
            var cap = tc.AddComponent<CapsuleCollider>();
            cap.radius = 0.2f;
            cap.height = 2f;
            // Fallen petals under the crown, and a slow fall of new ones.
            for (int i = 0; i < 70; i++)
            {
                float a = DetailShapes.Range(rnd, 0f, Mathf.PI * 2f), rr = DetailShapes.Range(rnd, 0.3f, 2.2f);
                var p = new Vector3(b.x + Mathf.Cos(a) * rr, 0f, b.z + Mathf.Sin(a) * rr);
                bool inBed = Mathf.Abs(p.x - b.x) < 1.45f && Mathf.Abs(p.z - b.z) < 3.0f;
                p.y = inBed ? 0.042f : -0.0265f;
                flat.Box(i % 3 == 0 ? m.BlossomPink : m.Blossom, p, new Vector3(0.04f, 0.003f, 0.028f), Quaternion.Euler(0f, DetailShapes.Range(rnd, 0f, 180f), 0f));
            }
            var go = new GameObject("FallingPetals");
            go.transform.SetParent(root, false);
            go.transform.localPosition = crown + Vector3.up * 0.1f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 11f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.05f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var tint = new Gradient { mode = GradientMode.Fixed };
            tint.SetKeys(new[] { new GradientColorKey(new Color(0.98f, 0.96f, 0.9f), 0.6f), new GradientColorKey(new Color(0.98f, 0.7f, 0.76f), 1f) },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.95f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(tint) { mode = ParticleSystemGradientMode.RandomColor };
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 2.6f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(2.6f, 0.5f, 2.6f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.16f); vel.y = new ParticleSystem.MinMaxCurve(-0.38f, -0.26f); vel.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            var spin = ps.rotationOverLifetime; spin.enabled = true; spin.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.12f; noise.frequency = 0.4f; noise.scrollSpeed = 0.2f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = Mats.Particle(Tex.SoftDot, false, Color.white, 1.1f, Mats.StencilNotEqual);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 0.08f;
            ps.Play();
        }

        // ------------------------------------------------------------------ life, lamps, postcards, bounds
        static void Life(Transform root)
        {
            // Small brown garden birds hopping on the two north lawns (Sparrow kind).
            AmbientBirds.Create(root, new Vector3(11.2f, -0.03f, 41.9f), 1.1f, 7, AmbientBirds.Kind.Sparrow);
            AmbientBirds.Create(root, new Vector3(18.6f, -0.03f, 40.2f), 0.95f, 6, AmbientBirds.Kind.Sparrow);
        }

        static void Lamps(Transform root)
        {
            SwitchLamp.Create(root, new Vector3(6.6f, -0.05f, 37.6f), LampColor);     // outside the gate, off the long walk
            SwitchLamp.Create(root, new Vector3(19.6f, -0.03f, 37.25f), LampColor);   // by the east cross walk
            SwitchLamp.Create(root, new Vector3(4.95f, -0.05f, 124.5f), LampColor);   // the long walk past the tank, toward the stamp
        }

        static void Postcards(Transform root)
        {
            Postcard.Create(root, CountryId.India, 1, new Vector3(AxisX, Top + 1.15f, 44.25f), "Sat under the little dome until dark. The water kept time for someone else.");
            Postcard.Create(root, CountryId.India, 2, new Vector3(16.2f, 1.1f, 31.55f), "Two of us came in through the gate. The gardener only ever counted one.");
            Postcard.Create(root, CountryId.India, 3, new Vector3(7.85f, 1.2f, 5.5f), "In the pool I stood beside myself. I waved. Only one of us did.");
        }

        /// <summary>The cell's east bound (x 12.15, z -6.5..170) opens between the garden's south and north walls.</summary>
        static void OpenBounds(Transform cellT)
        {
            var bounds = cellT.Find("Bounds");
            Transform east = null;
            if (bounds != null)
                foreach (Transform c in bounds)
                    if (c.name == "Bound" && Mathf.Abs(c.localPosition.x - 12.15f) < 0.05f) { east = c; break; }
            var box = east != null ? east.GetComponent<BoxCollider>() : null;
            if (box == null)
            {
                Debug.LogWarning("[TS-Expand] Agra: the east bound is not where expected; the water garden stays shut");
                return;
            }
            float z0 = east.localPosition.z - box.size.z * 0.5f, z1 = east.localPosition.z + box.size.z * 0.5f;
            if (z0 > GZ0 || z1 < GZ1) return;
            var p = east.localPosition;
            east.localPosition = new Vector3(p.x, p.y, (z0 + GZ0) * 0.5f);
            var size = box.size;
            box.size = new Vector3(size.x, size.y, GZ0 - z0);
            var north = new GameObject("Bound");
            north.transform.SetParent(bounds, false);
            north.transform.localPosition = new Vector3(p.x, p.y, (GZ1 + z1) * 0.5f);
            north.AddComponent<BoxCollider>().size = new Vector3(size.x, size.y, z1 - GZ1);
        }

        // ------------------------------------------------------------------ builders (after Detail_Agra / Detail_AgraArch)
        static void Chhatri(KitBatch k, Pal m, Vector3 p, float s)
        {
            var oct = Quaternion.Euler(0f, 22.5f, 0f);
            foreach (float cx in new[] { -0.72f, 0.72f })
                foreach (float cz in new[] { -0.72f, 0.72f })
                {
                    var c = p + new Vector3(cx, 0f, cz) * s;
                    k.Box(m.Sandstone, c + Vector3.up * 0.07f * s, new Vector3(0.26f, 0.14f, 0.26f) * s);
                    k.Of(m.Sandstone).AddCylinder(c + Vector3.up * 0.14f * s, 0.085f * s, 1.35f * s, 8, 1f, oct);
                    k.Box(m.Marble, c + Vector3.up * 0.76f * s, new Vector3(0.2f, 0.04f, 0.2f) * s);
                    k.Box(m.Sandstone, c + Vector3.up * 1.54f * s, new Vector3(0.22f, 0.1f, 0.22f) * s);
                    k.Box(m.Sandstone, c + Vector3.up * 1.61f * s, new Vector3(0.3f, 0.05f, 0.3f) * s);
                }
            k.Box(m.Sandstone, p + new Vector3(0f, 1.72f, 0.72f) * s, new Vector3(1.74f, 0.18f, 0.2f) * s);
            k.Box(m.Sandstone, p + new Vector3(0f, 1.72f, -0.72f) * s, new Vector3(1.74f, 0.18f, 0.2f) * s);
            k.Box(m.Sandstone, p + new Vector3(0.72f, 1.72f, 0f) * s, new Vector3(0.2f, 0.18f, 1.74f) * s);
            k.Box(m.Sandstone, p + new Vector3(-0.72f, 1.72f, 0f) * s, new Vector3(0.2f, 0.18f, 1.74f) * s);
            k.Box(m.Sandstone, p + new Vector3(0f, 1.79f, 0.98f) * s, new Vector3(2.45f, 0.05f, 0.58f) * s, Quaternion.Euler(18f, 0f, 0f));
            k.Box(m.Sandstone, p + new Vector3(0f, 1.79f, -0.98f) * s, new Vector3(2.45f, 0.05f, 0.58f) * s, Quaternion.Euler(-18f, 0f, 0f));
            k.Box(m.Sandstone, p + new Vector3(0.98f, 1.79f, 0f) * s, new Vector3(0.58f, 0.05f, 2.45f) * s, Quaternion.Euler(0f, 0f, -18f));
            k.Box(m.Sandstone, p + new Vector3(-0.98f, 1.79f, 0f) * s, new Vector3(0.58f, 0.05f, 2.45f) * s, Quaternion.Euler(0f, 0f, 18f));
            k.Box(m.Sandstone, p + Vector3.up * 2.05f * s, new Vector3(1.6f, 0.36f, 1.6f) * s);
            k.Box(m.Marble, p + Vector3.up * 2.05f * s, new Vector3(1.62f, 0.12f, 1.62f) * s);
            k.Box(m.Carnelian, p + Vector3.up * 2.05f * s, new Vector3(1.635f, 0.03f, 1.635f) * s);
            k.Of(m.Marble).AddCylinder(p + Vector3.up * 2.23f * s, 0.7f * s, 0.1f * s, 16);
            var dome = new List<Vector2>(s_Dome.Length);
            foreach (var d in s_Dome) dome.Add(d * (0.66f * s));
            k.Of(m.Marble).AddLathe(p + Vector3.up * 2.33f * s, dome, 16);
            var top = p + Vector3.up * (2.33f + 1.44f * 0.66f) * s;
            k.Of(m.Brass).AddSphere(top + Vector3.up * 0.05f * s, 0.07f * s, 8, 5);
            k.Of(m.Brass).AddCylinder(top + Vector3.up * 0.1f * s, 0.022f * s, 0.12f * s, 6);
            k.Of(m.Brass).AddSphere(top + Vector3.up * 0.25f * s, 0.045f * s, 8, 5);
            k.Of(m.Brass).AddFrustum(top + Vector3.up * 0.28f * s, 0.018f * s, 0f, 0.22f * s, 6);
            foreach (float cx in new[] { -0.76f, 0.76f })
                foreach (float cz in new[] { -0.76f, 0.76f })
                {
                    var c = p + new Vector3(cx, 2.23f, cz) * s;
                    k.Of(m.Marble).AddCylinder(c, 0.035f * s, 0.3f * s, 6);
                    k.Of(m.Marble).AddSphere(c + Vector3.up * 0.33f * s, 0.05f * s, 6, 4);
                }
        }

        static void Cypress(KitBatch k, Pal m, Vector3 b, System.Random rnd)
        {
            float h = DetailShapes.Range(rnd, 4.4f, 5.4f), R = DetailShapes.Range(rnd, 0.48f, 0.56f), seed = DetailShapes.Range(rnd, 0f, 10f);
            const float y0 = 0.32f;
            float Rad(float t)
            {
                float r = t < 0.25f
                    ? R * Mathf.Lerp(0.55f, 1f, Mathf.SmoothStep(0f, 1f, t / 0.25f))
                    : R * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((t - 0.25f) / 0.75f, 1.7f))) * (1f - 0.5f * (t - 0.25f) / 0.75f);
                return r * (1f + 0.05f * Mathf.Sin(t * 23f + seed));
            }
            float Y(float t) => y0 + 0.08f + t * (h - y0 - 0.08f);
            k.Of(m.Bark).AddFrustum(b, 0.1f, 0.07f, 0.5f, 7);
            var prof = new List<Vector2> { new Vector2(0.001f, y0) };
            const int rows = 16;
            for (int r = 0; r <= rows; r++)
            {
                float t = r / (float)rows;
                prof.Add(new Vector2(Mathf.Max(0.001f, Rad(t)), Y(t)));
            }
            k.Of(m.Leaf).AddLathe(b, prof, 11, 0.55f);
            for (int c = 0; c < 14; c++)
            {
                float t = 0.05f + 0.83f * (c + (float)rnd.NextDouble()) / 14f;
                float ang = DetailShapes.Range(rnd, 0f, Mathf.PI * 2f), rad = Rad(t);
                float a = Mathf.Max(0.09f, rad * 0.42f);
                var p = b + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (rad * 0.78f) + Vector3.up * Y(t);
                DetailShapes.Ellipsoid(k.Of(m.LeafLit), p, a, a * 2.1f, 7, 4, 0.55f);
            }
        }

        static void LanternPost(KitBatch k, Pal m, Vector3 b)
        {
            k.Box(m.Sandstone, b + Vector3.up * 0.08f, new Vector3(0.32f, 0.16f, 0.32f));
            k.Box(m.Marble, b + Vector3.up * 0.18f, new Vector3(0.26f, 0.04f, 0.26f));
            k.Box(m.Sandstone, b + Vector3.up * 0.79f, new Vector3(0.15f, 1.18f, 0.15f), 1.5f);
            k.Box(m.Marble, b + Vector3.up * 0.9f, new Vector3(0.17f, 0.03f, 0.17f));
            k.Box(m.Marble, b + Vector3.up * 1.41f, new Vector3(0.24f, 0.06f, 0.24f));
            k.Of(m.Brass).AddCylinder(b + Vector3.up * 1.44f, 0.13f, 0.03f, 6);
            k.Of(m.Glow).AddCylinder(b + Vector3.up * 1.47f, 0.1f, 0.24f, 6);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                k.Of(m.Brass).AddCylinder(b + new Vector3(Mathf.Cos(a) * 0.104f, 1.47f, Mathf.Sin(a) * 0.104f), 0.009f, 0.24f, 4);
            }
            k.Of(m.Brass).AddFrustum(b + Vector3.up * 1.71f, 0.15f, 0.035f, 0.11f, 6);
            k.Of(m.Brass).AddSphere(b + Vector3.up * 1.85f, 0.035f, 6, 4);
        }

        /// <summary>A brass hexagonal lantern on a chain, built under its hang point (the pivot WindSway turns about).</summary>
        static void HangingLantern(Transform pivot, Pal m, float chain)
        {
            var b = new MeshBuilder();
            b.AddCylinder(new Vector3(0f, -chain, 0f), 0.006f, chain, 5);
            float y = -chain - 0.36f;
            b.AddFrustum(new Vector3(0f, y + 0.3f, 0f), 0.13f, 0.03f, 0.08f, 6);
            b.AddCylinder(new Vector3(0f, y + 0.27f, 0f), 0.12f, 0.03f, 6);
            b.AddCylinder(new Vector3(0f, y, 0f), 0.11f, 0.03f, 6);
            b.AddFrustum(new Vector3(0f, y - 0.07f, 0f), 0.02f, 0.1f, 0.07f, 6);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                b.AddCylinder(new Vector3(Mathf.Cos(a) * 0.105f, y + 0.03f, Mathf.Sin(a) * 0.105f), 0.008f, 0.24f, 4);
            }
            MeshFactory.FromBuilder("Lantern", b, m.Brass, pivot);
            var g = new MeshBuilder();
            g.AddCylinder(new Vector3(0f, y + 0.03f, 0f), 0.095f, 0.24f, 6);
            var glow = MeshFactory.FromBuilder("LanternGlow", g, m.Glow, pivot);
            glow.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static void Niche(KitBatch k, Pal m, Vector3 baseCenter, Vector3 normal, float width, float spring, bool frame)
        {
            var right = Vector3.Cross(Vector3.up, -normal);
            var rot = Quaternion.LookRotation(-normal, Vector3.up);
            var arch = DetailShapes.PointedArch(width, spring, 0.3f, 7);
            k.Of(m.Recess).AddExtrudedXY(arch, 0.02f, baseCenter + normal * 0.012f, rot, 1.5f);
            Vector3 W(Vector2 v) => baseCenter + right * v.x + Vector3.up * v.y + normal * 0.03f;
            DetailShapes.Outline(k, m.Marble, arch, W, normal, 0.07f, 0.03f);
            if (!frame) return;
            float apex = DetailShapes.ArchApex(width, spring, 0.3f), hw = width * 0.5f + 0.16f;
            DetailShapes.PlaneStrip(k, m.Marble, W(new Vector2(-hw, 0f)), W(new Vector2(-hw, apex + 0.16f)), normal, 0.05f, 0.025f);
            DetailShapes.PlaneStrip(k, m.Marble, W(new Vector2(hw, 0f)), W(new Vector2(hw, apex + 0.16f)), normal, 0.05f, 0.025f);
            DetailShapes.PlaneStrip(k, m.Marble, W(new Vector2(-hw - 0.025f, apex + 0.16f)), W(new Vector2(hw + 0.025f, apex + 0.16f)), normal, 0.05f, 0.025f);
        }

        static void Parapet(KitBatch k, Pal m, Vector3 a, Vector3 b)
        {
            float len = Vector3.Distance(a, b);
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 0.55f));
            var rot = Quaternion.LookRotation((b - a).normalized, Vector3.up);
            for (int i = 0; i <= n; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)n);
                k.Box(m.Sandstone, p + Vector3.up * 0.16f, new Vector3(0.2f, 0.32f, 0.3f), rot);
                k.Box(m.Sandstone, p + Vector3.up * 0.38f, new Vector3(0.14f, 0.12f, 0.18f), rot);
            }
        }

        // ------------------------------------------------------------------ helpers
        static void Floor(Transform root, string name, Vector3 c, Vector3 s, string surface)
        {
            var go = new GameObject(name) { layer = Layers.Floor };
            go.transform.SetParent(root, false);
            go.transform.localPosition = c;
            go.AddComponent<BoxCollider>().size = s;
            go.AddComponent<Surface>().Kind = surface;
        }

        static GameObject Wall(Transform root, string name, Vector3 c, Vector3 s, Quaternion? rot = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = c;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.AddComponent<BoxCollider>().size = s;
            return go;
        }

        static Light PointLight(Transform parent, Vector3 pos, Color c, float intensity, float range)
        {
            var go = new GameObject("PointLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        static void ShotPoint(Transform root, Vector3 at, float yaw)
        {
            var shot = new GameObject("ShotPoint").transform;
            shot.SetParent(root, false);
            shot.localPosition = at;
            shot.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }

    /// <summary>
    /// The water garden's stone valve (Agra's set piece): turn the brass wheel and water slides down the chadar, then the
    /// jets wake one after another down the rill to the basin, with a soft patter of falling drops; turn it back and they
    /// settle again, last first. No fail state; nothing waits on it.
    /// </summary>
    public class AgraFountains : Interactable
    {
        Transform m_Space, m_Wheel;
        Quaternion m_WheelRest;
        GameObject m_Sheet;
        ParticleSystem[] m_Jets;
        Vector3[] m_JetLocal;
        Cell m_Cell;
        float m_Spin, m_Drip;
        bool m_On, m_Busy;
        static bool s_Told;

        public bool Running => m_On;
        /// <summary>Times the valve has been turned (tests).</summary>
        public int Turns { get; private set; }

        public void Setup(Transform space, Transform wheel, GameObject sheet, ParticleSystem[] jets, Vector3[] jetLocal)
        {
            m_Space = space;
            m_Wheel = wheel;
            m_WheelRest = wheel.localRotation;
            m_Sheet = sheet;
            m_Jets = jets;
            m_JetLocal = jetLocal;
            m_Cell = GetComponentInParent<Cell>();
            Prompt = "Turn the water on";
            MaxDistance = 2.6f;
            MarkerPoint = wheel;
        }

        bool Live => m_Cell == null || m_Cell.Mode == CellMode.Active;

        public override string GetPrompt(Interactor who) => m_On ? "Turn the water off" : "Turn the water on";

        public override bool CanUse(Interactor who) => base.CanUse(who) && !m_Busy && m_Wheel != null;

        public override void Use(Interactor who)
        {
            if (!CanUse(who)) return;
            Turns++;
            AudioDirector.Haptic(who != null ? who.Side : HandSide.Desktop, 0.3f, 0.1f);
            StartCoroutine(Turn(!m_On));
            base.Use(who);
        }

        IEnumerator Turn(bool on)
        {
            m_Busy = true;
            AudioDirector.I?.Play("drawer_open", m_Wheel.position, 0.6f, on ? 0.55f : 0.62f);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 1.1f;
                m_Spin += (on ? 1f : -1f) * 300f * Time.deltaTime;
                m_Wheel.localRotation = m_WheelRest * Quaternion.AngleAxis(m_Spin, Vector3.up);
                yield return null;
            }
            AudioDirector.I?.Play("lever_clunk", m_Wheel.position, 0.45f, 0.6f);
            m_On = on;
            if (on)
            {
                if (!s_Told) { s_Told = true; Hud.I?.ShowCaption("water wakes along the rill", 2.6f); }
                if (m_Sheet != null) m_Sheet.SetActive(true);
                for (int i = 0; i < m_Jets.Length; i++)
                {
                    yield return new WaitForSeconds(0.35f);
                    if (m_Jets[i] != null) m_Jets[i].Play();
                    if (Live && m_Space != null) AudioDirector.I?.Play("foot_wet_1", m_Space.TransformPoint(m_JetLocal[i]), 0.45f, 1.35f);
                }
            }
            else
            {
                for (int i = m_Jets.Length - 1; i >= 0; i--)
                {
                    if (m_Jets[i] != null) m_Jets[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    yield return new WaitForSeconds(0.15f);
                }
                if (m_Sheet != null) m_Sheet.SetActive(false);
            }
            m_Busy = false;
        }

        void OnEnable()
        {
            // A cell coming back from hidden restarts its systems: fountains left running keep running.
            if (!m_On || m_Jets == null || m_Busy) return;
            foreach (var ps in m_Jets) if (ps != null && !ps.isPlaying) ps.Play();
        }

        void Update()
        {
            if (!m_On || m_Busy || !Live || m_Space == null || m_JetLocal == null || m_JetLocal.Length == 0) return;
            m_Drip -= Time.deltaTime;
            if (m_Drip > 0f) return;
            m_Drip = Random.Range(0.18f, 0.4f);
            var p = m_Space.TransformPoint(m_JetLocal[Random.Range(0, m_JetLocal.Length)]);
            AudioDirector.I?.Play("foot_wet_1", p, Random.Range(0.18f, 0.32f), Random.Range(1.3f, 1.7f));
        }
    }
}

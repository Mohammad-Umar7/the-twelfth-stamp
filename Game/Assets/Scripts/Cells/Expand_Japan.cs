using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Japan's expansion pass (playtest: "expand the country maps ... more interactive animative and cool"). The street is
    /// closed in by its machiya and their walls, so the new side area opens off the route to the pagoda instead: a tea
    /// garden behind a low bamboo fence on the east side of the lantern approach, entered by a small roofed gate between
    /// the approach's stone lanterns. A stepping-stone path runs past a bamboo grove and a roofed waiting bench, a dripping
    /// stone basin and its lantern, a tea house with lit shoji and a noren at its door, to a bronze bell in its own roofed
    /// frame: swing the hanging log and the bell rings low while the paper lanterns along the fence brighten one after
    /// another, from the bell back to the gate. Maples lean over from the precinct and drop leaves; pigeons peck on the
    /// gravel. Elsewhere: crows on the street, two lamps in the lane, and three postcards (street, lane, tea house). Kept
    /// clear of the street's puzzle, seams and the Passenger's post, the lane's walk and the approach's centre line
    /// (Landmarks.MarkReachable re-checks that), and inside the cell's bounds. No shrine gates or sacred objects.
    /// </summary>
    public static class Expand_Japan
    {
        // The lane as laid out in Detail_JapanRoute.cs (its helpers are private to the cell).
        static readonly Vector3 s_LaneStart = new Vector3(0f, 0f, 13.45f);
        const float LowY = -0.97f;
        static Vector3 ToStreet => new Vector3(-JapanCell.PagodaPos.x, 0f, -JapanCell.PagodaPos.z).normalized;
        static Vector3 LaneEnd { get { var p = JapanCell.PagodaPos + ToStreet * 76.5f; p.y = -1f; return p; } }
        static Vector3 LaneDir { get { var d = LaneEnd - s_LaneStart; d.y = 0f; return d.normalized; } }
        static Vector3 LaneRight { get { var u = LaneDir; return new Vector3(u.z, 0f, -u.x); } }
        static Vector3 Lane(float t, float s, float y) => s_LaneStart + LaneDir * t + LaneRight * s + Vector3.up * y;

        // The garden's frame: its origin is on the approach's east lantern row (the vista's s = -5.5) at d = 44 m from the
        // pagoda, on the precinct ground (y -1); local +z runs up the approach toward the pagoda, +x east, away from it.
        // The east lanterns stand at local x 0, z -3, 4, 11, 18, 25; the stamp waits at local (-5.5, 28).
        const float FenceX = 0.65f;                 // the west fence, just behind the lanterns
        const float Z0 = 0f, Z1 = 25.4f;            // south and north fences
        const float Top = 0.04f;                    // the garden floor, 4 cm over the precinct ground
        const float EastWorldX = 4.52f;             // the east fence, just inside the cell's bounds (x 4.6)
        static readonly Color Warm = new Color(1f, 0.66f, 0.38f);

        sealed class GardenMats
        {
            public readonly Material Timber = Mats.Lit(new Color(0.8f, 0.78f, 0.76f), 0.35f, 0f, "wood_dark");
            public readonly Material Weathered = Mats.Lit(new Color(0.7f, 0.84f, 1f), 0.25f, 0f, "wood_dark");
            public readonly Material Board = Mats.Lit(new Color(0.72f, 0.7f, 0.7f), 0.35f, 0f, "wood_oak_floor");
            public readonly Material Earth = Mats.Lit(new Color(0.74f, 0.62f, 0.5f), 0.15f, 0f, "plaster_warm");
            public readonly Material Tile = Mats.Lit(new Color(0.82f, 0.84f, 0.9f), 0.55f, 0.3f, "stone_dark_slate");
            public readonly Material Stone = Mats.Lit(new Color(0.95f, 0.95f, 0.95f), 0.45f, 0f, "granite_pavers");
            public readonly Material StoneDark = Mats.Lit(new Color(0.82f, 0.84f, 0.88f), 0.5f, 0f, "stone_dark_slate");
            public readonly Material Gravel = Mats.Lit(new Color(0.84f, 0.9f, 1f), 0.3f, 0f, "sand_ripples");
            public readonly Material Moss = Mats.Lit(new Color(0.6f, 0.78f, 0.5f), 0.15f, 0f, "grass_short");
            public readonly Material Leaf = Mats.Lit(new Color(0.8f, 0.85f, 0.8f), 0.2f, 0f, "grass_short");
            public readonly Material Bamboo = Mats.Lit(new Color(0.82f, 0.84f, 0.8f), 0.45f, 0f, "wood_oak_floor");
            public readonly Material Culm = Mats.Lit(new Color(0.36f, 0.5f, 0.24f), 0.6f);
            public readonly Material Node = Mats.Lit(new Color(0.55f, 0.6f, 0.36f), 0.5f);
            public readonly Material Black = Mats.Lit(new Color(0.03f, 0.03f, 0.035f), 0.4f);
            public readonly Material Rope = Mats.Lit(new Color(0.62f, 0.52f, 0.38f), 0.2f, 0f, "fabric_linen");
            public readonly Material Bronze = Mats.Lit(new Color(0.55f, 0.45f, 0.3f), 0.55f, 0.7f, "metal_brushed");
            public readonly Material Felt = Mats.Lit(new Color(0.72f, 0.14f, 0.1f), 0.2f, 0f, "fabric_linen");
            public readonly Material Water = Mats.Lit(new Color(0.02f, 0.025f, 0.03f), 0.96f);
            public readonly Material Navy = Mats.Lit(new Color(0.19f, 0.27f, 0.58f), 0.2f, 0f, "fabric_linen");
            public readonly Material Interior = Mats.Emissive(new Color(0.42f, 0.32f, 0.22f), new Color(0.45f, 0.34f, 0.22f));
            public readonly Material PaperGlow = Mats.Lit(new Color(0.95f, 0.9f, 0.8f), 0.1f, 0f, "paper_shoji", false, new Color(0.62f, 0.5f, 0.34f));
            public readonly Material LanternCream = Mats.Lit(new Color(0.95f, 0.88f, 0.72f), 0.1f, 0f, "paper_shoji", false, new Color(1.1f, 0.85f, 0.6f));
            public readonly Material LanternRed = Mats.Lit(new Color(0.8f, 0.2f, 0.12f), 0.1f, 0f, "paper_shoji", false, new Color(1.05f, 0.38f, 0.22f));
            public readonly Material MapleRed = Mats.Lit(new Color(0.7f, 0.15f, 0.07f), 0.3f);
            public readonly Material MapleGold = Mats.Lit(new Color(0.85f, 0.42f, 0.1f), 0.3f);
        }

        /// <summary>The garden while it is built: its transform, materials, batches and the lantern wave.</summary>
        sealed class Garden
        {
            public Transform T;
            public GardenMats M;
            public readonly KitBatch K = new KitBatch();     // timber, bamboo, stone, plaster (shadows)
            public readonly KitBatch Soft = new KitBatch();  // moss, foliage, fallen leaves (no shadows)
            public JapanLanternWave Wave;
            Vector3 m_Origin, m_Right, m_Fwd;

            public Garden(Transform t, GardenMats m)
            {
                T = t;
                M = m;
                m_Origin = t.localPosition;
                m_Right = t.localRotation * Vector3.right;
                m_Fwd = t.localRotation * Vector3.forward;
            }

            /// <summary>Local x of the east fence (the world line x = EastWorldX) at local z.</summary>
            public float EastX(float z) => (EastWorldX - m_Origin.x - m_Fwd.x * z) / m_Right.x;

            /// <summary>A garden point in the expansion root's (the cell's) coordinates.</summary>
            public Vector3 ToCell(Vector3 local) => m_Origin + T.localRotation * local;
        }

        public static void Build(JapanCell cell, Transform parent)
        {
            var root = new GameObject("Expand_Japan").transform;
            root.SetParent(cell.transform, false);
            var m = new GardenMats();

            // Crows on the wet stones halfway along the street, clear of the hook posts, the route marks and the
            // Passenger's post by the gate: they scatter when you come near and wheel over the roofs.
            AmbientBirds.Create(root, new Vector3(-0.9f, 0f, 2.4f), 1f, 5, AmbientBirds.Kind.Crow);

            // Two lamps on the lane's gravel, off its slab walk: one where the steps come down, one in the dark before the
            // precinct's gate (the lane's own lights are at 13.5 m and 25.5 m along).
            SwitchLamp.Create(root, Lane(8.7f, 2.6f, LowY - 0.02f), Warm, false, 2.4f, 2.1f);
            SwitchLamp.Create(root, Lane(30f, -2.5f, LowY - 0.02f), Warm, false, 2.4f, 2.1f);

            var g = TeaGarden(root, m);

            Postcard.Create(root, CountryId.Japan, 1, g.ToCell(new Vector3(2.85f, Top + 1.1f, 15.6f)),
                "The tea house door was open a hand's width. Two cups set out. I only drank one.");
            Postcard.Create(root, CountryId.Japan, 2, new Vector3(2.45f, 1.1f, -5.3f),
                "Every lantern on this street was lit for someone. I walked it end to end and met no one.");
            Postcard.Create(root, CountryId.Japan, 3, Lane(22.9f, 3.15f, LowY + 1.1f),
                "Past the gate the gravel was raked fresh. My own footprints were already in it.");
        }

        // ================================================================== the tea garden
        static Garden TeaGarden(Transform root, GardenMats m)
        {
            var o = JapanCell.PagodaPos;
            var ax = ToStreet;
            var sd = Vector3.Cross(Vector3.up, ax);
            var t = new GameObject("TeaGarden").transform;
            t.SetParent(root, false);
            t.localPosition = o + ax * 44f + sd * -5.5f; // y -1: the precinct ground
            t.localRotation = Quaternion.LookRotation(-ax, Vector3.up);
            var g = new Garden(t, m);
            var wave = t.gameObject.AddComponent<JapanLanternWave>();
            g.Wave = wave;

            Floor(g);
            Fences(g);
            Gate(g);
            Grove(g);
            Bench(g);
            var basinLight = Basin(g);
            var houseLight = TeaHouse(g);
            var leaves = FallingLeaves(t, new Vector3(5f, 4.6f, 14.5f), new Vector3(3.4f, 0.6f, 19f));
            Bell(g, leaves);
            LanternLine(g);
            Dressing(g);
            wave.AddLight(basinLight);
            wave.AddLight(houseLight);

            // Pigeons on the gravel between the tea house and the bell; two lamps for the dark ends of the garden.
            AmbientBirds.Create(t, new Vector3(2.2f, Top, 19f), 0.85f, 6, AmbientBirds.Kind.Pigeon);
            SwitchLamp.Create(t, new Vector3(1.8f, Top, 4.3f), Warm, false, 2.4f, 2f);
            SwitchLamp.Create(t, new Vector3(1.65f, Top, 23.6f), Warm, false, 2.4f, 2f);

            g.K.Bake(t, "TeaGarden");
            g.Soft.Bake(t, "TeaGardenSoft", false);

            // The art review's viewpoint: just inside the gate, looking up the path to the basin, the tea house and the
            // bell, with the floodlit pagoda over the fence to the left.
            var shot = new GameObject("ShotPoint").transform;
            shot.SetParent(t, false);
            shot.localPosition = new Vector3(1.45f, Top, 8.15f);
            shot.localRotation = Quaternion.Euler(0f, 8f, 0f);
            return g;
        }

        /// <summary>Raked gravel over the whole plot (its collider is the walkable floor, gravel underfoot).</summary>
        static void Floor(Garden g)
        {
            var poly = new List<Vector2> { new Vector2(FenceX, Z0), new Vector2(g.EastX(Z0), Z0), new Vector2(g.EastX(Z1), Z1), new Vector2(FenceX, Z1) };
            var b = new MeshBuilder();
            b.AddPrism(poly, -0.08f, Top, 2f);
            var go = MeshFactory.FromBuilder("GardenFloor", b, g.M.Gravel, g.T, ColliderKind.Mesh, Layers.Floor);
            go.AddComponent<Surface>().Kind = "sand";
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // The gate's threshold and a few stones across the approach's gravel to it.
            g.K.Box(g.M.Stone, new Vector3(FenceX, 0.03f, 7.5f), new Vector3(0.62f, 0.06f, 0.95f), 1.2f);
            foreach (var (x, z, r) in new[] { (-1.75f, 7.35f, 0.26f), (-1.05f, 7.65f, 0.24f), (-0.4f, 7.4f, 0.27f) })
                g.K.Of(g.M.Stone).AddCylinder(new Vector3(x, 0f, z), r, 0.05f, 8, 1.2f);
        }

        // ------------------------------------------------------------------ fences and gate
        static void Fences(Garden g)
        {
            var m = g.M;
            // West: a low, open yotsume-gaki you see over from the approach, broken by the gate.
            Yotsume(g.K, m, FenceX, Z0 + 0.05f, 6.68f, 1.1f);
            Yotsume(g.K, m, FenceX, 8.32f, Z1 - 0.05f, 1.1f);
            Col(g.T, "FenceW", new Vector3(FenceX, 0.6f, (Z0 + 6.68f) * 0.5f), new Vector3(0.14f, 1.2f, 6.68f - Z0), Quaternion.identity);
            Col(g.T, "FenceW", new Vector3(FenceX, 0.6f, (8.32f + Z1) * 0.5f), new Vector3(0.14f, 1.2f, Z1 - 8.32f), Quaternion.identity);
            // South, east and north: solid split-bamboo kenninji-gaki (the east one stands in front of the cell's bounds).
            var sw = new Vector3(FenceX, 0f, Z0); var se = new Vector3(g.EastX(Z0), 0f, Z0);
            var ne = new Vector3(g.EastX(Z1), 0f, Z1); var nw = new Vector3(FenceX, 0f, Z1);
            KenninjiWithCollider(g, sw, se, 1.8f, Vector3.forward);
            KenninjiWithCollider(g, se, ne, 1.9f, Vector3.left);
            KenninjiWithCollider(g, ne, nw, 1.5f, Vector3.back);
        }

        static void KenninjiWithCollider(Garden g, Vector3 a, Vector3 b, float h, Vector3 inward)
        {
            Kenninji(g.K, g.M, a, b, h, inward);
            var d = b - a;
            Col(g.T, "FenceSolid", (a + b) * 0.5f + Vector3.up * (h * 0.5f + 0.05f), new Vector3(0.14f, h + 0.1f, d.magnitude + 0.1f), Quaternion.LookRotation(d.normalized, Vector3.up));
        }

        /// <summary>A low yotsume-gaki along x = const: log posts, three bamboo rails, sparse verticals, black cord ties.</summary>
        static void Yotsume(KitBatch k, GardenMats m, float x, float z0, float z1, float h)
        {
            int posts = Mathf.Max(1, Mathf.RoundToInt((z1 - z0) / 1.8f));
            for (int i = 0; i <= posts; i++)
                k.Of(m.Weathered).AddCylinder(new Vector3(x, 0f, Mathf.Lerp(z0, z1, i / (float)posts)), 0.045f, h + 0.08f, 7);
            var along = Quaternion.Euler(90f, 0f, 0f);
            float[] rails = { 0.32f, 0.68f, h - 0.06f };
            foreach (float y in rails) k.Of(m.Bamboo).AddCylinder(new Vector3(x + 0.05f, y, z0), 0.017f, z1 - z0, 7, 1f, along);
            for (float z = z0 + 0.15f; z < z1 - 0.08f; z += 0.3f)
            {
                k.Of(m.Bamboo).AddCylinder(new Vector3(x - 0.015f, 0.02f, z), 0.016f, h - 0.02f, 6);
                foreach (float y in rails) k.Box(m.Black, new Vector3(x + 0.02f, y, z), new Vector3(0.075f, 0.022f, 0.035f));
            }
        }

        /// <summary>A kenninji-gaki from a to b (ground points): a backing panel faced with split bamboo slats toward
        /// `inward`, three horizontal battens tied with black cord, a capping rail and end posts.</summary>
        static void Kenninji(KitBatch k, GardenMats m, Vector3 a, Vector3 b, float h, Vector3 inward)
        {
            var d = b - a;
            float len = d.magnitude;
            var dir = d / len;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            var mid = (a + b) * 0.5f;
            inward = Vector3.ProjectOnPlane(inward, Vector3.up).normalized;
            // Only the component across the fence matters (the east fence runs at an angle to the garden's axes).
            var across = rot * Vector3.right;
            if (Vector3.Dot(across, inward) < 0f) across = -across;
            k.Box(m.Bamboo, mid + Vector3.up * (h * 0.5f), new Vector3(0.05f, h, len), rot, 0.7f);
            int n = Mathf.Max(2, Mathf.FloorToInt(len / 0.08f));
            for (int i = 0; i < n; i++)
                k.Box(m.Bamboo, a + dir * ((i + 0.5f) * len / n) + across * 0.033f + Vector3.up * (h * 0.5f), new Vector3(0.016f, h - 0.03f, 0.05f), rot);
            foreach (float y in new[] { 0.3f, h * 0.52f, h - 0.3f })
            {
                k.Box(m.Bamboo, mid + across * 0.062f + Vector3.up * y, new Vector3(0.03f, 0.045f, len), rot);
                for (float s = 0.22f; s < len - 0.1f; s += 0.45f)
                    k.Box(m.Black, a + dir * s + across * 0.08f + Vector3.up * y, new Vector3(0.012f, 0.06f, 0.022f), rot);
            }
            k.Box(m.Bamboo, mid + Vector3.up * (h + 0.025f), new Vector3(0.12f, 0.05f, len + 0.04f), rot);
            k.Of(m.Bamboo).AddCylinder(a - dir * 0.02f + Vector3.up * (h + 0.07f), 0.03f, len + 0.04f, 7, 1f, rot * Quaternion.Euler(90f, 0f, 0f));
            foreach (var p in new[] { a, b }) k.Of(m.Weathered).AddCylinder(p, 0.05f, h + 0.1f, 8);
        }

        /// <summary>
        /// The garden gate: two posts on stone feet, a head beam flush with them (no overhang: a roofed garden gate, nothing
        /// like a shrine gate) under a small gabled roof of weathered shingles, and a paper lantern under the approach-side eave.
        /// </summary>
        static void Gate(Garden g)
        {
            var m = g.M;
            const float z0 = 6.75f, z1 = 8.25f, zc = (z0 + z1) * 0.5f;
            foreach (float z in new[] { z0, z1 })
            {
                g.K.Box(m.Stone, new Vector3(FenceX, 0.07f, z), new Vector3(0.28f, 0.14f, 0.28f), 1.2f);
                g.K.Box(m.Timber, new Vector3(FenceX, 1.19f, z), new Vector3(0.13f, 2.24f, 0.13f));
                Col(g.T, "GatePost", new Vector3(FenceX, 1.1f, z), new Vector3(0.15f, 2.2f, 0.15f), Quaternion.identity);
            }
            float w = z1 - z0 + 0.13f;
            g.K.Box(m.Timber, new Vector3(FenceX, 2.24f, zc), new Vector3(0.15f, 0.16f, w));
            g.K.Box(m.Timber, new Vector3(FenceX, 1.98f, zc), new Vector3(0.09f, 0.08f, w - 0.13f));
            Gable(g.K, m.Weathered, m.Tile, new Vector3(FenceX, 2.32f, zc), 0.6f, 0.34f, w + 0.62f, 0.05f);
            var paper = Chochin(g.T, "GateLantern", new Vector3(FenceX - 0.4f, 2.42f, zc), 0.08f, m.LanternCream, m.Black, 0.55f, out var pivot);
            WindSway.Add(pivot, 4f, 2.6f);
            g.Wave.Add(paper, pivot);
        }

        /// <summary>A small gable roof along local z: two shingle slabs from the ridge down to eaves `run` either side (x),
        /// `rise` below the ridge, `len` long, with a round ridge cap. `eaves` is the centre of the eave line.</summary>
        static void Gable(KitBatch k, Material shingle, Material cap, Vector3 eaves, float run, float rise, float len, float thick)
        {
            float slope = Mathf.Atan2(rise, run) * Mathf.Rad2Deg, slant = Mathf.Sqrt(run * run + rise * rise) + 0.04f;
            foreach (float s in new[] { -1f, 1f })
            {
                var c = eaves + new Vector3(s * run * 0.5f, rise * 0.5f + thick * 0.5f, 0f);
                k.Box(shingle, c, new Vector3(slant, thick, len), Quaternion.Euler(0f, 0f, -s * slope), 1.2f);
            }
            k.Of(cap).AddCylinder(eaves + new Vector3(0f, rise + thick, -len * 0.5f), 0.05f, len, 8, 1f, Quaternion.Euler(90f, 0f, 0f));
        }

        // ------------------------------------------------------------------ bamboo, bench, basin
        /// <summary>A grove of green bamboo filling the narrow south end: five clumps that sway in the wind.</summary>
        static void Grove(Garden g)
        {
            var m = g.M;
            var rnd = new System.Random(5);
            var clumps = new[] { new Vector3(1.55f, 0f, 1.05f), new Vector3(2.45f, 0f, 2.15f), new Vector3(3.2f, 0f, 0.85f), new Vector3(4.0f, 0f, 1.95f), new Vector3(4.65f, 0f, 0.9f) };
            for (int c = 0; c < clumps.Length; c++)
            {
                var pivot = new GameObject("BambooClump").transform;
                pivot.SetParent(g.T, false);
                pivot.localPosition = clumps[c];
                var k = new KitBatch();
                int n = 3 + rnd.Next(2);
                for (int i = 0; i < n; i++)
                {
                    var foot = new Vector3(R(rnd, -0.32f, 0.32f), 0f, R(rnd, -0.3f, 0.3f));
                    float h = R(rnd, 4.6f, 6.8f), r = R(rnd, 0.032f, 0.048f);
                    var lean = new Vector3(R(rnd, -0.35f, 0.35f), 0f, R(rnd, -0.3f, 0.3f));
                    Vector3 P(float u) => foot + Vector3.up * (h * u) + lean * (u * u);
                    var path = new List<Vector3> { P(0f), P(0.33f), P(0.66f), P(1f) };
                    k.Of(m.Culm).AddTube(path, r, 7);
                    for (float y = 0.32f; y < h - 0.2f; y += 0.34f)
                        k.Of(m.Node).AddFrustum(P(y / h) + Vector3.down * 0.006f, r + 0.005f, r + 0.005f, 0.012f, 7, false, false);
                    for (int l = 0; l < 6; l++)
                    {
                        float u = R(rnd, 0.55f, 1f);
                        var side = new Vector3(R(rnd, -0.42f, 0.42f), R(rnd, -0.1f, 0.1f), R(rnd, -0.42f, 0.42f));
                        Blob(k.Of(m.Leaf), P(u) + side, R(rnd, 0.24f, 0.38f), R(rnd, 0.05f, 0.08f), 7);
                    }
                }
                k.Bake(pivot, "Bamboo");
                WindSway.Add(pivot, R(rnd, 1.4f, 2.2f), R(rnd, 2.8f, 4f));
            }
            // Bamboo leaves on the ground and a moss bank in front.
            Blob(g.Soft.Of(m.Moss), new Vector3(3f, 0f, 3.05f), 1.6f, 0.1f, 12);
            Col(g.T, "Grove", new Vector3(3.05f, 1.1f, 1.6f), new Vector3(4f, 2.2f, 2.75f), Quaternion.identity);
        }

        /// <summary>The waiting bench (machiai) against the east fence: a lean-to roof on log posts over a plank seat
        /// covered with red felt, a split-bamboo back.</summary>
        static void Bench(Garden g)
        {
            var m = g.M;
            const float xf = 4.4f, xb = 5.45f, z0 = 3.6f, z1 = 6.2f;
            Kenninji(g.K, m, new Vector3(xb, 0f, z0), new Vector3(xb, 0f, z1), 1.85f, Vector3.left);
            foreach (float z in new[] { z0 + 0.1f, z1 - 0.1f })
            {
                g.K.Of(m.Weathered).AddCylinder(new Vector3(xf, 0f, z), 0.05f, 2.06f, 8);
                Col(g.T, "BenchPost", new Vector3(xf, 1f, z), new Vector3(0.11f, 2f, 0.11f), Quaternion.identity);
            }
            // Lean-to roof falling toward the path.
            float run = xb + 0.15f - (xf - 0.2f), rise = 0.3f, slope = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            g.K.Box(m.Weathered, new Vector3((xb + 0.15f + xf - 0.2f) * 0.5f, 2.18f, (z0 + z1) * 0.5f), new Vector3(Mathf.Sqrt(run * run + rise * rise), 0.05f, z1 - z0 + 0.5f), Quaternion.Euler(0f, 0f, slope), 1.2f);
            g.K.Box(m.Timber, new Vector3(xf, 2.04f, (z0 + z1) * 0.5f), new Vector3(0.1f, 0.1f, z1 - z0 + 0.1f));
            // Seat, legs and the felt.
            g.K.Box(m.Board, new Vector3(5.06f, 0.445f, (z0 + z1) * 0.5f), new Vector3(0.62f, 0.05f, 2.3f));
            foreach (float x in new[] { 4.8f, 5.32f })
                foreach (float z in new[] { 3.82f, 4.9f, 5.98f }) g.K.Box(m.Timber, new Vector3(x, 0.21f, z), new Vector3(0.06f, 0.42f, 0.06f));
            g.K.Box(m.Felt, new Vector3(5.06f, 0.474f, (z0 + z1) * 0.5f), new Vector3(0.58f, 0.008f, 1.9f));
            g.K.Box(m.Felt, new Vector3(4.768f, 0.4f, (z0 + z1) * 0.5f), new Vector3(0.006f, 0.15f, 1.9f));
            Col(g.T, "Bench", new Vector3(5.08f, 0.24f, (z0 + z1) * 0.5f), new Vector3(0.66f, 0.48f, 2.3f), Quaternion.identity);
            Col(g.T, "BenchBack", new Vector3(xb, 0.95f, (z0 + z1) * 0.5f), new Vector3(0.16f, 1.9f, z1 - z0), Quaternion.identity);
        }

        /// <summary>
        /// The stone basin (tsukubai): a round basin of dark water fed drop by drop from a bamboo spout (JapanBasinDrip rings
        /// the water), a flat front stone, and a small stone lantern beside it whose light is the garden's first.
        /// </summary>
        static Light Basin(Garden g)
        {
            var m = g.M;
            var c = new Vector3(2.45f, 0f, 10.6f);
            g.K.Of(m.Stone).AddLathe(c, new[] { new Vector2(0.3f, 0f), new Vector2(0.36f, 0.05f), new Vector2(0.38f, 0.2f), new Vector2(0.34f, 0.33f),
                new Vector2(0.3f, 0.36f), new Vector2(0.24f, 0.365f), new Vector2(0.22f, 0.32f), new Vector2(0.15f, 0.25f), new Vector2(0f, 0.23f) }, 18, 1.2f);
            g.K.Of(m.Water).AddDisc(c + Vector3.up * 0.3f, Vector3.up, 0.215f, 18);
            Col(g.T, "Basin", c + Vector3.up * 0.2f, new Vector3(0.7f, 0.4f, 0.7f), Quaternion.identity);
            // The spout on its post.
            var post = new Vector3(3.05f, 0f, 10.85f);
            g.K.Of(m.Weathered).AddCylinder(post, 0.045f, 0.74f, 8);
            var tip = new Vector3(2.5f, 0.6f, 10.63f); // over the middle of the water
            Pipe(g.K, m.Bamboo, post + Vector3.up * 0.68f, tip, 0.024f);
            g.K.Of(m.Stone).AddCylinder(new Vector3(2.3f, 0f, 9.75f), 0.3f, 0.07f, 8, 1.2f);
            // Drops and rings.
            var drip = new GameObject("BasinDrip");
            drip.transform.SetParent(g.T, false);
            var d = drip.AddComponent<JapanBasinDrip>();
            d.Drops = Drops(drip.transform, tip + new Vector3(-0.015f, -0.02f, -0.005f));
            d.RingMat = Mats.UnlitInstance(new Color(0.8f, 0.86f, 0.9f, 0f), Tex.Ring, true, 1f);
            d.Ring = MeshFactory.Quad("Ripple", drip.transform, 1f, 1f, d.RingMat, new Vector3(tip.x - 0.015f, 0.302f, tip.z - 0.005f), Quaternion.Euler(90f, 0f, 0f)).transform;
            d.Ring.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // The stone lantern beside it.
            var l = new Vector3(3.3f, 0f, 11.5f);
            StoneLantern(g, l, 0.78f);
            Col(g.T, "BasinLantern", l + Vector3.up * 0.55f, new Vector3(0.42f, 1.1f, 0.42f), Quaternion.identity);
            foreach (var (x, z, r) in new[] { (2.95f, 10.05f, 0.55f), (1.45f, 12.45f, 0.45f), (3.6f, 10.9f, 0.45f) }) Blob(g.Soft.Of(m.Moss), new Vector3(x, 0f, z), r, 0.11f, 10);
            return Point(g.T, l + Vector3.up * (0.86f * 0.78f), Warm, 1.5f, 4.2f);
        }

        /// <summary>A small stone garden lantern: foot, post, a lit firebox (its own paper, part of the wave), a pyramid
        /// roof and a finial. Scale 1 is about 1.2 m.</summary>
        static void StoneLantern(Garden g, Vector3 at, float s)
        {
            var m = g.M;
            g.K.Box(m.Stone, at + Vector3.up * 0.08f * s, new Vector3(0.42f, 0.16f, 0.42f) * s, 1.2f);
            g.K.Box(m.Stone, at + Vector3.up * 0.42f * s, new Vector3(0.16f, 0.52f, 0.16f) * s, 1.2f);
            g.K.Box(m.Stone, at + Vector3.up * 0.71f * s, new Vector3(0.36f, 0.06f, 0.36f) * s, 1.2f);
            foreach (float x in new[] { -0.13f, 0.13f })
                foreach (float z in new[] { -0.13f, 0.13f }) g.K.Box(m.Stone, at + new Vector3(x, 0.86f, z) * s, new Vector3(0.05f, 0.24f, 0.05f) * s, 1.2f);
            g.K.Of(m.Stone).AddFrustum(at + Vector3.up * 0.98f * s, 0.34f * s, 0.04f * s, 0.2f * s, 4, true, false, 1.2f, Quaternion.Euler(0f, 45f, 0f));
            g.K.Of(m.Stone).AddSphere(at + Vector3.up * 1.21f * s, 0.05f * s, 8, 5);
            var paper = new Material(m.LanternCream) { name = "GardenFirebox" };
            var box = MeshFactory.Box("Firebox", g.T, at + Vector3.up * 0.86f * s, new Vector3(0.22f, 0.22f, 0.22f) * s, paper, false);
            box.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.Wave.Add(paper, box.transform);
        }

        // ------------------------------------------------------------------ the tea house
        /// <summary>
        /// The tea house: earthen walls between timber posts on a stone plinth, a small crawl-in door slid half open on the
        /// lit room, a latticed paper window, a service door on the south under a navy noren that moves in the wind, a roof
        /// of weathered shingles, and a lantern on a bracket by the north wall lighting the way on to the bell.
        /// </summary>
        static Light TeaHouse(Garden g)
        {
            var m = g.M;
            var k = g.K;
            const float x0 = 3.4f, x1 = 6.35f, z0 = 13.4f, z1 = 16.6f, sill = 0.45f, wall = 2.5f;
            k.Box(m.Stone, new Vector3((x0 + x1) * 0.5f, 0.09f, (z0 + z1) * 0.5f), new Vector3(x1 - x0 + 0.12f, 0.18f, z1 - z0 + 0.12f), 1.2f);
            // Sill beams and the dark lower boards all round.
            k.Box(m.Timber, new Vector3((x0 + x1) * 0.5f, 0.315f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.27f, z1 - z0));
            // West face: crawl-in door, latticed window, plaster between.
            Wall(k, m.Earth, true, x0, 1f, z0, 13.75f, sill, wall);
            Wall(k, m.Earth, true, x0, 1f, 13.75f, 14.45f, 1.15f, wall);
            Wall(k, m.Earth, true, x0, 1f, 14.45f, 15.15f, sill, wall);
            Wall(k, m.Earth, true, x0, 1f, 15.15f, 16.15f, sill, 1f);
            Wall(k, m.Earth, true, x0, 1f, 15.15f, 16.15f, 1.75f, wall);
            Wall(k, m.Earth, true, x0, 1f, 16.15f, z1, sill, wall);
            k.Box(m.Interior, new Vector3(x0 + 0.25f, 0.8f, 14.1f), new Vector3(0.02f, 0.7f, 0.7f));
            k.Box(m.Board, new Vector3(x0 - 0.03f, 0.8f, 14.47f), new Vector3(0.03f, 0.72f, 0.72f));       // the door, slid half open
            k.Box(m.Timber, new Vector3(x0 - 0.02f, 1.17f, 14.1f), new Vector3(0.06f, 0.05f, 0.78f));
            var window = new Material(m.PaperGlow) { name = "TeaHouseShoji" };
            var shoji = MeshFactory.Box("TeaHouseWindow", g.T, new Vector3(x0 + 0.05f, 1.375f, 15.65f), new Vector3(0.02f, 0.75f, 1f), window, false);
            shoji.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.Wave.Add(window, shoji.transform);
            for (int i = 1; i < 4; i++) k.Box(m.Bamboo, new Vector3(x0 - 0.01f, 1.375f, 15.15f + i * 0.25f), new Vector3(0.02f, 0.75f, 0.02f));
            foreach (float y in new[] { 1.25f, 1.5f }) k.Box(m.Bamboo, new Vector3(x0 - 0.02f, y, 15.65f), new Vector3(0.02f, 0.02f, 1f));
            k.Box(m.Timber, new Vector3(x0 - 0.02f, 1.76f, 15.65f), new Vector3(0.05f, 0.04f, 1.06f));
            k.Box(m.Timber, new Vector3(x0 - 0.02f, 0.99f, 15.65f), new Vector3(0.05f, 0.04f, 1.06f));
            // South face: the service door with two paper-topped panels, under the noren.
            Wall(k, m.Earth, false, z0, 1f, x0, 4.45f, sill, wall);
            Wall(k, m.Earth, false, z0, 1f, 5.25f, x1, sill, wall);
            Wall(k, m.Earth, false, z0, 1f, 4.45f, 5.25f, 2.1f, wall);
            foreach (float x in new[] { 4.65f, 5.05f })
            {
                k.Box(m.Board, new Vector3(x, 0.9f, z0 - 0.02f), new Vector3(0.39f, 0.9f, 0.03f));
                k.Box(m.Timber, new Vector3(x, 1.785f, z0 - 0.03f), new Vector3(0.39f, 0.03f, 0.03f));
            }
            var door = new Material(m.PaperGlow) { name = "TeaHouseDoor" };
            var panel = MeshFactory.Box("TeaHouseDoorPaper", g.T, new Vector3(4.85f, 1.73f, z0 - 0.015f), new Vector3(0.78f, 0.68f, 0.02f), door, false);
            panel.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.Wave.Add(door, panel.transform);
            k.Of(m.Bamboo).AddCylinder(new Vector3(4.38f, 2.09f, z0 - 0.12f), 0.014f, 0.94f, 6, 1f, Quaternion.Euler(0f, 0f, -90f));
            foreach (float x in new[] { 4.45f, 4.86f })
                Hang(g.T, "Noren", new Vector3(x, 2.07f, z0 - 0.12f), Vector3.right, 0.39f, 0.72f, m.Navy, 0.035f);
            // North and east faces, plain.
            Wall(k, m.Earth, false, z1, -1f, x0, x1, sill, wall);
            Wall(k, m.Earth, true, x1, -1f, z0, z1, sill, wall);
            // Posts and the wall plate.
            foreach (var (x, z) in new[] { (x0, z0), (x1, z0), (x0, z1), (x1, z1), (x0, 14.45f), (x0, 15.15f), (4.45f, z0), (5.25f, z0) })
                k.Box(m.Timber, new Vector3(x, 1.37f, z), new Vector3(0.12f, 2.4f, 0.12f));
            k.Box(m.Timber, new Vector3((x0 + x1) * 0.5f, 2.57f, z0), new Vector3(x1 - x0 + 0.12f, 0.14f, 0.13f));
            k.Box(m.Timber, new Vector3((x0 + x1) * 0.5f, 2.57f, z1), new Vector3(x1 - x0 + 0.12f, 0.14f, 0.13f));
            k.Box(m.Timber, new Vector3(x0, 2.57f, (z0 + z1) * 0.5f), new Vector3(0.13f, 0.14f, z1 - z0));
            k.Box(m.Timber, new Vector3(x1, 2.57f, (z0 + z1) * 0.5f), new Vector3(0.13f, 0.14f, z1 - z0));
            k.Box(m.Earth, new Vector3(x0 + 0.05f, 2.75f, (z0 + z1) * 0.5f), new Vector3(0.1f, 0.24f, z1 - z0), 2f);
            k.Box(m.Earth, new Vector3(x1 - 0.05f, 2.75f, (z0 + z1) * 0.5f), new Vector3(0.1f, 0.24f, z1 - z0), 2f);
            // Roof: shingle slabs from the eaves (0.55 out) to a ridge along z, plaster gables.
            float xr = (x0 + x1) * 0.5f, run = xr - (x0 - 0.55f), rise = 0.92f;
            Gable(k, m.Weathered, m.Tile, new Vector3(xr, 2.6f, (z0 + z1) * 0.5f), run, rise, z1 - z0 + 1f, 0.09f);
            foreach (float z in new[] { z0, z1 }) Tri2(k.Of(m.Earth), new Vector3(x0, 2.64f, z), new Vector3(x1, 2.64f, z), new Vector3(xr, 2.6f + rise - 0.05f, z));
            // The lantern on its bracket by the north wall.
            k.Box(m.Timber, new Vector3(4.3f, 2.3f, z1 + 0.2f), new Vector3(0.05f, 0.05f, 0.42f));
            var paper = Chochin(g.T, "TeaHouseLantern", new Vector3(4.3f, 2.28f, z1 + 0.38f), 0.06f, m.LanternRed, m.Black, 0.6f, out var pivot);
            WindSway.Add(pivot, 3.5f, 2.4f);
            g.Wave.Add(paper, pivot);
            // The shoe stone before the crawl-in door, moss at the corners.
            k.Box(m.Stone, new Vector3(x0 - 0.33f, 0.09f, 14.1f), new Vector3(0.5f, 0.14f, 0.62f), 1.2f);
            Blob(g.Soft.Of(m.Moss), new Vector3(3.15f, 0f, 16.95f), 0.6f, 0.11f, 10);
            Blob(g.Soft.Of(m.Moss), new Vector3(3.2f, 0f, 13.05f), 0.45f, 0.1f, 10);
            Col(g.T, "TeaHouse", new Vector3((x0 + x1) * 0.5f, 1.3f, (z0 + z1) * 0.5f), new Vector3(x1 - x0 + 0.12f, 2.6f, z1 - z0 + 0.12f), Quaternion.identity);
            return Point(g.T, new Vector3(4.3f, 1.98f, z1 + 0.38f), new Color(1f, 0.6f, 0.36f), 1.4f, 4.6f);
        }

        /// <summary>An earthen wall panel 0.1 m thick standing on the `inward` side (+1 / -1) of a line: along z at x = c
        /// (alongZ) or along x at z = c, from a0 to a1 and v0 to v1.</summary>
        static void Wall(KitBatch k, Material m, bool alongZ, float c, float inward, float a0, float a1, float v0, float v1)
        {
            float w = c + inward * 0.05f;
            var size = alongZ ? new Vector3(0.1f, v1 - v0, a1 - a0) : new Vector3(a1 - a0, v1 - v0, 0.1f);
            var centre = alongZ ? new Vector3(w, (v0 + v1) * 0.5f, (a0 + a1) * 0.5f) : new Vector3((a0 + a1) * 0.5f, (v0 + v1) * 0.5f, w);
            k.Box(m, centre, size, 2f);
        }

        // ------------------------------------------------------------------ the bell
        /// <summary>
        /// The bell frame at the north end: four posts on stone feet, head beams and a hipped tiled roof; a bronze bell with
        /// raised bands and a striking boss hangs from the middle beam, and a log on two ropes hangs ready to swing into it.
        /// JapanGardenBell runs the swing, the note and the lanterns.
        /// </summary>
        static void Bell(Garden g, ParticleSystem leaves)
        {
            var m = g.M;
            var k = g.K;
            const float xa = 2.6f, xb = 4.4f, za = 20.8f, zb = 23.6f, xc = 3.5f;
            var stand = new GameObject("BellFrame").transform;
            stand.SetParent(g.T, false);
            foreach (float x in new[] { xa, xb })
                foreach (float z in new[] { za, zb })
                {
                    k.Box(m.Stone, new Vector3(x, 0.07f, z), new Vector3(0.32f, 0.14f, 0.32f), 1.2f);
                    k.Box(m.Timber, new Vector3(x, 1.46f, z), new Vector3(0.14f, 2.64f, 0.14f));
                    Col(stand, "BellPost", new Vector3(x, 1.4f, z), new Vector3(0.16f, 2.8f, 0.16f), Quaternion.identity);
                }
            foreach (float x in new[] { xa, xb }) k.Box(m.Timber, new Vector3(x, 2.74f, (za + zb) * 0.5f), new Vector3(0.16f, 0.16f, zb - za + 0.3f));
            foreach (float z in new[] { za, zb }) k.Box(m.Timber, new Vector3(xc, 2.74f, z), new Vector3(xb - xa + 0.3f, 0.16f, 0.16f));
            k.Box(m.Timber, new Vector3(xc, 2.62f, (za + zb) * 0.5f), new Vector3(0.14f, 0.12f, zb - za));
            // Hipped roof: eaves rectangle up to a short ridge along z.
            var tb = k.Of(m.Tile);
            float ey = 2.86f, ry = 3.6f;
            Vector3 e0 = new Vector3(xa - 0.55f, ey, za - 0.55f), e1 = new Vector3(xb + 0.55f, ey, za - 0.55f), e2 = new Vector3(xb + 0.55f, ey, zb + 0.55f), e3 = new Vector3(xa - 0.55f, ey, zb + 0.55f);
            Vector3 r0 = new Vector3(xc, ry, za + 0.4f), r1 = new Vector3(xc, ry, zb - 0.4f);
            Face(tb, e1, e2, r1, r0, Vector3.right + Vector3.up);
            Face(tb, e3, e0, r0, r1, Vector3.left + Vector3.up);
            Tri(tb, e0, e1, r0, Vector3.back + Vector3.up);
            Tri(tb, e2, e3, r1, Vector3.forward + Vector3.up);
            Face(tb, e0, e1, e2, e3, Vector3.down);
            k.Of(m.Tile).AddCylinder(r0 + new Vector3(0f, 0.03f, -0.1f), 0.06f, (zb - za) - 0.6f, 8, 1f, Quaternion.Euler(90f, 0f, 0f));
            foreach (var (a, b) in new[] { (e0, e1), (e1, e2), (e2, e3), (e3, e0) }) Rod(k, m.Timber, a + Vector3.down * 0.06f, b + Vector3.down * 0.06f, 0.08f);

            // The bell on its pivot under the middle beam.
            var bell = new GameObject("Bell").transform;
            bell.SetParent(stand, false);
            bell.localPosition = new Vector3(xc, 2.56f, 23f);
            var bk = new KitBatch();
            bk.Box(m.Black, new Vector3(0f, -0.03f, 0f), new Vector3(0.03f, 0.06f, 0.03f));
            bk.Of(m.Bronze).AddTorus(new Vector3(0f, -0.1f, 0f), 0.055f, 0.02f, 14, 5, Quaternion.Euler(90f, 0f, 0f));
            var mouth = new Vector3(0f, -0.98f, 0f);
            bk.Of(m.Bronze).AddLathe(mouth, new[] { new Vector2(0f, 0.06f), new Vector2(0.305f, 0.06f), new Vector2(0.31f, 0.01f), new Vector2(0.33f, 0f),
                new Vector2(0.336f, 0.03f), new Vector2(0.318f, 0.08f), new Vector2(0.3f, 0.22f), new Vector2(0.29f, 0.46f), new Vector2(0.284f, 0.62f),
                new Vector2(0.268f, 0.72f), new Vector2(0.22f, 0.79f), new Vector2(0.13f, 0.815f), new Vector2(0f, 0.825f) }, 24);
            foreach (var (y, r) in new[] { (0.09f, 0.318f), (0.5f, 0.288f), (0.73f, 0.262f) }) bk.Of(m.Bronze).AddTorus(mouth + Vector3.up * y, r, 0.011f, 24, 4, Quaternion.identity);
            for (int i = 0; i < 4; i++)
            {
                var q = Quaternion.Euler(0f, 45f + i * 90f, 0f);
                bk.Box(m.Bronze, mouth + q * new Vector3(0f, 0.31f, 0.296f), new Vector3(0.026f, 0.42f, 0.02f), q);
            }
            bk.Of(m.Bronze).AddDisc(mouth + new Vector3(0f, 0.27f, -0.305f), Vector3.back, 0.06f, 14);
            bk.Of(m.Bronze).AddTorus(mouth + new Vector3(0f, 0.27f, -0.302f), 0.065f, 0.012f, 14, 4, Quaternion.Euler(90f, 0f, 0f));
            bk.Bake(bell, "Bell");
            Col(stand, "BellBody", new Vector3(xc, 2.02f, 23f), new Vector3(0.66f, 0.9f, 0.66f), Quaternion.identity);

            // The log, hung level on two ropes from the middle beam, its nose a hand's width from the boss.
            const float ropeTop = 2.56f, logY = 1.9f, logZ = 22.01f;
            var log = new GameObject("BellLog").transform;
            log.SetParent(stand, false);
            log.localPosition = new Vector3(xc, logY, logZ);
            var lk = new KitBatch();
            lk.Of(m.Board).AddCylinder(new Vector3(0f, 0f, -0.6f), 0.075f, 1.2f, 10, 1f, Quaternion.Euler(90f, 0f, 0f));
            foreach (float z in new[] { -0.42f, 0.42f }) lk.Of(m.Rope).AddTorus(new Vector3(0f, 0f, z), 0.078f, 0.012f, 12, 4, Quaternion.Euler(90f, 0f, 0f));
            lk.Bake(log, "Log");
            var ropes = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                var r = new GameObject("BellRope").transform;
                r.SetParent(stand, false);
                r.localPosition = new Vector3(xc, ropeTop, logZ + (i == 0 ? -0.42f : 0.42f));
                MeshFactory.Cylinder("Rope", r, new Vector3(0f, -(ropeTop - logY - 0.06f), 0f), 0.012f, ropeTop - logY - 0.06f, m.Rope, 6, false);
                ropes[i] = r;
            }
            var use = new GameObject("RingTheBell");
            use.transform.SetParent(stand, false);
            use.transform.localPosition = new Vector3(xc, logY, logZ);
            var trigger = use.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(0.4f, 0.4f, 1.3f);
            var ring = use.AddComponent<JapanGardenBell>();
            ring.Log = log;
            ring.Bell = bell;
            ring.RopeA = ropes[0];
            ring.RopeB = ropes[1];
            ring.RopeLength = ropeTop - logY;
            ring.Wave = g.Wave;
            ring.Leaves = leaves;
            ring.MarkerPoint = log;
            ring.Init();

            // A paper lantern under the frame's west eave (the first to brighten) and moss round the feet.
            var paper = Chochin(stand, "BellLantern", new Vector3(xa - 0.3f, 2.855f, 22.2f), 0.16f, m.LanternCream, m.Black, 0.55f, out var pivot);
            WindSway.Add(pivot, 3.5f, 2.5f);
            g.Wave.Add(paper, pivot);
            foreach (var (x, z, r) in new[] { (4.75f, 24.45f, 0.75f), (5.6f, 20.9f, 0.7f), (2.25f, 24.6f, 0.5f) }) Blob(g.Soft.Of(m.Moss), new Vector3(x, 0f, z), r, 0.11f, 10);
        }

        // ------------------------------------------------------------------ lanterns along the fence
        /// <summary>
        /// Paper lanterns strung between bamboo poles along the inside of the west fence (they glow over it toward the
        /// approach), each on its own pivot swaying in the wind and each part of the bell's wave. Kept above head height.
        /// </summary>
        static void LanternLine(Garden g)
        {
            var m = g.M;
            float[] poles = { 2.4f, 6.1f, 9f, 12.8f, 16.6f, 20.4f, 24.2f };
            const float x = 0.95f, top = 2.72f, sag = 0.16f;
            foreach (float z in poles)
            {
                g.K.Of(m.Bamboo).AddCylinder(new Vector3(x, 0f, z), 0.032f, top + 0.06f, 7);
                g.K.Of(m.Black).AddCylinder(new Vector3(x, top - 0.04f, z), 0.036f, 0.03f, 7);
                var col = new GameObject("LanternPole");
                col.transform.SetParent(g.T, false);
                col.transform.localPosition = new Vector3(x, 1.3f, z);
                var cap = col.AddComponent<CapsuleCollider>();
                cap.radius = 0.06f;
                cap.height = 2.6f;
            }
            int n = 0;
            for (int i = 0; i + 1 < poles.Length; i++)
            {
                if (poles[i] < 7f && poles[i + 1] > 7f) continue; // the gate's roof stands in this span
                var a = new Vector3(x, top, poles[i]);
                var b = new Vector3(x, top, poles[i + 1]);
                Vector3 P(float u) => Vector3.Lerp(a, b, u) + Vector3.down * (sag * 4f * u * (1f - u));
                for (int s = 0; s < 12; s++) Pipe(g.K, m.Rope, P(s / 12f), P((s + 1) / 12f), 0.006f);
                foreach (float u in new[] { 0.25f, 0.5f, 0.75f })
                {
                    var paper = Chochin(g.T, "FenceLantern", P(u), 0.1f, n % 3 == 1 ? m.LanternRed : m.LanternCream, m.Black, 0.62f, out var pivot);
                    WindSway.Add(pivot, 5f, 2.2f + (n % 4) * 0.3f);
                    g.Wave.Add(paper, pivot);
                    n++;
                }
            }
        }

        // ------------------------------------------------------------------ path, moss, rocks, pine, leaves
        static void Dressing(Garden g)
        {
            var m = g.M;
            var rnd = new System.Random(23);
            // Stepping stones from the gate past the basin and the tea house to the bell, and a branch to the bench.
            var path = new[]
            {
                new Vector2(1.15f, 7.6f), new Vector2(1.7f, 7.95f), new Vector2(2.15f, 8.6f), new Vector2(1.75f, 10.45f), new Vector2(1.85f, 11.4f),
                new Vector2(2.25f, 12.2f), new Vector2(2.55f, 13.05f), new Vector2(2.65f, 15.1f), new Vector2(2.6f, 16f), new Vector2(2.5f, 16.95f),
                new Vector2(2.55f, 17.85f), new Vector2(2.8f, 18.75f), new Vector2(3f, 19.65f), new Vector2(3.3f, 20.45f),
                new Vector2(2.65f, 7.35f), new Vector2(3.35f, 6.85f), new Vector2(3.95f, 5.95f),
            };
            foreach (var p in path)
                g.K.Of(m.StoneDark).AddCylinder(new Vector3(p.x, 0f, p.y), R(rnd, 0.21f, 0.28f), 0.055f, 8, 1.2f, Quaternion.Euler(0f, R(rnd, 0f, 90f), 0f));
            // Moss banks along the fences, garden rocks, a small cloud-pruned pine by the basin.
            foreach (var (x, z, r) in new[] { (1.15f, 4.6f, 0.55f), (1.2f, 9.9f, 0.5f), (1.25f, 13.7f, 0.6f), (1.2f, 17.5f, 0.55f), (1.25f, 21.4f, 0.6f),
                (1.3f, 24.7f, 0.5f), (5.6f, 8.8f, 0.8f), (6.05f, 12.1f, 0.6f), (6.35f, 19.1f, 0.75f), (6.6f, 24.3f, 0.7f) })
                Blob(g.Soft.Of(m.Moss), new Vector3(x, 0f, z), r, 0.1f, 10);
            foreach (var (x, z, r) in new[] { (5.2f, 7.9f, 0.34f), (1.45f, 14.6f, 0.28f), (5.9f, 18f, 0.4f), (5.1f, 24.7f, 0.3f) })
                Blob(g.K.Of(m.StoneDark), new Vector3(x, 0f, z), r, r * 0.6f, 9);
            Pine(g, new Vector3(1.4f, 0f, 12.15f), 0.36f, 31);
            Col(g.T, "Pine", new Vector3(1.4f, 0.6f, 12.15f), new Vector3(0.24f, 1.2f, 0.24f), Quaternion.identity);
            // Fallen maple leaves, thickest under the overhanging trees on the east side.
            for (int i = 0; i < 70; i++)
            {
                float z = R(rnd, 3.2f, 25f), x = Mathf.Lerp(FenceX + 0.3f, g.EastX(z) - 0.2f, Mathf.Sqrt((float)rnd.NextDouble()));
                if (x > 3.3f && x < 6.45f && z > 13.3f && z < 16.7f) continue; // not inside the tea house
                var c = new Vector3(x, Top + 0.004f + (i % 5) * 0.0015f, z);
                var q = Quaternion.Euler(0f, R(rnd, 0f, 360f), 0f);
                var mat = i % 3 == 0 ? m.MapleGold : m.MapleRed;
                var hw = q * new Vector3(0.025f, 0f, 0f);
                var hd = q * new Vector3(0f, 0f, 0.02f);
                g.Soft.Of(mat).AddQuad(c - hw - hd, c - hw + hd, c + hw + hd, c + hw - hd, Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
            }
        }

        /// <summary>A small garden pine: a leaning trunk with flat cloud-pruned pads (scale 1 is about 4.8 m).</summary>
        static void Pine(Garden g, Vector3 at, float scale, int seed)
        {
            var rnd = new System.Random(seed);
            float h = 4.8f * scale;
            Vector3 Trunk(float u) => at + new Vector3((-0.9f * u * u - 0.12f * Mathf.Sin(u * 7f)) * scale, h * 0.8f * u, 0.25f * Mathf.Sin(u * 4f) * scale);
            var path = new List<Vector3>();
            for (int i = 0; i <= 6; i++) path.Add(Trunk(i / 6f));
            g.K.Of(g.M.Weathered).AddTube(path, 0.12f * scale, 8);
            foreach (var (u, dx, dz, r) in new[] { (0.4f, -0.85f, 0.1f, 1.05f), (0.6f, 0.65f, 0.3f, 0.9f), (0.8f, -0.5f, -0.5f, 0.85f), (1f, 0.2f, 0.2f, 0.8f) })
            {
                var from = Trunk(u * 0.95f);
                var pad = from + new Vector3(dx, 0.15f + (float)rnd.NextDouble() * 0.2f, dz) * scale;
                Pipe(g.K, g.M.Weathered, from, pad, 0.05f * scale + 0.004f);
                Blob(g.Soft.Of(g.M.Leaf), pad, r * scale, r * 0.32f * scale, 10);
            }
        }

        // ------------------------------------------------------------------ particles
        /// <summary>Maple leaves drifting down from the trees that lean over the garden (a burst when the bell rings).</summary>
        static ParticleSystem FallingLeaves(Transform parent, Vector3 centre, Vector3 size)
        {
            var go = new GameObject("FallingLeaves");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 10f);
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.035f, 0.055f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.022f, 0.032f);
            main.startSizeZ = 1f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.5f, 0.1f, 0.04f, 1f), new Color(0.62f, 0.3f, 0.07f, 1f));
            main.maxParticles = 140;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 1.6f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.6f, -0.38f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.22f;
            noise.frequency = 0.45f;
            noise.scrollSpeed = 0.2f;
            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-2.6f, 2.6f);
            FadeInOut(ps, 0.08f, 0.85f);
            Finish(go, false, 1.1f);
            ps.Play();
            return ps;
        }

        /// <summary>Single drops falling from the basin's spout (emitted by JapanBasinDrip).</summary>
        static ParticleSystem Drops(Transform parent, Vector3 at)
        {
            var go = new GameObject("Drops");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.26f;
            main.startSpeed = 0f;
            main.startSize = 0.014f;
            main.startColor = new Color(0.75f, 0.85f, 0.95f, 0.85f);
            main.gravityModifier = 1f;
            main.maxParticles = 8;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            Finish(go, false, 1.3f);
            return ps;
        }

        static void FadeInOut(ParticleSystem ps, float inAt, float outFrom)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inAt), new GradientAlphaKey(1f, outFrom), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
        }

        static void Finish(GameObject go, bool additive, float intensity)
        {
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = Mats.Particle(Tex.SoftDot, additive, Color.white, intensity, Mats.StencilNotEqual);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // ------------------------------------------------------------------ helpers
        /// <summary>
        /// A chochin on a cord under its own pivot (the hang point, for WindSway): a ribbed paper barrel with black caps.
        /// Returns the paper's own material instance (so the bell's wave can brighten it alone).
        /// </summary>
        static Material Chochin(Transform parent, string name, Vector3 hang, float cord, Material paperBase, Material black, float scale, out Transform pivot)
        {
            pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = hang;
            float H = 0.5f * scale, R = 0.22f * scale;
            var centre = Vector3.down * (cord + H * 0.5f + 0.035f * scale);
            var prof = new List<Vector2>();
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f;
                prof.Add(new Vector2(R * Mathf.Sqrt(1f - 0.55f * (2f * t - 1f) * (2f * t - 1f)), (t - 0.5f) * H));
            }
            var paper = new Material(paperBase) { name = paperBase.name + "_Garden" };
            var k = new KitBatch();
            k.Of(paper).AddLathe(centre, prof, 14);
            var b = k.Of(black);
            for (int i = 1; i < 8; i += 2) b.AddTorus(centre + Vector3.up * prof[i].y, prof[i].x + 0.002f, 0.0035f * Mathf.Max(0.7f, scale), 14, 3, Quaternion.identity);
            b.AddCylinder(centre + Vector3.up * (-H * 0.5f - 0.035f * scale), R * 0.72f, 0.05f * scale, 12);
            b.AddCylinder(centre + Vector3.up * (H * 0.5f - 0.015f * scale), R * 0.72f, 0.05f * scale, 12);
            b.AddCylinder(new Vector3(0f, -cord, 0f), 0.004f, cord, 4);
            k.Bake(pivot, name, false);
            return paper;
        }

        /// <summary>A cloth hanging from a horizontal rod (its top edge fixed, the bottom moving): a ClothFlag turned so its
        /// free edge is the bottom. It starts at `rodStart`, runs `width` along `along` and hangs `length` straight down.</summary>
        static ClothFlag Hang(Transform parent, string name, Vector3 rodStart, Vector3 along, float width, float length, Material mat, float wave)
        {
            var f = ClothFlag.Create(parent, name, rodStart, length, width, mat, 0f, wave);
            var y = -along.normalized;
            var z = Vector3.Cross(Vector3.down, y);
            f.transform.localRotation = Quaternion.LookRotation(z, y);
            return f;
        }

        static Light Point(Transform parent, Vector3 at, Color color, float intensity, float range)
        {
            var go = new GameObject("PointLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        static BoxCollider Col(Transform parent, string name, Vector3 centre, Vector3 size, Quaternion rot, int layer = Layers.Default)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = rot;
            var c = go.AddComponent<BoxCollider>();
            c.size = size;
            return c;
        }

        /// <summary>A round pipe (bamboo, rope, branch) between two points.</summary>
        static void Pipe(KitBatch k, Material m, Vector3 a, Vector3 b, float r)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            k.Of(m).AddCylinder(a, r, len, 6, 1f, Quaternion.FromToRotation(Vector3.up, d / len));
        }

        /// <summary>A square rod between two points.</summary>
        static void Rod(KitBatch k, Material m, Vector3 a, Vector3 b, float thick)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            d /= len;
            k.Box(m, (a + b) * 0.5f, new Vector3(thick, thick, len), Quaternion.LookRotation(d, Mathf.Abs(d.y) > 0.98f ? Vector3.forward : Vector3.up));
        }

        /// <summary>A quad whose front faces `outward` (its winding is flipped if need be).</summary>
        static void Face(MeshBuilder b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p3 - p0), outward) < 0f) { var t = p1; p1 = p3; p3 = t; }
            b.AddQuad(p0, p1, p2, p3, Vector2.zero, new Vector2(Vector3.Distance(p0, p1) / 1.5f, 0f), new Vector2(Vector3.Distance(p0, p1) / 1.5f, Vector3.Distance(p0, p3) / 1.5f), new Vector2(0f, Vector3.Distance(p0, p3) / 1.5f));
        }

        /// <summary>A triangle whose front faces `outward`.</summary>
        static void Tri(MeshBuilder b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), outward) < 0f) { var t = p1; p1 = p2; p2 = t; }
            b.AddTriangle(p0, p1, p2, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 1f));
        }

        /// <summary>A flat triangle visible from both sides.</summary>
        static void Tri2(MeshBuilder b, Vector3 a, Vector3 b1, Vector3 c)
        {
            b.AddTriangle(a, b1, c, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 1f));
            b.AddTriangle(a, c, b1, Vector2.zero, new Vector2(0f, 1f), new Vector2(1f, 0f));
        }

        /// <summary>A flattened ellipsoid (radius r, half-height h): moss cushions, foliage pads, garden rocks.</summary>
        static void Blob(MeshBuilder b, Vector3 c, float r, float h, int seg)
        {
            var prof = new List<Vector2>();
            for (int i = 0; i <= 6; i++) { float a = Mathf.Lerp(-90f, 90f, i / 6f) * Mathf.Deg2Rad; prof.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * h)); }
            b.AddLathe(c, prof, seg);
        }

        static float R(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
    }

    /// <summary>
    /// The tea garden's bell (Expand_Japan): use the hanging log to ring it. The log is drawn back on its two ropes and
    /// swings in level; the bell rings low and sways, a few maple leaves shake loose and the lanterns brighten one after
    /// another from the bell back to the gate (JapanLanternWave). Always works and cannot go wrong; the log has to come
    /// to rest before it swings again.
    /// </summary>
    public class JapanGardenBell : Interactable
    {
        public Transform Log, Bell, RopeA, RopeB;
        public float RopeLength = 0.66f;
        public JapanLanternWave Wave;
        public ParticleSystem Leaves;

        Vector3 m_LogRest;
        float m_T = -1f, m_Swing = -1f;
        bool m_Struck;

        /// <summary>Times the bell has been struck (tests).</summary>
        public int Rings { get; private set; }

        public void Init()
        {
            m_LogRest = Log.localPosition;
            Prompt = "Ring the bell";
            MaxDistance = 2.6f;
        }

        public override bool CanUse(Interactor who) => base.CanUse(who) && m_T < 0f;

        public override void Use(Interactor who)
        {
            if (!CanUse(who)) return;
            m_T = 0f;
            m_Struck = false;
            AudioDirector.I?.Play("grab", Log.position, 0.45f, 0.8f);
            base.Use(who);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (m_T >= 0f)
            {
                m_T += dt;
                float a; // degrees: negative draws the log back, positive swings it toward the bell
                if (m_T < 0.75f) a = -24f * Smooth(m_T / 0.75f);
                else if (m_T < 0.95f) { float u = (m_T - 0.75f) / 0.2f; a = Mathf.Lerp(-24f, 7.5f, u * u); }
                else
                {
                    if (!m_Struck) { m_Struck = true; Strike(); }
                    float u = m_T - 0.95f;
                    a = 7.5f * Mathf.Exp(-1.7f * u) * Mathf.Cos(4.4f * u);
                    if (m_T > 3.3f) { a = 0f; m_T = -1f; }
                }
                SetLog(a);
            }
            if (m_Swing >= 0f)
            {
                m_Swing += dt;
                float s = 4.5f * Mathf.Exp(-0.9f * m_Swing) * Mathf.Sin(m_Swing * Mathf.PI * 2f / 1.6f);
                if (m_Swing > 6f) { m_Swing = -1f; s = 0f; }
                Bell.localRotation = Quaternion.Euler(-s, 0f, 0f); // the bottom swings away from the log (+z) first
            }
        }

        void SetLog(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            Log.localPosition = m_LogRest + new Vector3(0f, RopeLength * (1f - Mathf.Cos(r)), RopeLength * Mathf.Sin(r));
            var q = Quaternion.Euler(-degrees, 0f, 0f);
            if (RopeA != null) RopeA.localRotation = q;
            if (RopeB != null) RopeB.localRotation = q;
        }

        void Strike()
        {
            Rings++;
            m_Swing = 0f;
            var p = Bell.position + Vector3.down * 0.5f;
            AudioDirector.I?.Play("clock_chime", p, 0.9f, 0.55f);
            AudioDirector.I?.Play("drop_hard", p, 0.3f, 0.7f);
            Wave?.Run(p);
            if (Leaves != null) Leaves.Emit(16);
            Hud.I?.ShowCaption("a low bell note rolls through the garden", 3f);
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
    }

    /// <summary>
    /// The tea garden's lanterns, brightening one after another when the bell rings (JapanGardenBell): each swells and
    /// settles over a second and a half, in order of distance from the bell, and the garden's two lights rise with their
    /// lanterns. Slow and gentle (no flashing).
    /// </summary>
    public class JapanLanternWave : MonoBehaviour
    {
        const float Swell = 1.5f, MetresPerSecond = 6f;
        readonly List<Material> m_Mats = new List<Material>();
        readonly List<Color> m_Base = new List<Color>();
        readonly List<Transform> m_At = new List<Transform>();
        readonly List<Light> m_Lights = new List<Light>();
        readonly List<float> m_LightBase = new List<float>();
        readonly List<float> m_Delay = new List<float>();
        float m_Start = -1f;

        /// <summary>Lanterns in the wave (tests).</summary>
        public int Count => m_Mats.Count;
        public bool Running => m_Start >= 0f;

        public void Add(Material paper, Transform at)
        {
            if (paper == null || at == null) return;
            m_Mats.Add(paper);
            m_Base.Add(paper.HasProperty("_EmissionColor") ? paper.GetColor("_EmissionColor") : Color.black);
            m_At.Add(at);
        }

        public void AddLight(Light l)
        {
            if (l == null) return;
            m_Lights.Add(l);
            m_LightBase.Add(l.intensity);
        }

        public void Run(Vector3 from)
        {
            m_Delay.Clear();
            foreach (var t in m_At) m_Delay.Add(0.1f + Vector3.Distance(from, t.position) / MetresPerSecond);
            foreach (var l in m_Lights) m_Delay.Add(0.1f + Vector3.Distance(from, l.transform.position) / MetresPerSecond);
            m_Start = Time.time;
        }

        void Update()
        {
            if (m_Start < 0f) return;
            float t = Time.time - m_Start;
            bool live = false;
            for (int i = 0; i < m_Mats.Count; i++)
            {
                float x = t - m_Delay[i];
                if (x < Swell) live = true;
                m_Mats[i].SetColor("_EmissionColor", m_Base[i] * (1f + 1.9f * Bump(x)));
            }
            for (int i = 0; i < m_Lights.Count; i++)
            {
                float x = t - m_Delay[m_Mats.Count + i];
                if (x < Swell) live = true;
                if (m_Lights[i] != null) m_Lights[i].intensity = m_LightBase[i] * (1f + 0.9f * Bump(x));
            }
            if (!live) m_Start = -1f;
        }

        static float Bump(float x)
        {
            if (x <= 0f || x >= Swell) return 0f;
            float s = Mathf.Sin(x / Swell * Mathf.PI);
            return s * s;
        }
    }

    /// <summary>The tea garden's stone basin: a drop falls from the bamboo spout every couple of seconds and a ring spreads
    /// and fades on the dark water where it lands.</summary>
    public class JapanBasinDrip : MonoBehaviour
    {
        public ParticleSystem Drops;
        public Transform Ring;
        public Material RingMat;
        public float Period = 1.8f, Fall = 0.24f;
        float m_T, m_Since = 99f;

        void Update()
        {
            float dt = Time.deltaTime;
            m_T += dt;
            m_Since += dt;
            if (m_T >= Period)
            {
                m_T = 0f;
                m_Since = 0f;
                if (Drops != null) Drops.Emit(1);
            }
            if (Ring == null || RingMat == null) return;
            float u = (m_Since - Fall) / 1.1f;
            bool on = u >= 0f && u <= 1f;
            Ring.localScale = Vector3.one * Mathf.Lerp(0.03f, 0.34f, Mathf.Clamp01(u)); // stays on the water (radius 0.2)
            RingMat.SetColor("_Color", new Color(0.8f, 0.86f, 0.9f, on ? 0.45f * (1f - u) : 0f));
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    // Place detail for Riyadh (playtest: "the places look cheap and bare"). The plaza becomes a Najdi square at dusk: mud-plaster
    // houses with stepped crenellations, triangular vents (a few lit), palm-trunk spouts and painted doors on the west and south
    // (Detail_RiyadhNajdi.cs); a crenellated low wall and a round watchtower to the north, kept low so the skyline reads over
    // the arches; a shaded majlis set for guests; geometric floor bands; palms, lantern posts, a well and a shade sail. The
    // east side stays low and open: the sun sets behind it and anything tall there would shadow the whole plaza. No
    // inscriptions or sacred motifs. The city beyond ~25 m is built in Landmarks.cs / Vista.cs.
    public partial class RiyadhCell
    {
        /// <summary>
        /// The plaza's materials. Linear-space rendering: a textured material shows its texture times the tint, so the tints
        /// stay pale and the texture carries the colour.
        /// </summary>
        sealed class NajdiMats
        {
            public Material Mud, MudDark, Gypsum, Trim, Paving, InlayDark, InlayLight, InlayRed, Door, PalmWood, Arish, Dark,
                GlowTri, Carpet, CushionRed, CushionOchre, Brass, Iron, LampGlow, Leaf, Water, Sail;
            public Material[] Paint;  // red, green, ochre, blue
            public Material PaintWhite; // also the glazed coffee cups
        }

        static NajdiMats PlazaMats() => new NajdiMats
        {
            Mud = Mats.Lit(new Color(0.88f, 0.71f, 0.56f), 0.15f, 0f, "plaster_warm"),
            MudDark = Mats.Lit(new Color(0.71f, 0.56f, 0.44f), 0.15f, 0f, "plaster_warm"),
            Gypsum = Mats.Lit(new Color(1f, 0.98f, 0.95f), 0.2f, 0f, "plaster_warm"),
            Trim = Mats.Lit(new Color(0.95f, 0.85f, 0.72f), 0.2f, 0f, "sandstone_blocks"),
            Paving = Mats.Lit(new Color(0.76f, 0.67f, 0.56f), 0.3f, 0f, "plaza_stone"),
            InlayDark = Mats.Lit(new Color(0.55f, 0.45f, 0.38f), 0.35f, 0f, "plaza_stone"),
            InlayLight = Mats.Lit(new Color(1f, 0.98f, 0.94f), 0.35f, 0f, "limestone_blocks"),
            InlayRed = Mats.Lit(new Color(0.85f, 0.85f, 0.85f), 0.3f, 0f, "plaster_terracotta"),
            Door = Mats.Lit(new Color(0.9f, 0.88f, 0.86f), 0.35f, 0f, "wood_dark"),
            PalmWood = Mats.Lit(new Color(1f, 0.95f, 0.9f), 0.3f, 0f, "wood_dark"),
            Arish = Mats.Lit(new Color(0.85f, 0.75f, 0.55f), 0.2f, 0f, "wood_oak_floor"),
            Dark = Mats.Lit(new Color(0.05f, 0.035f, 0.03f), 0.2f),
            GlowTri = Mats.Emissive(new Color(0.6f, 0.4f, 0.22f), new Color(0.95f, 0.62f, 0.3f)),
            Carpet = Mats.Lit(Color.white, 0.15f, 0f, "carpet_rug"),
            CushionRed = Mats.Lit(new Color(0.8f, 0.2f, 0.2f), 0.2f, 0f, "fabric_linen"),
            CushionOchre = Mats.Lit(new Color(1f, 0.95f, 0.7f), 0.2f, 0f, "fabric_linen"),
            Brass = Mats.Lit(new Color(0.78f, 0.6f, 0.3f), 0.7f, 0.9f),
            Iron = Mats.Lit(new Color(0.16f, 0.15f, 0.14f), 0.45f, 0.7f),
            LampGlow = Mats.Emissive(new Color(1f, 0.85f, 0.6f), new Color(1.25f, 0.95f, 0.6f)),
            Leaf = Mats.Lit(new Color(0.9f, 0.95f, 0.85f), 0.2f, 0f, "grass_short"),
            Water = Mats.Lit(new Color(0.03f, 0.04f, 0.05f), 0.95f),
            Sail = Mats.Lit(new Color(1f, 1f, 0.97f), 0.25f, 0f, "fabric_linen"),
            Paint = new[] { Mats.Lit(new Color(0.62f, 0.14f, 0.1f), 0.3f), Mats.Lit(new Color(0.14f, 0.42f, 0.3f), 0.3f), Mats.Lit(new Color(0.88f, 0.66f, 0.22f), 0.3f), Mats.Lit(new Color(0.16f, 0.28f, 0.55f), 0.3f) },
            PaintWhite = Mats.Lit(new Color(0.92f, 0.9f, 0.84f), 0.55f),
        };

        /// <summary>The plaza's architecture and set dressing around the puzzle (see the file header). One batch: the cell
        /// keeps to seven point lights, so a plaza-wide mesh never meets the forward renderer's eight-light limit.</summary>
        void BuildPlazaDetail(Transform g)
        {
            var m = PlazaMats();
            var k = new KitBatch();
            ArchDressing(k, m);
            FloorBands(k, m);
            // A paved apron one step down round the plaza, meeting the city's ground (the vista's) at -0.35.
            k.Box(m.Paving, new Vector3(0.2f, -0.26f, 3f), new Vector3(19.6f, 0.28f, 19.4f), 1.6f);
            WestHouses(g, k, m);
            Majlis(g, k, m);
            NorthWall(g, k, m);
            Tower(k, m, new Vector3(-10.6f, -0.35f, 13f), 9.4f);
            SouthGate(g, k, m);
            EastSide(g, k, m);
            k.Bake(g, "PlazaDetail");
            RouteColliders(g);
        }

        /// <summary>
        /// The three arches stay identical so they compare cleanly: stone plinths and gypsum imposts, plastered spandrels and
        /// a frieze with gypsum courses and a row of triangular niches front and back, stepped Najdi crenellations in the same
        /// plaster (never dark), and a pale threshold slab to walk over.
        /// </summary>
        static void ArchDressing(KitBatch k, NajdiMats m)
        {
            var right = new List<Vector2> { new Vector2(1.4f, 3f), new Vector2(1.4f, 4.38f), new Vector2(0.27f, 4.38f) };
            for (int s = 1; s <= 5; s++) { float t = (78f - 13f * s) * Mathf.Deg2Rad; right.Add(new Vector2(1.38f * Mathf.Cos(t), 3f + 1.38f * Mathf.Sin(t))); }
            var left = new List<Vector2>();
            for (int s = right.Count - 1; s >= 0; s--) left.Add(new Vector2(-right[s].x, right[s].y));
            for (int i = 0; i < 3; i++)
            {
                var a = new Vector3(-4f + i * 4f, 0f, 6f);
                foreach (float x in new[] { -1.15f, 1.15f })
                {
                    k.Box(m.Trim, a + new Vector3(x, 0.15f, 0f), new Vector3(0.72f, 0.3f, 1.0f), 3f);
                    k.Box(m.Gypsum, a + new Vector3(x, 3.0f, 0f), new Vector3(0.66f, 0.14f, 0.94f), 2f);
                }
                k.Of(m.Mud).AddExtrudedXY(right, 0.78f, a, Quaternion.identity, 2f);
                k.Of(m.Mud).AddExtrudedXY(left, 0.78f, a, Quaternion.identity, 2f);
                k.Box(m.Mud, a + new Vector3(0f, 4.62f, 0f), new Vector3(3.0f, 0.5f, 0.8f), 2f);
                k.Box(m.Gypsum, a + new Vector3(0f, 4.41f, 0f), new Vector3(3.06f, 0.08f, 0.86f), 2f);
                k.Box(m.Gypsum, a + new Vector3(0f, 4.9f, 0f), new Vector3(3.12f, 0.07f, 0.9f), 2f);
                for (int n = 0; n < 5; n++)
                    foreach (float sz in new[] { -1f, 1f })
                        TriFace(k.Of(m.Dark), a + new Vector3(-1.1f + n * 0.55f, 4.52f, sz * 0.405f), new Vector3(0f, 0f, sz), 0.22f, 0.24f);
                for (int c = 0; c < 6; c++) Merlon(k, m.Mud, a + new Vector3(-1.25f + c * 0.5f, 4.935f, 0f), Quaternion.identity, 0.36f, 0.62f, 0.15f);
                k.Box(m.InlayLight, a + new Vector3(0f, 0.0025f, 0f), new Vector3(1.8f, 0.005f, 0.8f), 1.5f);
            }
        }

        /// <summary>
        /// Geometric floor bands: a border of dark bands round a terracotta zigzag of pale triangles, a pale runner from the
        /// arrival toward the middle arch, a ringed medallion with a crown of triangles round the optical panel, and a zigzag
        /// band across the front of the arches. All flat (no colliders) and below the trace mark.
        /// </summary>
        static void FloorBands(KitBatch k, NajdiMats m)
        {
            const float x0 = -7.65f, x1 = 7.65f, z0 = -4.65f, z1 = 10.65f;
            Border(k, m, new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0), Vector3.forward, 0.001f);
            Border(k, m, new Vector3(x0, 0f, z1), new Vector3(x1, 0f, z1), Vector3.back, 0.001f);
            Border(k, m, new Vector3(x0, 0f, z0), new Vector3(x0, 0f, z1), Vector3.right, 0f);
            Border(k, m, new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1), Vector3.left, 0f);
            foreach (var (za, zb) in new[] { (-3.95f, -0.7f), (3.7f, 5.25f) })
            {
                float zm = (za + zb) * 0.5f, len = zb - za;
                k.Box(m.InlayLight, new Vector3(0f, 0.002f, zm), new Vector3(1.5f, 0.004f, len), 2f);
                foreach (float x in new[] { -0.79f, 0.79f }) k.Box(m.InlayDark, new Vector3(x, 0.002f, zm), new Vector3(0.08f, 0.004f, len), 1.5f);
            }
            for (float z = -3.3f; z < -0.9f; z += 1.2f) Diamond(k.Of(m.InlayDark), new Vector3(0f, 0.0045f, z), 0.2f);
            var c = new Vector3(0f, 0.002f, 1.5f);
            Annulus(k.Of(m.InlayLight), c, 0f, 1.62f, 48);
            Annulus(k.Of(m.InlayDark), c + Vector3.up * 0.001f, 1.06f, 1.14f, 48);
            Annulus(k.Of(m.InlayDark), c + Vector3.up * 0.001f, 1.62f, 1.74f, 48);
            Annulus(k.Of(m.InlayRed), c + Vector3.up * 0.001f, 1.74f, 2.08f, 48);
            Annulus(k.Of(m.InlayDark), c + Vector3.up * 0.001f, 2.08f, 2.16f, 48);
            var tb = k.Of(m.InlayLight);
            for (int i = 0; i < 20; i++)
            {
                float a0 = i * 18f * Mathf.Deg2Rad, a1 = (i + 1) * 18f * Mathf.Deg2Rad, am = (a0 + a1) * 0.5f;
                var y = Vector3.up * 0.0025f;
                FloorTri(tb, c + y + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 1.75f, c + y + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * 1.75f, c + y + new Vector3(Mathf.Cos(am), 0f, Mathf.Sin(am)) * 2.07f);
            }
            // The zigzag band across the front of the arches (a millimetre above the runner it crosses).
            k.Box(m.InlayRed, new Vector3(0f, 0.0027f, 4.55f), new Vector3(12.6f, 0.004f, 0.42f), 1.5f);
            foreach (float z in new[] { 4.3f, 4.8f }) k.Box(m.InlayDark, new Vector3(0f, 0.0027f, z), new Vector3(12.6f, 0.004f, 0.08f), 1.5f);
            for (float x = -6.3f; x < 6.25f; x += 0.42f)
                FloorTri(tb, new Vector3(x, 0.0052f, 4.34f), new Vector3(x + 0.42f, 0.0052f, 4.34f), new Vector3(x + 0.21f, 0.0052f, 4.76f));
        }

        /// <summary>One side of the floor border, from a to b, its bands laid toward `inward` (y = the bands' underside).</summary>
        static void Border(KitBatch k, NajdiMats m, Vector3 a, Vector3 b, Vector3 inward, float y)
        {
            var dir = b - a;
            float len = dir.magnitude;
            dir /= len;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            var mid = (a + b) * 0.5f + Vector3.up * (y + 0.002f);
            k.Box(m.InlayDark, mid + inward * 0.07f, new Vector3(0.14f, 0.004f, len), rot, 1.5f);
            k.Box(m.InlayRed, mid + inward * 0.35f, new Vector3(0.42f, 0.004f, len), rot, 1.5f);
            k.Box(m.InlayDark, mid + inward * 0.63f, new Vector3(0.14f, 0.004f, len), rot, 1.5f);
            int n = Mathf.FloorToInt(len / 0.42f);
            float start = (len - n * 0.42f) * 0.5f;
            var tb = k.Of(m.InlayLight);
            for (int i = 0; i < n; i++)
            {
                var p = a + dir * (start + i * 0.42f) + inward * 0.14f + Vector3.up * (y + 0.0045f);
                FloorTri(tb, p, p + dir * 0.42f, p + dir * 0.21f + inward * 0.42f);
            }
        }

        /// <summary>
        /// The east edge: a low crenellated parapet (the sun sets beyond it, so it stays low enough to leave the plaza in the
        /// light), a palm in a planter and one more in the corner, palms out on the sand whose long shadows reach across, a
        /// well with a palm-trunk frame in the north-east corner, benches with carpet runners, a shade sail over the south-west
        /// bench, the four lantern posts, and a post under the registration plaque.
        /// </summary>
        void EastSide(Transform g, KitBatch k, NajdiMats m)
        {
            k.Box(m.MudDark, new Vector3(8.8f, 0.215f, 3f), new Vector3(0.42f, 0.67f, 19.2f), 2f);
            k.Box(m.Gypsum, new Vector3(8.8f, 0.57f, 3f), new Vector3(0.46f, 0.04f, 19.24f), 2f);
            for (float z = -6.3f; z < 12.4f; z += 0.45f)
            {
                k.Box(m.Mud, new Vector3(8.8f, 0.64f, z), new Vector3(0.36f, 0.1f, 0.26f), 2f);
                k.Box(m.Mud, new Vector3(8.8f, 0.74f, z), new Vector3(0.3f, 0.1f, 0.13f), 2f);
            }
            // Palms: two in planters at the edges, four out in the city's dust beyond the walls (all clear of the view over the arches).
            var trunk = Mats.Lit(new Color(0.72f, 0.72f, 0.75f), 0.2f, 0f, "wood_oak_floor");
            var frond = Mats.Lit(new Color(0.62f, 0.72f, 0.38f), 0.25f, 0f, "fabric_linen");
            var dates = Mats.Lit(new Color(0.62f, 0.3f, 0.1f), 0.5f);
            int seed = 11;
            foreach (var (p, h, planter) in new[] { (new Vector3(6.95f, 0f, -3.5f), 6.4f, true), (new Vector3(7.95f, 0f, 4.6f), 7f, true),
                (new Vector3(12.8f, -0.35f, -1.2f), 8.5f, false), (new Vector3(13.6f, -0.35f, 7.4f), 9f, false), (new Vector3(-15f, -0.35f, 15f), 8f, false), (new Vector3(15.5f, -0.35f, 15.5f), 8.8f, false) })
            {
                if (planter)
                {
                    k.Box(m.Trim, p + Vector3.up * 0.28f, new Vector3(1.3f, 0.56f, 1.3f), 3f);
                    k.Box(m.Dark, p + Vector3.up * 0.565f, new Vector3(1.16f, 0.01f, 1.16f));
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 72f * Mathf.Deg2Rad;
                        Blob(k.Of(m.Leaf), p + new Vector3(Mathf.Cos(a) * 0.38f, 0.66f, Mathf.Sin(a) * 0.38f), 0.2f, 0.14f, 8);
                    }
                }
                MonumentKit.Palm(g, p + Vector3.up * (planter ? 0.56f : 0f), h, seed++, trunk, frond, null, planter ? dates : null);
            }
            Well(k, m, new Vector3(6.6f, 0f, 9.7f));
            foreach (float x in new[] { -5.8f, 5.8f })
            {
                k.Box(m.MudDark, new Vector3(x, 0.2f, -2.4f), new Vector3(0.55f, 0.4f, 2.1f), 2f);
                k.Box(m.Gypsum, new Vector3(x, 0.42f, -2.4f), new Vector3(0.6f, 0.04f, 2.15f), 2f);
                k.Box(m.Carpet, new Vector3(x, 0.446f, -2.4f), new Vector3(0.5f, 0.012f, 1.7f), 1.2f);
                k.Box(m.Carpet, new Vector3(x + (x < 0f ? 0.256f : -0.256f), 0.3f, -2.4f), new Vector3(0.012f, 0.3f, 1.7f), 1.2f);
            }
            // A shade sail over the south-west bench (its shadow falls away to the west, off the plaza).
            var posts = new[] { new Vector3(-7.45f, 0f, -3.95f), new Vector3(-4.75f, 0f, -0.95f), new Vector3(-7.65f, 0f, 0.35f) };
            float[] tops = { 3.3f, 2.7f, 3.5f };
            var centre = (posts[0] + posts[1] + posts[2]) / 3f;
            var corners = new Vector3[3];
            for (int i = 0; i < 3; i++)
            {
                Rod(k, m.Iron, posts[i], posts[i] + Vector3.up * (tops[i] + 0.15f), 0.07f);
                corners[i] = posts[i] + Vector3.up * tops[i] + (centre - posts[i]).normalized * 0.12f;
            }
            Tri2(k.Of(m.Sail), corners[0], corners[1], corners[2]);
            foreach (var p in new[] { new Vector3(-3.2f, 0f, -4.6f), new Vector3(3.2f, 0f, -4.6f), new Vector3(7.3f, 0f, 1.8f), new Vector3(-7.3f, 0f, -1.6f) })
            {
                k.Box(m.MudDark, p + Vector3.up * 0.1f, new Vector3(0.46f, 0.2f, 0.46f), 2f);
                k.Box(m.Mud, p + Vector3.up * 0.9f, new Vector3(0.3f, 1.4f, 0.3f), 2f);
                k.Box(m.Gypsum, p + Vector3.up * 1.63f, new Vector3(0.38f, 0.06f, 0.38f), 2f);
                Merlon(k, m.Mud, p + Vector3.up * 1.66f, Quaternion.identity, 0.32f, 0.32f, 0.07f);
                Lantern(k, m, p + Vector3.up * 2.08f, 1f, 0f);
                AddPointLight(g, p + Vector3.up * 2.08f, new Color(1f, 0.8f, 0.55f), 1.1f, 6f);
            }
            k.Box(m.Iron, new Vector3(0.85f, 0.46f, 1.545f), new Vector3(0.04f, 0.92f, 0.04f));
            k.Box(m.Iron, new Vector3(0.85f, 0.01f, 1.545f), new Vector3(0.2f, 0.02f, 0.2f));
        }

        /// <summary>A well: a stone curb round dark water under a palm-trunk frame with a pulley, rope and leather bucket.</summary>
        static void Well(KitBatch k, NajdiMats m, Vector3 at)
        {
            k.Of(m.Trim).AddLathe(at, new[] { new Vector2(0.56f, 0f), new Vector2(0.58f, 0.68f), new Vector2(0.54f, 0.76f), new Vector2(0.44f, 0.76f), new Vector2(0.41f, 0.68f), new Vector2(0.41f, 0.1f) }, 20, 1.5f);
            k.Of(m.Water).AddDisc(at + Vector3.up * 0.35f, Vector3.up, 0.42f, 20);
            foreach (float x in new[] { -0.75f, 0.75f }) Rod(k, m.PalmWood, at + new Vector3(x, 0f, 0f), at + new Vector3(x * 0.82f, 2.25f, 0f), 0.13f);
            Rod(k, m.PalmWood, at + new Vector3(-0.72f, 2.18f, 0f), at + new Vector3(0.72f, 2.18f, 0f), 0.11f);
            k.Of(m.PalmWood).AddTorus(at + new Vector3(0f, 1.98f, 0f), 0.16f, 0.035f, 14, 4, Quaternion.Euler(90f, 0f, 0f));
            Rod(k, m.Sail, at + new Vector3(0f, 2.18f, 0f), at + new Vector3(0f, 1.98f, 0f), 0.02f);
            Rod(k, m.Sail, at + new Vector3(0.16f, 1.98f, 0f), at + new Vector3(0.16f, 1.04f, 0f), 0.012f);
            k.Of(m.PalmWood).AddFrustum(at + new Vector3(0.16f, 0.82f, 0f), 0.09f, 0.12f, 0.22f, 10, true, false);
        }

        // ------------------------------------------------------------------ small geometry helpers
        /// <summary>A stepped Najdi merlon of three tiers standing on p, square to rot.</summary>
        static void Merlon(KitBatch k, Material m, Vector3 p, Quaternion rot, float width, float depth, float step)
        {
            for (int i = 0; i < 3; i++)
                k.Box(m, p + Vector3.up * (step * (i + 0.5f)), new Vector3(width * (1f - i / 3f), step, depth * (1f - i * 0.12f)), rot, 1.5f);
        }

        /// <summary>A flat triangle on a face whose outward normal is n (horizontal): base centred at c, apex up (or down).</summary>
        static void TriFace(MeshBuilder b, Vector3 c, Vector3 n, float width, float height, bool apexUp = true)
        {
            var right = Vector3.Cross(n, Vector3.up) * (width * 0.5f);
            var up = Vector3.up * height;
            if (apexUp) b.AddTriangle(c - right, c + up, c + right, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(1f, 0f));
            else b.AddTriangle(c - right + up, c + right + up, c, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0f));
        }

        /// <summary>A flat triangle on the floor, facing up whatever the order given (UVs in metres).</summary>
        static void FloorTri(MeshBuilder b, Vector3 p0, Vector3 p1, Vector3 p2)
        {
            if (Vector3.Cross(p1 - p0, p2 - p0).y < 0f) { var t = p1; p1 = p2; p2 = t; }
            b.AddTriangle(p0, p1, p2, new Vector2(p0.x, p0.z), new Vector2(p1.x, p1.z), new Vector2(p2.x, p2.z));
        }

        static void Diamond(MeshBuilder b, Vector3 c, float r)
        {
            FloorTri(b, c + new Vector3(-r, 0f, 0f), c + new Vector3(0f, 0f, r), c + new Vector3(r, 0f, 0f));
            FloorTri(b, c + new Vector3(-r, 0f, 0f), c + new Vector3(r, 0f, 0f), c + new Vector3(0f, 0f, -r));
        }

        /// <summary>A flat ring on the floor between radii r0 and r1 (r0 = 0 gives a disc).</summary>
        static void Annulus(MeshBuilder b, Vector3 c, float r0, float r1, int seg)
        {
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                FloorTri(b, c + d0 * r1, c + d1 * r1, c + d1 * r0);
                if (r0 > 0.0001f) FloorTri(b, c + d0 * r1, c + d1 * r0, c + d0 * r0);
            }
        }

        /// <summary>A square-section rod between two points.</summary>
        static void Rod(KitBatch k, Material m, Vector3 a, Vector3 b, float thick)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            d /= len;
            k.Box(m, (a + b) * 0.5f, new Vector3(thick, thick, len), Quaternion.LookRotation(d, Mathf.Abs(d.y) > 0.98f ? Vector3.forward : Vector3.up));
        }

        /// <summary>A flat triangle visible from both sides.</summary>
        static void Tri2(MeshBuilder b, Vector3 a, Vector3 b1, Vector3 c)
        {
            b.AddTriangle(a, b1, c, Vector2.zero, new Vector2(2f, 0f), new Vector2(1f, 2f));
            b.AddTriangle(a, c, b1, Vector2.zero, new Vector2(1f, 2f), new Vector2(2f, 0f));
        }

        /// <summary>A flattened ellipsoid (radius r, half-height h).</summary>
        static void Blob(MeshBuilder b, Vector3 c, float r, float h, int seg)
        {
            var prof = new List<Vector2>();
            for (int i = 0; i <= 6; i++) { float a = Mathf.Lerp(-90f, 90f, i / 6f) * Mathf.Deg2Rad; prof.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * h)); }
            b.AddLathe(c, prof, seg);
        }
    }
}

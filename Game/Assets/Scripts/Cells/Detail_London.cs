using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    // Place detail for London (playtest: "the places look cheap and bare"). The chime puzzle (checkpoint kiosk, clock, bell,
    // the seam on the embankment wall and the trace) stays in LondonCell.cs; these builders make the Thames-side walk
    // around it, batched per material and per stretch so lights stay local: a Victorian embankment wall with stone piers,
    // a gated arch and bracket lanterns; a Georgian terrace above it behind spear-top railings; the granite river parapet
    // with dolphin lamp standards and the necklace of lights between them; benches, a red telephone kiosk and pillar box
    // (plain designs, no crowns or ciphers); river stairs, a pontoon and a moored launch. The clock tower and the far
    // bank are the landmark vista's (x > 75, z > 62), so everything on the water stays low.
    public partial class LondonCell
    {
        /// <summary>Materials shared by the London detail builders.</summary>
        sealed class LonPalette
        {
            public Material Pavers, Setts, Kerb, Granite, Parapet, ParapetDark, Stone, Brick, BrickDark, Slate, Pot, Void;
            public Material Iron, LampIron, Gold, PaintWhite, Red, Cream, Orange, Leaves, Bark, Slat, NoticeGreen, Hull, Deck;
            public Material Glow, Bulb, PhoneGlass, SignWhite, WindowLit, WindowDark, Puddle, Streak;
            public Material[] Stucco, Doors, Curtains;
        }

        void BuildEmbankmentDetail(Transform g, Transform kiosk, Material granite, Material brick)
        {
            var p = new LonPalette
            {
                Pavers = granite,
                Setts = Mats.Lit(new Color(0.4f, 0.4f, 0.42f), 0.62f, 0f, "cobble_wet"),
                Kerb = Mats.Lit(new Color(0.52f, 0.52f, 0.53f), 0.45f, 0f, "granite_pavers"),
                Granite = Mats.Lit(new Color(0.4f, 0.4f, 0.42f), 0.5f, 0f, "granite_pavers"),
                Parapet = Mats.Lit(new Color(0.55f, 0.55f, 0.56f), 0.35f, 0f, "limestone_blocks"),
                ParapetDark = Mats.Lit(new Color(0.38f, 0.38f, 0.4f), 0.35f, 0f, "limestone_blocks"),
                Stone = Mats.Lit(new Color(0.78f, 0.75f, 0.68f), 0.3f, 0f, "limestone_blocks"),
                Brick = brick,
                BrickDark = Mats.Lit(new Color(0.46f, 0.22f, 0.16f), 0.3f, 0f, "brick_london"),
                Slate = Mats.Lit(new Color(0.2f, 0.21f, 0.23f), 0.45f, 0f, "stone_dark_slate"),
                Pot = Mats.Lit(new Color(0.6f, 0.32f, 0.2f), 0.3f, 0f, "plaster_terracotta"),
                Void = Mats.Lit(new Color(0.008f, 0.008f, 0.01f), 0.1f),
                Iron = Mats.Lit(new Color(0.04f, 0.045f, 0.045f), 0.45f, 0.6f),
                LampIron = Mats.Lit(new Color(0.05f, 0.09f, 0.075f), 0.5f, 0.7f),
                Gold = Mats.Lit(new Color(0.78f, 0.6f, 0.3f), 0.65f, 0.9f),
                PaintWhite = Mats.Lit(new Color(0.86f, 0.85f, 0.8f), 0.4f),
                Red = Mats.Lit(new Color(0.62f, 0.04f, 0.03f), 0.55f),
                Cream = Mats.Lit(new Color(0.85f, 0.8f, 0.68f), 0.3f),
                Orange = Mats.Lit(new Color(0.9f, 0.4f, 0.1f), 0.4f),
                Leaves = Mats.Lit(new Color(0.11f, 0.15f, 0.08f), 0.15f, 0f, "grass_short"),
                Bark = Mats.Lit(new Color(0.52f, 0.49f, 0.4f), 0.25f, 0f, "plaster_warm"),
                Slat = Mats.Lit(new Color(0.36f, 0.24f, 0.15f), 0.4f, 0f, "wood_dark"),
                NoticeGreen = Mats.Lit(new Color(0.06f, 0.17f, 0.12f), 0.5f),
                Hull = Mats.Lit(new Color(0.06f, 0.07f, 0.1f), 0.5f),
                Deck = Mats.Lit(new Color(0.42f, 0.3f, 0.2f), 0.35f, 0f, "wood_oak_floor"),
                Glow = Mats.Emissive(new Color(0.55f, 0.45f, 0.3f), new Color(1.3f, 0.92f, 0.5f)),
                Bulb = Mats.Emissive(new Color(1f, 0.9f, 0.7f), new Color(1.5f, 1.2f, 0.75f)),
                PhoneGlass = Mats.Emissive(new Color(0.7f, 0.66f, 0.52f), new Color(0.72f, 0.64f, 0.44f)),
                SignWhite = Mats.Emissive(new Color(0.9f, 0.9f, 0.86f), new Color(0.7f, 0.7f, 0.64f)),
                WindowLit = Mats.Emissive(new Color(0.45f, 0.32f, 0.18f), new Color(0.85f, 0.52f, 0.24f)),
                WindowDark = Mats.Lit(new Color(0.035f, 0.04f, 0.05f), 0.92f, 0.2f),
                Puddle = Mats.Lit(new Color(0.02f, 0.025f, 0.03f), 0.97f),
                Streak = Mats.Emissive(new Color(0.02f, 0.02f, 0.02f), new Color(0.55f, 0.38f, 0.18f)),
                // Few variants on purpose: each is another draw call for the whole terrace.
                Stucco = new[] { Mats.Lit(new Color(0.86f, 0.83f, 0.76f), 0.3f, 0f, "plaster_warm"), Mats.Lit(new Color(0.8f, 0.8f, 0.78f), 0.3f, 0f, "plaster_warm") },
                Doors = new[] { Mats.Lit(new Color(0.03f, 0.03f, 0.035f), 0.7f), Mats.Lit(new Color(0.05f, 0.16f, 0.1f), 0.6f), Mats.Lit(new Color(0.4f, 0.05f, 0.04f), 0.6f) },
                Curtains = new[] { Mats.Lit(new Color(0.35f, 0.07f, 0.06f), 0.2f, 0f, "fabric_linen") },
            };
            BuildPromenade(g, p);
            BuildLandWall(g, p);
            BuildTerrace(g, p);
            BuildRiverside(g, p);
            BuildRiverStairsAndLaunch(g, p);
            BuildStreetFurniture(g, p);
            DressCheckpoint(kiosk, p);
            BuildBridgeWalk(g, p); // Detail_LondonWalk.cs: the way to the far embankment
        }

        // ------------------------------------------------------------------ promenade surface
        /// <summary>
        /// The walk drawn in runs that carry on beyond the bounds (the slab keeps the collider), a strip of granite setts
        /// along the wall foot with its kerb and gratings, and a few dark puddles holding the lamps (none on the trace).
        /// </summary>
        void BuildPromenade(Transform g, LonPalette p)
        {
            // Runs on the pavers' 1.2 m repeat, aligned with the original slab, so the joins never show. The playable run
            // gathers exactly the eight lights around it (lanterns, lamps, kiosk, phone box); the others are extensions.
            foreach (var (z0, z1) in new[] { (-29.8f, -7f), (-7f, 11f), (11f, 103.4f) })
                MeshFactory.Box("PromenadeRun", g, new Vector3(0f, -0.15f, (z0 + z1) / 2f), new Vector3(9f, 0.3f, z1 - z0), p.Pavers, false, 1.2f);
            MeshFactory.Box("Setts", g, new Vector3(-3.8f, -0.008f, 36.8f), new Vector3(1.2f, 0.024f, 133.2f), p.Setts, false, 1.2f);
            var k = new KitBatch();
            k.Box(p.Kerb, new Vector3(-3.16f, 0f, 36.8f), new Vector3(0.16f, 0.024f, 133.2f));
            foreach (var z in new[] { -16f, -10f, -4.6f, 0.6f, 6.4f, 14f, 22f, 30f, 38f, 46f, 54f, 62f, 70f, 78f, 86f, 94f })
            {
                k.Box(p.Iron, new Vector3(-3.8f, 0.006f, z), new Vector3(0.45f, 0.008f, 0.3f));
                for (int i = 0; i < 5; i++) k.Box(p.Void, new Vector3(-3.8f - 0.16f + i * 0.08f, 0.0105f, z), new Vector3(0.025f, 0.002f, 0.24f));
            }
            k.Bake(g, "PromenadeDetail");
            var pb = new MeshBuilder();
            foreach (var (c, rx, rz, ang) in new[]
            {
                (new Vector2(1.7f, -3.6f), 0.95f, 0.5f, 15f), (new Vector2(-2.3f, -0.6f), 0.7f, 0.4f, 70f), (new Vector2(3.0f, 1.0f), 0.6f, 0.35f, 40f),
                (new Vector2(0.7f, 6.4f), 1.1f, 0.55f, 100f), (new Vector2(-0.6f, 9.6f), 0.8f, 0.45f, 10f), (new Vector2(3.3f, -6.2f), 0.7f, 0.4f, 60f),
                (new Vector2(-3.75f, 5.4f), 0.45f, 0.3f, 0f), (new Vector2(2.2f, 4.6f), 0.5f, 0.3f, 130f), (new Vector2(-1.6f, -5.8f), 0.55f, 0.35f, 30f),
                (new Vector2(1.2f, 14.5f), 1.0f, 0.5f, 80f), (new Vector2(-1.0f, -12f), 0.9f, 0.45f, 20f), (new Vector2(1.0f, 25f), 0.8f, 0.45f, 50f),
                (new Vector2(-2.0f, 33f), 0.6f, 0.35f, 10f), (new Vector2(2.5f, 48f), 1.0f, 0.5f, 120f), (new Vector2(-0.5f, 60f), 0.7f, 0.4f, 30f),
                (new Vector2(1.8f, 74f), 0.9f, 0.45f, 70f), (new Vector2(-1.2f, 87f), 0.8f, 0.4f, 150f), (new Vector2(2.2f, 96f), 0.6f, 0.35f, 20f),
            })
            {
                var poly = new List<Vector2>();
                for (int i = 0; i < 14; i++)
                {
                    float a = i / 14f * Mathf.PI * 2f, j = 0.78f + 0.22f * Mathf.Sin(a * 3f + c.x);
                    poly.Add(new Vector2(Mathf.Cos(a) * rx * j, Mathf.Sin(a) * rz * j));
                }
                pb.AddPrism(poly, 0.004f, 0.006f, 1f, true, new Vector3(c.x, 0f, c.y), Quaternion.Euler(0f, ang, 0f));
            }
            MeshFactory.FromBuilder("Puddles", pb, p.Puddle, g).GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ the embankment wall
        /// <summary>
        /// The land-side wall as a Victorian embankment wall: brick between Portland stone piers on a granite plinth, a string
        /// course and a stone coping with spear-top railings along it, a gated arch behind the checkpoint, iron bracket
        /// lanterns, a street-name plate, a riverside notice board and a festival poster. The stretch around the checkpoint
        /// seam (z 1.9..4.1) stays plain brick so the tear has a clean wall to open in.
        /// </summary>
        void BuildLandWall(Transform g, LonPalette p)
        {
            const float wf = -4.4f;
            // One long run (the 18 m slab keeps the collider); only the lanterns, kiosk, phone box and terrace lamps reach it.
            MeshFactory.Box("EmbankmentWall", g, new Vector3(-4.7f, 2f, 36.7f), new Vector3(0.6f, 4f, 134.6f), p.Brick, false, 1.4f);
            var k = new KitBatch();
            foreach (var (z0, z1) in new[] { (-30.6f, 8.5f), (10.2f, 104f) }) // the plinth stops at the arch
                k.Box(p.Granite, new Vector3(wf + 0.03f, 0.225f, (z0 + z1) / 2f), new Vector3(0.06f, 0.45f, z1 - z0), 1.2f);
            k.Box(p.Stone, new Vector3(wf + 0.025f, 3.02f, 36.7f), new Vector3(0.05f, 0.14f, 134.6f), 2f);
            k.Box(p.Stone, new Vector3(-4.65f, 4.05f, 36.7f), new Vector3(0.8f, 0.14f, 134.6f), 2f);
            foreach (var z in new[] { -30f, -25f, -20f, -15f, -10f, -5f, -0.4f, 5.6f, 10.6f, 15.6f, 20.6f, 25.6f, 30.6f, 35.6f, 40.6f, 45.6f, 50.6f, 55.6f, 60.6f, 65.6f, 70.6f, 75.6f, 80.6f, 85.6f })
            {
                k.Box(p.Stone, new Vector3(wf + 0.09f, 2.0f, z), new Vector3(0.18f, 3.9f, 0.62f), 2f);
                k.Box(p.Granite, new Vector3(wf + 0.11f, 0.3f, z), new Vector3(0.22f, 0.6f, 0.7f), 1.2f);
                k.Box(p.Stone, new Vector3(wf + 0.11f, 3.9f, z), new Vector3(0.24f, 0.14f, 0.72f), 2f);
            }
            // Spearheads over the play area; plain bars beyond it, where the railing is only ever seen from a distance.
            SpearRailing(k, p.Iron, new Vector3(-4.62f, 4.12f, -30.6f), new Vector3(-4.62f, 4.12f, -12f), 1.05f, false);
            SpearRailing(k, p.Iron, new Vector3(-4.62f, 4.12f, -12f), new Vector3(-4.62f, 4.12f, 22f), 1.05f);
            SpearRailing(k, p.Iron, new Vector3(-4.62f, 4.12f, 22f), new Vector3(-4.62f, 4.12f, 104f), 1.05f, false);

            // A barred arch behind the checkpoint, dark beyond.
            var ac = new Vector3(wf, 2.0f, 9.35f);
            var inWall = Quaternion.Euler(0f, 90f, 0f); // arch / polygon plane = the wall plane, depth along +X
            k.Of(p.Stone).AddArch(ac + new Vector3(0.05f, 0f, 0f), 0.8f, 0.94f, 0.1f, 12, inWall, 1f);
            foreach (var dz in new[] { -0.87f, 0.87f }) k.Box(p.Stone, ac + new Vector3(0.05f, -1.0f, dz), new Vector3(0.1f, 2.0f, 0.14f), 1f);
            k.Box(p.Stone, ac + new Vector3(0.06f, 0.92f, 0f), new Vector3(0.12f, 0.24f, 0.18f));
            var hole = new List<Vector2> { new Vector2(0.8f, -2.0f), new Vector2(-0.8f, -2.0f) };
            for (int i = 0; i <= 12; i++) { float a = Mathf.PI * i / 12f; hole.Add(new Vector2(-Mathf.Cos(a) * 0.8f, Mathf.Sin(a) * 0.8f)); }
            k.Of(p.Void).AddExtrudedXY(hole, 0.01f, ac + new Vector3(0.006f, 0f, 0f), inWall);
            for (int i = 0; i < 16; i++)
            {
                float dz = -0.75f + i * 0.1f, h = 2.0f + Mathf.Sqrt(Mathf.Max(0f, 0.8f * 0.8f - dz * dz)) - 0.05f;
                k.Box(p.Iron, new Vector3(wf + 0.04f, h / 2f, ac.z + dz), new Vector3(0.022f, h, 0.022f));
            }
            foreach (var y in new[] { 0.12f, 1.0f, 1.9f }) k.Box(p.Iron, new Vector3(wf + 0.045f, y, ac.z), new Vector3(0.03f, 0.03f, 1.6f));
            var archRail = new List<Vector3>();
            for (int i = 0; i <= 12; i++) { float a = Mathf.PI * i / 12f; archRail.Add(new Vector3(wf + 0.045f, 2.0f + Mathf.Sin(a) * 0.74f, ac.z + Mathf.Cos(a) * 0.74f)); }
            k.Of(p.Iron).AddTube(archRail, 0.015f, 6);
            k.Box(p.Iron, new Vector3(wf + 0.07f, 1.05f, ac.z + 0.06f), new Vector3(0.05f, 0.14f, 0.1f));

            // Bracket lanterns: on the piers either side of the walk's ends, and the one over the seam as before; unlit ones
            // (glow only) every 20 m on the way north to the bridge.
            foreach (var m in new[] { new Vector3(-4.22f, 2.55f, -5f), new Vector3(wf, 2.55f, 2.5f), new Vector3(-4.22f, 2.55f, 10.6f) })
            {
                WallLantern(k, p, m);
                AddPointLight(g, new Vector3(-3.9f, 2.65f, m.z), new Color(1f, 0.78f, 0.5f), 1.4f, 6f);
            }
            foreach (var z in new[] { 30.6f, 50.6f, 70.6f }) WallLantern(k, p, new Vector3(-4.22f, 2.55f, z));

            // A street-name plate and a riverside notice board south of the seam, a festival poster by the post box.
            var plate = new Vector3(wf + 0.015f, 2.45f, 0.55f);
            k.Box(p.PaintWhite, plate, new Vector3(0.03f, 0.26f, 1.0f));
            k.Box(p.Iron, plate + new Vector3(0.016f, 0.11f, 0f), new Vector3(0.004f, 0.012f, 0.94f));
            k.Box(p.Iron, plate + new Vector3(0.016f, -0.11f, 0f), new Vector3(0.004f, 0.012f, 0.94f));
            Sign(g, "THAMES WALK  <color=#B3261E>SW1</color>", 0.075f, new Color(0.08f, 0.08f, 0.08f), plate + new Vector3(0.02f, 0f, 0f), -90f, 0.95f);
            var board = new Vector3(wf + 0.03f, 1.5f, 0.55f);
            k.Box(p.NoticeGreen, board, new Vector3(0.06f, 0.85f, 0.9f));
            k.Box(p.Cream, board + new Vector3(0.032f, -0.07f, 0f), new Vector3(0.005f, 0.56f, 0.76f));
            for (int i = 0; i < 7; i++)
                k.Box(p.Iron, board + new Vector3(0.036f, 0.15f - i * 0.07f, -0.04f + (i % 3) * 0.02f), new Vector3(0.002f, 0.016f, 0.58f - (i % 3) * 0.1f));
            Sign(g, "RIVERSIDE WALK", 0.055f, new Color(0.88f, 0.74f, 0.45f), board + new Vector3(0.036f, 0.34f, 0f), -90f, 0.85f);
            k.Box(p.Iron, new Vector3(wf + 0.008f, 1.95f, -3.0f), new Vector3(0.016f, 0.66f, 1.02f));
            Poster(g, "postcard_4", new Vector3(wf + 0.018f, 1.95f, -3.0f), Quaternion.Euler(0f, -90f, 0f), 0.96f);
            k.Bake(g, "EmbankmentWallDetail");
        }

        // ------------------------------------------------------------------ the terrace
        /// <summary>
        /// A Georgian terrace on the raised walk above the wall: stucco ground floors with fanlit front doors on stone steps,
        /// brick upper floors with six-over-six sashes (some lit, curtains drawn back), cast-iron balconettes and cornice
        /// hoods on the first floor, a deep cornice and parapet, a slate mansard with dormers and chimney stacks crowded with
        /// pots against the sky. Two lamps on the walk light the fronts. Houses beyond the bounds are simpler.
        /// </summary>
        void BuildTerrace(Transform g, LonPalette p)
        {
            // One batch: only the two terrace lamps, the wall lanterns and the kiosk light reach the fronts.
            var k = new KitBatch();
            k.Box(p.Granite, new Vector3(-5.825f, 4.05f, 36.7f), new Vector3(1.55f, 0.14f, 134.6f), 1.2f);
            var rnd = new System.Random(1859);
            for (int i = -2; i <= 27; i++)
            {
                float zc = -20f + i * 4.5f;
                House(k, p, zc, i, zc > -9f && zc < 14f, rnd);
                if (i == 27) Chimney(k, p, zc + 2.25f, i);
            }
            foreach (var z in new[] { -2.0f, 6.8f })
            {
                TerraceLamp(k, p, new Vector3(-5.35f, 4.12f, z));
                AddPointLight(g, new Vector3(-5.35f, 7.45f, z), new Color(1f, 0.8f, 0.55f), 1.5f, 7.5f);
            }
            foreach (var z in new[] { -14f, -22.5f, 17.6f, 26f, 34.5f, 43f, 51.5f, 60f, 68.5f, 77f, 85.5f }) TerraceLamp(k, p, new Vector3(-5.35f, 4.12f, z)); // clear of the trees
            k.Bake(g, "Terrace");
        }

        static void House(KitBatch k, LonPalette p, float zc, int index, bool rich, System.Random rnd)
        {
            const float F = -6.6f; // the front plane (faces +X, toward the river)
            var stucco = p.Stucco[((index % p.Stucco.Length) + p.Stucco.Length) % p.Stucco.Length];
            k.Box(stucco, new Vector3(F - 0.3f, 5.785f, zc), new Vector3(0.6f, 3.33f, 4.5f), 2f);
            k.Box(p.Brick, new Vector3(F - 0.3f, 10.525f, zc), new Vector3(0.6f, 6.15f, 4.5f), 1.4f);
            k.Box(stucco, new Vector3(F + 0.06f, 7.535f, zc), new Vector3(0.12f, 0.17f, 4.52f), 2f);
            k.Box(stucco, new Vector3(F + 0.04f, 7.94f, zc), new Vector3(0.08f, 0.08f, 4.52f), 2f);
            k.Box(stucco, new Vector3(F + 0.075f, 13.375f, zc), new Vector3(0.15f, 0.15f, 4.52f), 2f);
            k.Box(stucco, new Vector3(F + 0.15f, 13.565f, zc), new Vector3(0.3f, 0.23f, 4.54f), 2f);
            k.Box(p.Brick, new Vector3(F - 0.17f, 14.04f, zc), new Vector3(0.36f, 0.72f, 4.5f), 1.4f);
            k.Box(stucco, new Vector3(F - 0.17f, 14.44f, zc), new Vector3(0.44f, 0.08f, 4.52f), 2f);
            if (rich) for (int r = 1; r <= 6; r++) k.Box(p.Slate, new Vector3(F + 0.004f, 4.12f + r * 0.48f, zc), new Vector3(0.008f, 0.022f, 4.5f));
            // Slate mansard with two dormers.
            k.Box(p.Slate, new Vector3(-7.125f, 15.5f, zc), new Vector3(0.1f, 2.32f, 4.5f), Quaternion.Euler(0f, 0f, 18.8f), 1.5f);
            k.Box(p.Slate, new Vector3(-8.5f, 16.62f, zc), new Vector3(2.1f, 0.06f, 4.5f), 1.5f);
            foreach (var dz in new[] { -1.1f, 1.1f })
            {
                var d = new Vector3(-7.2f, 15.35f, zc + dz);
                k.Box(p.Slate, d, new Vector3(0.5f, 0.95f, 0.95f));
                k.Box(stucco, d + new Vector3(0f, 0.5f, 0f), new Vector3(0.56f, 0.07f, 1.02f));
                k.Box(rnd.NextDouble() < 0.3 ? p.WindowLit : p.WindowDark, d + new Vector3(0.255f, -0.03f, 0f), new Vector3(0.01f, 0.62f, 0.62f));
                k.Box(p.PaintWhite, d + new Vector3(0.262f, -0.03f, 0f), new Vector3(0.01f, 0.025f, 0.62f));
            }
            Chimney(k, p, zc - 2.25f, index);
            // Three bays: the door (handed, so pairs of houses mirror) and sashes.
            int door = ((index % 2) + 2) % 2 == 0 ? 0 : 2;
            for (int b = 0; b < 3; b++)
            {
                float z = zc - 1.4f + b * 1.4f;
                if (b == door) Door(k, p, z, index, rich, stucco);
                else Sash(k, p, z, 4.95f, 1.0f, 2.0f, stucco, rich, rnd, true);
                Sash(k, p, z, 7.98f, 1.0f, 2.3f, stucco, rich, rnd, true, rich);
                Sash(k, p, z, 11.0f, 0.95f, 1.7f, null, rich, rnd);
            }
        }

        static void Chimney(KitBatch k, LonPalette p, float z, int index)
        {
            k.Box(p.Brick, new Vector3(-8.0f, 17.0f, z), new Vector3(1.0f, 1.8f, 0.9f), 1.4f);
            k.Box(p.Stone, new Vector3(-8.0f, 17.75f, z), new Vector3(1.1f, 0.12f, 1.0f));
            int pots = 3 + ((index % 2) + 2) % 2;
            for (int c = 0; c < pots; c++) k.Of(p.Pot).AddCylinder(new Vector3(-8.35f + (c % 2) * 0.6f, 17.81f, z + (c < 2 ? -0.22f : 0.22f)), 0.1f, 0.38f + 0.06f * (c % 3), 8);
        }

        static void Sash(KitBatch k, LonPalette p, float zc, float y0, float w, float h, Material surround, bool rich, System.Random rnd, bool hood = false, bool balcony = false)
        {
            const float F = -6.6f;
            double roll = rnd.NextDouble();
            bool lit = roll < 0.36;
            float yc = y0 + h / 2f;
            k.Box(lit ? p.WindowLit : p.WindowDark, new Vector3(F + 0.005f, yc, zc), new Vector3(0.01f, h, w));
            if (lit && rich)
            {
                var cur = p.Curtains[rnd.Next(p.Curtains.Length)];
                k.Box(cur, new Vector3(F + 0.012f, yc, zc - w / 2f + 0.12f), new Vector3(0.006f, h, 0.22f));
                k.Box(cur, new Vector3(F + 0.012f, yc, zc + w / 2f - 0.12f), new Vector3(0.006f, h, 0.22f));
            }
            // Sash frames and glazing bars (six over six where it can be seen).
            var wb = p.PaintWhite;
            const float fx = F + 0.02f;
            k.Box(wb, new Vector3(fx, y0 + 0.025f, zc), new Vector3(0.02f, 0.05f, w));
            k.Box(wb, new Vector3(fx, y0 + h - 0.025f, zc), new Vector3(0.02f, 0.05f, w));
            k.Box(wb, new Vector3(fx, yc, zc - w / 2f + 0.025f), new Vector3(0.02f, h, 0.05f));
            k.Box(wb, new Vector3(fx, yc, zc + w / 2f - 0.025f), new Vector3(0.02f, h, 0.05f));
            k.Box(wb, new Vector3(fx + 0.005f, yc, zc), new Vector3(0.02f, 0.045f, w));
            if (rich)
            {
                foreach (var t in new[] { -1f / 6f, 1f / 6f }) k.Box(wb, new Vector3(fx, yc, zc + t * w), new Vector3(0.015f, h, 0.018f));
                foreach (var t in new[] { 0.25f, 0.75f }) k.Box(wb, new Vector3(fx, y0 + t * h, zc), new Vector3(0.015f, 0.018f, w));
            }
            if (surround != null)
            {
                // A stucco architrave standing proud of the wall reads as the opening's depth.
                k.Box(surround, new Vector3(F + 0.06f, yc, zc - w / 2f - 0.06f), new Vector3(0.12f, h + 0.12f, 0.12f));
                k.Box(surround, new Vector3(F + 0.06f, yc, zc + w / 2f + 0.06f), new Vector3(0.12f, h + 0.12f, 0.12f));
                k.Box(surround, new Vector3(F + 0.06f, y0 + h + 0.06f, zc), new Vector3(0.12f, 0.12f, w + 0.24f));
                k.Box(surround, new Vector3(F + 0.07f, y0 - 0.035f, zc), new Vector3(0.14f, 0.07f, w + 0.3f));
            }
            else
            {
                k.Box(p.Stone, new Vector3(F + 0.07f, y0 - 0.035f, zc), new Vector3(0.14f, 0.07f, w + 0.3f));
                k.Box(p.BrickDark, new Vector3(F + 0.03f, y0 + h + 0.13f, zc), new Vector3(0.06f, 0.26f, w + 0.3f));
            }
            if (hood)
            {
                var hm = surround ?? p.Stone;
                k.Box(hm, new Vector3(F + 0.1f, y0 + h + 0.2f, zc), new Vector3(0.2f, 0.08f, w + 0.42f));
                foreach (var s in new[] { -1f, 1f }) k.Box(hm, new Vector3(F + 0.07f, y0 + h + 0.1f, zc + s * (w / 2f + 0.15f)), new Vector3(0.14f, 0.14f, 0.08f));
            }
            if (balcony)
            {
                // A cast-iron balconette at the sill.
                float bw = w + 0.3f, x = F + 0.26f;
                k.Box(p.Iron, new Vector3(F + 0.13f, y0 + 0.02f, zc), new Vector3(0.28f, 0.04f, bw));
                k.Box(p.Iron, new Vector3(x, y0 + 0.75f, zc), new Vector3(0.03f, 0.03f, bw));
                k.Box(p.Iron, new Vector3(x, y0 + 0.12f, zc), new Vector3(0.02f, 0.02f, bw));
                int n = Mathf.RoundToInt(bw / 0.11f);
                for (int i = 0; i <= n; i++) k.Box(p.Iron, new Vector3(x, y0 + 0.43f, zc - bw / 2f + i * bw / n), new Vector3(0.014f, 0.62f, 0.014f));
                foreach (var s in new[] { -1f, 1f })
                {
                    k.Box(p.Iron, new Vector3(F + 0.13f, y0 + 0.75f, zc + s * bw / 2f), new Vector3(0.26f, 0.03f, 0.014f));
                    k.Box(p.Iron, new Vector3(F + 0.13f, y0 + 0.43f, zc + s * bw / 2f), new Vector3(0.014f, 0.62f, 0.014f));
                }
            }
        }

        static void Door(KitBatch k, LonPalette p, float zd, int index, bool rich, Material stucco)
        {
            const float F = -6.6f;
            var leaf = p.Doors[((index % p.Doors.Length) + p.Doors.Length) % p.Doors.Length];
            var inWall = Quaternion.Euler(0f, 90f, 0f);
            k.Box(p.Stone, new Vector3(F + 0.27f, 4.195f, zd), new Vector3(0.55f, 0.15f, 1.5f));
            k.Box(p.Stone, new Vector3(F + 0.13f, 4.345f, zd), new Vector3(0.27f, 0.15f, 1.3f));
            k.Box(leaf, new Vector3(F + 0.01f, 5.52f, zd), new Vector3(0.02f, 2.2f, 1.0f));
            k.Box(p.PaintWhite, new Vector3(F + 0.015f, 6.64f, zd), new Vector3(0.03f, 0.05f, 1.0f));
            foreach (var s in new[] { -1f, 1f }) k.Box(stucco, new Vector3(F + 0.04f, 5.53f, zd + s * 0.56f), new Vector3(0.08f, 2.22f, 0.12f));
            k.Of(stucco).AddArch(new Vector3(F + 0.04f, 6.64f, zd), 0.48f, 0.6f, 0.08f, 12, inWall, 1f);
            k.Box(stucco, new Vector3(F + 0.06f, 7.18f, zd), new Vector3(0.12f, 0.2f, 0.14f));
            var fan = new List<Vector2>();
            for (int i = 0; i <= 12; i++) { float a = Mathf.PI * i / 12f; fan.Add(new Vector2(Mathf.Cos(a) * 0.48f, Mathf.Sin(a) * 0.48f)); }
            k.Of(p.WindowLit).AddExtrudedXY(fan, 0.01f, new Vector3(F + 0.01f, 6.64f, zd), inWall);
            if (!rich) return;
            for (int i = 1; i < 6; i++)
            {
                float a = i * 30f;
                var dir = new Vector3(0f, Mathf.Sin(a * Mathf.Deg2Rad), -Mathf.Cos(a * Mathf.Deg2Rad));
                k.Box(p.PaintWhite, new Vector3(F + 0.02f, 6.64f, zd) + dir * 0.24f, new Vector3(0.012f, 0.48f, 0.015f), Quaternion.Euler(a - 90f, 0f, 0f));
            }
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 2; c++)
                    k.Box(leaf, new Vector3(F + 0.024f, 4.78f + r * 0.66f, zd - 0.22f + c * 0.44f), new Vector3(0.012f, 0.5f, 0.34f));
            k.Of(p.Iron).AddSphere(new Vector3(F + 0.06f, 5.55f, zd), 0.035f, 8, 6);
            k.Box(p.Iron, new Vector3(F + 0.032f, 5.15f, zd), new Vector3(0.012f, 0.05f, 0.26f));
        }

        static void TerraceLamp(KitBatch k, LonPalette p, Vector3 b)
        {
            k.Of(p.Iron).AddLathe(b, new[] { new Vector2(0.14f, 0f), new Vector2(0.14f, 0.1f), new Vector2(0.1f, 0.2f), new Vector2(0.08f, 0.5f), new Vector2(0.055f, 0.6f) }, 10);
            k.Of(p.Iron).AddCylinder(b + Vector3.up * 0.6f, 0.045f, 2.4f, 8);
            Lantern(k, p, b + Vector3.up * 3.3f, 1.3f);
        }

        /// <summary>A square iron lantern: glazed body, corner posts, pyramid cap and finial, a cone below.</summary>
        static void Lantern(KitBatch k, LonPalette p, Vector3 c, float s)
        {
            var iron = p.Iron;
            var r45 = Quaternion.Euler(0f, 45f, 0f);
            k.Box(iron, c + new Vector3(0f, -0.2f, 0f) * s, new Vector3(0.22f, 0.03f, 0.22f) * s);
            k.Of(iron).AddFrustum(c + new Vector3(0f, -0.3f, 0f) * s, 0.03f * s, 0.12f * s, 0.09f * s, 4, true, true, 1f, r45);
            k.Box(p.Glow, c, new Vector3(0.18f, 0.36f, 0.18f) * s);
            foreach (var cx in new[] { -0.1f, 0.1f })
                foreach (var cz in new[] { -0.1f, 0.1f })
                    k.Box(iron, c + new Vector3(cx, 0f, cz) * s, new Vector3(0.025f, 0.4f, 0.025f) * s);
            k.Box(iron, c + new Vector3(0f, 0.2f, 0f) * s, new Vector3(0.24f, 0.03f, 0.24f) * s);
            k.Of(iron).AddFrustum(c + new Vector3(0f, 0.215f, 0f) * s, 0.19f * s, 0.03f * s, 0.16f * s, 4, true, true, 1f, r45);
            k.Of(iron).AddSphere(c + new Vector3(0f, 0.41f, 0f) * s, 0.025f * s, 6, 4);
        }

        static void WallLantern(KitBatch k, LonPalette p, Vector3 m)
        {
            var iron = p.Iron;
            k.Box(iron, m + new Vector3(0.02f, 0f, 0f), new Vector3(0.04f, 0.3f, 0.14f));
            k.Of(iron).AddTube(new[] { m + new Vector3(0.03f, 0.05f, 0f), m + new Vector3(0.2f, 0.08f, 0f), m + new Vector3(0.34f, 0.15f, 0f), m + new Vector3(0.36f, 0.06f, 0f) }, 0.018f, 6);
            k.Of(iron).AddTube(new[] { m + new Vector3(0.03f, -0.12f, 0f), m + new Vector3(0.1f, -0.1f, 0f), m + new Vector3(0.2f, -0.02f, 0f), m + new Vector3(0.25f, 0.06f, 0f), m + new Vector3(0.2f, 0.1f, 0f), m + new Vector3(0.16f, 0.06f, 0f) }, 0.012f, 6);
            Lantern(k, p, m + new Vector3(0.36f, 0.36f, 0f), 1f);
        }

        /// <summary>
        /// Spear-top railings: bars every 12.5 cm with spearheads, two rails, standards with ball finials every 2.5 m. Far
        /// stretches pass <paramref name="spears"/> = false: plain bars every 16 cm (a third of the triangles).
        /// </summary>
        static void SpearRailing(KitBatch k, Material iron, Vector3 a, Vector3 b, float h, bool spears = true)
        {
            var dir = b - a;
            float len = dir.magnitude;
            var rot = Quaternion.LookRotation(dir / len, Vector3.up);
            int n = Mathf.Max(2, Mathf.RoundToInt(len / (spears ? 0.125f : 0.16f)));
            int every = spears ? 20 : 16;
            for (int i = 0; i <= n; i++)
            {
                var q = Vector3.Lerp(a, b, i / (float)n);
                if (i % every == 0)
                {
                    k.Box(iron, q + Vector3.up * (h / 2f + 0.03f), new Vector3(0.045f, h + 0.06f, 0.045f));
                    k.Of(iron).AddSphere(q + Vector3.up * (h + 0.1f), 0.042f, 6, 4);
                    continue;
                }
                k.Box(iron, q + Vector3.up * (h / 2f), new Vector3(0.018f, h, 0.018f));
                if (spears) k.Of(iron).AddFrustum(q + Vector3.up * h, 0.028f, 0f, 0.09f, 4, true, false, 1f, Quaternion.Euler(0f, 45f, 0f));
            }
            k.Box(iron, (a + b) / 2f + Vector3.up * (h - 0.09f), new Vector3(0.022f, 0.04f, len), rot);
            k.Box(iron, (a + b) / 2f + Vector3.up * 0.1f, new Vector3(0.022f, 0.035f, len), rot);
        }

        // ------------------------------------------------------------------ the river side
        /// <summary>
        /// The granite river parapet with sunk panels and a moulded coping; lamp standards on pedestals, iron with coiled
        /// fish twisting round their bases, and the necklace of bulbs strung between them all along the river; two benches
        /// with cast-iron ends, a litter bin and a lifebuoy cabinet.
        /// </summary>
        void BuildRiverside(Transform g, LonPalette p)
        {
            var k = new KitBatch();
            // The parapet (the 18 m slab keeps the collider), with a gap and a gate at the river stairs (z 10.9..12.1).
            foreach (var (z0, z1) in new[] { (-29.8f, 10.9f), (12.1f, 103.2f) })
            {
                float cz = (z0 + z1) / 2f, len = z1 - z0;
                k.Box(p.Parapet, new Vector3(4.7f, 0.09f, cz), new Vector3(0.6f, 0.18f, len), 2.5f);
                k.Box(p.Parapet, new Vector3(4.71f, 0.54f, cz), new Vector3(0.48f, 0.72f, len), 2.5f);
                k.Box(p.Parapet, new Vector3(4.71f, 0.95f, cz), new Vector3(0.66f, 0.1f, len), 2.5f);
                k.Box(p.Parapet, new Vector3(4.71f, 1.03f, cz), new Vector3(0.56f, 0.06f, len), 2.5f);
            }
            k.Box(p.Granite, new Vector3(5.05f, -0.8f, 36.7f), new Vector3(0.2f, 1.6f, 133f), 1.2f); // the river face below the walk
            float[] pedestals = { -28f, -22f, -16f, -10f, -4f, 2f, 8f, 14f, 20f, 26f, 32f, 38f, 44f, 50f, 56f, 62f, 68f, 74f, 80f, 86f, 92f, 98f };
            for (float z = -29.4f; z < 101.6f; z += 2.0f)
            {
                float mid = z + 1.0f;
                bool clear = mid > 10.2f && mid < 12.8f;
                foreach (var pz in pedestals) if (Mathf.Abs(mid - pz) < 1.3f) clear = true;
                if (!clear) k.Box(p.ParapetDark, new Vector3(4.466f, 0.55f, mid), new Vector3(0.008f, 0.44f, 1.6f));
            }
            foreach (var pz in pedestals)
            {
                k.Box(p.Parapet, new Vector3(4.7f, 0.65f, pz), new Vector3(0.74f, 1.3f, 0.74f), 2.5f);
                k.Box(p.Parapet, new Vector3(4.7f, 1.35f, pz), new Vector3(0.84f, 0.1f, 0.84f), 2.5f);
                k.Box(p.Parapet, new Vector3(4.7f, 0.12f, pz), new Vector3(0.8f, 0.24f, 0.8f), 2.5f);
                EmbankmentLamp(k, p, new Vector3(4.7f, 1.4f, pz), pz < -12f || pz > 20f);
            }
            foreach (var pz in new[] { -4f, 2f, 8f }) AddPointLight(g, new Vector3(4.7f, 4.4f, pz), new Color(0.95f, 0.82f, 0.62f), 2.0f, 8.5f);
            for (int i = 0; i + 1 < pedestals.Length; i++)
                Swag(k, p, new Vector3(4.7f, 4.08f, pedestals[i] + 0.25f), new Vector3(4.7f, 4.08f, pedestals[i + 1] - 0.25f), 0.65f);
            // The gate at the head of the river stairs, on a granite threshold.
            k.Box(p.Granite, new Vector3(4.8f, -0.08f, 11.5f), new Vector3(0.7f, 0.16f, 1.2f), 1.2f);
            foreach (var gz in new[] { 10.95f, 12.05f }) k.Box(p.Iron, new Vector3(4.7f, 0.65f, gz), new Vector3(0.1f, 1.3f, 0.1f));
            for (int i = 1; i < 9; i++) k.Box(p.Iron, new Vector3(4.7f, 0.55f, 10.95f + i * 0.122f), new Vector3(0.018f, 1.1f, 0.018f));
            foreach (var y in new[] { 0.12f, 1.08f }) k.Box(p.Iron, new Vector3(4.7f, y, 11.5f), new Vector3(0.03f, 0.03f, 1.1f));
            // Benches, a bin and a lifebuoy.
            foreach (var z in new[] { -1.0f, 5.0f })
            {
                Bench(k, p, new Vector3(3.85f, 0f, z));
                Solid(g, new Vector3(3.88f, 0.45f, z), new Vector3(0.62f, 0.9f, 1.9f));
            }
            var bin = new Vector3(3.95f, 0f, 0.55f);
            k.Of(p.Iron).AddCylinder(bin, 0.24f, 0.85f, 14);
            k.Of(p.Gold).AddCylinder(bin + Vector3.up * 0.62f, 0.245f, 0.05f, 14);
            k.Of(p.Iron).AddFrustum(bin + Vector3.up * 0.85f, 0.26f, 0.16f, 0.12f, 14);
            var lb = new Vector3(4.3f, 0f, 3.25f);
            k.Box(p.Iron, lb + new Vector3(0f, 0.95f, 0f), new Vector3(0.06f, 1.9f, 0.06f));
            k.Box(p.Red, lb + new Vector3(-0.06f, 1.42f, 0f), new Vector3(0.12f, 0.8f, 0.66f));
            k.Of(p.Orange).AddTorus(lb + new Vector3(-0.15f, 1.38f, 0f), 0.23f, 0.06f, 16, 6, Quaternion.Euler(0f, 0f, 90f));
            Sign(g, "LIFEBUOY", 0.05f, new Color(0.95f, 0.95f, 0.92f), lb + new Vector3(-0.125f, 1.74f, 0f), 90f, 0.6f);
            k.Bake(g, "RiverParapet");
        }

        /// <summary>A lamp standard on a parapet pedestal. <paramref name="simple"/> (the ones away from the play area, passed
        /// on the run) uses fewer segments: the same silhouette for about two thirds of the triangles.</summary>
        static void EmbankmentLamp(KitBatch k, LonPalette p, Vector3 b, bool simple = false)
        {
            var iron = p.LampIron;
            int seg = simple ? 8 : 12, coilSteps = simple ? 10 : 14;
            k.Of(iron).AddLathe(b, new[] { new Vector2(0.22f, 0f), new Vector2(0.22f, 0.1f), new Vector2(0.17f, 0.16f), new Vector2(0.16f, 0.42f), new Vector2(0.12f, 0.52f), new Vector2(0.085f, 0.62f) }, simple ? 10 : 14);
            for (int s = 0; s < 2; s++)
            {
                var coil = new List<Vector3>();
                for (int i = 0; i <= coilSteps; i++)
                {
                    float t = i / (float)coilSteps, a = (s * 180f + t * 400f) * Mathf.Deg2Rad, r = 0.21f - t * 0.08f;
                    coil.Add(b + new Vector3(Mathf.Cos(a) * r, 0.12f + t * 0.62f, Mathf.Sin(a) * r));
                }
                k.Of(iron).AddTube(coil, 0.034f, simple ? 5 : 6);
            }
            k.Of(iron).AddCylinder(b + Vector3.up * 0.62f, 0.07f, 2.0f, seg);
            foreach (var y in new[] { 0.62f, 1.6f, 2.57f }) k.Of(iron).AddCylinder(b + Vector3.up * y, 0.09f, 0.05f, seg);
            var c = b + Vector3.up * 2.62f;
            k.Of(iron).AddFrustum(c, 0.06f, 0.2f, 0.14f, 6);
            k.Of(p.Glow).AddFrustum(c + Vector3.up * 0.14f, 0.19f, 0.24f, 0.42f, 6);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                k.Box(iron, c + new Vector3(Mathf.Cos(a) * 0.215f, 0.35f, Mathf.Sin(a) * 0.215f), new Vector3(0.025f, 0.44f, 0.025f));
            }
            k.Of(iron).AddCylinder(c + Vector3.up * 0.56f, 0.26f, 0.04f, 6);
            k.Of(iron).AddFrustum(c + Vector3.up * 0.6f, 0.27f, 0.04f, 0.22f, 6);
            k.Of(iron).AddSphere(c + Vector3.up * 0.86f, 0.045f, 6, 4);
            k.Box(iron, c + Vector3.up * 0.97f, new Vector3(0.015f, 0.14f, 0.015f));
        }

        /// <summary>A sagging string of bulbs between two lamp standards.</summary>
        static void Swag(KitBatch k, LonPalette p, Vector3 a, Vector3 b, float sag)
        {
            Vector3 P(float t) => Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1f - t);
            for (int i = 0; i < 10; i++)
            {
                var p0 = P(i / 10f);
                var p1 = P((i + 1) / 10f);
                k.Box(p.Iron, (p0 + p1) / 2f, new Vector3(0.01f, 0.01f, Vector3.Distance(p0, p1)), Quaternion.LookRotation((p1 - p0).normalized, Vector3.up));
            }
            int n = Mathf.Max(2, Mathf.RoundToInt(Vector3.Distance(a, b) / 0.42f));
            for (int i = 1; i < n; i++) k.Of(p.Bulb).AddSphere(P(i / (float)n) + Vector3.down * 0.04f, 0.032f, 4, 3); // a glowing dot: shape doesn't read
        }

        static void Bench(KitBatch k, LonPalette p, Vector3 c)
        {
            // Faces -X, toward the walk; cast-iron ends with an arm scroll and a curved back support, timber slats.
            var iron = p.Iron;
            foreach (var s in new[] { -0.82f, 0.82f })
            {
                var e = c + new Vector3(0f, 0f, s);
                k.Of(iron).AddTube(new[] { e + new Vector3(-0.28f, 0f, 0f), e + new Vector3(-0.24f, 0.25f, 0f), e + new Vector3(-0.26f, 0.45f, 0f), e + new Vector3(-0.31f, 0.6f, 0f), e + new Vector3(-0.23f, 0.68f, 0f), e + new Vector3(0f, 0.66f, 0f), e + new Vector3(0.12f, 0.62f, 0f) }, 0.026f, 6);
                k.Of(iron).AddTube(new[] { e + new Vector3(0.28f, 0f, 0f), e + new Vector3(0.22f, 0.3f, 0f), e + new Vector3(0.2f, 0.45f, 0f), e + new Vector3(0.24f, 0.7f, 0f), e + new Vector3(0.3f, 0.9f, 0f), e + new Vector3(0.26f, 0.98f, 0f), e + new Vector3(0.2f, 0.95f, 0f) }, 0.026f, 6);
                k.Of(iron).AddTube(new[] { e + new Vector3(-0.24f, 0.25f, 0f), e + new Vector3(0f, 0.2f, 0f), e + new Vector3(0.22f, 0.29f, 0f) }, 0.018f, 6);
                k.Box(iron, e + new Vector3(0f, 0.42f, 0f), new Vector3(0.5f, 0.05f, 0.04f));
            }
            for (int i = 0; i < 5; i++) k.Box(p.Slat, c + new Vector3(-0.22f + i * 0.1f, 0.46f, 0f), new Vector3(0.075f, 0.035f, 1.82f));
            for (int i = 0; i < 3; i++) k.Box(p.Slat, c + new Vector3(0.24f + i * 0.022f, 0.6f + i * 0.13f, 0f), new Vector3(0.03f, 0.09f, 1.82f), Quaternion.Euler(0f, 0f, -10f));
        }

        // ------------------------------------------------------------------ river stairs, pontoon and launch
        /// <summary>
        /// Beyond the walk's north end: granite water stairs down the outside of the wall to a floating pontoon with a lamp
        /// and a small lit shelter, and a river launch moored alongside, its cabin windows lit and its running lights on.
        /// Low on the water, so the clock tower stays clear above it.
        /// </summary>
        void BuildRiverStairsAndLaunch(Transform g, LonPalette p)
        {
            var k = new KitBatch();
            k.Box(p.Granite, new Vector3(5.7f, -0.08f, 11.55f), new Vector3(1.1f, 0.16f, 1.3f), 1.2f);
            for (int i = 0; i < 9; i++) k.Box(p.Granite, new Vector3(5.7f, -0.235f - i * 0.15f, 12.41f + i * 0.42f), new Vector3(1.1f, 0.15f, 0.44f), 1.2f);
            k.Box(p.Granite, new Vector3(5.7f, -1.32f, 16.6f), new Vector3(1.1f, 0.1f, 1.9f), 1.2f);
            // The stairs' outer wall follows them down; the landing at the foot is open to the pontoon.
            k.Box(p.Granite, new Vector3(6.35f, -0.6f, 13.3f), new Vector3(0.2f, 1.6f, 4.9f), Quaternion.Euler(16.4f, 0f, 0f), 1.2f);
            var rail = new List<Vector3> { new Vector3(6.35f, 1.15f, 10.9f), new Vector3(6.35f, -0.2f, 15.55f) };
            k.Of(p.Iron).AddTube(rail, 0.022f, 6);
            for (int i = 0; i <= 4; i++) k.Box(p.Iron, Vector3.Lerp(rail[0], rail[1], i / 4f) + Vector3.down * 0.25f, new Vector3(0.03f, 0.5f, 0.03f));
            // The pontoon: black floats, a timber deck, bollards, a lamp and a little lit shelter.
            k.Box(p.Hull, new Vector3(7.95f, -1.33f, 18.75f), new Vector3(2.8f, 0.3f, 8.5f));
            k.Box(p.Deck, new Vector3(7.95f, -1.16f, 18.75f), new Vector3(2.8f, 0.05f, 8.5f), 1.5f);
            k.Box(p.Deck, new Vector3(6.4f, -1.2f, 17.0f), new Vector3(0.32f, 0.04f, 1.2f), 1.5f);
            foreach (var bz in new[] { 15.2f, 22.3f }) { k.Of(p.Iron).AddCylinder(new Vector3(9.1f, -1.14f, bz), 0.1f, 0.35f, 10); k.Of(p.Iron).AddCylinder(new Vector3(9.1f, -0.82f, bz), 0.13f, 0.05f, 10); }
            k.Of(p.Iron).AddCylinder(new Vector3(6.8f, -1.14f, 20.5f), 0.05f, 2.4f, 8);
            Lantern(k, p, new Vector3(6.8f, 1.5f, 20.5f), 1.1f);
            var hut = new Vector3(7.65f, -1.14f, 22.2f);
            k.Box(p.Cream, hut + new Vector3(0f, 1.1f, 0f), new Vector3(1.6f, 2.2f, 1.4f));
            k.Box(p.Slate, hut + new Vector3(0f, 2.28f, 0f), new Vector3(1.8f, 0.12f, 1.6f));
            k.Box(p.WindowLit, hut + new Vector3(-0.81f, 1.35f, 0f), new Vector3(0.02f, 0.7f, 0.9f));
            k.Box(p.PaintWhite, hut + new Vector3(-0.825f, 1.35f, 0f), new Vector3(0.02f, 0.04f, 0.9f));
            k.Box(p.NoticeGreen, hut + new Vector3(0f, 1.0f, -0.71f), new Vector3(0.7f, 1.9f, 0.02f));
            // Lamplight lying on the water in broken streaks.
            for (int i = 0; i < 5; i++)
                k.Box(p.Streak, new Vector3(6.35f - i * 0.17f, -1.375f, 20.2f - i * 0.38f + (i % 2) * 0.1f), new Vector3(0.12f, 0.004f, 0.32f - i * 0.03f), Quaternion.Euler(0f, 32f, 0f));

            // The launch, moored on the pontoon's river side (coordinates offset by ox).
            var l = k;
            const float ox = 0.3f;
            var plan = new List<Vector2>
            {
                new Vector2(9.25f + ox, 13.2f), new Vector2(9.8f + ox, 12.5f), new Vector2(11.9f + ox, 12.5f), new Vector2(12.45f + ox, 13.2f),
                new Vector2(12.45f + ox, 24.0f), new Vector2(11.6f + ox, 25.8f), new Vector2(10.85f + ox, 26.5f), new Vector2(10.1f + ox, 25.8f), new Vector2(9.25f + ox, 24.0f),
            };
            l.Of(p.Hull).AddPrism(plan, -1.9f, -0.45f, 2f);
            l.Of(p.Deck).AddPrism(plan, -0.45f, -0.4f, 1.5f);
            foreach (var x in new[] { 9.23f, 12.47f }) l.Box(p.PaintWhite, new Vector3(x + ox, -0.6f, 18.6f), new Vector3(0.03f, 0.1f, 10.8f));
            l.Box(p.Cream, new Vector3(10.85f + ox, 0.3f, 18.5f), new Vector3(2.2f, 1.4f, 9f));
            l.Box(p.PaintWhite, new Vector3(10.85f + ox, 1.04f, 18.5f), new Vector3(2.4f, 0.08f, 9.2f));
            var lr = new System.Random(59);
            for (int i = 0; i < 8; i++)
            {
                var pane = lr.NextDouble() < 0.75 ? p.WindowLit : p.WindowDark;
                l.Box(pane, new Vector3(9.745f + ox, 0.55f, 14.6f + i * 1.1f), new Vector3(0.01f, 0.5f, 0.8f));
                l.Box(pane, new Vector3(11.955f + ox, 0.55f, 14.6f + i * 1.1f), new Vector3(0.01f, 0.5f, 0.8f));
            }
            l.Box(p.Cream, new Vector3(10.85f + ox, 1.45f, 22.35f), new Vector3(1.8f, 0.75f, 1.4f));
            l.Box(p.PaintWhite, new Vector3(10.85f + ox, 1.86f, 22.35f), new Vector3(2.0f, 0.07f, 1.6f));
            l.Box(p.WindowDark, new Vector3(10.85f + ox, 1.5f, 23.06f), new Vector3(1.6f, 0.4f, 0.01f));
            l.Box(p.WindowDark, new Vector3(9.945f + ox, 1.5f, 22.35f), new Vector3(0.01f, 0.4f, 1.2f));
            l.Of(p.Iron).AddCylinder(new Vector3(10.85f + ox, 1.9f, 22.0f), 0.04f, 1.6f, 8);
            l.Of(p.Bulb).AddSphere(new Vector3(10.85f + ox, 3.55f, 22.0f), 0.06f, 8, 6);
            l.Of(Mats.Emissive(new Color(0.5f, 0.08f, 0.06f), new Color(1.4f, 0.14f, 0.09f))).AddSphere(new Vector3(9.9f + ox, 1.25f, 22.9f), 0.05f, 8, 6);
            l.Of(Mats.Emissive(new Color(0.08f, 0.4f, 0.15f), new Color(0.15f, 1.4f, 0.45f))).AddSphere(new Vector3(11.8f + ox, 1.25f, 22.9f), 0.05f, 8, 6);
            for (int i = 0; i < 4; i++) l.Of(p.PaintWhite).AddCylinder(new Vector3(9.18f + ox, -1.15f, 14.5f + i * 2.6f), 0.09f, 0.5f, 10);
            var stern = new[] { new Vector3(9.4f + ox, -0.4f, 13.3f), new Vector3(9.4f + ox, 0.25f, 13.3f), new Vector3(9.9f + ox, 0.25f, 12.75f), new Vector3(11.8f + ox, 0.25f, 12.75f), new Vector3(12.3f + ox, 0.25f, 13.3f), new Vector3(12.3f + ox, -0.4f, 13.3f) };
            l.Of(p.PaintWhite).AddTube(stern, 0.02f, 6);
            l.Of(p.Iron).AddTube(Sag(new Vector3(9.1f, -0.8f, 15.2f), new Vector3(9.6f + ox, -0.4f, 14.0f), 0.12f, 6), 0.02f, 5);
            l.Of(p.Iron).AddTube(Sag(new Vector3(9.1f, -0.8f, 22.3f), new Vector3(9.6f + ox, -0.4f, 23.4f), 0.12f, 6), 0.02f, 5);
            l.Bake(g, "RiverStairs"); // the stairs, the pontoon and the launch share one batch
            Sign(g, "ELEVENTH HOUR", 0.12f, new Color(0.93f, 0.92f, 0.86f), new Vector3(9.215f + ox, -0.95f, 22.9f), 90f, 2f).font = TextFactory.Serif;
            AddPointLight(g, new Vector3(9.4f + ox, 0.9f, 18.2f), new Color(1f, 0.76f, 0.46f), 1.2f, 6f);
        }

        // ------------------------------------------------------------------ street furniture
        /// <summary>
        /// A red telephone kiosk (domed roof, glazing bars, lit from inside, TELEPHONE on its header; no crown) and a red
        /// pillar box against the wall south of the seam; cannon bollards across the walk's south end; two plane trees, one
        /// beyond each end of the walk.
        /// </summary>
        void BuildStreetFurniture(Transform g, LonPalette p)
        {
            var k = new KitBatch();
            PhoneBox(k, g, p, new Vector3(-3.88f, 0f, -1.9f));
            AddPointLight(g, new Vector3(-3.88f, 2.0f, -1.9f), new Color(1f, 0.93f, 0.78f), 0.9f, 2.8f);
            Solid(g, new Vector3(-3.88f, 1.25f, -1.9f), new Vector3(0.95f, 2.5f, 0.95f));
            PillarBox(k, p, new Vector3(-3.85f, 0f, -3.75f));
            Solid(g, new Vector3(-3.85f, 0.75f, -3.75f), new Vector3(0.62f, 1.5f, 0.62f));
            foreach (var x in new[] { -2.4f, 0f, 2.4f }) Bollard(k, p, new Vector3(x, 0f, -8.6f));
            PlaneTree(k, p, new Vector3(-2.4f, 0f, 13.6f), 11);
            Solid(g, new Vector3(-2.4f, 1.6f, 13.6f), new Vector3(0.6f, 3.2f, 0.6f)); // the walk now runs on past it
            PlaneTree(k, p, new Vector3(-2.4f, 0f, -10.4f), 23);
            k.Bake(g, "StreetFurniture");
        }

        void PhoneBox(KitBatch k, Transform g, LonPalette p, Vector3 b)
        {
            var red = p.Red;
            k.Box(red, b + new Vector3(0f, 0.06f, 0f), new Vector3(0.95f, 0.12f, 0.95f));
            foreach (var cx in new[] { -0.42f, 0.42f })
                foreach (var cz in new[] { -0.42f, 0.42f })
                    k.Box(red, b + new Vector3(cx, 1.16f, cz), new Vector3(0.11f, 2.08f, 0.11f));
            k.Box(red, b + new Vector3(-0.43f, 1.16f, 0f), new Vector3(0.05f, 2.08f, 0.75f));
            k.Box(red, b + new Vector3(0f, 2.32f, 0f), new Vector3(0.95f, 0.24f, 0.95f));
            k.Box(red, b + new Vector3(0f, 2.47f, 0f), new Vector3(1.0f, 0.06f, 1.0f));
            k.Of(red).AddLathe(b + Vector3.up * 2.5f, new[] { new Vector2(0.47f, 0f), new Vector2(0.44f, 0.06f), new Vector2(0.36f, 0.12f), new Vector2(0.22f, 0.16f), new Vector2(0.01f, 0.175f) }, 16);
            Vector3 Sz(Vector3 n, Vector3 t, float thick, float h, float along) =>
                new Vector3(Mathf.Abs(n.x) * thick + Mathf.Abs(t.x) * along, h, Mathf.Abs(n.z) * thick + Mathf.Abs(t.z) * along);
            // Three glazed sides (the door faces the walk): lit panes behind a red grid, three panes across and eight high.
            foreach (var (n, t) in new[] { (Vector3.right, Vector3.forward), (Vector3.forward, Vector3.right), (Vector3.back, Vector3.right) })
            {
                k.Box(red, b + n * 0.43f + Vector3.up * 0.245f, Sz(n, t, 0.04f, 0.25f, 0.73f));
                k.Box(p.PhoneGlass, b + n * 0.425f + Vector3.up * 1.24f, Sz(n, t, 0.012f, 1.74f, 0.73f));
                foreach (var o in new[] { -0.122f, 0.122f }) k.Box(red, b + n * 0.44f + t * o + Vector3.up * 1.24f, Sz(n, t, 0.05f, 1.74f, 0.035f));
                for (int r = 1; r < 8; r++) k.Box(red, b + n * 0.44f + Vector3.up * (0.37f + r * 1.74f / 8f), Sz(n, t, 0.05f, 0.03f, 0.73f));
                k.Box(red, b + n * 0.44f + Vector3.up * 2.13f, Sz(n, t, 0.05f, 0.05f, 0.73f));
                k.Box(p.SignWhite, b + n * 0.478f + Vector3.up * 2.32f, Sz(n, t, 0.01f, 0.11f, 0.62f));
            }
            k.Box(red, b + new Vector3(0.47f, 1.2f, 0.3f), new Vector3(0.03f, 0.4f, 0.03f)); // door handle
            Sign(g, "TELEPHONE", 0.065f, new Color(0.05f, 0.05f, 0.05f), b + new Vector3(0.486f, 2.32f, 0f), -90f, 0.6f);
            Sign(g, "TELEPHONE", 0.065f, new Color(0.05f, 0.05f, 0.05f), b + new Vector3(0f, 2.32f, -0.486f), 0f, 0.6f);
        }

        static void PillarBox(KitBatch k, LonPalette p, Vector3 b)
        {
            k.Of(p.Iron).AddCylinder(b, 0.29f, 0.12f, 18);
            k.Of(p.Red).AddCylinder(b + Vector3.up * 0.12f, 0.27f, 1.18f, 18);
            k.Of(p.Red).AddCylinder(b + Vector3.up * 1.3f, 0.31f, 0.06f, 18);
            k.Of(p.Red).AddLathe(b + Vector3.up * 1.36f, new[] { new Vector2(0.3f, 0f), new Vector2(0.27f, 0.1f), new Vector2(0.18f, 0.18f), new Vector2(0.01f, 0.22f) }, 18);
            k.Box(p.Iron, b + new Vector3(0.262f, 1.08f, 0f), new Vector3(0.03f, 0.045f, 0.26f));
            k.Box(p.Red, b + new Vector3(0.275f, 1.13f, 0f), new Vector3(0.05f, 0.035f, 0.32f));
            k.Box(p.Cream, b + new Vector3(0.268f, 0.88f, 0f), new Vector3(0.01f, 0.1f, 0.14f));
        }

        static void Bollard(KitBatch k, LonPalette p, Vector3 b)
        {
            k.Of(p.Iron).AddLathe(b, new[]
            {
                new Vector2(0.15f, 0f), new Vector2(0.15f, 0.06f), new Vector2(0.12f, 0.1f), new Vector2(0.105f, 0.75f), new Vector2(0.125f, 0.8f),
                new Vector2(0.125f, 0.86f), new Vector2(0.09f, 0.9f), new Vector2(0.06f, 0.97f), new Vector2(0.01f, 0.99f),
            }, 12);
            k.Of(p.Gold).AddCylinder(b + Vector3.up * 0.6f, 0.11f, 0.04f, 12);
        }

        static void PlaneTree(KitBatch k, LonPalette p, Vector3 b, int seed)
        {
            var r = new System.Random(seed);
            k.Box(p.Iron, b + new Vector3(0f, 0.01f, 0f), new Vector3(1.2f, 0.02f, 1.2f));
            k.Of(p.Bark).AddFrustum(b, 0.26f, 0.18f, 3.4f, 10);
            // Three limbs leaning out over the walk (away from the wall and its railings), then overlapping leaf masses.
            var tops = new List<Vector3>();
            for (int i = 0; i < 3; i++)
            {
                float a = (-70f + i * 70f + (float)(r.NextDouble() - 0.5) * 20f) * Mathf.Deg2Rad;
                var end = b + new Vector3(Mathf.Cos(a) * 1.1f, 5.6f + (float)r.NextDouble() * 1.2f, Mathf.Sin(a) * 1.1f);
                k.Of(p.Bark).AddTube(new[] { b + Vector3.up * 3.3f, b + new Vector3(Mathf.Cos(a) * 0.45f, 4.4f, Mathf.Sin(a) * 0.45f), end }, 0.11f, 8);
                tops.Add(end);
            }
            for (int i = 0; i < 11; i++)
            {
                var c = tops[i % 3] + new Vector3((float)(r.NextDouble() - 0.3) * 1.8f, (float)(r.NextDouble() - 0.3) * 1.4f, (float)(r.NextDouble() - 0.5) * 1.8f);
                k.Of(p.Leaves).AddSphere(c, 0.85f + (float)r.NextDouble() * 0.45f, 10, 7);
            }
        }

        // ------------------------------------------------------------------ the checkpoint
        /// <summary>
        /// The checkpoint kiosk dressed as a green timber shelter: corner posts, a dark plinth, raised panels, a white-barred
        /// window, a fascia with its name in gold, a door on the far side and a little bell-cote carrying the bell's frame.
        /// Built in the kiosk's own space; the clock, minute hand, bell and chime glow are untouched.
        /// </summary>
        void DressCheckpoint(Transform kiosk, LonPalette p)
        {
            var k = new KitBatch();
            var green = Mats.Lit(new Color(0.07f, 0.19f, 0.13f), 0.55f);
            var frame = p.PaintWhite;
            foreach (var cx in new[] { -1.1f, 1.1f })
                foreach (var cz in new[] { -0.8f, 0.8f })
                    k.Box(green, new Vector3(cx, 1.3f, cz), new Vector3(0.12f, 2.6f, 0.12f));
            k.Box(p.Iron, new Vector3(0f, 0.09f, 0f), new Vector3(2.26f, 0.18f, 1.66f));
            k.Box(green, new Vector3(0f, 2.47f, 0f), new Vector3(2.26f, 0.22f, 1.66f));
            k.Box(green, new Vector3(0f, 2.83f, 0f), new Vector3(2.56f, 0.06f, 1.96f));
            foreach (var y in new[] { 0.92f, 1.98f }) k.Box(green, new Vector3(0f, y, 0f), new Vector3(2.24f, 0.06f, 1.64f));
            // Raised panels: under the window (south), under the clock (east), on the north and west faces.
            foreach (var px in new[] { -0.5f, 0.5f }) k.Box(green, new Vector3(px, 0.55f, -0.815f), new Vector3(0.75f, 0.55f, 0.03f));
            foreach (var pz in new[] { -0.38f, 0.38f })
            {
                k.Box(green, new Vector3(1.115f, 0.55f, pz), new Vector3(0.03f, 0.55f, 0.6f));
                k.Box(green, new Vector3(1.115f, 1.38f, pz), new Vector3(0.03f, 0.7f, 0.6f)); // stops under the clock
                k.Box(green, new Vector3(-1.115f, 0.55f, pz), new Vector3(0.03f, 0.55f, 0.6f));
                k.Box(green, new Vector3(-1.115f, 1.38f, pz), new Vector3(0.03f, 0.7f, 0.6f));
            }
            k.Box(green, new Vector3(-0.45f, 0.55f, 0.815f), new Vector3(0.75f, 0.55f, 0.03f));
            k.Box(green, new Vector3(-0.45f, 1.45f, 0.815f), new Vector3(0.75f, 0.75f, 0.03f));
            // The window's bars and sill, a door at the back with its knob.
            k.Box(frame, new Vector3(0f, 1.05f, -0.832f), new Vector3(1.46f, 0.05f, 0.02f));
            k.Box(frame, new Vector3(0f, 1.85f, -0.832f), new Vector3(1.46f, 0.05f, 0.02f));
            foreach (var bx in new[] { -0.705f, -0.35f, 0f, 0.35f, 0.705f }) k.Box(frame, new Vector3(bx, 1.45f, -0.832f), new Vector3(0.04f, 0.8f, 0.02f));
            k.Box(frame, new Vector3(0f, 1.45f, -0.834f), new Vector3(1.42f, 0.03f, 0.02f));
            k.Box(green, new Vector3(0f, 1.0f, -0.86f), new Vector3(1.6f, 0.06f, 0.1f));
            k.Box(green, new Vector3(0.55f, 1.0f, 0.82f), new Vector3(0.62f, 2.0f, 0.03f));
            k.Of(p.Gold).AddSphere(new Vector3(0.32f, 1.0f, 0.85f), 0.03f, 8, 6);
            // A bell-cote over the bell's frame; the bell swings along Z between its posts.
            foreach (var bx in new[] { -0.42f, 0.42f }) k.Box(green, new Vector3(bx, 3.03f, 0f), new Vector3(0.06f, 0.46f, 0.06f));
            k.Box(p.Slate, new Vector3(-0.24f, 3.42f, 0f), new Vector3(0.6f, 0.04f, 0.32f), Quaternion.Euler(0f, 0f, 25f));
            k.Box(p.Slate, new Vector3(0.24f, 3.42f, 0f), new Vector3(0.6f, 0.04f, 0.32f), Quaternion.Euler(0f, 0f, -25f));
            k.Of(p.Gold).AddTorus(new Vector3(1.245f, 2.2f, 0f), 0.44f, 0.025f, 28, 6, Quaternion.Euler(0f, 0f, 90f));
            k.Bake(kiosk, "CheckpointDressing");
            var name = Sign(kiosk, "CHECKPOINT", 0.11f, new Color(0.9f, 0.76f, 0.45f), new Vector3(0f, 2.47f, -0.84f), 0f, 2f);
            name.font = TextFactory.Serif;
            name.characterSpacing = 12f;
        }

        // ------------------------------------------------------------------ helpers
        static TMPro.TextMeshPro Sign(Transform g, string text, float size, Color color, Vector3 pos, float yaw, float width, TMPro.TextAlignmentOptions align = TMPro.TextAlignmentOptions.Center)
        {
            var t = TextFactory.World(g, text, size, color, align, false, width);
            t.transform.localPosition = pos;
            t.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        /// <summary>A festival postcard printed as a poster (front faces the quad's -Z, like Unity's quad).</summary>
        static void Poster(Transform g, string art, Vector3 pos, Quaternion rot, float width)
        {
            var tex = Tex.Art(art);
            if (tex == null) return;
            var q = MeshFactory.Quad("Poster", g, width, width / 1.6f, Mats.Printed(tex, new Color(0.82f, 0.8f, 0.74f), 0.2f), pos, rot);
            q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// An invisible collider so the player can't walk through a solid prop at the edge of the walk. It sits on the
        /// IgnoreRaycast layer, so the Passenger's floor probe, interaction rays and tear aiming all pass straight through.
        /// </summary>
        static void Solid(Transform g, Vector3 center, Vector3 size)
        {
            var go = new GameObject("Solid") { layer = Layers.IgnoreRaycast };
            go.transform.SetParent(g, false);
            go.transform.localPosition = center;
            go.AddComponent<BoxCollider>().size = size;
        }

        static List<Vector3> Sag(Vector3 a, Vector3 b, float sag, int n)
        {
            var pts = new List<Vector3>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                pts.Add(Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1f - t));
            }
            return pts;
        }
    }
}

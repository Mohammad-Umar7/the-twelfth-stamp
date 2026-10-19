using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Rio grows a hillside lane past the end of the parade (the expansion pass: "expand the country maps ... more
    /// interactive animative and cool"). Behind the silent bateria the avenue's north bound opens onto a lane of tiled
    /// steps between painted houses, with bunting and laundry moving in the wind, doves on the landing, frozen confetti in
    /// the air; at the top a little square: a corner bar with its radio and a fan turning, string lights, a frangipani,
    /// a carnival banner, a chained gate where the hill goes on, and a samba circle frozen mid-song round a drum you can
    /// hit (the set piece): each hit puffs confetti up into the air and the frozen figure nearest you twitches, its head
    /// turning a little further toward you every time. Everything stands north of z 14 and its lights are far enough
    /// north that the carnival's own meshes keep exactly their eight lights.
    /// </summary>
    public static class Expand_Brazil
    {
        const float LaneHW = 2.0f;                                     // the lane is x -2..2
        const float Lane0 = 15.0f, Stair0 = 15.6f, Tread = 0.7f, Rise = 0.2f;
        const float Land0 = 19.1f, Land1 = 20.3f, Sq0 = 23.8f, Sq1 = 34.6f, SqX = 6.5f, SqY = 2.4f;
        static readonly Color LampColor = new Color(1f, 0.74f, 0.48f);

        enum FigPose { Samba, Wave, Drum, Seated, Cheer }

        sealed class Pal
        {
            public Material Skin, Gold, Chrome, DarkMetal, DrumHead, Wood, SequinGold, SequinMagenta, SequinTurquoise, Straw;
            public Material TreadTile, Trim, Terracotta, Soil, Foliage, FoliageLit, Bloom, Flower, GlassDark, WhiteStone, BlackStone, Slate, Chalk;
            public Material[] Plume, Bulbs, Plaster, Tiles, GlassLit;
        }

        public static void Build(RioCell cell, Transform parent)
        {
            var root = new GameObject("Expand_Brazil").transform;
            root.SetParent(cell.transform, false);
            try
            {
                var m = Palette();
                var lane = new KitBatch();     // the stairs and the houses along them
                var square = new KitBatch();   // the square and its houses
                var props = new KitBatch();    // bar, drum circle, furniture, plants
                var glow = new KitBatch();     // bulbs, lit glass: no shadows
                Lane(root, lane, glow, m);
                LaneHouses(root, lane, glow, m);
                Square(root, square, glow, m);
                Bar(root, props, glow, m);
                var figures = DrumCircle(root, props, m);
                Furnish(root, props, m);
                Confetti(root, lane, square, m);
                lane.Bake(root, "Ladeira");
                square.Bake(root, "Praca");
                props.Bake(root, "PracaProps");
                glow.Bake(root, "LadeiraGlow", false);
                Moving(root, m);
                Life(root);
                Lamps(root);
                Postcards(root);
                ShotPoint(root, new Vector3(-1.0f, SqY, 25.0f), 15f);
                OpenBound(cell.transform); // last: if anything above failed, the avenue stays shut
                if (figures == null) Debug.LogWarning("[TS-Expand] Rio: the samba circle has no figures");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[TS-Expand] Rio expansion failed: " + e);
            }
        }

        static Pal Palette()
        {
            // The avenue's own carnival colours (same parameters as Detail_Rio, so the same cached materials).
            var plumeColours = new[]
            {
                new Color(0.95f, 0.12f, 0.6f), new Color(1f, 0.84f, 0.1f), new Color(0.05f, 0.82f, 0.82f), new Color(0.97f, 0.95f, 0.92f),
                new Color(1f, 0.42f, 0.05f), new Color(0.58f, 0.22f, 0.95f), new Color(0.2f, 0.82f, 0.3f),
            };
            var plume = new Material[plumeColours.Length];
            for (int i = 0; i < plume.Length; i++)
            {
                var c = plumeColours[i];
                plume[i] = Mats.Lit(c, 0.3f, 0f, null, false, new Color(c.r * 0.16f, c.g * 0.16f, c.b * 0.16f));
            }
            return new Pal
            {
                Skin = Mats.Lit(new Color(0.88f, 0.84f, 0.8f), 0.62f),
                Gold = Mats.Lit(new Color(0.95f, 0.72f, 0.3f), 0.7f, 0.7f, null, false, new Color(0.12f, 0.08f, 0.02f)),
                Chrome = Mats.Lit(new Color(0.86f, 0.87f, 0.9f), 0.85f, 0.85f),
                DarkMetal = Mats.Lit(new Color(0.08f, 0.08f, 0.09f), 0.5f, 0.6f),
                DrumHead = Mats.Lit(new Color(0.95f, 0.94f, 0.9f), 0.4f),
                Wood = Mats.Lit(new Color(0.45f, 0.3f, 0.18f), 0.4f, 0f, "wood_dark"),
                SequinGold = Mats.Lit(new Color(1f, 0.8f, 0.32f), 0.9f, 0.8f, null, false, new Color(0.08f, 0.06f, 0.02f)),
                SequinMagenta = Mats.Lit(new Color(0.92f, 0.1f, 0.56f), 0.88f, 0.5f, null, false, new Color(0.1f, 0.01f, 0.06f)),
                SequinTurquoise = Mats.Lit(new Color(0.05f, 0.78f, 0.78f), 0.88f, 0.5f, null, false, new Color(0.01f, 0.08f, 0.08f)),
                Straw = Mats.Lit(new Color(0.85f, 0.72f, 0.45f), 0.3f),
                Plume = plume,
                Bulbs = new[]
                {
                    Mats.Emissive(new Color(1f, 0.85f, 0.6f), new Color(1.5f, 1.1f, 0.6f)), Mats.Emissive(new Color(1f, 0.3f, 0.25f), new Color(1.5f, 0.22f, 0.16f)),
                    Mats.Emissive(new Color(0.4f, 1f, 0.45f), new Color(0.3f, 1.35f, 0.35f)), Mats.Emissive(new Color(0.3f, 0.85f, 1f), new Color(0.2f, 0.95f, 1.45f)),
                    Mats.Emissive(new Color(1f, 0.35f, 0.9f), new Color(1.45f, 0.3f, 1.2f)),
                },
                // Painted plaster (texture x pale tint), white trim, glossy tiles for the risers.
                Plaster = new[]
                {
                    Mats.Lit(new Color(0.97f, 0.66f, 0.66f), 0.25f, 0f, "plaster_warm"), Mats.Lit(new Color(0.98f, 0.86f, 0.5f), 0.25f, 0f, "plaster_warm"),
                    Mats.Lit(new Color(0.55f, 0.85f, 0.82f), 0.25f, 0f, "plaster_warm"), Mats.Lit(new Color(0.72f, 0.88f, 0.5f), 0.25f, 0f, "plaster_warm"),
                    Mats.Lit(new Color(0.78f, 0.68f, 0.92f), 0.25f, 0f, "plaster_warm"), Mats.Lit(new Color(0.98f, 0.72f, 0.5f), 0.25f, 0f, "plaster_warm"),
                },
                Tiles = new[]
                {
                    Mats.Lit(new Color(0.85f, 0.12f, 0.12f), 0.75f), Mats.Lit(new Color(0.98f, 0.8f, 0.1f), 0.75f), Mats.Lit(new Color(0.1f, 0.6f, 0.3f), 0.75f),
                    Mats.Lit(new Color(0.12f, 0.35f, 0.8f), 0.75f), Mats.Lit(new Color(0.95f, 0.95f, 0.92f), 0.75f), Mats.Lit(new Color(0.95f, 0.45f, 0.08f), 0.75f),
                },
                GlassLit = new[]
                {
                    Mats.Emissive(new Color(0.9f, 0.65f, 0.4f), new Color(1.0f, 0.6f, 0.26f)), Mats.Emissive(new Color(0.4f, 0.55f, 0.9f), new Color(0.26f, 0.44f, 1.0f)),
                },
                TreadTile = Mats.Lit(new Color(0.9f, 0.86f, 0.8f), 0.35f, 0f, "tile_terrazzo"),
                Trim = Mats.Lit(new Color(0.92f, 0.9f, 0.86f), 0.3f, 0f, "plaster_warm"),
                Terracotta = Mats.Lit(new Color(0.95f, 0.62f, 0.5f), 0.3f, 0f, "plaster_terracotta"),
                Soil = Mats.Lit(new Color(0.3f, 0.2f, 0.14f), 0.1f, 0f, "sand_ripples"),
                Foliage = Mats.Lit(new Color(0.16f, 0.4f, 0.18f), 0.3f, 0f, null, false, new Color(0.01f, 0.025f, 0.01f)),
                FoliageLit = Mats.Lit(new Color(0.3f, 0.55f, 0.25f), 0.3f),
                Bloom = Mats.Lit(new Color(0.85f, 0.12f, 0.55f), 0.3f, 0f, null, false, new Color(0.08f, 0.01f, 0.05f)),
                Flower = Mats.Lit(new Color(0.98f, 0.95f, 0.85f), 0.3f),
                GlassDark = Mats.Lit(new Color(0.03f, 0.035f, 0.05f), 0.92f, 0.3f),
                WhiteStone = Mats.Lit(new Color(0.96f, 0.94f, 0.9f), 0.35f, 0f, "tile_terrazzo"),
                BlackStone = Mats.Lit(new Color(0.55f, 0.55f, 0.58f), 0.45f, 0f, "cobble_wet"),
                Slate = Mats.Lit(new Color(0.08f, 0.09f, 0.09f), 0.3f),
                Chalk = Mats.Lit(new Color(0.9f, 0.9f, 0.86f), 0.2f),
            };
        }

        // ------------------------------------------------------------------ the lane
        /// <summary>
        /// Twelve 20 cm steps on 70 cm treads in two flights (a landing at 1.2 m), each riser faced with a row of glossy
        /// tiles in its own colours, an iron handrail; invisible ramps over each flight carry the walker smoothly.
        /// </summary>
        static void Lane(Transform root, KitBatch k, KitBatch glow, Pal m)
        {
            float w = LaneHW * 2f;
            k.Box(m.TreadTile, new Vector3(0f, -0.02f, (Lane0 + Stair0) * 0.5f), new Vector3(w, 0.05f, Stair0 - Lane0), 1.2f);
            Floor(root, "LaneFoot", new Vector3(0f, -0.05f, (14.2f + Stair0) * 0.5f), new Vector3(w, 0.1f, Stair0 - 14.2f), "tile");
            for (int s = 1; s <= 12; s++)
            {
                float z0 = s <= 6 ? Stair0 + Tread * (s - 1) : Land1 + Tread * (s - 7);
                float z1 = s == 6 ? Land1 : s == 12 ? Sq0 + 0.3f : z0 + Tread;
                float top = Rise * s;
                if (s < 12) k.Box(m.TreadTile, new Vector3(0f, top - 0.14f, (z0 + z1) * 0.5f), new Vector3(w, 0.28f, z1 - z0), 1.2f);
                k.Box(m.DarkMetal, new Vector3(0f, top - 0.004f, z0 + 0.03f), new Vector3(w, 0.01f, 0.06f));
                // The riser's tiles: a checker of two colours, a different pair on every step.
                var a = m.Tiles[s % m.Tiles.Length];
                var b = m.Tiles[(s * 2 + 3) % m.Tiles.Length];
                for (int i = 0; i < 20; i++)
                {
                    float x = -LaneHW + 0.1f + i * 0.2f;
                    k.Box(i % 2 == 0 ? a : b, new Vector3(x, top - Rise * 0.5f, z0 - 0.006f), new Vector3(0.185f, 0.185f, 0.012f));
                }
            }
            Floor(root, "LaneLanding", new Vector3(0f, Rise * 6f - 0.05f, (Land0 + Land1) * 0.5f), new Vector3(w, 0.1f, Land1 - Land0), "tile");
            Ramp(root, new Vector3(0f, 0f, Stair0 - 0.35f), new Vector3(0f, Rise * 6f, Land0 + 0.35f), w);
            Ramp(root, new Vector3(0f, Rise * 6f, Land1 - 0.35f), new Vector3(0f, SqY, Sq0 + 0.35f), w);
            // An iron handrail up the west side on short posts.
            var rail = new List<Vector3>
            {
                new Vector3(-1.86f, 0.95f, Stair0 - 0.2f), new Vector3(-1.86f, Rise * 6f + 0.95f, Land0), new Vector3(-1.86f, Rise * 6f + 0.95f, Land1),
                new Vector3(-1.86f, SqY + 0.95f, Sq0 + 0.1f),
            };
            k.Of(m.DarkMetal).AddTube(rail.GetRange(0, 2), 0.022f, 6);
            k.Of(m.DarkMetal).AddTube(rail.GetRange(2, 2), 0.022f, 6);
            foreach (var z in new[] { 15.75f, 16.95f, 18.15f, 19.0f, 20.45f, 21.6f, 22.75f, 23.65f })
            {
                float y = StairTop(z), ry = RailY(rail, z);
                k.Box(m.DarkMetal, new Vector3(-1.86f, (y + ry) * 0.5f, z), new Vector3(0.03f, ry - y, 0.03f));
                k.Box(m.DarkMetal, new Vector3(-1.95f, ry - 0.06f, z), new Vector3(0.18f, 0.03f, 0.03f));
            }
            // The entrance sign over the foot of the lane, hung between the houses and ringed with bulbs.
            var sign = new Vector3(0f, 4.35f, 15.2f);
            k.Box(m.Plume[5], sign, new Vector3(3.3f, 0.6f, 0.06f));
            k.Box(m.Gold, sign + new Vector3(0f, 0.33f, 0f), new Vector3(3.4f, 0.05f, 0.08f));
            k.Box(m.Gold, sign + new Vector3(0f, -0.33f, 0f), new Vector3(3.4f, 0.05f, 0.08f));
            foreach (var x in new[] { -1.2f, 1.2f }) k.Box(m.DarkMetal, sign + new Vector3(x, 0.55f, 0f), new Vector3(0.015f, 0.45f, 0.015f));
            k.Box(m.DarkMetal, new Vector3(0f, sign.y + 0.78f, sign.z), new Vector3(w, 0.03f, 0.03f));
            for (int i = 0; i < 15; i++) glow.Of(m.Bulbs[i % m.Bulbs.Length]).AddSphere(sign + new Vector3(-1.6f + i * (3.2f / 14f), 0.4f, -0.04f), 0.035f, 6, 4);
            Text(root, "LADEIRA DO TAMBOR", sign + new Vector3(0f, 0f, -0.035f), 0f, 0.26f, 3.1f, new Color(1f, 0.93f, 0.6f));
            Text(root, "LADEIRA DO TAMBOR", sign + new Vector3(0f, 0f, 0.035f), 180f, 0.26f, 3.1f, new Color(1f, 0.93f, 0.6f));
        }

        /// <summary>The handrail's height at z (it runs straight between its points).</summary>
        static float RailY(List<Vector3> rail, float z)
        {
            for (int i = 1; i < rail.Count; i++)
                if (z <= rail[i].z) return Mathf.Lerp(rail[i - 1].y, rail[i].y, Mathf.InverseLerp(rail[i - 1].z, rail[i].z, z));
            return rail[rail.Count - 1].y;
        }

        /// <summary>The height of the tread (or landing) at z, for things set on the stairs.</summary>
        static float StairTop(float z)
        {
            if (z < Stair0) return 0f;
            if (z < Land0) return Rise * (1 + Mathf.FloorToInt((z - Stair0) / Tread));
            if (z < Land1) return Rise * 6f;
            if (z < Sq0) return Rise * (7 + Mathf.FloorToInt((z - Land1) / Tread));
            return SqY;
        }

        /// <summary>The houses either side of the lane: painted, two storeys, doors at the landing, balconies and laundry.</summary>
        static void LaneHouses(Transform root, KitBatch k, KitBatch glow, Pal m)
        {
            House(root, k, m, m.Plaster[0], -SqX, -LaneHW, Lane0 + 0.05f, 19.4f, 0f, 6.2f);
            House(root, k, m, m.Plaster[1], -SqX, -LaneHW, 19.4f, Sq0, 0f, 6.8f);
            House(root, k, m, m.Plaster[2], LaneHW, SqX, Lane0 + 0.05f, 20.0f, 0f, 5.8f);
            House(root, k, m, m.Plaster[3], LaneHW, SqX, 20.0f, Sq0, 0f, 7.0f);
            // The thin stubs that close the gap between the lane's houses and the avenue's remaining bound.
            foreach (var sx in new[] { -1f, 1f })
                Wall(root, "Bound", new Vector3(sx * (LaneHW + 0.15f), 2f, (14.0f + Lane0 + 0.1f) * 0.5f), new Vector3(0.3f, 4f, Lane0 + 0.1f - 14.0f));
            var west = Vector3.right;   // the west houses' lane face looks east
            var east = Vector3.left;
            var shutter = m.Plaster[2];
            // West, lower house (rose): a barred window by the stairs, two windows upstairs over a balcony.
            Win(k, glow, m, new Vector3(-LaneHW, 2.15f, 16.7f), west, 0.8f, 0.9f, false, null, true);
            Win(k, glow, m, new Vector3(-LaneHW, 4.3f, 16.3f), west, 0.85f, 1.2f, true, shutter, false);
            Win(k, glow, m, new Vector3(-LaneHW, 4.3f, 18.5f), west, 0.85f, 1.2f, false, shutter, false);
            Balcony(k, m, new Vector3(-LaneHW, 3.35f, 16.4f), west, 1.7f, 0.5f);
            Win(k, glow, m, new Vector3(-4.3f, 2.1f, Lane0 + 0.05f), Vector3.back, 0.85f, 1.0f, false, shutter, false);
            Win(k, glow, m, new Vector3(-4.3f, 4.4f, Lane0 + 0.05f), Vector3.back, 0.85f, 1.1f, true, shutter, false);
            // West, upper house (sun yellow): a door onto the landing, a barred window, two windows upstairs.
            Door(k, m, new Vector3(-LaneHW, Rise * 6f, 19.85f), west, 0.82f, 2.1f, m.Plume[2]);
            Win(k, glow, m, new Vector3(-LaneHW, 3.15f, 21.7f), west, 0.8f, 0.85f, false, null, true);
            Win(k, glow, m, new Vector3(-LaneHW, 4.9f, 21.0f), west, 0.85f, 1.15f, false, m.Plaster[0], false);
            Win(k, glow, m, new Vector3(-LaneHW, 4.9f, 22.9f), west, 0.85f, 1.15f, true, m.Plaster[0], false);
            Door(k, m, new Vector3(-4.6f, SqY, Sq0), Vector3.forward, 0.9f, 2.1f, m.Plume[6]);
            Win(k, glow, m, new Vector3(-3.0f, 3.5f, Sq0), Vector3.forward, 0.8f, 1.0f, true, m.Plaster[2], false);
            Win(k, glow, m, new Vector3(-5.3f, 5.5f, Sq0), Vector3.forward, 0.8f, 1.0f, false, m.Plaster[2], false);
            Win(k, glow, m, new Vector3(-3.4f, 5.5f, Sq0), Vector3.forward, 0.8f, 1.0f, false, m.Plaster[2], false);
            // East, lower house (turquoise): a door onto the landing, a barred window, two windows upstairs; a sign.
            Door(k, m, new Vector3(LaneHW, Rise * 6f, 19.55f), east, 0.8f, 2.1f, m.Plume[1]);
            Win(k, glow, m, new Vector3(LaneHW, 2.05f, 16.9f), east, 0.8f, 0.9f, false, null, true);
            Win(k, glow, m, new Vector3(LaneHW, 4.2f, 16.2f), east, 0.85f, 1.15f, false, m.Plaster[1], false);
            Win(k, glow, m, new Vector3(LaneHW, 4.2f, 18.3f), east, 0.85f, 1.15f, true, m.Plaster[1], false);
            Win(k, glow, m, new Vector3(4.3f, 2.1f, Lane0 + 0.05f), Vector3.back, 0.85f, 1.0f, true, m.Plaster[1], false);
            var board = new Vector3(4.25f, 3.65f, Lane0 + 0.02f);
            k.Box(m.Slate, board, new Vector3(1.1f, 0.42f, 0.03f));
            Text(root, "BAR  ↑", board + new Vector3(0f, 0f, -0.02f), 0f, 0.2f, 1.0f, new Color(0.95f, 0.93f, 0.86f));
            // East, upper house (lime): a barred window, two windows upstairs behind a balcony with plants.
            Win(k, glow, m, new Vector3(LaneHW, 3.15f, 21.8f), east, 0.8f, 0.85f, false, null, true);
            Win(k, glow, m, new Vector3(LaneHW, 5.1f, 21.2f), east, 0.85f, 1.15f, true, m.Plaster[4], false);
            Win(k, glow, m, new Vector3(LaneHW, 5.1f, 22.9f), east, 0.85f, 1.15f, false, m.Plaster[4], false);
            Balcony(k, m, new Vector3(LaneHW, 4.25f, 22.05f), east, 2.4f, 0.5f);
            Win(k, glow, m, new Vector3(3.6f, 3.7f, Sq0), Vector3.forward, 0.8f, 1.0f, false, m.Plaster[0], false);
            Win(k, glow, m, new Vector3(5.4f, 5.7f, Sq0), Vector3.forward, 0.8f, 1.0f, true, m.Plaster[0], false);
            // Plants on the landing (with colliders), water tanks on the roofs.
            Pot(k, m, new Vector3(1.7f, Rise * 6f, 20.12f), 1f);
            Wall(root, "Pot", new Vector3(1.7f, Rise * 6f + 0.3f, 20.12f), new Vector3(0.4f, 0.6f, 0.36f));
            Pot(k, m, new Vector3(-1.72f, Rise * 6f, 19.26f), 0.8f);
            Wall(root, "Pot", new Vector3(-1.72f, Rise * 6f + 0.25f, 19.26f), new Vector3(0.34f, 0.5f, 0.3f));
            foreach (var (p, h) in new[] { (new Vector3(-5.2f, 6.2f, 17.2f), 1.1f), (new Vector3(5.3f, 5.8f, 17.6f), 1.0f), (new Vector3(-5.0f, 6.8f, 22.2f), 0.9f) })
            {
                k.Of(m.Plume[3]).AddCylinder(p, 0.55f, h, 12);
                k.Of(m.DarkMetal).AddCylinder(p + Vector3.up * h, 0.57f, 0.05f, 12);
            }
        }

        // ------------------------------------------------------------------ the square
        /// <summary>
        /// The square at 2.4 m: Portuguese pavement in white and black waves, the houses round it (a chained gate at the
        /// north-east corner where steps climb on up the hill), bougainvillea over the west walls, string lights.
        /// </summary>
        static void Square(Transform root, KitBatch k, KitBatch glow, Pal m)
        {
            var fc = new Vector3(0f, SqY - 0.5f, (Sq0 + Sq1) * 0.5f);
            var fs = new Vector3(SqX * 2f, 1f, Sq1 - Sq0);
            k.Box(m.WhiteStone, fc, fs, 0.5f);
            Floor(root, "Praca", fc, fs, "stone");
            for (int i = 0; i < 9; i++)
                WaveBand(k.Of(m.BlackStone), -SqX, SqX, Sq0 + 0.2f, Sq1 - 0.2f, Sq0 + 0.4f + i * 1.2f, 0.34f, 0.22f, 2.6f, i * 0.7f, SqY + 0.003f);
            // North houses (lilac, yellow, rose) facing the square, with an alley and its chained gate at the east end.
            House(root, k, m, m.Plaster[4], -SqX, -2.2f, Sq1, Sq1 + 3f, SqY, SqY + 9.0f);
            House(root, k, m, m.Plaster[1], -2.2f, 2.4f, Sq1, Sq1 + 3f, SqY, SqY + 7.6f);
            House(root, k, m, m.Plaster[0], 2.4f, 5.0f, Sq1, Sq1 + 3f, SqY, SqY + 8.4f);
            var n = Vector3.back;
            Door(k, m, new Vector3(-4.3f, SqY, Sq1), n, 0.9f, 2.1f, m.Plume[3]);
            Door(k, m, new Vector3(0.9f, SqY, Sq1), n, 0.9f, 2.1f, m.Plume[0]);
            foreach (var x in new[] { -5.6f, -3.0f }) Win(k, glow, m, new Vector3(x, SqY + 1.2f, Sq1), n, 0.8f, 1.0f, x < -4f, m.Plaster[3], false);
            Win(k, glow, m, new Vector3(-1.2f, SqY + 1.2f, Sq1), n, 0.8f, 1.0f, false, m.Plaster[2], false);
            Win(k, glow, m, new Vector3(3.7f, SqY + 1.2f, Sq1), n, 0.8f, 1.0f, true, m.Plaster[2], false);
            foreach (var x in new[] { -5.6f, -4.3f, -3.0f, -1.2f, 0.9f, 3.7f })
                Win(k, glow, m, new Vector3(x, SqY + 3.6f, Sq1), n, 0.8f, 1.1f, x == -4.3f || x == 0.9f, x < -2.2f ? m.Plaster[1] : m.Plaster[5], false);
            foreach (var x in new[] { -5.0f, -3.6f }) Win(k, glow, m, new Vector3(x, SqY + 6.1f, Sq1), n, 0.8f, 1.0f, x < -4f, m.Plaster[1], false);
            Balcony(k, m, new Vector3(-4.3f, SqY + 2.95f, Sq1), n, 3.4f, 0.55f);
            Balcony(k, m, new Vector3(3.7f, SqY + 2.95f, Sq1), n, 1.6f, 0.5f);
            // The alley: walls, steps climbing away north into the dark, and the chained iron gate.
            k.Box(m.Plaster[5], new Vector3(SqX + 0.2f, SqY + 2.2f, Sq1 + 2.0f), new Vector3(0.4f, 4.4f, 4.0f), 1.5f);
            for (int s = 0; s < 8; s++)
                k.Box(m.TreadTile, new Vector3(5.75f, SqY + (s + 1) * 0.125f, Sq1 + 0.5f + s * 0.42f), new Vector3(1.5f, (s + 1) * 0.25f, 0.42f), 1.2f);
            k.Box(m.Plaster[2], new Vector3(5.75f, SqY + 3.5f, Sq1 + 4.0f), new Vector3(1.5f, 7f, 0.3f), 1.5f);
            glow.Of(m.Bulbs[0]).AddSphere(new Vector3(5.75f, SqY + 3.6f, Sq1 + 3.8f), 0.07f, 8, 6);
            for (int i = 0; i <= 10; i++) k.Box(m.DarkMetal, new Vector3(5.05f + i * 0.14f, SqY + 1.0f, Sq1 + 0.05f), new Vector3(0.025f, 2.0f, 0.025f));
            foreach (var y in new[] { 0.15f, 1.95f }) k.Box(m.DarkMetal, new Vector3(5.75f, SqY + y, Sq1 + 0.05f), new Vector3(1.5f, 0.04f, 0.04f));
            k.Of(m.Chrome).AddTube(new[] { new Vector3(5.6f, SqY + 1.1f, Sq1 + 0.02f), new Vector3(5.75f, SqY + 0.95f, Sq1 - 0.01f), new Vector3(5.9f, SqY + 1.1f, Sq1 + 0.02f) }, 0.012f, 4);
            k.Box(m.DarkMetal, new Vector3(5.75f, SqY + 0.9f, Sq1 - 0.02f), new Vector3(0.08f, 0.1f, 0.04f));
            Wall(root, "Bound", new Vector3(5.75f, SqY + 1.5f, Sq1 + 0.05f), new Vector3(1.5f, 3f, 0.15f));
            Wall(root, "Bound", new Vector3(0f, SqY + 3f, Sq1 + 1.5f), new Vector3(SqX * 2f, 6f, 3f));
            // East houses (turquoise, lime) facing west, a stoop at a door.
            House(root, k, m, m.Plaster[2], SqX, SqX + 3f, Sq0, 29.2f, SqY, SqY + 6.6f);
            House(root, k, m, m.Plaster[3], SqX, SqX + 3f, 29.2f, Sq1, SqY, SqY + 7.4f);
            var e = Vector3.left;
            Door(k, m, new Vector3(SqX, SqY + 0.15f, 26.4f), e, 0.9f, 2.1f, m.Plume[5]);
            k.Box(m.TreadTile, new Vector3(SqX - 0.2f, SqY + 0.075f, 26.4f), new Vector3(0.4f, 0.15f, 1.2f));
            Floor(root, "Stoop", new Vector3(SqX - 0.2f, SqY + 0.075f, 26.4f), new Vector3(0.4f, 0.15f, 1.2f), "tile");
            Win(k, glow, m, new Vector3(SqX, SqY + 1.3f, 24.8f), e, 0.8f, 1.0f, false, m.Plaster[0], false);
            Win(k, glow, m, new Vector3(SqX, SqY + 1.3f, 28.0f), e, 0.8f, 1.0f, true, m.Plaster[0], false);
            foreach (var z in new[] { 24.8f, 26.4f, 28.0f }) Win(k, glow, m, new Vector3(SqX, SqY + 3.7f, z), e, 0.8f, 1.1f, z == 26.4f, m.Plaster[0], false);
            foreach (var z in new[] { 30.4f, 32.0f, 33.6f }) Win(k, glow, m, new Vector3(SqX, SqY + 1.3f, z), e, 0.8f, 1.0f, z == 32.0f, m.Plaster[4], false);
            foreach (var z in new[] { 30.4f, 32.0f, 33.6f }) Win(k, glow, m, new Vector3(SqX, SqY + 3.9f, z), e, 0.8f, 1.1f, z == 30.4f, m.Plaster[4], false);
            Balcony(k, m, new Vector3(SqX, SqY + 3.25f, 32.0f), e, 3.6f, 0.5f);
            // West walls either side of the bar, bougainvillea tumbling over them.
            foreach (var (z0, z1) in new[] { (Sq0, 26.5f), (33.0f, Sq1) })
            {
                var c = new Vector3(-SqX - 0.2f, SqY + 1.6f, (z0 + z1) * 0.5f);
                k.Box(m.Plaster[5], c, new Vector3(0.4f, 3.2f, z1 - z0), 1.5f);
                k.Box(m.Trim, c + Vector3.up * 1.63f, new Vector3(0.5f, 0.06f, z1 - z0 + 0.1f));
                Wall(root, "Bound", c, new Vector3(0.4f, 3.2f, z1 - z0));
                var rnd = new System.Random(Mathf.RoundToInt(z0 * 10f));
                for (int i = 0; i < 22; i++)
                {
                    var p = new Vector3(-SqX + DetailShapes.Range(rnd, -0.25f, 0.1f), SqY + DetailShapes.Range(rnd, 2.0f, 3.5f), DetailShapes.Range(rnd, z0 + 0.15f, z1 - 0.15f));
                    DetailShapes.Ellipsoid(k.Of(i % 3 == 0 ? m.Foliage : m.Bloom), p, DetailShapes.Range(rnd, 0.14f, 0.26f), DetailShapes.Range(rnd, 0.12f, 0.2f), 6, 3);
                }
            }
            // String lights across the square from the bar's roof to the east houses.
            foreach (var (z, c) in new[] { (26.0f, 0), (29.2f, 2), (32.4f, 4) })
                StringLights(k, glow, m, new Vector3(-3.4f, SqY + 3.4f, z), new Vector3(SqX, SqY + 3.8f, z + 0.6f), 0.7f, c);
        }

        /// <summary>
        /// The corner bar (botequim): open to the square behind a tiled counter, shelves of bottles, a radio with a lit dial,
        /// a ceiling fan turning, a bare bulb, a striped awning with a valance that ripples, a chalk menu, three stools.
        /// </summary>
        static void Bar(Transform root, KitBatch k, KitBatch glow, Pal m)
        {
            const float x0 = -SqX, x1 = -3.4f, z0 = 26.5f, z1 = 33.0f, H = 3.3f;
            float y = SqY;
            k.Box(m.Plaster[1], new Vector3((x0 + x1) * 0.5f, y + H + 0.1f, (z0 + z1) * 0.5f), new Vector3(x1 - x0 + 0.1f, 0.2f, z1 - z0 + 0.1f), 1.5f);
            k.Box(m.Plaster[1], new Vector3(x0 + 0.15f, y + H * 0.5f, (z0 + z1) * 0.5f), new Vector3(0.3f, H, z1 - z0), 1.5f);
            k.Box(m.Plaster[1], new Vector3((x0 + x1) * 0.5f, y + H * 0.5f, z0 + 0.15f), new Vector3(x1 - x0, H, 0.3f), 1.5f);
            k.Box(m.Plaster[1], new Vector3((x0 + x1) * 0.5f, y + H * 0.5f, z1 - 0.15f), new Vector3(x1 - x0, H, 0.3f), 1.5f);
            k.Box(m.Trim, new Vector3((x0 + x1) * 0.5f, y + H + 0.26f, (z0 + z1) * 0.5f), new Vector3(x1 - x0 + 0.2f, 0.12f, z1 - z0 + 0.2f));
            k.Box(m.Terracotta, new Vector3((x0 + x1) * 0.5f, y + 0.01f, (z0 + z1) * 0.5f), new Vector3(x1 - x0 - 0.6f, 0.02f, z1 - z0 - 0.6f), 1.2f);
            Wall(root, "Bound", new Vector3((x0 + x1) * 0.5f, y + 1.8f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 3.6f, z1 - z0));
            // The counter: a wooden top over a front of blue and white tiles.
            var cz0 = z0 + 0.5f; var cz1 = z1 - 0.5f;
            k.Box(m.Wood, new Vector3(x1 - 0.25f, y + 1.03f, (cz0 + cz1) * 0.5f), new Vector3(0.62f, 0.05f, cz1 - cz0 + 0.1f));
            for (int r = 0; r < 5; r++)
                for (int c = 0; c < Mathf.FloorToInt((cz1 - cz0) / 0.2f); c++)
                    k.Box((r + c) % 2 == 0 ? m.Tiles[3] : m.Tiles[4], new Vector3(x1 + 0.005f, y + 0.1f + r * 0.2f, cz0 + 0.1f + c * 0.2f), new Vector3(0.012f, 0.185f, 0.185f));
            k.Box(m.Wood, new Vector3(x1 - 0.3f, y + 0.5f, (cz0 + cz1) * 0.5f), new Vector3(0.6f, 1.0f, cz1 - cz0));
            // Shelves of bottles on the back wall.
            var rnd = new System.Random(77);
            for (int sh = 0; sh < 3; sh++)
            {
                float sy = y + 1.35f + sh * 0.45f;
                k.Box(m.Wood, new Vector3(x0 + 0.42f, sy, (z0 + z1) * 0.5f), new Vector3(0.24f, 0.03f, z1 - z0 - 1.2f));
                for (float z = z0 + 0.8f; z < z1 - 0.8f; z += 0.13f + (float)rnd.NextDouble() * 0.08f)
                {
                    float bh = 0.18f + (float)rnd.NextDouble() * 0.14f;
                    var mat = rnd.NextDouble() < 0.5 ? m.GlassLit[0] : m.Plume[rnd.Next(m.Plume.Length)];
                    var bp = new Vector3(x0 + 0.42f, sy + 0.015f, z);
                    k.Of(mat).AddCylinder(bp, 0.035f, bh, 8);
                    k.Of(mat).AddCylinder(bp + Vector3.up * bh, 0.012f, 0.06f, 6);
                }
            }
            // The radio on the counter: a wooden case, a grille and a lit dial.
            var rp = new Vector3(x1 - 0.28f, y + 1.055f, 27.8f);
            k.Box(m.Wood, rp + Vector3.up * 0.13f, new Vector3(0.22f, 0.26f, 0.36f));
            k.Box(m.Straw, rp + new Vector3(0.112f, 0.15f, 0.06f), new Vector3(0.01f, 0.16f, 0.16f));
            glow.Box(m.GlassLit[0], rp + new Vector3(0.113f, 0.15f, -0.1f), new Vector3(0.01f, 0.06f, 0.1f));
            k.Of(m.Chrome).AddCylinder(rp + new Vector3(0f, 0.26f, -0.12f), 0.004f, 0.3f, 4, 1f, Quaternion.Euler(0f, 0f, -20f));
            // The menu on the south wall, chalk on slate.
            var menu = new Vector3((x0 + x1) * 0.5f, y + 1.9f, z0 - 0.01f);
            k.Box(m.Slate, menu, new Vector3(1.6f, 1.0f, 0.03f));
            k.Box(m.Wood, menu + new Vector3(0f, 0.53f, 0f), new Vector3(1.7f, 0.06f, 0.05f));
            Text(root, "CAIPIRINHA\nCERVEJA\nPASTEL\nCAFEZINHO", menu + new Vector3(0f, 0f, -0.02f), 0f, 0.12f, 1.4f, new Color(0.93f, 0.92f, 0.86f));
            // The awning over the front and its valance.
            for (int i = 0; i < 13; i++)
            {
                float z = z0 - 0.1f + i * 0.52f;
                k.Box(i % 2 == 0 ? m.Plume[0] : m.Plume[3], new Vector3(x1 + 0.55f, y + 2.73f, z + 0.26f), new Vector3(1.15f, 0.03f, 0.52f), Quaternion.Euler(0f, 0f, -18f));
            }
            foreach (var z in new[] { z0, z1 }) k.Box(m.DarkMetal, new Vector3(x1 + 0.55f, y + 2.73f, z), new Vector3(1.15f, 0.03f, 0.03f), Quaternion.Euler(0f, 0f, -18f));
            // A bare bulb and a fan under the bar's ceiling (moving parts and the light come in Moving).
            k.Box(m.DarkMetal, new Vector3(-4.9f, y + H - 0.12f, 28.8f), new Vector3(0.04f, 0.24f, 0.04f));
            // Stools along the counter.
            foreach (var z in new[] { 28.0f, 29.3f, 31.75f })
            {
                var sp = new Vector3(x1 + 0.45f, y, z);
                k.Of(m.Chrome).AddCylinder(sp, 0.16f, 0.03f, 12);
                k.Of(m.Chrome).AddCylinder(sp, 0.025f, 0.72f, 8);
                k.Of(m.Plume[0]).AddCylinder(sp + Vector3.up * 0.72f, 0.17f, 0.06f, 14);
                k.Of(m.Chrome).AddTorus(sp + Vector3.up * 0.3f, 0.13f, 0.01f, 12, 3, Quaternion.identity);
                Wall(root, "Stool", sp + Vector3.up * 0.4f, new Vector3(0.36f, 0.8f, 0.36f));
            }
        }

        /// <summary>
        /// The samba circle frozen mid-song on the east of the square: a surdo on its stand (the set piece), a drummer with
        /// a tamborim raised, a passista mid-step, a woman on a plastic chair, a child watching, and a man at the bar with
        /// his cup in the air. Returns the figures the drum can make twitch.
        /// </summary>
        static RioFrozenFigure[] DrumCircle(Transform root, KitBatch k, Pal m)
        {
            var figs = new List<RioFrozenFigure>
            {
                Figure(root, m, new Vector3(4.75f, SqY, 30.35f), -125f, FigPose.Drum, 1f, 0),
                Figure(root, m, new Vector3(2.25f, SqY, 31.7f), 150f, FigPose.Samba, 1f, 1),
                Figure(root, m, new Vector3(-2.85f, SqY, 30.5f), -90f, FigPose.Cheer, 1f, 2),
                Figure(root, m, new Vector3(5.3f, SqY, 27.9f), -50f, FigPose.Seated, 1f, 3),
                Figure(root, m, new Vector3(1.2f, SqY, 27.2f), 40f, FigPose.Wave, 0.72f, 4),
            };
            Chair(k, m, new Vector3(5.3f, SqY, 27.9f), -50f, m.DrumHead);
            Chair(k, m, new Vector3(3.9f, SqY, 32.6f), 170f, m.Plume[0]);
            Wall(root, "Figure", new Vector3(5.3f, SqY + 0.6f, 27.95f), new Vector3(0.75f, 1.2f, 0.75f));
            Wall(root, "Chair", new Vector3(3.9f, SqY + 0.45f, 32.6f), new Vector3(0.5f, 0.9f, 0.5f));
            foreach (var p in new[] { new Vector3(4.75f, 0f, 30.35f), new Vector3(2.25f, 0f, 31.7f), new Vector3(-2.85f, 0f, 30.5f) })
                Wall(root, "Figure", new Vector3(p.x, SqY + 0.9f, p.z), new Vector3(0.5f, 1.8f, 0.5f));
            Wall(root, "Figure", new Vector3(1.2f, SqY + 0.65f, 27.2f), new Vector3(0.4f, 1.3f, 0.4f));
            // A plastic table with two cups and a bottle, all as they were.
            var t = new Vector3(4.5f, SqY, 32.35f);
            k.Of(m.DrumHead).AddCylinder(t + Vector3.up * 0.7f, 0.4f, 0.03f, 18);
            foreach (var a in new[] { 45f, 135f, 225f, 315f })
            {
                var d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                k.Box(m.DrumHead, t + d * 0.28f + Vector3.up * 0.35f, new Vector3(0.04f, 0.7f, 0.04f));
            }
            k.Of(m.DrumHead).AddFrustum(t + new Vector3(0.12f, 0.73f, 0.05f), 0.03f, 0.04f, 0.1f, 10);
            k.Of(m.DrumHead).AddFrustum(t + new Vector3(-0.15f, 0.73f, -0.08f), 0.03f, 0.04f, 0.1f, 10);
            k.Of(m.GlassLit[0]).AddCylinder(t + new Vector3(0f, 0.73f, 0.15f), 0.035f, 0.24f, 8);
            Wall(root, "Table", t + Vector3.up * 0.37f, new Vector3(0.82f, 0.74f, 0.82f));

            // The surdo on its stand: chrome shell, coloured band, rims and rods; its head and mallet move on a hit.
            var d0 = new Vector3(3.6f, SqY, 29.4f);
            for (int i = 0; i < 3; i++)
            {
                var dir = Quaternion.Euler(0f, i * 120f + 30f, 0f) * Vector3.forward;
                Limb(k.Of(m.DarkMetal), d0 + dir * 0.34f, d0 + Vector3.up * 0.62f + dir * 0.13f, 0.013f, 0.013f, 5);
            }
            k.Of(m.DarkMetal).AddTorus(d0 + Vector3.up * 0.63f, 0.28f, 0.013f, 18, 4, Quaternion.identity);
            var drumBase = d0 + Vector3.up * 0.64f;
            k.Of(m.Chrome).AddCylinder(drumBase, 0.3f, 0.5f, 20);
            k.Of(m.Plume[0]).AddCylinder(drumBase + Vector3.up * 0.18f, 0.302f, 0.13f, 20);
            foreach (var yy in new[] { 0.01f, 0.49f }) k.Of(m.Chrome).AddTorus(drumBase + Vector3.up * yy, 0.31f, 0.02f, 20, 4, Quaternion.identity);
            for (int i = 0; i < 8; i++)
            {
                var dd = Quaternion.Euler(0f, i * 45f + 22f, 0f) * Vector3.right * 0.322f;
                Limb(k.Of(m.Chrome), drumBase + dd + Vector3.up * 0.04f, drumBase + dd + Vector3.up * 0.46f, 0.007f, 0.007f, 4);
            }
            Wall(root, "Drum", d0 + Vector3.up * 0.6f, new Vector3(0.7f, 1.2f, 0.7f));
            var head = new GameObject("DrumHead").transform;
            head.SetParent(root, false);
            head.localPosition = drumBase + Vector3.up * 0.5f;
            var hb = new MeshBuilder();
            hb.AddCylinder(Vector3.zero, 0.29f, 0.008f, 20);
            MeshFactory.FromBuilder("Skin", hb, m.DrumHead, head);
            var mallet = new GameObject("Mallet").transform;
            mallet.SetParent(root, false);
            mallet.localPosition = drumBase + new Vector3(-0.3f, 0.52f, 0f);
            mallet.localRotation = Quaternion.Euler(0f, 0f, -80f);
            var mb = new MeshBuilder();
            mb.AddCylinder(Vector3.zero, 0.012f, 0.42f, 6);
            mb.AddSphere(new Vector3(0f, 0.44f, 0f), 0.055f, 8, 6);
            MeshFactory.FromBuilder("MalletMesh", mb, m.Wood, mallet);

            // Confetti that puffs up off the drum on every hit and hangs in the air.
            var puff = new GameObject("ConfettiPuff");
            puff.transform.SetParent(root, false);
            puff.transform.localPosition = drumBase + Vector3.up * 0.56f;
            puff.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = puff.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.06f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(ConfettiColours()) { mode = ParticleSystemGradientMode.RandomColor };
            main.gravityModifier = -0.012f;
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 35f; sh.radius = 0.24f;
            var drag = ps.limitVelocityOverLifetime; drag.enabled = true; drag.drag = 3f; drag.multiplyDragByParticleSize = false;
            var spin = ps.rotationOverLifetime; spin.enabled = true; spin.z = new ParticleSystem.MinMaxCurve(-1f, 1f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) });
            col.color = fade;
            var r = puff.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = Mats.Particle(Tex.White, false, Color.white, 1.1f, Mats.StencilNotEqual);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 0.08f;

            var use = new GameObject("DrumUse");
            use.transform.SetParent(root, false);
            use.transform.localPosition = drumBase + Vector3.up * 0.45f;
            var trig = use.AddComponent<SphereCollider>();
            trig.radius = 0.45f;
            trig.isTrigger = true;
            var arr = figs.ToArray();
            use.AddComponent<RioDrum>().Setup(head, mallet, ps, arr);
            return arr;
        }

        /// <summary>Pots and plants round the square, a frangipani in a tiled planter by the stairs.</summary>
        static void Furnish(Transform root, KitBatch k, Pal m)
        {
            foreach (var (p, s) in new[]
            {
                (new Vector3(-5.9f, SqY, 24.4f), 1.2f), (new Vector3(-5.9f, SqY, 34.0f), 1.0f), (new Vector3(-2.6f, SqY, 34.1f), 0.9f), (new Vector3(2.0f, SqY, 34.1f), 1.1f),
                (new Vector3(6.0f, SqY, 24.4f), 1.0f), (new Vector3(6.05f, SqY, 29.2f), 1.2f), (new Vector3(-4.6f, SqY, 24.3f), 0.85f),
            })
            {
                Pot(k, m, p, s);
                Wall(root, "Pot", p + Vector3.up * 0.3f * s, new Vector3(0.45f * s, 0.6f * s, 0.45f * s));
            }
            // The frangipani: a round planter faced with tiles, a forked trunk and a crown of leaves and pale flowers.
            var tp = new Vector3(3.5f, SqY, 25.5f);
            k.Of(m.Terracotta).AddCylinder(tp, 0.6f, 0.45f, 20);
            k.Of(m.Trim).AddTorus(tp + Vector3.up * 0.45f, 0.6f, 0.04f, 20, 4, Quaternion.identity);
            k.Of(m.Soil).AddCylinder(tp + Vector3.up * 0.44f, 0.56f, 0.02f, 18);
            for (int i = 0; i < 16; i++)
            {
                var d = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                k.Box(i % 2 == 0 ? m.Tiles[1] : m.Tiles[3], tp + d * 0.602f + Vector3.up * 0.22f, new Vector3(0.2f, 0.2f, 0.012f), Quaternion.LookRotation(d, Vector3.up));
            }
            var fork = tp + new Vector3(0.1f, 1.7f, 0.05f);
            k.Of(m.Wood).AddTube(new[] { tp + Vector3.up * 0.45f, tp + new Vector3(0.05f, 1.1f, 0f), fork }, 0.07f, 7);
            var rnd = new System.Random(9);
            foreach (var dir in new[] { new Vector3(0.6f, 1f, 0.2f), new Vector3(-0.5f, 1f, 0.3f), new Vector3(0f, 1f, -0.6f) })
            {
                var tip = fork + dir.normalized * 0.7f;
                k.Of(m.Wood).AddTube(new[] { fork, tip }, 0.04f, 5);
                for (int i = 0; i < 4; i++)
                {
                    var p = tip + new Vector3(DetailShapes.Range(rnd, -0.3f, 0.3f), DetailShapes.Range(rnd, -0.05f, 0.3f), DetailShapes.Range(rnd, -0.3f, 0.3f));
                    DetailShapes.Ellipsoid(k.Of(i % 2 == 0 ? m.Foliage : m.FoliageLit), p, DetailShapes.Range(rnd, 0.22f, 0.32f), 0.16f, 7, 4);
                }
                for (int i = 0; i < 7; i++)
                    k.Of(m.Flower).AddSphere(tip + new Vector3(DetailShapes.Range(rnd, -0.35f, 0.35f), DetailShapes.Range(rnd, 0.1f, 0.4f), DetailShapes.Range(rnd, -0.35f, 0.35f)), 0.045f, 6, 4);
            }
            Wall(root, "Planter", tp + Vector3.up * 0.3f, new Vector3(1.2f, 0.6f, 1.2f));
            var trunk = new GameObject("PlanterTrunk");
            trunk.transform.SetParent(root, false);
            trunk.transform.localPosition = tp + Vector3.up * 1.1f;
            var cap = trunk.AddComponent<CapsuleCollider>();
            cap.radius = 0.15f;
            cap.height = 1.4f;
        }

        /// <summary>Confetti stopped in the air over the lane and the square: glinting specks and paper pieces.</summary>
        static void Confetti(Transform root, KitBatch lane, KitBatch square, Pal m)
        {
            foreach (var (c, s, n) in new[] { (new Vector3(0f, 2.6f, 19.5f), new Vector3(3.6f, 3.0f, 8.4f), 160), (new Vector3(0.8f, SqY + 2.0f, 29.5f), new Vector3(10f, 2.6f, 9.4f), 240) })
            {
                var ps = Kit.Weather(root, c, s, false, new Color(0.9f, 0.75f, 0.55f, 0.9f), 0f, 0f);
                var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)n) });
                var main = ps.main;
                main.startLifetime = 9999f;
                main.loop = false;
                main.startColor = new ParticleSystem.MinMaxGradient(ConfettiColours()) { mode = ParticleSystemGradientMode.RandomColor };
                main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.04f);
            }
            var rnd = new System.Random(515);
            for (int i = 0; i < 300; i++)
            {
                bool inLane = i < 130;
                var p = inLane
                    ? new Vector3(DetailShapes.Range(rnd, -1.8f, 1.8f), 0f, DetailShapes.Range(rnd, Lane0 + 0.4f, Sq0 - 0.3f))
                    : new Vector3(DetailShapes.Range(rnd, -3.2f, SqX - 0.4f), 0f, DetailShapes.Range(rnd, Sq0 + 0.5f, Sq1 - 0.5f));
                p.y = StairTop(p.z) + DetailShapes.Range(rnd, 2.1f, 3.8f);
                if (!inLane && p.x > 1.0f && p.z > 27f && p.z < 33f && p.y < SqY + 2.6f) continue; // clear over the samba circle
                var mat = i % 9 == 0 ? m.SequinGold : m.Plume[i % m.Plume.Length];
                (inLane ? lane : square).Box(mat, p, new Vector3(0.036f, 0.025f, 0.0025f), Quaternion.Euler(DetailShapes.Range(rnd, 0f, 360f), DetailShapes.Range(rnd, 0f, 360f), DetailShapes.Range(rnd, 0f, 360f)));
            }
            for (int i = 0; i < 160; i++)
            {
                bool inLane = i < 70;
                var p = inLane
                    ? new Vector3(DetailShapes.Range(rnd, -1.9f, 1.9f), 0f, DetailShapes.Range(rnd, Lane0 + 0.2f, Sq0 - 0.1f))
                    : new Vector3(DetailShapes.Range(rnd, -3.3f, SqX - 0.3f), 0f, DetailShapes.Range(rnd, Sq0 + 0.3f, Sq1 - 0.3f));
                p.y = (p.z < Stair0 ? 0.005f : StairTop(p.z)) + 0.0045f;
                var mat = i % 13 == 0 ? m.SequinGold : m.Plume[i % m.Plume.Length];
                (inLane ? lane : square).Box(mat, p, new Vector3(0.036f, 0.002f, 0.025f), Quaternion.Euler(0f, DetailShapes.Range(rnd, 0f, 180f), 0f));
            }
        }

        static Gradient ConfettiColours()
        {
            var g = new Gradient { mode = GradientMode.Fixed };
            g.SetKeys(new[]
            {
                new GradientColorKey(new Color(1f, 0.25f, 0.7f), 0.2f), new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0.4f), new GradientColorKey(new Color(0.2f, 0.9f, 0.9f), 0.6f),
                new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0.8f), new GradientColorKey(new Color(0.7f, 0.4f, 1f), 1f),
            }, new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.9f, 1f) });
            return g;
        }

        // ------------------------------------------------------------------ what moves, lights, life, lamps, postcards
        static void Moving(Transform root, Pal m)
        {
            // Bunting across the lane and the square swings about its own cord.
            Bunting(root, m, new Vector3(-LaneHW, 5.05f, 16.1f), new Vector3(LaneHW, 5.05f, 16.1f), 0.35f, 0);
            Bunting(root, m, new Vector3(-LaneHW, 5.2f, 18.7f), new Vector3(LaneHW, 5.2f, 18.7f), 0.3f, 2);
            Bunting(root, m, new Vector3(-LaneHW, 5.4f, 21.4f), new Vector3(LaneHW, 5.4f, 21.4f), 0.35f, 4);
            Bunting(root, m, new Vector3(-LaneHW, 5.6f, 23.1f), new Vector3(LaneHW, 5.6f, 23.1f), 0.3f, 1);
            Bunting(root, m, new Vector3(-3.4f, SqY + 3.2f, 27.6f), new Vector3(SqX, SqY + 3.2f, 27.6f), 0.6f, 3);
            Bunting(root, m, new Vector3(-3.4f, SqY + 3.3f, 30.8f), new Vector3(SqX, SqY + 3.3f, 30.8f), 0.6f, 5);
            // Laundry across the lane between the rose house's balcony and the turquoise house's window.
            var lk = new KitBatch();
            lk.Of(m.DarkMetal).AddTube(Sag(new Vector3(-LaneHW + 0.45f, 4.55f, 17.35f), new Vector3(LaneHW, 4.55f, 17.35f), 0.12f, 10), 0.006f, 4);
            lk.Bake(root, "LaundryLine", false);
            var sheet = Mats.Lit(new Color(0.96f, 0.96f, 0.93f), 0.2f, 0f, "fabric_linen", false);
            var towel = Mats.Lit(new Color(0.5f, 0.86f, 0.86f), 0.2f, 0f, "fabric_linen", false);
            var shirt = Mats.Lit(new Color(1f, 0.82f, 0.35f), 0.2f, 0f, "fabric_linen", false);
            ClothFlag.Create(root, "Sheet", new Vector3(-1.45f, 4.48f, 17.35f), 1.15f, 0.95f, sheet, 0f, 0.06f);
            ClothFlag.Create(root, "Towel", new Vector3(-0.1f, 4.45f, 17.35f), 0.55f, 0.8f, towel, 0f, 0.06f);
            ClothFlag.Create(root, "Shirt", new Vector3(0.65f, 4.47f, 17.35f), 0.6f, 0.62f, shirt, 0f, 0.06f);
            // A carnival banner hung from the yellow house over the square, and the bar's valance under its awning.
            var banner = Mats.Lit(new Color(0.92f, 0.2f, 0.6f), 0.3f, 0f, "fabric_linen", false);
            ClothFlag.Create(root, "Banner", new Vector3(-1.45f, SqY + 5.55f, Sq1 - 0.08f), 2.7f, 1.1f, banner, 0f, 0.05f);
            var valance = Mats.Lit(new Color(0.98f, 0.92f, 0.86f), 0.25f, 0f, "fabric_linen", false);
            ClothFlag.Create(root, "Valance", new Vector3(-3.4f + 1.09f, SqY + 2.53f, 33.0f), 6.5f, 0.26f, valance, 90f, 0.035f);
            // The bar's fan turns under the ceiling; its bulb is a real, softly unsteady light.
            var fan = new GameObject("BarFan").transform;
            fan.SetParent(root, false);
            fan.localPosition = new Vector3(-4.9f, SqY + 3.05f, 28.8f);
            var fb = new MeshBuilder();
            fb.AddCylinder(new Vector3(0f, -0.06f, 0f), 0.07f, 0.1f, 10);
            for (int i = 0; i < 4; i++) fb.AddBox(Quaternion.Euler(0f, i * 90f, 0f) * new Vector3(0.33f, -0.03f, 0f), new Vector3(0.5f, 0.012f, 0.12f), Quaternion.Euler(0f, i * 90f, 8f));
            MeshFactory.FromBuilder("FanBlades", fb, m.Wood, fan);
            Rotor.Add(fan, 140f);
            var bulb = MeshFactory.Sphere("BarBulb", root, new Vector3(-5.2f, SqY + 2.85f, 31.2f), 0.05f, m.Bulbs[0], 10, 8);
            bulb.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            MeshFactory.Cylinder("BulbFlex", root, new Vector3(-5.2f, SqY + 2.9f, 31.2f), 0.004f, 0.4f, m.DarkMetal, 4, false);
            var barLight = PointLight(root, new Vector3(-5.2f, SqY + 2.75f, 31.2f), new Color(1f, 0.75f, 0.45f), 1.7f, 5.5f);
            Flicker.Add(barLight, 0.08f, 4f);
            // The square's string lights get one real light between them (pinkish warm, carnival).
            PointLight(root, new Vector3(1.6f, SqY + 3.9f, 30.0f), new Color(1f, 0.62f, 0.72f), 1.5f, 6.5f);
        }

        static void Life(Transform root)
        {
            AmbientBirds.Create(root, new Vector3(-0.6f, SqY, 30.4f), 1.4f, 8, AmbientBirds.Kind.Dove);
            AmbientBirds.Create(root, new Vector3(0f, Rise * 6f, 19.7f), 0.45f, 4, AmbientBirds.Kind.Dove);
        }

        static void Lamps(Transform root)
        {
            // Both stand on the square, far enough north that a lit lamp never reaches the avenue's meshes.
            SwitchLamp.Create(root, new Vector3(-2.75f, SqY, 25.0f), LampColor);
            SwitchLamp.Create(root, new Vector3(-0.6f, SqY, 34.05f), LampColor);
        }

        static void Postcards(Transform root)
        {
            Postcard.Create(root, CountryId.Brazil, 1, new Vector3(-2.95f, SqY + 1.15f, 32.45f), "Ordered one drink. They poured two and slid the second to an empty stool.");
            Postcard.Create(root, CountryId.Brazil, 2, new Vector3(5.6f, SqY + 1.1f, 33.95f), "Hit the drum once. Every face on the hill turned a little. Not all the way.");
            Postcard.Create(root, CountryId.Brazil, 3, new Vector3(-4.15f, 1.2f, 9.6f), "The whole avenue is holding its breath. I let mine out and they all heard.");
        }

        /// <summary>The avenue's north bound (z 14.15, x -4.8..4.8) keeps its ends and opens over the lane (x -2..2).</summary>
        static void OpenBound(Transform cellT)
        {
            var bounds = cellT.Find("Bounds");
            Transform north = null;
            if (bounds != null)
                foreach (Transform c in bounds)
                    if (c.name == "Bound" && Mathf.Abs(c.localPosition.z - 14.15f) < 0.05f && Mathf.Abs(c.localPosition.x) < 0.05f) { north = c; break; }
            var box = north != null ? north.GetComponent<BoxCollider>() : null;
            if (box == null)
            {
                Debug.LogWarning("[TS-Expand] Rio: the north bound is not where expected; the lane stays shut");
                return;
            }
            float half = box.size.x * 0.5f;
            if (half <= LaneHW) return;
            var p = north.localPosition;
            var size = box.size;
            float w = half - LaneHW;
            north.localPosition = new Vector3(-(LaneHW + w * 0.5f), p.y, p.z);
            box.size = new Vector3(w, size.y, size.z);
            var east = new GameObject("Bound");
            east.transform.SetParent(bounds, false);
            east.transform.localPosition = new Vector3(LaneHW + w * 0.5f, p.y, p.z);
            east.AddComponent<BoxCollider>().size = new Vector3(w, size.y, size.z);
        }

        // ------------------------------------------------------------------ builders
        /// <summary>A painted house block (walls, a trim band, a parapet with its coping) with a collider.</summary>
        static void House(Transform root, KitBatch k, Pal m, Material plaster, float x0, float x1, float z0, float z1, float y0, float y1)
        {
            var c = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
            k.Box(plaster, c, new Vector3(x1 - x0, y1 - y0, z1 - z0), 1.6f);
            k.Box(m.Trim, new Vector3(c.x, y1 - 0.45f, c.z), new Vector3(x1 - x0 + 0.06f, 0.1f, z1 - z0 + 0.06f));
            k.Box(m.Trim, new Vector3(c.x, y1 + 0.03f, c.z), new Vector3(x1 - x0 + 0.1f, 0.06f, z1 - z0 + 0.1f));
            k.Box(m.Trim, new Vector3(c.x, y0 + 0.2f, c.z), new Vector3(x1 - x0 + 0.04f, 0.4f, z1 - z0 + 0.04f));
            Wall(root, "Bound", c, new Vector3(x1 - x0, y1 - y0, z1 - z0));
        }

        /// <summary>A window on a wall face (centre on the face, n outward): glass, a white frame and sill, optional shutters or bars.</summary>
        static void Win(KitBatch k, KitBatch glow, Pal m, Vector3 c, Vector3 n, float w, float h, bool lit, Material shutter, bool bars)
        {
            var q = Quaternion.LookRotation(n, Vector3.up);
            var r = Vector3.Cross(Vector3.up, n);
            if (lit) glow.Box(m.GlassLit[Mathf.Abs(Mathf.RoundToInt(c.x * 3f + c.z)) % m.GlassLit.Length], c + n * 0.005f, new Vector3(w, h, 0.02f), q);
            else k.Box(m.GlassDark, c + n * 0.005f, new Vector3(w, h, 0.02f), q);
            k.Box(m.Trim, c + n * 0.03f + Vector3.up * (h * 0.5f + 0.05f), new Vector3(w + 0.16f, 0.1f, 0.06f), q);
            k.Box(m.Trim, c + n * 0.05f - Vector3.up * (h * 0.5f + 0.04f), new Vector3(w + 0.2f, 0.07f, 0.1f), q);
            k.Box(m.Trim, c + n * 0.03f - r * (w * 0.5f + 0.04f), new Vector3(0.08f, h, 0.06f), q);
            k.Box(m.Trim, c + n * 0.03f + r * (w * 0.5f + 0.04f), new Vector3(0.08f, h, 0.06f), q);
            if (shutter != null)
                foreach (var s in new[] { -1f, 1f })
                {
                    k.Box(shutter, c + n * 0.04f + r * s * (w * 0.5f + 0.08f + w * 0.25f), new Vector3(w * 0.5f, h, 0.03f), q);
                    for (int i = 0; i < 5; i++) k.Box(m.Trim, c + n * 0.058f + r * s * (w * 0.5f + 0.08f + w * 0.25f) + Vector3.up * (-h * 0.4f + i * h * 0.2f), new Vector3(w * 0.46f, 0.015f, 0.006f), q);
                }
            if (bars)
                for (int i = 0; i < 5; i++) k.Box(m.DarkMetal, c + n * 0.06f + r * (-w * 0.4f + i * w * 0.2f), new Vector3(0.018f, h, 0.018f), q);
            if (!bars && shutter != null && !lit) Pot(k, m, c + n * 0.12f - Vector3.up * (h * 0.5f + 0.075f), 0.45f);
        }

        static void Door(KitBatch k, Pal m, Vector3 bottom, Vector3 n, float w, float h, Material paint)
        {
            var q = Quaternion.LookRotation(n, Vector3.up);
            var r = Vector3.Cross(Vector3.up, n);
            var c = bottom + Vector3.up * (h * 0.5f);
            k.Box(paint, c + n * 0.01f, new Vector3(w, h, 0.04f), q);
            k.Box(m.Trim, c + n * 0.035f + Vector3.up * (h * 0.5f + 0.06f), new Vector3(w + 0.2f, 0.12f, 0.06f), q);
            k.Box(m.Trim, c + n * 0.035f - r * (w * 0.5f + 0.05f), new Vector3(0.1f, h + 0.06f, 0.06f), q);
            k.Box(m.Trim, c + n * 0.035f + r * (w * 0.5f + 0.05f), new Vector3(0.1f, h + 0.06f, 0.06f), q);
            k.Box(m.Gold, c + n * 0.04f + r * (w * 0.32f) - Vector3.up * 0.08f, new Vector3(0.03f, 0.12f, 0.03f), q);
            foreach (var s in new[] { -1f, 1f }) k.Box(m.Trim, c + n * 0.03f + r * s * w * 0.22f + Vector3.up * h * 0.18f, new Vector3(w * 0.3f, h * 0.36f, 0.012f), q);
        }

        static void Balcony(KitBatch k, Pal m, Vector3 c, Vector3 n, float length, float depth)
        {
            var q = Quaternion.LookRotation(n, Vector3.up);
            var r = Vector3.Cross(Vector3.up, n);
            k.Box(m.Trim, c + n * (depth * 0.5f), new Vector3(length, 0.1f, depth), q);
            var a = c + n * (depth - 0.03f) - r * (length * 0.5f - 0.03f) + Vector3.up * 0.05f;
            var b = c + n * (depth - 0.03f) + r * (length * 0.5f - 0.03f) + Vector3.up * 0.05f;
            UrbanKit.Railing(k, m.DarkMetal, a, b, 0.9f, Vector3.zero);
            UrbanKit.Railing(k, m.DarkMetal, c - r * (length * 0.5f - 0.03f) + Vector3.up * 0.05f, a, 0.9f, Vector3.zero);
            UrbanKit.Railing(k, m.DarkMetal, c + r * (length * 0.5f - 0.03f) + Vector3.up * 0.05f, b, 0.9f, Vector3.zero);
            int pots = Mathf.Max(1, Mathf.RoundToInt(length / 0.9f));
            for (int i = 0; i < pots; i++)
                Pot(k, m, c + n * (depth * 0.45f) + r * ((i + 0.5f) / pots - 0.5f) * (length - 0.4f) + Vector3.up * 0.05f, 0.6f);
        }

        /// <summary>A terracotta pot with a mound of leaves (and now and then a few pink flowers).</summary>
        static void Pot(KitBatch k, Pal m, Vector3 b, float s)
        {
            k.Of(m.Terracotta).AddFrustum(b, 0.13f * s, 0.19f * s, 0.32f * s, 10);
            k.Of(m.Terracotta).AddTorus(b + Vector3.up * 0.32f * s, 0.19f * s, 0.022f * s, 10, 3, Quaternion.identity);
            k.Of(m.Soil).AddCylinder(b + Vector3.up * 0.3f * s, 0.17f * s, 0.02f * s, 10);
            DetailShapes.Ellipsoid(k.Of(m.Foliage), b + Vector3.up * 0.48f * s, 0.24f * s, 0.2f * s, 7, 4);
            DetailShapes.Ellipsoid(k.Of(m.FoliageLit), b + new Vector3(0.08f, 0.58f, 0.05f) * s, 0.14f * s, 0.12f * s, 6, 3);
            if (Mathf.RoundToInt(b.x * 7f + b.z * 3f) % 3 == 0)
                for (int i = 0; i < 4; i++) k.Of(m.Bloom).AddSphere(b + Quaternion.Euler(0f, i * 90f + 20f, 0f) * new Vector3(0.14f * s, 0.6f * s, 0f), 0.04f * s, 6, 4);
        }

        static void Chair(KitBatch k, Pal m, Vector3 at, float yaw, Material plastic)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            k.Box(plastic, at + q * new Vector3(0f, 0.44f, 0f), new Vector3(0.44f, 0.04f, 0.42f), q);
            k.Box(plastic, at + q * new Vector3(0f, 0.72f, -0.22f), new Vector3(0.44f, 0.5f, 0.03f), q * Quaternion.Euler(-10f, 0f, 0f));
            foreach (var x in new[] { -0.19f, 0.19f })
                foreach (var z in new[] { -0.18f, 0.18f })
                    k.Box(plastic, at + q * new Vector3(x, 0.22f, z), new Vector3(0.035f, 0.44f, 0.035f), q);
        }

        static void StringLights(KitBatch k, KitBatch glow, Pal m, Vector3 a, Vector3 b, float sag, int colour)
        {
            Vector3 P(float t) => Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
            const int segs = 20;
            for (int i = 0; i < segs; i++)
            {
                var p0 = P(i / (float)segs); var p1 = P((i + 1) / (float)segs);
                k.Box(m.DarkMetal, (p0 + p1) * 0.5f, new Vector3(0.012f, 0.012f, Vector3.Distance(p0, p1)), Quaternion.LookRotation((p1 - p0).normalized, Vector3.up));
            }
            int n = Mathf.RoundToInt(Vector3.Distance(a, b) / 0.5f);
            for (int i = 1; i < n; i++) glow.Of(m.Bulbs[(i + colour) % m.Bulbs.Length]).AddSphere(P(i / (float)n) + Vector3.down * 0.06f, 0.042f, 6, 4);
        }

        /// <summary>
        /// A string of pennants on its own transform at the cord's middle (local X along the cord), so WindSway can swing
        /// the whole sagging string about the line between its two hooks.
        /// </summary>
        static void Bunting(Transform root, Pal m, Vector3 a, Vector3 b, float sag, int colour)
        {
            var d = b - a; d.y = 0f;
            float len = d.magnitude;
            var dir = d / len;
            var t = new GameObject("Bunting").transform;
            t.SetParent(root, false);
            t.localPosition = (a + b) * 0.5f;
            t.localRotation = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up), Vector3.up);
            var k = new KitBatch();
            Vector3 P(float s) => new Vector3(Mathf.Lerp(-len * 0.5f, len * 0.5f, s), -sag * 4f * s * (1f - s), 0f);
            const int segs = 16;
            for (int i = 0; i < segs; i++)
            {
                var p0 = P(i / (float)segs); var p1 = P((i + 1) / (float)segs);
                k.Box(m.DarkMetal, (p0 + p1) * 0.5f, new Vector3(0.01f, 0.01f, Vector3.Distance(p0, p1)), Quaternion.LookRotation((p1 - p0).normalized, Vector3.up));
            }
            int n = Mathf.Max(4, Mathf.RoundToInt(len / 0.32f));
            for (int i = 1; i < n; i++)
            {
                var q = P(i / (float)n);
                var l = q - Vector3.right * 0.11f; var r = q + Vector3.right * 0.11f; var tip = q + Vector3.down * 0.28f;
                var mb = k.Of(m.Plume[(i % 2 == 0 ? colour : colour + 3) % m.Plume.Length]);
                mb.AddTriangle(l, r, tip, Vector2.zero, Vector2.right, new Vector2(0.5f, 1f));
                mb.AddTriangle(r, l, tip, Vector2.zero, Vector2.right, new Vector2(0.5f, 1f));
            }
            k.Bake(t, "Pennants", false);
            WindSway.Add(t, 7f, 2.6f + colour * 0.15f, Vector3.right);
        }

        /// <summary>One black band of the wave pavement along x (after Detail_Rio): across it from offset by width, with a sine.</summary>
        static void WaveBand(MeshBuilder b, float a0, float a1, float c0, float c1, float offset, float width, float amp, float wavelength, float phase, float y)
        {
            int n = Mathf.CeilToInt((a1 - a0) / 0.2f);
            for (int i = 0; i < n; i++)
            {
                float s0 = Mathf.Lerp(a0, a1, i / (float)n), s1 = Mathf.Lerp(a0, a1, (i + 1) / (float)n);
                float w0 = offset + amp * Mathf.Sin(s0 / wavelength * Mathf.PI * 2f + phase), w1 = offset + amp * Mathf.Sin(s1 / wavelength * Mathf.PI * 2f + phase);
                float lo0 = Mathf.Clamp(w0, c0, c1), hi0 = Mathf.Clamp(w0 + width, c0, c1), lo1 = Mathf.Clamp(w1, c0, c1), hi1 = Mathf.Clamp(w1 + width, c0, c1);
                if (hi0 - lo0 < 0.004f || hi1 - lo1 < 0.004f) continue;
                Vector3 A = new Vector3(s0, y, lo0), B = new Vector3(s1, y, lo1), C = new Vector3(s1, y, hi1), D = new Vector3(s0, y, hi0);
                if (Vector3.Cross(B - A, D - A).y < 0f) { var tmp = B; B = D; D = tmp; }
                b.AddQuad(A, B, C, D, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            }
        }

        // ------------------------------------------------------------------ the frozen figures
        static void PoseJoints(FigPose pose, out Vector3 hip, out Vector3 chest, out Vector3 footR, out Vector3 kneeR, out Vector3 footL, out Vector3 kneeL,
            out Vector3 elbowR, out Vector3 handR, out Vector3 elbowL, out Vector3 handL)
        {
            // The avenue's mannequin poses (Detail_RioFigures), feet at the origin, facing +Z.
            switch (pose)
            {
                case FigPose.Samba:
                    hip = new Vector3(0.02f, 0.94f, 0f); chest = new Vector3(0.04f, 1.38f, 0.03f);
                    footR = new Vector3(0.13f, 0.05f, 0.2f); kneeR = new Vector3(0.14f, 0.5f, 0.14f); footL = new Vector3(-0.1f, 0.13f, -0.16f); kneeL = new Vector3(-0.1f, 0.5f, -0.02f);
                    elbowR = new Vector3(0.33f, 1.62f, 0.06f); handR = new Vector3(0.4f, 1.95f, 0.12f); elbowL = new Vector3(-0.41f, 1.36f, 0.04f); handL = new Vector3(-0.63f, 1.44f, 0.14f);
                    break;
                case FigPose.Wave:
                    hip = new Vector3(0f, 0.95f, 0f); chest = new Vector3(0f, 1.39f, 0.02f);
                    footR = new Vector3(0.14f, 0.05f, 0.03f); kneeR = new Vector3(0.13f, 0.5f, 0.05f); footL = new Vector3(-0.14f, 0.05f, -0.02f); kneeL = new Vector3(-0.13f, 0.5f, 0f);
                    elbowR = new Vector3(0.34f, 1.7f, 0f); handR = new Vector3(0.47f, 2.0f, 0.06f); elbowL = new Vector3(-0.34f, 1.7f, 0f); handL = new Vector3(-0.47f, 2.0f, 0.06f);
                    break;
                case FigPose.Drum:
                    hip = new Vector3(0f, 0.95f, 0f); chest = new Vector3(0f, 1.38f, -0.03f);
                    footR = new Vector3(0.17f, 0.05f, 0.05f); kneeR = new Vector3(0.15f, 0.5f, 0.08f); footL = new Vector3(-0.17f, 0.05f, 0f); kneeL = new Vector3(-0.15f, 0.5f, 0.04f);
                    elbowR = new Vector3(0.31f, 1.56f, 0.16f); handR = new Vector3(0.27f, 1.84f, 0.32f); elbowL = new Vector3(-0.28f, 1.2f, 0.18f); handL = new Vector3(-0.13f, 1.22f, 0.42f);
                    break;
                case FigPose.Seated:
                    hip = new Vector3(0f, 0.47f, 0f); chest = new Vector3(0f, 0.9f, -0.02f);
                    footR = new Vector3(0.12f, 0.03f, 0.48f); kneeR = new Vector3(0.12f, 0.5f, 0.42f); footL = new Vector3(-0.12f, 0.03f, 0.44f); kneeL = new Vector3(-0.12f, 0.5f, 0.42f);
                    elbowR = new Vector3(0.26f, 1.18f, 0.06f); handR = new Vector3(0.3f, 1.48f, 0.1f); elbowL = new Vector3(-0.3f, 0.72f, 0.18f); handL = new Vector3(-0.16f, 0.55f, 0.32f);
                    break;
                default:
                    hip = new Vector3(0f, 0.95f, 0f); chest = new Vector3(0f, 1.39f, 0f);
                    footR = new Vector3(0.12f, 0.05f, 0f); kneeR = new Vector3(0.12f, 0.5f, 0.02f); footL = new Vector3(-0.12f, 0.05f, 0.02f); kneeL = new Vector3(-0.12f, 0.5f, 0.04f);
                    elbowR = new Vector3(0.24f, 1.76f, 0f); handR = new Vector3(0.28f, 2.06f, 0.03f); elbowL = new Vector3(-0.3f, 1.2f, 0.1f); handL = new Vector3(-0.2f, 1.38f, 0.26f);
                    break;
            }
        }

        /// <summary>
        /// A faceless store mannequin frozen mid-gesture (no real person), built in three pieces so it can twitch: the
        /// body, the head on its own pivot at the neck, and the right arm on its own pivot at the shoulder. The costume
        /// depends on <paramref name="dress"/>: 0 drummer, 1 passista, 2 man at the bar, 3 woman on a chair, 4 child.
        /// </summary>
        static RioFrozenFigure Figure(Transform root, Pal m, Vector3 feet, float yaw, FigPose pose, float s, int dress)
        {
            var go = new GameObject("FrozenFigure");
            go.transform.SetParent(root, false);
            go.transform.localPosition = feet;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            PoseJoints(pose, out var hip, out var chest, out var footR, out var kneeR, out var footL, out var kneeL, out var elbowR, out var handR, out var elbowL, out var handL);
            hip *= s; chest *= s; footR *= s; kneeR *= s; footL *= s; kneeL *= s; elbowR *= s; handR *= s; elbowL *= s; handL *= s;
            var up = (chest - hip).normalized;
            var right = Vector3.right;
            var fwd = Vector3.forward;
            var shoulderR = chest + up * 0.03f * s + right * 0.19f * s;
            var shoulderL = chest + up * 0.03f * s - right * 0.19f * s;
            var hipR = hip + right * 0.1f * s - up * 0.02f * s;
            var hipL = hip - right * 0.1f * s - up * 0.02f * s;
            var neck = chest + up * 0.09f * s;

            var body = new KitBatch();
            var skin = body.Of(m.Skin);
            Limb(skin, hip - up * 0.04f * s, chest, 0.13f * s, 0.155f * s);
            DetailShapes.Ellipsoid(skin, hip, 0.15f * s, 0.1f * s, 8, 4);
            DetailShapes.Ellipsoid(skin, chest, 0.19f * s, 0.07f * s, 8, 4);
            Limb(skin, chest + up * 0.03f * s, neck + up * 0.01f * s, 0.045f * s, 0.043f * s, 6);
            Limb(skin, hipR, kneeR, 0.075f * s, 0.055f * s); Limb(skin, kneeR, footR, 0.055f * s, 0.04f * s);
            Limb(skin, hipL, kneeL, 0.075f * s, 0.055f * s); Limb(skin, kneeL, footL, 0.055f * s, 0.04f * s);
            Limb(skin, shoulderL, elbowL, 0.048f * s, 0.038f * s); Limb(skin, elbowL, handL, 0.038f * s, 0.03f * s);
            foreach (var j in new[] { kneeR, kneeL, elbowL, shoulderL }) skin.AddSphere(j, 0.05f * s, 6, 4);
            skin.AddSphere(handL, 0.04f * s, 6, 4);
            foreach (var f in new[] { footR, footL }) skin.AddBox(f + fwd * 0.05f * s - Vector3.up * 0.015f * s, new Vector3(0.08f, 0.05f, 0.2f) * s);
            var torsoRot = Quaternion.FromToRotation(Vector3.up, up);
            switch (dress)
            {
                case 0: // drummer: a sequinned shirt, a stick in the left hand
                    body.Of(m.SequinTurquoise).AddFrustum(hip - up * 0.02f * s, 0.16f * s, 0.175f * s, 0.46f * s, 10, true, true, 1f, torsoRot);
                    Limb(body.Of(m.Wood), handL - fwd * 0.04f * s, handL + fwd * 0.28f * s + Vector3.up * 0.05f * s, 0.01f * s, 0.01f * s, 5);
                    break;
                case 1: // passista: sequinned bodice, a flared skirt with a gold belt, a small fan of plumes behind
                    body.Of(m.SequinGold).AddFrustum(hip + up * 0.1f * s, 0.15f * s, 0.168f * s, 0.34f * s, 10, true, true, 1f, torsoRot);
                    body.Of(m.SequinMagenta).AddFrustum(hip - up * 0.17f * s, 0.33f * s, 0.15f * s, 0.2f * s, 12, true, true, 1f, torsoRot);
                    body.Of(m.Gold).AddTorus(hip + up * 0.03f * s, 0.155f * s, 0.018f * s, 12, 4, torsoRot);
                    for (int i = 0; i < 11; i++)
                    {
                        float a = Mathf.Lerp(-75f, 75f, i / 10f) * Mathf.Deg2Rad;
                        var dir = (up * Mathf.Cos(a) + right * Mathf.Sin(a)).normalized;
                        DetailShapes.Feather(body.Of(m.Plume[i % 2 == 0 ? 0 : 3]), chest - fwd * 0.16f * s + dir * 0.25f * s, dir, fwd, (1.0f - Mathf.Abs(a) * 0.18f) * s, 0.18f * s, -fwd - up * 0.25f, 0.16f, 5);
                    }
                    break;
                case 2: // the man at the bar: a loose orange shirt
                    body.Of(m.Plume[4]).AddFrustum(hip - up * 0.06f * s, 0.17f * s, 0.18f * s, 0.5f * s, 10, true, true, 1f, torsoRot);
                    break;
                case 3: // the woman on the chair: a dress over the lap
                    body.Of(m.Plume[2]).AddFrustum(hip + up * 0.02f * s, 0.16f * s, 0.17f * s, 0.42f * s, 10, true, true, 1f, torsoRot);
                    var lap = (kneeR + kneeL) * 0.5f;
                    body.Of(m.Plume[2]).AddFrustum(hip, 0.2f * s, 0.17f * s, Vector3.Distance(hip, lap) + 0.05f * s, 10, true, true, 1f, Quaternion.FromToRotation(Vector3.up, (lap - hip).normalized));
                    break;
                default: // the child: a little dress
                    body.Of(m.Plume[1]).AddFrustum(hip - up * 0.3f * s, 0.26f * s, 0.15f * s, 0.75f * s, 10, true, true, 1f, torsoRot);
                    break;
            }
            body.Bake(go.transform, "Body");

            var headPivot = new GameObject("Head").transform;
            headPivot.SetParent(go.transform, false);
            headPivot.localPosition = neck;
            var hk = new KitBatch();
            var hs = hk.Of(m.Skin);
            Limb(hs, Vector3.zero, up * 0.07f * s, 0.043f * s, 0.04f * s, 6);
            var face = up * 0.17f * s + fwd * 0.01f * s;
            DetailShapes.Ellipsoid(hs, face, 0.088f * s, 0.115f * s, 9, 6);
            switch (dress)
            {
                case 0: // a white bucket hat with a coloured band
                    hk.Of(m.DrumHead).AddCylinder(face + up * 0.06f * s, 0.17f * s, 0.015f * s, 14);
                    hk.Of(m.DrumHead).AddCylinder(face + up * 0.07f * s, 0.095f * s, 0.11f * s, 12);
                    hk.Of(m.Plume[0]).AddCylinder(face + up * 0.075f * s, 0.098f * s, 0.03f * s, 12);
                    break;
                case 1: // a gold band and a crown of plumes
                    hk.Of(m.Gold).AddTorus(face + up * 0.06f * s, 0.095f * s, 0.016f * s, 12, 4, Quaternion.identity);
                    for (int i = 0; i < 7; i++)
                    {
                        float a = Mathf.Lerp(-50f, 50f, i / 6f) * Mathf.Deg2Rad;
                        var dir = (Vector3.up * Mathf.Cos(a) + right * Mathf.Sin(a)).normalized;
                        DetailShapes.Feather(hk.Of(m.Plume[i % 2 == 0 ? 0 : 1]), face + up * 0.08f * s - fwd * 0.03f * s, dir, fwd, (0.5f - Mathf.Abs(a) * 0.1f) * s, 0.1f * s, -fwd + dir * 0.3f, 0.18f, 4);
                    }
                    break;
                case 2: // a straw hat
                    hk.Of(m.Straw).AddCylinder(face + up * 0.065f * s, 0.2f * s, 0.012f * s, 16);
                    hk.Of(m.Straw).AddFrustum(face + up * 0.07f * s, 0.1f * s, 0.085f * s, 0.09f * s, 12);
                    hk.Of(m.DarkMetal).AddCylinder(face + up * 0.075f * s, 0.102f * s, 0.022f * s, 12);
                    break;
                case 3: // a head scarf
                    hk.Of(m.Plume[0]).AddFrustum(face + up * 0.02f * s, 0.098f * s, 0.07f * s, 0.12f * s, 12);
                    break;
                default: // a hair bow
                    hk.Of(m.Plume[0]).AddSphere(face + up * 0.11f * s - fwd * 0.03f * s, 0.035f * s, 8, 5);
                    hk.Of(m.Plume[0]).AddBox(face + up * 0.11f * s - fwd * 0.03f * s, new Vector3(0.16f, 0.05f, 0.02f) * s);
                    break;
            }
            hk.Bake(headPivot, "Head");

            var armPivot = new GameObject("ArmR").transform;
            armPivot.SetParent(go.transform, false);
            armPivot.localPosition = shoulderR;
            var ak = new KitBatch();
            var askin = ak.Of(m.Skin);
            var e = elbowR - shoulderR;
            var h = handR - shoulderR;
            Limb(askin, Vector3.zero, e, 0.048f * s, 0.038f * s);
            Limb(askin, e, h, 0.038f * s, 0.03f * s);
            askin.AddSphere(Vector3.zero, 0.05f * s, 6, 4);
            askin.AddSphere(e, 0.05f * s, 6, 4);
            askin.AddSphere(h, 0.04f * s, 6, 4);
            switch (dress)
            {
                case 0: // a tamborim held up, its frame and skin
                    var tq = Quaternion.FromToRotation(Vector3.up, (fwd + Vector3.up).normalized);
                    ak.Of(m.Chrome).AddCylinder(h + fwd * 0.05f * s, 0.12f * s, 0.05f * s, 14, 1f, tq);
                    ak.Of(m.DrumHead).AddCylinder(h + fwd * 0.05f * s + tq * Vector3.up * 0.05f * s, 0.115f * s, 0.004f * s, 14, 1f, tq);
                    break;
                case 1: // a gold bangle
                    ak.Of(m.Gold).AddTorus(h - (h - e).normalized * 0.06f * s, 0.04f * s, 0.012f * s, 8, 3, Quaternion.FromToRotation(Vector3.up, (h - e).normalized));
                    break;
                case 2: // the cup in the air, its drink stopped as it slopped
                    ak.Of(m.DrumHead).AddFrustum(h + Vector3.up * 0.02f * s, 0.032f * s, 0.043f * s, 0.11f * s, 10);
                    var drink = Mats.Glass(new Color(1f, 0.72f, 0.22f, 0.55f), 0.95f, new Color(0.22f, 0.12f, 0.02f));
                    for (int i = 0; i < 6; i++) ak.Of(drink).AddSphere(h + Vector3.up * (0.14f + i * 0.035f) * s + fwd * (0.02f + i * 0.03f) * s, (0.022f - i * 0.0025f) * s, 6, 4);
                    break;
                case 3: // a folded paper fan
                    var fq = Quaternion.LookRotation(fwd, Vector3.up);
                    for (int i = 0; i < 7; i++)
                        ak.Of(m.Plume[5]).AddBox(h + Quaternion.Euler(0f, 0f, -45f + i * 15f) * Vector3.up * 0.09f * s, new Vector3(0.03f, 0.18f, 0.004f) * s, fq * Quaternion.Euler(0f, 0f, -45f + i * 15f));
                    break;
            }
            ak.Bake(armPivot, "Arm");
            var fig = go.AddComponent<RioFrozenFigure>();
            fig.Setup(headPivot, armPivot);
            return fig;
        }

        static void Limb(MeshBuilder b, Vector3 a, Vector3 c, float ra, float rc, int seg = 7)
        {
            var d = c - a;
            if (d.sqrMagnitude < 1e-6f) return;
            b.AddFrustum(a, ra, rc, d.magnitude, seg, true, true, 1f, Quaternion.FromToRotation(Vector3.up, d));
        }

        // ------------------------------------------------------------------ helpers
        static void Ramp(Transform root, Vector3 a, Vector3 b, float width)
        {
            var d = b - a;
            var rot = Quaternion.LookRotation(d.normalized, Vector3.up);
            var go = new GameObject("StairRamp") { layer = Layers.Floor };
            go.transform.SetParent(root, false);
            go.transform.localPosition = (a + b) * 0.5f - rot * Vector3.up * 0.05f;
            go.transform.localRotation = rot;
            go.AddComponent<BoxCollider>().size = new Vector3(width, 0.1f, d.magnitude);
            go.AddComponent<Surface>().Kind = "tile";
        }

        static void Floor(Transform root, string name, Vector3 c, Vector3 s, string surface)
        {
            var go = new GameObject(name) { layer = Layers.Floor };
            go.transform.SetParent(root, false);
            go.transform.localPosition = c;
            go.AddComponent<BoxCollider>().size = s;
            go.AddComponent<Surface>().Kind = surface;
        }

        static GameObject Wall(Transform root, string name, Vector3 c, Vector3 s)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = c;
            go.AddComponent<BoxCollider>().size = s;
            return go;
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

        static void Text(Transform root, string text, Vector3 pos, float yaw, float size, float width, Color color)
        {
            var t = TextFactory.World(root, text, size, color, TMPro.TextAlignmentOptions.Center, false, width);
            t.transform.localPosition = pos;
            t.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
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
    /// A frozen figure of the samba circle that twitches when the drum is hit near it: its head snaps a few degrees toward
    /// whoever hit the drum (a little further each time, up to about 50 degrees), its body turns a hair, its raised arm
    /// jerks; after a quiet while it eases back, slowly, as if it hopes you did not see. World motion only.
    /// </summary>
    public class RioFrozenFigure : MonoBehaviour
    {
        Transform m_Head, m_Arm;
        Quaternion m_HeadRest, m_ArmRest, m_BodyRest;
        float m_HeadYaw, m_HeadShown, m_BodyYaw, m_BodyShown, m_Jerk, m_Last = -100f;

        /// <summary>Times this figure has twitched (tests).</summary>
        public int Twitches { get; private set; }
        public Vector3 HeadPosition => m_Head != null ? m_Head.position : transform.position + Vector3.up * 1.6f;

        public void Setup(Transform head, Transform arm)
        {
            m_Head = head;
            m_Arm = arm;
            m_HeadRest = head != null ? head.localRotation : Quaternion.identity;
            m_ArmRest = arm != null ? arm.localRotation : Quaternion.identity;
            m_BodyRest = transform.localRotation;
        }

        public void Twitch(Vector3 worldTarget)
        {
            Twitches++;
            m_Last = Time.time;
            var to = worldTarget - transform.position;
            to.y = 0f;
            var restFwd = (transform.parent != null ? transform.parent.rotation : Quaternion.identity) * m_BodyRest * Vector3.forward;
            restFwd.y = 0f;
            float angle = to.sqrMagnitude > 1e-4f && restFwd.sqrMagnitude > 1e-4f ? Vector3.SignedAngle(restFwd, to, Vector3.up) : 0f;
            m_HeadYaw = Mathf.MoveTowards(m_HeadYaw, Mathf.Clamp(angle, -50f, 50f), 9f);
            m_BodyYaw = Mathf.MoveTowards(m_BodyYaw, Mathf.Clamp(angle * 0.12f, -6f, 6f), 1.2f);
            m_Jerk = 1f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (Time.time - m_Last > 6f)
            {
                m_HeadYaw = Mathf.MoveTowards(m_HeadYaw, 0f, dt * 1.6f);
                m_BodyYaw = Mathf.MoveTowards(m_BodyYaw, 0f, dt * 0.3f);
            }
            if (m_HeadShown == m_HeadYaw && m_BodyShown == m_BodyYaw && m_Jerk <= 0f) return;
            m_HeadShown = Mathf.MoveTowards(m_HeadShown, m_HeadYaw, dt * 140f);
            m_BodyShown = Mathf.MoveTowards(m_BodyShown, m_BodyYaw, dt * 30f);
            m_Jerk = Mathf.MoveTowards(m_Jerk, 0f, dt * 3.5f);
            transform.localRotation = m_BodyRest * Quaternion.Euler(0f, m_BodyShown, 0f);
            if (m_Head != null) m_Head.localRotation = Quaternion.Euler(0f, m_HeadShown - m_BodyShown, 0f) * m_HeadRest;
            if (m_Arm != null) { float j = m_Jerk * m_Jerk; m_Arm.localRotation = m_ArmRest * Quaternion.Euler(-7f * j, 0f, 4f * j); }
        }
    }

    /// <summary>
    /// The samba circle's surdo (Rio's set piece): hit it and it booms, its skin dips, the mallet hanging on the stand
    /// jumps, a puff of confetti rises off it and hangs in the air, and the frozen figure nearest you twitches toward you.
    /// No fail state; it never touches the stamp plate's beat.
    /// </summary>
    public class RioDrum : Interactable
    {
        Transform m_Skin, m_Mallet;
        Vector3 m_SkinRest;
        Quaternion m_MalletRest;
        ParticleSystem m_Puff;
        RioFrozenFigure[] m_Figures;
        float m_Hit;

        /// <summary>Times the drum has been hit (tests).</summary>
        public int Hits { get; private set; }

        public void Setup(Transform skin, Transform mallet, ParticleSystem puff, RioFrozenFigure[] figures)
        {
            m_Skin = skin;
            m_SkinRest = skin.localPosition;
            m_Mallet = mallet;
            m_MalletRest = mallet != null ? mallet.localRotation : Quaternion.identity;
            m_Puff = puff;
            m_Figures = figures;
            Prompt = "Hit the drum";
            MaxDistance = 2.4f;
            MarkerPoint = skin;
        }

        public override bool CanUse(Interactor who) => base.CanUse(who) && m_Skin != null;

        public override void Use(Interactor who)
        {
            if (!CanUse(who)) return;
            Hits++;
            var at = m_Skin.position;
            AudioDirector.I?.Play("drum_hit", at, 0.9f, Random.Range(1.04f, 1.12f));
            AudioDirector.Haptic(who != null ? who.Side : HandSide.Desktop, 0.5f, 0.08f);
            m_Hit = 1f;
            if (m_Puff != null) m_Puff.Emit(30);
            var rig = Game.Rig;
            var from = rig != null ? rig.Feet : at;
            RioFrozenFigure near = null;
            float best = float.MaxValue;
            if (m_Figures != null)
                foreach (var f in m_Figures)
                {
                    if (f == null) continue;
                    float d = (f.transform.position - from).sqrMagnitude;
                    if (d < best) { best = d; near = f; }
                }
            if (near != null)
            {
                near.Twitch(rig != null ? rig.Head.position : at);
                AudioDirector.I?.Play("pass_creak_" + Random.Range(1, 4), near.HeadPosition, 0.2f, Random.Range(1.2f, 1.4f));
            }
            base.Use(who);
        }

        void Update()
        {
            if (m_Hit <= 0f || m_Skin == null) return;
            m_Hit = Mathf.MoveTowards(m_Hit, 0f, Time.deltaTime * 5f);
            float k = Mathf.Sin(m_Hit * Mathf.PI);
            m_Skin.localPosition = m_SkinRest - Vector3.up * 0.012f * k;
            if (m_Mallet != null) m_Mallet.localRotation = m_MalletRest * Quaternion.Euler(0f, 0f, 14f * k);
        }
    }
}

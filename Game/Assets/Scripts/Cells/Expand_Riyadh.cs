using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Riyadh's expansion pass (playtest: "expand the country maps ... more interactive animative and cool"). The plaza is
    /// closed in by its houses, walls and low east parapet, so the new side area opens straight off the way out: step
    /// through the north gatehouse and a Najdi coffee court stands against the back of the plaza's north wall, its painted
    /// doors open on your left. Mud-plaster walls with triangle vents and stepped merlons, a spice stall under a moving
    /// awning, baskets of dates and a carpet pile, carpets hung to air along the east wall, a date palm in a ring planter
    /// with pigeons at its foot, a stair up to a roof terrace that looks over the walls to the tower, and at the far end a
    /// coffee corner: cushions under a shade cloth, a coal hearth with a row of dallahs, and one you can lift to pour
    /// (RiyadhDallahPour): steam, a thin stream into the cup and a drift of cardamom-coloured motes. Doves on the plaza
    /// and three postcards (plaza, coffee corner, terrace). Its lights stay north of z 15, out of reach of the plaza's
    /// meshes (the plaza keeps to seven lights), and it keeps clear of the walk from the gatehouse to the stamp
    /// (Landmarks.MarkReachable re-checks it), the seams, the arches and the panel.
    /// </summary>
    public static class Expand_Riyadh
    {
        // The court, in cell coordinates: the city ground outside is at y -0.35, the court's floor 2 cm above it. Its south
        // side is the plaza's north wall (z 12.6-13.2); its own walls are 0.6 m thick and 3.2 m tall.
        const float Ground = -0.35f, Floor = -0.33f, WallH = 3.2f;
        const float XW = -7.4f, XE = 2f, ZS = 13.2f, ZN = 31.7f;   // wall centre lines (west, east, north) and the south face
        const float DoorZ0 = 15.4f, DoorZ1 = 16.9f, DoorTop = 2.25f;
        const float TerraceY = 2.25f;
        static readonly Color Warm = new Color(1f, 0.74f, 0.48f);

        sealed class CourtMats
        {
            public readonly Material Mud = Mats.Lit(new Color(0.88f, 0.71f, 0.56f), 0.15f, 0f, "plaster_warm");
            public readonly Material MudDark = Mats.Lit(new Color(0.71f, 0.56f, 0.44f), 0.15f, 0f, "plaster_warm");
            public readonly Material Gypsum = Mats.Lit(new Color(1f, 0.98f, 0.95f), 0.2f, 0f, "plaster_warm");
            public readonly Material Trim = Mats.Lit(new Color(0.95f, 0.85f, 0.72f), 0.2f, 0f, "sandstone_blocks");
            public readonly Material Paving = Mats.Lit(new Color(0.8f, 0.7f, 0.58f), 0.3f, 0f, "plaza_stone");
            public readonly Material Door = Mats.Lit(new Color(0.9f, 0.88f, 0.86f), 0.35f, 0f, "wood_dark");
            public readonly Material PalmWood = Mats.Lit(new Color(1f, 0.95f, 0.9f), 0.3f, 0f, "wood_dark");
            public readonly Material Arish = Mats.Lit(new Color(0.85f, 0.75f, 0.55f), 0.2f, 0f, "wood_oak_floor");
            public readonly Material Dark = Mats.Lit(new Color(0.05f, 0.035f, 0.03f), 0.2f);
            public readonly Material GlowTri = Mats.Emissive(new Color(0.6f, 0.4f, 0.22f), new Color(0.95f, 0.62f, 0.3f));
            public readonly Material CushionRed = Mats.Lit(new Color(0.8f, 0.2f, 0.2f), 0.2f, 0f, "fabric_linen");
            public readonly Material CushionOchre = Mats.Lit(new Color(1f, 0.95f, 0.7f), 0.2f, 0f, "fabric_linen");
            public readonly Material Brass = Mats.Lit(new Color(0.78f, 0.6f, 0.3f), 0.7f, 0.9f);
            public readonly Material Iron = Mats.Lit(new Color(0.16f, 0.15f, 0.14f), 0.45f, 0.7f);
            public readonly Material LampGlow = Mats.Emissive(new Color(1f, 0.85f, 0.6f), new Color(1.25f, 0.95f, 0.6f));
            public readonly Material Leaf = Mats.Lit(new Color(0.9f, 0.95f, 0.85f), 0.2f, 0f, "grass_short");
            public readonly Material Sail = Mats.Lit(new Color(1f, 1f, 0.97f), 0.25f, 0f, "fabric_linen");
            public readonly Material Awning = Mats.Lit(new Color(0.95f, 0.72f, 0.4f), 0.25f, 0f, "fabric_linen");
            public readonly Material PaintWhite = Mats.Lit(new Color(0.92f, 0.9f, 0.84f), 0.55f);
            public readonly Material[] Paint =
            {
                Mats.Lit(new Color(0.62f, 0.14f, 0.1f), 0.3f), Mats.Lit(new Color(0.14f, 0.42f, 0.3f), 0.3f),
                Mats.Lit(new Color(0.88f, 0.66f, 0.22f), 0.3f), Mats.Lit(new Color(0.16f, 0.28f, 0.55f), 0.3f),
            };
            public readonly Material[] Carpets =
            {
                Mats.Lit(new Color(0.95f, 0.55f, 0.5f), 0.15f, 0f, "carpet_rug"), Mats.Lit(new Color(0.6f, 0.68f, 0.95f), 0.15f, 0f, "carpet_rug"),
                Mats.Lit(new Color(1f, 0.86f, 0.6f), 0.15f, 0f, "carpet_rug"), Mats.Lit(Color.white, 0.15f, 0f, "carpet_rug"),
            };
            public readonly Material[] Spices =
            {
                Mats.Lit(new Color(0.72f, 0.18f, 0.08f), 0.1f), Mats.Lit(new Color(0.92f, 0.66f, 0.12f), 0.1f), Mats.Lit(new Color(0.5f, 0.6f, 0.32f), 0.1f),
                Mats.Lit(new Color(0.45f, 0.12f, 0.15f), 0.1f), Mats.Lit(new Color(0.55f, 0.38f, 0.2f), 0.1f), Mats.Lit(new Color(0.9f, 0.85f, 0.7f), 0.1f),
                Mats.Lit(new Color(0.18f, 0.14f, 0.1f), 0.1f), Mats.Lit(new Color(0.78f, 0.42f, 0.48f), 0.1f),
            };
            public readonly Material Burlap = Mats.Lit(new Color(0.85f, 0.74f, 0.58f), 0.15f, 0f, "fabric_linen");
            public readonly Material Basket = Mats.Lit(new Color(0.9f, 0.76f, 0.5f), 0.2f, 0f, "wood_oak_floor");
            public readonly Material Dates = Mats.Lit(new Color(0.42f, 0.2f, 0.08f), 0.55f);
            public readonly Material Clay = Mats.Lit(new Color(0.85f, 0.8f, 0.78f), 0.25f, 0f, "plaster_terracotta");
            public readonly Material Ash = Mats.Lit(new Color(0.32f, 0.3f, 0.28f), 0.15f);
            public readonly Material Coal = Mats.Emissive(new Color(0.25f, 0.06f, 0.02f), new Color(1.5f, 0.42f, 0.1f));
            public readonly Material Coffee = Mats.Lit(new Color(0.6f, 0.42f, 0.2f), 0.8f);
            public readonly Material Trunk = Mats.Lit(new Color(0.72f, 0.72f, 0.75f), 0.2f, 0f, "wood_oak_floor");
            public readonly Material Frond = Mats.Lit(new Color(0.62f, 0.72f, 0.38f), 0.25f, 0f, "fabric_linen");
            public readonly Material DateCluster = Mats.Lit(new Color(0.62f, 0.3f, 0.1f), 0.5f);
        }

        /// <summary>A wall face: a = along it, v = up, w = out from it. Boxes turn with it.</summary>
        readonly struct Face
        {
            public readonly Vector3 O, Out, Right;
            public readonly Quaternion Rot;
            public Face(Vector3 origin, Vector3 outward)
            {
                O = origin;
                Out = outward.normalized;
                Right = Vector3.Cross(Vector3.up, Out);
                Rot = Quaternion.LookRotation(Out, Vector3.up);
            }
            public Vector3 P(float a, float v, float w) => O + Right * a + Vector3.up * v + Out * w;
            public void Box(KitBatch k, Material m, float a0, float a1, float v0, float v1, float w0, float w1, float uv = 1f)
                => k.Box(m, P((a0 + a1) * 0.5f, (v0 + v1) * 0.5f, (w0 + w1) * 0.5f), new Vector3(Mathf.Abs(a1 - a0), Mathf.Abs(v1 - v0), Mathf.Abs(w1 - w0)), Rot, uv);
        }

        public static void Build(RiyadhCell cell, Transform parent)
        {
            var root = new GameObject("Expand_Riyadh").transform;
            root.SetParent(cell.transform, false);
            var m = new CourtMats();

            // Doves on the plaza's west side, in front of the arches (nothing here should draw anyone through an arch before
            // the panel is set): they burst up and wheel over the arches when you come near, and settle once it is quiet.
            AmbientBirds.Create(root, new Vector3(-4.3f, 0f, 2.4f), 1f, 7, AmbientBirds.Kind.Dove);
            // A postcard in the shade of the sail by the south-west bench, off the walk from the arrival to the panel.
            Postcard.Create(root, CountryId.SaudiArabia, 2, new Vector3(-6.45f, 1.1f, -1.2f),
                "Sat in this shade until the sun went down. The shade stayed where it was.");

            var k = new KitBatch();
            var soft = new KitBatch(); // cloth, cushions and foliage: no shadows
            Court(root, k, m);
            Stalls(root, k, soft, m);
            Middle(root, k, soft, m);
            Terrace(root, k, soft, m);
            CoffeeCorner(root, k, soft, m);
            k.Bake(root, "CoffeeCourt");
            soft.Bake(root, "CoffeeCourtSoft", false);

            Postcard.Create(root, CountryId.SaudiArabia, 1, new Vector3(-3f, -0.08f + 1.1f, 30f),
                "They poured me coffee here at dusk. When I looked up, the cushions were all empty.");
            Postcard.Create(root, CountryId.SaudiArabia, 3, new Vector3(-6.15f, TerraceY + 1.1f, 28.9f),
                "From the roof the tower looks near enough to touch. It was always one more street away.");

            // The art review's viewpoint: just inside the doors, looking across the court to the palm, the stair and the
            // coffee corner at the far end.
            var shot = new GameObject("ShotPoint").transform;
            shot.SetParent(root, false);
            shot.localPosition = new Vector3(0.85f, Floor, 16.35f);
            shot.localRotation = Quaternion.Euler(0f, -18f, 0f);
        }

        // ================================================================== walls, floor, doors
        static void Court(Transform root, KitBatch k, CourtMats m)
        {
            MeshFactory.Floor("CourtFloor", root, new Vector3((XW + 0.3f + XE - 0.3f) * 0.5f, Floor, (ZS + ZN - 0.3f) * 0.5f),
                new Vector2(XE - XW - 0.6f, ZN - 0.3f - ZS), m.Paving, "stone", 0.2f, 1.6f);
            MeshFactory.Floor("CourtThreshold", root, new Vector3(XE, Floor + 0.01f, (DoorZ0 + DoorZ1) * 0.5f), new Vector2(0.62f, DoorZ1 - DoorZ0), m.Trim, "stone", 0.2f, 1.5f);

            // West and north walls (inner faces dressed; the north's outer face too, it is seen from the city), and the east
            // wall either side of the doorway, dressed on both faces.
            NajdiWall(k, m, new Vector3(XW, Ground, ZS), new Vector3(XW, Ground, ZN + 0.3f), WallH, 0.6f, true, false, false, 61);
            NajdiWall(k, m, new Vector3(XE + 0.3f, Ground, ZN), new Vector3(XW - 0.3f, Ground, ZN), WallH, 0.6f, true, true, true, 63);
            NajdiWall(k, m, new Vector3(XE, Ground, ZS), new Vector3(XE, Ground, DoorZ0), WallH, 0.6f, true, true, true, 65);
            NajdiWall(k, m, new Vector3(XE, Ground, DoorZ1), new Vector3(XE, Ground, ZN + 0.3f), WallH, 0.6f, true, true, true, 67);
            float lintelH = Ground + WallH - DoorTop;
            k.Box(m.Mud, new Vector3(XE, DoorTop + lintelH * 0.5f, (DoorZ0 + DoorZ1) * 0.5f), new Vector3(0.6f, lintelH, DoorZ1 - DoorZ0), 2.2f);
            for (int i = 0; i < 2; i++) Merlon(k, m.Mud, new Vector3(XE, Ground + WallH, DoorZ0 + 0.42f + i * 0.66f), Quaternion.LookRotation(Vector3.right), 0.42f, 0.36f, 0.17f);
            var top = Ground + WallH + 0.6f;
            Col(root, "CourtWallW", new Vector3(XW, (Ground + top) * 0.5f, (ZS + ZN + 0.3f) * 0.5f), new Vector3(0.6f, top - Ground, ZN + 0.3f - ZS));
            Col(root, "CourtWallN", new Vector3((XW - 0.3f + XE + 0.3f) * 0.5f, (Ground + top) * 0.5f, ZN), new Vector3(XE - XW + 0.6f, top - Ground, 0.6f));
            Col(root, "CourtWallE", new Vector3(XE, (Ground + top) * 0.5f, (ZS + DoorZ0) * 0.5f), new Vector3(0.6f, top - Ground, DoorZ0 - ZS));
            Col(root, "CourtWallE", new Vector3(XE, (Ground + top) * 0.5f, (DoorZ1 + ZN + 0.3f) * 0.5f), new Vector3(0.6f, top - Ground, ZN + 0.3f - DoorZ1));
            Col(root, "CourtLintel", new Vector3(XE, (DoorTop + top) * 0.5f, (DoorZ0 + DoorZ1) * 0.5f), new Vector3(0.6f, top - DoorTop, DoorZ1 - DoorZ0));

            // The doorway: gypsum surrounds both sides, a triangle frieze over it on the street side, the two painted leaves
            // standing open against the inside of the wall, and a brass lantern on a bracket above.
            foreach (float s in new[] { 1f, -1f })
            {
                float x = XE + s * 0.31f;
                foreach (float z in new[] { DoorZ0 - 0.09f, DoorZ1 + 0.09f }) k.Box(m.Gypsum, new Vector3(x, (Floor + DoorTop) * 0.5f, z), new Vector3(0.04f, DoorTop - Floor, 0.18f), 2f);
                k.Box(m.Gypsum, new Vector3(x, DoorTop + 0.08f, (DoorZ0 + DoorZ1) * 0.5f), new Vector3(0.04f, 0.16f, DoorZ1 - DoorZ0 + 0.36f), 2f);
            }
            for (int i = 0; i < 5; i++) TriFace(k.Of(i == 2 ? m.GlowTri : m.Dark), new Vector3(XE + 0.306f, DoorTop + 0.3f, DoorZ0 + 0.15f + i * 0.3f), Vector3.right, 0.2f, 0.22f);
            Leaf(k, m, new Vector3(XE - 0.36f, Floor, DoorZ0), Vector3.back, Vector3.left, 0.74f, 2.2f, 1);
            Leaf(k, m, new Vector3(XE - 0.36f, Floor, DoorZ1), Vector3.forward, Vector3.left, 0.74f, 2.2f, 3);
            Rod(k, m.Iron, new Vector3(XE + 0.3f, DoorTop + 0.55f, (DoorZ0 + DoorZ1) * 0.5f), new Vector3(XE + 0.62f, DoorTop + 0.55f, (DoorZ0 + DoorZ1) * 0.5f), 0.03f);
            var lamp = BrassLantern(root, "DoorLantern", new Vector3(XE + 0.6f, DoorTop + 0.53f, (DoorZ0 + DoorZ1) * 0.5f), 0.1f, 0.8f, m);
            WindSway.Add(lamp, 4f, 2.6f);

            // A date palm outside by the doors, so the court reads from the gatehouse (clear of the walk north).
            var palm = MonumentKit.Palm(root, new Vector3(3.1f, Ground, 21f), 7f, 61, m.Trunk, m.Frond, null, m.DateCluster);
            SwayCrown(palm, 7f, 2f, 3.6f);
        }

        /// <summary>
        /// A Najdi mud wall from a to b (ground points), `thick` deep and h tall: a darker plinth, the plastered body, a
        /// gypsum course and a row of triangle vents (a few lit) on each dressed face, palm-trunk spouts on the right-hand
        /// face (the outer one) when `spouts`, and stepped merlons along the top. "Right" is the side to the right walking
        /// from a to b.
        /// </summary>
        static void NajdiWall(KitBatch k, CourtMats m, Vector3 a, Vector3 b, float h, float thick, bool dressRight, bool dressLeft, bool spouts, int seed)
        {
            var d = b - a;
            d.y = 0f;
            float len = d.magnitude;
            var dir = d / len;
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            var right = rot * Vector3.right;
            var across = Quaternion.LookRotation(right, Vector3.up); // merlons and vents run along the wall
            var mid = (a + b) * 0.5f;
            var rnd = new System.Random(seed);
            k.Box(m.Mud, mid + Vector3.up * (h * 0.5f), new Vector3(thick, h, len), rot, 2.2f);
            k.Box(m.MudDark, mid + Vector3.up * 0.3f, new Vector3(thick + 0.08f, 0.6f, len), rot, 2f);
            foreach (float side in new[] { 1f, -1f })
            {
                if (side > 0f ? !dressRight : !dressLeft) continue;
                var n = right * side;
                k.Box(m.Gypsum, mid + n * (thick * 0.5f + 0.02f) + Vector3.up * (h - 0.66f), new Vector3(0.04f, 0.12f, len), rot, 2f);
                for (float s = 0.35f; s < len - 0.2f; s += 0.45f)
                    TriFace(k.Of(rnd.NextDouble() < 0.16 ? m.GlowTri : m.Dark), a + dir * s + n * (thick * 0.5f + 0.006f) + Vector3.up * (h - 1.02f), n, 0.2f, 0.24f);
                if (side > 0f && spouts)
                    for (float s = 1.3f; s < len - 0.6f; s += 2.7f + (float)rnd.NextDouble() * 0.6f)
                    {
                        var root = a + dir * s + Vector3.up * (h - 0.85f);
                        Rod(k, m.PalmWood, root + n * (thick * 0.5f - 0.1f), root + n * (thick * 0.5f + 0.6f) + Vector3.down * 0.09f, 0.13f);
                        k.Box(m.MudDark, a + dir * s + n * (thick * 0.5f + 0.003f) + Vector3.up * (h - 1.62f), new Vector3(0.006f, 1.4f, 0.14f), rot);
                    }
            }
            for (float s = 0.31f; s < len - 0.2f; s += 0.62f) Merlon(k, m.Mud, a + dir * s + Vector3.up * h, across, 0.42f, thick * 0.6f, 0.17f);
        }

        /// <summary>A painted Najdi door leaf (planks, white lines between bands of coloured triangles, iron studs, a brass
        /// ring), its painted face toward `face`, hung at `hinge` (ground) and reaching `along` for `width`.</summary>
        static void Leaf(KitBatch k, CourtMats m, Vector3 hinge, Vector3 along, Vector3 face, float width, float height, int colour)
        {
            var rot = Quaternion.LookRotation(face, Vector3.up);
            k.Box(m.Door, hinge + along * (width * 0.5f) + Vector3.up * (height * 0.5f), new Vector3(width, height, 0.06f), rot, 1.2f);
            foreach (float at in new[] { 0.22f, 0.5f, 0.78f })
            {
                var row = hinge + face * 0.031f + Vector3.up * (height * at);
                k.Box(m.PaintWhite, row + along * (width * 0.5f) + Vector3.down * 0.004f, new Vector3(width - 0.08f, 0.016f, 0.004f), rot);
                k.Box(m.PaintWhite, row + along * (width * 0.5f) + Vector3.up * 0.178f, new Vector3(width - 0.08f, 0.016f, 0.004f), rot);
                const int n = 5;
                float step = (width - 0.1f) / n;
                for (int i = 0; i < n; i++)
                {
                    var p = row + along * (0.05f + (i + 0.5f) * step) + face * 0.003f + Vector3.up * 0.012f;
                    TriFace(k.Of(m.Paint[(colour + i) % m.Paint.Length]), p, face, step * 0.9f, 0.14f);
                    k.Box(m.Iron, p + Vector3.down * 0.037f, new Vector3(0.024f, 0.024f, 0.014f), rot);
                }
                colour++;
            }
            k.Of(m.Brass).AddTorus(hinge + along * (width - 0.14f) + Vector3.up * (height * 0.42f - 0.05f) + face * 0.04f, 0.055f, 0.008f, 12, 4, Quaternion.FromToRotation(Vector3.up, face));
        }

        // ================================================================== the south stalls
        /// <summary>
        /// Against the back of the plaza's wall: a spice stall (sacks heaped with colour on a plank platform, brass scales)
        /// under an ochre awning that lifts in the wind, a mat of date baskets, and a pile of folded carpets with a rolled one.
        /// No lights here: this end is within reach of the plaza's meshes, so its brass lantern only glows.
        /// </summary>
        static void Stalls(Transform root, KitBatch k, KitBatch soft, CourtMats m)
        {
            // The spice platform.
            const float x0 = -6.4f, x1 = -3f, z0 = 13.3f, z1 = 14.4f, deck = 0.12f;
            k.Box(m.PalmWood, new Vector3((x0 + x1) * 0.5f, (Floor + deck) * 0.5f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, deck - Floor, z1 - z0), 1.2f);
            k.Box(m.Door, new Vector3((x0 + x1) * 0.5f, (Floor + deck) * 0.5f, z1 + 0.01f), new Vector3(x1 - x0, deck - Floor - 0.04f, 0.02f), 1.2f);
            k.Box(m.PalmWood, new Vector3((x0 + x1) * 0.5f, deck + 0.08f, 13.62f), new Vector3(x1 - x0 - 0.1f, 0.16f, 0.55f), 1.2f);
            Col(root, "SpiceStall", new Vector3((x0 + x1) * 0.5f, (Ground + 0.6f) * 0.5f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.6f - Ground, z1 - z0));
            var rnd = new System.Random(17);
            int s = 0;
            foreach (var (z, y) in new[] { (13.62f, deck + 0.16f), (14.08f, deck) })
                for (float x = -5.8f; x < x1 - 0.2f; x += 0.8f, s++)
                    Sack(k, m, new Vector3(x + R(rnd, -0.04f, 0.04f), y, z), R(rnd, 0.17f, 0.2f), R(rnd, 0.26f, 0.32f), m.Spices[s % m.Spices.Length]);
            // A brass balance at the west end.
            var bal = new Vector3(-6.25f, deck, 14.08f);
            k.Of(m.Brass).AddCylinder(bal, 0.05f, 0.02f, 10);
            k.Of(m.Brass).AddCylinder(bal, 0.008f, 0.32f, 6);
            Rod(k, m.Brass, bal + new Vector3(-0.15f, 0.32f, 0f), bal + new Vector3(0.15f, 0.32f, 0f), 0.008f);
            foreach (float x in new[] { -0.15f, 0.15f })
            {
                Rod(k, m.Brass, bal + new Vector3(x, 0.32f, 0f), bal + new Vector3(x, 0.16f, 0f), 0.003f);
                k.Of(m.Brass).AddLathe(bal + new Vector3(x, 0.15f, 0f), new[] { new Vector2(0f, 0f), new Vector2(0.05f, 0.005f), new Vector2(0.07f, 0.025f) }, 10);
            }
            // Awning poles and beam, the cloth fixed along the wall and resting on the beam.
            foreach (float x in new[] { x0 - 0.15f, x1 + 0.05f }) k.Of(m.PalmWood).AddCylinder(new Vector3(x, Floor, 14.95f), 0.06f, 2.36f, 8);
            k.Box(m.PalmWood, new Vector3((x0 + x1) * 0.5f - 0.05f, 1.99f, 14.95f), new Vector3(x1 - x0 + 0.4f, 0.1f, 0.1f));
            Awning(root, "SpiceAwning", new Vector3(x1 + 0.15f, 2.3f, ZS + 0.05f), Vector3.left, Vector3.forward, x1 - x0 + 0.3f, 1.7f, 10f, m.Awning, 0.07f);
            foreach (float x in new[] { x0 - 0.15f, x1 + 0.05f })
            {
                var col = new GameObject("AwningPole"); // thin colliders so nobody walks through the poles
                col.transform.SetParent(root, false);
                col.transform.localPosition = new Vector3(x, 0.85f, 14.95f);
                var cap = col.AddComponent<CapsuleCollider>();
                cap.radius = 0.08f;
                cap.height = 2.4f;
            }
            var lamp = BrassLantern(root, "StallLantern", new Vector3(-4.7f, 1.94f, 14.95f), 0.02f, 0.62f, m); // bottom ~1.85 m up
            WindSway.Add(lamp, 4f, 2.4f);

            // Date baskets on a mat.
            soft.Box(m.Carpets[2], new Vector3(-1.7f, Floor + 0.006f, 13.78f), new Vector3(2.1f, 0.012f, 0.9f), 1.2f);
            foreach (float x in new[] { -2.35f, -1.7f, -1.05f })
            {
                var c = new Vector3(x, Floor + 0.012f, 13.78f);
                k.Of(m.Basket).AddLathe(c, new[] { new Vector2(0f, 0f), new Vector2(0.17f, 0f), new Vector2(0.21f, 0.18f), new Vector2(0.23f, 0.24f),
                    new Vector2(0.21f, 0.245f), new Vector2(0.19f, 0.2f), new Vector2(0.15f, 0.06f), new Vector2(0f, 0.04f) }, 14, 1.2f);
                k.Of(m.Dates).AddLathe(c + Vector3.up * 0.19f, new[] { new Vector2(0.2f, 0f), new Vector2(0.15f, 0.06f), new Vector2(0.07f, 0.09f), new Vector2(0f, 0.1f) }, 12);
                for (int i = 0; i < 7; i++)
                {
                    float a = i * 51f * Mathf.Deg2Rad;
                    k.Of(m.Dates).AddSphere(c + new Vector3(Mathf.Cos(a) * 0.1f, 0.27f + (i % 2) * 0.012f, Mathf.Sin(a) * 0.1f), 0.022f, 6, 4);
                }
            }
            Col(root, "DateBaskets", new Vector3(-1.7f, (Ground + 0.4f) * 0.5f, 13.78f), new Vector3(1.9f, 0.4f - Ground, 0.55f));

            // Folded carpets piled in the corner by the doors, a rolled one leaning on the wall.
            for (int i = 0; i < 6; i++)
                soft.Box(m.Carpets[i % m.Carpets.Length], new Vector3(0.72f + R(rnd, -0.04f, 0.04f), Floor + 0.03f + i * 0.06f, 13.66f), new Vector3(1f, 0.058f, 0.7f), Quaternion.Euler(0f, R(rnd, -6f, 6f), 0f), 1.2f);
            soft.Of(m.Carpets[1]).AddCylinder(new Vector3(-0.15f, Floor, 13.64f), 0.11f, 1.5f, 10, 1f, Quaternion.Euler(-12f, 0f, 0f)); // leaning back on the wall
            Col(root, "CarpetPile", new Vector3(0.72f, 0f, 13.66f), new Vector3(1.05f, 0.7f, 0.75f));
        }

        /// <summary>A spice sack, its mouth rolled down round a heap of colour.</summary>
        static void Sack(KitBatch k, CourtMats m, Vector3 at, float r, float h, Material spice)
        {
            k.Of(m.Burlap).AddLathe(at, new[] { new Vector2(0f, 0f), new Vector2(0.75f * r, 0f), new Vector2(0.95f * r, 0.18f * h), new Vector2(r, 0.55f * h),
                new Vector2(0.95f * r, 0.8f * h), new Vector2(1.06f * r, 0.86f * h), new Vector2(1.1f * r, 0.95f * h), new Vector2(1.02f * r, h) }, 12, 1.2f);
            k.Of(spice).AddLathe(at, new[] { new Vector2(1.02f * r, 0.97f * h), new Vector2(0.72f * r, 1.1f * h), new Vector2(0.36f * r, 1.18f * h), new Vector2(0f, 1.21f * h) }, 12);
        }

        // ================================================================== the middle of the court
        /// <summary>A date palm in a raised ring planter, pigeons at its foot, carpets hung to air on a rail along the east
        /// wall, and the court's first lamp to light.</summary>
        static void Middle(Transform root, KitBatch k, KitBatch soft, CourtMats m)
        {
            var c = new Vector3(-1.5f, Floor, 22.6f);
            k.Of(m.Mud).AddLathe(c, new[] { new Vector2(0.84f, 0f), new Vector2(0.84f, 0.42f), new Vector2(0.8f, 0.46f), new Vector2(0.66f, 0.46f), new Vector2(0.62f, 0.4f), new Vector2(0f, 0.38f) }, 24, 2f);
            k.Of(m.Gypsum).AddTorus(c + Vector3.up * 0.46f, 0.73f, 0.035f, 24, 4, Quaternion.identity);
            var cyl = new GameObject("Planter");
            cyl.transform.SetParent(root, false);
            cyl.transform.localPosition = c + Vector3.up * 0.3f;
            var cc = cyl.AddComponent<CapsuleCollider>();
            cc.radius = 0.86f;
            cc.height = 0.6f;
            var palm = MonumentKit.Palm(root, c + Vector3.up * 0.38f, 6.2f, 23, m.Trunk, m.Frond, null, m.DateCluster);
            SwayCrown(palm, 6.2f, 2.4f, 3.2f);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f * Mathf.Deg2Rad;
                Blob(soft.Of(m.Leaf), c + new Vector3(Mathf.Cos(a) * 0.38f, 0.44f, Mathf.Sin(a) * 0.38f), 0.2f, 0.12f, 8);
            }

            AmbientBirds.Create(root, new Vector3(-3f, Floor, 20.8f), 0.9f, 7, AmbientBirds.Kind.Pigeon);

            // Carpets on a rail 0.35 m off the east wall (too tight to walk behind).
            const float railX = XE - 0.64f, railY = 2.46f;
            Rod(k, m.PalmWood, new Vector3(railX, railY, 19.5f), new Vector3(railX, railY, 25.2f), 0.05f);
            foreach (float z in new[] { 19.7f, 22.35f, 25f }) Rod(k, m.Iron, new Vector3(XE - 0.3f, railY, z), new Vector3(railX, railY, z), 0.025f);
            float[] starts = { 19.72f, 21.6f, 23.48f };
            for (int i = 0; i < starts.Length; i++)
                Hang(root, "AiringCarpet", new Vector3(railX, railY - 0.03f, starts[i]), Vector3.forward, 1.5f, 1.75f, m.Carpets[i], 0.05f);

            SwitchLamp.Create(root, new Vector3(-4.3f, Floor, 20.3f), Warm, false, 2f, 2.2f);
        }

        // ================================================================== stair, room and roof terrace
        /// <summary>
        /// Along the west wall: a mud stair (a solid flight with a sloping balustrade) up to the roof of a small room, whose
        /// terrace has a merlon parapet on its open sides and looks over the walls to the tower and back over the plaza.
        /// </summary>
        static void Terrace(Transform root, KitBatch k, KitBatch soft, CourtMats m)
        {
            const float sx0 = -7.1f, sx1 = -6f, sz0 = 19.6f, sz1 = 23.6f;
            const int steps = 12;
            float rise = (TerraceY - Floor) / steps, going = (sz1 - sz0) / steps;
            for (int i = 0; i < steps; i++)
            {
                float zA = sz0 + i * going, top = Floor + (i + 1) * rise;
                k.Box(m.Mud, new Vector3((sx0 + sx1) * 0.5f, (Ground + top) * 0.5f, zA + going * 0.5f), new Vector3(sx1 - sx0, top - Ground, going), 2f);
                k.Box(m.Gypsum, new Vector3((sx0 + sx1) * 0.5f, top + 0.004f, zA + 0.03f), new Vector3(sx1 - sx0, 0.008f, 0.06f), 2f);
            }
            // The walking surface: a ramp half a step above the step corners, meeting the court floor and the terrace.
            float zLow = sz0 - rise * 0.5f / (rise / going), zHigh = zLow + (TerraceY - Floor) / (rise / going);
            float run = zHigh - zLow, lift = TerraceY - Floor, len = Mathf.Sqrt(run * run + lift * lift), ang = Mathf.Atan2(lift, run) * Mathf.Rad2Deg;
            var normal = new Vector3(0f, run / len, -lift / len);
            var mid = new Vector3((sx0 + sx1) * 0.5f, (Floor + TerraceY) * 0.5f, (zLow + zHigh) * 0.5f) - normal * 0.1f;
            Col(root, "TerraceStair", mid, new Vector3(sx1 - sx0, 0.2f, len), Layers.Floor, Quaternion.Euler(-ang, 0f, 0f)).gameObject.AddComponent<Surface>().Kind = "stone";
            // The balustrade on the stair's open side: a solid mud parapet whose top follows the flight, capped with gypsum.
            const float bx = -5.89f, bw = 0.22f;
            var poly = new List<Vector2> { new Vector2(sz0 - 0.3f, Ground), new Vector2(sz1, Ground), new Vector2(sz1, TerraceY + 0.9f), new Vector2(zHigh, TerraceY + 0.9f), new Vector2(zLow, Floor + 0.9f), new Vector2(sz0 - 0.3f, Floor + 0.9f) };
            k.Of(m.Mud).AddExtrudedXY(poly, bw, new Vector3(bx, 0f, 0f), Quaternion.Euler(0f, -90f, 0f), 2f);
            k.Box(m.Gypsum, new Vector3(bx, (Floor + TerraceY) * 0.5f + 0.9f + 0.02f, (zLow + zHigh) * 0.5f), new Vector3(bw + 0.04f, 0.04f, len), Quaternion.Euler(-ang, 0f, 0f), 2f);
            Col(root, "StairBalustrade", new Vector3(bx, (Ground + TerraceY + 1.05f) * 0.5f, (sz0 - 0.3f + sz1) * 0.5f), new Vector3(bw, TerraceY + 1.05f - Ground, sz1 - sz0 + 0.3f));

            // The room under the terrace.
            const float rx0 = -7.1f, rx1 = -4.6f, rz0 = 23.6f, rz1 = 31.4f;
            k.Box(m.Mud, new Vector3((rx0 + rx1) * 0.5f, (Ground + TerraceY) * 0.5f, (rz0 + rz1) * 0.5f), new Vector3(rx1 - rx0, TerraceY - Ground, rz1 - rz0), 2.2f);
            var east = new Face(new Vector3(rx1, Floor, (rz0 + rz1) * 0.5f), Vector3.right); // a runs toward -z
            float half = (rz1 - rz0) * 0.5f;
            east.Box(k, m.MudDark, -half, half, Ground - Floor, 0.5f, -0.1f, 0.04f, 2f);
            east.Box(k, m.Gypsum, -half, half, 1.92f, 2.02f, 0f, 0.05f, 2f);
            NajdiDoor(k, m, east, 0.1f, 1.2f, 1.95f, 2);
            Window(k, m, east, 2.4f, 1.0f, true, 1);
            Window(k, m, east, -2.45f, 1.05f, false, 3);
            var rnd = new System.Random(71);
            for (float a = -half + 0.4f; a < half - 0.2f; a += 0.45f)
                if (Mathf.Abs(a - 0.1f) > 1.05f && Mathf.Abs(a - 2.4f) > 0.55f && Mathf.Abs(a + 2.45f) > 0.55f)
                    TriFace(k.Of(rnd.NextDouble() < 0.2 ? m.GlowTri : m.Dark), east.P(a, 1.62f, 0.006f), east.Out, 0.18f, 0.22f);
            foreach (float a in new[] { -3.1f, 3.1f }) Rod(k, m.PalmWood, east.P(a, 2.02f, -0.1f), east.P(a, 1.94f, 0.55f), 0.12f);
            var south = new Face(new Vector3((rx0 + rx1) * 0.5f, Floor, rz0), Vector3.back);
            south.Box(k, m.MudDark, -1.25f, 1.25f, Ground - Floor, 0.5f, -0.1f, 0.04f, 2f);
            TriFace(k.Of(m.Dark), south.P(-0.6f, 1.7f, 0.006f), south.Out, 0.18f, 0.22f);
            Col(root, "TerraceRoom", new Vector3((rx0 + rx1) * 0.5f, (Ground + TerraceY - 0.2f) * 0.5f, (rz0 + rz1) * 0.5f), new Vector3(rx1 - rx0, TerraceY - 0.2f - Ground, rz1 - rz0));
            var deck = Col(root, "Terrace", new Vector3((rx0 + rx1) * 0.5f, TerraceY - 0.1f, (rz0 + rz1) * 0.5f), new Vector3(rx1 - rx0, 0.2f, rz1 - rz0), Layers.Floor);
            deck.gameObject.AddComponent<Surface>().Kind = "stone";
            var landing = Col(root, "TerraceLanding", new Vector3((sx0 + sx1) * 0.5f, TerraceY - 0.1f, (zHigh - 0.05f + rz0 + 0.02f) * 0.5f), new Vector3(sx1 - sx0, 0.2f, rz0 + 0.02f - zHigh + 0.05f), Layers.Floor);
            landing.gameObject.AddComponent<Surface>().Kind = "stone";

            // Parapets on the terrace's open sides (east over the court, south over the stair's landing), merlons on top.
            const float pH = 0.75f, pT = 0.25f;
            k.Box(m.Mud, new Vector3(rx1 - pT * 0.5f, TerraceY + pH * 0.5f, (rz0 + rz1) * 0.5f), new Vector3(pT, pH, rz1 - rz0), 2f);
            k.Box(m.Mud, new Vector3((sx1 + rx1 - pT) * 0.5f, TerraceY + pH * 0.5f, rz0 + pT * 0.5f), new Vector3(rx1 - pT - sx1, pH, pT), 2f);
            k.Box(m.Gypsum, new Vector3(rx1 - pT * 0.5f, TerraceY + pH - 0.12f, (rz0 + rz1) * 0.5f), new Vector3(pT + 0.04f, 0.06f, rz1 - rz0), 2f);
            for (float z = rz0 + 0.3f; z < rz1 - 0.15f; z += 0.6f) Merlon(k, m.Mud, new Vector3(rx1 - pT * 0.5f, TerraceY + pH, z), Quaternion.LookRotation(Vector3.right), 0.4f, pT * 0.8f, 0.15f);
            for (float x = sx1 + 0.3f; x < rx1 - 0.2f; x += 0.6f) Merlon(k, m.Mud, new Vector3(x, TerraceY + pH, rz0 + pT * 0.5f), Quaternion.LookRotation(Vector3.forward), 0.4f, pT * 0.8f, 0.15f);
            Col(root, "TerraceParapetE", new Vector3(rx1 - pT * 0.5f, TerraceY + 0.6f, (rz0 + rz1) * 0.5f), new Vector3(pT, 1.2f, rz1 - rz0));
            Col(root, "TerraceParapetS", new Vector3((sx1 + rx1) * 0.5f, TerraceY + 0.6f, rz0 + pT * 0.5f), new Vector3(rx1 - sx1, 1.2f, pT));

            // On the terrace: water jars, a rolled carpet, a palm-leaf mat, and the second lamp.
            foreach (var (x, z, s) in new[] { (-6.75f, 30.85f, 1f), (-6.45f, 31.05f, 0.82f) })
                k.Of(m.Clay).AddLathe(new Vector3(x, TerraceY, z), new[] { new Vector2(0.05f * s, 0f), new Vector2(0.17f * s, 0.12f * s), new Vector2(0.2f * s, 0.32f * s),
                    new Vector2(0.15f * s, 0.5f * s), new Vector2(0.08f * s, 0.58f * s), new Vector2(0.09f * s, 0.64f * s), new Vector2(0.06f * s, 0.65f * s) }, 14, 1.2f);
            soft.Of(m.Carpets[3]).AddCylinder(new Vector3(-6.85f, TerraceY + 0.1f, 25.2f), 0.1f, 1.6f, 10, 1f, Quaternion.Euler(90f, 0f, 0f));
            soft.Box(m.Arish, new Vector3(-5.9f, TerraceY + 0.006f, 27.6f), new Vector3(1.4f, 0.012f, 2f), 1.2f);
            SwitchLamp.Create(root, new Vector3(-5.35f, TerraceY, 30.5f), Warm, false, 2f, 2f);
        }

        /// <summary>A closed Najdi double door: gypsum surround, two painted leaves, a frieze of triangles above.</summary>
        static void NajdiDoor(KitBatch k, CourtMats m, Face f, float a, float width, float height, int colour)
        {
            float hw = width * 0.5f;
            f.Box(k, m.Gypsum, a - hw - 0.18f, a + hw + 0.18f, 0f, height + 0.22f, -0.05f, 0.06f, 2f);
            f.Box(k, m.Dark, a - hw - 0.02f, a + hw + 0.02f, 0f, height + 0.02f, 0.06f, 0.062f);
            Leaf(k, m, f.P(a - hw, 0.01f, 0.064f), f.Right, f.Out, hw - 0.01f, height - 0.02f, colour);
            Leaf(k, m, f.P(a + hw, 0.01f, 0.064f), -f.Right, f.Out, hw - 0.01f, height - 0.02f, colour + 2);
            for (int i = 0; i < 4; i++) TriFace(k.Of(m.Dark), f.P(a + (i - 1.5f) * 0.3f, height + 0.3f, 0.006f), f.Out, 0.18f, 0.2f);
        }

        /// <summary>A small window: gypsum surround, a painted shutter (or a dark opening), three triangle vents above.</summary>
        static void Window(KitBatch k, CourtMats m, Face f, float a, float v, bool shutter, int paint)
        {
            f.Box(k, m.Gypsum, a - 0.36f, a + 0.36f, v - 0.08f, v + 0.8f, 0f, 0.05f, 2f);
            if (shutter)
            {
                f.Box(k, m.Door, a - 0.28f, a + 0.28f, v, v + 0.72f, 0.05f, 0.075f);
                f.Box(k, m.Dark, a - 0.006f, a + 0.006f, v, v + 0.72f, 0.075f, 0.078f);
                for (int i = 0; i < 4; i++)
                    TriFace(k.Of(m.Paint[(paint + i) % m.Paint.Length]), f.P(a + (i % 2 == 0 ? -0.14f : 0.14f), v + (i < 2 ? 0.44f : 0.12f), 0.078f), f.Out, 0.15f, 0.15f, i < 2);
            }
            else f.Box(k, m.Dark, a - 0.28f, a + 0.28f, v, v + 0.72f, 0.05f, 0.052f);
            for (int i = 0; i < 3; i++) TriFace(k.Of(m.Dark), f.P(a + (i - 1) * 0.22f, v + 0.92f, 0.006f), f.Out, 0.16f, 0.18f);
        }

        // ================================================================== the coffee corner
        /// <summary>
        /// The north end: a raised dais with a carpet and cushions under a shade cloth on palm-trunk posts, two brass lanterns
        /// on the beam, a brass oil lamp and an incense burner smoking on the dais, and before it the coal hearth with a row
        /// of dallahs and a tray of cups: the one in front can be lifted to pour (RiyadhDallahPour).
        /// </summary>
        static void CoffeeCorner(Transform root, KitBatch k, KitBatch soft, CourtMats m)
        {
            const float dx0 = -4.35f, dx1 = 1.45f, dz0 = 28.5f, dz1 = ZN - 0.3f, dTop = -0.08f;
            k.Box(m.MudDark, new Vector3((dx0 + dx1) * 0.5f, (Ground + dTop) * 0.5f, (dz0 + dz1) * 0.5f), new Vector3(dx1 - dx0, dTop - Ground, dz1 - dz0), 2f);
            k.Box(m.Gypsum, new Vector3((dx0 + dx1) * 0.5f, dTop - 0.03f, dz0 - 0.005f), new Vector3(dx1 - dx0 + 0.02f, 0.06f, 0.02f), 2f);
            var dais = Col(root, "Dais", new Vector3((dx0 + dx1) * 0.5f, (Ground + dTop) * 0.5f, (dz0 + dz1) * 0.5f), new Vector3(dx1 - dx0, dTop - Ground, dz1 - dz0), Layers.Floor);
            dais.gameObject.AddComponent<Surface>().Kind = "wood";
            soft.Box(m.Carpets[0], new Vector3((dx0 + dx1) * 0.5f, dTop + 0.005f, (dz0 + dz1) * 0.5f), new Vector3(dx1 - dx0 - 0.3f, 0.01f, dz1 - dz0 - 0.25f), 1.2f);
            // Cushions along the wall and down the west end, bolsters at the ends.
            soft.Box(m.CushionRed, new Vector3(-1.45f, dTop + 0.08f, 30.85f), new Vector3(5.3f, 0.15f, 0.5f));
            for (int i = 0; i < 7; i++)
                soft.Box(i % 2 == 0 ? m.CushionRed : m.CushionOchre, new Vector3(-3.75f + i * 0.77f, dTop + 0.38f, 31.2f), new Vector3(0.72f, 0.46f, 0.17f), Quaternion.Euler(12f, 0f, 0f));
            soft.Box(m.CushionRed, new Vector3(-4.07f, dTop + 0.08f, 29.6f), new Vector3(0.5f, 0.15f, 1.9f));
            for (int i = 0; i < 2; i++)
                soft.Box(i == 0 ? m.CushionOchre : m.CushionRed, new Vector3(-4.24f, dTop + 0.38f, 29.05f + i * 0.8f), new Vector3(0.17f, 0.46f, 0.72f), Quaternion.Euler(0f, 0f, -12f));
            foreach (float x in new[] { -3.85f, 0.95f })
                soft.Of(m.CushionOchre).AddCylinder(new Vector3(x, dTop + 0.24f, 30.55f), 0.08f, 0.5f, 10, 1f, Quaternion.Euler(90f, 0f, 0f));
            // A second tray on the dais: a dallah, poured cups and dates.
            var tray = new Vector3(-1.7f, dTop, 29.85f);
            k.Of(m.PalmWood).AddCylinder(tray, 0.2f, 0.16f, 12);
            k.Of(m.Brass).AddCylinder(tray + Vector3.up * 0.16f, 0.38f, 0.02f, 24);
            k.Of(m.Brass).AddTorus(tray + Vector3.up * 0.18f, 0.37f, 0.01f, 24, 4, Quaternion.identity);
            DallahMesh(k.Of(m.Brass), tray + new Vector3(0.18f, 0.18f, 0f), 1f);
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 55f + 80f) * Mathf.Deg2Rad;
                var cp = tray + new Vector3(Mathf.Cos(a) * 0.25f, 0.18f, Mathf.Sin(a) * 0.25f);
                CupMesh(k.Of(m.PaintWhite), cp);
                k.Of(m.Coffee).AddDisc(cp + Vector3.up * 0.033f, Vector3.up, 0.026f, 10);
            }
            var bowl = tray + new Vector3(0.05f, 0.18f, -0.24f);
            k.Of(m.Brass).AddLathe(bowl, new[] { new Vector2(0f, 0f), new Vector2(0.05f, 0f), new Vector2(0.1f, 0.04f), new Vector2(0.11f, 0.06f) }, 14);
            for (int i = 0; i < 8; i++) k.Of(m.Dates).AddSphere(bowl + new Vector3(Mathf.Cos(i * 0.8f) * 0.05f, 0.055f + (i % 3) * 0.01f, Mathf.Sin(i * 0.8f) * 0.05f), 0.02f, 6, 4);
            // The brass oil lamp (modelled when the file is present) and an incense burner with a thread of smoke.
            var lampAt = new Vector3(0.95f, dTop, 29.5f);
            var glass = Mats.Glass(new Color(0.85f, 0.8f, 0.7f, 0.25f));
            if (Models.Spawn("GardenLamp", root, Models.Slots(("TS_Brass", m.Brass), ("TS_EmissiveWarm", m.LampGlow), ("TS_Glass", glass), ("TS_Fabric", m.Burlap)), lampAt, 0f, 1.25f) == null)
            {
                k.Of(m.Brass).AddLathe(lampAt, new[] { new Vector2(0.05f, 0f), new Vector2(0.05f, 0.02f), new Vector2(0.015f, 0.05f), new Vector2(0.05f, 0.12f), new Vector2(0.02f, 0.16f) }, 12);
                k.Box(m.LampGlow, lampAt + Vector3.up * 0.2f, new Vector3(0.025f, 0.05f, 0.025f));
            }
            var burner = new Vector3(-3.05f, dTop, 29.3f);
            k.Of(m.Door).AddFrustum(burner, 0.08f, 0.06f, 0.16f, 4, true, true, 1f, Quaternion.Euler(0f, 45f, 0f));
            k.Of(m.Brass).AddFrustum(burner + Vector3.up * 0.16f, 0.075f, 0.09f, 0.05f, 4, true, true, 1f, Quaternion.Euler(0f, 45f, 0f));
            k.Of(m.Coal).AddSphere(burner + Vector3.up * 0.215f, 0.025f, 8, 5);
            Particles(root, "IncenseSmoke", burner + Vector3.up * 0.24f, true, 2.4f, new Vector2(2.5f, 4f), new Vector2(0.025f, 0.05f),
                new Color(0.82f, 0.8f, 0.76f, 0.2f), new Color(0.7f, 0.68f, 0.66f, 0.14f), new Vector2(0.08f, 0.16f), 0.012f, false, 2.4f, 0.08f, 0.01f, 60);

            // Shade cloth over the dais from the north wall to a beam on two palm-trunk posts; lanterns on the beam.
            foreach (float x in new[] { dx0 + 0.2f, dx1 - 0.2f })
            {
                k.Of(m.PalmWood).AddCylinder(new Vector3(x, Floor, dz0 - 0.05f), 0.1f, 2.7f, 9, 1.5f);
                for (int r = 0; r < 6; r++) k.Of(m.PalmWood).AddCylinder(new Vector3(x, Floor + 0.25f + r * 0.4f, dz0 - 0.05f), 0.112f, 0.05f, 9);
                var post = new GameObject("ShadePost");
                post.transform.SetParent(root, false);
                post.transform.localPosition = new Vector3(x, 1.0f, dz0 - 0.05f);
                var pc = post.AddComponent<CapsuleCollider>();
                pc.radius = 0.12f;
                pc.height = 2.7f;
            }
            k.Box(m.PalmWood, new Vector3((dx0 + dx1) * 0.5f, 2.42f, dz0 - 0.05f), new Vector3(dx1 - dx0, 0.14f, 0.15f));
            Awning(root, "ShadeCloth", new Vector3(dx1 - 0.2f, 2.85f, dz1 - 0.05f), Vector3.left, Vector3.back, dx1 - dx0 - 0.4f, dz1 - dz0 + 0.05f - 0.07f, 8f, m.Sail, 0.07f);
            var lantern = BrassLantern(root, "ShadeLantern", new Vector3(-3.2f, 2.35f, dz0 - 0.05f), 0.04f, 0.72f, m);
            WindSway.Add(lantern, 3.5f, 2.7f);
            var lit = BrassLantern(root, "ShadeLanternLit", new Vector3(0.6f, 2.35f, dz0 - 0.05f), 0.04f, 0.72f, m);
            WindSway.Add(lit, 3.5f, 3f);
            var l2 = Point(root, new Vector3(0.6f, 1.98f, dz0 - 0.05f), new Color(1f, 0.74f, 0.46f), 1.3f, 4.2f);
            Flicker.Add(l2, 0.06f, 2f);

            Hearth(root, k, m);
        }

        /// <summary>
        /// The coffee hearth in front of the dais: a mud block with a trough of glowing coals (sparks lift off them), an iron
        /// grate, two dallahs keeping warm at the back, the one to pour in front, and a brass tray of three empty cups.
        /// </summary>
        static void Hearth(Transform root, KitBatch k, CourtMats m)
        {
            const float hx0 = -2.3f, hx1 = -0.3f, hz0 = 27.5f, hz1 = 28.3f, top = 0.12f;
            k.Box(m.MudDark, new Vector3((hx0 + hx1) * 0.5f, (Ground + top) * 0.5f, (hz0 + hz1) * 0.5f), new Vector3(hx1 - hx0, top - Ground, hz1 - hz0), 2f);
            k.Box(m.Gypsum, new Vector3((hx0 + hx1) * 0.5f, top - 0.04f, hz0 - 0.006f), new Vector3(hx1 - hx0, 0.05f, 0.012f), 2f);
            Col(root, "Hearth", new Vector3((hx0 + hx1) * 0.5f, (Ground + top) * 0.5f, (hz0 + hz1) * 0.5f), new Vector3(hx1 - hx0, top - Ground, hz1 - hz0));
            // Coals in an ash bed, the grate over them.
            var bed = new Vector3(-1.68f, top, 27.9f);
            k.Box(m.Ash, bed + Vector3.up * 0.006f, new Vector3(0.95f, 0.012f, 0.6f));
            var rnd = new System.Random(29);
            for (int i = 0; i < 20; i++)
                Blob(k.Of(m.Coal), bed + new Vector3(R(rnd, -0.42f, 0.42f), 0.014f, R(rnd, -0.25f, 0.25f)), R(rnd, 0.035f, 0.055f), R(rnd, 0.022f, 0.032f), 7);
            for (int i = 0; i < 6; i++)
            {
                float z = 27.68f + i * 0.088f;
                Rod(k, m.Iron, new Vector3(bed.x - 0.45f, 0.155f, z), new Vector3(bed.x + 0.45f, 0.155f, z), 0.012f);
            }
            foreach (float x in new[] { -0.4f, 0.4f }) Rod(k, m.Iron, new Vector3(bed.x + x, 0.148f, 27.62f), new Vector3(bed.x + x, 0.148f, 28.18f), 0.014f);
            DallahMesh(k.Of(m.Brass), new Vector3(-2f, 0.165f, 27.95f), 1f);
            DallahMesh(k.Of(m.Brass), new Vector3(-1.72f, 0.165f, 28.1f), 0.92f);
            Particles(root, "CoalSparks", bed + Vector3.up * 0.04f, true, 1.2f, new Vector2(0.8f, 1.5f), new Vector2(0.006f, 0.012f),
                new Color(1f, 0.62f, 0.25f, 1f), new Color(1f, 0.45f, 0.15f, 1f), new Vector2(0.3f, 0.7f), 0.05f, true, 1f, 0.15f, 0.3f, 40);
            var fire = Point(root, bed + Vector3.up * 0.33f, new Color(1f, 0.55f, 0.25f), 1.6f, 4f);
            Flicker.Add(fire, 0.12f, 3.5f);

            // The tray of three cups on the hearth's east end.
            var hearth = new GameObject("CoffeeHearth").transform;
            hearth.SetParent(root, false);
            var trayC = new Vector3(-0.72f, top, 27.9f);
            k.Of(m.Brass).AddCylinder(trayC, 0.27f, 0.02f, 24);
            k.Of(m.Brass).AddTorus(trayC + Vector3.up * 0.02f, 0.265f, 0.008f, 24, 4, Quaternion.identity);
            var cups = new[] { trayC + new Vector3(0.12f, 0.065f, 0.08f), trayC + new Vector3(-0.12f, 0.065f, 0.08f), trayC + new Vector3(0f, 0.065f, -0.13f) };
            var fills = new Transform[cups.Length];
            var discMesh = new MeshBuilder();
            discMesh.AddDisc(Vector3.zero, Vector3.up, 1f, 12);
            for (int i = 0; i < cups.Length; i++)
            {
                CupMesh(k.Of(m.PaintWhite), cups[i] - Vector3.up * 0.045f);
                var fill = MeshFactory.FromBuilder("CupFill", discMesh, m.Coffee, hearth);
                fill.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                fill.transform.localScale = Vector3.zero;
                fills[i] = fill.transform;
            }
            // The pot to pour, on its own pivot (its base centre) so it can lift, tip and come back.
            var pot = new GameObject("Dallah").transform;
            pot.SetParent(hearth, false);
            pot.localPosition = new Vector3(-1.42f, 0.165f, 27.82f);
            pot.localScale = Vector3.one * 1.15f;
            var pk = new KitBatch();
            DallahMesh(pk.Of(m.Brass), Vector3.zero, 1f);
            pk.Bake(pot, "Dallah");
            Particles(pot, "SpoutSteam", RiyadhDallahPour.SpoutTip, true, 2.6f, new Vector2(1.6f, 2.4f), new Vector2(0.03f, 0.06f),
                new Color(0.92f, 0.92f, 0.9f, 0.2f), new Color(0.85f, 0.85f, 0.84f, 0.14f), new Vector2(0.12f, 0.24f), 0.01f, false, 2.2f, 0.06f, 0.008f, 60);
            var streamMesh = new MeshBuilder();
            streamMesh.AddCylinder(Vector3.zero, 1f, 1f, 6);
            var stream = MeshFactory.FromBuilder("PourStream", streamMesh, m.Coffee, hearth);
            stream.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            stream.transform.localScale = Vector3.zero;
            var cupSteam = Particles(hearth, "CupSteam", cups[0], false, 0f, new Vector2(1.2f, 2f), new Vector2(0.03f, 0.06f),
                new Color(0.92f, 0.92f, 0.9f, 0.24f), new Color(0.85f, 0.85f, 0.84f, 0.16f), new Vector2(0.12f, 0.24f), 0.012f, false, 2.4f, 0.06f, 0.015f, 80);
            var motes = Particles(hearth, "CardamomMotes", cups[0], false, 0f, new Vector2(2.6f, 4f), new Vector2(0.01f, 0.022f),
                new Color(0.8f, 0.86f, 0.5f, 0.9f), new Color(0.98f, 0.86f, 0.55f, 0.8f), new Vector2(0.06f, 0.18f), 0.12f, true, 1f, 0.12f, 0.08f, 80);

            var use = new GameObject("PourTheCoffee");
            use.transform.SetParent(hearth, false);
            use.transform.localPosition = pot.localPosition + new Vector3(0.05f, 0.2f, 0f);
            var trigger = use.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(0.38f, 0.45f, 0.38f);
            var pour = use.AddComponent<RiyadhDallahPour>();
            pour.Pot = pot;
            pour.Stream = stream.transform;
            pour.Fills = fills;
            pour.Cups = cups;
            pour.CupSteam = cupSteam;
            pour.Motes = motes;
            pour.MarkerPoint = pot;
            pour.Init();
        }

        /// <summary>A dallah: waisted body, flared lid with a finial, a long beak spout and a handle (base centre at `at`).</summary>
        static void DallahMesh(MeshBuilder b, Vector3 at, float s)
        {
            b.AddLathe(at, new[] { new Vector2(0f, 0f), new Vector2(0.075f * s, 0f), new Vector2(0.085f * s, 0.03f * s), new Vector2(0.08f * s, 0.09f * s), new Vector2(0.05f * s, 0.15f * s),
                new Vector2(0.04f * s, 0.19f * s), new Vector2(0.06f * s, 0.22f * s), new Vector2(0.065f * s, 0.235f * s), new Vector2(0.03f * s, 0.27f * s), new Vector2(0.012f * s, 0.3f * s), new Vector2(0f, 0.34f * s) }, 16);
            b.AddSphere(at + new Vector3(0f, 0.345f * s, 0f), 0.014f * s, 8, 5);
            b.AddTube(new List<Vector3> { at + new Vector3(0.07f, 0.06f, 0f) * s, at + new Vector3(0.12f, 0.12f, 0f) * s, at + new Vector3(0.15f, 0.2f, 0f) * s, at + new Vector3(0.17f, 0.25f, 0f) * s, at + new Vector3(0.22f, 0.27f, 0f) * s }, 0.012f * s, 6);
            b.AddTube(new List<Vector3> { at + new Vector3(-0.07f, 0.05f, 0f) * s, at + new Vector3(-0.13f, 0.09f, 0f) * s, at + new Vector3(-0.13f, 0.18f, 0f) * s, at + new Vector3(-0.05f, 0.21f, 0f) * s }, 0.008f * s, 5);
        }

        /// <summary>A small handleless coffee cup with an inside to it (base centre at `at`, rim 0.045 m up).</summary>
        static void CupMesh(MeshBuilder b, Vector3 at)
        {
            b.AddLathe(at, new[] { new Vector2(0.02f, 0f), new Vector2(0.032f, 0.045f), new Vector2(0.028f, 0.045f), new Vector2(0.017f, 0.006f), new Vector2(0f, 0.006f) }, 12);
            b.AddDisc(at, Vector3.down, 0.02f, 12);
        }

        // ================================================================== helpers
        /// <summary>A brass lantern on a chain under its own pivot (for WindSway): a glowing core in an iron frame with a
        /// pyramid cap and finial (as on the plaza's posts).</summary>
        static Transform BrassLantern(Transform parent, string name, Vector3 hang, float chain, float scale, CourtMats m)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = hang;
            float hw = 0.12f * scale, hh = 0.19f * scale, bar = 0.016f * Mathf.Max(1f, scale);
            var c = Vector3.down * (chain + hh * 1.9f + 0.05f * scale);
            var k = new KitBatch();
            k.Box(m.LampGlow, c, new Vector3(hw * 1.7f, hh * 1.8f, hw * 1.7f));
            foreach (float x in new[] { -hw, hw })
                foreach (float z in new[] { -hw, hw }) k.Box(m.Iron, c + new Vector3(x, 0f, z), new Vector3(bar, hh * 2f, bar));
            foreach (float y in new[] { -hh, -hh * 0.25f, hh * 0.45f, hh })
                foreach (float s in new[] { -hw, hw })
                {
                    k.Box(m.Iron, c + new Vector3(0f, y, s), new Vector3(hw * 2f, bar * 0.8f, bar * 0.8f));
                    k.Box(m.Iron, c + new Vector3(s, y, 0f), new Vector3(bar * 0.8f, bar * 0.8f, hw * 2f));
                }
            var square = Quaternion.Euler(0f, 45f, 0f);
            k.Of(m.Brass).AddFrustum(c + Vector3.up * hh, hw * 1.55f, 0.02f * scale, hh * 0.9f, 4, true, false, 1f, square);
            k.Of(m.Brass).AddFrustum(c + Vector3.down * (hh + 0.04f * scale), hw * 0.9f, hw * 1.35f, 0.04f * scale, 4, true, true, 1f, square);
            k.Of(m.Brass).AddSphere(c + Vector3.up * (hh * 1.9f + 0.03f * scale), 0.025f * scale, 8, 5);
            k.Of(m.Iron).AddCylinder(Vector3.down * chain, 0.006f, chain, 4);
            k.Bake(pivot, name);
            return pivot;
        }

        /// <summary>A cloth hanging from a horizontal rail (top edge fixed, the bottom moving): a ClothFlag turned so its
        /// free edge is the bottom. It starts at `railStart`, runs `width` along `along` and hangs `length` down.</summary>
        static ClothFlag Hang(Transform parent, string name, Vector3 railStart, Vector3 along, float width, float length, Material mat, float wave)
        {
            var f = ClothFlag.Create(parent, name, railStart, length, width, mat, 0f, wave);
            var y = -along.normalized;
            var z = Vector3.Cross(Vector3.down, y);
            f.transform.localRotation = Quaternion.LookRotation(z, y);
            return f;
        }

        /// <summary>A shade cloth fixed along a wall edge: from `corner` it runs `width` along `along` and reaches `depth`
        /// out along `outward`, dipping `slopeDeg` toward its free edge, which ripples up and down in the wind.</summary>
        static ClothFlag Awning(Transform parent, string name, Vector3 corner, Vector3 along, Vector3 outward, float width, float depth, float slopeDeg, Material mat, float wave)
        {
            var f = ClothFlag.Create(parent, name, corner, depth, width, mat, 0f, wave);
            float r = slopeDeg * Mathf.Deg2Rad;
            var x = (outward.normalized * Mathf.Cos(r) + Vector3.down * Mathf.Sin(r)).normalized;
            var y = -along.normalized;
            var z = Vector3.Cross(x, y);
            f.transform.localRotation = Quaternion.LookRotation(z, y);
            return f;
        }

        /// <summary>Lets a palm's crown (fronds, dead fronds, dates) sway about the top of its trunk.</summary>
        static void SwayCrown(GameObject palm, float height, float degrees, float period)
        {
            if (palm == null) return;
            var t = palm.transform;
            var fronds = t.Find("Fronds");
            if (fronds == null) return;
            var mf = fronds.GetComponent<MeshFilter>();
            var centre = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.center : Vector3.up * height;
            var pivot = new GameObject("Crown").transform;
            pivot.SetParent(t, false);
            pivot.localPosition = new Vector3(centre.x, height * 0.98f, centre.z);
            foreach (var part in new[] { "Fronds", "DeadFronds", "Dates" })
            {
                var c = t.Find(part);
                if (c != null) c.SetParent(pivot, true);
            }
            WindSway.Add(pivot, degrees, period);
        }

        /// <summary>A small particle system (soft dots): looping at `rate` or emitted by hand, rising and drifting.</summary>
        static ParticleSystem Particles(Transform parent, string name, Vector3 at, bool loop, float rate, Vector2 life, Vector2 size, Color c0, Color c1,
            Vector2 rise, float drift, bool additive, float grow, float noise, float radius, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = loop;
            main.playOnAwake = loop;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = new ParticleSystem.MinMaxGradient(c0, c1);
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Max(0.001f, radius);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-drift, drift);
            vel.y = new ParticleSystem.MinMaxCurve(rise.x, rise.y);
            vel.z = new ParticleSystem.MinMaxCurve(-drift, drift);
            if (noise > 0f)
            {
                var n = ps.noise;
                n.enabled = true;
                n.strength = noise;
                n.frequency = 0.6f;
                n.scrollSpeed = 0.25f;
            }
            if (grow != 1f)
            {
                var sz = ps.sizeOverLifetime;
                sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, grow));
            }
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = Mats.Particle(Tex.SoftDot, additive, Color.white, additive ? 1.4f : 1f, Mats.StencilNotEqual);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (loop) ps.Play();
            return ps;
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

        static BoxCollider Col(Transform parent, string name, Vector3 centre, Vector3 size, int layer = Layers.Default, Quaternion? rot = null)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = rot ?? Quaternion.identity;
            var c = go.AddComponent<BoxCollider>();
            c.size = size;
            return c;
        }

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

        /// <summary>A square-section rod between two points.</summary>
        static void Rod(KitBatch k, Material m, Vector3 a, Vector3 b, float thick)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            d /= len;
            k.Box(m, (a + b) * 0.5f, new Vector3(thick, thick, len), Quaternion.LookRotation(d, Mathf.Abs(d.y) > 0.98f ? Vector3.forward : Vector3.up));
        }

        /// <summary>A flattened ellipsoid (radius r, half-height h).</summary>
        static void Blob(MeshBuilder b, Vector3 c, float r, float h, int seg)
        {
            var prof = new List<Vector2>();
            for (int i = 0; i <= 6; i++) { float a = Mathf.Lerp(-90f, 90f, i / 6f) * Mathf.Deg2Rad; prof.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * h)); }
            b.AddLathe(c, prof, seg);
        }

        static float R(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
    }

    /// <summary>
    /// The coffee court's dallah (Expand_Riyadh): lift it and it pours. The pot rises off the coals, tips its long beak over
    /// the next cup on the tray and pours a thin stream; the cup fills, steam curls off it and a drift of cardamom-coloured
    /// motes rises, then the pot settles back on the grate with a clink. Three cups, then a fresh round. It never blocks
    /// anything and cannot go wrong.
    /// </summary>
    public class RiyadhDallahPour : Interactable
    {
        /// <summary>The beak's tip in the pot's own frame (before the pot's scale).</summary>
        public static readonly Vector3 SpoutTip = new Vector3(0.22f, 0.27f, 0f);
        const float Tilt = 66f, Lift = 0.25f, Above = 0.22f, CupDepth = 0.04f;

        public Transform Pot, Stream;
        public Transform[] Fills;
        public Vector3[] Cups;       // rim centres, in the pot's parent's frame
        public ParticleSystem CupSteam, Motes;

        Vector3 m_Home;
        float m_T = -1f, m_Puff;
        int m_Cup;
        bool m_Pouring;

        /// <summary>Cups poured so far (tests).</summary>
        public int Pours { get; private set; }

        public void Init()
        {
            m_Home = Pot.localPosition;
            Prompt = "Pour the coffee";
            MaxDistance = 2.6f;
        }

        public override bool CanUse(Interactor who) => base.CanUse(who) && m_T < 0f && Cups != null && Cups.Length > 0;

        public override void Use(Interactor who)
        {
            if (!CanUse(who)) return;
            if (m_Cup == 0 && Pours > 0) for (int i = 0; i < Cups.Length; i++) SetFill(i, 0f); // a fresh round
            m_T = 0f;
            m_Pouring = false;
            AudioDirector.I?.Play("grab", Pot.position, 0.55f, 1.1f);
            base.Use(who);
        }

        void Update()
        {
            if (m_T < 0f || Pot == null) return;
            m_T += Time.deltaTime;
            var cup = Cups[m_Cup];
            var tip = Quaternion.Euler(0f, 0f, -Tilt) * (SpoutTip * Pot.localScale.x);
            var pour = cup + Vector3.up * Above - tip;
            var lifted = m_Home + Vector3.up * Lift;
            float t = m_T;
            if (t < 0.45f) Pose(Vector3.Lerp(m_Home, lifted, Ease(t / 0.45f)), 0f);
            else if (t < 0.95f) { float u = Ease((t - 0.45f) / 0.5f); Pose(Vector3.Lerp(lifted, pour, u), Tilt * u); }
            else if (t < 2.15f)
            {
                Pose(pour + Vector3.up * (Mathf.Sin((t - 0.95f) * 5f) * 0.004f), Tilt);
                if (!m_Pouring) BeginPour(cup);
                float u = (t - 0.95f) / 1.2f;
                float fill = Mathf.Clamp01(u * 1.15f) * 0.85f;
                SetFill(m_Cup, fill);
                float surface = cup.y - CupDepth + CupDepth * fill;
                float r = 0.0065f * Mathf.Clamp01(u / 0.08f) * Mathf.Clamp01((1f - u) / 0.08f);
                if (Stream != null)
                {
                    Stream.localPosition = new Vector3(cup.x, surface, cup.z);
                    Stream.localScale = new Vector3(r, Mathf.Max(0.001f, cup.y + Above - surface), r);
                }
                m_Puff -= Time.deltaTime;
                if (m_Puff <= 0f && CupSteam != null)
                {
                    m_Puff = 0.14f;
                    CupSteam.transform.localPosition = cup + Vector3.up * 0.02f;
                    CupSteam.Emit(2);
                }
            }
            else if (t < 2.55f)
            {
                if (Stream != null) Stream.localScale = Vector3.zero;
                float u = Ease((t - 2.15f) / 0.4f);
                Pose(Vector3.Lerp(pour, lifted, u), Tilt * (1f - u));
            }
            else if (t < 3f) Pose(Vector3.Lerp(lifted, m_Home, Ease((t - 2.55f) / 0.45f)), 0f);
            else
            {
                Pose(m_Home, 0f);
                m_T = -1f;
                m_Cup = (m_Cup + 1) % Cups.Length;
                AudioDirector.I?.Play("lantern_place", Pot.position, 0.4f, 1.25f);
            }
        }

        void BeginPour(Vector3 cup)
        {
            m_Pouring = true;
            Pours++;
            m_Puff = 0f;
            var world = Pot.parent != null ? Pot.parent.TransformPoint(cup) : cup;
            AudioDirector.I?.Play("sun_sizzle", world, 0.32f, 0.5f);
            if (Motes != null)
            {
                Motes.transform.localPosition = cup + Vector3.up * 0.08f;
                Motes.Emit(28);
            }
            if (Pours == 1) Hud.I?.ShowCaption("coffee pours, and the air fills with cardamom", 3f);
        }

        void Pose(Vector3 pos, float tilt)
        {
            Pot.localPosition = pos;
            Pot.localRotation = Quaternion.Euler(0f, 0f, -tilt);
        }

        void SetFill(int i, float f)
        {
            if (Fills == null || i >= Fills.Length || Fills[i] == null) return;
            if (f <= 0.02f) { Fills[i].localScale = Vector3.zero; return; }
            var cup = Cups[i];
            float r = 0.019f + 0.011f * f; // the inside widens toward the rim; the edge tucks into the wall
            Fills[i].localPosition = new Vector3(cup.x, cup.y - CupDepth + CupDepth * f, cup.z);
            Fills[i].localScale = new Vector3(r, 1f, r);
        }

        static float Ease(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
    }
}

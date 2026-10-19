using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    // Place detail for New York (playtest: "the places look cheap and bare"). The puzzle objects stay in NewYorkCell.cs;
    // these builders dress the terminal around them, batched per material and per area so lights stay local: the arrivals
    // hall seen through the glazing, canopy fittings and signage, the baggage claim and the west wall, the harbour edge
    // with a moored ferry and an empty ferry slip, and the street beyond the security fence. The skyline and the statue
    // are the landmark vista's (x > 73), so everything east of the railing stays low or skeletal.
    public partial class NewYorkCell
    {
        /// <summary>Materials shared by the New York detail builders.</summary>
        sealed class NyPalette
        {
            public Material Concrete, Metal, Steel, Frame, Black, White, Yellow, Red, Orange, Navy, Green, Granite, Chrome, Wood, Rope, Glass;
            public Material Panel, Warm, Sodium, DarkGlass;
        }

        static readonly Vector2[] s_LampBase =
        {
            new Vector2(0.2f, 0f), new Vector2(0.2f, 0.1f), new Vector2(0.15f, 0.18f), new Vector2(0.14f, 0.62f), new Vector2(0.1f, 0.74f), new Vector2(0.075f, 0.82f),
        };

        /// <summary>Called from BuildTerminalDetail with its batch <paramref name="terminal"/>, which bakes after this returns.</summary>
        void BuildPlaceDetail(Transform g, KitBatch terminal, Material concrete, Material metal, Color sodium)
        {
            var p = new NyPalette
            {
                Concrete = concrete,
                Metal = metal,
                // Linear colour space: textured materials show texture x tint, so their tints stay pale (the texture carries
                // the colour); plain colours are chosen a step lighter than they look in sRGB so they read at night.
                Steel = Mats.Lit(new Color(0.62f, 0.65f, 0.7f), 0.55f, 0.6f, "metal_brushed"),
                Frame = Mats.Lit(new Color(0.24f, 0.25f, 0.27f), 0.5f, 0.5f),
                Black = Mats.Lit(new Color(0.05f, 0.05f, 0.05f), 0.4f),
                White = Mats.Lit(new Color(0.85f, 0.85f, 0.82f), 0.3f),
                Yellow = Mats.Lit(new Color(0.85f, 0.65f, 0.12f), 0.35f),
                Red = Mats.Lit(new Color(0.62f, 0.07f, 0.05f), 0.5f),
                Orange = Mats.Lit(new Color(0.8f, 0.34f, 0.08f), 0.45f),
                Navy = Mats.Lit(new Color(0.12f, 0.24f, 0.42f), 0.4f),
                Green = Mats.Lit(new Color(0.08f, 0.36f, 0.22f), 0.45f),
                Granite = Mats.Lit(new Color(0.78f, 0.78f, 0.8f), 0.6f, 0f, "stone_dark_slate"),
                Chrome = Mats.Lit(new Color(0.72f, 0.73f, 0.75f), 0.8f, 0.8f, "metal_brushed"),
                Wood = Mats.Lit(new Color(0.95f, 0.9f, 0.85f), 0.3f, 0f, "wood_dark"),
                Rope = Mats.Lit(new Color(0.85f, 0.75f, 0.6f), 0.2f, 0f, "fabric_linen"),
                Glass = Mats.Glass(new Color(0.45f, 0.52f, 0.56f, 0.15f), 0.96f),
                DarkGlass = Mats.Lit(new Color(0.03f, 0.035f, 0.045f), 0.92f, 0.2f),
                Panel = Mats.Emissive(new Color(0.85f, 0.88f, 0.92f), new Color(1.1f, 1.15f, 1.22f)),
                Warm = Mats.Emissive(new Color(0.55f, 0.42f, 0.26f), new Color(1.0f, 0.68f, 0.34f)),
                Sodium = Mats.Emissive(new Color(0.5f, 0.36f, 0.2f), sodium * 1.35f),
            };
            BuildHall(g, p);
            BuildCanopyDetail(g, terminal, p);
            BuildBaggageAndWestWall(g, terminal, p);
            var harbour = new KitBatch();
            BuildHarbourEdge(g, harbour, p, sodium);
            BuildFerry(g, harbour, p);
            BuildIslandPier(g, harbour, p, sodium); // Detail_NewYorkWalk.cs: the way out to the statue
            harbour.Bake(g, "Harbour");
            BuildStreet(g, p, sodium);
        }

        // ------------------------------------------------------------------ the arrivals hall
        /// <summary>
        /// The back wall becomes a real frontage (playtest: the old glazing read as blank white screens): concrete piers on
        /// a granite plinth, mullioned windows of dark reflective glass and automatic doors behind the ARRIVALS portal. Through
        /// the glass, a lit customs hall: a panel ceiling, four immigration booths under lane numbers, an empty queue maze,
        /// seats, festival posters and a row of unclaimed cases standing to attention. The board and the hall sign hang on the
        /// band above the glazing; a plain upper storey closes the building above the canopy.
        /// </summary>
        void BuildHall(Transform g, NyPalette p)
        {
            var k = new KitBatch();
            var glass = new KitBatch();
            const float f0 = 7.62f, f1 = 7.95f; // frontage depth (the old wall keeps its collider, 7.65..7.95)
            float fz = (f0 + f1) / 2f, fd = f1 - f0;
            foreach (var pr in new[] { new Vector2(-6.0f, -5.7f), new Vector2(-3.9f, -3.6f), new Vector2(-1.8f, -1.5f), new Vector2(0.55f, 0.85f), new Vector2(3.55f, 3.85f), new Vector2(5.7f, 6.0f) })
                k.Box(p.Concrete, new Vector3((pr.x + pr.y) / 2f, 1.525f, fz), new Vector3(pr.y - pr.x, 2.45f, fd), 2f);
            k.Box(p.Granite, new Vector3(-2.575f, 0.15f, fz - 0.01f), new Vector3(6.85f, 0.3f, fd + 0.02f), 1.5f);
            k.Box(p.Granite, new Vector3(4.775f, 0.15f, fz - 0.01f), new Vector3(2.45f, 0.3f, fd + 0.02f), 1.5f);
            k.Box(p.Frame, new Vector3(0f, 2.9f, fz), new Vector3(12f, 0.3f, fd));
            k.Box(p.Concrete, new Vector3(0f, 3.625f, fz), new Vector3(12f, 1.15f, fd), 2f);

            void Window(float x0, float x1, float sill)
            {
                float cx = (x0 + x1) / 2f, w = x1 - x0, top = 2.75f, h = top - sill, cy = sill + h / 2f;
                glass.Box(p.Glass, new Vector3(cx, cy, 7.8f), new Vector3(w, h, 0.012f));
                k.Box(p.Frame, new Vector3(cx, sill + 0.03f, 7.74f), new Vector3(w, 0.06f, 0.14f));
                k.Box(p.Frame, new Vector3(cx, top - 0.03f, 7.76f), new Vector3(w, 0.06f, 0.1f));
                k.Box(p.Frame, new Vector3(x0 + 0.03f, cy, 7.76f), new Vector3(0.06f, h, 0.1f));
                k.Box(p.Frame, new Vector3(x1 - 0.03f, cy, 7.76f), new Vector3(0.06f, h, 0.1f));
                k.Box(p.Frame, new Vector3(cx, cy, 7.765f), new Vector3(0.05f, h, 0.08f));
                if (h > 1.8f) k.Box(p.Frame, new Vector3(cx, 2.2f, 7.765f), new Vector3(w, 0.05f, 0.08f));
            }
            Window(-5.7f, -3.9f, 0.3f);
            Window(-3.6f, -1.8f, 1.35f); // the baggage belt comes through the steel panel below
            k.Box(p.Steel, new Vector3(-2.7f, 0.825f, 7.78f), new Vector3(1.8f, 1.05f, 0.05f));
            Window(-1.5f, 0.55f, 0.3f);
            Window(3.85f, 5.7f, 0.3f);

            // Automatic doors behind the portal: two framed glass leaves, a header with its sensor, a steel threshold.
            k.Box(p.Frame, new Vector3(2.2f, 2.55f, 7.72f), new Vector3(2.7f, 0.4f, 0.25f));
            k.Box(p.DarkGlass, new Vector3(2.2f, 2.33f, 7.6f), new Vector3(0.32f, 0.05f, 0.08f));
            k.Box(p.Frame, new Vector3(0.9f, 1.175f, 7.76f), new Vector3(0.1f, 2.35f, 0.12f));
            k.Box(p.Frame, new Vector3(3.5f, 1.175f, 7.76f), new Vector3(0.1f, 2.35f, 0.12f));
            k.Box(p.Steel, new Vector3(2.2f, 0.01f, 7.78f), new Vector3(2.7f, 0.02f, 0.32f));
            foreach (var lx in new[] { 1.575f, 2.825f })
            {
                glass.Box(p.Glass, new Vector3(lx, 1.19f, 7.79f), new Vector3(1.25f, 2.3f, 0.012f));
                k.Box(p.Frame, new Vector3(lx, 0.08f, 7.79f), new Vector3(1.25f, 0.12f, 0.04f));
                k.Box(p.Frame, new Vector3(lx, 2.32f, 7.79f), new Vector3(1.25f, 0.05f, 0.04f));
                k.Box(p.Frame, new Vector3(lx - 0.6f, 1.19f, 7.79f), new Vector3(0.05f, 2.3f, 0.04f));
                k.Box(p.Frame, new Vector3(lx + 0.6f, 1.19f, 7.79f), new Vector3(0.05f, 2.3f, 0.04f));
            }
            Sign(g, "NO RE-ENTRY BEYOND THIS POINT", 0.06f, new Color(0.9f, 0.9f, 0.86f), new Vector3(2.2f, 2.56f, 7.588f), 0f, 2.5f);
            // A camera on the right pier watches the portal.
            var cam = new Vector3(4.25f, 3.18f, 7.42f);
            k.Box(p.Frame, cam + new Vector3(0f, 0f, 0.1f), new Vector3(0.04f, 0.04f, 0.2f));
            k.Box(p.Chrome, cam, new Vector3(0.12f, 0.12f, 0.3f), Quaternion.LookRotation(new Vector3(2.2f, 1.4f, 6.2f) - cam));

            // The arrivals board (rows in a fixed pitch, like a flap board) and the hall sign on the band.
            var board = new Vector3(-3.7f, 3.55f, 7.57f);
            k.Box(p.DarkGlass, board, new Vector3(3.5f, 0.86f, 0.1f));
            k.Box(p.Steel, board + new Vector3(0f, 0.45f, 0f), new Vector3(3.6f, 0.04f, 0.12f));
            k.Box(p.Steel, board + new Vector3(0f, -0.45f, 0f), new Vector3(3.6f, 0.04f, 0.12f));
            k.Box(p.Navy, board + new Vector3(0f, 0.36f, -0.052f), new Vector3(3.44f, 0.12f, 0.005f));
            // One text: the heading sits on the navy strip, the rows below it.
            var rows = Sign(g, "<mspace=0.6em><size=80%>ARRIVALS             PIER 12</size>\n<line-height=115%>CAIRO      FESTIVAL 09   12    ARRIVED\nPARIS      FESTIVAL 10   12    ARRIVED\n<color=#F3C14A>—          FESTIVAL 11   B12   LANDED\n—          FESTIVAL 12   B12   LANDED</color></mspace>",
                0.09f, new Color(0.95f, 0.95f, 0.9f), board + new Vector3(0f, 0.03f, -0.058f), 0f, 3.3f, TMPro.TextAlignmentOptions.TopLeft);
            rows.rectTransform.sizeDelta = new Vector2(3.3f, 0.8f);
            var hall = new Vector3(2.05f, 3.575f, 7.6f);
            k.Box(p.Navy, hall, new Vector3(7.3f, 0.55f, 0.06f));
            Sign(g, "INTERNATIONAL ARRIVALS  ·  BAGGAGE CLAIM", 0.17f, new Color(0.95f, 0.95f, 0.92f), hall + new Vector3(0f, 0f, -0.035f), 0f, 7.1f);

            // ---- The hall itself (only ever seen through the glass).
            // Few interior materials: every one is another draw call (walls and columns share the marble, the ceiling is the
            // canopy's concrete, counters are the wood, seats the navy).
            var terrazzo = Mats.Lit(new Color(0.9f, 0.88f, 0.84f), 0.55f, 0f, "tile_terrazzo");
            var marble = Mats.Lit(new Color(0.93f, 0.91f, 0.87f), 0.5f, 0f, "marble_white");
            var tile = marble;
            var ceiling = p.Concrete;
            var laminate = p.Wood;
            var seat = p.Navy;
            k.Box(terrazzo, new Vector3(0f, -0.02f, 11.65f), new Vector3(11.7f, 0.05f, 7.4f), 1.5f);
            k.Box(ceiling, new Vector3(0f, 3.65f, 11.65f), new Vector3(11.7f, 0.1f, 7.4f), 2f);
            k.Box(tile, new Vector3(-6.0f, 2.1f, 11.8f), new Vector3(0.3f, 4.2f, 7.7f), 1.5f);
            k.Box(tile, new Vector3(6.0f, 2.1f, 11.8f), new Vector3(0.3f, 4.2f, 7.7f), 1.5f);
            k.Box(tile, new Vector3(0f, 2.1f, 15.5f), new Vector3(12.3f, 4.2f, 0.3f), 1.5f);
            k.Box(p.Granite, new Vector3(-5.835f, 0.5f, 11.65f), new Vector3(0.03f, 1.0f, 7.4f));
            k.Box(p.Granite, new Vector3(5.835f, 0.5f, 11.65f), new Vector3(0.03f, 1.0f, 7.4f));
            k.Box(p.Granite, new Vector3(0f, 0.5f, 15.335f), new Vector3(11.7f, 1.0f, 0.03f));
            foreach (var x in new[] { -4.4f, -1.5f, 1.5f, 4.4f })
                foreach (var z in new[] { 9.0f, 11.2f, 13.4f })
                {
                    k.Box(p.Frame, new Vector3(x, 3.592f, z), new Vector3(1.12f, 0.016f, 0.62f));
                    k.Box(p.Panel, new Vector3(x, 3.582f, z), new Vector3(1.0f, 0.01f, 0.5f));
                }
            foreach (var x in new[] { -3.0f, 3.0f })
                foreach (var z in new[] { 10.2f, 13.6f })
                {
                    k.Of(marble).AddCylinder(new Vector3(x, 0f, z), 0.26f, 3.6f, 16);
                    k.Box(p.Steel, new Vector3(x, 0.06f, z), new Vector3(0.62f, 0.12f, 0.62f));
                    k.Box(p.Steel, new Vector3(x, 3.54f, z), new Vector3(0.62f, 0.12f, 0.62f));
                }
            // Immigration booths under lane numbers; one desk lamp left on.
            float[] boothX = { -4.2f, -1.4f, 1.4f, 4.2f };
            for (int i = 0; i < boothX.Length; i++)
            {
                var b = new Vector3(boothX[i], 0f, 12.3f);
                k.Box(laminate, b + new Vector3(0f, 0.55f, 0f), new Vector3(1.5f, 1.1f, 0.7f));
                k.Box(p.Steel, b + new Vector3(0f, 1.12f, -0.05f), new Vector3(1.56f, 0.04f, 0.8f));
                k.Box(p.Navy, b + new Vector3(0f, 1.1f, 0.5f), new Vector3(1.6f, 2.2f, 0.06f));
                k.Box(p.DarkGlass, b + new Vector3(0.35f, 1.34f, 0.12f), new Vector3(0.42f, 0.3f, 0.05f));
                k.Box(p.DarkGlass, b + new Vector3(0.35f, 1.17f, 0.14f), new Vector3(0.06f, 0.06f, 0.06f));
                var ls = new Vector3(boothX[i], 2.85f, 12.0f);
                k.Box(p.Navy, ls, new Vector3(0.62f, 0.4f, 0.06f));
                foreach (var hx in new[] { -0.22f, 0.22f }) k.Box(p.Steel, ls + new Vector3(hx, 0.475f, 0f), new Vector3(0.015f, 0.55f, 0.015f));
            }
            // The four lane numbers as one text: each digit placed over its sign (x = -4.2, -1.4, 1.4, 4.2 across 11.2 m).
            Sign(g, "<pos=11.8%>1<pos=36.8%>2<pos=61.8%>3<pos=86.8%>4", 0.26f, new Color(0.96f, 0.96f, 0.92f), new Vector3(0f, 2.85f, 11.965f), 0f, 11.2f, TMPro.TextAlignmentOptions.Left);
            var lamp = new Vector3(-1.85f, 1.14f, 12.32f);
            k.Box(p.DarkGlass, lamp + new Vector3(0f, 0.17f, 0f), new Vector3(0.02f, 0.34f, 0.02f));
            k.Of(p.Warm).AddFrustum(lamp + new Vector3(0f, 0.28f, 0f), 0.11f, 0.045f, 0.11f, 12);
            var customs = new Vector3(0f, 2.95f, 9.6f);
            k.Box(p.Navy, customs, new Vector3(7.0f, 0.42f, 0.06f));
            foreach (var hx in new[] { -2.2f, 0f, 2.2f }) k.Box(p.Steel, customs + new Vector3(hx, 0.43f, 0f), new Vector3(0.015f, 0.45f, 0.015f));
            Sign(g, "CUSTOMS  ·  IMMIGRATION  ·  ALL PASSPORTS", 0.16f, new Color(0.95f, 0.95f, 0.92f), customs + new Vector3(0f, 0f, -0.035f), 0f, 6.8f);
            // An empty queue maze between the doors and the booths.
            var post = p.Chrome;
            foreach (var rz in new[] { 9.3f, 10.75f })
            {
                bool second = rz > 10f;
                for (int i = 0; i < 9; i++)
                {
                    var q = new Vector3(-4.6f + i * 1.15f, 0f, rz);
                    k.Of(post).AddCylinder(q, 0.16f, 0.025f, 14);
                    k.Of(post).AddCylinder(q, 0.028f, 0.96f, 8);
                    bool gap = second ? i == 0 : i == 7; // the lanes turn at alternate ends
                    if (i < 8 && !gap) k.Box(p.Navy, q + new Vector3(0.575f, 0.9f, 0f), new Vector3(1.1f, 0.05f, 0.01f));
                }
            }
            // Seats along both side walls.
            foreach (var side in new[] { -1f, 1f })
            {
                var row = new Vector3(side * 5.35f, 0f, 11.2f);
                k.Box(p.Steel, row + new Vector3(0f, 0.22f, 0f), new Vector3(0.08f, 0.06f, 3.3f));
                for (int s = 0; s < 6; s++)
                {
                    var c = row + new Vector3(0f, 0f, -1.4f + s * 0.56f);
                    k.Box(seat, c + new Vector3(-side * 0.05f, 0.46f, 0f), new Vector3(0.46f, 0.07f, 0.5f));
                    k.Box(seat, c + new Vector3(side * 0.2f, 0.76f, 0f), new Vector3(0.06f, 0.52f, 0.5f));
                    k.Box(p.Steel, c + new Vector3(0f, 0.2f, 0f), new Vector3(0.05f, 0.4f, 0.05f));
                }
            }
            // Unclaimed cases in a perfectly straight row, facing the doors.
            for (int i = 0; i < 7; i++)
            {
                var c = new Vector3(-0.6f + i * 0.5f, 0f, 11.35f);
                k.Box(p.Frame, c + new Vector3(0f, 0.34f, 0f), new Vector3(0.42f, 0.62f, 0.24f));
                k.Box(p.DarkGlass, c + new Vector3(0f, 0.68f, 0f), new Vector3(0.16f, 0.04f, 0.04f));
            }
            Sign(g, "WELCOME TO NEW YORK", 0.24f, new Color(0.93f, 0.9f, 0.82f), new Vector3(0f, 3.2f, 15.33f), 0f, 6f);
            Poster(g, "postcard_1", new Vector3(-4.0f, 2.35f, 15.335f), Quaternion.identity, 1.28f);
            Poster(g, "postcard_3", new Vector3(4.0f, 2.35f, 15.335f), Quaternion.identity, 1.28f);
            // The hall is brighter than the canopy outside, so through the glass it reads as a lit room (its spill through
            // the wall also rims the figure standing in the portal). Wide spots aimed down: no hot spots on the ceiling.
            AddSpotLight(g, new Vector3(-2.4f, 3.45f, 11.0f), new Vector3(90f, 0f, 0f), new Color(0.92f, 0.95f, 1f), 2.4f, 9f, 150f);
            AddSpotLight(g, new Vector3(2.8f, 3.45f, 11.0f), new Vector3(90f, 0f, 0f), new Color(0.92f, 0.95f, 1f), 2.4f, 9f, 150f);

            // The upper storey above the canopy (only its edges show, from the harbour side).
            k.Box(p.Concrete, new Vector3(0f, 6.2f, 11.65f), new Vector3(12.3f, 4.0f, 8.0f), 3f);
            k.Box(p.Frame, new Vector3(0f, 8.27f, 11.65f), new Vector3(12.5f, 0.14f, 8.2f));
            for (int i = 0; i < 4; i++) k.Box(i == 1 ? p.Warm : p.DarkGlass, new Vector3(6.16f, 6.0f, 9.2f + i * 1.8f), new Vector3(0.02f, 1.4f, 1.2f));
            k.Bake(g, "ArrivalsHall");
            glass.Bake(g, "HallGlass", false);
        }

        // ------------------------------------------------------------------ canopy
        /// <summary>
        /// Canopy fittings: the harsh light strips become housed fluorescent fittings hung between the beams, the edges get a
        /// lettered fascia, wayfinding signs hang over the walk and the carousel, and the services read overhead: a sprinkler
        /// main, camera domes, an EXIT sign by the west wall.
        /// </summary>
        void BuildCanopyDetail(Transform g, KitBatch k, NyPalette p)
        {
            foreach (var x in new[] { -3.5f, 0f, 3.5f })
                foreach (var z in new[] { -0.15f, 1.95f, 4.05f, 6.15f })
                {
                    k.Box(p.Frame, new Vector3(x, 3.84f, z), new Vector3(0.26f, 0.08f, 1.7f));
                    k.Box(p.Panel, new Vector3(x, 3.795f, z), new Vector3(0.2f, 0.012f, 1.62f));
                    foreach (var dz in new[] { -0.6f, 0.6f }) k.Box(p.Steel, new Vector3(x, 3.965f, z + dz), new Vector3(0.012f, 0.17f, 0.012f));
                }
            // Fascia boards with the terminal's name.
            k.Box(p.Frame, new Vector3(0f, 4.12f, -1.56f), new Vector3(12.24f, 0.76f, 0.14f));
            k.Box(p.Frame, new Vector3(6.06f, 4.12f, 3.0f), new Vector3(0.14f, 0.76f, 9.26f));
            Sign(g, "HARBOR TERMINAL     ·     PIER 12", 0.28f, new Color(0.95f, 0.92f, 0.84f), new Vector3(0f, 4.12f, -1.64f), 0f, 11f);
            // Wayfinding over the walk: baggage to the left, the arrivals gate straight on.
            var way = new Vector3(1.0f, 3.35f, -0.45f);
            k.Box(p.Black, way, new Vector3(2.9f, 0.42f, 0.07f));
            foreach (var hx in new[] { -1.2f, 1.2f }) k.Box(p.Steel, way + new Vector3(hx, 0.455f, 0f), new Vector3(0.015f, 0.49f, 0.015f));
            var yellowText = new Color(0.98f, 0.8f, 0.22f);
            // One text, two alignments on the same line (line-height 0 keeps the second line on the first's baseline).
            Sign(g, "<align=left>←  BAGGAGE CLAIM<line-height=0>\n<align=right>ARRIVALS  ↑", 0.13f, yellowText, way + new Vector3(0f, 0f, -0.04f), 0f, 2.7f, TMPro.TextAlignmentOptions.Left);
            var bag = new Vector3(-2.8f, 3.3f, 0.9f); // hung from the beam, between two light fittings
            k.Box(p.Black, bag, new Vector3(1.9f, 0.42f, 0.07f));
            foreach (var hx in new[] { -0.8f, 0.8f }) k.Box(p.Steel, bag + new Vector3(hx, 0.4f, 0f), new Vector3(0.015f, 0.38f, 0.015f));
            Sign(g, "BAGGAGE CLAIM   3", 0.13f, yellowText, bag + new Vector3(0f, 0f, -0.04f), 0f, 1.8f);
            // Services: a sprinkler main between two beams, camera domes, the EXIT sign.
            k.Of(p.Red).AddCylinder(new Vector3(-6f, 3.97f, 2.0f), 0.035f, 12f, 8, 1f, Quaternion.Euler(0f, 0f, -90f));
            for (int i = 0; i < 4; i++) k.Box(p.Chrome, new Vector3(-4.5f + i * 3f, 3.91f, 2.0f), new Vector3(0.04f, 0.06f, 0.04f));
            foreach (var d in new[] { new Vector3(-4.8f, 4.05f, -0.6f), new Vector3(4.8f, 4.05f, 6.7f), new Vector3(0.6f, 4.05f, 6.7f) })
                k.Of(p.Black).AddSphere(d, 0.09f, 10, 5, 1f, -90f, 0f);
            var exit = new Vector3(-5.82f, 3.3f, 6.4f);
            k.Box(p.White, exit, new Vector3(0.06f, 0.24f, 0.55f));
            Sign(g, "EXIT", 0.13f, new Color(1f, 0.16f, 0.1f), exit + new Vector3(0.035f, 0f, 0f), -90f, 0.5f);
        }

        // ------------------------------------------------------------------ baggage claim and the west wall
        /// <summary>
        /// The carousel gets its stainless skirts, rubber bumpers and plate joints, and feeds back into the wall through a
        /// steel hood with a rubber flap curtain. The west wall (between the canopy columns, the hall seam left clear) gets a
        /// dark dado, a pair of payphones, a clock over the bench, a fire cabinet, a festival poster and the bags left by the
        /// corner where it retreats.
        /// </summary>
        void BuildBaggageAndWestWall(Transform g, KitBatch k, NyPalette p)
        {
            var bc = new Vector3(-2.8f, 0f, 4.2f);
            foreach (var side in new[] { -1f, 1f })
            {
                k.Box(p.Chrome, bc + new Vector3(side * 0.74f, 0.36f, 0f), new Vector3(0.03f, 0.56f, 5.5f), Quaternion.Euler(0f, 0f, side * -10f));
                k.Box(p.Black, bc + new Vector3(side * 0.66f, 0.735f, 0f), new Vector3(0.1f, 0.06f, 5.5f));
                k.Box(p.Black, bc + new Vector3(side * 0.73f, 0.04f, 0f), new Vector3(0.06f, 0.08f, 5.5f));
            }
            for (int i = 0; i < 21; i++) k.Box(p.Steel, bc + new Vector3(0f, 0.745f, -2.6f + i * 0.26f), new Vector3(1.18f, 0.008f, 0.02f));
            var hood = new Vector3(-2.8f, 0f, 7.285f);
            k.Box(p.Steel, hood + new Vector3(0f, 0.65f, 0f), new Vector3(1.7f, 1.3f, 0.67f));
            k.Box(p.Black, hood + new Vector3(0f, 0.93f, -0.337f), new Vector3(1.3f, 0.42f, 0.01f));
            for (int i = 0; i < 9; i++) k.Box(p.Black, hood + new Vector3(-0.6f + i * 0.15f, 0.93f, -0.347f), new Vector3(0.13f, 0.4f, 0.006f));
            for (int i = 0; i < 8; i++) k.Box(i % 2 == 0 ? p.Yellow : p.Black, hood + new Vector3(-0.75f + i * 0.214f, 1.27f, -0.338f), new Vector3(0.214f, 0.06f, 0.012f));
            Solid(g, hood + Vector3.up * 0.65f, new Vector3(1.7f, 1.3f, 0.67f));
            // A painted keep-back line round the carousel, two more bags riding the belt.
            k.Box(p.Yellow, new Vector3(-3.95f, 0.004f, 4.0f), new Vector3(0.08f, 0.004f, 5.9f));
            k.Box(p.Yellow, new Vector3(-1.65f, 0.004f, 4.0f), new Vector3(0.08f, 0.004f, 5.9f));
            k.Box(p.Yellow, new Vector3(-2.8f, 0.004f, 1.05f), new Vector3(2.38f, 0.004f, 0.08f));
            k.Of(p.Navy).AddCylinder(new Vector3(-3.05f, 0.89f, 6.42f), 0.15f, 0.52f, 12, 1f, Quaternion.Euler(0f, 0f, -90f));
            k.Box(p.Navy, new Vector3(-2.88f, 0.86f, 3.3f), new Vector3(0.4f, 0.24f, 0.55f), Quaternion.Euler(0f, 12f, 0f));

            // West wall (face x = -5.85). Columns stand at z = -1, 1.8, 4.6, 7.4; the hall seam (z = 4.8) stays bare.
            const float wf = -5.85f;
            k.Box(p.Granite, new Vector3(wf + 0.015f, 0.45f, 3.0f), new Vector3(0.03f, 0.9f, 9.6f));
            k.Box(p.Frame, new Vector3(wf + 0.02f, 0.92f, 3.0f), new Vector3(0.04f, 0.04f, 9.6f));
            foreach (var z in new[] { 0.78f, 1.24f })
            {
                var ph = new Vector3(wf, 0f, z);
                k.Box(p.Steel, ph + new Vector3(0.1f, 1.35f, 0f), new Vector3(0.2f, 0.62f, 0.34f));
                k.Box(p.Black, ph + new Vector3(0.205f, 1.5f, 0.03f), new Vector3(0.012f, 0.14f, 0.16f));
                k.Box(p.Black, ph + new Vector3(0.205f, 1.26f, 0.03f), new Vector3(0.012f, 0.18f, 0.12f));
                k.Box(p.Black, ph + new Vector3(0.23f, 1.4f, -0.12f), new Vector3(0.06f, 0.26f, 0.06f));
                k.Box(p.Steel, ph + new Vector3(0.21f, 1.83f, 0f), new Vector3(0.42f, 0.03f, 0.44f));
                k.Box(p.Steel, ph + new Vector3(0.21f, 1.46f, 0.215f), new Vector3(0.42f, 0.76f, 0.012f));
            }
            k.Box(p.Navy, new Vector3(wf + 0.012f, 2.07f, 1.01f), new Vector3(0.02f, 0.2f, 0.82f));
            Sign(g, "TELEPHONE", 0.09f, new Color(0.95f, 0.95f, 0.92f), new Vector3(wf + 0.026f, 2.07f, 1.01f), -90f, 0.8f);
            // A clock over the bench, a fire cabinet on the column, a festival poster by the corner.
            var clock = new Vector3(wf + 0.03f, 2.55f, 3.1f);
            k.Of(p.White).AddCylinder(clock, 0.27f, 0.03f, 24, 1f, Quaternion.Euler(0f, 0f, -90f));
            k.Of(p.Black).AddTorus(clock + new Vector3(0.03f, 0f, 0f), 0.27f, 0.025f, 24, 6, Quaternion.Euler(0f, 0f, 90f));
            k.Box(p.Black, clock + new Vector3(0.04f, Mathf.Cos(96f * Mathf.Deg2Rad) * 0.07f, Mathf.Sin(96f * Mathf.Deg2Rad) * 0.07f), new Vector3(0.008f, 0.14f, 0.022f), Quaternion.Euler(96f, 0f, 0f));
            k.Box(p.Black, clock + new Vector3(0.045f, Mathf.Cos(72f * Mathf.Deg2Rad) * 0.1f, Mathf.Sin(72f * Mathf.Deg2Rad) * 0.1f), new Vector3(0.008f, 0.2f, 0.014f), Quaternion.Euler(72f, 0f, 0f));
            k.Box(p.Red, new Vector3(-5.29f, 1.3f, 1.8f), new Vector3(0.07f, 0.62f, 0.3f));
            k.Box(p.White, new Vector3(-5.252f, 1.42f, 1.8f), new Vector3(0.005f, 0.3f, 0.22f));
            k.Box(p.Frame, new Vector3(wf + 0.006f, 1.55f, 6.4f), new Vector3(0.012f, 0.72f, 1.12f));
            Poster(g, "postcard_2", new Vector3(wf + 0.014f, 1.55f, 6.4f), Quaternion.Euler(0f, -90f, 0f), 1.04f);
            // The west side reads at a glance: warm light over the carousel end, the bags and the poster.
            AddPointLight(g, new Vector3(-3.9f, 3.0f, 5.4f), new Color(1f, 0.86f, 0.66f), 1.2f, 5.5f);
        }

        // ------------------------------------------------------------------ harbour edge
        /// <summary>
        /// The quay below the railing: a concrete face with a timber curb and a yellow edge, fender piles and wales, tyre
        /// fenders, a ladder and coiled lines at the mooring posts. Cast-iron pier lamps replace the plain posts (a bishop's
        /// crook in the open, short acorn posts under the canopy edge). North-east, an empty ferry slip: lattice towers and a
        /// gantry over the hinged apron, fender pile racks, channel lights and buoys.
        /// </summary>
        void BuildHarbourEdge(Transform g, KitBatch k, NyPalette p, Color sodium)
        {
            // The island pier leaves the railing at z -4.95..-1.12 (Detail_NewYorkWalk.cs): curb, fender piles, ladder and
            // the lamp all keep clear of it.
            k.Box(p.Concrete, new Vector3(7.05f, -0.95f, -11f), new Vector3(0.2f, 1.9f, 62f), 2f);
            foreach (var (z0, z1) in new[] { (-42f, -4.95f), (-1.1f, 20f) })
            {
                k.Box(p.Wood, new Vector3(6.95f, 0.06f, (z0 + z1) / 2f), new Vector3(0.4f, 0.12f, z1 - z0), 1f);
                k.Box(p.Yellow, new Vector3(6.95f, 0.122f, (z0 + z1) / 2f), new Vector3(0.4f, 0.004f, z1 - z0));
            }
            for (float z = -41f; z <= 19.5f; z += 1.6f)
                if (z < -5.3f || z > -0.8f) k.Of(p.Wood).AddCylinder(new Vector3(7.3f, -2.0f, z), 0.14f, 2.25f, 8);
            foreach (var y in new[] { -0.55f, -1.15f }) k.Box(p.Wood, new Vector3(7.46f, y, -11f), new Vector3(0.18f, 0.22f, 62f), 1f);
            for (float z = -38f; z <= 18f; z += 4.8f) k.Of(p.Black).AddTorus(new Vector3(7.64f, -0.85f, z), 0.3f, 0.1f, 14, 6, Quaternion.Euler(0f, 0f, 90f));
            // A ladder down to the water.
            foreach (var dz in new[] { -0.22f, 0.22f })
            {
                k.Box(p.Steel, new Vector3(7.2f, -0.5f, 7.0f + dz), new Vector3(0.04f, 2.8f, 0.04f));
                k.Box(p.Steel, new Vector3(7.05f, 0.9f, 7.0f + dz), new Vector3(0.34f, 0.04f, 0.04f));
            }
            for (int i = 0; i < 7; i++) k.Box(p.Steel, new Vector3(7.2f, -1.7f + i * 0.3f, 7.0f), new Vector3(0.03f, 0.03f, 0.44f));
            // Lines coiled at the mooring posts.
            foreach (var z in new[] { -5.5f, 0.5f, 5f })
                for (int i = 0; i < 3; i++) k.Of(p.Rope).AddTorus(new Vector3(6.88f, 0.15f + i * 0.045f, z + (z < 0f ? -0.5f : 0.5f)), 0.17f - i * 0.025f, 0.025f, 14, 4, Quaternion.identity);
            // Pier lamps: the crook stands just south of the pier gate.
            CrookLamp(k, p, new Vector3(6.15f, 0f, -5.3f));
            AddPointLight(g, new Vector3(5.42f, 3.6f, -5.3f), sodium, 2.4f, 8f);
            Solid(g, new Vector3(6.15f, 1.9f, -5.3f), new Vector3(0.3f, 3.8f, 0.3f));
            foreach (var z in new[] { 1.5f, 6f })
            {
                AcornLamp(k, p, new Vector3(5.6f, 0f, z));
                AddPointLight(g, new Vector3(5.6f, 3.42f, z), sodium, 2.4f, 8f);
                Solid(g, new Vector3(5.6f, 1.7f, z), new Vector3(0.3f, 3.4f, 0.3f));
            }

            // ---- The empty slip.
            var s = k;
            var red = Mats.Emissive(new Color(0.5f, 0.08f, 0.06f), new Color(1.4f, 0.14f, 0.09f));
            var green = Mats.Emissive(new Color(0.08f, 0.4f, 0.15f), new Color(0.15f, 1.4f, 0.45f));
            foreach (var tz in new[] { 10.6f, 17.4f })
            {
                var t = new Vector3(8.9f, 0f, tz);
                for (int cx = -1; cx <= 1; cx += 2)
                    for (int cz = -1; cz <= 1; cz += 2)
                        s.Box(p.Steel, t + new Vector3(cx * 0.45f, 3.75f, cz * 0.45f), new Vector3(0.16f, 10.5f, 0.16f));
                for (int lv = 0; lv < 6; lv++)
                {
                    float y0 = -1.0f + lv * 1.6f;
                    foreach (var f in new[] { -0.45f, 0.45f })
                    {
                        s.Box(p.Steel, t + new Vector3(0f, y0, f), new Vector3(0.9f, 0.07f, 0.07f));
                        s.Box(p.Steel, t + new Vector3(f, y0, 0f), new Vector3(0.07f, 0.07f, 0.9f));
                        foreach (var a in new[] { 29.4f, -29.4f })
                        {
                            s.Box(p.Steel, t + new Vector3(f, y0 + 0.8f, 0f), new Vector3(0.05f, 1.84f, 0.05f), Quaternion.Euler(a, 0f, 0f));
                            s.Box(p.Steel, t + new Vector3(0f, y0 + 0.8f, f), new Vector3(0.05f, 1.84f, 0.05f), Quaternion.Euler(0f, 0f, a));
                        }
                    }
                }
                s.Box(p.Frame, t + new Vector3(0f, 9.05f, 0f), new Vector3(1.1f, 0.12f, 1.1f));
            }
            s.Box(p.Steel, new Vector3(8.9f, 9.25f, 14f), new Vector3(0.22f, 0.2f, 7.9f));
            s.Box(p.Steel, new Vector3(8.9f, 8.45f, 14f), new Vector3(0.22f, 0.18f, 5.9f));
            for (int i = 0; i < 6; i++)
                s.Box(p.Steel, new Vector3(8.9f, 8.85f, 11.55f + i * 0.98f), new Vector3(0.06f, 0.95f, 0.06f), Quaternion.Euler(i % 2 == 0 ? 45f : -45f, 0f, 0f));
            s.Box(p.Concrete, new Vector3(8.9f, 7.0f, 10.6f), new Vector3(0.62f, 1.3f, 0.62f), 1f);
            // The hinged apron, hung from the gantry on two cables.
            var dip = Quaternion.Euler(0f, 0f, -3f);
            s.Box(p.Steel, new Vector3(9.6f, -0.08f, 14f), new Vector3(4.9f, 0.16f, 3.6f), dip);
            foreach (var ez in new[] { 12.25f, 15.75f })
            {
                s.Box(p.Frame, new Vector3(9.6f, 0.16f, ez), new Vector3(4.9f, 0.32f, 0.1f), dip);
                s.Box(p.Yellow, new Vector3(9.6f, 0.92f, ez), new Vector3(4.9f, 0.05f, 0.05f), dip);
                for (int i = 0; i < 5; i++) s.Box(p.Frame, new Vector3(7.5f + i * 1.05f, 0.6f - i * 0.055f, ez), new Vector3(0.04f, 0.65f, 0.04f));
                s.Box(p.Steel, new Vector3(10.45f, 4.15f, ez), new Vector3(0.025f, 9.05f, 0.025f), Quaternion.Euler(0f, 0f, 20f)); // cable from the gantry to the apron's end
            }
            foreach (var rz in new[] { 10.05f, 17.95f })
            {
                for (float x = 10.2f; x <= 25.5f; x += 1.1f) s.Of(p.Wood).AddCylinder(new Vector3(x, -2.0f, rz), 0.15f, 3.2f, 8);
                s.Box(p.Wood, new Vector3(17.85f, 0.7f, rz), new Vector3(15.6f, 0.24f, 0.2f), 1f);
                s.Box(p.Wood, new Vector3(17.85f, -0.25f, rz), new Vector3(15.6f, 0.24f, 0.2f), 1f);
            }
            foreach (var (dz, glow) in new[] { (10.05f, red), (17.95f, green) })
            {
                var d = new Vector3(26.5f, 0f, dz);
                for (int i = 0; i < 5; i++)
                {
                    float a = i * 72f * Mathf.Deg2Rad;
                    s.Of(p.Wood).AddCylinder(d + new Vector3(Mathf.Cos(a) * 0.32f, -2.0f, Mathf.Sin(a) * 0.32f), 0.16f, 3.7f, 8);
                }
                s.Of(p.Wood).AddCylinder(d + new Vector3(0f, -2.0f, 0f), 0.18f, 4.1f, 8);
                s.Of(p.Frame).AddCylinder(d + new Vector3(0f, 2.1f, 0f), 0.05f, 0.5f, 6);
                s.Of(glow).AddSphere(d + new Vector3(0f, 2.68f, 0f), 0.1f, 8, 6);
            }
            // Channel buoys further out (their small lights are the only things moving on the water at night).
            foreach (var (b, glow, cone) in new[] { (new Vector3(19f, 0f, 25f), green, true), (new Vector3(28f, 0f, 31f), red, false) })
            {
                if (cone) s.Of(p.Frame).AddFrustum(b + new Vector3(0f, -1.9f, 0f), 0.5f, 0.12f, 1.4f, 12);
                else s.Of(p.Orange).AddCylinder(b + new Vector3(0f, -1.9f, 0f), 0.45f, 1.2f, 12);
                s.Box(p.Frame, b + new Vector3(0f, -0.3f, 0f), new Vector3(0.05f, 1.0f, 0.05f));
                s.Of(glow).AddSphere(b + new Vector3(0f, 0.25f, 0f), 0.09f, 8, 6);
            }
            var slip = new Vector3(8.9f, 6.4f, 9.99f);
            s.Box(p.Frame, slip, new Vector3(1.5f, 0.6f, 0.05f));
            Sign(g, "SLIP 2", 0.32f, new Color(0.95f, 0.93f, 0.86f), slip + new Vector3(0f, 0f, -0.03f), 0f, 1.4f);
        }

        static void CrookLamp(KitBatch k, NyPalette p, Vector3 b)
        {
            var iron = p.Frame;
            k.Of(iron).AddLathe(b, s_LampBase, 12);
            k.Of(iron).AddCylinder(b + Vector3.up * 0.8f, 0.06f, 2.95f, 10);
            foreach (var y in new[] { 1.4f, 3.7f }) k.Of(iron).AddCylinder(b + Vector3.up * y, 0.08f, 0.06f, 10);
            // The crook: up, over and down toward the walk (-X), with a scroll inside it.
            k.Of(iron).AddTube(new[] { b + new Vector3(0f, 3.72f, 0f), b + new Vector3(0f, 4.1f, 0f), b + new Vector3(-0.07f, 4.3f, 0f), b + new Vector3(-0.24f, 4.4f, 0f), b + new Vector3(-0.48f, 4.37f, 0f), b + new Vector3(-0.66f, 4.24f, 0f), b + new Vector3(-0.73f, 4.05f, 0f) }, 0.032f, 8);
            k.Of(iron).AddTube(new[] { b + new Vector3(0f, 3.85f, 0f), b + new Vector3(-0.18f, 3.95f, 0f), b + new Vector3(-0.3f, 4.12f, 0f), b + new Vector3(-0.24f, 4.25f, 0f), b + new Vector3(-0.14f, 4.2f, 0f) }, 0.015f, 6);
            var l = b + new Vector3(-0.73f, 0f, 0f);
            k.Of(iron).AddFrustum(l + Vector3.up * 3.82f, 0.19f, 0.05f, 0.2f, 12);
            k.Of(p.Sodium).AddSphere(l + Vector3.up * 3.66f, 0.15f, 12, 8);
            k.Of(iron).AddSphere(l + Vector3.up * 3.48f, 0.03f, 6, 4);
        }

        static void AcornLamp(KitBatch k, NyPalette p, Vector3 b)
        {
            var iron = p.Frame;
            k.Of(iron).AddLathe(b, s_LampBase, 12);
            k.Of(iron).AddCylinder(b + Vector3.up * 0.8f, 0.06f, 2.4f, 10);
            k.Of(iron).AddCylinder(b + Vector3.up * 1.4f, 0.08f, 0.06f, 10);
            k.Of(iron).AddFrustum(b + Vector3.up * 3.15f, 0.07f, 0.12f, 0.1f, 12);
            k.Of(p.Sodium).AddSphere(b + Vector3.up * 3.42f, 0.17f, 12, 8);
            k.Of(iron).AddFrustum(b + Vector3.up * 3.55f, 0.13f, 0.02f, 0.14f, 12);
            k.Of(iron).AddSphere(b + Vector3.up * 3.72f, 0.03f, 6, 4);
        }

        // ------------------------------------------------------------------ the moored ferry
        /// <summary>
        /// A harbour ferry moored along the quay south of the fence, bow toward the terminal: an orange hull with lit
        /// portholes, two white cabin decks with rows of lit and dark windows, a wheelhouse and a mast with its running
        /// lights, life rings, a gangplank down to the quay and its lines to two bollards. Nobody aboard. It sits south-east
        /// of the play area, clear of the statue on the horizon from every standing point.
        /// </summary>
        void BuildFerry(Transform g, KitBatch k, NyPalette p)
        {
            var cream = p.White;
            var deck = p.Concrete;
            var hullPlan = new List<Vector2>
            {
                new Vector2(7.9f, -36.5f), new Vector2(8.4f, -38.0f), new Vector2(11.0f, -38.6f), new Vector2(13.6f, -38.0f), new Vector2(14.1f, -36.5f),
                new Vector2(14.1f, -14.5f), new Vector2(12.6f, -13.0f), new Vector2(11.0f, -12.5f), new Vector2(9.4f, -13.0f), new Vector2(7.9f, -14.5f),
            };
            k.Of(p.Orange).AddPrism(hullPlan, -2.2f, 0.42f, 2f);
            k.Of(deck).AddPrism(hullPlan, 0.42f, 0.5f, 2f);
            foreach (var x in new[] { 7.88f, 14.12f }) k.Box(p.White, new Vector3(x, 0.32f, -25.5f), new Vector3(0.06f, 0.18f, 22f));
            for (float z = -35.5f; z <= -15.2f; z += 1.1f) k.Of(p.Warm).AddDisc(new Vector3(7.885f, -0.45f, z), Vector3.left, 0.14f, 10);
            // Cabin decks.
            k.Box(cream, new Vector3(11f, 1.7f, -25.5f), new Vector3(5f, 2.4f, 18f));
            k.Box(p.White, new Vector3(11f, 2.95f, -25.5f), new Vector3(5.6f, 0.1f, 19f));
            k.Box(cream, new Vector3(11f, 4.0f, -24.75f), new Vector3(3.6f, 2.0f, 11.5f));
            k.Box(p.White, new Vector3(11f, 5.05f, -24.75f), new Vector3(4.0f, 0.1f, 12f));
            k.Box(cream, new Vector3(11f, 5.9f, -17.75f), new Vector3(2.8f, 1.6f, 2.5f));
            k.Box(p.White, new Vector3(11f, 6.75f, -17.75f), new Vector3(3.2f, 0.1f, 2.9f));
            var rnd = new System.Random(1212);
            for (int i = 0; i < 15; i++)
            {
                float z = -33.8f + i * 1.15f;
                k.Box(rnd.NextDouble() < 0.6 ? p.Warm : p.Frame, new Vector3(8.49f, 1.85f, z), new Vector3(0.02f, 0.9f, 0.9f));
            }
            for (int i = 0; i < 8; i++)
            {
                float z = -29.5f + i * 1.25f;
                k.Box(rnd.NextDouble() < 0.45 ? p.Warm : p.Frame, new Vector3(9.19f, 4.05f, z), new Vector3(0.02f, 0.8f, 1.0f));
            }
            k.Box(p.Frame, new Vector3(11f, 6.05f, -16.49f), new Vector3(2.6f, 0.65f, 0.02f));
            k.Box(p.Frame, new Vector3(9.59f, 6.05f, -17.75f), new Vector3(0.02f, 0.65f, 2.2f));
            // Deck rails on the quay side and round the foredeck.
            for (int i = 0; i <= 16; i++) k.Box(p.White, new Vector3(8.25f, 3.45f, -34.5f + i * 1.1f), new Vector3(0.03f, 0.9f, 0.03f));
            k.Box(p.White, new Vector3(8.25f, 3.9f, -25.7f), new Vector3(0.04f, 0.04f, 17.6f));
            var bowRail = new[] { new Vector3(8.05f, 1.4f, -14.75f), new Vector3(9.45f, 1.4f, -13.15f), new Vector3(11f, 1.4f, -12.65f), new Vector3(12.55f, 1.4f, -13.15f), new Vector3(13.95f, 1.4f, -14.75f), new Vector3(13.95f, 1.4f, -16.4f) };
            k.Of(p.White).AddTube(bowRail, 0.022f, 6);
            foreach (var bp in bowRail) k.Box(p.White, bp + Vector3.down * 0.45f, new Vector3(0.03f, 0.9f, 0.03f));
            k.Box(p.White, new Vector3(8.05f, 1.4f, -16.2f), new Vector3(0.04f, 0.04f, 0.5f)); // the gap at the gangplank
            k.Box(p.White, new Vector3(8.05f, 0.95f, -16.45f), new Vector3(0.03f, 0.9f, 0.03f));
            // Mast and running lights: white masthead, red to port (west), green to starboard.
            k.Of(p.Frame).AddCylinder(new Vector3(11f, 6.8f, -18.2f), 0.06f, 3.0f, 8);
            k.Box(p.Frame, new Vector3(11f, 8.6f, -18.2f), new Vector3(1.4f, 0.06f, 0.06f));
            k.Of(p.Warm).AddSphere(new Vector3(11f, 9.86f, -18.2f), 0.08f, 8, 6);
            k.Of(Mats.Emissive(new Color(0.5f, 0.08f, 0.06f), new Color(1.4f, 0.14f, 0.09f))).AddSphere(new Vector3(9.5f, 6.4f, -17.0f), 0.07f, 8, 6);
            k.Of(Mats.Emissive(new Color(0.08f, 0.4f, 0.15f), new Color(0.15f, 1.4f, 0.45f))).AddSphere(new Vector3(12.5f, 6.4f, -17.0f), 0.07f, 8, 6);
            foreach (var z in new[] { -31f, -25f, -19.5f }) k.Of(p.Orange).AddTorus(new Vector3(8.45f, 1.0f, z), 0.28f, 0.07f, 16, 6, Quaternion.Euler(0f, 0f, 90f));
            // Gangplank to the quay and the mooring lines to two bollards.
            var plank = Quaternion.Euler(0f, 0f, 17f);
            k.Box(p.Steel, new Vector3(7.75f, 0.32f, -15.3f), new Vector3(1.5f, 0.06f, 0.9f), plank);
            foreach (var dz in new[] { -0.45f, 0.45f })
            {
                k.Box(p.Steel, new Vector3(7.75f, 1.2f, -15.3f + dz), new Vector3(1.5f, 0.04f, 0.04f), plank);
                foreach (var dx in new[] { -0.6f, 0.6f }) k.Box(p.Steel, new Vector3(7.75f + dx, 0.75f + dx * 0.3f, -15.3f + dz), new Vector3(0.035f, 0.9f, 0.035f));
            }
            foreach (var (bz, hold) in new[] { (-12.6f, new Vector3(9.1f, 0.55f, -13.7f)), (-38.2f, new Vector3(8.6f, 0.55f, -37.0f)) })
            {
                var bol = new Vector3(6.85f, 0f, bz);
                k.Of(p.Frame).AddFrustum(bol, 0.18f, 0.14f, 0.45f, 12);
                k.Of(p.Frame).AddCylinder(bol + Vector3.up * 0.45f, 0.21f, 0.07f, 12);
                k.Of(p.Rope).AddTube(Sag(bol + Vector3.up * 0.38f, hold, 0.25f, 8), 0.025f, 5);
            }
            Sign(g, "No. 12", 0.32f, new Color(0.95f, 0.94f, 0.9f), new Vector3(7.86f, -0.05f, -16.0f), 90f, 1.6f);
            AddPointLight(g, new Vector3(8.6f, 2.2f, -15.6f), new Color(1f, 0.78f, 0.5f), 1.3f, 8f);
        }

        // ------------------------------------------------------------------ the street outside
        /// <summary>
        /// Beyond the bounds the place carries on: the quay continues south behind a steel palisade with a gate, and a street
        /// with a double yellow line runs past the terminal's west end with a cab waiting at the kerb. Inside the fence,
        /// by the arrival: a cobra-head street light, an orange and white steam stack venting into the night, a hydrant and
        /// the corner's street signs, a wire litter basket by the booth.
        /// </summary>
        void BuildStreet(Transform g, NyPalette p, Color sodium)
        {
            var asphalt = Mats.Lit(new Color(0.92f, 0.92f, 0.95f), 0.7f, 0f, "asphalt_wet");
            MeshFactory.Box("QuaySouth", g, new Vector3(0.075f, -0.15f, -24.1f), new Vector3(14.15f, 0.3f, 36.2f), asphalt, false, 1.2f);
            MeshFactory.Box("StreetWest", g, new Vector3(-12.75f, -0.15f, -2f), new Vector3(11.5f, 0.3f, 36f), asphalt, false, 1.2f);
            var k = new KitBatch();
            k.Box(p.Concrete, new Vector3(-7.7f, 0.06f, -2f), new Vector3(1.4f, 0.12f, 36f), 2f);
            k.Box(p.Concrete, new Vector3(-17.8f, 0.06f, -2f), new Vector3(1.4f, 0.12f, 36f), 2f);
            foreach (var dx in new[] { -0.08f, 0.08f }) k.Box(p.Yellow, new Vector3(-12.6f + dx, 0.003f, -2f), new Vector3(0.1f, 0.004f, 36f));
            for (int i = 0; i < 10; i++) k.Box(p.White, new Vector3(-15.6f, 0.003f, -18f + i * 3.6f), new Vector3(0.12f, 0.004f, 1.8f));
            // The far side of the street: a brick tenement with its fire escape between a lower block and a warehouse,
            // shutters down at street level and one corner shop still lit.
            var brick = Mats.Lit(new Color(0.85f, 0.75f, 0.7f), 0.3f, 0f, "brick_london");
            var rw = new System.Random(77);
            foreach (var (z0, z1, h, m) in new[] { (-20f, -9f, 11f, brick), (-9f, 3f, 14f, brick), (3f, 16f, 9.5f, brick) })
            {
                float cz = (z0 + z1) / 2f, len = z1 - z0;
                k.Box(m, new Vector3(-21.25f, h / 2f, cz), new Vector3(5.5f, h, len), 1.4f);
                k.Box(p.Frame, new Vector3(-18.42f, h + 0.15f, cz), new Vector3(0.3f, 0.3f, len));
                for (float y = 4.2f; y + 1.6f < h; y += 3.2f)
                    for (float z = z0 + 1.3f; z < z1 - 0.8f; z += 2.2f)
                    {
                        k.Box(rw.NextDouble() < 0.25 ? p.Warm : p.DarkGlass, new Vector3(-18.49f, y + 0.8f, z), new Vector3(0.02f, 1.6f, 1.0f));
                        k.Box(p.Concrete, new Vector3(-18.46f, y - 0.03f, z), new Vector3(0.08f, 0.08f, 1.2f));
                    }
            }
            for (int i = 0; i < 4; i++)
            {
                float z = -7.5f + i * 2.6f;
                if (i == 2)
                {
                    k.Box(p.Warm, new Vector3(-18.49f, 1.5f, z), new Vector3(0.02f, 2.2f, 2.2f));
                    k.Box(p.Frame, new Vector3(-18.47f, 1.5f, z), new Vector3(0.03f, 2.2f, 0.06f));
                }
                else
                {
                    k.Box(p.Steel, new Vector3(-18.47f, 1.5f, z), new Vector3(0.04f, 2.6f, 2.3f));
                    for (int gr = 0; gr < 12; gr++) k.Box(p.Frame, new Vector3(-18.445f, 0.35f + gr * 0.2f, z), new Vector3(0.01f, 0.02f, 2.3f));
                }
                k.Box(p.Frame, new Vector3(-18.42f, 2.95f, z), new Vector3(0.12f, 0.4f, 2.5f));
            }
            Sign(g, "OPEN", 0.22f, new Color(1f, 0.25f, 0.2f), new Vector3(-18.46f, 1.95f, -2.3f), -90f, 1f);
            for (int n = 0; n < 3; n++)
            {
                float y = 3.5f + n * 3.2f;
                k.Box(p.Frame, new Vector3(-18.05f, y, -4f), new Vector3(0.9f, 0.05f, 5f));
                for (int b = 0; b <= 33; b++) k.Box(p.Frame, new Vector3(-17.62f, y + 0.5f, -6.5f + b * 0.15f), new Vector3(0.015f, 1.0f, 0.015f));
                k.Box(p.Frame, new Vector3(-17.62f, y + 1.0f, -4f), new Vector3(0.04f, 0.04f, 5f));
                foreach (var ez in new[] { -6.5f, -1.5f }) k.Box(p.Frame, new Vector3(-18.05f, y + 1.0f, ez), new Vector3(0.9f, 0.04f, 0.04f));
                if (n < 2) k.Box(p.Frame, new Vector3(-18.1f, y + 1.6f, -3.2f), new Vector3(0.6f, 0.05f, 4.12f), Quaternion.Euler(50.9f, 0f, 0f));
            }
            foreach (var dz in new[] { -6.35f, -5.95f }) k.Box(p.Frame, new Vector3(-17.7f, 2.4f, dz), new Vector3(0.03f, 2.2f, 0.03f));
            // The security fence behind the arrival (a footing strip hides the join with the quay beyond).
            k.Box(p.Concrete, new Vector3(0.03f, 0.04f, -5.95f), new Vector3(13.1f, 0.08f, 0.3f), 2f);
            k.Box(p.Concrete, new Vector3(-6.45f, 0.04f, -3.88f), new Vector3(0.3f, 0.08f, 4.2f), 2f);
            Palisade(k, p.Frame, new Vector3(-6.45f, 0f, -5.95f), new Vector3(6.5f, 0f, -5.95f), 2.2f);
            Palisade(k, p.Frame, new Vector3(6.5f, 0f, -5.95f), new Vector3(6.5f, 0f, -5.05f), 2.2f);
            Palisade(k, p.Frame, new Vector3(-6.45f, 0f, -5.95f), new Vector3(-6.45f, 0f, -1.8f), 2.2f);
            foreach (var gx in new[] { -1.9f, 1.9f }) k.Box(p.Frame, new Vector3(gx, 1.2f, -5.95f), new Vector3(0.12f, 2.4f, 0.12f));
            k.Box(p.Frame, new Vector3(0f, 2.0f, -5.95f), new Vector3(3.8f, 0.07f, 0.07f));
            k.Box(p.Frame, new Vector3(0f, 1.1f, -5.95f), new Vector3(4.2f, 0.06f, 0.06f), Quaternion.Euler(0f, 0f, 26f));
            k.Box(p.White, new Vector3(0f, 1.45f, -5.92f), new Vector3(1.5f, 0.42f, 0.02f));
            Sign(g, "PIER 12\nAUTHORIZED PERSONNEL ONLY", 0.07f, new Color(0.1f, 0.1f, 0.12f), new Vector3(0f, 1.45f, -5.905f), 180f, 1.4f);

            // Cobra-head street light over the arrival.
            var pole = new Vector3(-6.0f, 0f, -4.6f);
            k.Of(p.Steel).AddFrustum(pole, 0.11f, 0.06f, 4.55f, 10);
            k.Box(p.Steel, pole + new Vector3(0.75f, 4.62f, 0f), new Vector3(1.55f, 0.08f, 0.08f), Quaternion.Euler(0f, 0f, 4f));
            k.Box(p.Steel, pole + new Vector3(1.6f, 4.66f, 0f), new Vector3(0.62f, 0.13f, 0.3f));
            k.Box(p.Sodium, pole + new Vector3(1.62f, 4.59f, 0f), new Vector3(0.46f, 0.02f, 0.22f));
            AddPointLight(g, pole + new Vector3(1.6f, 4.4f, 0f), sodium, 2.2f, 10f);
            Solid(g, pole + Vector3.up * 1.5f, new Vector3(0.25f, 3f, 0.25f));
            // A steam stack, striped orange and white, on a manhole plate.
            var st = new Vector3(-5.2f, 0f, -3.6f);
            k.Of(p.Steel).AddCylinder(st, 0.45f, 0.02f, 20);
            for (int i = 0; i < 8; i++) k.Of(i % 2 == 0 ? p.Orange : p.White).AddCylinder(st + Vector3.up * (0.02f + i * 0.3f), 0.3f, 0.3f, 16);
            k.Of(p.Orange).AddCylinder(st + Vector3.up * 2.42f, 0.33f, 0.06f, 16);
            Steam(g, st + Vector3.up * 2.55f);
            Solid(g, st + Vector3.up * 1.2f, new Vector3(0.62f, 2.4f, 0.62f));
            // Hydrant and the corner's signs at the edge of the harbour walk.
            var hy = new Vector3(3.4f, 0f, -5.0f); // by the fence, clear of the way to the pier gate
            k.Of(p.Red).AddCylinder(hy, 0.15f, 0.06f, 12);
            k.Of(p.Red).AddCylinder(hy + Vector3.up * 0.06f, 0.11f, 0.5f, 12);
            k.Of(p.Steel).AddFrustum(hy + Vector3.up * 0.56f, 0.125f, 0.05f, 0.13f, 12);
            k.Box(p.Steel, hy + Vector3.up * 0.72f, new Vector3(0.05f, 0.05f, 0.05f));
            foreach (var sx in new[] { -1f, 1f }) k.Of(p.Steel).AddCylinder(hy + new Vector3(sx * 0.1f, 0.4f, 0f), 0.045f, 0.1f, 10, 1f, Quaternion.Euler(0f, 0f, -90f * sx));
            k.Of(p.Steel).AddCylinder(hy + new Vector3(0f, 0.36f, -0.1f), 0.06f, 0.1f, 10, 1f, Quaternion.Euler(-90f, 0f, 0f));
            var sp = new Vector3(4.35f, 0f, -1.95f);
            k.Of(p.Green).AddCylinder(sp, 0.04f, 3.1f, 8);
            k.Box(p.Green, sp + new Vector3(0f, 3.0f, 0f), new Vector3(0.95f, 0.19f, 0.02f));
            Sign(g, "HARBOR ST", 0.1f, new Color(0.96f, 0.96f, 0.94f), sp + new Vector3(0f, 3.0f, -0.015f), 0f, 0.9f);
            // A wire litter basket by the booth.
            var bin = new Vector3(-2.25f, 0f, -0.45f);
            k.Of(p.Frame).AddCylinder(bin, 0.22f, 0.02f, 14);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                k.Box(p.Frame, bin + new Vector3(Mathf.Cos(a) * 0.24f, 0.42f, Mathf.Sin(a) * 0.24f), new Vector3(0.012f, 0.84f, 0.012f));
            }
            foreach (var y in new[] { 0.06f, 0.45f, 0.84f }) k.Of(p.Frame).AddTorus(bin + Vector3.up * y, 0.245f, 0.012f, 16, 4, Quaternion.identity);
            k.Of(p.White).AddSphere(bin + new Vector3(0.05f, 0.72f, 0.03f), 0.1f, 8, 6);

            // A cab waiting at the kerb on the street west of the terminal, its roof light on.
            var cab = new Vector3(-9.5f, 0f, -7.6f);
            var cabPaint = Mats.Lit(new Color(0.85f, 0.62f, 0.08f), 0.65f, 0.1f);
            k.Box(cabPaint, cab + new Vector3(0f, 0.62f, 0f), new Vector3(1.85f, 0.56f, 4.7f));
            k.Box(cabPaint, cab + new Vector3(0f, 1.15f, -0.15f), new Vector3(1.66f, 0.5f, 2.3f));
            k.Box(p.DarkGlass, cab + new Vector3(0f, 1.15f, -0.15f), new Vector3(1.68f, 0.38f, 2.0f));
            k.Box(p.DarkGlass, cab + new Vector3(0f, 1.12f, 1.05f), new Vector3(1.5f, 0.4f, 0.3f), Quaternion.Euler(-35f, 0f, 0f));
            k.Box(p.DarkGlass, cab + new Vector3(0f, 1.12f, -1.33f), new Vector3(1.5f, 0.4f, 0.24f), Quaternion.Euler(35f, 0f, 0f));
            k.Box(p.Steel, cab + new Vector3(0f, 0.42f, 2.37f), new Vector3(1.8f, 0.12f, 0.06f));
            k.Box(p.Steel, cab + new Vector3(0f, 0.42f, -2.37f), new Vector3(1.8f, 0.12f, 0.06f));
            k.Box(p.Warm, cab + new Vector3(0f, 1.47f, -0.1f), new Vector3(0.6f, 0.16f, 0.2f));
            k.Box(p.Red, cab + new Vector3(0f, 0.7f, -2.36f), new Vector3(1.6f, 0.1f, 0.02f));
            foreach (var wx in new[] { -0.82f, 0.82f })
                foreach (var wz in new[] { -1.45f, 1.5f })
                {
                    k.Of(p.Frame).AddCylinder(cab + new Vector3(wx - 0.11f, 0.33f, wz), 0.33f, 0.22f, 14, 1f, Quaternion.Euler(0f, 0f, -90f));
                    k.Of(p.Steel).AddDisc(cab + new Vector3(wx + (wx > 0 ? 0.115f : -0.115f), 0.33f, wz), wx > 0 ? Vector3.right : Vector3.left, 0.16f, 12);
                }
            k.Bake(g, "Street");
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
        /// An invisible collider so the player can't walk through a solid prop. It sits on the IgnoreRaycast layer, so the
        /// Passenger's floor probe, interaction rays and tear aiming all pass straight through it.
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

        /// <summary>Steel palisade: flat pales every 15 cm with pointed tops, two rails and posts every 2.4 m (batched).</summary>
        static void Palisade(KitBatch k, Material m, Vector3 a, Vector3 b, float h)
        {
            var dir = b - a;
            float len = dir.magnitude;
            var rot = Quaternion.LookRotation(dir / len, Vector3.up);
            int n = Mathf.Max(2, Mathf.RoundToInt(len / 0.15f));
            for (int i = 0; i <= n; i++)
            {
                var q = Vector3.Lerp(a, b, i / (float)n);
                k.Box(m, q + Vector3.up * (h / 2f), new Vector3(0.015f, h, 0.06f), rot);
                k.Box(m, q + Vector3.up * (h + 0.02f), new Vector3(0.015f, 0.05f, 0.05f), rot * Quaternion.Euler(45f, 0f, 0f));
            }
            int posts = Mathf.Max(1, Mathf.RoundToInt(len / 2.4f));
            for (int i = 0; i <= posts; i++) k.Box(m, Vector3.Lerp(a, b, i / (float)posts) + Vector3.up * (h / 2f + 0.05f), new Vector3(0.08f, h + 0.1f, 0.08f));
            foreach (var y in new[] { 0.3f, h - 0.3f }) k.Box(m, (a + b) / 2f + Vector3.up * y, new Vector3(0.05f, 0.05f, len), rot);
        }

        /// <summary>Steam from the street stack: soft puffs that rise, swell, drift east and thin out (kept out of open tears).</summary>
        static void Steam(Transform g, Vector3 pos)
        {
            var go = new GameObject("Steam");
            go.transform.SetParent(g, false);
            go.transform.localPosition = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 5.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.75f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.62f, 0.58f, 0.52f, 0.3f);
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 12f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            vel.y = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.6f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = Mats.Particle(Tex.SoftDot, false, Color.white, 1f, Mats.StencilNotEqual);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 1.5f;
        }
    }
}

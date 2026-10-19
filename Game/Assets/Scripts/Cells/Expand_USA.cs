using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// New York grows a recreation pier on the harbour beside the terminal (the expansion pass: "expand the country maps
    /// ... more interactive animative and cool"). A chain-link wicket opens in the harbour railing just north of the
    /// island gate's post, and a boardwalk leads out to the pier: a fenced ball court under two pole lights, a brick pier
    /// shed with a fire escape and a caretaker's laundry, a steam grate that huffs when you step on it, benches and a
    /// coin viewer looking out at the statue, pigeons and gulls, a dinghy on a line, and a fire hydrant you can open (the
    /// set piece). Everything new stands east of the railing and north of the island pier (its rail runs ESE from the
    /// gate post), clear of the seams, the booth, the guard, the arrival gate and the way out to the statue.
    /// </summary>
    public static class Expand_USA
    {
        const float Deck = 0.15f;                                           // top of the boardwalk and the pier
        const float WicketS = -0.95f, WicketN = 0.34f;                      // the gap in the railing (the island gate's post is at z -1.1)
        const float PierX0 = 12f, PierX1 = 30f, PierZ0 = -2.5f, PierZ1 = 9.5f;
        const float FenceX0 = 18.2f, FenceX1 = 29.7f, FenceZ0 = -0.3f, FenceZ1 = 9.25f;
        static readonly Color LampColor = new Color(1f, 0.72f, 0.42f);
        static readonly Color CourtLight = new Color(0.9f, 0.94f, 1f);

        sealed class Pal
        {
            public Material Deck, Pile, Metal, Steel, Frame, Black, White, Yellow, Red, Orange, Green, Navy, Brick, Concrete;
            public Material Court, Key, Glass, Warm, Lamp, Pool, Chain, Rope;
        }

        public static void Build(NewYorkCell cell, Transform parent)
        {
            var root = new GameObject("Expand_USA").transform;
            root.SetParent(cell.transform, false);
            try
            {
                var m = Palette();
                var pier = new KitBatch();
                var court = new KitBatch();
                var fence = new KitBatch();
                var shed = new KitBatch();
                var glow = new KitBatch();
                Boardwalk(root, pier, m);
                PierDeck(root, pier, m);
                Court(root, court, fence, glow, m);
                Shed(root, shed, glow, m);
                Plaza(root, pier, m);
                Lookout(root, pier, m);
                pier.Bake(root, "RecPier");
                court.Bake(root, "BallCourt");
                fence.Bake(root, "CourtFence", false);
                shed.Bake(root, "PierShed");
                glow.Bake(root, "CourtGlow", false);
                Life(root, m);
                Lamps(root);
                Postcards(root);
                ShotPoint(root, new Vector3(12.8f, Deck, -1.7f), 55f);
                OpenWicket(cell.transform, root, m); // last: if anything above failed, the railing stays shut
            }
            catch (System.Exception e)
            {
                Debug.LogError("[TS-Expand] New York expansion failed: " + e);
            }
        }

        static Pal Palette()
        {
            // Linear colour: textured materials show texture x tint, so tints stay pale; these match the terminal's palette.
            var chain = new Material(Mats.Template("Lit_Transparent")) { name = "ChainLink" };
            chain.SetTexture("_BaseMap", ChainLinkTex());
            chain.SetColor("_BaseColor", new Color(0.82f, 0.84f, 0.86f, 1f));
            chain.SetFloat("_Smoothness", 0.55f);
            chain.SetFloat("_Metallic", 0.5f);
            return new Pal
            {
                Deck = Mats.Lit(new Color(0.78f, 0.76f, 0.74f), 0.35f, 0f, "wood_oak_floor"),
                Pile = Mats.Lit(new Color(0.95f, 0.9f, 0.85f), 0.3f, 0f, "wood_dark"),
                Metal = Mats.Lit(new Color(0.7f, 0.72f, 0.76f), 0.5f, 0.6f, "metal_brushed"),
                Steel = Mats.Lit(new Color(0.62f, 0.65f, 0.7f), 0.55f, 0.6f, "metal_brushed"),
                Frame = Mats.Lit(new Color(0.24f, 0.25f, 0.27f), 0.5f, 0.5f),
                Black = Mats.Lit(new Color(0.05f, 0.05f, 0.05f), 0.4f),
                White = Mats.Lit(new Color(0.85f, 0.85f, 0.82f), 0.3f),
                Yellow = Mats.Lit(new Color(0.85f, 0.65f, 0.12f), 0.35f),
                Red = Mats.Lit(new Color(0.62f, 0.07f, 0.05f), 0.5f),
                Orange = Mats.Lit(new Color(0.8f, 0.34f, 0.08f), 0.45f),
                Green = Mats.Lit(new Color(0.08f, 0.36f, 0.22f), 0.45f),
                Navy = Mats.Lit(new Color(0.12f, 0.24f, 0.42f), 0.4f),
                Brick = Mats.Lit(new Color(0.85f, 0.75f, 0.7f), 0.3f, 0f, "brick_london"),
                Concrete = Mats.Lit(new Color(0.9f, 0.9f, 0.88f), 0.25f, 0f, "concrete_panels"),
                Court = Mats.Lit(new Color(0.22f, 0.4f, 0.32f), 0.32f),
                Key = Mats.Lit(new Color(0.55f, 0.22f, 0.14f), 0.32f),
                Glass = Mats.Lit(new Color(0.03f, 0.035f, 0.045f), 0.92f, 0.2f),
                Warm = Mats.Emissive(new Color(0.55f, 0.42f, 0.26f), new Color(1.0f, 0.68f, 0.34f)),
                Lamp = Mats.Emissive(new Color(0.85f, 0.88f, 0.92f), new Color(1.25f, 1.3f, 1.35f)),
                Pool = Mats.Particle(Tex.SoftDot, true, Color.white, 1f),
                Chain = chain,
                Rope = Mats.Lit(new Color(0.85f, 0.75f, 0.6f), 0.2f, 0f, "fabric_linen"),
            };
        }

        // ------------------------------------------------------------------ the boardwalk and the pier
        /// <summary>A narrow deck through the wicket, then a 2.4 m boardwalk over the water out to the pier.</summary>
        static void Boardwalk(Transform root, KitBatch k, Pal m)
        {
            Slab(root, k, m, 6.45f, 7.6f, WicketS, WicketN - 0.07f);
            Slab(root, k, m, 7.6f, PierX0 + 0.05f, WicketS, 1.45f);
            foreach (var x in new[] { 8.3f, 10.2f })
                foreach (var z in new[] { WicketS + 0.14f, 1.3f })
                    Pile(k, m, new Vector3(x, 0f, z));
            k.Box(m.Frame, new Vector3((7.6f + PierX0) * 0.5f, Deck - 0.14f, 1.49f), new Vector3(PierX0 - 7.6f, 0.3f, 0.08f));
            k.Box(m.Frame, new Vector3((6.45f + PierX0) * 0.5f, Deck - 0.14f, WicketS - 0.04f), new Vector3(PierX0 - 6.45f, 0.3f, 0.08f));
            Rail(root, k, m, new Vector3(6.62f, Deck, WicketS + 0.07f), new Vector3(PierX0, Deck, WicketS + 0.07f));
            Rail(root, k, m, new Vector3(7.62f, Deck, 1.38f), new Vector3(PierX0, Deck, 1.38f));
            Rail(root, k, m, new Vector3(7.62f, Deck, WicketN - 0.02f), new Vector3(7.62f, Deck, 1.38f));
            // The quay edge north of the narrow deck stays shut: a sagging chain from the wicket post to the rail, and a wall.
            k.Of(m.Frame).AddTube(Sag(new Vector3(6.5f, 0.78f, WicketN), new Vector3(7.62f, Deck + 0.75f, WicketN - 0.02f), 0.12f, 8), 0.012f, 4);
            Wall(root, "Bound", new Vector3(7.09f, 1.25f, WicketN - 0.02f), new Vector3(1.06f, 2.5f, 0.1f));
        }

        static void PierDeck(Transform root, KitBatch k, Pal m)
        {
            Slab(root, k, m, PierX0, PierX1, PierZ0, PierZ1);
            for (float x = PierX0 + 0.2f; x <= PierX1 - 0.1f; x += 2f)
                foreach (var z in new[] { PierZ0 + 0.18f, PierZ1 - 0.18f })
                    Pile(k, m, new Vector3(x, 0f, z));
            for (float z = PierZ0 + 2.2f; z < PierZ1 - 1f; z += 2f) Pile(k, m, new Vector3(PierX1 - 0.18f, 0f, z));
            for (float x = PierX0 + 1.2f; x < PierX1; x += 4f)
                k.Box(m.Pile, new Vector3(x, Deck - 0.37f, (PierZ0 + PierZ1) * 0.5f), new Vector3(0.22f, 0.22f, PierZ1 - PierZ0 - 0.3f));
            float cx = (PierX0 + PierX1) * 0.5f, cz = (PierZ0 + PierZ1) * 0.5f;
            k.Box(m.Frame, new Vector3(cx, Deck - 0.14f, PierZ0 - 0.04f), new Vector3(PierX1 - PierX0, 0.3f, 0.08f));
            k.Box(m.Frame, new Vector3(cx, Deck - 0.14f, PierZ1 + 0.04f), new Vector3(PierX1 - PierX0, 0.3f, 0.08f));
            k.Box(m.Frame, new Vector3(PierX1 + 0.04f, Deck - 0.14f, cz), new Vector3(0.08f, 0.3f, PierZ1 - PierZ0 + 0.16f));
            k.Box(m.Yellow, new Vector3(cx, Deck + 0.002f, PierZ0 + 0.14f), new Vector3(PierX1 - PierX0 - 0.2f, 0.004f, 0.1f));
            k.Box(m.Yellow, new Vector3(PierX1 - 0.14f, Deck + 0.002f, cz), new Vector3(0.1f, 0.004f, PierZ1 - PierZ0 - 0.2f));
            for (float x = 14f; x < PierX1 - 1f; x += 6f)
            {
                k.Box(m.Frame, new Vector3(x, Deck + 0.05f, PierZ0 + 0.3f), new Vector3(0.36f, 0.05f, 0.08f));
                k.Box(m.Frame, new Vector3(x, Deck + 0.02f, PierZ0 + 0.3f), new Vector3(0.1f, 0.04f, 0.1f));
            }
            // Rails round the open edges (the west one in two pieces either side of the boardwalk).
            Rail(root, k, m, new Vector3(PierX0 + 0.08f, Deck, PierZ0 + 0.07f), new Vector3(PierX1 - 0.07f, Deck, PierZ0 + 0.07f));
            Rail(root, k, m, new Vector3(PierX1 - 0.07f, Deck, PierZ0 + 0.07f), new Vector3(PierX1 - 0.07f, Deck, PierZ1 - 0.07f));
            Rail(root, k, m, new Vector3(PierX0 + 0.08f, Deck, PierZ1 - 0.07f), new Vector3(PierX1 - 0.07f, Deck, PierZ1 - 0.07f));
            Rail(root, k, m, new Vector3(PierX0 + 0.08f, Deck, PierZ0 + 0.07f), new Vector3(PierX0 + 0.08f, Deck, WicketS + 0.07f));
            Rail(root, k, m, new Vector3(PierX0 + 0.08f, Deck, 1.38f), new Vector3(PierX0 + 0.08f, Deck, PierZ1 - 0.07f));
            // Invisible catchers at deck height over the water round the new decks (the Passenger's floor probe finds these
            // when it cuts a corner, instead of dropping to the harbour); the player never reaches them past the rails, and
            // they keep clear of the island pier.
            Wall(root, "Catcher", new Vector3(9.5f, Deck - 0.03f, 5.53f), new Vector3(5f, 0.04f, 8.15f));
            Wall(root, "Catcher", new Vector3(22f, Deck - 0.03f, 3.3f), new Vector3(20f, 0.04f, 12.6f));
        }

        // ------------------------------------------------------------------ the ball court
        /// <summary>
        /// A fenced half court: painted surface with a terracotta key and white lines, a hoop on a pole with a chain net
        /// that sways, a chain-link fence with gates west (from the plaza) and south (from the benches), two pole lights
        /// leaning over the fence and a ball left on the court.
        /// </summary>
        static void Court(Transform root, KitBatch k, KitBatch fence, KitBatch glow, Pal m)
        {
            const float baseX = 29.0f, halfX = 18.75f, s0 = 0.35f, s1 = 8.65f, ftX = 24.3f, mid = 4.5f;
            float y = Deck;
            k.Box(m.Court, new Vector3((FenceX0 + FenceX1) * 0.5f, y + 0.005f, (FenceZ0 + FenceZ1) * 0.5f), new Vector3(FenceX1 - FenceX0, 0.01f, FenceZ1 - FenceZ0));
            k.Box(m.Key, new Vector3((ftX + baseX) * 0.5f, y + 0.0125f, mid), new Vector3(baseX - ftX, 0.005f, 3.6f));
            float ly = y + 0.016f;
            Line(k, m.White, new Vector3(baseX, ly, s0), new Vector3(baseX, ly, s1));
            Line(k, m.White, new Vector3(halfX, ly, s0), new Vector3(halfX, ly, s1));
            Line(k, m.White, new Vector3(halfX, ly, s0), new Vector3(baseX, ly, s0));
            Line(k, m.White, new Vector3(halfX, ly, s1), new Vector3(baseX, ly, s1));
            Line(k, m.White, new Vector3(ftX, ly, mid - 1.8f), new Vector3(ftX, ly, mid + 1.8f));
            Line(k, m.White, new Vector3(ftX, ly, mid - 1.8f), new Vector3(baseX, ly, mid - 1.8f));
            Line(k, m.White, new Vector3(ftX, ly, mid + 1.8f), new Vector3(baseX, ly, mid + 1.8f));
            Arc(k, m.White, new Vector3(ftX, ly, mid), 1.5f, 0f, 360f, 24);
            Arc(k, m.White, new Vector3(halfX, ly, mid), 1.5f, -90f, 90f, 12);
            var basket = new Vector3(27.75f, ly, mid);
            const float r3 = 4.6f;
            float phi = Mathf.Asin(3.55f / r3) * Mathf.Rad2Deg;
            Arc(k, m.White, basket, r3, 180f - phi, 180f + phi, 18);
            float xEnd = basket.x - r3 * Mathf.Cos(phi * Mathf.Deg2Rad);
            Line(k, m.White, new Vector3(xEnd, ly, mid - 3.55f), new Vector3(baseX, ly, mid - 3.55f));
            Line(k, m.White, new Vector3(xEnd, ly, mid + 3.55f), new Vector3(baseX, ly, mid + 3.55f));

            // The hoop: rim, bracket, backboard with its square, the pole behind the baseline and its arm.
            var rim = new Vector3(27.75f, y + 3.05f, mid);
            k.Of(m.Orange).AddTorus(rim, 0.23f, 0.012f, 20, 5, Quaternion.identity);
            k.Box(m.Orange, rim + new Vector3(0.3f, 0f, 0f), new Vector3(0.18f, 0.025f, 0.08f));
            k.Box(m.White, new Vector3(28.17f, y + 3.33f, mid), new Vector3(0.03f, 1.05f, 1.8f));
            var face = 28.15f;
            k.Box(m.Orange, new Vector3(face, y + 3.12f, mid), new Vector3(0.008f, 0.04f, 0.6f));
            k.Box(m.Orange, new Vector3(face, y + 3.56f, mid), new Vector3(0.008f, 0.04f, 0.6f));
            k.Box(m.Orange, new Vector3(face, y + 3.34f, mid - 0.3f), new Vector3(0.008f, 0.48f, 0.04f));
            k.Box(m.Orange, new Vector3(face, y + 3.34f, mid + 0.3f), new Vector3(0.008f, 0.48f, 0.04f));
            k.Of(m.Frame).AddCylinder(new Vector3(29.4f, y, mid), 0.08f, 3.95f, 10);
            k.Box(m.Frame, new Vector3(28.8f, y + 3.6f, mid), new Vector3(1.2f, 0.08f, 0.08f));
            k.Box(m.Frame, new Vector3(29.4f, y + 0.02f, mid), new Vector3(0.42f, 0.04f, 0.42f));
            Solid(root, new Vector3(29.4f, y + 1.5f, mid), new Vector3(0.25f, 3f, 0.25f));
            // The chain net hangs from its own pivot at the rim and swings a little in the wind.
            var net = new GameObject("NetPivot").transform;
            net.SetParent(root, false);
            net.localPosition = rim;
            var nb = new MeshBuilder();
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad, b = a + 0.35f;
                var top = new Vector3(Mathf.Cos(a) * 0.22f, -0.01f, Mathf.Sin(a) * 0.22f);
                var bot = new Vector3(Mathf.Cos(b) * 0.13f, -0.44f, Mathf.Sin(b) * 0.13f);
                nb.AddTube(new[] { top, Vector3.Lerp(top, bot, 0.5f) + new Vector3(0f, 0.02f, 0f), bot }, 0.006f, 4, false);
            }
            nb.AddTorus(new Vector3(0f, -0.44f, 0f), 0.13f, 0.006f, 16, 3, Quaternion.identity);
            var netGo = MeshFactory.FromBuilder("Net", nb, m.Steel, net);
            netGo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            WindSway.Add(net, 5f, 1.9f);

            // Chain-link fence: posts, a top rail and a tension wire, the mesh (both faces) and its colliders.
            FenceRun(root, k, fence, m, new Vector3(FenceX0, y, FenceZ0), new Vector3(FenceX1, y, FenceZ0), 23.0f - FenceX0, 24.4f - FenceX0);
            FenceRun(root, k, fence, m, new Vector3(FenceX1, y, FenceZ0), new Vector3(FenceX1, y, FenceZ1), -1f, -1f);
            FenceRun(root, k, fence, m, new Vector3(FenceX1, y, FenceZ1), new Vector3(FenceX0, y, FenceZ1), -1f, -1f);
            FenceRun(root, k, fence, m, new Vector3(FenceX0, y, FenceZ1), new Vector3(FenceX0, y, FenceZ0), FenceZ1 - 5.3f, FenceZ1 - 3.7f);

            // Two pole lights south of the fence lean their heads over the court; one real light between them, and a
            // soft pool of light on the paint under each head.
            foreach (var px in new[] { 19.0f, 28.5f })
            {
                var pb = new Vector3(px, y, FenceZ0 - 0.32f);
                k.Of(m.Frame).AddCylinder(pb, 0.12f, 0.06f, 10);
                k.Of(m.Frame).AddCylinder(pb, 0.065f, 5.9f, 10);
                k.Box(m.Frame, pb + new Vector3(0f, 5.85f, 0.55f), new Vector3(0.06f, 0.06f, 1.1f));
                var head = pb + new Vector3(0f, 5.78f, 1.05f);
                var tilt = Quaternion.Euler(12f, 0f, 0f);
                k.Box(m.Frame, head, new Vector3(0.6f, 0.12f, 0.34f), tilt);
                k.Box(m.Lamp, head + new Vector3(0f, -0.066f, 0.01f), new Vector3(0.52f, 0.014f, 0.26f), tilt);
                Solid(root, pb + Vector3.up * 1.5f, new Vector3(0.2f, 3f, 0.2f));
                Pool(glow.Of(m.Pool), new Vector3(px + (px < 24f ? 1.6f : -1.6f), y + 0.022f, 2.4f), 3.0f, CourtLight * 0.2f);
            }
            PointLight(root, new Vector3(23.6f, y + 4.6f, 3.8f), CourtLight, 2.3f, 8f);
            var ball = new Vector3(25.4f, y + 0.13f, 2.3f);
            k.Of(m.Orange).AddSphere(ball, 0.12f, 14, 10);
            k.Of(m.Black).AddTorus(ball, 0.121f, 0.004f, 20, 3, Quaternion.identity);
            k.Of(m.Black).AddTorus(ball, 0.121f, 0.004f, 20, 3, Quaternion.Euler(90f, 30f, 0f));
        }

        /// <summary>One side of the court fence from a to b; an optional gate between g0 and g1 metres from a.</summary>
        static void FenceRun(Transform root, KitBatch k, KitBatch mesh, Pal m, Vector3 a, Vector3 b, float g0, float g1)
        {
            const float H = 3.0f;
            var d = b - a;
            float len = d.magnitude;
            var dir = d / len;
            var q = Quaternion.FromToRotation(Vector3.up, dir);
            var spans = new List<Vector2>();
            if (g0 >= 0f) { spans.Add(new Vector2(0f, g0)); spans.Add(new Vector2(g1, len)); }
            else spans.Add(new Vector2(0f, len));
            foreach (var s in spans)
            {
                float w = s.y - s.x;
                if (w < 0.05f) continue;
                int n = Mathf.Max(1, Mathf.CeilToInt(w / 2.4f));
                for (int i = 0; i <= n; i++) k.Of(m.Steel).AddCylinder(a + dir * Mathf.Lerp(s.x, s.y, i / (float)n), 0.035f, H, 8);
                var p0 = a + dir * s.x;
                k.Of(m.Steel).AddCylinder(p0 + Vector3.up * H, 0.025f, w, 8, 1f, q);
                k.Of(m.Steel).AddCylinder(p0 + Vector3.up * 0.08f, 0.01f, w, 4, 1f, q);
                Mesh2(mesh, m.Chain, p0 + Vector3.up * 0.06f, dir, w, H - 0.1f);
                Wall(root, "Fence", p0 + dir * (w * 0.5f) + Vector3.up * (H * 0.5f), new Vector3(0.1f, H, w), Quaternion.LookRotation(dir, Vector3.up));
            }
            if (g0 < 0f) return;
            var ga = a + dir * g0;
            k.Of(m.Steel).AddCylinder(ga + Vector3.up * 2.25f, 0.025f, g1 - g0, 8, 1f, q);
            k.Of(m.Steel).AddCylinder(ga + Vector3.up * H, 0.025f, g1 - g0, 8, 1f, q);
            Mesh2(mesh, m.Chain, ga + Vector3.up * 2.27f, dir, g1 - g0, H - 2.3f);
        }

        // ------------------------------------------------------------------ the pier shed
        /// <summary>
        /// A two-storey brick pier shed in the north-west corner: a green steel door under a caged lamp that sways, one
        /// lit window upstairs (the caretaker's), "PIER 11" painted on the brick, a fire escape on the court side with a
        /// towel and a shirt drying on its rail, a turbine vent turning on the roof and a pennant on a pole.
        /// </summary>
        static void Shed(Transform root, KitBatch k, KitBatch glow, Pal m)
        {
            const float x0 = 12.4f, x1 = 16.6f, z0 = 5.6f, z1 = 9.3f, H = 6.2f;
            float y = Deck, cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f, w = x1 - x0, d = z1 - z0;
            k.Box(m.Brick, new Vector3(cx, y + H * 0.5f, cz), new Vector3(w, H, d), 1.4f);
            k.Box(m.Concrete, new Vector3(cx, y + 0.2f, cz), new Vector3(w + 0.06f, 0.4f, d + 0.06f), 2f);
            k.Box(m.Concrete, new Vector3(cx, y + 3.1f, cz), new Vector3(w + 0.08f, 0.12f, d + 0.08f), 2f);
            k.Box(m.Frame, new Vector3(cx, y + H + 0.09f, cz), new Vector3(w + 0.24f, 0.18f, d + 0.24f));
            k.Box(m.Brick, new Vector3(cx, y + H + 0.43f, cz), new Vector3(w, 0.5f, d), 1.4f);
            k.Box(m.Concrete, new Vector3(cx, y + H + 0.71f, cz), new Vector3(w + 0.1f, 0.06f, d + 0.1f), 2f);
            Wall(root, "PierShed", new Vector3(cx, y + 3.45f, cz), new Vector3(w, 6.9f, d));

            // Windows (dark, one lit) on every face but the back.
            Window(k, m, new Vector3(15.6f, y + 1.65f, z0), Vector3.back, 0.9f, 1.3f, false);
            Window(k, m, new Vector3(13.3f, y + 4.4f, z0), Vector3.back, 0.9f, 1.4f, true);
            Window(k, m, new Vector3(15.6f, y + 4.4f, z0), Vector3.back, 0.9f, 1.4f, false);
            Window(k, m, new Vector3(x0, y + 1.65f, cz), Vector3.left, 0.9f, 1.3f, false);
            Window(k, m, new Vector3(x0, y + 4.4f, cz), Vector3.left, 0.9f, 1.4f, true);
            Window(k, m, new Vector3(x1, y + 1.65f, 6.5f), Vector3.right, 0.9f, 1.3f, false);
            Window(k, m, new Vector3(13.6f, y + 4.4f, z1), Vector3.forward, 0.9f, 1.4f, false);
            // The upstairs door onto the fire escape.
            k.Box(m.Glass, new Vector3(x1 + 0.01f, y + 4.45f, 7.45f), new Vector3(0.02f, 2.1f, 0.85f));
            k.Box(m.Frame, new Vector3(x1 + 0.03f, y + 5.53f, 7.45f), new Vector3(0.05f, 0.08f, 1.0f));
            k.Box(m.Frame, new Vector3(x1 + 0.03f, y + 4.45f, 7.0f), new Vector3(0.05f, 2.16f, 0.07f));
            k.Box(m.Frame, new Vector3(x1 + 0.03f, y + 4.45f, 7.9f), new Vector3(0.05f, 2.16f, 0.07f));

            // The street door: green steel in a dark frame, a little lit window, a kick plate and a concrete step.
            var door = new Vector3(14.0f, y, z0);
            k.Box(m.Green, door + new Vector3(0f, 1.1f, -0.025f), new Vector3(1.0f, 2.2f, 0.05f));
            k.Box(m.Frame, door + new Vector3(0f, 2.25f, -0.04f), new Vector3(1.16f, 0.1f, 0.08f));
            k.Box(m.Frame, door + new Vector3(-0.55f, 1.1f, -0.04f), new Vector3(0.08f, 2.3f, 0.08f));
            k.Box(m.Frame, door + new Vector3(0.55f, 1.1f, -0.04f), new Vector3(0.08f, 2.3f, 0.08f));
            k.Box(m.Warm, door + new Vector3(0f, 1.65f, -0.055f), new Vector3(0.28f, 0.28f, 0.01f));
            k.Box(m.Steel, door + new Vector3(0f, 0.18f, -0.055f), new Vector3(0.9f, 0.3f, 0.01f));
            k.Box(m.Steel, door + new Vector3(0.36f, 1.05f, -0.07f), new Vector3(0.04f, 0.18f, 0.04f));
            k.Box(m.Concrete, door + new Vector3(0f, 0.06f, -0.24f), new Vector3(1.4f, 0.12f, 0.44f), 2f);
            Floor(root, "ShedStep", door + new Vector3(0f, 0.06f, -0.24f), new Vector3(1.4f, 0.12f, 0.44f), "stone");
            Sign(root, "PIER  11", new Vector3(cx, y + 5.55f, z0 - 0.02f), 0f, 0.42f, 3.6f, new Color(0.93f, 0.91f, 0.85f));

            // The caged lamp over the door hangs from a bracket and sways; its warmth lies on the boards as a soft pool (no
            // real light: the harbour's big batched meshes already sit at the forward renderer's eight lights per object).
            k.Box(m.Frame, new Vector3(14.0f, y + 2.8f, z0 - 0.17f), new Vector3(0.04f, 0.04f, 0.34f));
            var lampPivot = new GameObject("DoorLampPivot").transform;
            lampPivot.SetParent(root, false);
            lampPivot.localPosition = new Vector3(14.0f, y + 2.8f, z0 - 0.34f);
            MeshFactory.Cylinder("Chain", lampPivot, new Vector3(0f, -0.16f, 0f), 0.006f, 0.16f, m.Frame, 6, false);
            MeshFactory.Frustum("Shade", lampPivot, new Vector3(0f, -0.26f, 0f), 0.16f, 0.05f, 0.1f, m.Green, 12);
            var bulb = MeshFactory.Sphere("Bulb", lampPivot, new Vector3(0f, -0.3f, 0f), 0.05f, m.Warm, 10, 8);
            bulb.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            WindSway.Add(lampPivot, 4f, 2.3f);
            Pool(glow.Of(m.Pool), new Vector3(14.0f, y + 0.02f, z0 - 0.9f), 1.6f, new Color(1f, 0.72f, 0.42f) * 0.3f);
            Pool(glow.Of(m.Pool), new Vector3(14.0f, y + 0.021f, z0 - 0.6f), 0.8f, new Color(1f, 0.72f, 0.42f) * 0.25f);

            // Fire escape on the east face: a slatted landing at the upstairs door, its rails, brackets, the retracted
            // drop ladder and the ladder on to the roof.
            float fy = y + 3.35f;
            for (int i = 0; i < 12; i++) k.Box(m.Frame, new Vector3(17.075f, fy, 6.08f + i * 0.24f), new Vector3(0.95f, 0.03f, 0.06f));
            k.Box(m.Frame, new Vector3(17.075f, fy - 0.04f, 6.0f), new Vector3(0.95f, 0.08f, 0.05f));
            k.Box(m.Frame, new Vector3(17.075f, fy - 0.04f, 8.9f), new Vector3(0.95f, 0.08f, 0.05f));
            k.Box(m.Frame, new Vector3(17.53f, fy - 0.04f, 7.45f), new Vector3(0.05f, 0.08f, 2.95f));
            UrbanKit.Railing(k, m.Frame, new Vector3(17.53f, fy, 6.0f), new Vector3(17.53f, fy, 8.9f), 0.9f, Vector3.zero);
            UrbanKit.Railing(k, m.Frame, new Vector3(16.62f, fy, 6.0f), new Vector3(17.53f, fy, 6.0f), 0.9f, Vector3.zero);
            UrbanKit.Railing(k, m.Frame, new Vector3(16.62f, fy, 8.9f), new Vector3(17.53f, fy, 8.9f), 0.9f, Vector3.zero);
            foreach (var z in new[] { 6.2f, 8.7f })
                k.Box(m.Frame, new Vector3(17.05f, fy - 0.45f, z), new Vector3(0.05f, 1.27f, 0.05f), Quaternion.Euler(0f, 0f, -45f));
            foreach (var z in new[] { 6.15f, 6.55f }) k.Box(m.Frame, new Vector3(17.42f, (fy + y + 1.4f) * 0.5f, z), new Vector3(0.03f, fy - y - 1.4f, 0.03f));
            for (float ry = y + 1.6f; ry < fy - 0.1f; ry += 0.3f) k.Box(m.Frame, new Vector3(17.42f, ry, 6.35f), new Vector3(0.025f, 0.025f, 0.4f));
            foreach (var z in new[] { 8.58f, 8.86f }) k.Box(m.Frame, new Vector3(16.7f, (fy + y + H + 0.7f) * 0.5f, z), new Vector3(0.03f, y + H + 0.7f - fy, 0.03f));
            for (float ry = fy + 0.3f; ry < y + H + 0.6f; ry += 0.3f) k.Box(m.Frame, new Vector3(16.7f, ry, 8.72f), new Vector3(0.025f, 0.025f, 0.3f));
            var towel = Mats.Lit(new Color(0.95f, 0.95f, 0.92f), 0.2f, 0f, "fabric_linen", false);
            var shirt = Mats.Lit(new Color(0.55f, 0.66f, 0.86f), 0.2f, 0f, "fabric_linen", false);
            ClothFlag.Create(root, "Towel", new Vector3(17.57f, fy + 0.88f, 6.35f), 0.55f, 0.75f, towel, -90f, 0.05f);
            ClothFlag.Create(root, "Shirt", new Vector3(17.57f, fy + 0.88f, 7.3f), 0.6f, 0.62f, shirt, -90f, 0.05f);

            // Roof: a turbine vent turning slowly, and a pennant on a pole.
            var vent = new Vector3(13.3f, y + H, 8.4f);
            k.Of(m.Steel).AddCylinder(vent, 0.15f, 0.75f, 12);
            var turbine = new GameObject("Turbine").transform;
            turbine.SetParent(root, false);
            turbine.localPosition = vent + Vector3.up * 0.75f;
            var tb = new MeshBuilder();
            for (int i = 0; i < 10; i++)
                tb.AddBox(Quaternion.Euler(0f, i * 36f, 0f) * new Vector3(0.15f, 0.17f, 0f), new Vector3(0.015f, 0.34f, 0.12f), Quaternion.Euler(0f, i * 36f + 25f, 8f));
            tb.AddSphere(new Vector3(0f, 0.34f, 0f), 0.17f, 12, 6, 1f, 0f, 90f);
            tb.AddCylinder(Vector3.zero, 0.17f, 0.02f, 12);
            MeshFactory.FromBuilder("TurbineHead", tb, m.Steel, turbine);
            Rotor.Add(turbine, 75f);
            k.Of(m.Steel).AddCylinder(new Vector3(16.25f, y + H, 9.0f), 0.03f, 2.6f, 8);
            k.Of(m.Steel).AddSphere(new Vector3(16.25f, y + H + 2.63f, 9.0f), 0.05f, 8, 6);
            var pennant = Mats.Lit(new Color(0.2f, 0.3f, 0.62f), 0.25f, 0f, "fabric_linen", false);
            ClothFlag.Create(root, "Pennant", new Vector3(16.22f, y + H + 2.55f, 9.0f), 1.0f, 0.5f, pennant, 180f, 0.14f);
        }

        static void Window(KitBatch k, Pal m, Vector3 c, Vector3 n, float w, float h, bool lit)
        {
            var q = Quaternion.LookRotation(n, Vector3.up);
            var r = Vector3.Cross(Vector3.up, n);
            k.Box(lit ? m.Warm : m.Glass, c + n * 0.006f, new Vector3(w, h, 0.02f), q);
            k.Box(m.Frame, c + n * 0.03f + Vector3.up * (h * 0.5f + 0.03f), new Vector3(w + 0.12f, 0.06f, 0.06f), q);
            k.Box(m.Concrete, c + n * 0.06f - Vector3.up * (h * 0.5f + 0.04f), new Vector3(w + 0.2f, 0.08f, 0.12f), q);
            k.Box(m.Frame, c + n * 0.03f - r * (w * 0.5f + 0.03f), new Vector3(0.06f, h, 0.06f), q);
            k.Box(m.Frame, c + n * 0.03f + r * (w * 0.5f + 0.03f), new Vector3(0.06f, h, 0.06f), q);
            k.Box(m.Frame, c + n * 0.025f, new Vector3(0.035f, h, 0.03f), q);
            for (int i = 1; i < 3; i++) k.Box(m.Frame, c + n * 0.025f + Vector3.up * (-h * 0.5f + i * h / 3f), new Vector3(w, 0.03f, 0.03f), q);
        }

        // ------------------------------------------------------------------ the plaza and the lookout
        /// <summary>The plaza between the boardwalk and the court: the hydrant, the steam grate and a newspaper box.</summary>
        static void Plaza(Transform root, KitBatch k, Pal m)
        {
            Hydrant(root, k, m, new Vector3(13.1f, Deck, 4.75f), new Vector3(16.25f, Deck, 4.65f));
            SteamGrate(root, k, m, new Vector3(14.5f, Deck, -1.55f));
            var box = new Vector3(12.55f, Deck, 2.6f);
            k.Box(m.Navy, box + new Vector3(0f, 0.62f, 0f), new Vector3(0.42f, 0.7f, 0.4f));
            k.Box(m.Glass, box + new Vector3(0.212f, 0.75f, 0f), new Vector3(0.01f, 0.3f, 0.28f));
            k.Box(m.Frame, box + new Vector3(0f, 1.0f, 0f), new Vector3(0.46f, 0.06f, 0.44f));
            foreach (var dz in new[] { -0.15f, 0.15f }) k.Box(m.Frame, box + new Vector3(0f, 0.14f, dz), new Vector3(0.36f, 0.28f, 0.03f));
            Solid(root, box + Vector3.up * 0.5f, new Vector3(0.45f, 1f, 0.42f));
        }

        /// <summary>The south strip: two benches against the fence facing the statue, a coin viewer, a basket, a life ring.</summary>
        static void Lookout(Transform root, KitBatch k, Pal m)
        {
            foreach (var x in new[] { 20.6f, 26.4f }) Kit.Bench(root, new Vector3(x, Deck, -0.62f), 0f, m.Pile, m.Frame);
            var vb = new Vector3(29.25f, Deck, -1.8f);
            k.Of(m.Frame).AddCylinder(vb, 0.17f, 0.06f, 12);
            k.Of(m.Frame).AddCylinder(vb, 0.06f, 1.12f, 10);
            var q = Quaternion.Euler(0f, 119f, 0f);
            var hc = vb + Vector3.up * 1.26f;
            k.Box(m.Green, hc, new Vector3(0.36f, 0.24f, 0.3f), q);
            k.Box(m.Green, hc + Vector3.up * 0.15f, new Vector3(0.3f, 0.06f, 0.26f), q);
            foreach (var sx in new[] { -0.08f, 0.08f })
            {
                k.Of(m.Frame).AddCylinder(hc + q * new Vector3(sx, 0.03f, 0.1f), 0.055f, 0.2f, 10, 1f, q * Quaternion.Euler(90f, 0f, 0f));
                k.Of(m.Black).AddCylinder(hc + q * new Vector3(sx, 0.03f, -0.21f), 0.035f, 0.06f, 10, 1f, q * Quaternion.Euler(90f, 0f, 0f));
            }
            k.Box(m.Steel, hc + q * new Vector3(0.19f, -0.04f, 0f), new Vector3(0.03f, 0.1f, 0.06f), q);
            Solid(root, vb + Vector3.up * 0.7f, new Vector3(0.32f, 1.4f, 0.32f));
            // A wire litter basket and a life ring on its post by the rail.
            var bin = new Vector3(18.75f, Deck, -2.0f);
            k.Of(m.Frame).AddCylinder(bin, 0.22f, 0.02f, 14);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                k.Box(m.Frame, bin + new Vector3(Mathf.Cos(a) * 0.24f, 0.42f, Mathf.Sin(a) * 0.24f), new Vector3(0.012f, 0.84f, 0.012f));
            }
            foreach (var yy in new[] { 0.06f, 0.45f, 0.84f }) k.Of(m.Frame).AddTorus(bin + Vector3.up * yy, 0.245f, 0.012f, 16, 4, Quaternion.identity);
            Solid(root, bin + Vector3.up * 0.45f, new Vector3(0.5f, 0.9f, 0.5f));
            var ring = new Vector3(24.0f, Deck, -2.28f);
            k.Box(m.Frame, ring + Vector3.up * 0.8f, new Vector3(0.07f, 1.6f, 0.07f));
            k.Of(m.Orange).AddTorus(ring + new Vector3(0f, 1.2f, 0.07f), 0.26f, 0.07f, 18, 6, Quaternion.Euler(90f, 0f, 0f));
            Solid(root, ring + Vector3.up * 0.8f, new Vector3(0.12f, 1.6f, 0.12f));
        }

        // ------------------------------------------------------------------ the hydrant (set piece)
        static void Hydrant(Transform root, KitBatch k, Pal m, Vector3 at, Vector3 land)
        {
            var hy = new GameObject("Hydrant").transform;
            hy.SetParent(root, false);
            hy.localPosition = at;
            var hk = new KitBatch();
            hk.Of(m.Red).AddCylinder(Vector3.zero, 0.17f, 0.06f, 14);
            hk.Of(m.Red).AddCylinder(new Vector3(0f, 0.06f, 0f), 0.12f, 0.5f, 14);
            hk.Of(m.Red).AddFrustum(new Vector3(0f, 0.56f, 0f), 0.135f, 0.06f, 0.13f, 14);
            hk.Of(m.Steel).AddCylinder(new Vector3(0f, 0.69f, 0f), 0.035f, 0.05f, 5);
            hk.Of(m.Red).AddTorus(new Vector3(0f, 0.5f, 0f), 0.125f, 0.012f, 14, 4, Quaternion.identity);
            var right = Quaternion.FromToRotation(Vector3.up, Vector3.right);
            var left = Quaternion.FromToRotation(Vector3.up, Vector3.left);
            hk.Of(m.Red).AddCylinder(new Vector3(0.1f, 0.4f, 0f), 0.055f, 0.13f, 12, 1f, right);   // the outlet the water comes from
            hk.Of(m.Red).AddCylinder(new Vector3(-0.1f, 0.4f, 0f), 0.05f, 0.1f, 12, 1f, left);
            hk.Of(m.Steel).AddCylinder(new Vector3(-0.2f, 0.4f, 0f), 0.056f, 0.04f, 12, 1f, left);
            // The wheel on the south side (its axle points south), on a short stem.
            hk.Of(m.Steel).AddCylinder(new Vector3(0f, 0.38f, -0.1f), 0.022f, 0.11f, 8, 1f, Quaternion.FromToRotation(Vector3.up, Vector3.back));
            hk.Bake(hy, "HydrantBody");
            var wheel = new GameObject("Wheel").transform;
            wheel.SetParent(hy, false);
            wheel.localPosition = new Vector3(0f, 0.38f, -0.22f);
            wheel.localRotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            var wb = new MeshBuilder();
            wb.AddTorus(Vector3.zero, 0.15f, 0.014f, 22, 5, Quaternion.Euler(90f, 0f, 0f));
            wb.AddCylinder(new Vector3(0f, 0f, -0.03f), 0.03f, 0.06f, 10, 1f, Quaternion.Euler(90f, 0f, 0f));
            for (int i = 0; i < 5; i++)
            {
                var dir = Quaternion.Euler(0f, 0f, i * 72f) * Vector3.up;
                wb.AddBox(dir * 0.085f, new Vector3(0.016f, 0.13f, 0.012f), Quaternion.Euler(0f, 0f, i * 72f));
            }
            MeshFactory.FromBuilder("WheelMesh", wb, m.Yellow, wheel);
            // The outlet cap hangs from a hinge at the top of the mouth and swings off when the water comes.
            var cap = new GameObject("Cap").transform;
            cap.SetParent(hy, false);
            cap.localPosition = new Vector3(0.235f, 0.465f, 0f);
            var cb = new MeshBuilder();
            cb.AddCylinder(new Vector3(-0.012f, -0.065f, 0f), 0.062f, 0.045f, 12, 1f, right);
            cb.AddCylinder(new Vector3(0.033f, -0.065f, 0f), 0.025f, 0.03f, 5, 1f, right);
            MeshFactory.FromBuilder("CapMesh", cb, m.Steel, cap);
            Solid(root, at + Vector3.up * 0.37f, new Vector3(0.36f, 0.74f, 0.36f));

            // Water: the arc, a mist where it lands and droplets jumping off the boards; the puddle spreads under it.
            var outlet = at + new Vector3(0.26f, 0.4f, 0f);
            var spray = Particles(root, "Spray", outlet, Quaternion.Euler(-22f, 90f, 0f), false);
            {
                var main = spray.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.56f, 0.68f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(5.2f, 6.0f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.07f);
                main.startColor = new Color(0.78f, 0.86f, 0.95f, 0.55f);
                main.gravityModifier = 1f;
                main.maxParticles = 400;
                var em = spray.emission; em.rateOverTime = 150f;
                var sh = spray.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 5f; sh.radius = 0.03f;
                var r = spray.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.035f; r.lengthScale = 1.2f; r.maxParticleSize = 0.2f;
            }
            var landAt = new Vector3(land.x, Deck + 0.1f, land.z);
            var mist = Particles(root, "SprayMist", landAt, Quaternion.identity, false);
            {
                var main = mist.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
                main.startColor = new Color(0.8f, 0.85f, 0.9f, 0.16f);
                main.maxParticles = 60;
                var em = mist.emission; em.rateOverTime = 14f;
                var sh = mist.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(1.4f, 0.1f, 0.8f);
                var vel = mist.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.12f); vel.y = new ParticleSystem.MinMaxCurve(0.15f, 0.35f); vel.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
                var size = mist.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
            }
            var splash = Particles(root, "SpraySplash", landAt, Quaternion.Euler(-90f, 0f, 0f), false);
            {
                var main = splash.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
                main.startColor = new Color(0.8f, 0.88f, 0.95f, 0.6f);
                main.gravityModifier = 1f;
                main.maxParticles = 120;
                var em = splash.emission; em.rateOverTime = 50f;
                var sh = splash.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 32f; sh.radius = 0.45f;
            }
            var poolMat = new Material(Mats.Template("Lit_Normal")) { name = "HydrantPuddle" };
            poolMat.SetColor("_BaseColor", new Color(0.05f, 0.06f, 0.07f));
            var ripple = Tex.Normal("water_ripple");
            if (ripple != null)
            {
                poolMat.SetTexture("_BumpMap", ripple);
                poolMat.SetTextureScale("_BumpMap", new Vector2(0.8f, 0.8f));
                poolMat.SetFloat("_BumpScale", 0.3f);
            }
            poolMat.SetFloat("_Smoothness", 0.94f);
            var db = new MeshBuilder();
            db.AddDisc(Vector3.zero, Vector3.up, 1f, 28);
            var puddle = MeshFactory.Make("Puddle", db.ToMesh("Puddle"), poolMat, root, new Vector3(land.x, Deck + 0.004f, land.z));
            puddle.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            puddle.AddComponent<WaterDrift>();

            var valve = new GameObject("HydrantValve");
            valve.transform.SetParent(hy, false);
            valve.transform.localPosition = new Vector3(0f, 0.45f, -0.05f);
            var trig = valve.AddComponent<SphereCollider>();
            trig.radius = 0.42f;
            trig.isTrigger = true;
            var h = valve.AddComponent<NyHydrant>();
            h.Setup(root, wheel, cap, puddle.transform, new Vector3(land.x, Deck, land.z), spray, mist, splash);
        }

        static void SteamGrate(Transform root, KitBatch k, Pal m, Vector3 at)
        {
            const float w = 1.3f, d = 0.9f;
            k.Box(m.Black, at + new Vector3(0f, 0.002f, 0f), new Vector3(w - 0.06f, 0.004f, d - 0.06f));
            k.Box(m.Steel, at + new Vector3(0f, 0.008f, d * 0.5f - 0.03f), new Vector3(w, 0.016f, 0.06f));
            k.Box(m.Steel, at + new Vector3(0f, 0.008f, -d * 0.5f + 0.03f), new Vector3(w, 0.016f, 0.06f));
            k.Box(m.Steel, at + new Vector3(w * 0.5f - 0.03f, 0.008f, 0f), new Vector3(0.06f, 0.016f, d));
            k.Box(m.Steel, at + new Vector3(-w * 0.5f + 0.03f, 0.008f, 0f), new Vector3(0.06f, 0.016f, d));
            for (int i = 1; i < 12; i++) k.Box(m.Steel, at + new Vector3(-w * 0.5f + i * w / 12f, 0.007f, 0f), new Vector3(0.025f, 0.014f, d - 0.06f));
            var grate = new GameObject("SteamGrate").transform;
            grate.SetParent(root, false);
            grate.localPosition = at;
            var drift = Particles(grate, "Steam", new Vector3(0f, 0.04f, 0f), Quaternion.identity, true);
            SteamLook(drift, 6f, new Vector2(0.15f, 0.45f), new Vector2(0.3f, 0.6f), 0.22f, new Vector3(w - 0.2f, 0.02f, d - 0.2f));
            var huff = Particles(grate, "SteamHuff", new Vector3(0f, 0.04f, 0f), Quaternion.identity, false);
            SteamLook(huff, 0f, new Vector2(0.8f, 1.6f), new Vector2(0.45f, 0.9f), 0.3f, new Vector3(w - 0.2f, 0.02f, d - 0.2f));
            grate.gameObject.AddComponent<NySteamGrate>().Setup(drift, huff, new Vector2(w * 0.5f, d * 0.5f));
        }

        static void SteamLook(ParticleSystem ps, float rate, Vector2 rise, Vector2 size, float alpha, Vector3 area)
        {
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.4f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.66f, 0.64f, 0.6f, alpha);
            main.maxParticles = 120;
            var em = ps.emission; em.rateOverTime = rate;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = area;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.02f, 0.18f); vel.y = new ParticleSystem.MinMaxCurve(rise.x, rise.y); vel.z = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.4f));
            var spin = ps.rotationOverLifetime; spin.enabled = true; spin.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.maxParticleSize = 1.4f;
        }

        // ------------------------------------------------------------------ life, lamps, postcards
        static void Life(Transform root, Pal m)
        {
            AmbientBirds.Create(root, new Vector3(15.3f, Deck, 1.4f), 1.3f, 8, AmbientBirds.Kind.Pigeon);
            AmbientBirds.Create(root, new Vector3(25.6f, Deck + 0.01f, 6.3f), 1.5f, 4, AmbientBirds.Kind.Gull);
            // A dinghy on a line off the south edge, bobbing on the harbour.
            var boat = new GameObject("Dinghy").transform;
            boat.SetParent(root, false);
            boat.localPosition = new Vector3(22.6f, -1.5f, -4.1f);
            boat.localRotation = Quaternion.Euler(0f, 75f, 0f);
            var hull = new List<Vector2> { new Vector2(-0.55f, -1.25f), new Vector2(0.55f, -1.25f), new Vector2(0.6f, 0.4f), new Vector2(0.3f, 1.1f), new Vector2(0f, 1.32f), new Vector2(-0.3f, 1.1f), new Vector2(-0.6f, 0.4f) };
            var inner = new List<Vector2>();
            foreach (var p in hull) inner.Add(p * 0.86f);
            var bk = new KitBatch();
            bk.Of(m.White).AddPrism(hull, -0.12f, 0.2f, 1f);
            bk.Of(m.Navy).AddPrism(hull, 0.2f, 0.27f, 1f, false);
            bk.Of(m.Pile).AddPrism(inner, 0.2f, 0.215f, 1f);
            var rim = new List<Vector3>();
            foreach (var p in hull) rim.Add(new Vector3(p.x, 0.28f, p.y));
            rim.Add(rim[0]);
            bk.Of(m.Pile).AddTube(rim, 0.03f, 5, false);
            foreach (var z in new[] { -0.6f, 0.25f }) bk.Box(m.Pile, new Vector3(0f, 0.3f, z), new Vector3(1.0f, 0.04f, 0.22f));
            bk.Box(m.Pile, new Vector3(0.18f, 0.25f, -0.15f), new Vector3(0.05f, 0.04f, 1.9f), Quaternion.Euler(0f, 4f, 0f));
            bk.Bake(boat, "Dinghy");
            Bob.Add(boat, 0.035f, 3.4f, 2.5f);
            var bow = boat.localPosition + boat.localRotation * new Vector3(0f, 0.3f, 1.25f);
            var pk = new KitBatch();
            pk.Of(m.Rope).AddTube(Sag(bow, new Vector3(22.2f, Deck + 0.05f, PierZ0 + 0.3f), 0.4f, 10), 0.015f, 5);
            pk.Bake(root, "DinghyLine");
        }

        static void Lamps(Transform root)
        {
            SwitchLamp.Create(root, new Vector3(8.3f, Deck, 1.16f), LampColor);         // the boardwalk, by the wicket
            SwitchLamp.Create(root, new Vector3(17.4f, Deck, 7.4f), LampColor);         // the dark passage under the fire escape
            SwitchLamp.Create(root, new Vector3(28.3f, Deck, -2.05f), LampColor);       // the far end of the lookout
            // One on the island pier's dark stretch between its lamps (right side, 44 m out along the sight line).
            var flat = new Vector3(150f, 0f, -70f);
            var u = flat.normalized;
            var lft = new Vector3(-u.z, 0f, u.x);
            SwitchLamp.Create(root, u * 44f - lft * 1.35f, LampColor);
        }

        static void Postcards(Transform root)
        {
            Postcard.Create(root, CountryId.USA, 1, new Vector3(27.7f, Deck + 1.1f, -1.45f), "Counted the lamps out to the island twice. The second time there was one more.");
            Postcard.Create(root, CountryId.USA, 2, new Vector3(14.0f, Deck + 1.15f, 5.1f), "Knocked on the caretaker's door. Someone knocked back, a beat too late.");
            Postcard.Create(root, CountryId.USA, 3, new Vector3(-5.22f, 1.15f, 1.01f), "Called home from here. It rang in the next booth. I didn't pick up.");
        }

        // ------------------------------------------------------------------ the wicket
        /// <summary>
        /// Opens the harbour railing between the island gate's north post (z -1.1) and z 0.34: the terminal's railing
        /// mesh is hidden and rebuilt (identically) from z 0.34, its collider and the terminal's east bound are shortened to
        /// match, and the gate post (a visual only until now) gets a collider. A post, a top bar and a sign frame the gap.
        /// </summary>
        static void OpenWicket(Transform cellT, Transform root, Pal m)
        {
            var terminal = cellT.Find("Terminal");
            var rail = terminal != null ? terminal.Find("Railing") : null;
            var railCol = terminal != null ? terminal.Find("RailingCollider") : null;
            Transform bound = null;
            if (terminal != null)
                foreach (Transform c in terminal)
                    if (c.name == "Bound" && (c.localPosition - new Vector3(6.45f, 2.5f, 4.19f)).sqrMagnitude < 0.01f) { bound = c; break; }
            var railBox = railCol != null ? railCol.GetComponent<BoxCollider>() : null;
            var boundBox = bound != null ? bound.GetComponent<BoxCollider>() : null;
            if (rail == null || railBox == null || boundBox == null)
            {
                Debug.LogWarning("[TS-Expand] New York: the harbour railing is not where expected; the wicket to the rec pier stays shut");
                return;
            }
            var mr = rail.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
            Kit.Railing(root, new Vector3(6.5f, 0f, WicketN), new Vector3(6.5f, 0f, 9f), 1.05f, m.Metal, 0.18f);
            railCol.localPosition = new Vector3(6.5f, 0.6f, (WicketN + 9f) * 0.5f);
            railBox.size = new Vector3(0.2f, 1.2f, 9f - WicketN);
            bound.localPosition = new Vector3(6.45f, 2.5f, (WicketN + 9.5f) * 0.5f);
            boundBox.size = new Vector3(0.3f, 5f, 9.5f - WicketN);

            var k = new KitBatch();
            k.Box(m.Frame, new Vector3(6.5f, 1.25f, WicketN), new Vector3(0.12f, 2.5f, 0.12f));
            k.Box(m.Frame, new Vector3(6.5f, 2.42f, -0.38f), new Vector3(0.08f, 0.08f, 1.44f));
            k.Box(m.Green, new Vector3(6.5f, 2.78f, -0.38f), new Vector3(0.05f, 0.34f, 1.25f));
            foreach (var z in new[] { -0.9f, 0.14f }) k.Box(m.Frame, new Vector3(6.5f, 2.53f, z), new Vector3(0.02f, 0.14f, 0.02f));
            // The chain-link leaf, swung back against the outside of the railing.
            const float l0 = WicketN + 0.08f, l1 = WicketN + 1.3f;
            k.Box(m.Steel, new Vector3(6.6f, 0.12f, (l0 + l1) * 0.5f), new Vector3(0.04f, 0.04f, l1 - l0));
            k.Box(m.Steel, new Vector3(6.6f, 2.1f, (l0 + l1) * 0.5f), new Vector3(0.04f, 0.04f, l1 - l0));
            k.Box(m.Steel, new Vector3(6.6f, 1.11f, l0), new Vector3(0.04f, 2.02f, 0.04f));
            k.Box(m.Steel, new Vector3(6.6f, 1.11f, l1), new Vector3(0.04f, 2.02f, 0.04f));
            k.Bake(root, "Wicket");
            var leaf = new KitBatch();
            Mesh2(leaf, m.Chain, new Vector3(6.6f, 0.14f, l0), Vector3.forward, l1 - l0, 1.94f);
            leaf.Bake(root, "WicketLeaf", false);
            Solid(root, new Vector3(6.5f, 1.65f, -1.1f), new Vector3(0.2f, 3.3f, 0.2f));     // the island gate's north post
            Solid(root, new Vector3(6.5f, 1.25f, WicketN), new Vector3(0.14f, 2.5f, 0.14f));
            Solid(root, new Vector3(7.0f, 0.4f, 0.5f), new Vector3(0.44f, 0.8f, 0.44f));      // the mooring post on the quay edge
            Sign(root, "PIER 11  ·  COURTS", new Vector3(6.47f, 2.78f, -0.38f), 90f, 0.12f, 1.2f, null);
            Sign(root, "PIER 11  ·  COURTS", new Vector3(6.53f, 2.78f, -0.38f), -90f, 0.12f, 1.2f, null);
        }

        // ------------------------------------------------------------------ helpers
        static Texture2D s_Chain;

        /// <summary>A diamond chain-link mesh with soft wire edges (mipmapped, so a far fence fades to a faint screen).</summary>
        static Texture2D ChainLinkTex()
        {
            if (s_Chain != null) return s_Chain;
            const int N = 64, P = 32;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "TS_ChainLink", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int a = (x + y) % P, b = ((x - y) % P + P) % P;
                    float d = Mathf.Min(Mathf.Min(a, P - a), Mathf.Min(b, P - b));
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(1.9f - d) * 255f);
                    px[y * N + x] = new Color32(214, 216, 218, alpha);
                }
            t.SetPixels32(px);
            t.Apply(true);
            s_Chain = t;
            return t;
        }

        static void Slab(Transform root, KitBatch k, Pal m, float x0, float x1, float z0, float z1)
        {
            var c = new Vector3((x0 + x1) * 0.5f, Deck - 0.125f, (z0 + z1) * 0.5f);
            var s = new Vector3(x1 - x0, 0.25f, z1 - z0);
            k.Box(m.Deck, c, s, 1.6f);
            Floor(root, "PierDeck", c, s, "wood");
        }

        static void Pile(KitBatch k, Pal m, Vector3 at) => k.Of(m.Pile).AddCylinder(new Vector3(at.x, -2.3f, at.z), 0.15f, 2.2f, 8);

        /// <summary>A steel pipe railing from a to b (posts, three rails) with a tall collider (stops VR teleport arcs too).</summary>
        static void Rail(Transform root, KitBatch k, Pal m, Vector3 a, Vector3 b, float h = 1.05f)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 0.05f) return;
            var dir = d / len;
            int n = Mathf.Max(1, Mathf.CeilToInt(len / 1.6f));
            for (int i = 0; i <= n; i++) k.Box(m.Steel, Vector3.Lerp(a, b, i / (float)n) + Vector3.up * (h * 0.5f), new Vector3(0.05f, h, 0.05f));
            var q = Quaternion.FromToRotation(Vector3.up, dir);
            var heights = new[] { h, h * 0.62f, h * 0.26f };
            for (int i = 0; i < heights.Length; i++) k.Of(m.Steel).AddCylinder(a + Vector3.up * heights[i], i == 0 ? 0.026f : 0.018f, len, 8, 1f, q);
            Wall(root, "Bound", (a + b) * 0.5f + Vector3.up * 1.25f, new Vector3(0.12f, 2.5f, len), Quaternion.LookRotation(dir, Vector3.up));
        }

        static void Mesh2(KitBatch k, Material mat, Vector3 p, Vector3 dir, float w, float h)
        {
            var b = k.Of(mat);
            Vector3 A = p, B = p + dir * w, C = B + Vector3.up * h, D = A + Vector3.up * h;
            b.AddQuadWorldUV(A, B, C, D, 0.2f);
            b.AddQuadWorldUV(B, A, D, C, 0.2f);
        }

        static void Line(KitBatch k, Material m, Vector3 a, Vector3 b)
        {
            var d = b - a; d.y = 0f;
            float len = d.magnitude;
            if (len < 1e-3f) return;
            k.Box(m, (a + b) * 0.5f, new Vector3(0.05f, 0.004f, len + 0.05f), Quaternion.LookRotation(d / len, Vector3.up));
        }

        static void Arc(KitBatch k, Material m, Vector3 c, float r, float a0, float a1, int segs)
        {
            for (int i = 0; i < segs; i++)
            {
                float t0 = Mathf.Lerp(a0, a1, i / (float)segs) * Mathf.Deg2Rad, t1 = Mathf.Lerp(a0, a1, (i + 1) / (float)segs) * Mathf.Deg2Rad;
                Line(k, m, c + new Vector3(Mathf.Cos(t0), 0f, Mathf.Sin(t0)) * r, c + new Vector3(Mathf.Cos(t1), 0f, Mathf.Sin(t1)) * r);
            }
        }

        static void Pool(MeshBuilder b, Vector3 c, float r, Color color)
        {
            var prev = b.VertexColor;
            color.a = 1f;
            b.VertexColor = color;
            b.AddQuad(c + new Vector3(-r, 0f, -r), c + new Vector3(-r, 0f, r), c + new Vector3(r, 0f, r), c + new Vector3(r, 0f, -r),
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));
            b.VertexColor = prev;
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

        static ParticleSystem Particles(Transform parent, string name, Vector3 pos, Quaternion rot, bool playing)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = playing;
            main.prewarm = playing;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = Mats.Particle(Tex.SoftDot, false, Color.white, 1.15f, Mats.StencilNotEqual);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (playing) ps.Play();
            return ps;
        }

        static void Floor(Transform root, string name, Vector3 c, Vector3 s, string surface)
        {
            var go = new GameObject(name) { layer = Layers.Floor };
            go.transform.SetParent(root, false);
            go.transform.localPosition = c;
            go.AddComponent<BoxCollider>().size = s;
            go.AddComponent<Surface>().Kind = surface;
        }

        static GameObject Wall(Transform root, string name, Vector3 c, Vector3 s, Quaternion? rot = null, int layer = Layers.Default)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(root, false);
            go.transform.localPosition = c;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.AddComponent<BoxCollider>().size = s;
            return go;
        }

        /// <summary>A prop's collider on IgnoreRaycast (the Passenger's floor probe, interaction rays and tear aim pass through).</summary>
        static void Solid(Transform root, Vector3 c, Vector3 s) => Wall(root, "Solid", c, s, null, Layers.IgnoreRaycast);

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

        static void Sign(Transform root, string text, Vector3 pos, float yaw, float size, float width, Color? color)
        {
            var t = TextFactory.World(root, text, size, color ?? new Color(0.96f, 0.96f, 0.94f), TMPro.TextAlignmentOptions.Center, false, width);
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
    /// The pier hydrant (New York's set piece): turn its yellow wheel and the outlet cap swings off, water arcs across the
    /// boards with a low rumble and a patter of splashes, mist and droplets rise where it lands and a puddle spreads; turn
    /// it again and it stops, the puddle slowly drying. No fail state; nothing waits on it.
    /// </summary>
    public class NyHydrant : Interactable
    {
        Transform m_Space, m_Wheel, m_Cap, m_Puddle;
        Quaternion m_WheelRest, m_CapRest;
        ParticleSystem[] m_Water;
        Vector3 m_Land;
        Cell m_Cell;
        AudioSource m_Rumble;
        float m_Level, m_Patter, m_Spin;
        bool m_Open, m_Busy;
        static bool s_Told;

        public bool IsOpen => m_Open;
        /// <summary>Times the wheel has been turned (tests).</summary>
        public int Turns { get; private set; }

        public void Setup(Transform space, Transform wheel, Transform cap, Transform puddle, Vector3 landLocal, params ParticleSystem[] water)
        {
            m_Space = space;
            m_Wheel = wheel; m_WheelRest = wheel.localRotation;
            m_Cap = cap; m_CapRest = cap.localRotation;
            m_Puddle = puddle;
            m_Land = landLocal;
            m_Water = water;
            m_Cell = GetComponentInParent<Cell>();
            Prompt = "Open the hydrant";
            MaxDistance = 2.4f;
            MarkerPoint = wheel;
            if (m_Puddle != null) m_Puddle.gameObject.SetActive(false);
        }

        bool Live => m_Cell == null || m_Cell.Mode == CellMode.Active;

        public override string GetPrompt(Interactor who) => m_Open ? "Close the hydrant" : "Open the hydrant";

        public override bool CanUse(Interactor who) => base.CanUse(who) && !m_Busy && m_Wheel != null;

        public override void Use(Interactor who)
        {
            if (!CanUse(who)) return;
            Turns++;
            AudioDirector.Haptic(who != null ? who.Side : HandSide.Desktop, 0.35f, 0.12f);
            StartCoroutine(Turn(!m_Open));
            base.Use(who);
        }

        IEnumerator Turn(bool open)
        {
            m_Busy = true;
            AudioDirector.I?.Play("lever_clunk", m_Wheel.position, 0.75f, open ? 0.7f : 0.85f);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.8f;
                m_Spin += (open ? 1f : -1f) * 540f * Time.deltaTime;
                m_Wheel.localRotation = m_WheelRest * Quaternion.AngleAxis(m_Spin, Vector3.forward);
                if (!open) SetCap(1f - Mathf.Clamp01(t * 1.4f));
                yield return null;
            }
            m_Open = open;
            if (open)
            {
                foreach (var ps in m_Water) if (ps != null) ps.Play();
                if (Live && AudioDirector.I != null)
                {
                    AudioDirector.I.Play("drop_hard", transform.position, 0.6f, 0.7f);
                    m_Rumble = AudioDirector.I.Loop("fridge_hum_loop", transform, 0.4f, Bus.SFX, 1f, 1.5f, 16f);
                    if (m_Rumble != null) m_Rumble.pitch = 0.55f;
                }
                if (!s_Told) { s_Told = true; Hud.I?.ShowCaption("the hydrant coughs, then gushes across the boards", 3f); }
                float c = 0f;
                while (c < 1f) { c += Time.deltaTime / 0.25f; SetCap(c); yield return null; }
            }
            else
            {
                foreach (var ps in m_Water) if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (m_Rumble != null) { AudioDirector.I?.FadeOutAndStop(m_Rumble, 0.6f); m_Rumble = null; }
                SetCap(0f);
            }
            m_Busy = false;
        }

        void SetCap(float k)
        {
            if (m_Cap != null) m_Cap.localRotation = m_CapRest * Quaternion.AngleAxis(105f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k)), Vector3.forward);
        }

        void OnEnable()
        {
            // A cell coming back from hidden restarts its systems: a hydrant left open keeps running.
            if (!m_Open || m_Water == null) return;
            foreach (var ps in m_Water) if (ps != null && !ps.isPlaying) ps.Play();
            if (m_Rumble != null && !m_Rumble.isPlaying) m_Rumble.Play();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            m_Level = Mathf.Clamp01(m_Level + (m_Open && !m_Busy ? dt / 6f : -dt / 30f));
            if (m_Puddle != null)
            {
                bool show = m_Level > 0.005f;
                if (m_Puddle.gameObject.activeSelf != show) m_Puddle.gameObject.SetActive(show);
                if (show)
                {
                    float s = Mathf.Sqrt(m_Level);
                    m_Puddle.localScale = new Vector3(1.7f * s, 1f, 1.0f * s);
                }
            }
            if (!m_Open || m_Busy || !Live || m_Space == null) return;
            m_Patter -= dt;
            if (m_Patter > 0f) return;
            m_Patter = Random.Range(0.06f, 0.13f);
            var p = m_Space.TransformPoint(m_Land + new Vector3(Random.Range(-0.6f, 0.6f), 0.05f, Random.Range(-0.35f, 0.35f)));
            AudioDirector.I?.Play("foot_wet_1", p, Random.Range(0.45f, 0.8f), Random.Range(0.8f, 1.25f));
        }
    }

    /// <summary>A steam grate in the pier boards: it breathes now and then, and huffs a cloud when someone steps on it.</summary>
    public class NySteamGrate : MonoBehaviour
    {
        ParticleSystem m_Huff;
        Vector2 m_Half;
        Cell m_Cell;
        float m_Breath, m_Cool;
        bool m_On;

        /// <summary>Clouds huffed by being stepped on (tests).</summary>
        public int Huffs { get; private set; }

        /// <summary>The drift is a looping system of its own; the huff is burst-only (breaths and footsteps).</summary>
        public void Setup(ParticleSystem drift, ParticleSystem huff, Vector2 halfSize)
        {
            m_Huff = huff;
            m_Half = halfSize;
            m_Cell = GetComponentInParent<Cell>();
            m_Breath = Random.Range(3f, 7f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || m_Huff == null) return;
            m_Cool -= dt;
            m_Breath -= dt;
            if (m_Breath <= 0f) { m_Breath = Random.Range(6f, 12f); m_Huff.Emit(12); }
            bool live = m_Cell == null || m_Cell.Mode == CellMode.Active;
            var rig = Game.Rig;
            bool on = false;
            if (live && rig != null)
            {
                var p = transform.InverseTransformPoint(rig.Feet);
                on = Mathf.Abs(p.x) < m_Half.x + 0.15f && Mathf.Abs(p.z) < m_Half.y + 0.15f && p.y > -0.5f && p.y < 0.8f;
            }
            if (on && !m_On && m_Cool <= 0f)
            {
                m_Cool = 2.4f;
                Huffs++;
                m_Huff.Emit(34);
                AudioDirector.I?.Play("shift_whoosh", transform.position + Vector3.up * 0.3f, 0.2f, 1.6f);
            }
            m_On = on;
        }
    }
}

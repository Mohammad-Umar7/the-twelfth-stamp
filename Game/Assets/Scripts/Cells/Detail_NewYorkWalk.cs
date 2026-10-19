using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    // The way out to the landmark (open world: after the seal is verified the stamp waits on the statue's island and the
    // player runs to it with the Passenger behind). A long timber pier leaves the harbour railing through a gate and runs
    // straight down the sight line to the statue, lamps fading into the mist, and lands on the island's seawall; a paved
    // path then leads between railings to a lamp-lit circle where the stamp hangs (Landmarks.TryStampSpot).
    public partial class NewYorkCell
    {
        /// <summary>
        /// The island pier. Distances run along the statue's sight line from the cell origin (the axis the vista's island
        /// faces), offsets to the walker's left. Heights: the deck is at the apron's level (0) to 112 m out, then eases down
        /// to the island path (-0.2; the island's granite is at -0.3, its lawn at -0.22) over the last 6 m. The island's
        /// front edge (118.2 m) and front lamp (120.3 m) come from the vista's seeded outline (Landmarks.NewYorkSite).
        /// </summary>
        void BuildIslandPier(Transform g, KitBatch k, NyPalette p, Color sodium)
        {
            var flat = new Vector3(LibertyRoot.x, 0f, LibertyRoot.z);
            float toRoot = flat.magnitude;
            var u = flat / toRoot;                     // out along the pier, toward the statue
            var left = new Vector3(-u.z, 0f, u.x);     // the walker's left (north-east)
            float yaw = Mathf.Atan2(u.x, u.z) * Mathf.Rad2Deg;
            var q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 At(float d, float lat, float y) => u * d + left * lat + Vector3.up * y;
            const float dDeck = 7.92f, dRamp = 112f, dRampEnd = 118.3f, dIsle = 118.2f, dPath = 117.6f, half = 1.6f, side = 1.72f, plazaR = 4.65f;
            float dStamp = toRoot - 30f;                   // the vista's stamp spot: Origin + ToViewer * 30
            float dIsleLamp = toRoot + 8f - 56f * 0.95f;   // the vista's island lamp that stands on the axis
            float dPlazaBack = dStamp - plazaR * Mathf.Cos(22.5f * Mathf.Deg2Rad);
            float pitch = Mathf.Atan2(0.2f, dRampEnd - dRamp) * Mathf.Rad2Deg;
            var qr = Quaternion.Euler(pitch, yaw, 0f);
            float RailLineStart(float s) => (6.5f - left.x * s * side) / u.x; // where a side line meets the railing (x = 6.5)

            var deck = Mats.Lit(new Color(0.78f, 0.76f, 0.74f), 0.35f, 0f, "wood_oak_floor");
            var pool = Mats.Particle(Tex.SoftDot, true, Color.white, 1f);

            // ---- The gate in the harbour railing: iron posts, an arch and its sign, a timber plate over the quay edge.
            foreach (var gz in new[] { -4.97f, -1.1f })
            {
                k.Box(p.Frame, new Vector3(6.5f, 1.65f, gz), new Vector3(0.16f, 3.3f, 0.16f));
                k.Of(p.Frame).AddSphere(new Vector3(6.5f, 3.38f, gz), 0.09f, 8, 6);
            }
            k.Of(p.Frame).AddTube(new[] { new Vector3(6.5f, 3.2f, -4.97f), new Vector3(6.5f, 3.58f, -4.1f), new Vector3(6.5f, 3.72f, -3.035f), new Vector3(6.5f, 3.58f, -1.97f), new Vector3(6.5f, 3.2f, -1.1f) }, 0.035f, 8);
            k.Box(p.Frame, new Vector3(6.5f, 2.95f, -3.035f), new Vector3(0.05f, 0.38f, 2.5f));
            Sign(g, "TO THE ISLAND", 0.14f, new Color(0.95f, 0.92f, 0.84f), new Vector3(6.47f, 2.95f, -3.035f), 90f, 2.3f);
            k.Box(deck, new Vector3(7.175f, -0.088f, -3.025f), new Vector3(1.55f, 0.2f, 3.65f), 1.6f);

            // ---- The deck: oak planks running out to the statue, a timber fascia, piles rising as rail posts, two rails.
            k.Box(deck, At((dDeck + dRamp) / 2f, 0f, -0.1f), new Vector3(2f * half, 0.2f, dRamp - dDeck), q, 1.6f);
            k.Box(deck, At((dRamp + dRampEnd) / 2f, 0f, -0.2f), new Vector3(2f * half, 0.2f, dRampEnd - dRamp + 0.02f), qr, 1.6f);
            foreach (var s in new[] { 1f, -1f })
            {
                float d0 = RailLineStart(s);
                k.Box(p.Wood, At((dDeck + dRamp) / 2f, s * 1.62f, -0.16f), new Vector3(0.1f, 0.3f, dRamp - dDeck), q);
                k.Box(p.Wood, At((d0 + dIsle) / 2f, s * side, 1.06f), new Vector3(0.1f, 0.08f, dIsle - d0), q);
                k.Box(p.Wood, At((d0 + dIsle) / 2f, s * side, 0.56f), new Vector3(0.07f, 0.06f, dIsle - d0), q);
                for (float d = 10f; d <= 114f; d += 4f)
                {
                    k.Of(p.Wood).AddCylinder(At(d, s * side, -2.3f), 0.14f, 3.42f, 8);
                    k.Box(p.Wood, At(d + 2f, s * side, 0.56f), new Vector3(0.1f, 1.12f, 0.1f), q);
                }
            }
            // Bents under the deck: a cap beam on each pair of piles, cross braces on every other one.
            for (float d = 10f; d <= 114f; d += 4f)
            {
                k.Box(p.Wood, At(d, 0f, -0.33f), new Vector3(3.8f, 0.22f, 0.22f), q);
                if (((int)d / 4) % 2 == 0)
                    foreach (var r in new[] { 17f, -17f })
                        k.Box(p.Wood, At(d, 0f, -1.0f), new Vector3(3.6f, 0.12f, 0.12f), q * Quaternion.Euler(0f, 0f, r));
            }
            // Cleats on the edges and one lifebuoy hung on the rail.
            for (float d = 20f; d <= 104f; d += 12f)
                foreach (var s in new[] { 1f, -1f })
                {
                    k.Box(p.Frame, At(d, s * 1.48f, 0.04f), new Vector3(0.1f, 0.08f, 0.1f), q);
                    k.Box(p.Frame, At(d, s * 1.48f, 0.1f), new Vector3(0.08f, 0.04f, 0.36f), q);
                }
            k.Of(p.Orange).AddTorus(At(40f, side + 0.1f, 0.78f), 0.26f, 0.07f, 16, 6, Quaternion.FromToRotation(Vector3.up, left));
            // Lamps on alternate sides every 12 m, their light lying on the planks; one real light over the middle.
            for (int i = 0; i < 9; i++)
            {
                float d = 14f + i * 12f, s = i % 2 == 0 ? 1f : -1f;
                AcornLamp(k, p, At(d, s * 1.35f, 0f));
                Solid(g, At(d, s * 1.35f, 1.7f), new Vector3(0.3f, 3.4f, 0.3f));
                Pool(k.Of(pool), At(d, s * 0.8f, 0.013f), 2.3f, sodium * 0.32f);
            }
            AddPointLight(g, At(63f, 0f, 3.2f), sodium, 1.6f, 9f);
            // Halfway out, a single hard-shell case standing on the boards. Nobody's.
            var bag = Quaternion.Euler(0f, yaw + 20f, 0f);
            k.Box(p.Frame, At(63.4f, 0.35f, 0.34f), new Vector3(0.46f, 0.66f, 0.26f), bag);
            k.Box(p.Steel, At(63.4f, 0.35f, 0.69f), new Vector3(0.16f, 0.04f, 0.04f), bag);
            Solid(g, At(63.4f, 0.35f, 0.35f), new Vector3(0.5f, 0.7f, 0.5f));

            // ---- The island: a paved path round the vista's front lamp to a railed, lamp-lit circle at the stamp.
            float pathEnd = dPlazaBack + 0.4f;
            k.Box(p.Concrete, At((dPath + pathEnd) / 2f, 0f, -0.27f), new Vector3(3.4f, 0.14f, pathEnd - dPath), q, 2f);
            var disc = new List<Vector2>();
            for (int i = 0; i < 16; i++) { float a = i / 16f * Mathf.PI * 2f; disc.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 4.5f); }
            k.Of(p.Concrete).AddPrism(disc, -0.34f, -0.2f, 2f, true, At(dStamp, 0f, 0f), q);
            k.Of(p.Concrete).AddCylinder(At(dIsleLamp, 0f, -0.2f), 0.42f, 0.45f, 8);
            Solid(g, At(dIsleLamp, 0f, 0.05f), new Vector3(0.84f, 0.6f, 0.84f));
            foreach (var s in new[] { 1f, -1f })
            {
                for (float d = dIsle; d <= dPlazaBack + 0.01f; d += (dPlazaBack - dIsle) / 9f)
                    k.Box(p.Frame, At(d, s * 1.78f, 0.28f), new Vector3(0.05f, 0.96f, 0.05f), q);
                foreach (var y in new[] { 0.3f, 0.72f })
                    k.Box(p.Frame, At((dIsle + dPlazaBack) / 2f, s * 1.78f, y), new Vector3(0.04f, 0.04f, dPlazaBack - dIsle), q);
            }
            Vector3 Corner(int i, float y) { float a = (22.5f + 45f * i) * Mathf.Deg2Rad; return At(dStamp + plazaR * Mathf.Cos(a), plazaR * Mathf.Sin(a), y); }
            for (int i = 0; i < 8; i++)
            {
                if (i == 3) continue; // the side facing the pier is the way in
                var a = Corner(i, 0f);
                var b = Corner(i + 1, 0f);
                var along = Quaternion.LookRotation(b - a, Vector3.up);
                foreach (var y in new[] { 0.3f, 0.72f }) k.Box(p.Frame, (a + b) / 2f + Vector3.up * y, new Vector3(0.04f, 0.04f, Vector3.Distance(a, b)), along);
                k.Box(p.Frame, a + Vector3.up * 0.28f, new Vector3(0.05f, 0.96f, 0.05f));
                k.Box(p.Frame, (a + b) / 2f + Vector3.up * 0.28f, new Vector3(0.05f, 0.96f, 0.05f));
                Col(g, "CircleSide", (a + b) / 2f + Vector3.up * 2.5f, new Vector3(0.15f, 5f, Vector3.Distance(a, b)), along, Layers.Default);
            }
            k.Box(p.Frame, Corner(4, 0.28f), new Vector3(0.05f, 0.96f, 0.05f));
            foreach (var deg in new[] { 45f, 135f, 225f, 315f })
            {
                float a = deg * Mathf.Deg2Rad;
                var lp = At(dStamp + 3.9f * Mathf.Cos(a), 3.9f * Mathf.Sin(a), -0.2f);
                AcornLamp(k, p, lp);
                Solid(g, lp + Vector3.up * 1.7f, new Vector3(0.3f, 3.4f, 0.3f));
                Pool(k.Of(pool), lp + Vector3.up * 0.015f, 1.8f, sodium * 0.25f);
            }

            // ---- Colliders. Walkable (Floor layer, footsteps): the gate plate, the deck, the ramp, the path and the circle.
            Col(g, "PierGatePlate", new Vector3(7.175f, -0.088f, -3.025f), new Vector3(1.55f, 0.2f, 3.65f), Quaternion.identity, Layers.Floor, "wood");
            Col(g, "PierDeck", At((dDeck + dRamp) / 2f, 0f, -0.1f), new Vector3(2f * half, 0.2f, dRamp - dDeck), q, Layers.Floor, "wood");
            Col(g, "PierRamp", At((dRamp + dRampEnd) / 2f, 0f, -0.2f), new Vector3(2f * half, 0.2f, dRampEnd - dRamp + 0.02f), qr, Layers.Floor, "wood");
            Col(g, "IslandPath", At((dPath + pathEnd) / 2f, 0f, -0.27f), new Vector3(3.4f, 0.14f, pathEnd - dPath), q, Layers.Floor, "stone");
            Col(g, "IslandCircle", At(dStamp, 0f, -0.27f), new Vector3(9f, 0.14f, 9f), q, Layers.Floor, "stone");
            // Sides: on the Default layer so a VR teleport arc stops on them instead of landing in the water. The pier's run
            // from the gate posts; the island's are taller (the island's own edge is unrailed).
            foreach (var s in new[] { 1f, -1f })
            {
                float d0 = RailLineStart(s);
                Col(g, "PierSide", At((d0 + dPath) / 2f, s * side, 1.25f), new Vector3(0.15f, 2.5f, dPath - d0), q, Layers.Default);
                Col(g, "IslandSide", At((dPath + dPlazaBack) / 2f, s * 1.78f, 2.5f), new Vector3(0.15f, 5f, dPlazaBack - dPath), q, Layers.Default);
                // Catchers at deck height beside the pier (invisible): the Passenger's floor probe finds these when it cuts
                // a corner over the water, instead of dropping to the harbour surface and running on under the deck.
                Col(g, "PierCatcher", At(56.75f, s * 5.65f, -0.05f), new Vector3(7.5f, 0.04f, 109.5f), q, Layers.Default); // D 2..111.5 (under the apron near the gate)
                Col(g, "PierCatcher", At((111.5f + 118f) / 2f, s * 5.65f, -0.15f), new Vector3(7.5f, 0.04f, 6.5f), qr, Layers.Default);
            }
            // The same round the terminal, for when it comes from behind a wall.
            Col(g, "TerminalCatcher", new Vector3(-8.85f, -0.05f, 6f), new Vector3(30.3f, 0.04f, 40f), Quaternion.identity, Layers.Default);
            // The terminal keeps its old limits (the pier gate cut into the east one), inside one box round everything.
            Col(g, "Bound", new Vector3(0f, 2.5f, 9.65f), new Vector3(13.2f, 5f, 0.3f), Quaternion.identity, Layers.Default);
            Col(g, "Bound", new Vector3(0f, 2.5f, -5.65f), new Vector3(13.2f, 5f, 0.3f), Quaternion.identity, Layers.Default);
            Col(g, "Bound", new Vector3(-6.45f, 2.5f, 2f), new Vector3(0.3f, 5f, 15f), Quaternion.identity, Layers.Default);
            Col(g, "Bound", new Vector3(6.45f, 2.5f, -5.225f), new Vector3(0.3f, 5f, 0.55f), Quaternion.identity, Layers.Default);
            Col(g, "Bound", new Vector3(6.45f, 2.5f, 4.19f), new Vector3(0.3f, 5f, 10.62f), Quaternion.identity, Layers.Default);
            AddBounds(new Vector3(60.95f, 0f, -26.6f), new Vector3(135.1f, 6f, 72.8f), 6f);
            Landmarks.MarkReachable(this);
        }

        static GameObject Col(Transform g, string name, Vector3 center, Vector3 size, Quaternion rot, int layer, string surface = null)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(g, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rot;
            go.AddComponent<BoxCollider>().size = size;
            if (surface != null) go.AddComponent<Surface>().Kind = surface;
            return go;
        }

        /// <summary>A soft pool of lamplight lying on a floor (an additive quad, coloured through the vertex colour).</summary>
        static void Pool(MeshBuilder b, Vector3 c, float r, Color color)
        {
            var prev = b.VertexColor;
            color.a = 1f; // additive: the colour alone sets the strength
            b.VertexColor = color;
            b.AddQuad(c + new Vector3(-r, 0f, -r), c + new Vector3(-r, 0f, r), c + new Vector3(r, 0f, r), c + new Vector3(r, 0f, -r),
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));
            b.VertexColor = prev;
        }
    }
}

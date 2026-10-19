using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    // Japan's street ends, poles and wires, lights and clutter (see Detail_Japan.cs for the houses).
    public partial class JapanCell
    {
        /// <summary>
        /// Both ends of the street. North: earthen walls with tiled copings run from the last houses to a timber gate, all kept
        /// under about 4.2 m so the pagoda reads above the gate; a pine leans over the right-hand wall and low roofs beyond the
        /// walls carry the town on (well clear of the gate's sight line). South, past the arrival: a board fence with a wicket
        /// door closes the street in the dark.
        /// </summary>
        void BuildStreetEnds(StreetCtx c)
        {
            var m = c.M;
            foreach (float s in new[] { -1f, 1f })
            {
                var k = c.At(s, 12f);
                Wall(k, m, new Vector3(s * 3.96f, 0f, 11.1f), new Vector3(s * 3.96f, 0f, 13.69f), 0.48f, 2.5f);
                Wall(k, m, new Vector3(s * 2.46f, 0f, 13.45f), new Vector3(s * 3.72f, 0f, 13.45f), 0.48f, 2.5f);
            }
            // The gate: posts on stone bases with iron bands, tie beams, closed plank doors with a lattice head, a tiled cap.
            var kg = c.At(1f, 13f);
            const float gz = 13.3f;
            foreach (float x in new[] { -2.3f, 2.3f })
            {
                kg.Box(m.Stone, new Vector3(x, 0.13f, gz), new Vector3(0.5f, 0.26f, 0.5f), 1.2f);
                kg.Box(m.Timber, new Vector3(x, 2.0f, gz), new Vector3(0.3f, 3.74f, 0.3f));
                foreach (float y in new[] { 0.42f, 3.2f }) kg.Box(m.Black, new Vector3(x, y, gz), new Vector3(0.32f, 0.06f, 0.32f));
            }
            // Beams stop flush with the posts (no overhang): a roofed street gate with doors, nothing like a shrine gate. The
            // two door leaves swing on their own pivots (BuildRoute): shut until the puzzle is solved, then open onto the lane.
            kg.Box(m.Timber, new Vector3(0f, 3.6f, gz), new Vector3(4.9f, 0.26f, 0.3f));
            kg.Box(m.Timber, new Vector3(0f, 3.1f, gz), new Vector3(4.3f, 0.16f, 0.24f));
            for (int i = 0; i < 34; i++) kg.Box(m.Timber, new Vector3(-2.0f + i * 0.121f, 2.86f, gz + 0.04f), new Vector3(0.035f, 0.38f, 0.04f));
            kg.Box(m.Timber, new Vector3(0f, 2.69f, gz + 0.04f), new Vector3(4.3f, 0.06f, 0.08f));
            Coping(kg, m.Tile, new Vector3(-2.8f, 3.74f, gz), new Vector3(2.8f, 3.74f, gz), 1.25f, 26f);
            Pine(c.At(1f, 12f), m, new Vector3(5.5f, 0f, 12.6f), 1f, 11);
            foreach (var (x, z, w, d, h) in new[] { (-7.6f, 17.5f, 6f, 5f, 4.2f), (-9.4f, 24.5f, 7f, 6f, 5.4f), (7.9f, 18.5f, 6f, 6f, 4.6f), (9.8f, 26f, 8f, 6f, 5.8f) })
                TownRoof(c, new Vector3(x, 0f, z), w, d, h, (int)(x * 10f));
            // South, behind the arrival: a weathered board fence with a wicket door under a tiled cap.
            var kb = c.At(-1f, -12f);
            kb.Box(m.Stone, new Vector3(0f, 0.13f, -12f), new Vector3(7.6f, 0.26f, 0.36f), 1.2f);
            for (int p = 0; p < 19; p++) kb.Box(m.Weathered, new Vector3(-3.6f + p * 0.4f, 1.5f, -12f), new Vector3(0.39f, 2.5f, 0.05f), 1.5f);
            foreach (float y in new[] { 0.55f, 2.35f }) kb.Box(m.Timber, new Vector3(0f, y, -11.96f), new Vector3(7.6f, 0.1f, 0.05f));
            kb.Box(m.Timber, new Vector3(0f, 1.0f, -11.94f), new Vector3(0.95f, 1.8f, 0.04f));
            kb.Box(m.Weathered, new Vector3(0f, 0.98f, -11.92f), new Vector3(0.8f, 1.66f, 0.03f), 1.5f);
            Coping(kb, m.Tile, new Vector3(-3.8f, 2.75f, -12f), new Vector3(3.8f, 2.75f, -12f), 0.6f);
            // One small lantern still burns by the wicket door: the street goes on past it, somewhere.
            kb.Box(m.Timber, new Vector3(0.72f, 2.08f, -11.8f), new Vector3(0.04f, 0.04f, 0.34f));
            Chochin(c, kb, new Vector3(0.72f, 1.78f, -11.66f), m.LanternCream, true, 0.55f, 2.08f);
        }

        /// <summary>An earthen wall on a stone footing with a tiled coping, from a to b (on the ground).</summary>
        static void Wall(KitBatch k, JapanMats m, Vector3 a, Vector3 b, float thick, float height)
        {
            var d = b - a;
            float len = d.magnitude;
            var rot = Quaternion.LookRotation(d / len, Vector3.up);
            var mid = (a + b) * 0.5f;
            k.Box(m.Stone, mid + Vector3.up * 0.18f, new Vector3(thick + 0.08f, 0.36f, len), rot, 1.2f);
            k.Box(m.Earth, mid + Vector3.up * (0.36f + (height - 0.36f) * 0.5f), new Vector3(thick, height - 0.36f, len), rot, 2f);
            Coping(k, m.Tile, a + Vector3.up * height, b + Vector3.up * height, thick + 0.32f);
        }

        /// <summary>A small gabled tile coping along a horizontal line a-b (its eaves at a.y), with a round ridge.</summary>
        static void Coping(KitBatch k, Material tile, Vector3 a, Vector3 b, float width, float pitchDeg = 24f)
        {
            var dir = b - a;
            float len = dir.magnitude;
            dir /= len;
            var rot0 = Quaternion.LookRotation(dir, Vector3.up);
            var right = rot0 * Vector3.right;
            float t = Mathf.Tan(pitchDeg * Mathf.Deg2Rad), half = width * 0.5f, mid = half * 0.5f;
            var centre = (a + b) * 0.5f;
            foreach (float s in new[] { -1f, 1f })
                k.Box(tile, centre + right * (s * mid) + Vector3.up * (mid * t), new Vector3(half / Mathf.Cos(pitchDeg * Mathf.Deg2Rad) + 0.02f, 0.05f, len + 0.1f), rot0 * Quaternion.Euler(0f, 0f, -s * pitchDeg), 1.5f);
            k.Of(tile).AddCylinder(a - dir * 0.05f + Vector3.up * (half * t + 0.02f), 0.06f, len + 0.1f, 8, 1f, Quaternion.FromToRotation(Vector3.up, dir));
            foreach (var e in new[] { a - dir * 0.05f, b + dir * 0.05f }) Tri2(k.Of(tile), e - right * half, e + right * half, e + Vector3.up * (half * t)); // gable ends
        }

        /// <summary>A house beyond the end walls: plaster block, gabled tiled roof, a lit window or two toward the street.</summary>
        static void TownRoof(StreetCtx c, Vector3 at, float w, float d, float h, int seed)
        {
            var m = c.M;
            var k = c.At(at.x, 12f);
            var rnd = new System.Random(seed);
            k.Box(m.Plaster, at + Vector3.up * ((h - 1f) * 0.5f), new Vector3(w, h + 1f, d), 2.2f);
            Coping(k, m.Tile, at + new Vector3(0f, h, -d * 0.5f - 0.3f), at + new Vector3(0f, h, d * 0.5f + 0.3f), w + 0.9f, 26f);
            float rise = (w + 0.9f) * 0.5f * Mathf.Tan(26f * Mathf.Deg2Rad) - 0.03f;
            foreach (float s in new[] { -1f, 1f })
                Tri2(k.Of(m.Plaster), at + new Vector3(-w * 0.5f, h, s * d * 0.5f), at + new Vector3(w * 0.5f, h, s * d * 0.5f), at + new Vector3(0f, h + rise, s * d * 0.5f));
            k.Box(m.Timber, at + new Vector3(0f, h - 0.1f, -d * 0.5f - 0.02f), new Vector3(w, 0.16f, 0.06f));
            for (int i = 0; i < 2; i++)
            {
                if (rnd.NextDouble() < 0.3) continue;
                var p = at + new Vector3((i == 0 ? -0.25f : 0.25f) * w, h * 0.55f, -d * 0.5f - 0.01f);
                c.Glow.Box(m.PaperFaint, p, new Vector3(0.9f, 0.8f, 0.02f));
                for (int b = -1; b <= 1; b++) k.Box(m.Timber, p + new Vector3(b * 0.3f, 0f, -0.015f), new Vector3(0.03f, 0.82f, 0.02f));
                k.Box(m.Timber, p + new Vector3(0f, 0f, -0.015f), new Vector3(0.92f, 0.03f, 0.02f));
            }
            // The side facing the lane: a timber band and two windows above the lane wall, one lit.
            float sx = -Mathf.Sign(at.x);
            var side = at + new Vector3(sx * (w * 0.5f + 0.01f), 0f, 0f);
            k.Box(m.Timber, side + new Vector3(sx * 0.02f, 1.55f, 0f), new Vector3(0.04f, 0.12f, d));
            for (int i = 0; i < 2; i++)
            {
                var p = side + new Vector3(0f, 2.45f, (i == 0 ? -0.25f : 0.25f) * d);
                (i == seed % 2 ? c.Glow : k).Box(i == seed % 2 ? m.PaperFaint : m.PaperDim, p, new Vector3(0.02f, 0.8f, 0.9f));
                for (int b = -1; b <= 1; b++) k.Box(m.Timber, p + new Vector3(sx * 0.015f, 0f, b * 0.3f), new Vector3(0.02f, 0.82f, 0.03f));
                k.Box(m.Timber, p + new Vector3(sx * 0.015f, 0f, 0f), new Vector3(0.02f, 0.03f, 0.92f));
            }
        }

        /// <summary>
        /// Concrete poles (alternating sides) with crossarms, insulators, step bolts and two transformers. Wires sag along each
        /// side and cross high over the street, never across the pagoda above the end gate; service drops run to the houses; a
        /// failing lamp on the last pole lights the gate, the route marks and whatever stands there.
        /// </summary>
        void BuildPoles(StreetCtx c)
        {
            Vector3 l1 = new Vector3(-3.4f, 0f, -7.35f), l2 = new Vector3(-3.4f, 0f, 1.9f), l3 = new Vector3(-3.4f, 0f, 11.3f);
            Vector3 r1 = new Vector3(3.4f, 0f, -2.7f), r2 = new Vector3(3.4f, 0f, 6.5f), r3 = new Vector3(3.4f, 0f, 12.4f);
            Pole(c, l1, false); Pole(c, l2, false); Pole(c, l3, true);
            Pole(c, r1, true); Pole(c, r2, false); Pole(c, r3, false);
            Vector3 Top(Vector3 p, float x) => p + new Vector3(x, 7.62f, 0f);
            Vector3 Low(Vector3 p, float x) => p + new Vector3(x, 6.97f, 0f);
            void Run(Vector3 a, Vector3 b)
            {
                for (int i = -1; i <= 1; i++) Wire(c, Top(a, i * 0.55f), Top(b, i * 0.55f), 0.42f + 0.06f * i);
                foreach (int i in new[] { -1, 1 }) Wire(c, Low(a, i * 0.38f), Low(b, i * 0.38f), 0.5f);
                Wire(c, a + Vector3.up * 5.9f, b + Vector3.up * 5.9f, 0.62f, 0.034f);
            }
            Run(l1, l2); Run(l2, l3); Run(r1, r2); Run(r2, r3);
            Wire(c, Top(l1, 0.55f), Top(r1, -0.55f), 0.5f);
            Wire(c, Low(l2, 0.38f), Low(r1, -0.38f), 0.55f);
            Wire(c, Top(l2, 0.55f), Top(r2, -0.55f), 0.45f);
            Wire(c, l2 + Vector3.up * 5.9f, r2 + Vector3.up * 5.9f, 0.7f, 0.03f);
            foreach (var (p, to) in new[] { (l2, new Vector3(-3.93f, 4.75f, 0.9f)), (l2, new Vector3(-3.93f, 4.6f, 3.0f)), (r1, new Vector3(3.93f, 4.7f, -4.0f)),
                (r1, new Vector3(3.93f, 4.8f, -1.6f)), (r2, new Vector3(3.93f, 4.7f, 5.2f)), (r2, new Vector3(3.93f, 4.6f, 7.6f)), (l1, new Vector3(-3.93f, 4.6f, -5.8f)), (l3, new Vector3(-3.93f, 4.9f, 9.6f)) })
                Wire(c, Low(p, 0f), to, 0.25f, 0.012f);
            Wire(c, Top(l1, 0f), new Vector3(-3.4f, 7.1f, -18f), 0.5f);
            Wire(c, Top(l3, -0.55f), new Vector3(-9f, 7f, 19f), 0.5f);
            Wire(c, Top(r3, 0.55f), new Vector3(9f, 7.2f, 20f), 0.5f);
            PoleLamp(c, l2, false);
            PoleLamp(c, r2, false);
            PoleLamp(c, r3, true);
        }

        static void Pole(StreetCtx c, Vector3 p, bool transformer)
        {
            var m = c.M;
            var k = c.At(p.x, p.z);
            float s = Mathf.Sign(p.x);
            k.Of(m.Stone).AddFrustum(p, 0.17f, 0.12f, 8.2f, 10, false, true, 2f);
            k.Box(m.Metal, p + new Vector3(0f, 7.6f, 0f), new Vector3(1.32f, 0.08f, 0.09f));
            k.Box(m.Metal, p + new Vector3(0f, 6.95f, 0f), new Vector3(0.92f, 0.07f, 0.08f));
            foreach (var (x, y) in new[] { (-0.55f, 7.64f), (0f, 7.64f), (0.55f, 7.64f), (-0.38f, 6.99f), (0.38f, 6.99f) })
                k.Of(m.Frosted).AddCylinder(p + new Vector3(x, y, 0f), 0.035f, 0.1f, 8);
            for (int i = 0; i < 10; i++)
            {
                float y = 2.1f + i * 0.42f;
                Rod(k, m.Metal, p + new Vector3(0f, y, 0f), p + new Vector3(0f, y, i % 2 == 0 ? 0.3f : -0.3f), 0.022f);
            }
            k.Of(m.Black).AddCylinder(p + new Vector3(0f, 5.86f, 0f), 0.15f, 0.06f, 10);
            k.Box(m.Metal, p + new Vector3(-s * 0.16f, 2.3f, 0f), new Vector3(0.012f, 0.3f, 0.15f));
            if (!transformer) return;
            var tc = p + new Vector3(-s * 0.38f, 5.2f, 0f);
            k.Of(m.Frosted).AddCylinder(tc, 0.24f, 0.8f, 12);
            k.Of(m.Frosted).AddCylinder(tc + Vector3.up * 0.8f, 0.26f, 0.05f, 12);
            k.Box(m.Metal, p + new Vector3(-s * 0.18f, 5.5f, 0f), new Vector3(0.36f, 0.08f, 0.36f));
            foreach (float dz in new[] { -0.1f, 0.1f })
            {
                k.Of(m.Frosted).AddCylinder(tc + new Vector3(0f, 0.85f, dz), 0.025f, 0.12f, 6);
                Wire(c, tc + new Vector3(0f, 0.97f, dz), p + new Vector3(-s * 0.38f, 6.97f, 0f), 0.05f, 0.01f);
            }
        }

        /// <summary>A pole lamp on an arm over the street. The lit one is a failing lamp (its own light and lens instance).</summary>
        void PoleLamp(StreetCtx c, Vector3 p, bool lit)
        {
            var m = c.M;
            var k = c.At(p.x, p.z);
            float s = Mathf.Sign(p.x);
            var a1 = p + new Vector3(-s * 0.85f, 5.22f, 0f);
            Rod(k, m.Metal, p + new Vector3(0f, 5f, 0f), a1, 0.05f);
            Rod(k, m.Metal, p + new Vector3(0f, 4.62f, 0f), p + new Vector3(-s * 0.5f, 5.12f, 0f), 0.035f);
            var head = a1 + new Vector3(-s * 0.14f, -0.02f, 0f);
            k.Box(m.Metal, head, new Vector3(0.44f, 0.09f, 0.2f));
            var lensPos = head + new Vector3(0f, -0.055f, 0f);
            if (!lit) { k.Box(m.Glass, lensPos, new Vector3(0.36f, 0.02f, 0.15f)); return; }
            var emission = new Color(1.1f, 1.25f, 1.35f);
            var lens = new Material(Mats.Emissive(new Color(0.85f, 0.95f, 1f), emission)) { name = "FailingLampLens" };
            var go = MeshFactory.Box("FailingLampLens", c.Root, lensPos, new Vector3(0.36f, 0.02f, 0.15f), lens, false);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var fl = go.AddComponent<FailingLamp>();
            fl.Light = AddPointLight(c.Root, lensPos + Vector3.down * 0.3f, new Color(0.72f, 0.86f, 0.95f), 3f, 8f);
            fl.Lens = lens;
            fl.Emission = emission;
        }

        /// <summary>A sagging wire (or cable) between two points, batched with the half of the street it hangs over.</summary>
        static void Wire(StreetCtx c, Vector3 a, Vector3 b, float sag, float thick = 0.016f)
        {
            var k = c.At((a.x + b.x) * 0.5f, (a.z + b.z) * 0.5f);
            const int n = 12;
            Vector3 P(float t) => Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1f - t);
            for (int i = 0; i < n; i++) Rod(k, c.M.Black, P(i / (float)n), P((i + 1) / (float)n), thick);
        }

        /// <summary>
        /// The street's practical lights and clutter: the house chochin lights, vending machines with their cool pool, a
        /// recycling bin, two bicycles, pots and a plant stand, fire buckets, crates, an A-board, downpipes, a rain chain, a
        /// hanging board, and the three lantern posts given stone feet, caps and visible iron hooks.
        /// </summary>
        void BuildStreetProps(StreetCtx c, Material postWood)
        {
            var m = c.M;
            // Each lit chochin lights its own pool. The short range keeps every street mesh (paving pieces, facade groups) to
            // six lights or fewer, leaving room under the forward renderer's eight for the hand lantern and an open tear.
            foreach (var h in s_Houses)
                if (h.lit) AddPointLight(c.Root, new Vector3(h.side * 2.92f, 2.42f, h.z), h.red ? new Color(1f, 0.46f, 0.3f) : new Color(1f, 0.66f, 0.38f), 1.5f, 3.3f);

            var l0 = new Facade(-1f, -5f);
            VendingMachine(c, l0, 1.25f, m.Frosted, 3);
            VendingMachine(c, l0, 2.3f, m.RedPaint, 4);
            AddPointLight(c.Root, l0.P(1.3f, 1.3f, 1.78f), new Color(0.78f, 0.88f, 1f), 0.8f, 3f);
            var k0 = c.At(-1f, -4.4f);
            l0.Box(k0, m.Frosted, 0.04f, 0.42f, 0f, 0.8f, 0.44f, 0.74f);
            k0.Of(m.Black).AddDisc(l0.P(0.424f, 0.62f, 0.59f), l0.Dir(1f, 0f), 0.06f, 12);
            PottedShrub(k0, m, l0.P(0.25f, 0f, -1.75f), 0.8f, 1, m.Pot);
            PottedShrub(k0, m, l0.P(0.25f, 0f, -1.45f), 0.65f, 2, m.PotBlue);

            var l1 = new Facade(-1f, -0.4f);
            PlantRack(c.At(-1f, -0.8f), m, l1, -1.02f, 0.2f, 5);
            HangingSign(c, c.At(-1f, 1.5f), l1, 1.95f, "WASHI", m.Board, new Color(0.12f, 0.08f, 0.06f));

            var l2 = new Facade(-1f, 4.2f);
            PlasticCrates(c.At(-1f, 2.1f), m, l2.P(0.36f, 0f, -2.06f), 0f, 3);
            ABoard(c.At(-1f, 6.2f), m, l2.P(0.55f, 0f, 2.02f));
            Downpipe(c.At(-1f, 6.4f), m, l2, 2.27f, 5.2f);

            var l3 = new Facade(-1f, 8.8f);
            FireBuckets(c.At(-1f, 6.9f), m, l3.P(0.32f, 0f, -1.85f));
            Bicycle(c.At(-1f, 9.9f), m, m.Bengara, new Vector3(-3.33f, 0f, 9.95f), 180f);

            var r0 = new Facade(1f, -5f);
            Bicycle(c.At(1f, -5.7f), m, m.Frosted, new Vector3(3.33f, 0f, -5.75f), 0f);
            WoodCrates(c.At(1f, -3.3f), m, r0.P(0.32f, 0f, 1.75f), 8f);

            var r1 = new Facade(1f, -0.4f);
            PottedShrub(c.At(1f, -2.4f), m, r1.P(0.32f, 0f, -2.02f), 1.3f, 7, m.PotBlue);
            PottedShrub(c.At(1f, 1.6f), m, r1.P(0.32f, 0f, 2.02f), 1.3f, 8, m.Pot);
            Downpipe(c.At(1f, 1.85f), m, r1, 2.27f, 5.45f);

            var r3 = new Facade(1f, 8.8f);
            RainChain(c.At(1f, 11f), m, r3.P(0.45f, 3.22f, 2.18f), 0.36f);

            // Oil-paper umbrellas left leaning by the teahouse door, and a road mirror watching the end of the street.
            var r2 = new Facade(1f, 4.2f);
            var kr2 = c.At(1f, 5.2f);
            Wagasa(kr2, m, r2.P(0.2f, 0.02f, 0.9f), r2.Dir(-0.16f, 0f) + Vector3.forward * 0.05f, m.Cloth[1]);
            Wagasa(kr2, m, r2.P(0.22f, 0.02f, 1.03f), r2.Dir(-0.14f, 0f) - Vector3.forward * 0.04f, m.Cloth[0]);
            RoadMirror(c, new Vector3(-3.36f, 0f, 12.35f), 160f);

            // A string of small paper lanterns across the street near the arrival, lit for a procession that never comes
            // (kept close, high in the view, well clear of the pagoda over the gate).
            var s0 = new Vector3(-3.93f, 4.45f, -1.7f);
            var s1 = new Vector3(3.93f, 4.45f, -0.9f);
            Wire(c, s0, s1, 0.35f, 0.012f);
            foreach (var e in new[] { s0, s1 }) Rod(c.At(e.x, e.z), m.Black, e, e + new Vector3(-Mathf.Sign(e.x) * 0.25f, 0f, 0f), 0.025f);
            for (int i = 1; i < 10; i++)
            {
                float t = i / 10f;
                var wp = Vector3.Lerp(s0, s1, t) + Vector3.down * (0.35f * 4f * t * (1f - t));
                Chochin(c, c.At(wp.x, wp.z), wp + Vector3.down * 0.17f, i % 3 == 1 ? m.LanternRed : m.LanternCream, true, 0.42f, wp.y);
            }

            // The lantern posts carry on above their colliders to a little cap; an iron hook shows where the lantern hangs.
            for (int i = 0; i < 3; i++)
            {
                float x = -2.2f + i * 2.2f;
                var k = c.At(x, 7.6f);
                k.Box(m.Stone, new Vector3(x, 0.07f, 7.6f), new Vector3(0.34f, 0.14f, 0.34f), 1.2f);
                k.Box(postWood, new Vector3(x, 2.5f, 7.6f), new Vector3(0.12f, 0.6f, 0.12f));
                foreach (float y in new[] { 0.5f, 1.9f }) k.Box(m.Black, new Vector3(x, y, 7.6f), new Vector3(0.13f, 0.035f, 0.13f));
                Coping(k, m.Tile, new Vector3(x, 2.8f, 7.42f), new Vector3(x, 2.8f, 7.78f), 0.3f, 30f);
                Rod(k, m.Black, new Vector3(x, 2.62f, 7.54f), new Vector3(x, 2.62f, 7.44f), 0.022f);
                Rod(k, m.Black, new Vector3(x, 2.63f, 7.45f), new Vector3(x, 2.53f, 7.45f), 0.012f);
            }
        }

        /// <summary>A drinks machine against a front, face to the street: three rows of bottles on a soft backlight behind
        /// glass, a lit button under each, a header strip, coin panel and dispenser flap. The glow stays behind the bottles.</summary>
        static void VendingMachine(StreetCtx c, Facade f, float u, Material body, int seed)
        {
            var m = c.M;
            var k = c.At(f.Side, f.Zc + u);
            var rnd = new System.Random(seed);
            const float w0 = 0.02f, w1 = 0.76f, hw = 0.49f, H = 1.83f, d0 = 0.98f, d1 = 1.7f, dw = 0.44f;
            // The cabinet, built round a 10 cm deep display recess (the window is open, not painted on).
            f.Box(k, body, w0, w1 - 0.1f, 0.05f, H, u - hw, u + hw);
            f.Box(k, body, w1 - 0.1f, w1, 0.05f, d0, u - hw, u + hw);
            f.Box(k, body, w1 - 0.1f, w1, d1, H, u - hw, u + hw);
            f.Box(k, body, w1 - 0.1f, w1, d0, d1, u - hw, u - dw);
            f.Box(k, body, w1 - 0.1f, w1, d0, d1, u + dw, u + hw);
            f.Box(k, m.Black, w1 - 0.04f, w1 + 0.004f, 0f, 0.05f, u - hw + 0.03f, u + hw - 0.03f);
            f.Box(k, body, w1 - 0.01f, w1 + 0.02f, H - 0.05f, H + 0.02f, u - hw, u + hw);
            c.Glow.Box(m.Display, f.P(w1 - 0.095f, (d0 + d1) * 0.5f, u), new Vector3(0.01f, d1 - d0, dw * 2f));
            f.Box(k, m.Black, w1 - 0.1f, w1 + 0.002f, d0 - 0.03f, d0, u - dw, u + dw);
            f.Box(k, m.Black, w1 - 0.1f, w1 + 0.002f, d1, d1 + 0.03f, u - dw, u + dw);
            for (int r = 0; r < 3; r++)
            {
                float sv = d0 + 0.02f + r * 0.235f;
                f.Box(k, m.Metal, w1 - 0.1f, w1 - 0.02f, sv - 0.012f, sv, u - 0.42f, u + 0.42f);
                for (int i = 0; i < 8; i++)
                {
                    float pu = u - 0.365f + i * 0.104f, ph = 0.12f + (float)rnd.NextDouble() * 0.05f;
                    var goods = c.Glow.Of(m.Goods[rnd.Next(m.Goods.Length)]);
                    goods.AddCylinder(f.P(w1 - 0.055f, sv, pu), 0.03f, ph, 8);
                    goods.AddCylinder(f.P(w1 - 0.055f, sv + ph, pu), 0.013f, 0.035f, 6);
                    c.Glow.Box(m.Buttons, f.P(w1 - 0.016f, sv - 0.035f, pu), new Vector3(0.006f, 0.018f, 0.04f));
                }
            }
            f.Box(k, m.Pane, w1 - 0.006f, w1 - 0.002f, d0, d1, u - 0.44f, u + 0.44f);
            c.Glow.Box(m.Display, f.P(w1 + 0.004f, 1.77f, u), new Vector3(0.006f, 0.04f, 0.9f));
            f.Box(k, m.Metal, w1 - 0.005f, w1 + 0.012f, 0.62f, 0.92f, u + 0.1f, u + 0.42f);
            f.Box(k, m.Black, w1 + 0.012f, w1 + 0.016f, 0.79f, 0.86f, u + 0.2f, u + 0.215f);
            c.Glow.Box(m.Goods[0], f.P(w1 + 0.014f, 0.88f, u + 0.32f), new Vector3(0.004f, 0.025f, 0.1f));
            f.Box(k, m.Black, w1 - 0.06f, w1 + 0.003f, 0.14f, 0.36f, u - 0.38f, u + 0.38f);
            f.Box(k, body, w1 - 0.01f, w1 + 0.01f, 0.3f, 0.36f, u - 0.38f, u + 0.38f);
        }

        /// <summary>A step-through town bicycle with a front basket and a rear rack, upright on its stand (length along local z).</summary>
        static void Bicycle(KitBatch k, JapanMats m, Material frame, Vector3 at, float yaw)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 P(float x, float y, float z) => at + q * new Vector3(x, y, z);
            var axle = q * Quaternion.Euler(0f, 0f, 90f);
            foreach (float z in new[] { -0.52f, 0.52f })
            {
                k.Of(m.Black).AddTorus(P(0f, 0.34f, z), 0.31f, 0.024f, 20, 5, axle);
                k.Of(m.Metal).AddTorus(P(0f, 0.34f, z), 0.282f, 0.008f, 20, 3, axle);
                Rod(k, m.Metal, P(-0.05f, 0.34f, z), P(0.05f, 0.34f, z), 0.04f);
                for (int s = 0; s < 3; s++)
                {
                    float a = s * 60f * Mathf.Deg2Rad;
                    var d = q * new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a)) * 0.28f;
                    Rod(k, m.Metal, P(0f, 0.34f, z) - d, P(0f, 0.34f, z) + d, 0.005f);
                }
                for (int s = 0; s < 5; s++)
                {
                    float a0 = Mathf.Lerp(25f, 155f, s / 5f) * Mathf.Deg2Rad, a1 = Mathf.Lerp(25f, 155f, (s + 1) / 5f) * Mathf.Deg2Rad;
                    Rod(k, frame, P(0f, 0.34f + Mathf.Sin(a0) * 0.355f, z + Mathf.Cos(a0) * 0.355f), P(0f, 0.34f + Mathf.Sin(a1) * 0.355f, z + Mathf.Cos(a1) * 0.355f), 0.065f, 0.012f);
                }
            }
            var bb = P(0f, 0.3f, -0.1f);
            var headLo = P(0f, 0.6f, 0.36f);
            var headHi = P(0f, 0.92f, 0.41f);
            Rod(k, frame, headLo, P(0f, 0.36f, 0.14f), 0.04f);
            Rod(k, frame, P(0f, 0.36f, 0.14f), bb, 0.04f);
            Rod(k, frame, bb, P(0f, 0.86f, -0.3f), 0.035f);
            Rod(k, frame, bb, P(0f, 0.34f, -0.52f), 0.025f);
            Rod(k, frame, P(0f, 0.8f, -0.28f), P(0f, 0.34f, -0.52f), 0.022f);
            Rod(k, frame, headLo, headHi, 0.045f);
            foreach (float x in new[] { -0.045f, 0.045f }) Rod(k, frame, headLo + q * new Vector3(x, 0f, 0f), P(x, 0.34f, 0.52f), 0.022f);
            Rod(k, m.Metal, headHi, P(0f, 1.02f, 0.39f), 0.025f);
            Rod(k, m.Metal, P(-0.27f, 1f, 0.3f), P(-0.1f, 1.03f, 0.38f), 0.022f);
            Rod(k, m.Metal, P(-0.1f, 1.03f, 0.38f), P(0.1f, 1.03f, 0.38f), 0.022f);
            Rod(k, m.Metal, P(0.1f, 1.03f, 0.38f), P(0.27f, 1f, 0.3f), 0.022f);
            foreach (float x in new[] { -1f, 1f }) Rod(k, m.Black, P(x * 0.2f, 1.008f, 0.315f), P(x * 0.29f, 0.998f, 0.288f), 0.032f);
            k.Box(m.Black, P(0f, 0.9f, -0.33f), new Vector3(0.17f, 0.07f, 0.27f), q);
            // Wire basket over the front wheel.
            var bc = P(0f, 0.88f, 0.64f);
            float bx = 0.18f, by = 0.12f, bz = 0.15f;
            foreach (float y in new[] { -by, 0f, by })
            {
                Rod(k, m.Metal, bc + q * new Vector3(-bx, y, -bz), bc + q * new Vector3(bx, y, -bz), 0.01f);
                Rod(k, m.Metal, bc + q * new Vector3(-bx, y, bz), bc + q * new Vector3(bx, y, bz), 0.01f);
                Rod(k, m.Metal, bc + q * new Vector3(-bx, y, -bz), bc + q * new Vector3(-bx, y, bz), 0.01f);
                Rod(k, m.Metal, bc + q * new Vector3(bx, y, -bz), bc + q * new Vector3(bx, y, bz), 0.01f);
            }
            foreach (float x in new[] { -bx, -bx * 0.33f, bx * 0.33f, bx })
                foreach (float z in new[] { -bz, bz }) Rod(k, m.Metal, bc + q * new Vector3(x, -by, z), bc + q * new Vector3(x, by, z), 0.008f);
            k.Box(m.Metal, bc + q * new Vector3(0f, -by, 0f), new Vector3(bx * 2f, 0.006f, bz * 2f), q);
            Rod(k, m.Metal, headHi, bc + q * new Vector3(0f, -by, -bz), 0.014f);
            // Rear rack, chain guard, stand and pedals.
            foreach (float x in new[] { -0.07f, 0.07f })
            {
                Rod(k, m.Metal, P(x, 0.74f, -0.3f), P(x, 0.74f, -0.8f), 0.015f);
                Rod(k, m.Metal, P(x, 0.74f, -0.72f), P(x * 0.7f, 0.34f, -0.52f), 0.012f);
            }
            for (int i = 0; i < 3; i++) Rod(k, m.Metal, P(-0.07f, 0.745f, -0.4f - i * 0.17f), P(0.07f, 0.745f, -0.4f - i * 0.17f), 0.012f);
            k.Box(m.Black, P(0.06f, 0.4f, -0.3f), new Vector3(0.015f, 0.14f, 0.5f), q);
            Rod(k, m.Metal, P(0.05f, 0.34f, -0.52f), P(0.17f, 0.02f, -0.62f), 0.02f);
            Rod(k, m.Metal, P(-0.12f, 0.3f, -0.1f), P(0.12f, 0.3f, -0.1f), 0.02f);
            foreach (float x in new[] { -0.15f, 0.15f }) k.Box(m.Black, P(x, x < 0f ? 0.44f : 0.16f, -0.1f), new Vector3(0.09f, 0.02f, 0.06f), q);
        }

        /// <summary>A glazed pot with a rounded shrub of a few overlapping leaf clumps.</summary>
        static void PottedShrub(KitBatch k, JapanMats m, Vector3 at, float size, int seed, Material pot)
        {
            var rnd = new System.Random(seed);
            var prof = new[] { new Vector2(0f, 0f), new Vector2(0.11f, 0f), new Vector2(0.14f, 0.05f), new Vector2(0.16f, 0.27f), new Vector2(0.18f, 0.3f), new Vector2(0.15f, 0.31f) };
            for (int i = 0; i < prof.Length; i++) prof[i] *= size;
            k.Of(pot).AddLathe(at, prof, 12);
            k.Of(m.Black).AddDisc(at + Vector3.up * 0.29f * size, Vector3.up, 0.15f * size, 12);
            for (int i = 0; i < 4; i++)
            {
                var o = new Vector3((float)(rnd.NextDouble() - 0.5) * 0.18f, 0.38f + (float)rnd.NextDouble() * 0.16f, (float)(rnd.NextDouble() - 0.5) * 0.18f) * size;
                k.Of(m.Leaf).AddSphere(at + o, (0.1f + (float)rnd.NextDouble() * 0.07f) * size, 9, 6);
            }
        }

        /// <summary>A stepped plant stand along the front with small pots of shrubs and little pines.</summary>
        static void PlantRack(KitBatch k, JapanMats m, Facade f, float u0, float u1, int seed)
        {
            var rnd = new System.Random(seed);
            for (int t = 0; t < 3; t++)
            {
                float v = 0.24f + t * 0.24f, wb = 0.52f - t * 0.13f;
                f.Box(k, m.Board, 0.12f, wb, v - 0.03f, v, u0, u1);
                foreach (float u in new[] { u0 + 0.03f, u1 - 0.03f }) f.Box(k, m.Timber, wb - 0.04f, wb, 0f, v - 0.03f, u - 0.025f, u + 0.025f);
                float uu = u0 + 0.14f + (float)rnd.NextDouble() * 0.06f;
                while (uu < u1 - 0.1f)
                {
                    var at = f.P((0.12f + wb) * 0.5f, v, uu);
                    if (rnd.NextDouble() < 0.3)
                    {
                        k.Of(m.PotBlue).AddCylinder(at, 0.1f, 0.05f, 12);
                        Pine(k, m, at + Vector3.up * 0.05f, 0.11f, rnd.Next(100));
                    }
                    else PottedShrub(k, m, at, 0.5f + (float)rnd.NextDouble() * 0.25f, rnd.Next(100), rnd.NextDouble() < 0.5 ? m.Pot : m.PotBlue);
                    uu += 0.26f + (float)rnd.NextDouble() * 0.08f;
                }
            }
        }

        /// <summary>A garden pine (scale 1 is about 4.8 m): a leaning, kinked trunk with flat cloud-pruned pads of needles.</summary>
        static void Pine(KitBatch k, JapanMats m, Vector3 at, float scale, int seed)
        {
            var rnd = new System.Random(seed);
            float h = 4.8f * scale;
            var path = new List<Vector3>();
            Vector3 Trunk(float t) => at + new Vector3((-0.9f * t * t - 0.12f * Mathf.Sin(t * 7f)) * scale, h * 0.8f * t, 0.25f * Mathf.Sin(t * 4f) * scale);
            for (int i = 0; i <= 6; i++) path.Add(Trunk(i / 6f));
            k.Of(m.Weathered).AddTube(path, 0.12f * scale, 8);
            foreach (var (t, dx, dz, r) in new[] { (0.38f, -0.85f, 0.1f, 1.05f), (0.55f, 0.65f, 0.3f, 0.9f), (0.72f, -0.5f, -0.5f, 0.85f), (0.88f, 0.25f, 0.15f, 1f), (1f, -0.75f, 0.45f, 0.7f) })
            {
                var from = Trunk(Mathf.Min(t, 1f) * 0.95f);
                var pad = from + new Vector3(dx, 0.15f + (float)rnd.NextDouble() * 0.2f, dz) * scale;
                Rod(k, m.Weathered, from, pad, 0.05f * scale + 0.004f);
                Blob(k.Of(m.Leaf), pad, r * scale, r * 0.32f * scale, 10);
            }
        }

        /// <summary>A pyramid of six red fire buckets on a low stand, dark water in the top one.</summary>
        static void FireBuckets(KitBatch k, JapanMats m, Vector3 at)
        {
            k.Box(m.Board, at + Vector3.up * 0.12f, new Vector3(0.34f, 0.24f, 0.9f));
            int[] rows = { 3, 2, 1 };
            for (int r = 0; r < 3; r++)
                for (int i = 0; i < rows[r]; i++)
                {
                    var p = at + new Vector3(0f, 0.24f + r * 0.235f, (i - (rows[r] - 1) * 0.5f) * 0.28f);
                    k.Of(m.RedPaint).AddFrustum(p, 0.1f, 0.135f, 0.24f, 12, true, false);
                    k.Of(m.Black).AddFrustum(p + Vector3.up * 0.17f, 0.127f, 0.132f, 0.035f, 12, false, false);
                    k.Of(m.Black).AddDisc(p + Vector3.up * 0.2f, Vector3.up, 0.127f, 12);
                }
        }

        /// <summary>A stack of blue plastic crates, bottle tops showing in the top one.</summary>
        static void PlasticCrates(KitBatch k, JapanMats m, Vector3 at, float yaw, int count)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            for (int i = 0; i < count; i++)
            {
                var p = at + q * new Vector3((i % 2) * 0.03f, i * 0.29f, 0f);
                k.Box(m.Plastic, p + q * new Vector3(0f, 0.015f, 0f), new Vector3(0.34f, 0.03f, 0.48f), q);
                foreach (float x in new[] { -0.16f, 0.16f }) k.Box(m.Plastic, p + q * new Vector3(x, 0.145f, 0f), new Vector3(0.02f, 0.29f, 0.48f), q);
                foreach (float z in new[] { -0.23f, 0.23f }) k.Box(m.Plastic, p + q * new Vector3(0f, 0.145f, z), new Vector3(0.34f, 0.29f, 0.02f), q);
                if (i == count - 1)
                    for (int b = 0; b < 12; b++) k.Of(m.Glass).AddCylinder(p + q * new Vector3(-0.1f + (b / 4) * 0.1f, 0.03f, -0.18f + (b % 4) * 0.12f), 0.032f, 0.27f, 6);
            }
        }

        /// <summary>Two slatted wooden crates, the top one turned a little.</summary>
        static void WoodCrates(KitBatch k, JapanMats m, Vector3 at, float yaw)
        {
            for (int i = 0; i < 2; i++)
            {
                var qi = Quaternion.Euler(0f, yaw + i * 9f, 0f);
                var p = at + new Vector3(i * 0.04f, i * 0.36f, i * 0.05f);
                for (int s = 0; s < 3; s++) k.Box(m.Board, p + qi * new Vector3(0f, 0.06f + s * 0.12f, 0f), new Vector3(0.42f, 0.1f, 0.62f), qi);
                foreach (float x in new[] { -0.2f, 0.2f })
                    foreach (float z in new[] { -0.3f, 0.3f }) k.Box(m.Timber, p + qi * new Vector3(x, 0.18f, z), new Vector3(0.04f, 0.36f, 0.04f), qi);
            }
        }

        /// <summary>A folding A-board menu stand, its faces toward the people walking along the street.</summary>
        static void ABoard(KitBatch k, JapanMats m, Vector3 at)
        {
            foreach (float s in new[] { -1f, 1f })
            {
                var rot = Quaternion.Euler(-s * 12f, 0f, 0f);
                k.Box(m.Timber, at + new Vector3(0f, 0.45f, s * 0.1f), new Vector3(0.46f, 0.9f, 0.025f), rot);
                k.Box(m.PaperDim, at + new Vector3(0f, 0.5f, s * 0.116f), new Vector3(0.38f, 0.6f, 0.005f), rot);
            }
        }

        /// <summary>A run of eave gutter, the leader to the wall and a downpipe to a shoe at the foot of the front.</summary>
        static void Downpipe(KitBatch k, JapanMats m, Facade f, float u, float eave)
        {
            f.Box(k, m.Metal, 0.58f, 0.7f, eave - 0.06f, eave + 0.02f, u - 1.4f, u + 0.05f);
            Rod(k, m.Metal, f.P(0.64f, eave - 0.06f, u), f.P(0.06f, eave - 0.45f, u), 0.06f);
            k.Of(m.Metal).AddCylinder(f.P(0.06f, 0.12f, u), 0.04f, eave - 0.57f, 8);
            Rod(k, m.Metal, f.P(0.06f, 0.16f, u), f.P(0.22f, 0.06f, u), 0.07f);
        }

        /// <summary>A closed oil-paper umbrella leaning on the front: bamboo handle, folded canopy, a tie, a pointed crown.</summary>
        static void Wagasa(KitBatch k, JapanMats m, Vector3 foot, Vector3 lean, Material paper)
        {
            var dir = (Vector3.up + lean).normalized;
            var rot = Quaternion.FromToRotation(Vector3.up, dir);
            k.Of(m.Bamboo).AddCylinder(foot, 0.012f, 0.42f, 6, 1f, rot);
            k.Of(paper).AddFrustum(foot + dir * 0.42f, 0.03f, 0.055f, 0.5f, 10, true, false, 1f, rot);
            k.Of(paper).AddFrustum(foot + dir * 0.92f, 0.055f, 0.012f, 0.14f, 10, false, true, 1f, rot);
            k.Of(m.Black).AddCylinder(foot + dir * 0.62f, 0.05f, 0.02f, 10, 1f, rot);
        }

        /// <summary>A convex road mirror on an orange pole, tipped to watch the street (its glass reflects like wet stone).</summary>
        static void RoadMirror(StreetCtx c, Vector3 at, float yaw)
        {
            var m = c.M;
            var k = c.At(at.x, at.z);
            var orange = Mats.Lit(new Color(0.88f, 0.4f, 0.12f), 0.5f);
            k.Of(orange).AddCylinder(at, 0.045f, 2.62f, 10);
            var n = Quaternion.Euler(8f, yaw, 0f) * Vector3.forward;
            var head = at + Vector3.up * 2.78f + n * 0.06f;
            k.Of(orange).AddTorus(head, 0.33f, 0.035f, 24, 6, Quaternion.FromToRotation(Vector3.up, n));
            k.Of(m.Glass).AddDisc(head + n * 0.012f, n, 0.32f, 24);
            k.Of(m.Black).AddDisc(head - n * 0.02f, -n, 0.34f, 24);
            Rod(k, orange, at + Vector3.up * 2.6f, head - n * 0.03f, 0.04f);
        }

        /// <summary>A rain chain of little cups from the eave into a round stone basin.</summary>
        static void RainChain(KitBatch k, JapanMats m, Vector3 top, float bottomY)
        {
            int n = Mathf.FloorToInt((top.y - bottomY) / 0.2f);
            for (int i = 0; i < n; i++)
            {
                var p = top + Vector3.down * (0.2f * i + 0.16f);
                k.Of(m.Metal).AddFrustum(p, 0.03f, 0.05f, 0.08f, 8, true, false);
                Rod(k, m.Metal, p + Vector3.up * 0.08f, p + Vector3.up * 0.2f, 0.008f);
            }
            var basin = new[] { new Vector2(0f, 0f), new Vector2(0.2f, 0f), new Vector2(0.24f, 0.05f), new Vector2(0.24f, 0.28f), new Vector2(0.2f, 0.3f), new Vector2(0.17f, 0.24f) };
            var b = new Vector3(top.x, 0.03f, top.z);
            k.Of(m.Stone).AddLathe(b, basin, 14);
            k.Of(m.Glass).AddDisc(b + Vector3.up * 0.25f, Vector3.up, 0.18f, 14);
        }

        /// <summary>A hanging signboard square to the front (both faces lettered top to bottom) on its own arm under the pent roof.</summary>
        static void HangingSign(StreetCtx c, KitBatch k, Facade f, float u, string word, Material board, Color ink)
        {
            var m = c.M;
            const float w0 = 0.26f, w1 = 0.62f, v0 = 1.62f, v1 = 2.72f;
            f.Box(k, board, w0, w1, v0, v1, u - 0.022f, u + 0.022f);
            f.Box(k, m.Timber, w0 - 0.02f, w1 + 0.02f, v1, v1 + 0.05f, u - 0.03f, u + 0.03f);
            f.Box(k, m.Timber, 0.13f, w1 + 0.06f, 2.86f, 2.93f, u - 0.03f, u + 0.03f);
            foreach (float w in new[] { w0 + 0.05f, w1 - 0.05f }) Rod(k, m.Black, f.P(w, v1 + 0.05f, u), f.P(w, 2.86f, u), 0.01f);
            string vertical = string.Join("\n", word.ToCharArray());
            foreach (float s in new[] { -1f, 1f })
            {
                var t = TextFactory.World(c.Root, vertical, 0.11f, ink, TMPro.TextAlignmentOptions.Center, true, 0.32f);
                t.transform.localPosition = f.P((w0 + w1) * 0.5f, (v0 + v1) * 0.5f, u + s * 0.024f);
                t.transform.localRotation = Quaternion.Euler(0f, s < 0f ? 0f : 180f, 0f); // TMP faces -Z
            }
        }

        /// <summary>A painted word on a shop fascia, facing the street.</summary>
        static void FasciaSign(StreetCtx c, Facade f, string word, float v, float w, Color ink)
        {
            var t = TextFactory.World(c.Root, word, 0.17f, ink, TMPro.TextAlignmentOptions.Center, true, 3f);
            t.transform.localPosition = f.P(w + 0.004f, v, 0f);
            t.transform.localRotation = Quaternion.Euler(0f, 90f * f.Side, 0f);
            t.characterSpacing = 40f;
        }

        /// <summary>A chochin: a ribbed paper barrel with black lacquered caps, hung by a cord from cordTop.</summary>
        static void Chochin(StreetCtx c, KitBatch k, Vector3 centre, Material paper, bool glowing, float scale, float cordTop)
        {
            float H = 0.5f * scale, R = 0.22f * scale;
            var prof = new List<Vector2>();
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f;
                prof.Add(new Vector2(R * Mathf.Sqrt(1f - 0.55f * (2f * t - 1f) * (2f * t - 1f)), (t - 0.5f) * H));
            }
            (glowing ? c.Glow : k).Of(paper).AddLathe(centre, prof, 14);
            var black = k.Of(c.M.Black);
            for (int i = 1; i < 8; i += 2) black.AddTorus(centre + Vector3.up * prof[i].y, prof[i].x + 0.002f, 0.0035f * Mathf.Max(0.7f, scale), 14, 3, Quaternion.identity);
            black.AddCylinder(centre + Vector3.up * (-H * 0.5f - 0.035f * scale), R * 0.72f, 0.05f * scale, 12);
            black.AddCylinder(centre + Vector3.up * (H * 0.5f - 0.015f * scale), R * 0.72f, 0.05f * scale, 12);
            Rod(k, c.M.Black, centre + Vector3.up * (H * 0.5f + 0.03f * scale), new Vector3(centre.x, cordTop, centre.z), 0.008f);
        }

        // ------------------------------------------------------------------ shadows on paper
        // Outlines (metres; u to the right, v up, feet at the origin): a woman with her hair up, someone kneeling, a head and shoulders.
        static readonly Vector2[] s_Standing =
        {
            new Vector2(-0.17f, 0.02f), new Vector2(0.17f, 0.02f), new Vector2(0.15f, 0.5f), new Vector2(0.2f, 0.95f), new Vector2(0.27f, 1f),
            new Vector2(0.26f, 1.18f), new Vector2(0.2f, 1.32f), new Vector2(0.05f, 1.37f), new Vector2(0.045f, 1.41f), new Vector2(0.08f, 1.45f),
            new Vector2(0.085f, 1.52f), new Vector2(0.07f, 1.58f), new Vector2(0.06f, 1.62f), new Vector2(0.04f, 1.68f), new Vector2(0f, 1.7f),
            new Vector2(-0.04f, 1.68f), new Vector2(-0.06f, 1.62f), new Vector2(-0.07f, 1.58f), new Vector2(-0.085f, 1.52f), new Vector2(-0.08f, 1.45f),
            new Vector2(-0.045f, 1.41f), new Vector2(-0.05f, 1.37f), new Vector2(-0.2f, 1.32f), new Vector2(-0.26f, 1.18f), new Vector2(-0.27f, 1f),
            new Vector2(-0.2f, 0.95f), new Vector2(-0.15f, 0.5f),
        };
        static readonly Vector2[] s_Seated =
        {
            new Vector2(-0.32f, 0f), new Vector2(0.32f, 0f), new Vector2(0.3f, 0.15f), new Vector2(0.24f, 0.3f), new Vector2(0.22f, 0.55f),
            new Vector2(0.2f, 0.68f), new Vector2(0.06f, 0.73f), new Vector2(0.05f, 0.77f), new Vector2(0.085f, 0.82f), new Vector2(0.09f, 0.9f),
            new Vector2(0.07f, 0.97f), new Vector2(0f, 1f), new Vector2(-0.07f, 0.97f), new Vector2(-0.09f, 0.9f), new Vector2(-0.085f, 0.82f),
            new Vector2(-0.05f, 0.77f), new Vector2(-0.06f, 0.73f), new Vector2(-0.2f, 0.68f), new Vector2(-0.22f, 0.55f), new Vector2(-0.24f, 0.3f),
            new Vector2(-0.3f, 0.15f),
        };
        static readonly Vector2[] s_Bust =
        {
            new Vector2(-0.24f, 0f), new Vector2(0.24f, 0f), new Vector2(0.22f, 0.12f), new Vector2(0.06f, 0.18f), new Vector2(0.05f, 0.22f),
            new Vector2(0.085f, 0.28f), new Vector2(0.09f, 0.36f), new Vector2(0.07f, 0.43f), new Vector2(0f, 0.46f), new Vector2(-0.07f, 0.43f),
            new Vector2(-0.09f, 0.36f), new Vector2(-0.085f, 0.28f), new Vector2(-0.05f, 0.22f), new Vector2(-0.06f, 0.18f), new Vector2(-0.22f, 0.12f),
        };

        /// <summary>A shadow on lit paper: an outline laid on the front's plane at w (just over the paper, under the lattice),
        /// facing the street.</summary>
        static void ShadowShape(MeshBuilder b, Facade f, float w, Vector2 at, IList<Vector2> outline, float scale, bool mirror = false)
        {
            var tris = MeshBuilder.Triangulate(outline);
            bool flip = (f.Side < 0f) ^ mirror; // Triangulate is counter-clockwise in (u, v), which faces -X on the right-hand side
            Vector3 V(int i) { var p = outline[i]; return f.P(w, at.y + p.y * scale, at.x + (mirror ? -p.x : p.x) * scale); }
            for (int t = 0; t + 2 < tris.Count; t += 3)
            {
                var a = V(tris[t]); var b1 = V(tris[t + 1]); var c = V(tris[t + 2]);
                if (flip) b.AddTriangle(a, c, b1, Vector2.zero, Vector2.zero, Vector2.zero);
                else b.AddTriangle(a, b1, c, Vector2.zero, Vector2.zero, Vector2.zero);
            }
        }

        /// <summary>A shadow figure as its own object, for one that moves (PaperShadow): it walks along the paper between minU
        /// and maxU, or (blink) is at the window or gone each time you look back.</summary>
        static void PaperShadowFigure(StreetCtx c, Facade f, float w, float u, float v, Vector2[] outline, float scale, float minU, float maxU, bool blink)
        {
            var b = new MeshBuilder();
            ShadowShape(b, new Facade(f.Side, 0f), w, new Vector2(0f, v), outline, scale);
            var go = MeshFactory.FromBuilder(blink ? "WatcherShadow" : "WalkingShadow", b, c.M.Shadow, c.Root);
            go.transform.localPosition = new Vector3(0f, 0f, f.Zc + u);
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var s = go.AddComponent<PaperShadow>();
            s.MinZ = f.Zc + minU;
            s.MaxZ = f.Zc + maxU;
            s.Blink = blink;
        }
    }

    /// <summary>
    /// A person's shadow on lit paper in Japan's empty street. It only moves while nobody is looking: it walks slowly along the
    /// paper, or (Blink) is simply there or gone when you look back.
    /// </summary>
    public class PaperShadow : MonoBehaviour
    {
        public float MinZ, MaxZ, Speed = 0.2f;
        public bool Blink;
        Vector3 m_LocalCentre;
        float m_Target, m_Unseen;

        void Start()
        {
            var mf = GetComponent<MeshFilter>();
            m_LocalCentre = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.center : Vector3.zero;
            m_Target = transform.localPosition.z;
        }

        void Update()
        {
            var rig = Game.Rig;
            if (rig == null || rig.Head == null) return;
            var to = transform.position + transform.rotation * m_LocalCentre - rig.Head.position;
            bool seen = to.sqrMagnitude < 900f && Vector3.Dot(rig.Head.forward, to.normalized) > 0.45f;
            if (seen) { m_Unseen = 0f; return; }
            float before = m_Unseen;
            m_Unseen += Time.deltaTime;
            if (Blink)
            {
                // Decided once each time you look away: at the window when you look back, or not.
                if (before < 1.5f && m_Unseen >= 1.5f) transform.localScale = Random.value < 0.5f ? Vector3.one : Vector3.zero;
                return;
            }
            var p = transform.localPosition;
            if (Mathf.Abs(p.z - m_Target) < 0.02f) m_Target = Random.Range(MinZ, MaxZ);
            p.z = Mathf.MoveTowards(p.z, m_Target, Speed * Time.deltaTime);
            transform.localPosition = p;
        }
    }

    /// <summary>A failing street lamp: steady for a while, then a short stutter of dips (its light and lens together).</summary>
    public class FailingLamp : MonoBehaviour
    {
        public Light Light;
        public Material Lens;
        public Color Emission;
        float m_Base, m_Next, m_Until;

        void Start()
        {
            m_Base = Light != null ? Light.intensity : 1f;
            m_Next = Time.time + Random.Range(4f, 10f);
        }

        void Update()
        {
            float t = Time.time, k = 1f;
            if (t > m_Next) { m_Until = t + Random.Range(0.3f, 1.1f); m_Next = m_Until + Random.Range(5f, 14f); }
            if (t < m_Until) k = Mathf.PerlinNoise(t * 21f, 0.37f) > 0.42f ? 1f : 0.12f;
            if (Light != null) Light.intensity = m_Base * k;
            if (Lens != null) Lens.SetColor("_EmissionColor", Emission * k);
        }
    }
}

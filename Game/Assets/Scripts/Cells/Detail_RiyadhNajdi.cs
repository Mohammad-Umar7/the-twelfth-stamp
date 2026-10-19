using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    // Riyadh's Najdi architecture (see Detail_Riyadh.cs): mud-plaster blocks, painted doors, the watchtower, the walls and the majlis.
    public partial class RiyadhCell
    {
        /// <summary>A wall face: a = along the face, v = height, w = out from the face. Boxes turn with it.</summary>
        readonly struct Front
        {
            public readonly Vector3 O, Out, Right;
            public readonly Quaternion Rot;
            public Front(Vector3 origin, Vector3 outward)
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

        /// <summary>
        /// A Najdi mud-plaster house front (a0..a1 along the face, h tall, depth back from it): a darker plinth, a slightly
        /// battered lower wall, gypsum courses, a row of triangular vents under the parapet (a few lit from inside), stepped
        /// crenellations, palm-trunk spouts with their water stains, and the ends of the ceiling beams along the floor line.
        /// </summary>
        static void NajdiBlock(KitBatch k, NajdiMats m, Front f, float a0, float a1, float h, float depth, int seed, bool beams)
        {
            var rnd = new System.Random(seed);
            f.Box(k, m.MudDark, a0, a1, -0.35f, 0.55f, -depth, 0.08f, 2f);
            f.Box(k, m.Mud, a0, a1, 0.55f, h * 0.5f, -depth, 0.035f, 2.2f);
            f.Box(k, m.Mud, a0, a1, h * 0.5f, h, -depth, 0f, 2.2f);
            f.Box(k, m.Gypsum, a0, a1, h - 0.72f, h - 0.6f, 0f, 0.05f, 2f);
            if (h > 4.6f) f.Box(k, m.Gypsum, a0, a1, 3f, 3.1f, 0.03f, 0.07f, 2f);
            for (float a = a0 + 0.35f; a < a1 - 0.2f; a += 0.45f)
                TriFace(k.Of(rnd.NextDouble() < 0.18 ? m.GlowTri : m.Dark), f.P(a, h - 1.02f, 0.006f), f.Out, 0.2f, 0.24f);
            for (float a = a0 + 0.31f; a < a1 - 0.2f; a += 0.62f) Merlon(k, m.Mud, f.P(a, h, -0.17f), f.Rot, 0.42f, 0.34f, 0.17f);
            for (float a = a0 + 1.1f; a < a1 - 0.6f; a += 2.6f + (float)rnd.NextDouble() * 0.6f)
            {
                var root = f.P(a, h - 0.85f, 0f);
                Rod(k, m.PalmWood, root - f.Out * 0.1f, root + f.Out * 0.62f + Vector3.down * 0.09f, 0.13f);
                f.Box(k, m.MudDark, a - 0.07f, a + 0.07f, h - 2.3f, h - 0.92f, 0f, 0.004f, 2f);
            }
            if (beams && h > 4.6f)
                for (float a = a0 + 0.3f; a < a1 - 0.2f; a += 0.55f)
                    k.Of(m.PalmWood).AddCylinder(f.P(a, 2.88f, 0.03f), 0.055f, 0.17f, 7, 1f, Quaternion.FromToRotation(Vector3.up, f.Out));
        }

        /// <summary>A small window: gypsum surround, a painted wooden shutter (or a dark opening), three triangle vents above.</summary>
        static void Window(KitBatch k, NajdiMats m, Front f, float a, float v, bool shutter, int paint)
        {
            f.Box(k, m.Gypsum, a - 0.38f, a + 0.38f, v - 0.08f, v + 0.86f, 0f, 0.05f, 2f);
            if (shutter)
            {
                f.Box(k, m.Door, a - 0.3f, a + 0.3f, v, v + 0.78f, 0.05f, 0.075f);
                f.Box(k, m.Dark, a - 0.006f, a + 0.006f, v, v + 0.78f, 0.075f, 0.078f);
                for (int i = 0; i < 4; i++)
                    TriFace(k.Of(m.Paint[(paint + i) % m.Paint.Length]), f.P(a + (i % 2 == 0 ? -0.15f : 0.15f), v + (i < 2 ? 0.48f : 0.14f), 0.078f), f.Out, 0.16f, 0.16f, i < 2);
                f.Box(k, m.PaintWhite, a - 0.28f, a + 0.28f, v + 0.38f, v + 0.4f, 0.075f, 0.078f);
            }
            else f.Box(k, m.Dark, a - 0.3f, a + 0.3f, v, v + 0.78f, 0.05f, 0.052f);
            for (int i = 0; i < 3; i++) TriFace(k.Of(m.Dark), f.P(a + (i - 1) * 0.24f, v + 1f, 0.006f), f.Out, 0.18f, 0.2f);
        }

        /// <summary>
        /// A Najdi door: a gypsum surround (deep enough to sit on a round wall too), two plank leaves painted with bands of
        /// coloured triangles between white lines, iron studs, brass ring knockers, and a frieze of triangles above.
        /// </summary>
        static void Door(KitBatch k, NajdiMats m, Front f, float a, float width, float height, int seed, bool frieze = true)
        {
            float hw = width * 0.5f;
            f.Box(k, m.Gypsum, a - hw - 0.18f, a + hw + 0.18f, 0f, height + 0.24f, -0.25f, 0.08f, 2f);
            f.Box(k, m.Dark, a - hw - 0.02f, a + hw + 0.02f, 0f, height + 0.02f, 0.08f, 0.082f);
            var ring = Quaternion.FromToRotation(Vector3.up, f.Out);
            for (int s = -1; s <= 1; s += 2)
            {
                float l0 = s < 0 ? a - hw : a + 0.008f, l1 = s < 0 ? a - 0.008f : a + hw;
                f.Box(k, m.Door, l0, l1, 0.02f, height, 0.082f, 0.12f, 1.2f);
                int colour = seed + (s > 0 ? 2 : 0);
                foreach (float vb in new[] { height * 0.22f, height * 0.5f, height * 0.78f })
                {
                    f.Box(k, m.PaintWhite, l0 + 0.04f, l1 - 0.04f, vb - 0.012f, vb + 0.004f, 0.12f, 0.123f);
                    f.Box(k, m.PaintWhite, l0 + 0.04f, l1 - 0.04f, vb + 0.17f, vb + 0.186f, 0.12f, 0.123f);
                    int n = Mathf.Max(2, Mathf.FloorToInt((l1 - l0 - 0.1f) / 0.13f));
                    float step = (l1 - l0 - 0.1f) / n;
                    for (int i = 0; i < n; i++)
                    {
                        float x = l0 + 0.05f + (i + 0.5f) * step;
                        TriFace(k.Of(m.Paint[(colour + i) % m.Paint.Length]), f.P(x, vb + 0.012f, 0.123f), f.Out, step * 0.9f, 0.14f);
                        f.Box(k, m.Iron, x - 0.012f, x + 0.012f, vb - 0.035f, vb - 0.025f + 0.014f, 0.12f, 0.135f);
                    }
                    colour++;
                }
                var knocker = f.P(s < 0 ? a - 0.14f : a + 0.14f, height * 0.42f, 0.135f);
                k.Of(m.Brass).AddTorus(knocker + Vector3.down * 0.05f, 0.055f, 0.008f, 12, 4, ring);
                k.Of(m.Brass).AddCylinder(f.P(s < 0 ? a - 0.14f : a + 0.14f, height * 0.42f, 0.12f), 0.02f, 0.02f, 8, 1f, ring);
            }
            if (!frieze) return;
            for (int i = 0; i < 5; i++) TriFace(k.Of(m.Dark), f.P(a + (i - 2) * 0.3f, height + 0.4f, 0.006f), f.Out, 0.2f, 0.22f);
        }

        /// <summary>
        /// The west side, lit by the low sun: two Najdi houses (a painted double door stands behind the plaza seam), the majlis
        /// house set back behind its portico, and mud benches along the walls.
        /// </summary>
        void WestHouses(Transform g, KitBatch k, NajdiMats m)
        {
            var a = new Front(new Vector3(-9.2f, 0f, -3.95f), Vector3.right);
            NajdiBlock(k, m, a, -2.65f, 2.65f, 6.2f, 3f, 31, true);
            Door(k, m, a, 0.9f, 1.2f, 2.2f, 1);
            Window(k, m, a, -1.3f, 3.6f, true, 0);
            Window(k, m, a, 1.4f, 3.6f, false, 0);
            Window(k, m, a, -1.5f, 1.1f, true, 2);
            var b = new Front(new Vector3(-9.2f, 0f, 1.85f), Vector3.right);
            NajdiBlock(k, m, b, -3.15f, 3.15f, 5.5f, 3f, 37, true);
            Door(k, m, b, -0.15f, 1.8f, 2.8f, 3);
            Window(k, m, b, -2.3f, 3.5f, true, 1);
            Window(k, m, b, 2.05f, 3.5f, true, 3);
            b.Box(k, m.MudDark, 1.25f, 2.95f, -0.12f, 0.42f, 0f, 0.5f, 2f);
            b.Box(k, m.Gypsum, 1.23f, 2.97f, 0.42f, 0.45f, 0f, 0.52f, 2f);
            var c = new Front(new Vector3(-11.6f, 0f, 8.2f), Vector3.right);
            NajdiBlock(k, m, c, -3.2f, 3.2f, 5.2f, 0.6f, 41, false);
            Door(k, m, c, 2.62f, 1.0f, 2.1f, 2);
            Window(k, m, c, -1.6f, 3.45f, false, 0);
            Window(k, m, c, 0.6f, 3.45f, true, 2);
        }

        /// <summary>
        /// The majlis: a raised, gypsum-edged platform under a palm-rib roof on palm-trunk posts, a carpet, floor and back
        /// cushions, bolsters, a brass tray on a low stand with the dallah, poured cups and a bowl of dates. Set for guests, and
        /// nobody here: a cup knocked over, sandals left at the carpet's edge. Its hanging lantern is the corner's light.
        /// </summary>
        void Majlis(Transform g, KitBatch k, NajdiMats m)
        {
            k.Box(m.MudDark, new Vector3(-9.95f, 0.09f, 8.25f), new Vector3(3.3f, 0.42f, 6.1f), 2f);
            k.Box(m.Gypsum, new Vector3(-8.32f, 0.27f, 8.25f), new Vector3(0.06f, 0.06f, 6.12f));
            foreach (float z in new[] { 5.45f, 8.25f, 11.05f })
            {
                k.Of(m.PalmWood).AddCylinder(new Vector3(-8.5f, 0.3f, z), 0.11f, 2.62f, 9, 1.5f);
                for (int r = 0; r < 6; r++) k.Of(m.PalmWood).AddCylinder(new Vector3(-8.5f, 0.55f + r * 0.4f, z), 0.125f, 0.05f, 9);
                k.Box(m.PalmWood, new Vector3(-10.05f, 2.98f, z), new Vector3(3.1f, 0.14f, 0.16f));
            }
            k.Box(m.PalmWood, new Vector3(-8.5f, 3f, 8.25f), new Vector3(0.2f, 0.18f, 6.2f));
            for (float z = 5.35f; z < 11.2f; z += 0.32f) Rod(k, m.PalmWood, new Vector3(-8.4f, 3.12f, z), new Vector3(-11.6f, 3.12f, z), 0.07f);
            k.Box(m.Arish, new Vector3(-9.95f, 3.19f, 8.25f), new Vector3(3.4f, 0.06f, 6.2f), 1.5f);
            int fringe = 0;
            for (float z = 5.2f; z < 11.3f; z += 0.09f, fringe++)
                k.Box(m.Arish, new Vector3(-8.27f, 3.02f - (fringe % 4) * 0.02f, z), new Vector3(0.02f, 0.32f + (fringe * 7 % 5) * 0.03f, 0.07f));
            k.Box(m.Mud, new Vector3(-9.95f, 0.8f, 11.25f), new Vector3(3.3f, 1f, 0.12f), 2f);
            k.Box(m.Gypsum, new Vector3(-9.95f, 1.315f, 11.25f), new Vector3(3.32f, 0.03f, 0.16f), 2f);
            // Carpet, cushions along the wall and the north parapet, bolsters.
            k.Box(m.Carpet, new Vector3(-9.95f, 0.305f, 8.35f), new Vector3(2.4f, 0.01f, 4.9f), 1.2f);
            k.Box(m.CushionRed, new Vector3(-11.27f, 0.39f, 8.35f), new Vector3(0.6f, 0.16f, 4.9f));
            k.Box(m.CushionRed, new Vector3(-9.95f, 0.39f, 10.92f), new Vector3(2f, 0.16f, 0.5f));
            k.Box(m.CushionOchre, new Vector3(-10.98f, 0.47f, 8.35f), new Vector3(0.04f, 0.02f, 4.9f));
            for (int i = 0; i < 6; i++)
                k.Box(i % 2 == 0 ? m.CushionRed : m.CushionOchre, new Vector3(-11.47f, 0.7f, 6.35f + i * 0.8f), new Vector3(0.18f, 0.46f, 0.74f), Quaternion.Euler(0f, 0f, 12f));
            for (int i = 0; i < 2; i++)
                k.Box(i == 0 ? m.CushionOchre : m.CushionRed, new Vector3(-10.5f + i * 0.9f, 0.7f, 11.1f), new Vector3(0.82f, 0.46f, 0.18f), Quaternion.Euler(12f, 0f, 0f));
            foreach (float z in new[] { 7.55f, 9.15f })
                k.Of(m.CushionOchre).AddCylinder(new Vector3(-11.55f, 0.56f, z), 0.08f, 0.5f, 10, 1f, Quaternion.FromToRotation(Vector3.up, Vector3.right));
            // The tray and the coffee: the dallah, six poured cups, a bowl of dates; two more cups by the cushions, one spilt.
            var tray = new Vector3(-9.85f, 0.31f, 8.3f);
            k.Of(m.PalmWood).AddCylinder(tray, 0.24f, 0.2f, 12);
            k.Of(m.Brass).AddCylinder(tray + Vector3.up * 0.2f, 0.44f, 0.025f, 24);
            k.Of(m.Brass).AddTorus(tray + Vector3.up * 0.225f, 0.43f, 0.012f, 24, 4, Quaternion.identity);
            Dallah(k, m, tray + new Vector3(0.1f, 0.225f, -0.08f));
            for (int i = 0; i < 6; i++)
            {
                float ang = (i * 50f + 70f) * Mathf.Deg2Rad;
                Cup(k, m, tray + new Vector3(Mathf.Cos(ang) * 0.29f, 0.225f, Mathf.Sin(ang) * 0.29f), false);
            }
            var bowl = tray + new Vector3(-0.16f, 0.225f, -0.2f);
            k.Of(m.Brass).AddLathe(bowl, new[] { new Vector2(0f, 0f), new Vector2(0.05f, 0f), new Vector2(0.1f, 0.04f), new Vector2(0.11f, 0.06f) }, 14);
            for (int i = 0; i < 9; i++)
            {
                float ang = i * 40f * Mathf.Deg2Rad, r = i == 8 ? 0f : 0.05f;
                Blob(k.Of(m.Door), bowl + new Vector3(Mathf.Cos(ang) * r, 0.05f + (i == 8 ? 0.02f : 0f), Mathf.Sin(ang) * r), 0.02f, 0.014f, 6);
            }
            Cup(k, m, new Vector3(-10.92f, 0.31f, 7.05f), false);
            Cup(k, m, new Vector3(-10.4f, 0.31f, 9.8f), true);
            k.Of(m.Dark).AddDisc(new Vector3(-10.33f, 0.312f, 9.84f), Vector3.up, 0.07f, 12);
            foreach (float dz in new[] { -0.07f, 0.07f })
            {
                var s = new Vector3(-8.64f, 0.31f, 7.2f + dz);
                var turn = Quaternion.Euler(0f, dz * 70f, 0f);
                k.Box(m.PalmWood, s + new Vector3(0f, 0.012f, 0f), new Vector3(0.26f, 0.024f, 0.1f), turn);
                k.Box(m.Dark, s + turn * new Vector3(0.04f, 0.04f, 0f), new Vector3(0.05f, 0.025f, 0.095f), turn);
            }
            Lantern(k, m, new Vector3(-9.6f, 2.32f, 8.25f), 1f, 2.91f);
            AddPointLight(g, new Vector3(-9.6f, 2.3f, 8.25f), new Color(1f, 0.72f, 0.45f), 1.4f, 5f);
        }

        /// <summary>
        /// The north edge: a low crenellated wall with a row of triangle vents (low, so the skyline shows over the arches),
        /// broken straight behind the honest arch by the gatehouse onto the city (NorthGate), and the round watchtower at the
        /// west corner.
        /// </summary>
        void NorthWall(Transform g, KitBatch k, NajdiMats m)
        {
            var f = new Front(new Vector3(0.2f, 0f, 12.6f), Vector3.back); // a runs toward -x: x = 0.2 - a
            const float gateA = -3.8f;                                      // x = 4, behind the right-hand arch
            foreach (var (a0, a1) in new[] { (-9.2f, gateA - 1.5f), (gateA + 1.5f, 9.2f) })
            {
                f.Box(k, m.MudDark, a0, a1, -0.35f, 0.5f, -0.6f, 0.06f, 2f);
                f.Box(k, m.Mud, a0, a1, 0.5f, 2.5f, -0.6f, 0f, 2.2f);
                f.Box(k, m.Gypsum, a0, a1, 1.9f, 2f, 0f, 0.04f, 2f);
            }
            for (float a = -8.8f; a < 9f; a += 0.8f)
                if (Mathf.Abs(a - gateA) > 2f) TriFace(k.Of(m.Dark), f.P(a, 1.48f, 0.006f), f.Out, 0.18f, 0.22f);
            for (float a = -8.9f; a < 9f; a += 0.6f)
                if (Mathf.Abs(a - gateA) > 1.75f) Merlon(k, m.Mud, f.P(a, 2.5f, -0.3f), f.Rot, 0.4f, 0.5f, 0.16f);
            NorthGate(g, k, m, new Vector3(0.2f - gateA, 0f, 12.45f));
        }

        /// <summary>A round, slightly tapering Najdi watchtower: plinth, gypsum rings, rings of triangle vents (some lit),
        /// spouts and a crown of stepped merlons; a painted door toward the plaza.</summary>
        static void Tower(KitBatch k, NajdiMats m, Vector3 at, float h)
        {
            const float r0 = 1.7f, r1 = 1.32f;
            float R(float v) => Mathf.Lerp(r0, r1, v / h);
            k.Of(m.Mud).AddFrustum(at, r0, r1, h, 24, false, true, 2.2f);
            k.Of(m.MudDark).AddFrustum(at, r0 + 0.08f, r0 + 0.04f, 0.9f, 24, false, true, 2f);
            foreach (float v in new[] { 3.4f, 6.5f }) k.Of(m.Gypsum).AddFrustum(at + Vector3.up * v, R(v) + 0.05f, R(v + 0.12f) + 0.05f, 0.12f, 24, true, true, 2f);
            k.Of(m.Gypsum).AddFrustum(at + Vector3.up * (h - 0.62f), R(h - 0.62f) + 0.06f, R(h - 0.52f) + 0.06f, 0.1f, 24, true, true, 2f);
            for (int i = 0; i < 14; i++)
            {
                var d = Quaternion.Euler(0f, i * 360f / 14f, 0f) * Vector3.forward;
                Merlon(k, m.Mud, at + Vector3.up * h + d * (r1 - 0.16f), Quaternion.LookRotation(d), 0.42f, 0.3f, 0.17f);
            }
            var rnd = new System.Random(77);
            for (int ring = 0; ring < 3; ring++)
            {
                float v = 2.3f + ring * 2.6f;
                for (int i = 0; i < 8; i++)
                {
                    var d = Quaternion.Euler(0f, (i + ring * 0.5f) * 45f, 0f) * Vector3.forward;
                    TriFace(k.Of(rnd.NextDouble() < 0.25 ? m.GlowTri : m.Dark), at + Vector3.up * v + d * (R(v + 0.15f) + 0.008f), d, 0.24f, 0.3f);
                }
            }
            for (int i = 0; i < 4; i++)
            {
                var d = Quaternion.Euler(0f, 20f + i * 90f, 0f) * Vector3.forward;
                Rod(k, m.PalmWood, at + Vector3.up * (h - 0.85f) + d * (r1 - 0.1f), at + Vector3.up * (h - 0.95f) + d * (r1 + 0.62f), 0.13f);
            }
            var toPlaza = new Vector3(-at.x, 0f, 3f - at.z).normalized;
            Door(k, m, new Front(new Vector3(at.x, at.y, at.z) + toPlaza * (r0 - 0.02f), toPlaza), 0f, 1f, 2.1f, 4, false);
        }

        /// <summary>
        /// The south side, behind the arrival: walls either side of a taller gate tower whose great painted door is the one you
        /// came through, lanterns on brackets either side of it and their light; low enough that only the arrival corner falls
        /// into its evening shadow.
        /// </summary>
        void SouthGate(Transform g, KitBatch k, NajdiMats m)
        {
            var f = new Front(new Vector3(0.2f, 0f, -6.6f), Vector3.forward); // a runs toward +x: x = a + 0.2
            NajdiBlock(k, m, f, -9.4f, -1.9f, 3.6f, 0.8f, 51, false);
            NajdiBlock(k, m, f, 1.5f, 9.4f, 3.6f, 0.8f, 53, false);
            Window(k, m, f, -5.4f, 1.1f, true, 1);
            Window(k, m, f, 5.6f, 1.1f, true, 3);
            var t = new Front(new Vector3(0f, 0f, -6.3f), Vector3.forward);
            NajdiBlock(k, m, t, -1.7f, 1.7f, 5.2f, 1.1f, 55, false);
            Door(k, m, t, 0f, 2f, 3f, 0);
            foreach (float x in new[] { -1.38f, 1.38f })
            {
                Rod(k, m.Iron, t.P(x, 2.85f, 0f), t.P(x, 2.85f, 0.32f), 0.03f);
                Lantern(k, m, t.P(x, 2.6f, 0.32f), 0.8f, 2.85f);
            }
            AddPointLight(g, new Vector3(0f, 2.9f, -5.75f), new Color(1f, 0.74f, 0.48f), 1f, 5f);
        }

        /// <summary>A brass lantern: a glowing core in an iron frame with a pyramid cap and finial, standing or on a chain.</summary>
        static void Lantern(KitBatch k, NajdiMats m, Vector3 c, float scale, float chainTop)
        {
            float hw = 0.12f * scale, hh = 0.19f * scale, bar = 0.016f * Mathf.Max(1f, scale);
            k.Box(m.LampGlow, c, new Vector3(hw * 1.7f, hh * 1.8f, hw * 1.7f));
            foreach (float x in new[] { -hw, hw })
                foreach (float z in new[] { -hw, hw }) k.Box(m.Iron, c + new Vector3(x, 0f, z), new Vector3(bar, hh * 2f, bar));
            foreach (float y in new[] { -hh, -hh * 0.25f, hh * 0.45f, hh })
                foreach (float s in new[] { -hw, hw })
                {
                    k.Box(m.Iron, c + new Vector3(0f, y, s), new Vector3(hw * 2f, bar * 0.8f, bar * 0.8f));
                    k.Box(m.Iron, c + new Vector3(s, y, 0f), new Vector3(bar * 0.8f, bar * 0.8f, hw * 2f));
                }
            foreach (float s in new[] { -hw, hw })
            {
                k.Box(m.Iron, c + new Vector3(0f, 0f, s), new Vector3(bar * 0.6f, hh * 2f, bar * 0.6f));
                k.Box(m.Iron, c + new Vector3(s, 0f, 0f), new Vector3(bar * 0.6f, hh * 2f, bar * 0.6f));
            }
            var square = Quaternion.Euler(0f, 45f, 0f);
            k.Of(m.Brass).AddFrustum(c + Vector3.up * hh, hw * 1.55f, 0.02f * scale, hh * 0.9f, 4, true, false, 1f, square);
            k.Of(m.Brass).AddFrustum(c + Vector3.down * (hh + 0.04f * scale), hw * 0.9f, hw * 1.35f, 0.04f * scale, 4, true, true, 1f, square);
            k.Of(m.Brass).AddSphere(c + Vector3.up * (hh * 1.9f + 0.03f * scale), 0.025f * scale, 8, 5);
            if (chainTop > c.y) Rod(k, m.Iron, c + Vector3.up * (hh * 1.9f + 0.05f * scale), new Vector3(c.x, chainTop, c.z), 0.012f);
        }

        /// <summary>A dallah, the Arabic coffee pot: waisted body, flared lid with a finial, a long beak spout and a handle.</summary>
        static void Dallah(KitBatch k, NajdiMats m, Vector3 at)
        {
            var b = k.Of(m.Brass);
            b.AddLathe(at, new[] { new Vector2(0f, 0f), new Vector2(0.075f, 0f), new Vector2(0.085f, 0.03f), new Vector2(0.08f, 0.09f), new Vector2(0.05f, 0.15f),
                new Vector2(0.04f, 0.19f), new Vector2(0.06f, 0.22f), new Vector2(0.065f, 0.235f), new Vector2(0.03f, 0.27f), new Vector2(0.012f, 0.3f), new Vector2(0f, 0.34f) }, 16);
            b.AddTube(new List<Vector3> { at + new Vector3(0.07f, 0.06f, 0f), at + new Vector3(0.12f, 0.12f, 0f), at + new Vector3(0.15f, 0.2f, 0f), at + new Vector3(0.17f, 0.25f, 0f), at + new Vector3(0.22f, 0.27f, 0f) }, 0.012f, 6);
            b.AddTube(new List<Vector3> { at + new Vector3(-0.07f, 0.05f, 0f), at + new Vector3(-0.13f, 0.09f, 0f), at + new Vector3(-0.13f, 0.18f, 0f), at + new Vector3(-0.05f, 0.21f, 0f) }, 0.008f, 5);
        }

        /// <summary>A small handleless coffee cup, poured (or knocked over on its side).</summary>
        static void Cup(KitBatch k, NajdiMats m, Vector3 at, bool tipped)
        {
            if (tipped)
            {
                k.Of(m.PaintWhite).AddFrustum(at + new Vector3(0f, 0.026f, 0f), 0.02f, 0.032f, 0.045f, 12, true, false, 1f, Quaternion.FromToRotation(Vector3.up, new Vector3(0.8f, 0f, 0.6f)));
                return;
            }
            k.Of(m.PaintWhite).AddFrustum(at, 0.02f, 0.032f, 0.045f, 12, true, false);
            k.Of(m.Dark).AddDisc(at + Vector3.up * 0.034f, Vector3.up, 0.028f, 12);
        }
    }
}

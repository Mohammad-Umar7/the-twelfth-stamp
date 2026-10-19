using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    public partial class RioCell
    {
        /// <summary>
        /// The parade float, rebuilt over the old platform (whose collider stays): a turquoise body sheathed in tilted
        /// gold and magenta sequin diamonds, marquee bulbs and a low gold rail (kept below the sightline to the drum),
        /// tassels at the hem, plume clusters on gold poles at the front corners, a sequinned upper tier, two frozen
        /// passistas with feather fans and tropical leaves behind them, the surdo dressed in chrome, and the centrepiece:
        /// a gold sun mask with staring eyes and puffed cheeks, holding its breath, ringed by rays and plumes.
        /// </summary>
        void BuildFloat(KitBatch k, CarnivalMats m, Transform g, Transform floatRoot)
        {
            var f = floatRoot.localPosition;
            var body = m.Plume[2];
            var rnd = new System.Random(21);
            k.Box(body, f + new Vector3(0f, 0.62f, 0f), new Vector3(4.75f, 1.18f, 3.55f));
            k.Box(m.Gold, f + new Vector3(0f, 1.225f, 0f), new Vector3(4.82f, 0.07f, 3.62f));
            k.Box(m.Gold, f + new Vector3(0f, 0.05f, 0f), new Vector3(4.8f, 0.1f, 3.6f));
            // Sequin diamonds, each tilted a few degrees so they catch the coloured spots unevenly.
            float front = f.z - 1.775f;
            for (int row = 0; row < 5; row++)
                for (int col = 0; col < 26; col++)
                {
                    float x = -2.25f + col * 0.18f + (row % 2) * 0.09f;
                    if (x > 2.3f || (x > -2.0f && x < -0.4f)) continue; // the panel behind the stamp plate stays calm
                    var q = Quaternion.Euler(DetailShapes.Range(rnd, -9f, 9f), DetailShapes.Range(rnd, -9f, 9f), 45f);
                    k.Box((row + col) % 2 == 0 ? m.SequinGold : m.SequinMagenta, new Vector3(x, 0.34f + row * 0.17f, front - 0.012f), new Vector3(0.12f, 0.12f, 0.008f), q);
                }
            foreach (float sx in new[] { -1f, 1f })
                for (int row = 0; row < 5; row++)
                    for (int col = 0; col < 19; col++)
                    {
                        float z = f.z - 1.6f + col * 0.18f + (row % 2) * 0.09f;
                        if (z > f.z + 1.65f) continue;
                        var q = Quaternion.Euler(DetailShapes.Range(rnd, -9f, 9f), 90f + DetailShapes.Range(rnd, -9f, 9f), 45f);
                        k.Box((row + col) % 2 == 0 ? m.SequinGold : m.SequinTurquoise, new Vector3(sx * 2.388f, 0.34f + row * 0.17f, z), new Vector3(0.12f, 0.12f, 0.008f), q);
                    }
            // A dark panel framed in gold behind the stamp plate: the rhythm ring reads against it, not against sequins.
            k.Box(m.DarkMetal, new Vector3(-1.2f, 0.69f, front - 0.012f), new Vector3(1.5f, 0.86f, 0.02f));
            foreach (float y in new[] { 0.25f, 1.13f }) k.Box(m.Gold, new Vector3(-1.2f, y, front - 0.024f), new Vector3(1.56f, 0.035f, 0.02f));
            foreach (float x in new[] { -1.96f, -0.44f }) k.Box(m.Gold, new Vector3(x, 0.69f, front - 0.024f), new Vector3(0.035f, 0.9f, 0.02f));
            // Marquee bulbs along the top edges, a low gold rail, tassels at the hem.
            for (int i = 0; i < 19; i++) k.Of(m.Bulbs[0]).AddSphere(new Vector3(-2.25f + i * 0.25f, 1.15f, front - 0.03f), 0.032f, 6, 4);
            foreach (float sx in new[] { -1f, 1f })
                for (int i = 0; i < 14; i++) k.Of(m.Bulbs[0]).AddSphere(new Vector3(sx * 2.41f, 1.15f, f.z - 1.6f + i * 0.25f), 0.032f, 6, 4);
            for (int i = 0; i < 24; i++)
            {
                float x = -2.3f + i * 0.2f;
                k.Box(m.Gold, new Vector3(x, 1.33f, front + 0.06f), new Vector3(0.025f, 0.16f, 0.025f));
                k.Box(m.Gold, new Vector3(x + 0.1f, 0.0f + 0.13f, front - 0.02f), new Vector3(0.03f, 0.2f, 0.012f));
            }
            k.Box(m.Gold, new Vector3(0f, 1.415f, front + 0.06f), new Vector3(4.65f, 0.035f, 0.035f));
            foreach (float sx in new[] { -1f, 1f })
                k.Box(m.Gold, new Vector3(sx * 2.33f, 1.415f, f.z), new Vector3(0.035f, 0.035f, 3.4f));
            // Upper tier behind the drum, sequinned, with bulbs and a gold cap.
            k.Box(m.SequinMagenta, f + new Vector3(0f, 1.55f, 1.0f), new Vector3(3.2f, 0.6f, 1.6f));
            k.Box(m.Gold, f + new Vector3(0f, 1.87f, 1.0f), new Vector3(3.26f, 0.06f, 1.66f));
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 19; col++)
                {
                    float x = -1.48f + col * 0.164f + (row % 2) * 0.082f;
                    if (x > 1.5f) continue;
                    k.Box(row % 2 == 0 ? m.SequinGold : m.SequinTurquoise, new Vector3(x, 1.36f + row * 0.17f, f.z + 0.188f), new Vector3(0.1f, 0.1f, 0.008f), Quaternion.Euler(DetailShapes.Range(rnd, -9f, 9f), DetailShapes.Range(rnd, -9f, 9f), 45f));
                }
            for (int i = 0; i < 13; i++) k.Of(m.Bulbs[i % 2 == 0 ? 0 : 4]).AddSphere(new Vector3(-1.5f + i * 0.25f, 1.84f, f.z + 0.17f), 0.03f, 6, 4);

            // The surdo on the float: chrome shell (material set by the cell), rims, a white head, band and rods.
            var drum = new Vector3(1.2f, 1.2f, f.z - 0.6f);
            k.Of(m.DrumHead).AddCylinder(drum + Vector3.up * 0.6f, 0.44f, 0.006f, 20);
            foreach (float y in new[] { 0.02f, 0.59f }) k.Of(m.Chrome).AddTorus(drum + Vector3.up * y, 0.458f, 0.024f, 20, 4, Quaternion.identity);
            k.Of(m.Plume[0]).AddCylinder(drum + Vector3.up * 0.23f, 0.456f, 0.14f, 20);
            for (int i = 0; i < 10; i++)
            {
                var d = Quaternion.Euler(0f, i * 36f + 18f, 0f) * Vector3.right * 0.472f;
                Limb(k.Of(m.Chrome), drum + d + Vector3.up * 0.05f, drum + d + Vector3.up * 0.56f, 0.008f, 0.008f, 4);
            }

            // Plume clusters on gold poles at the front corners, fanned outward (clear of the drum).
            foreach (float sx in new[] { -1f, 1f })
            {
                var b = new Vector3(sx * 2.05f, 1.2f, front + 0.32f);
                Limb(k.Of(m.Gold), b, b + Vector3.up * 0.6f, 0.035f, 0.03f, 8);
                k.Of(m.Gold).AddFrustum(b + Vector3.up * 0.58f, 0.04f, 0.1f, 0.12f, 10);
                var top = b + Vector3.up * 0.68f;
                for (int i = 0; i < 11; i++)
                {
                    float a = Mathf.Lerp(-12f, 64f, i / 10f) * Mathf.Deg2Rad;
                    var dir = (Vector3.up * Mathf.Cos(a) + Vector3.right * (sx * Mathf.Sin(a))).normalized;
                    DetailShapes.Feather(k.Of(m.Plume[new[] { 0, 1, 2, 3 }[i % 4]]), top, dir, Vector3.back, DetailShapes.Range(rnd, 0.95f, 1.25f), 0.2f, Vector3.right * sx - Vector3.up * 0.4f, 0.2f, 6);
                }
                // Big tropical leaves and a passista at each rear corner, beside the sun mask.
                var leafRoot = new Vector3(sx * 2.15f, 1.45f, f.z + 1.45f);
                for (int i = 0; i < 5; i++)
                {
                    float a = Mathf.Lerp(10f, 80f, i / 4f) * Mathf.Deg2Rad;
                    var dir = (Vector3.up * Mathf.Cos(a) + Vector3.right * (sx * Mathf.Sin(a))).normalized;
                    DetailShapes.Feather(k.Of(i % 2 == 0 ? m.Plume[6] : m.Gold), leafRoot, dir, Vector3.back, 1.6f - i * 0.08f, 0.52f, Vector3.right * sx - Vector3.up * 0.5f, 0.25f, 7, 0.2f);
                }
                Dancer(k, m, new Vector3(sx * 2.0f, 1.26f, f.z + 1.15f), 180f, sx < 0 ? FigurePose.Wave : FigurePose.Samba, sx < 0 ? 0 : 2, 0.95f);
            }

            BuildSunMask(k, m, g, f + new Vector3(0f, 3.3f, 1.2f));
        }

        /// <summary>
        /// The sun mask: a gold sphere-cap face on a gold disc and ring, wide staring eyes, puffed rose cheeks and pressed
        /// lips (the carnival holding its breath), a ridge of a nose, arched brows; sixteen rays and a ring of plumes.
        /// The face is its own small mesh (a cap that bulges toward the avenue).
        /// </summary>
        void BuildSunMask(KitBatch k, CarnivalMats m, Transform g, Vector3 c)
        {
            const float rim = 0.74f, depth = 0.3f;
            float Rs = (rim * rim + depth * depth) / (2f * depth);
            var sphereCentre = c + Vector3.forward * (Rs - depth);
            // The cap: a lathe around +Y, turned so it faces -Z (toward the player).
            var cap = new MeshBuilder();
            var prof = new List<Vector2>();
            float thetaRim = Mathf.Asin(rim / Rs);
            for (int i = 0; i <= 10; i++)
            {
                float th = Mathf.Lerp(thetaRim, 0f, i / 10f);
                prof.Add(new Vector2(Mathf.Max(0.001f, Mathf.Sin(th) * Rs), Mathf.Cos(th) * Rs - (Rs - depth)));
            }
            cap.AddLathe(Vector3.zero, prof, 24, 0.5f);
            var face = MeshFactory.FromBuilder("SunMaskFace", cap, m.Gold, g);
            face.transform.localPosition = c;
            face.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            Vector3 On(float x, float y, float lift)
            {
                var p = new Vector3(c.x + x, c.y + y, 0f);
                float z = sphereCentre.z - Mathf.Sqrt(Mathf.Max(0f, Rs * Rs - x * x - y * y));
                var n = (new Vector3(p.x, p.y, z) - sphereCentre).normalized;
                return new Vector3(p.x, p.y, z) + n * lift;
            }
            Vector3 N(float x, float y) => (On(x, y, 0f) - sphereCentre).normalized;
            k.Of(m.Gold).AddDisc(c + Vector3.forward * 0.005f, Vector3.back, 0.99f, 36);
            k.Of(m.Gold).AddDisc(c + Vector3.forward * 0.015f, Vector3.forward, 0.99f, 36);
            k.Of(m.Gold).AddTorus(c, 0.99f, 0.065f, 36, 6, Quaternion.Euler(90f, 0f, 0f));
            k.Of(m.SequinMagenta).AddTorus(c + Vector3.back * 0.02f, 0.84f, 0.035f, 32, 4, Quaternion.Euler(90f, 0f, 0f));
            // Eyes: white, wide open, small black pupils turned down toward whoever stands at the stamp plate.
            foreach (float sx in new[] { -1f, 1f })
            {
                k.Of(m.DrumHead).AddDisc(On(sx * 0.25f, 0.12f, 0.012f), N(sx * 0.25f, 0.12f), 0.115f, 18);
                k.Of(m.DarkMetal).AddDisc(On(sx * 0.25f - 0.03f, 0.085f, 0.02f), N(sx * 0.25f - 0.03f, 0.085f), 0.03f, 12); // staring down at the plate
                for (int i = 0; i < 3; i++) // arched brow
                {
                    float x0 = sx * (0.13f + i * 0.09f), x1 = sx * (0.13f + (i + 1) * 0.09f);
                    float y0 = 0.29f - (i - 1f) * (i - 1f) * 0.02f, y1 = 0.29f - i * i * 0.02f;
                    DetailShapes.PlaneStrip(k, m.Gold, On(x0, y0, 0.02f), On(x1, y1, 0.02f), N(sx * 0.22f, 0.29f), 0.035f, 0.03f, 0.02f);
                }
                // Puffed cheeks.
                k.Of(m.SequinMagenta).AddSphere(On(sx * 0.37f, -0.15f, -0.08f), 0.19f, 12, 8);
            }
            // Pressed lips and a ridge of a nose.
            var lips = On(0f, -0.3f, 0.02f);
            k.Box(m.Plume[0], lips, new Vector3(0.24f, 0.05f, 0.06f), Quaternion.LookRotation(N(0f, -0.3f), Vector3.up));
            foreach (float sx in new[] { -1f, 1f }) k.Of(m.Plume[0]).AddSphere(On(sx * 0.12f, -0.3f, 0.015f), 0.03f, 8, 5);
            k.Box(m.Gold, On(0f, -0.04f, 0.035f), new Vector3(0.08f, 0.2f, 0.08f), Quaternion.LookRotation(N(0f, -0.04f), Vector3.up));
            // Rays (long gold, short orange) and the ring of plumes; the bottom stays clear of the tier.
            var longRay = new List<Vector2> { new Vector2(0.15f, 1.0f), new Vector2(0f, 1.95f), new Vector2(-0.15f, 1.0f) };
            var shortRay = new List<Vector2> { new Vector2(0.12f, 1.0f), new Vector2(0f, 1.55f), new Vector2(-0.12f, 1.0f) };
            for (int i = 0; i < 16; i++)
            {
                float a = i * 22.5f;
                if (a > 140f && a < 220f) continue;
                k.Of(i % 2 == 0 ? m.Gold : m.Plume[4]).AddExtrudedXY(i % 2 == 0 ? longRay : shortRay, 0.04f, c + Vector3.forward * 0.03f, Quaternion.Euler(0f, 0f, a), 1f);
            }
            for (int i = 0; i < 28; i++)
            {
                float a = i / 28f * 360f;
                if (a > 125f && a < 235f) continue;
                float rad = a * Mathf.Deg2Rad;
                var dir = new Vector3(-Mathf.Sin(rad), Mathf.Cos(rad), 0f);
                var mat = m.Plume[new[] { 0, 2, 1, 3 }[i % 4]];
                DetailShapes.Feather(k.Of(mat), c + dir * 1.45f + Vector3.forward * 0.08f, dir, Vector3.back, i % 2 == 0 ? 1.35f : 1.12f, 0.27f, Vector3.forward, 0.12f, 6);
            }
            k.Box(m.Gold, c + new Vector3(0f, -1.4f, 0.12f), new Vector3(0.18f, 1.2f, 0.18f)); // mount
        }
    }
}

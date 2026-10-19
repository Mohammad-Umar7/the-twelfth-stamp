using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    // Place detail for Rio (playtest: "make all the countries detailed"). Builders add architecture, set dressing
    // and practical lights around the puzzle objects, batched per material; far vistas are built in Landmarks.cs / Vista.cs.
    public partial class RioCell
    {
        /// <summary>
        /// The carnival holding its breath. A parade avenue paved in pale granite with black-and-white wave mosaic on the
        /// arrival plaza and both promenades; painted barrier walls and grandstands with coloured seats and frozen
        /// mannequin spectators; floodlight masts; criss-crossing strings of coloured bulbs and pennants; streamers and
        /// confetti stopped mid-air; passistas in sequins and feather fans frozen mid-step; a silent bateria behind the
        /// float; coconut palms and pastel apartment blocks with flags on their balconies above the stands; the float
        /// itself (Detail_RioFloat.cs). Coloured spots (magenta, cyan) and a warm front light make the float the stage.
        /// </summary>
        void BuildCarnivalDetail(Transform g, Transform floatRoot)
        {
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
            var m = new CarnivalMats
            {
                Gold = Mats.Lit(new Color(0.95f, 0.72f, 0.3f), 0.7f, 0.7f, null, false, new Color(0.12f, 0.08f, 0.02f)),
                Chrome = Mats.Lit(new Color(0.86f, 0.87f, 0.9f), 0.85f, 0.85f),
                DarkMetal = Mats.Lit(new Color(0.08f, 0.08f, 0.09f), 0.5f, 0.6f),
                Skin = Mats.Lit(new Color(0.88f, 0.84f, 0.8f), 0.62f),
                DrumHead = Mats.Lit(new Color(0.95f, 0.94f, 0.9f), 0.4f),
                Wood = Mats.Lit(new Color(0.45f, 0.3f, 0.18f), 0.4f, 0f, "wood_dark"),
                SequinGold = Mats.Lit(new Color(1f, 0.8f, 0.32f), 0.9f, 0.8f, null, false, new Color(0.08f, 0.06f, 0.02f)),
                SequinMagenta = Mats.Lit(new Color(0.92f, 0.1f, 0.56f), 0.88f, 0.5f, null, false, new Color(0.1f, 0.01f, 0.06f)),
                SequinTurquoise = Mats.Lit(new Color(0.05f, 0.78f, 0.78f), 0.88f, 0.5f, null, false, new Color(0.01f, 0.08f, 0.08f)),
                Plume = plume,
                Bulbs = new[]
                {
                    Mats.Emissive(new Color(1f, 0.85f, 0.6f), new Color(1.5f, 1.1f, 0.6f)), Mats.Emissive(new Color(1f, 0.3f, 0.25f), new Color(1.5f, 0.22f, 0.16f)),
                    Mats.Emissive(new Color(0.4f, 1f, 0.45f), new Color(0.3f, 1.35f, 0.35f)), Mats.Emissive(new Color(0.3f, 0.85f, 1f), new Color(0.2f, 0.95f, 1.45f)),
                    Mats.Emissive(new Color(1f, 0.35f, 0.9f), new Color(1.45f, 0.3f, 1.2f)),
                },
            };
            // One batch for everything (one mesh per material, about thirty in all); the flat paving on its own without
            // shadows. With exactly eight lights in the cell every mesh receives all of them, so nothing pops.
            var k = new KitBatch();
            var paving = new KitBatch();
            BuildPavement(paving);
            BuildStands(k, m);
            BuildMasts(k, m);
            BuildFloat(k, m, g, floatRoot);
            BuildParade(k, m);
            BuildFestoons(k, m);
            BuildSurroundings(k, m);
            BuildLitter(k, m);
            k.Bake(g, "CarnivalDetail");
            paving.Bake(g, "CarnivalPaving", false);
            // The old flat fans and plain platform give way to the built float (their colliders stay).
            foreach (var r in floatRoot.GetComponentsInChildren<MeshRenderer>())
                if (r.name == "Fan" || r.name == "Platform") r.enabled = false;

            // Stage lights (with the four floodlights: eight in all, the forward renderer's per-object limit): magenta
            // and cyan spots from the near masts cross on the float, a warm light on its face and the plate, a violet
            // rim behind the plumes.
            var target = new Vector3(0.2f, 2.6f, 9.7f);
            foreach (var (pos, col) in new[] { (new Vector3(-4.45f, 5.55f, 0.35f), new Color(1f, 0.25f, 0.8f)), (new Vector3(4.45f, 5.55f, 0.35f), new Color(0.2f, 0.85f, 1f)) })
                AddSpotLight(g, pos, Quaternion.LookRotation(target - pos).eulerAngles, col, 9f, 17f, 48f);
            AddPointLight(g, new Vector3(0f, 2.3f, 6.4f), new Color(1f, 0.72f, 0.42f), 2.2f, 6.5f);
            AddPointLight(g, new Vector3(0f, 4.6f, 12.6f), new Color(0.55f, 0.35f, 1f), 3.2f, 8f);
        }

        /// <summary>Wave mosaic (calçada): white stone with black bands, across the plaza and along both promenades.</summary>
        void BuildPavement(KitBatch k)
        {
            var white = Mats.Lit(new Color(0.96f, 0.94f, 0.9f), 0.35f, 0f, "tile_terrazzo");
            var black = Mats.Lit(new Color(0.55f, 0.55f, 0.58f), 0.45f, 0f, "cobble_wet");
            const float y = 0.0068f, band = 0.34f;
            k.Box(white, new Vector3(0f, 0.003f, -5.175f), new Vector3(6.7f, 0.006f, 3.55f), 0.5f);
            for (int i = 0; i < 7; i++)
                WaveBand(k.Of(black), true, -3.35f, 3.35f, -6.95f, -3.4f, -6.95f - band + i * 2f * band, band, 0.2f, 2.6f, 0f, y);
            foreach (float sx in new[] { -1f, 1f })
            {
                float c0 = sx > 0 ? 3.35f : -5.05f, c1 = sx > 0 ? 5.05f : -3.35f;
                k.Box(white, new Vector3(sx * 4.2f, 0.003f, 4.025f), new Vector3(1.7f, 0.006f, 21.95f), 0.5f);
                for (int i = 0; i < 4; i++)
                    WaveBand(k.Of(black), false, -6.95f, 15f, c0, c1, c0 - band + i * 2f * band, band, 0.2f, 2.6f, sx * 0.8f, y);
                k.Box(black, new Vector3(sx * 3.35f, 0.012f, 5.8f), new Vector3(0.14f, 0.024f, 18.4f)); // basalt kerb line
            }
            k.Box(black, new Vector3(0f, 0.012f, -3.4f), new Vector3(6.84f, 0.024f, 0.14f));
        }

        /// <summary>
        /// One black band of the wave mosaic: along the waves (x when <paramref name="alongX"/>, else z) it runs over
        /// [a0, a1]; across them it spans [offset, offset + width] plus a sine, clamped to [c0, c1].
        /// </summary>
        static void WaveBand(MeshBuilder b, bool alongX, float a0, float a1, float c0, float c1, float offset, float width, float amp, float wavelength, float phase, float y)
        {
            int n = Mathf.CeilToInt((a1 - a0) / 0.2f);
            Vector3 P(float s, float c) => alongX ? new Vector3(s, y, c) : new Vector3(c, y, s);
            for (int i = 0; i < n; i++)
            {
                float s0 = Mathf.Lerp(a0, a1, i / (float)n), s1 = Mathf.Lerp(a0, a1, (i + 1) / (float)n);
                float w0 = offset + amp * Mathf.Sin(s0 / wavelength * Mathf.PI * 2f + phase), w1 = offset + amp * Mathf.Sin(s1 / wavelength * Mathf.PI * 2f + phase);
                float lo0 = Mathf.Clamp(w0, c0, c1), hi0 = Mathf.Clamp(w0 + width, c0, c1), lo1 = Mathf.Clamp(w1, c0, c1), hi1 = Mathf.Clamp(w1 + width, c0, c1);
                if (hi0 - lo0 < 0.004f || hi1 - lo1 < 0.004f) continue;
                Vector3 A = P(s0, lo0), B = P(s1, lo1), C = P(s1, hi1), D = P(s0, hi0);
                if (Vector3.Cross(B - A, D - A).y < 0f) { var t = B; B = D; D = t; }
                b.AddQuad(A, B, C, D, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            }
        }

        /// <summary>
        /// Grandstand dressing: painted barrier walls with a rail, coloured seats on every tier, handrails, a top rail
        /// with flags, and frozen mannequin spectators (none behind the stand seam).
        /// </summary>
        void BuildStands(KitBatch k, CarnivalMats m)
        {
            var rnd = new System.Random(8);
            int[] seatCols = { 1, 6, 2, 3, 0, 5 };
            foreach (float sx in new[] { -1f, 1f })
            {
                for (int i = 0; i < 10; i++)
                {
                    float zc = -5.9f + i * 2.2f;
                    k.Box(m.Plume[new[] { 0, 1, 2, 4, 5, 6 }[(i + (sx > 0 ? 3 : 0)) % 6]], new Vector3(sx * 5.175f, 0.575f, zc), new Vector3(0.25f, 1.15f, 2.18f));
                    k.Box(m.DrumHead, new Vector3(sx * 5.045f, 0.62f, zc), new Vector3(0.012f, 0.42f, 0.42f), Quaternion.Euler(45f, 0f, 0f));
                }
                k.Box(m.DrumHead, new Vector3(sx * 5.175f, 1.17f, 4f), new Vector3(0.31f, 0.045f, 22f));
                for (int i = 0; i <= 20; i++) k.Box(m.DarkMetal, new Vector3(sx * 5.12f, 1.42f, -7f + i * 1.1f), new Vector3(0.03f, 0.5f, 0.03f));
                k.Box(m.DarkMetal, new Vector3(sx * 5.12f, 1.67f, 4f), new Vector3(0.04f, 0.04f, 22f));
                // Seats: a block and a back on each tier, in coloured sections.
                for (int s = 0; s < 5; s++)
                {
                    float top = 0.5f + s, xb = sx * (5.3f + 0.6f * s + 0.38f);
                    int idx = 0;
                    for (float z = -6.7f; z < 14.8f; z += 0.55f, idx++)
                    {
                        var mat = m.Plume[seatCols[(idx / 6 + s) % seatCols.Length]];
                        k.Box(mat, new Vector3(xb, top + 0.2f, z), new Vector3(0.36f, 0.4f, 0.42f));
                        k.Box(mat, new Vector3(xb + sx * 0.19f, top + 0.6f, z), new Vector3(0.04f, 0.4f, 0.42f));
                    }
                }
                // Handrails up the aisles, a top rail and flags.
                foreach (float z in new[] { -4.3f, 7.9f, 12.9f })
                    Limb(k.Of(m.DarkMetal), new Vector3(sx * 5.3f, 1.7f, z), new Vector3(sx * 8.2f, 5.5f, z), 0.025f, 0.025f, 6);
                for (int i = 0; i <= 20; i++) k.Box(m.DarkMetal, new Vector3(sx * 8.25f, 5.0f, -7f + i * 1.1f), new Vector3(0.035f, 1.0f, 0.035f));
                k.Box(m.DarkMetal, new Vector3(sx * 8.25f, 5.5f, 4f), new Vector3(0.045f, 0.045f, 22f));
                k.Box(m.DarkMetal, new Vector3(sx * 8.25f, 5.05f, 4f), new Vector3(0.03f, 0.03f, 22f));
                for (int i = 0; i < 7; i++)
                {
                    float z = -6.2f + i * 3.4f;
                    Limb(k.Of(m.DarkMetal), new Vector3(sx * 8.35f, 4.5f, z), new Vector3(sx * 8.35f, 7.8f, z), 0.03f, 0.022f, 6);
                    k.Box(m.Plume[(i * 3 + (sx > 0 ? 1 : 0)) % m.Plume.Length], new Vector3(sx * 8.35f, 7.35f, z + 0.5f), new Vector3(0.02f, 0.7f, 1.0f), Quaternion.Euler(DetailShapes.Range(rnd, -6f, 6f), DetailShapes.Range(rnd, -14f, 14f), DetailShapes.Range(rnd, -4f, 4f)));
                }
                // Frozen spectators: seated with an arm up, or standing at the top cheering.
                for (int i = 0; i < 9; i++)
                {
                    int s = 1 + rnd.Next(3);
                    float z = DetailShapes.Range(rnd, -6.2f, 14.2f);
                    if (sx > 0 && z > 0.8f && z < 5.2f) z += 4.6f; // keep the view behind the stand seam clear
                    float top = 0.5f + s, xb = sx * (5.3f + 0.6f * s + 0.38f);
                    Mannequin(k, m, new Vector3(xb, top - 0.05f, z), sx > 0 ? -90f : 90f, FigurePose.Seated, 1f, out var hand, out _, out _);
                    if (i % 3 == 0) // a little flag in the raised hand
                    {
                        Limb(k.Of(m.Wood), hand, hand + Vector3.up * 0.45f, 0.008f, 0.008f, 4);
                        k.Box(m.Plume[(i + 2) % m.Plume.Length], hand + Vector3.up * 0.36f + Vector3.forward * 0.13f, new Vector3(0.01f, 0.18f, 0.26f));
                    }
                }
                for (int i = 0; i < 3; i++)
                {
                    float z = -4.5f + i * 7.3f + DetailShapes.Range(rnd, -0.6f, 0.6f) + (sx > 0 && i == 1 ? 3.4f : 0f);
                    Mannequin(k, m, new Vector3(sx * 7.95f, 4.5f, z), sx > 0 ? -90f : 90f, FigurePose.Cheer, 1f, out _, out _, out _);
                }
            }
        }

        /// <summary>Floodlight masts: a plinth, a head frame and four lamp housings with lit lenses; coloured gel heads.</summary>
        void BuildMasts(KitBatch k, CarnivalMats m)
        {
            var lens = m.Bulbs[0];
            foreach (float x in new[] { -4.6f, 4.6f })
                for (int i = 0; i < 2; i++)
                {
                    float sx = Mathf.Sign(x);
                    var b = new Vector3(x, 0f, i * 9f);
                    k.Box(m.DarkMetal, b + Vector3.up * 0.25f, new Vector3(0.42f, 0.5f, 0.42f));
                    k.Box(m.DarkMetal, b + new Vector3(0f, 6.2f, 0f), new Vector3(0.1f, 0.45f, 0.1f));
                    foreach (float y in new[] { 5.95f, 6.42f })
                        k.Box(m.DarkMetal, b + new Vector3(-sx * 0.12f, y, 0f), new Vector3(0.07f, 0.07f, 1.5f));
                    var dir = new Vector3(-sx, -0.75f, 0.35f).normalized;
                    var rot = Quaternion.LookRotation(dir, Vector3.up);
                    foreach (float dz in new[] { -0.55f, -0.18f, 0.18f, 0.55f })
                    {
                        var p = b + new Vector3(-sx * 0.25f, 6.18f, dz);
                        k.Box(m.DarkMetal, p, new Vector3(0.3f, 0.3f, 0.26f), rot);
                        k.Box(lens, p + dir * 0.135f, new Vector3(0.24f, 0.24f, 0.012f), rot);
                    }
                    if (i == 0) // the coloured gel head that throws its spot on the float
                    {
                        var gel = sx < 0 ? m.Bulbs[4] : m.Bulbs[3];
                        var gp = new Vector3(sx * 4.45f, 5.55f, 0.35f);
                        var gd = (new Vector3(0.2f, 2.6f, 9.7f) - gp).normalized;
                        k.Of(m.DarkMetal).AddCylinder(gp - gd * 0.2f, 0.15f, 0.38f, 12, 1f, Quaternion.FromToRotation(Vector3.up, gd));
                        k.Of(gel).AddDisc(gp + gd * 0.185f, gd, 0.13f, 12);
                        k.Box(m.DarkMetal, (gp + b + Vector3.up * 5.55f) * 0.5f, new Vector3(0.05f, 0.05f, 0.3f), Quaternion.LookRotation(gp - (b + Vector3.up * 5.55f), Vector3.up));
                    }
                }
        }

        /// <summary>Passistas frozen along the avenue sides and a silent bateria behind the float.</summary>
        void BuildParade(KitBatch k, CarnivalMats m)
        {
            Dancer(k, m, new Vector3(-3.45f, 0f, -1.0f), 70f, FigurePose.Samba, 0);
            Dancer(k, m, new Vector3(3.5f, 0f, -2.4f), -110f, FigurePose.Wave, 2);
            Dancer(k, m, new Vector3(-3.3f, 0f, 3.8f), 25f, FigurePose.Samba, 1);
            Dancer(k, m, new Vector3(3.55f, 0f, 6.5f), -65f, FigurePose.Samba, 0);
            int c = 0;
            foreach (float x in new[] { -3.7f, -2.6f, -1.4f, 1.4f, 2.6f, 3.7f })
                Drummer(k, m, new Vector3(x, 0f, 12.1f + (c % 2) * 1.1f), 180f + (x < 0 ? 6f : -6f), c++);
        }

        /// <summary>Strings of coloured bulbs with pennants across the avenue, streamers and confetti stopped mid-air.</summary>
        void BuildFestoons(KitBatch k, CarnivalMats m)
        {
            var rnd = new System.Random(5);
            StringLights(k, m, new Vector3(-4.6f, 5.6f, 0f), new Vector3(4.6f, 5.6f, 0f), 1.1f, false, 0);
            StringLights(k, m, new Vector3(-4.6f, 5.6f, 9f), new Vector3(4.6f, 5.6f, 9f), 1.1f, false, 2);
            StringLights(k, m, new Vector3(-8.3f, 5.5f, -6.0f), new Vector3(8.3f, 5.5f, -3.0f), 1.2f, true, 1);
            StringLights(k, m, new Vector3(8.3f, 5.5f, -1.5f), new Vector3(-8.3f, 5.5f, 2.2f), 1.25f, false, 3);
            StringLights(k, m, new Vector3(-8.3f, 5.5f, 3.8f), new Vector3(8.3f, 5.5f, 6.6f), 1.15f, true, 4);
            StringLights(k, m, new Vector3(8.3f, 5.5f, 11.0f), new Vector3(-8.3f, 5.5f, 13.6f), 1.2f, true, 0);
            // Streamers thrown from the stands, caught mid-flight; a few hanging from the strings.
            for (int i = 0; i < 16; i++)
            {
                float sx = i % 2 == 0 ? -1f : 1f;
                var a = new Vector3(sx * DetailShapes.Range(rnd, 5.8f, 7.6f), DetailShapes.Range(rnd, 2.2f, 4.8f), DetailShapes.Range(rnd, -5.5f, 13f));
                var b = new Vector3(-sx * DetailShapes.Range(rnd, 0.5f, 4.4f), DetailShapes.Range(rnd, 0.6f, 3.4f), a.z + DetailShapes.Range(rnd, -2.5f, 2.5f));
                var path = StreamerPath(a, b, DetailShapes.Range(rnd, 0.5f, 1.4f), DetailShapes.Range(rnd, 0.14f, 0.26f), DetailShapes.Range(rnd, 7f, 12f), 72);
                if (!path.TrueForAll(q => ClearOfPlay(q) && ClearOfStage(q))) continue;
                DetailShapes.Ribbon(k.Of(m.Plume[i % m.Plume.Length]), path, 0.026f, Vector3.up, DetailShapes.Range(rnd, 2f, 5f));
            }
            for (int i = 0; i < 8; i++)
            {
                var a = new Vector3(DetailShapes.Range(rnd, -3.8f, 3.8f), 4.6f, DetailShapes.Range(rnd, -5f, 6f));
                if (Mathf.Abs(a.x + 1.2f) < 1f && a.z > 4.5f) a.x += 2.5f; // nothing dangling over the stamp plate
                var b = a + new Vector3(DetailShapes.Range(rnd, -0.3f, 0.3f), -DetailShapes.Range(rnd, 1.2f, 2.2f), DetailShapes.Range(rnd, -0.3f, 0.3f));
                var path = StreamerPath(a, b, 0f, 0.07f, DetailShapes.Range(rnd, 5f, 8f), 30);
                DetailShapes.Ribbon(k.Of(m.Plume[(i + 3) % m.Plume.Length]), path, 0.024f, Vector3.forward, 3f);
            }
            // Confetti hanging in the air (clear of the plate, the seams and the inside of the float).
            for (int i = 0; i < 700; i++)
            {
                var p = new Vector3(DetailShapes.Range(rnd, -4.6f, 4.6f), DetailShapes.Range(rnd, 0.35f, 5.2f), DetailShapes.Range(rnd, -6.2f, 13.6f));
                if (!ClearOfPlay(p) || (Mathf.Abs(p.x) < 2.5f && p.z > 7.1f && p.z < 10.9f && p.y < 1.35f)) continue;
                var mat = i % 9 == 0 ? m.SequinGold : i % 11 == 0 ? m.Chrome : m.Plume[i % m.Plume.Length];
                k.Box(mat, p, new Vector3(0.036f, 0.025f, 0.0025f), Quaternion.Euler(DetailShapes.Range(rnd, 0f, 360f), DetailShapes.Range(rnd, 0f, 360f), DetailShapes.Range(rnd, 0f, 360f)));
            }
        }

        /// <summary>False inside the spaces that must stay uncluttered: over the stamp plate and its rhythm ring, and in
        /// front of the stand and arrival seams.</summary>
        static bool ClearOfPlay(Vector3 p)
        {
            if (p.x > -2.2f && p.x < -0.2f && p.z > 5.2f && p.z < 7.9f && p.y < 2.5f) return false;
            if (p.x > 3.2f && p.z > 1.6f && p.z < 4.4f && p.y < 2.6f) return false;
            if (Mathf.Abs(p.x) < 0.9f && p.z < -4.8f && p.y < 2.6f) return false;
            return true;
        }

        /// <summary>False in front of the sun mask and around the drum and its mallet: the float's two faces stay clean.</summary>
        static bool ClearOfStage(Vector3 p)
        {
            if (Mathf.Abs(p.x) < 1.5f && p.y > 1.9f && p.y < 4.8f && p.z > 7.0f && p.z < 10.5f) return false;
            if (p.x > 0.4f && p.x < 2.0f && p.y > 1.1f && p.y < 2.8f && p.z > 7.0f && p.z < 9.3f) return false;
            return true;
        }

        /// <summary>A sagging cord with coloured bulbs (and optionally pennants between them).</summary>
        static void StringLights(KitBatch k, CarnivalMats m, Vector3 a, Vector3 b, float sag, bool pennants, int colour)
        {
            Vector3 P(float t) => Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
            const int segs = 20;
            for (int i = 0; i < segs; i++)
            {
                var p0 = P(i / (float)segs); var p1 = P((i + 1) / (float)segs);
                k.Box(m.DarkMetal, (p0 + p1) * 0.5f, new Vector3(0.012f, 0.012f, Vector3.Distance(p0, p1)), Quaternion.LookRotation((p1 - p0).normalized, Vector3.up));
            }
            int n = Mathf.RoundToInt(Vector3.Distance(a, b) / 0.45f);
            var along = (b - a).normalized;
            var side = Vector3.Cross(Vector3.up, along).normalized;
            for (int i = 1; i < n; i++)
            {
                var p = P(i / (float)n);
                k.Of(m.Bulbs[(i + colour) % m.Bulbs.Length]).AddSphere(p + Vector3.down * 0.06f, 0.042f, 6, 4);
                if (!pennants || i % 2 == 0) continue;
                var q = P((i + 0.5f) / n);
                var l = q - along * 0.12f; var r = q + along * 0.12f; var tip = q + Vector3.down * 0.3f + side * 0.02f;
                var b2 = k.Of(m.Plume[(i / 2 + colour) % m.Plume.Length]);
                b2.AddTriangle(l, r, tip, Vector2.zero, Vector2.right, new Vector2(0.5f, 1f));
                b2.AddTriangle(r, l, tip, Vector2.zero, Vector2.right, new Vector2(0.5f, 1f));
            }
        }

        /// <summary>A thrown streamer: a parabola from a to b with a coil that opens along the flight.</summary>
        static List<Vector3> StreamerPath(Vector3 a, Vector3 b, float arc, float coil, float turns, int n)
        {
            var pts = new List<Vector3>(n);
            var d = b - a;
            var fwd = d.normalized;
            var side = Vector3.Cross(Vector3.up, fwd);
            if (side.sqrMagnitude < 0.01f) side = Vector3.right;
            side.Normalize();
            var up2 = Vector3.Cross(fwd, side).normalized;
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                var p = a + d * t + Vector3.up * (4f * arc * t * (1f - t));
                float ang = t * turns * Mathf.PI * 2f, rr = coil * Mathf.SmoothStep(0.2f, 1f, t);
                pts.Add(p + (side * Mathf.Cos(ang) + up2 * Mathf.Sin(ang)) * rr);
            }
            return pts;
        }
    }
}

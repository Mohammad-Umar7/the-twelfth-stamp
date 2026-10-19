using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    public partial class RioCell
    {
        /// <summary>
        /// Above and behind the grandstands: coconut palms, and pastel apartment blocks with continuous balconies, lit
        /// and dark windows, air-conditioner boxes, flags and cloths hung over the parapets, water tanks on the roofs.
        /// Outside the playable bounds, no colliders.
        /// </summary>
        void BuildSurroundings(KitBatch k, CarnivalMats m)
        {
            var rnd = new System.Random(1964);
            var trunk = m.Wood;
            var frond = Mats.Lit(new Color(0.16f, 0.36f, 0.16f), 0.3f, 0f, null, false, new Color(0.01f, 0.025f, 0.01f));
            var nut = m.Wood;
            foreach (float sx in new[] { -1f, 1f })
                foreach (var (z, h) in new[] { (-4.2f, 9.6f), (4.6f, 10.8f), (12.8f, 9.9f) })
                    Palm(k, trunk, frond, nut, new Vector3(sx * 10.0f, 0f, z + (sx > 0 ? 1.1f : 0f)), h + (sx > 0 ? -0.6f : 0f), rnd);

            var plasters = new[]
            {
                Mats.Lit(new Color(0.93f, 0.6f, 0.5f), 0.25f, 0f, "plaster_warm"), Mats.Lit(new Color(0.95f, 0.84f, 0.55f), 0.25f, 0f, "plaster_warm"),
                Mats.Lit(new Color(0.6f, 0.82f, 0.8f), 0.25f, 0f, "plaster_warm"),
            };
            var trim = Mats.Lit(new Color(0.92f, 0.9f, 0.86f), 0.3f, 0f, "plaster_warm");
            var glassDark = Mats.Lit(new Color(0.03f, 0.035f, 0.05f), 0.92f, 0.3f);
            var glassLit = new[]
            {
                Mats.Emissive(new Color(0.9f, 0.65f, 0.4f), new Color(1.0f, 0.6f, 0.26f)), Mats.Emissive(new Color(0.4f, 0.55f, 0.9f), new Color(0.26f, 0.44f, 1.0f)),
            };
            var ac = trim;
            var tank = m.DarkMetal;
            int p = 0;
            foreach (float sx in new[] { -1f, 1f })
                foreach (var (zc, floors) in new[] { (-3.6f, 7), (4.6f, 8), (12.6f, 6) })
                    Apartment(k, m, plasters[(p++ * 2 + (sx > 0 ? 1 : 0)) % plasters.Length], trim, glassDark, glassLit, ac, tank, sx, zc + (sx > 0 ? 0.8f : 0f), 7.6f, floors + (sx > 0 ? -1 : 0), rnd);
        }

        /// <summary>A coconut palm: a leaning ringed trunk and a crown of arching, drooping fronds (two leaflet rows each).</summary>
        static void Palm(KitBatch k, Material trunk, Material frond, Material nut, Vector3 b, float h, System.Random rnd)
        {
            var path = new List<Vector3>();
            float lean = DetailShapes.Range(rnd, 0.05f, 0.12f) * h, a = DetailShapes.Range(rnd, 0f, Mathf.PI * 2f);
            var leanDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                path.Add(b + Vector3.up * (h * t) + leanDir * (lean * t * t));
            }
            k.Of(trunk).AddTube(path.GetRange(0, 6), 0.2f, 8);
            k.Of(trunk).AddTube(path.GetRange(5, 6), 0.15f, 8);
            for (int i = 1; i < 10; i++) k.Of(trunk).AddTorus(path[i], i < 5 ? 0.2f : 0.15f, 0.025f, 8, 3, Quaternion.identity);
            var top = path[10];
            for (int f = 0; f < 13; f++)
            {
                float yaw = f / 13f * Mathf.PI * 2f + DetailShapes.Range(rnd, -0.15f, 0.15f);
                float elev = Mathf.Lerp(40f, -12f, (f % 3) / 2f) * Mathf.Deg2Rad;
                var hor = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                var dir = (hor * Mathf.Cos(elev) + Vector3.up * Mathf.Sin(elev)).normalized;
                var side = Vector3.Cross(Vector3.up, hor).normalized;
                float len = DetailShapes.Range(rnd, 2.5f, 3.1f);
                foreach (float s in new[] { -1f, 1f })
                    DetailShapes.Feather(k.Of(frond), top, dir, (Vector3.up + side * (0.9f * s)).normalized, len, 0.62f, Vector3.down, 0.5f, 6, 0.3f);
            }
            for (int i = 0; i < 4; i++)
            {
                float t = i / 4f * Mathf.PI * 2f + 0.4f;
                k.Of(nut).AddSphere(top + new Vector3(Mathf.Cos(t) * 0.16f, -0.18f, Mathf.Sin(t) * 0.16f), 0.1f, 7, 5);
            }
        }

        /// <summary>A pastel apartment block facing the avenue (front at |x| = 12.6) with continuous balconies.</summary>
        static void Apartment(KitBatch k, CarnivalMats m, Material plaster, Material trim, Material glassDark, Material[] glassLit, Material ac, Material tank,
            float sx, float zc, float width, int floors, System.Random rnd)
        {
            const float floorH = 2.9f, baseH = 3.4f, depth = 3f;
            float face = sx * 12.6f, H = baseH + floors * floorH;
            k.Box(plaster, new Vector3(sx * (12.6f + depth * 0.5f), H * 0.5f, zc), new Vector3(depth, H, width), 3f);
            int bays = Mathf.Max(2, Mathf.RoundToInt(width / 1.9f));
            float bw = width / bays;
            for (int f = 0; f < floors; f++)
            {
                float y = baseH + f * floorH;
                k.Box(trim, new Vector3(face - sx * 0.45f, y + 0.05f, zc), new Vector3(0.9f, 0.12f, width + 0.1f));      // balcony slab
                k.Box(trim, new Vector3(face - sx * 0.87f, y + 0.55f, zc), new Vector3(0.06f, 0.9f, width + 0.1f));     // parapet
                k.Box(m.DarkMetal, new Vector3(face - sx * 0.88f, y + 1.04f, zc), new Vector3(0.05f, 0.04f, width + 0.1f)); // rail
                for (int bi = 0; bi < bays; bi++)
                {
                    float z = zc - width * 0.5f + (bi + 0.5f) * bw;
                    var glass = rnd.NextDouble() < 0.36 ? glassLit[rnd.Next(glassLit.Length)] : glassDark;
                    k.Box(glass, new Vector3(face - sx * 0.01f, y + 1.35f, z), new Vector3(0.02f, 1.6f, 1.1f));
                    k.Box(trim, new Vector3(face - sx * 0.03f, y + 2.19f, z), new Vector3(0.05f, 0.08f, 1.2f));
                    k.Box(trim, new Vector3(face - sx * 0.03f, y + 1.35f, z), new Vector3(0.04f, 1.6f, 0.05f));
                    if (rnd.NextDouble() < 0.3)
                        k.Box(ac, new Vector3(face - sx * 0.2f, y + 2.42f, z + bw * 0.36f), new Vector3(0.36f, 0.36f, 0.55f));
                    if (rnd.NextDouble() < 0.26) // a flag or cloth hung over the parapet
                        k.Box(m.Plume[rnd.Next(m.Plume.Length)], new Vector3(face - sx * 0.92f, y + 0.5f, z), new Vector3(0.015f, 1.0f, 0.85f), Quaternion.Euler(DetailShapes.Range(rnd, -3f, 3f), 0f, sx * DetailShapes.Range(rnd, 2f, 6f)));
                }
            }
            k.Box(trim, new Vector3(sx * (12.6f + depth * 0.5f), H + 0.45f, zc), new Vector3(depth + 0.1f, 0.9f, width + 0.1f));
            var tp = new Vector3(sx * (13.4f + DetailShapes.Range(rnd, 0f, 0.8f)), H + 0.9f, zc + DetailShapes.Range(rnd, -2f, 2f));
            k.Of(tank).AddCylinder(tp, 0.6f, 1.1f, 12);
            Limb(k.Of(m.DarkMetal), tp + new Vector3(0.9f, 0f, 1.2f), tp + new Vector3(0.9f, 2.4f, 1.2f), 0.02f, 0.015f, 4);
        }

        /// <summary>
        /// What the carnival dropped: confetti and flat streamers on the paving, fallen feathers, a tambourine, a mask,
        /// and two cups frozen mid-tumble with their drinks hanging in the air.
        /// </summary>
        void BuildLitter(KitBatch k, CarnivalMats m)
        {
            var rnd = new System.Random(55);
            for (int i = 0; i < 460; i++)
            {
                var p = new Vector3(DetailShapes.Range(rnd, -4.9f, 4.9f), 0.0088f, DetailShapes.Range(rnd, -6.9f, 14.8f));
                if (Mathf.Abs(p.x) < 2.45f && p.z > 7.15f && p.z < 10.85f) continue; // under the float
                var mat = i % 13 == 0 ? m.SequinGold : m.Plume[i % m.Plume.Length];
                k.Box(mat, p, new Vector3(0.036f, 0.002f, 0.025f), Quaternion.Euler(0f, DetailShapes.Range(rnd, 0f, 180f), 0f));
            }
            for (int i = 0; i < 10; i++)
            {
                var a = new Vector3(DetailShapes.Range(rnd, -4.5f, 4.5f), 0.0105f, DetailShapes.Range(rnd, -6f, 13.5f));
                if (Mathf.Abs(a.x) < 2.6f && a.z > 6.8f && a.z < 11f) a.z -= 5f;
                float dir = DetailShapes.Range(rnd, 0f, Mathf.PI * 2f), len = DetailShapes.Range(rnd, 1.2f, 2.6f);
                var path = new List<Vector3>();
                for (int j = 0; j < 30; j++)
                {
                    float t = j / 29f, ang = dir + Mathf.Sin(t * 9f + i) * 0.9f;
                    var step = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (len / 29f);
                    path.Add(j == 0 ? a : path[j - 1] + step);
                }
                DetailShapes.Ribbon(k.Of(m.Plume[(i + 1) % m.Plume.Length]), path, 0.026f, Vector3.forward, 0f);
            }
            for (int i = 0; i < 14; i++)
            {
                var p = new Vector3(DetailShapes.Range(rnd, -4.4f, 4.4f), 0.013f, DetailShapes.Range(rnd, -5.5f, 13.5f));
                if (Mathf.Abs(p.x) < 2.6f && p.z > 6.8f && p.z < 11f) continue;
                float a = DetailShapes.Range(rnd, 0f, Mathf.PI * 2f);
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                DetailShapes.Feather(k.Of(m.Plume[i % 4 == 3 ? 3 : i % m.Plume.Length]), p, dir, Vector3.up, DetailShapes.Range(rnd, 0.5f, 0.9f), 0.13f, Vector3.Cross(Vector3.up, dir), 0.15f, 5);
            }
            // A tambourine and a carnival mask left on the paving.
            var pan = new Vector3(2.4f, 0f, -0.6f);
            k.Of(m.Wood).AddTorus(pan + Vector3.up * 0.025f, 0.12f, 0.025f, 18, 5, Quaternion.identity);
            k.Of(m.DrumHead).AddCylinder(pan + Vector3.up * 0.04f, 0.113f, 0.004f, 18);
            for (int i = 0; i < 5; i++)
            {
                var d = Quaternion.Euler(0f, i * 72f, 0f) * Vector3.right;
                foreach (float y in new[] { 0.012f, 0.03f }) k.Of(m.Chrome).AddCylinder(pan + d * 0.12f + Vector3.up * y, 0.024f, 0.006f, 10);
            }
            var mask = new Vector3(-2.5f, 0.015f, 1.9f);
            var mr = Quaternion.Euler(0f, 35f, 0f);
            k.Box(m.SequinGold, mask, new Vector3(0.18f, 0.014f, 0.075f), mr);
            foreach (float sx in new[] { -1f, 1f }) k.Of(m.DarkMetal).AddDisc(mask + mr * new Vector3(sx * 0.045f, 0.0085f, 0.005f), Vector3.up, 0.02f, 10);
            for (int i = 0; i < 3; i++)
                DetailShapes.Feather(k.Of(m.Plume[i == 1 ? 3 : 0]), mask + mr * new Vector3(0.085f, 0.006f, 0f), mr * Quaternion.Euler(0f, -20f + i * 20f, 0f) * Vector3.right, Vector3.up, 0.3f, 0.06f, Vector3.zero, 0f, 4);
            // Two plastic cups frozen mid-tumble, their drinks hanging in the air.
            var drink = Mats.Glass(new Color(1f, 0.72f, 0.22f, 0.55f), 0.95f, new Color(0.22f, 0.12f, 0.02f));
            foreach (var (pos, euler, throwDir) in new[]
            {
                (new Vector3(-1.9f, 1.35f, -1.7f), new Vector3(0f, 30f, 75f), new Vector3(0.6f, 0f, 0.3f)),
                (new Vector3(2.1f, 1.0f, 1.2f), new Vector3(20f, -40f, -110f), new Vector3(-0.5f, 0f, -0.4f)),
            })
            {
                var q = Quaternion.Euler(euler);
                k.Of(m.DrumHead).AddFrustum(pos, 0.032f, 0.043f, 0.11f, 10, true, false, 1f, q);
                var mouth = pos + q * Vector3.up * 0.11f;
                var db = k.Of(drink);
                for (int i = 0; i < 16; i++)
                {
                    float t = i / 15f;
                    var dp = mouth + throwDir * (0.55f * t) + Vector3.up * (0.12f * t - 0.5f * t * t) + new Vector3(DetailShapes.Range(rnd, -0.03f, 0.03f), DetailShapes.Range(rnd, -0.03f, 0.03f), DetailShapes.Range(rnd, -0.03f, 0.03f));
                    db.AddSphere(dp, Mathf.Lerp(0.028f, 0.01f, t), 6, 4);
                }
            }
        }
    }
}

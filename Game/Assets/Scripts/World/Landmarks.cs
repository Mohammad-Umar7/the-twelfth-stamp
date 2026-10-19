using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Distant landmarks: one recognisable silhouette per country (never mixed), 60–230 m from the player. Each uses its
    /// Blender model when the file is present, otherwise the procedural shapes at the end of this file, and stands in a
    /// vista built for its cell's view (<see cref="Vista"/>): ground, streets, trees, lamps and water, lit for the cell's
    /// time of day, under a gentler haze than the cell's fog, so the place still reads at that distance and the landmark
    /// never floats. Each vista keeps clear of the playable area and of the cell's own near scenery.
    /// </summary>
    public static class Landmarks
    {
        /// <summary>Ground each landmark's site claims, per cell group: (x, z, radius). Skylines built later keep off it.</summary>
        static readonly Dictionary<Transform, List<Vector3>> s_Sites = new Dictionary<Transform, List<Vector3>>();

        static void ClaimSite(Transform parent, Vector3 center, float radius)
        {
            if (!s_Sites.TryGetValue(parent, out var list)) s_Sites[parent] = list = new List<Vector3>();
            list.Add(new Vector3(center.x, center.z, radius));
        }

        static bool OnSite(Transform parent, Vector3 p, float margin)
        {
            if (!s_Sites.TryGetValue(parent, out var list)) return false;
            foreach (var s in list) if (new Vector2(p.x - s.x, p.z - s.y).magnitude < s.z + margin) return true;
            return false;
        }

        /// <summary>
        /// Where a country's stamp can hang near its landmark: on the vista's walkable ground, between the landmark and the
        /// player's spot, about 1.5 m up. False when the cell has no landmark, or until the cell has opened its play area to
        /// the site (<see cref="MarkReachable"/>), so a stamp is never placed where the player cannot get to it.
        /// </summary>
        public static bool TryStampSpot(Cell cell, out Vector3 worldPos)
        {
            worldPos = default;
            if (cell == null) return false;
            foreach (var info in cell.GetComponentsInChildren<VistaInfo>(true))
            {
                if (!info.HasStampSpot || !info.Reachable) continue;
                var space = info.transform.parent != null ? info.transform.parent : cell.transform;
                worldPos = space.TransformPoint(info.StampLocal);
                return true;
            }
            return false;
        }

        /// <summary>The cell has opened its play area (bounds, paths, bridges) to its landmark's site, or closed it again.</summary>
        public static void MarkReachable(Cell cell, bool reachable = true)
        {
            if (cell == null) return;
            foreach (var info in cell.GetComponentsInChildren<VistaInfo>(true)) if (info.HasStampSpot) info.Reachable = reachable;
        }

        static GameObject Root(Transform parent, string name, Vector3 pos, float yaw, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        static void NoShadows(GameObject go) => MeshFactory.NoShadows(go);

        /// <summary>Slot map for a landmark model: the listed TS_* slots, every other slot on <paramref name="main"/>.</summary>
        static System.Func<string, Material> Palette(Material main, params (string slot, Material mat)[] map) => name =>
        {
            var id = Models.SlotOf(name);
            foreach (var (s, m) in map) if (s == id) return m;
            return main;
        };

        /// <summary>A point in the root's own frame (model coordinates), in cell coordinates.</summary>
        static Vector3 OnRoot(GameObject root, Vector3 local)
        {
            var t = root.transform;
            return t.localPosition + t.localRotation * Vector3.Scale(t.localScale, local);
        }

        /// <summary>Night surroundings: the cell's moonlight, a brighter sky ambient and a warm bounce from street lights.</summary>
        static Vista.Rig Night(Vista v, Color bounce)
        {
            var r = v.CellLight(1f, 2.4f);
            r.Ground = bounce;
            return r;
        }

        /// <summary>
        /// Night foliage and roofs: a strong blue moonlit sky ambient and a warm bounce (colours are set as sRGB and become
        /// linear in the shader, so a dim ambient would leave dark albedos black), so dark shapes keep their form.
        /// </summary>
        static Vista.Rig Moonlit(Vista v, Color bounce)
        {
            var r = v.CellLight(1.5f, 1f);
            r.Sky = new Color(1.1f, 1.2f, 1.5f);
            r.Ground = bounce * 1.6f;
            return r;
        }

        static List<Vector2> Ellipse(Vector3 c, Vector3 u, float ru, float rv, int n, float wobble, int seed)
        {
            var w = Vector3.Cross(Vector3.up, u);
            var rnd = new System.Random(seed);
            var pts = new List<Vector2>();
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n * Mathf.PI * 2f;
                float s = 1f + wobble * ((float)rnd.NextDouble() - 0.5f);
                var p = c + (u * Mathf.Cos(t) * ru + w * Mathf.Sin(t) * rv) * s;
                pts.Add(new Vector2(p.x, p.z));
            }
            return pts;
        }

        /// <summary>A shadowed arch opening: a half disc standing on <paramref name="c"/>, facing <paramref name="normal"/> (vista coordinates).</summary>
        static void ArchOpening(MeshBuilder b, Vector3 c, Vector3 normal, float r, int segs = 12)
        {
            var right = Vector3.Cross(Vector3.up, normal);
            for (int i = 0; i < segs; i++)
            {
                float t0 = Mathf.PI * i / segs, t1 = Mathf.PI * (i + 1) / segs;
                var p0 = c + (right * Mathf.Cos(t0) + Vector3.up * Mathf.Sin(t0)) * r;
                var p1 = c + (right * Mathf.Cos(t1) + Vector3.up * Mathf.Sin(t1)) * r;
                if (Vector3.Dot(Vector3.Cross(p0 - c, p1 - c), normal) >= 0f) b.AddTriangle(c, p0, p1, Vector2.zero, Vector2.right, Vector2.up);
                else b.AddTriangle(c, p1, p0, Vector2.zero, Vector2.up, Vector2.right);
            }
        }

        /// <summary>An arch ring (half annulus) on a bridge face, centred on <paramref name="c"/> (vista coordinates).</summary>
        static void ArchRing(MeshBuilder b, Vector3 c, float r0, float r1, Vector3 normal, int segs = 14)
        {
            var right = Vector3.Cross(Vector3.up, normal);
            for (int i = 0; i < segs; i++)
            {
                float t0 = Mathf.PI * i / segs, t1 = Mathf.PI * (i + 1) / segs;
                Vector3 d0 = right * Mathf.Cos(t0) + Vector3.up * Mathf.Sin(t0), d1 = right * Mathf.Cos(t1) + Vector3.up * Mathf.Sin(t1);
                Vector3 a = c + d0 * r0, q = c + d0 * r1, e = c + d1 * r1, d = c + d1 * r0;
                if (Vector3.Dot(Vector3.Cross(q - a, d - a), normal) < 0f) { var tmp = q; q = d; d = tmp; }
                b.AddQuad(a, q, e, d, Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            }
        }

        /// <summary>Onion dome profile (lathe: x = radius, y = height) on a base of radius ~0.85 r.</summary>
        static List<Vector2> Onion(float r) => new List<Vector2>
        {
            new Vector2(r * 0.85f, 0f), new Vector2(r * 1.04f, r * 0.38f), new Vector2(r * 0.98f, r * 0.78f), new Vector2(r * 0.7f, r * 1.12f),
            new Vector2(r * 0.26f, r * 1.45f), new Vector2(0.1f, r * 1.75f), new Vector2(0.04f, r * 2.05f),
        };

        /// <summary>Low blocks on a jittered grid inside a rectangle, with a facade texture and, at night, lit windows.</summary>
        static void Blocks(Vista v, float x0, float z0, float x1, float z1, float y, float cell, float hMin, float hMax, Color[] tints,
            float lights, float roofH, System.Random rnd, System.Func<Vector3, float, bool> skip)
        {
            for (float x = x0 + cell / 2f; x < x1; x += cell)
                for (float z = z0 + cell / 2f; z < z1; z += cell)
                {
                    float w = cell * (0.55f + 0.3f * (float)rnd.NextDouble()), d = cell * (0.55f + 0.3f * (float)rnd.NextDouble());
                    var p = new Vector3(x + ((float)rnd.NextDouble() - 0.5f) * (cell - w) * 0.8f, y, z + ((float)rnd.NextDouble() - 0.5f) * (cell - d) * 0.8f);
                    float h = Mathf.Round(Mathf.Lerp(hMin, hMax, (float)rnd.NextDouble()) / 3.2f) * 3.2f + 0.8f;
                    var tint = tints[rnd.Next(tints.Length)];
                    if (skip != null && skip(p, Mathf.Max(w, d) * 0.5f)) continue;
                    v.Building(v.At(p + Vector3.up * h * 0.5f, tint, default, VistaTex.Facade), v.At(p + Vector3.up * h, tint * 0.7f), p, w, d, h, 0f, 25.6f,
                        roofH, Mathf.Min(w, d) * 0.3f, lights);
                }
        }

        // ================================================================== Paris
        /// <summary>Lattice tower (Paris), ~100 m * scale, floodlit gold in its Seine-side park at night.</summary>
        public static GameObject LatticeTower(Transform parent, Vector3 pos, float scale, Material mat, Material lightMat = null)
        {
            var go = Root(parent, "EiffelSilhouette", pos, 0, scale);
            var v = Vista.ForLandmark(go, mat, 0.4f);
            float k = v.FogAt(90f * scale);
            var flood = v.Flood(new Color(1f, 0.74f, 0.42f) * 0.9f, 32f, 24f, new Color(0.14f, 0.15f, 0.2f), new Color(0.4f, 0.26f, 0.12f));
            var iron = v.Mat(new Color(0.34f, 0.23f, 0.13f), flood, k, new Color(0.72f, 0.42f, 0.13f));
            var decks = v.Mat(new Color(0.45f, 0.34f, 0.2f), flood, k, new Color(0.95f, 0.6f, 0.2f));
            var beacon = v.Mat(Color.black, Vista.Even(0f), k, new Color(2.6f, 2.2f, 1.6f));
            if (Models.Spawn("Landmark_Eiffel", go.transform, Palette(iron, ("TS_Metal", decks), ("TS_EmissiveWarm", beacon))) == null)
                EiffelShapes(go, iron, beacon);
            v.Halo(OnRoot(go, new Vector3(0f, 99f, 0f)), 6f, new Color(1f, 0.85f, 0.6f));
            ParisSite(v);
            ClaimSite(parent, pos, 60f);
            v.StampSpot(new Vector3(98f, 4.6f + 1.5f, 138f)); // the garden by the bridge landing
            v.Finish(4.6f);
            NoShadows(go);
            return go;
        }

        /// <summary>
        /// The Seine's east bank and the Champ de Mars. The cell's river (x 4–56, z −35..55) runs on north as vista water
        /// between stone quays whose tops (y 4.6) meet the cell's footbridge deck at x 54. Haussmann blocks line the quay south
        /// of the sight line; north of it come plane trees, the Pont d'Iéna (z 150) and the tower in its park, with lit blocks
        /// round the Champ de Mars behind.
        /// </summary>
        static void ParisSite(Vista v)
        {
            const float Top = 4.6f, Water = -1.55f;
            v.Context = Night(v, new Color(0.3f, 0.21f, 0.12f));
            var warm = new Color(1f, 0.76f, 0.46f);
            var rnd = new System.Random(75);
            var limestone = Tex.Albedo("limestone_blocks");
            var cobble = Tex.Albedo("cobble_wet");
            var grass = Tex.Albedo("grass_short");

            v.GroundRect(v.Mat(v.FogColor * 0.8f, Vista.Even(1f), v.FogK(170f)), 3f, 54f, 55f, 330f, Water, 10f);
            float qh = Top - Water + 0.5f, qy = Water - 0.5f + qh / 2f;
            var quay = new Color(0.5f, 0.47f, 0.42f);
            v.Box(v.At(new Vector3(56f, 1f, 20f), quay, default, limestone), new Vector3(55.5f, qy, 15f), new Vector3(3f, qh, 110f), 0f, 3f);
            v.Box(v.At(new Vector3(56f, 1f, 200f), quay, default, limestone), new Vector3(55.5f, qy, 215f), new Vector3(3f, qh, 290f), 0f, 3f);
            v.Box(v.At(new Vector3(2f, 1f, 190f), quay, default, limestone), new Vector3(2.5f, qy, 190f), new Vector3(3f, qh, 270f), 0f, 3f);

            var paving = new Color(0.27f, 0.27f, 0.29f);
            v.GroundRect(v.At(new Vector3(85f, Top, 30f), paving, default, cobble), 57f, -60f, 110f, 110f, Top, 4f);
            v.GroundRect(v.At(new Vector3(85f, Top, 200f), paving, default, cobble), 57f, 110f, 110f, 330f, Top, 4f);
            v.GroundRect(v.At(new Vector3(200f, Top, 150f), paving, default, cobble), 110f, -60f, 330f, 330f, Top, 4f);
            var lawn = new Color(0.12f, 0.16f, 0.1f);
            var walk = new Color(0.3f, 0.28f, 0.25f);
            v.GroundRect(v.At(new Vector3(215f, Top, 135f), lawn, default, grass), 142f, 128f, 300f, 145f, Top + 0.08f, 4f);
            v.GroundRect(v.At(new Vector3(215f, Top, 165f), lawn, default, grass), 142f, 155f, 300f, 172f, Top + 0.08f, 4f);
            v.GroundRect(v.At(new Vector3(215f, Top, 150f), walk), 140f, 145f, 300f, 155f, Top + 0.06f, 4f);
            v.GroundRect(v.At(new Vector3(82f, Top, 120f), lawn, default, grass), 63f, 100f, 102f, 140f, Top + 0.08f, 4f);
            v.GroundRect(v.At(new Vector3(82f, Top, 180f), lawn, default, grass), 63f, 160f, 102f, 200f, Top + 0.08f, 4f);
            v.Pool(new Vector3(120f, Top, 150f), 46f, new Color(0.5f, 0.32f, 0.12f));

            // Haussmann blocks: limestone, six storeys, zinc mansards, lit windows.
            var zinc = new Color(0.2f, 0.22f, 0.26f);
            var stone = new[] { new Color(0.72f, 0.67f, 0.58f), new Color(0.66f, 0.62f, 0.55f), new Color(0.78f, 0.72f, 0.62f) };
            void Block(Vector3 at, float w, float d)
            {
                float h = (5 + rnd.Next(2)) * 3.2f + 1f;
                var tint = stone[rnd.Next(stone.Length)];
                v.Building(v.At(at + Vector3.up * h * 0.5f, tint, default, VistaTex.Facade), v.At(at + Vector3.up * h, zinc), at, w, d, h, 0f, 25.6f, 4.2f, 2.4f, 0.85f);
            }
            for (float z = -58f; z < 56f;)
            {
                float len = Mathf.Min(22f + (float)rnd.NextDouble() * 14f, 62f - z);
                if (len > 8f) Block(new Vector3(71f, Top, z + len / 2f), 26f, len);
                z += len + 9f;
            }
            for (float z = 214f; z < 300f;)
            {
                float len = 22f + (float)rnd.NextDouble() * 14f;
                Block(new Vector3(71f, Top, z + len / 2f), 26f, len);
                z += len + 9f;
            }
            for (float x = 142f; x < 292f; x += 34f)
            {
                Block(new Vector3(x + 13f, Top, 100f), 26f, 24f);
                if (x < 236f) Block(new Vector3(x + 13f, Top, 200f), 26f, 24f);
            }

            // Plane trees on the quay, rows down the Champ de Mars, clumps round the tower's feet clear of the view to it.
            var leaf = new Color(0.16f, 0.22f, 0.14f);
            var bark = new Color(0.16f, 0.13f, 0.11f);
            var moon = Moonlit(v, new Color(0.3f, 0.21f, 0.12f));
            int seed = 1;
            void Plane(Vector3 p, float h, float w) => v.Tree(v.Mat(leaf, moon, v.FogK(p + Vector3.up * h * 0.6f)), v.Mat(bark, moon, v.FogK(p)), p, h, w, seed++);
            for (float z = 66f; z < 305f; z += 10f) if (z < 138f || z > 162f) Plane(new Vector3(61f, Top, z), 11f, 8f);
            foreach (float row in new[] { 119f, 126f, 174f, 181f })
                for (float x = 142f; x < 300f; x += 9f) Plane(new Vector3(x, Top, row), 10f, 7f);
            for (int i = 0; i < 30; i++)
            {
                var p = new Vector3(66f + (float)rnd.NextDouble() * 44f, Top, 98f + (float)rnd.NextDouble() * 104f);
                float h = 9f + (float)rnd.NextDouble() * 3f;
                if (Mathf.Abs(p.z - 150f) < 13f || v.FromSightLine(p) < 14f || (Mathf.Abs(p.x - 120f) < 24f && Mathf.Abs(p.z - 150f) < 24f)) continue;
                Plane(p, h, 7f);
            }

            // Lamps along both quays, their reflections on the river; lamps down the Champ de Mars.
            var iron = v.At(new Vector3(57f, 3f, 120f), new Color(0.05f, 0.055f, 0.06f));
            for (float z = -50f; z < 305f; z += 13f)
            {
                if (z > 140f && z < 160f) continue;
                v.Lamp(iron, new Vector3(57.4f, Top, z), 4.2f, warm, 2.6f);
                v.Pool(new Vector3(58.5f, Top, z), 5f, warm * 0.3f);
                v.Streak(new Vector3(52.5f, Water + 0.03f, z), 1.3f, 16f, warm * 0.5f);
            }
            for (float z = 62f; z < 305f; z += 13f)
            {
                v.Lamp(iron, new Vector3(3.2f, Top, z), 4.2f, warm, 2.6f);
                v.Streak(new Vector3(8f, Water + 0.03f, z), 1.3f, 16f, warm * 0.45f);
            }
            for (float x = 146f; x < 300f; x += 16f)
                foreach (float z in new[] { 144f, 156f })
                {
                    v.Lamp(iron, new Vector3(x, Top, z), 4f, warm, 2.2f);
                    v.Pool(new Vector3(x, Top, z), 4.5f, warm * 0.25f);
                }
            for (float z = 104f; z < 200f; z += 16f)
                foreach (float x in new[] { 66f, 104f }) if (Mathf.Abs(z - 150f) > 12f) v.Pool(new Vector3(x, Top, z), 6f, warm * 0.18f);

            // Pont d'Iéna: a stone bridge of five arches from the west quay to the tower's forecourt.
            var bridge = v.At(new Vector3(29f, 2f, 150f), new Color(0.52f, 0.49f, 0.44f), default, limestone);
            v.Box(bridge, new Vector3(29f, (Top + Water) / 2f, 150f), new Vector3(52f, Top - Water, 13f), 0f, 3f);
            v.Box(bridge, new Vector3(29f, Top + 0.45f, 143.75f), new Vector3(52f, 0.9f, 0.5f), 0f, 2f);
            v.Box(bridge, new Vector3(29f, Top + 0.45f, 156.25f), new Vector3(52f, 0.9f, 0.5f), 0f, 2f);
            var shade = v.Mat(new Color(0.015f, 0.02f, 0.025f), Vista.Even(1f), v.FogK(new Vector3(29f, 0f, 150f)));
            for (int i = 0; i < 5; i++)
            {
                float x = 8.2f + i * 10.4f;
                ArchOpening(v.B(shade), v.L(new Vector3(x, Water, 143.45f)), Vector3.back, 4.1f);
                ArchOpening(v.B(shade), v.L(new Vector3(x, Water, 156.55f)), Vector3.forward, 4.1f);
            }
            for (float x = 6f; x < 54f; x += 8f)
                foreach (float z in new[] { 143.75f, 156.25f })
                {
                    v.Lamp(iron, new Vector3(x, Top + 0.9f, z), 3.4f, warm, 2.2f);
                    if (z < 150f) v.Streak(new Vector3(x, Water + 0.03f, 141f), 1.1f, 14f, warm * 0.4f);
                }

            foreach (float z in new[] { 100f, 165f, 235f }) v.Mist(new Vector3(29f, Water + 2.5f, z), 72f, 6f, 0.4f);
            v.Mist(new Vector3(120f, Top + 7f, 150f), 115f, 15f, 0.26f);
        }

        // ================================================================== New York
        public static GameObject Liberty(Transform parent, Vector3 pos, float yaw, Material mat, Material torch)
        {
            var go = Root(parent, "LibertySilhouette", pos, yaw, 1f);
            var v = Vista.ForLandmark(go, mat, 0.36f);
            float k = v.FogAt(93f);
            var flood = v.Flood(new Color(0.92f, 0.96f, 1f) * 0.85f, -28f, 30f, new Color(0.12f, 0.14f, 0.2f), new Color(0.3f, 0.3f, 0.26f));
            var copper = v.Mat(new Color(0.4f, 0.64f, 0.56f), flood, k, new Color(0.1f, 0.22f, 0.18f));
            var stone = v.Mat(new Color(0.62f, 0.57f, 0.5f), flood, k, new Color(0.11f, 0.09f, 0.07f));
            var panels = v.Mat(new Color(0.22f, 0.2f, 0.18f), flood, k, new Color(0.035f, 0.032f, 0.028f));
            var flame = v.Mat(Color.black, Vista.Even(0f), k, new Color(3f, 2f, 0.7f));
            // The model faces +Z, which the cell turns toward the player (its torch is in the right hand, at +X).
            bool model = Models.Spawn("Landmark_Liberty", go.transform, Palette(stone, ("TS_Metal", copper), ("TS_MetalDark", panels), ("TS_EmissiveWarm", flame)), Vector3.zero, 0f) != null;
            if (!model) LibertyShapes(go, copper, flame);
            v.Halo(OnRoot(go, model ? new Vector3(5.95f, 92.2f, -0.6f) : new Vector3(4.1f, 70f, 0f)), 7f, new Color(1f, 0.75f, 0.35f));
            NewYorkSite(v);
            ClaimSite(parent, pos, 72f);
            { var sp = v.Origin + v.ToViewer * 30f; sp.x += 0.0f; sp.y = -0.3f + 1.5f; v.StampSpot(sp); } // the island's front lawn
            v.Finish(-0.3f);
            NoShadows(go);
            return go;
        }

        /// <summary>
        /// Liberty Island in the harbour: a granite seawall and lamp-lit promenade, lawns, trees and two low buildings behind
        /// the statue; dark water to the horizon, and Manhattan's lit waterfront under the cell's skyline.
        /// </summary>
        static void NewYorkSite(Vista v)
        {
            const float Water = -1.6f, Isle = -0.3f;
            v.Context = Night(v, new Color(0.26f, 0.2f, 0.14f));
            var lamp = new Color(1f, 0.9f, 0.7f);
            var granite = Tex.Albedo("granite_pavers");
            v.GroundRect(v.Mat(v.FogColor * 0.85f, Vista.Even(1f), v.FogK(200f)), -60f, -330f, 330f, 330f, Water, 10f);

            var axis = v.ToViewer;
            var side = Vector3.Cross(Vector3.up, axis);
            var c = v.Origin - axis * 8f;
            c.y = Isle;
            const float Ra = 56f, Rb = 58f;
            v.Slab(v.At(c, new Color(0.34f, 0.33f, 0.32f), default, granite), Ellipse(c, axis, Ra, Rb, 30, 0.06f, 3), Isle, 3.5f, 4f);
            v.Slab(v.At(c, new Color(0.11f, 0.15f, 0.1f), default, Tex.Albedo("grass_short")), Ellipse(c, axis, Ra - 8f, Rb - 8f, 26, 0.04f, 4), Isle + 0.08f, 0.05f, 4f);
            var pole = v.At(c, new Color(0.05f, 0.05f, 0.06f));
            var leaf = new Color(0.14f, 0.2f, 0.13f);
            var moon = Moonlit(v, new Color(0.26f, 0.2f, 0.14f));
            for (int i = 0; i < 34; i++)
            {
                float t = i / 34f * Mathf.PI * 2f;
                var dir = axis * Mathf.Cos(t) * Ra + side * Mathf.Sin(t) * Rb;
                v.Lamp(pole, c + dir * 0.95f, 4f, lamp, 2.4f);
                v.Pool(c + dir * 0.93f, 4f, lamp * 0.25f);
                var w = c + dir * 1.05f;
                w.y = Water + 0.03f;
                v.Streak(w, 1.2f, 14f, lamp * 0.45f);
                if (i % 2 == 0 && Vector3.Dot(dir.normalized, axis) < 0.45f)
                {
                    var tp = c + dir * 0.8f;
                    v.Tree(v.Mat(leaf, moon, v.FogK(tp + Vector3.up * 6f)), v.Mat(new Color(0.15f, 0.13f, 0.11f), moon, v.FogK(tp)), tp, 10f, 7f, 300 + i);
                }
            }
            float yaw = Mathf.Atan2(axis.x, axis.z) * Mathf.Rad2Deg;
            foreach (float s in new[] { -1f, 1f })
            {
                var p = c - axis * 40f + side * s * 20f;
                v.Building(v.At(p, new Color(0.62f, 0.56f, 0.48f), default, VistaTex.Facade), v.At(p, new Color(0.18f, 0.18f, 0.2f)), p, 24f, 12f, 8.4f, yaw, 25.6f, 0f, 0f, 0.7f);
            }

            // Manhattan's waterfront under the skyline (bearings 26–95 degrees), with a string of lights along the water.
            var strip = new List<Vector2>();
            for (int i = 0; i <= 18; i++) { float a = Mathf.Lerp(26f, 95f, i / 18f) * Mathf.Deg2Rad; strip.Add(new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 200f); }
            for (int i = 18; i >= 0; i--) { float a = Mathf.Lerp(26f, 95f, i / 18f) * Mathf.Deg2Rad; strip.Add(new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 330f); }
            v.Slab(v.At(new Vector3(180f, 0f, 120f), new Color(0.1f, 0.1f, 0.11f), default, granite), strip, -0.4f, 2.5f, 6f);
            for (int i = 0; i <= 40; i++)
            {
                float a = Mathf.Lerp(26f, 95f, i / 40f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                v.Lamp(null, dir * 201f + Vector3.up * -0.4f, 5f, lamp, 2f);
                v.Streak(dir * 197f + Vector3.up * (Water + 0.03f), 1.4f, 18f, lamp * 0.4f);
            }
            v.Mist(c + side * 60f + Vector3.up * 1.5f, 120f, 8f, 0.3f);
            v.Mist(c - side * 60f + Vector3.up * 1.5f, 120f, 8f, 0.3f);
        }

        // ================================================================== Japan
        public static GameObject Pagoda(Transform parent, Vector3 pos, float yaw, Material mat, Material roof)
        {
            var go = Root(parent, "PagodaSilhouette", pos, yaw, 1f);
            var v = Vista.ForLandmark(go, mat, 0.3f);
            float k = v.FogAt(39f);
            var flood = v.Flood(new Color(1f, 0.7f, 0.42f) * 1.1f, 22f, 38f, new Color(0.06f, 0.06f, 0.1f), new Color(0.34f, 0.2f, 0.1f));
            var plaster = v.Mat(new Color(0.9f, 0.82f, 0.7f), flood, k, new Color(0.3f, 0.16f, 0.06f));
            var timber = v.Mat(new Color(0.66f, 0.18f, 0.08f), flood, k, new Color(0.2f, 0.05f, 0.02f));
            var brackets = v.Mat(new Color(0.16f, 0.09f, 0.06f), flood, k, new Color(0.04f, 0.02f, 0.01f));
            var tiles = v.Mat(new Color(0.15f, 0.15f, 0.17f), flood, k, new Color(0.015f, 0.015f, 0.02f));
            var bronze = v.Mat(new Color(0.8f, 0.6f, 0.3f), flood, k, new Color(0.45f, 0.3f, 0.1f));
            var stone = v.Mat(new Color(0.46f, 0.44f, 0.42f), flood, k, new Color(0.05f, 0.04f, 0.03f));
            // Default turn: the entry steps face the approach and the player.
            if (Models.Spawn("Landmark_Pagoda", go.transform, Palette(plaster, ("TS_Wood", timber), ("TS_WoodDark", brackets), ("TS_MetalDark", tiles), ("TS_Brass", bronze), ("TS_Stone", stone))) == null)
                PagodaShapes(go, plaster, tiles);
            JapanSite(v, flood);
            ClaimSite(parent, pos, 40f);
            { var sp = v.Origin + v.ToViewer * 16f; sp.x += 0.0f; sp.y = -1f + 1.5f; v.StampSpot(sp); } // the end of the lantern approach
            v.Finish(-1f);
            NoShadows(go);
            return go;
        }

        static void StoneLantern(Vista v, Vector3 at, Color light, float size)
        {
            var s = v.At(at, new Color(0.5f, 0.48f, 0.45f));
            v.Box(s, at + new Vector3(0f, 0.35f, 0f) * size, new Vector3(0.8f, 0.7f, 0.8f) * size);
            v.Box(s, at + new Vector3(0f, 1f, 0f) * size, new Vector3(0.32f, 0.7f, 0.32f) * size);
            v.Box(v.Glow(at, light * 1.4f), at + new Vector3(0f, 1.62f, 0f) * size, new Vector3(0.62f, 0.56f, 0.62f) * size);
            v.Box(s, at + new Vector3(0f, 2.02f, 0f) * size, new Vector3(1.1f, 0.24f, 1.1f) * size);
            v.Box(s, at + new Vector3(0f, 2.28f, 0f) * size, new Vector3(0.3f, 0.3f, 0.3f) * size);
            v.Halo(at + Vector3.up * 1.62f * size, 2.4f * size, light);
            v.Pool(at, 3.2f * size, light * 0.35f);
        }

        /// <summary>
        /// The temple precinct behind the street's end gate: gravel, a stone approach lined with glowing lanterns, a two-storey
        /// gate, white walls with tiled caps, the main hall with lit shoji, maples by the path and dark cedars all round. The
        /// precinct is laid out on the pagoda's sight line (d metres toward the player, s metres across it), so the lantern
        /// path leads the eye from the street straight to the pagoda wherever the cell puts it.
        /// </summary>
        static void JapanSite(Vista v, Vista.Rig flood)
        {
            const float G = -1f;
            var ax = v.ToViewer;
            var sd = Vector3.Cross(Vector3.up, ax);
            Vector3 A(float d, float s) => v.Origin + ax * d + sd * s;
            float frame = Mathf.Atan2(ax.x, ax.z) * Mathf.Rad2Deg + 180f; // local -Z faces the player
            var q = Quaternion.Euler(0f, frame, 0f);
            v.Context = Night(v, new Color(0.24f, 0.15f, 0.08f));
            var glow = new Color(1f, 0.64f, 0.3f);
            var rnd = new System.Random(19);
            var o = v.Origin;
            v.GroundRect(v.At(o + Vector3.forward * 10f, new Color(0.3f, 0.29f, 0.28f), default, Tex.Albedo("stone_dark_slate")), o.x - 110f, o.z - 80f, o.x + 110f, o.z + 180f, G, 5f);
            v.Ground(v.At(A(42f, 0f), new Color(0.44f, 0.42f, 0.4f), default, Tex.Albedo("granite_pavers")), A(42f, 0f) + Vector3.up * 0.06f, new Vector2(7f, 68f), frame, 3f);

            for (float d = 12f; d < 70f; d += 7f)
                foreach (float s in new[] { -5.5f, 5.5f }) StoneLantern(v, A(d, s), glow, 1f);
            foreach (var c in new[] { new Vector3(-9.5f, 0f, -9.5f), new Vector3(9.5f, 0f, -9.5f), new Vector3(-9.5f, 0f, 9.5f), new Vector3(9.5f, 0f, 9.5f) })
                StoneLantern(v, o + c, glow, 1.5f);

            // The outer gate in the front wall: one storey, so from the street it stays under the end gate's line and the
            // pagoda's upper tiers show above it (a two-storey gate here hid the whole pagoda).
            var gate = A(74f, 0f);
            var vermilion = v.Mat(new Color(0.58f, 0.15f, 0.07f), flood, v.FogK(gate + Vector3.up * 5f), new Color(0.12f, 0.03f, 0.012f));
            var gateTiles = v.Mat(new Color(0.24f, 0.24f, 0.27f), Moonlit(v, new Color(0.3f, 0.18f, 0.09f)), v.FogK(gate + Vector3.up * 10f));
            // Two piers and a panel over the passage: a real way through, 3 m wide with 3.4 m headroom, on the sight line where
            // the street's lane arrives (A(76.5, 0)); nothing solid between s -1.5 and 1.5 from d 71.5 to 76.5. In the pagoda's
            // style: white plaster framed by vermilion posts and beams, on stone plinths. Off the Floor layer: no teleporting
            // onto the gate.
            var gatePlaster = v.Mat(new Color(0.88f, 0.84f, 0.76f), flood, v.FogK(gate + Vector3.up * 3f), new Color(0.07f, 0.045f, 0.02f));
            var plinth = v.At(gate, new Color(0.42f, 0.4f, 0.38f), default, Tex.Albedo("granite_pavers"));
            v.NotFloor(vermilion);
            v.NotFloor(gatePlaster);
            foreach (float s in new[] { -3.75f, 3.75f })
            {
                v.Building(gatePlaster, null, A(74f, s), 4.5f, 5f, 6f, frame, 4f);
                v.Box(plinth, A(74f, s) + Vector3.up * 0.25f, new Vector3(4.5f, 0.5f, 5.2f), frame, 1f); // flush with the passage
            }
            v.Box(gatePlaster, gate + Vector3.up * 4.7f, new Vector3(3f, 2.6f, 5f), frame);
            foreach (float s in new[] { -5.75f, -3.75f, -1.75f, 1.75f, 3.75f, 5.75f }) // posts, proud of both faces
                v.Box(vermilion, A(74f, s) + Vector3.up * 3f, new Vector3(0.5f, 6f, 5.12f), frame, 2f);
            foreach (float y in new[] { 4.2f, 5.65f }) // tie beam and head beam, across the passage above head height
                v.Box(vermilion, gate + Vector3.up * y, new Vector3(12.2f, 0.36f, 5.14f), frame, 2f);
            v.Roof(gateTiles, gate + Vector3.up * 6f, 16f, 9f, frame, 2.4f, 3.4f);

            // White precinct walls with tiled caps.
            void Wall(Vector3 a, Vector3 b)
            {
                var mid = (a + b) / 2f;
                var d = b - a;
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                var plaster = v.At(mid, new Color(0.86f, 0.82f, 0.74f));
                var cap = v.At(mid, new Color(0.12f, 0.12f, 0.14f));
                v.NotFloor(plaster); // the walls block, but nobody teleports onto them and drops inside
                v.NotFloor(cap);
                v.Box(plaster, mid + Vector3.up * 1.5f, new Vector3(1f, 3f, d.magnitude), yaw, 3f);
                v.Box(cap, mid + Vector3.up * 3.2f, new Vector3(1.8f, 0.45f, d.magnitude + 0.8f), yaw, 3f);
            }
            Wall(A(76f, -90f), A(76f, -6.5f));
            Wall(A(76f, 6.5f), A(76f, 90f));
            Wall(A(76f, -90f), A(-100f, -90f));
            Wall(A(76f, 90f), A(-100f, 90f));
            Wall(A(-100f, -90f), A(-100f, 90f));

            // The main hall on a stone base beside and behind the pagoda, with lit shoji along its front.
            var hall = A(-14f, -46f);
            float hallYaw = frame - 8f;
            var turn = Quaternion.Euler(0f, hallYaw, 0f);
            var hallWalls = v.Mat(new Color(0.55f, 0.15f, 0.07f), flood, v.FogK(hall + Vector3.up * 5f), new Color(0.08f, 0.02f, 0.01f));
            v.Box(v.At(hall, new Color(0.44f, 0.42f, 0.4f)), hall + Vector3.up * 0.6f, new Vector3(34f, 1.2f, 24f), hallYaw, 3f);
            v.Building(hallWalls, null, hall + Vector3.up * 1.2f, 30f, 20f, 7f, hallYaw, 4f);
            v.Roof(v.Mat(new Color(0.24f, 0.24f, 0.27f), Moonlit(v, new Color(0.3f, 0.18f, 0.09f)), v.FogK(hall + Vector3.up * 12f)), hall + Vector3.up * 8.2f, 36f, 26f, hallYaw, 9f, 10f);
            for (int i = -3; i <= 3; i++)
            {
                var p = hall + turn * new Vector3(i * 4f, 4.4f, -10.08f);
                v.Box(v.Glow(p, new Color(0.9f, 0.6f, 0.3f)), p, new Vector3(2.6f, 3.6f, 0.1f), hallYaw, 1f);
            }
            v.Halo(hall + turn * new Vector3(0f, 4f, -14f), 26f, glow * 0.22f);

            // Dark cedars round the precinct; red maples by the approach, lit from below by the lanterns.
            var cedar = new Color(0.17f, 0.25f, 0.18f);
            var moon = Moonlit(v, new Color(0.3f, 0.18f, 0.09f));
            for (int i = 0; i < 70; i++)
            {
                float d = -96f + (float)rnd.NextDouble() * 166f, s = -86f + (float)rnd.NextDouble() * 172f;
                float h = 17f + (float)rnd.NextDouble() * 11f, w = 7f + (float)rnd.NextDouble() * 3f;
                var p = A(d, s);
                if (Vector3.Distance(p, o) < 20f || (Mathf.Abs(d + 14f) < 19f && Mathf.Abs(s + 46f) < 24f)
                    || (Mathf.Abs(s) < 14f && d > 8f) || v.FromSightLine(p) < 10f) continue;
                v.Conifer(v.Mat(cedar, moon, v.FogK(p + Vector3.up * h * 0.5f)), v.Mat(new Color(0.16f, 0.11f, 0.08f), moon, v.FogK(p)), p, h, w);
            }
            var maple = new Color(0.42f, 0.09f, 0.05f);
            for (float d = 14f; d < 66f; d += 8f)
                foreach (float s in new[] { -12f, 12f })
                {
                    var p = A(d, s + ((float)rnd.NextDouble() - 0.5f) * 3f);
                    if (v.FromSightLine(p) < 7f) continue;
                    v.Tree(v.Mat(maple, flood, v.FogK(p), new Color(0.05f, 0.01f, 0.005f)), v.At(p, new Color(0.12f, 0.08f, 0.06f)), p, 6.5f, 6f, 500 + (int)d + (int)s);
                }
            v.Halo(o + ax * 8f + Vector3.up * 7f, 34f, glow * 0.18f);
        }

        // ================================================================== Riyadh
        /// <summary>Tall tower with a parabolic opening and sky bridge near the top (Riyadh skyline icon, abstracted), at dusk.</summary>
        public static GameObject ArchTower(Transform parent, Vector3 pos, float yaw, Material mat, Material lights)
        {
            var go = Root(parent, "ArchTowerSilhouette", pos, yaw, 1f);
            var v = Vista.ForLandmark(go, mat, 0.42f);
            float k = v.FogAt(150f);
            var sun = v.CellLight(1.2f, 1.1f);
            var glass = v.Mat(new Color(0.09f, 0.1f, 0.13f), sun, k, new Color(0.24f, 0.14f, 0.15f));
            var fins = v.Mat(new Color(0.68f, 0.64f, 0.6f), sun, k, new Color(0.1f, 0.08f, 0.08f));
            var bridge = v.Mat(Color.black, Vista.Even(0f), k, new Color(0.75f, 0.95f, 1.7f));
            var beacon = v.Mat(Color.black, Vista.Even(0f), k, new Color(2.6f, 0.45f, 0.3f));
            bool model = Models.Spawn("Landmark_ArchTower", go.transform, Palette(glass, ("TS_Metal", fins), ("TS_Glass", bridge), ("TS_EmissiveWarm", beacon))) != null;
            if (!model) ArchTowerShapes(go, glass, bridge);
            v.Halo(OnRoot(go, new Vector3(0f, model ? 103f : 129f, -12f)), 22f, new Color(0.45f, 0.65f, 1f) * 0.5f);
            RiyadhSite(v);
            ClaimSite(parent, pos, 48f);
            { var sp = v.Origin + v.ToViewer * 45f; sp.x += 0.0f; sp.y = -0.35f + 1.5f; v.StampSpot(sp); } // the forecourt in front of the arch
            v.Finish(-0.35f);
            NoShadows(go);
            return go;
        }

        /// <summary>
        /// The city round the tower at dusk: dusty paving, avenues and streets with sodium lamps and median palms, low sand
        /// and white blocks with lit windows on a grid, mid-rise glass towers near the landmark, and haze at street level.
        /// </summary>
        static void RiyadhSite(Vista v)
        {
            const float G = -0.35f;
            var rnd = new System.Random(41);
            var sodium = new Color(1f, 0.66f, 0.36f);
            var plaza = Tex.Albedo("plaza_stone");
            var dust = new Color(0.55f, 0.46f, 0.38f);
            v.GroundRect(v.At(new Vector3(0f, G, 60f), dust, default, plaza), -330f, -330f, 330f, 120f, G, 8f);
            v.GroundRect(v.At(new Vector3(0f, G, 220f), dust, default, plaza), -330f, 120f, 330f, 330f, G, 8f);
            var asphalt = new Color(0.2f, 0.2f, 0.21f);
            var lampPole = new Color(0.3f, 0.3f, 0.3f);
            float[] avenues = { -150f, -48f, 82f, 190f };
            float[] streets = { 62f, 140f, 270f };
            foreach (float x in avenues)
            {
                v.GroundRect(v.At(new Vector3(x, G, 150f), asphalt), x - 10f, -330f, x + 10f, 330f, G + 0.05f, 6f);
                for (float z = -300f; z < 310f; z += 24f)
                {
                    foreach (float s in new[] { -11f, 11f }) v.Lamp(v.At(new Vector3(x, 4f, z), lampPole), new Vector3(x + s, G, z), 9f, sodium, 3.2f);
                    var palm = new Vector3(x, G, z + 12f);
                    if (Vista.InDome(palm, 4f)) v.Palm(v.At(palm + Vector3.up * 8f, new Color(0.16f, 0.24f, 0.12f)), v.At(palm, new Color(0.32f, 0.25f, 0.18f)), palm, 9f, (int)z);
                }
            }
            foreach (float z in streets)
            {
                v.GroundRect(v.At(new Vector3(0f, G, z), asphalt), -330f, z - 8f, 330f, z + 8f, G + 0.04f, 6f);
                for (float x = -300f; x < 310f; x += 26f)
                    foreach (float s in new[] { -9f, 9f }) v.Lamp(v.At(new Vector3(x, 4f, z), lampPole), new Vector3(x, G, z + s), 8f, sodium, 3f);
            }

            bool OnRoad(Vector3 p, float r)
            {
                foreach (float x in avenues) if (Mathf.Abs(p.x - x) < 12f + r) return true;
                foreach (float z in streets) if (Mathf.Abs(p.z - z) < 10f + r) return true;
                return false;
            }
            var tints = new[] { new Color(0.82f, 0.72f, 0.58f), new Color(0.88f, 0.85f, 0.8f), new Color(0.74f, 0.7f, 0.66f), new Color(0.8f, 0.66f, 0.52f) };
            Blocks(v, -310f, -300f, 310f, 310f, G, 30f, 6f, 22f, tints, 0.55f, 0f, rnd,
                (p, r) => new Vector2(p.x, p.z).magnitude < 34f + r || Vector3.Distance(p, v.Origin) < 40f + r || v.FromSightLine(p) < 16f + r || OnRoad(p, r));

            // Mid-rise glass towers round the landmark (between the roads, off the sight line).
            var towers = new[] { new Vector3(-100f, G, 200f), new Vector3(-92f, G, 108f), new Vector3(130f, G, 178f), new Vector3(142f, G, 238f),
                new Vector3(118f, G, 100f), new Vector3(-24f, G, 292f), new Vector3(62f, G, 294f), new Vector3(-112f, G, 250f) };
            foreach (var p in towers)
            {
                float h = Mathf.Round((42f + (float)rnd.NextDouble() * 46f) / 3.2f) * 3.2f, w = 18f + (float)rnd.NextDouble() * 10f;
                float spin = (float)rnd.NextDouble() * 30f - 15f;
                if (!Vista.InDome(p, w)) continue;
                var walls = v.At(p + Vector3.up * h * 0.5f, new Color(0.3f, 0.32f, 0.38f), new Color(0.06f, 0.04f, 0.045f), VistaTex.Facade);
                var top = v.At(p + Vector3.up * h, new Color(0.25f, 0.24f, 0.26f));
                v.Building(walls, top, p, w, w * 0.8f, h, spin, 25.6f, 0f, 0f, 0.75f);
                v.Building(walls, top, p + Vector3.up * h, w * 0.6f, w * 0.5f, 6.4f, spin, 25.6f, 0f, 0f, 0.75f);
            }
            v.Mist(new Vector3(20f, 8f, 200f), 170f, 18f, 0.24f);
            v.Mist(new Vector3(-90f, 5f, 150f), 140f, 12f, 0.2f);
            v.Mist(new Vector3(120f, 5f, 150f), 140f, 12f, 0.2f);
        }

        // ================================================================== Agra
        /// <summary>Domed mausoleum with four minarets on a platform (Agra), at the end of the cell's garden axis at dawn.</summary>
        public static GameObject DomedPavilion(Transform parent, Vector3 pos, float yaw, Material mat)
        {
            var go = Root(parent, "DomedPavilion", pos, yaw, 1f);
            var v = Vista.ForLandmark(go, mat, 0.3f);
            float k = v.FogAt(65f);
            var sun = v.CellLight(1.15f, 1.05f);
            var marble = v.Mat(new Color(0.97f, 0.93f, 0.9f), sun, k, new Color(0.05f, 0.04f, 0.045f));
            var platform = v.Mat(new Color(0.88f, 0.83f, 0.78f), sun, k);
            var gold = v.Mat(new Color(0.95f, 0.76f, 0.42f), sun, k, new Color(0.18f, 0.12f, 0.05f));
            if (Models.Spawn("Landmark_DomedPavilion", go.transform, Palette(marble, ("TS_Stone", platform), ("TS_Gold", gold))) == null)
                PavilionShapes(go, marble);
            AgraSite(v);
            ClaimSite(parent, pos, 60f);
            { var sp = v.Origin + v.ToViewer * 64f; sp.x += 3f; sp.y = -0.05f + 1.5f; v.StampSpot(sp); } // over the marble walk, before the platform
            v.Finish(-0.05f);
            NoShadows(go);
            return go;
        }

        /// <summary>
        /// The garden carries on from the cell: the channel and its marble walks run to the platform through a raised tank,
        /// between rows of cypress (the cell's own shape) and broadleaf trees, inside red sandstone walls with domed corner
        /// towers; red-and-white side halls flank the platform, and dawn mist lies at its foot.
        /// </summary>
        static void AgraSite(Vista v)
        {
            const float G = -0.05f;
            v.Context = v.CellLight(1.1f, 1.05f);
            var rnd = new System.Random(23);
            var grass = Tex.Albedo("grass_short");
            var sandstone = Tex.Albedo("sandstone_blocks");
            var marbleTex = Tex.Albedo("marble_white");
            var lawn = new Color(0.52f, 0.62f, 0.38f);
            var red = new Color(0.66f, 0.38f, 0.29f);
            var white = new Color(0.9f, 0.88f, 0.86f);
            foreach (var (z0, z1) in new[] { (15f, 80f), (80f, 150f), (150f, 240f) })
                v.GroundRect(v.At(new Vector3(0f, G, (z0 + z1) / 2f), lawn, default, grass), -80f, z0, 80f, z1, G, 4f);
            // Round the cell's own garden (x ±8.5, z -7..15) to the walls, and back to the great gate.
            v.GroundRect(v.At(new Vector3(-40f, G, -10f), lawn, default, grass), -80f, -60f, -8.5f, 15f, G, 4f);
            v.GroundRect(v.At(new Vector3(40f, G, -10f), lawn, default, grass), 8.5f, -60f, 80f, 15f, G, 4f);
            v.GroundRect(v.At(new Vector3(0f, G, -30f), lawn, default, grass), -8.5f, -60f, 8.5f, -7.5f, G, 4f);

            var water = v.Mat(new Color(0.6f, 0.63f, 0.74f), Vista.Even(1f), v.FogK(100f));
            foreach (var (z0, z1) in new[] { (15f, 91f), (109f, 184f) })
            {
                var mid = new Vector3(0f, G, (z0 + z1) / 2f);
                var m = v.At(mid, white, default, marbleTex);
                v.GroundRect(m, -4.5f, z0, -1.5f, z1, G + 0.05f, 2f);
                v.GroundRect(m, 1.5f, z0, 4.5f, z1, G + 0.05f, 2f);
                v.GroundRect(water, -1.3f, z0, 1.3f, z1, G + 0.02f, 4f, false);
                v.Box(m, mid + new Vector3(-1.4f, 0.1f, 0f), new Vector3(0.25f, 0.3f, z1 - z0), 0f, 1f);
                v.Box(m, mid + new Vector3(1.4f, 0.1f, 0f), new Vector3(0.25f, 0.3f, z1 - z0), 0f, 1f);
            }
            var tank = v.At(new Vector3(0f, 0f, 100f), white, default, marbleTex);
            v.Box(tank, new Vector3(0f, G + 0.45f, 100f), new Vector3(18f, 0.9f, 18f), 0f, 2f);
            v.GroundRect(water, -6f, 94f, 6f, 106f, G + 0.92f, 4f, false);
            // Four 22.5 cm steps up onto the tank's rim at both ends of the axis, and red sandstone paths that turn off the
            // marble walks and pass along its sides (the cell's bounds are x +-12), for anyone going round it.
            for (int k = 1; k <= 3; k++)
                foreach (float dir in new[] { -1f, 1f })
                {
                    float depth = 0.4f * (4 - k), edge = 100f + dir * 9f;
                    v.Box(tank, new Vector3(0f, G + 0.1125f * k, edge + dir * depth / 2f), new Vector3(6f, 0.225f * k, depth), 0f, 1f);
                }
            var path = v.At(new Vector3(10f, G, 100f), red, default, sandstone);
            foreach (float s in new[] { -1f, 1f })
            {
                float a = s * 4.5f, b = s * 12f, c = s * 9.2f;
                v.GroundRect(path, Mathf.Min(a, c), 86f, Mathf.Max(a, c), 90.2f, G + 0.05f, 3f);
                v.GroundRect(path, Mathf.Min(c, b), 86f, Mathf.Max(c, b), 114f, G + 0.05f, 3f);
                v.GroundRect(path, Mathf.Min(a, c), 109.8f, Mathf.Max(a, c), 114f, G + 0.05f, 3f);
            }
            v.GroundRect(v.At(new Vector3(0f, G, 100f), red, default, sandstone), -80f, 96f, 80f, 104f, G + 0.04f, 3f);
            foreach (float x in new[] { -41f, 41f }) v.GroundRect(v.At(new Vector3(x, G, 100f), red, default, sandstone), x - 3f, 15f, x + 3f, 184f, G + 0.04f, 3f);

            var cypress = new Color(0.17f, 0.29f, 0.18f);
            for (float z = 17.4f; z < 182f; z += 3.4f)
            {
                if (z > 88f && z < 112f) continue;
                foreach (float x in new[] { -5.2f, 5.2f })
                {
                    var p = new Vector3(x, G, z);
                    v.B(v.At(p + Vector3.up * 2f, cypress)).AddFrustum(v.L(p), 0.55f, 0.05f, 3.6f, 10, true, false, 1f);
                }
            }
            var leafy = new[] { new Color(0.26f, 0.38f, 0.2f), new Color(0.3f, 0.42f, 0.22f) };
            foreach (float x in new[] { -60f, -24f, 24f, 60f })
                for (float z = 22f; z < 180f; z += 9f)
                {
                    var p = new Vector3(x + ((float)rnd.NextDouble() - 0.5f) * 2f, G, z);
                    float h = 7f + (float)rnd.NextDouble() * 2f;
                    var tint = leafy[rnd.Next(2)];
                    if (z > 92f && z < 108f) continue;
                    v.Tree(v.At(p + Vector3.up * 5f, tint), v.At(p, new Color(0.25f, 0.2f, 0.16f)), p, h, 6f, (int)(x * 10f + z));
                }

            foreach (float x in new[] { -80f, 80f })
            {
                foreach (var (z0, z1) in new[] { (-60f, 80f), (80f, 229f) })
                {
                    var wall = v.At(new Vector3(x, 4f, (z0 + z1) / 2f), red, default, sandstone);
                    v.Box(wall, new Vector3(x, G + 4f, (z0 + z1) / 2f), new Vector3(2f, 8f, z1 - z0), 0f, 3f);
                    for (float z = z0 + 2f; z < z1 - 1f; z += 3.2f) v.Box(wall, new Vector3(x, G + 8.5f, z), new Vector3(2.2f, 1f, 1.6f), 0f, 1f);
                }
                foreach (float z in new[] { -60f, 229f })
                {
                    var t = new Vector3(x, G, z);
                    v.B(v.At(t + Vector3.up * 6f, red, default, sandstone)).AddFrustum(v.L(t), 4.4f, 4f, 12f, 8, false, true, 3f);
                    v.B(v.At(t + Vector3.up * 13f, white)).AddSphere(v.L(t + Vector3.up * 12f), 4.2f, 10, 6, 2f, 0f, 90f);
                }
            }
            // The great gate behind the garden, facing the tomb: red sandstone, a deep arch, small white domes along the top.
            var gateAt = new Vector3(0f, G, -60f);
            v.Building(v.At(gateAt + Vector3.up * 11f, red, default, VistaTex.Gothic), v.At(gateAt + Vector3.up * 22f, red * 0.9f), gateAt, 44f, 16f, 22f, 0f, 25.6f);
            foreach (var (x0, x1) in new[] { (-80f, -22f), (22f, 80f) })
            {
                var mid = new Vector3((x0 + x1) / 2f, G, -60f);
                var wall = v.At(mid + Vector3.up * 4f, red, default, sandstone);
                v.Box(wall, mid + Vector3.up * 4f, new Vector3(x1 - x0, 8f, 2f), 0f, 3f);
                for (float x = x0 + 2f; x < x1 - 1f; x += 3.2f) v.Box(wall, new Vector3(x, G + 8.5f, -60f), new Vector3(1.6f, 1f, 2.2f), 0f, 1f);
            }
            var doorway = v.Mat(new Color(0.06f, 0.04f, 0.035f), Vista.Even(1f), v.FogK(gateAt));
            var face = gateAt + new Vector3(0f, 0f, 8.05f);
            v.Box(doorway, face + new Vector3(0f, 4.5f, 0f), new Vector3(12f, 9f, 0.1f));
            ArchOpening(v.B(doorway), v.L(face + Vector3.up * 9f), Vector3.forward, 6f);
            ArchRing(v.B(v.At(face + Vector3.up * 9f, white)), v.L(face + Vector3.up * 9f + Vector3.forward * 0.03f), 6f, 6.9f, Vector3.forward);
            for (int i = 0; i < 11; i++)
            {
                var d = gateAt + new Vector3(-17.5f + i * 3.5f, 22f, 6.5f);
                var chhatri = v.At(d + Vector3.up * 2f, white);
                v.B(chhatri).AddCylinder(v.L(d), 1f, 1.2f, 8, 1f);
                v.B(chhatri).AddLathe(v.L(d + Vector3.up * 1.2f), Onion(1.2f), 10, 1f);
            }
            foreach (float x in new[] { -21f, 21f })
                foreach (float z in new[] { -67f, -53f })
                {
                    var t = new Vector3(x, G, z);
                    v.B(v.At(t + Vector3.up * 13f, red, default, sandstone)).AddFrustum(v.L(t), 2.6f, 2.3f, 26f, 8, false, true, 3f);
                    v.B(v.At(t + Vector3.up * 27f, white)).AddLathe(v.L(t + Vector3.up * 26f), Onion(2.6f), 12, 2f);
                }

            foreach (float s in new[] { -1f, 1f })
            {
                var b = new Vector3(64f * s, G, 230f);
                v.Building(v.At(b + Vector3.up * 7f, red, default, VistaTex.Gothic), v.At(b + Vector3.up * 14f, red * 0.9f), b, 20f, 54f, 13f, 0f, 25.6f);
                foreach (float dz in new[] { -15f, 0f, 15f })
                {
                    var d = b + new Vector3(0f, 13f, dz);
                    float r = dz == 0f ? 6f : 4.2f;
                    var dome = v.At(d + Vector3.up * 4f, white);
                    v.B(dome).AddCylinder(v.L(d), r * 0.8f, 2f, 12, 2f);
                    v.B(dome).AddLathe(v.L(d + Vector3.up * 2f), Onion(r), 14, 2f);
                }
            }
            v.Mist(new Vector3(0f, G + 4f, 205f), 170f, 12f, 0.32f);
            v.Mist(new Vector3(0f, G + 2f, 176f), 150f, 6f, 0.22f); // past the end of the walk (the cell's bounds stop at z 170)
        }

        // ================================================================== London
        public static GameObject ClockTower(Transform parent, Vector3 pos, float yaw, Material mat, Material face)
        {
            var go = Root(parent, "ClockTowerSilhouette", pos, yaw, 1f);
            var v = Vista.ForLandmark(go, mat, 0.34f);
            float k = v.FogAt(100f);
            var flood = v.Flood(new Color(1f, 0.8f, 0.52f), 30f, 28f, new Color(0.1f, 0.12f, 0.18f), new Color(0.36f, 0.26f, 0.13f));
            var stone = v.Mat(new Color(0.8f, 0.68f, 0.47f), flood, k, new Color(0.3f, 0.2f, 0.08f));
            var spire = v.Mat(new Color(0.09f, 0.1f, 0.11f), flood, k, new Color(0.02f, 0.02f, 0.025f));
            var gilt = v.Mat(new Color(0.92f, 0.72f, 0.36f), flood, k, new Color(0.5f, 0.36f, 0.14f));
            var dial = v.Mat(new Color(0.95f, 0.92f, 0.78f), Vista.Even(0.6f), k, new Color(1.5f, 1.32f, 0.92f));
            bool model = Models.Spawn("Landmark_ClockTower", go.transform, Palette(stone, ("TS_MetalDark", spire), ("TS_Gold", gilt), ("TS_EmissiveWarm", dial))) != null;
            if (!model) ClockTowerShapes(go, stone, dial);
            ClockHands(go, v.Mat(new Color(0.03f, 0.03f, 0.035f), Vista.Even(1f), k), model ? 64.5f : 66f, model ? 7.6f : 7.15f);
            LondonSite(v);
            ClaimSite(parent, pos, 30f);
            v.StampSpot(new Vector3(95f, 1f + 1.5f, 98f)); // the embankment between the palace and the bridge
            v.Finish(1f);
            NoShadows(go);
            return go;
        }

        /// <summary>Hour and minute hands at one minute to midnight on all four faces (root frame), with a centre boss.</summary>
        static void ClockHands(GameObject root, Material mat, float height, float offset)
        {
            var b = new MeshBuilder();
            foreach (var n in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
            {
                var c = Vector3.up * height + n * offset;
                var right = Vector3.Cross(Vector3.up, -n); // the viewer's right, looking at this face
                foreach (var (deg, len, width) in new[] { (-6f, 3f, 0.22f), (-0.5f, 2f, 0.34f) })
                {
                    float a = deg * Mathf.Deg2Rad;
                    var dir = Vector3.up * Mathf.Cos(a) + right * Mathf.Sin(a);
                    b.AddBox(c + dir * (len / 2f - 0.35f), new Vector3(width, len, 0.1f), Quaternion.LookRotation(n, dir), 1f);
                }
                b.AddSphere(c + n * 0.06f, 0.32f, 8, 5, 1f);
            }
            var go = MeshFactory.FromBuilder("ClockHands", b, mat, root.transform);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// Westminster across the river at night: the Thames past the cell's water, the far embankment with lamps and plane
        /// trees, the floodlit Gothic palace (river front, pavilions, pinnacles, the central and Victoria towers), lit
        /// buildings beyond, and Westminster Bridge (z 112) with its green arches and a double row of lamps.
        /// </summary>
        static void LondonSite(Vista v)
        {
            const float Top = 1f, Water = -1.6f;
            v.Context = Night(v, new Color(0.28f, 0.2f, 0.12f));
            var flood = v.Flood(new Color(1f, 0.8f, 0.52f), 40f, 22f, new Color(0.1f, 0.12f, 0.18f), new Color(0.36f, 0.26f, 0.13f));
            var lamp = new Color(1f, 0.88f, 0.66f);
            var rnd = new System.Random(52);
            var granite = Tex.Albedo("granite_pavers");
            v.GroundRect(v.Mat(v.FogColor * 0.8f, Vista.Even(1f), v.FogK(150f)), -40f, -330f, 80f, 330f, Water, 10f);
            v.Box(v.At(new Vector3(79f, 0f, 0f), new Color(0.4f, 0.38f, 0.35f), default, granite), new Vector3(79f, (Top + Water) / 2f - 0.3f, 0f), new Vector3(2.4f, Top - Water + 0.6f, 640f), 0f, 3f);
            var pave = new Color(0.2f, 0.2f, 0.22f);
            v.GroundRect(v.At(new Vector3(100f, Top, 0f), pave, default, granite), 80f, -330f, 140f, 330f, Top, 4f);
            v.GroundRect(v.At(new Vector3(200f, Top, 0f), pave, default, granite), 140f, -330f, 330f, 330f, Top, 4f);

            // The palace: river front in sections, projecting pavilions, pinnacles, the central and Victoria towers.
            var gold = new Color(0.78f, 0.66f, 0.46f);
            var slate = new Color(0.12f, 0.13f, 0.15f);
            Material Facade(Vector3 at) => v.Mat(gold, flood, v.FogK(at), new Color(0.1f, 0.07f, 0.03f), VistaTex.Gothic);
            Material Slate(Vector3 at) => v.Mat(slate, flood, v.FogK(at));
            for (float z = -110f; z < 55f; z += 33f)
            {
                float len = Mathf.Min(33f, 55f - z);
                var p = new Vector3(103f, Top, z + len / 2f);
                v.Building(Facade(p), Slate(p), p, 36f, len, 22f, 0f, 25.6f, 4f, 3f, 0.5f, VistaTex.GothicLights);
            }
            // A low wing joins the tower, so the view to its clock faces stays open over it.
            var wing = new Vector3(110f, Top, 70f);
            v.Building(Facade(wing), Slate(wing), wing, 22f, 30f, 10f, 0f, 25.6f, 3f, 2f, 0.4f, VistaTex.GothicLights);
            foreach (float z in new[] { -98f, -58f, -18f, 22f, 46f })
            {
                var p = new Vector3(86f, Top, z);
                v.Building(Facade(p), Slate(p), p, 9f, 10f, 29f, 0f, 25.6f, 8f, 4.4f, 0.5f, VistaTex.GothicLights);
            }
            for (float z = -106f; z < 54f; z += 8f)
            {
                var p = new Vector3(85f, Top + 22f, z);
                v.B(v.Mat(gold, flood, v.FogK(p), new Color(0.1f, 0.07f, 0.03f))).AddFrustum(v.L(p), 0.7f, 0.05f, 5f, 4, true, false, 2f);
            }
            var ct = new Vector3(104f, Top, -4f);
            v.B(Facade(ct)).AddFrustum(v.L(ct), 8f, 7f, 50f, 8, false, true, 6f);
            v.B(Slate(ct)).AddFrustum(v.L(ct + Vector3.up * 50f), 7f, 0.2f, 26f, 8, false, false, 4f);
            var vt = new Vector3(104f, Top, -118f);
            v.Building(Facade(vt), Slate(vt), vt, 22f, 22f, 70f, 0f, 25.6f, 0f, 0f, 0.4f, VistaTex.GothicLights);
            foreach (var o in new[] { new Vector3(-10f, 0f, -10f), new Vector3(10f, 0f, -10f), new Vector3(-10f, 0f, 10f), new Vector3(10f, 0f, 10f) })
                v.B(Facade(vt)).AddFrustum(v.L(vt + o + Vector3.up * 70f), 1.6f, 0.2f, 10f, 8, false, false, 2f);

            // Lit buildings beyond the bridge on the far bank.
            var brick = new Color(0.5f, 0.42f, 0.34f);
            foreach (var p in new[] { new Vector3(104f, Top, 140f), new Vector3(104f, Top, 178f), new Vector3(140f, Top, 150f), new Vector3(140f, Top, 196f) })
            {
                float h = 19.2f + 3.2f * rnd.Next(3);
                v.Building(v.At(p + Vector3.up * h * 0.5f, brick, default, VistaTex.Facade), v.At(p + Vector3.up * h, slate), p, 26f, 30f, h, 0f, 25.6f, 4f, 3f, 0.8f);
            }

            // Westminster Bridge: deck, green fascia and arch rings, piers, parapets and two rows of lamps.
            const float Bz = 112f;
            var green = v.Mat(new Color(0.16f, 0.34f, 0.24f), flood, v.FogK(new Vector3(30f, 3f, Bz)), new Color(0.02f, 0.05f, 0.035f));
            var deck = v.Mat(new Color(0.42f, 0.42f, 0.44f), flood, v.FogK(new Vector3(30f, 4f, Bz)));
            var shade = v.Mat(new Color(0.012f, 0.016f, 0.02f), Vista.Even(1f), v.FogK(new Vector3(30f, 0f, Bz)));
            v.Box(deck, new Vector3(30f, 4.8f, Bz), new Vector3(110f, 1.2f, 16f), 0f, 3f);
            v.Box(green, new Vector3(30f, 3.6f, Bz - 8.1f), new Vector3(110f, 1.4f, 0.3f), 0f, 2f);
            v.Box(green, new Vector3(30f, 3.6f, Bz + 8.1f), new Vector3(110f, 1.4f, 0.3f), 0f, 2f);
            v.Box(deck, new Vector3(30f, 5.9f, Bz - 7.8f), new Vector3(110f, 1f, 0.4f), 0f, 2f);
            v.Box(deck, new Vector3(30f, 5.9f, Bz + 7.8f), new Vector3(110f, 1f, 0.4f), 0f, 2f);
            v.Box(deck, new Vector3(86f, (Top + 5.4f) / 2f, Bz), new Vector3(12f, 5.4f - Top, 16f), 0f, 3f);
            for (float x = -21f; x <= 81f; x += 14.5f)
                v.Box(deck, new Vector3(x, (Water + 4.2f) / 2f, Bz), new Vector3(3f, 4.2f - Water, 17f), 0f, 3f);
            for (float x = -13.75f; x < 81f; x += 14.5f)
                foreach (float s in new[] { -1f, 1f })
                {
                    var n = s < 0f ? Vector3.back : Vector3.forward;
                    var at = v.L(new Vector3(x, -3.2f, Bz + s * 8.15f));
                    ArchOpening(v.B(shade), at, n, 7.3f);
                    ArchRing(v.B(green), at + n * 0.05f, 7.3f, 7.9f, n);
                }
            var lampIron = v.At(new Vector3(30f, 6f, Bz), new Color(0.05f, 0.08f, 0.06f));
            for (float x = -20f; x <= 84f; x += 12f)
                foreach (float s in new[] { -7.8f, 7.8f })
                {
                    v.Lamp(lampIron, new Vector3(x, 6.4f, Bz + s), 4.6f, lamp, 2.6f);
                    if (s < 0f) v.Streak(new Vector3(x, Water + 0.03f, Bz - 9f), 1.3f, 16f, lamp * 0.42f);
                }

            // Embankment lamps and their reflections; plane trees beyond the palace.
            var pole = v.At(new Vector3(80f, 3f, 0f), new Color(0.05f, 0.06f, 0.05f));
            for (float z = -300f; z < 300f; z += 14f)
            {
                if (z > Bz - 10f && z < Bz + 10f) continue;
                v.Lamp(pole, new Vector3(80.6f, Top, z), 4f, lamp, 2.4f);
                v.Pool(new Vector3(81.5f, Top, z), 4.5f, lamp * 0.25f);
                v.Streak(new Vector3(77.5f, Water + 0.03f, z), 1.3f, 16f, lamp * 0.45f);
            }
            var leaf = new Color(0.15f, 0.2f, 0.13f);
            var bark = new Color(0.15f, 0.13f, 0.11f);
            var moon = Moonlit(v, new Color(0.28f, 0.2f, 0.12f));
            int seed = 700;
            void Plane(Vector3 p) => v.Tree(v.Mat(leaf, moon, v.FogK(p + Vector3.up * 7f)), v.Mat(bark, moon, v.FogK(p)), p, 12f, 8f, seed++);
            for (float z = 126f; z < 300f; z += 11f) Plane(new Vector3(84f, Top, z));
            for (float z = -300f; z < -134f; z += 11f) Plane(new Vector3(84f, Top, z));

            v.Mist(new Vector3(40f, 0.5f, 92f), 110f, 6f, 0.35f);
            v.Mist(new Vector3(40f, 0.5f, 165f), 120f, 7f, 0.35f);
            v.Mist(new Vector3(70f, 2f, -40f), 100f, 6f, 0.3f);
        }

        // ================================================================== Berlin
        /// <summary>Six-column gate with entablature and quadriga (Berlin), closing Pariser Platz on an overcast day.</summary>
        public static GameObject ColumnGate(Transform parent, Vector3 pos, float yaw, Material mat, float scale = 1f)
        {
            var go = Root(parent, "ColumnGate", pos, yaw, scale);
            var v = Vista.ForLandmark(go, mat, 0.22f);
            float k = v.FogAt(26f);
            var sun = v.CellLight(1.1f, 1f);
            var stone = v.Mat(new Color(0.8f, 0.76f, 0.68f), sun, k);
            var copper = v.Mat(new Color(0.32f, 0.5f, 0.44f), sun, k);
            // Default turn: the quadriga faces the square and the player.
            if (Models.Spawn("Landmark_ColumnGate", go.transform, Palette(stone, ("TS_Metal", copper))) == null)
                GateShapes(go, stone);
            BerlinSite(v);
            ClaimSite(parent, pos + Vector3.forward * 90f, 130f); // the Tiergarten behind the gate
            v.StampSpot(new Vector3(0f, -0.05f + 1.5f, 46f)); // the square in front of the gate
            v.Finish(-0.1f);
            NoShadows(go);
            return go;
        }

        /// <summary>
        /// Pariser Platz and the Tiergarten: stone blocks along the square's sides and the two houses that flank the gate,
        /// the road running on through the park to the victory column, and the city's blocks fading in the haze beyond.
        /// </summary>
        static void BerlinSite(Vista v)
        {
            const float G = -0.1f;
            v.Context = v.CellLight(1.05f, 1f);
            var rnd = new System.Random(61);
            var park = new Color(0.27f, 0.3f, 0.24f);
            var asphalt = new Color(0.36f, 0.36f, 0.37f);
            var granite = Tex.Albedo("granite_pavers");
            var grass = Tex.Albedo("grass_short");
            v.GroundRect(v.At(new Vector3(0f, G, 120f), park, default, grass), -330f, 70f, 330f, 170f, G, 5f);
            v.GroundRect(v.At(new Vector3(0f, G, 240f), park, default, grass), -330f, 170f, 330f, 330f, G, 5f);
            v.GroundRect(v.At(new Vector3(100f, G, 30f), asphalt, default, granite), 60f, -330f, 330f, 70f, G, 4f);
            v.GroundRect(v.At(new Vector3(-100f, G, 30f), asphalt, default, granite), -330f, -330f, -60f, 70f, G, 4f);
            v.GroundRect(v.At(new Vector3(0f, G, 150f), asphalt), -13f, 70f, 13f, 330f, G + 0.04f, 4f);

            var tints = new[] { new Color(0.8f, 0.76f, 0.68f), new Color(0.7f, 0.7f, 0.7f), new Color(0.78f, 0.7f, 0.58f) };
            var roof = new Color(0.3f, 0.3f, 0.32f);
            foreach (float s in new[] { -1f, 1f })
            {
                for (float z = 12f; z < 68f; z += 19f)
                {
                    var p = new Vector3(s * 70f, G, z + 9f);
                    var t = tints[rnd.Next(tints.Length)];
                    float h = 6 * 3.2f + 1f + 3.2f * rnd.Next(2);
                    v.Building(v.At(p + Vector3.up * h * 0.5f, t, default, VistaTex.Facade), v.At(p + Vector3.up * h, roof), p, 20f, 18f, h, 0f, 25.6f, 3f, 2f);
                }
                var f = new Vector3(s * 42f, G, 66f);
                v.Building(v.At(f + Vector3.up * 7f, tints[0], default, VistaTex.Facade), v.At(f + Vector3.up * 14f, roof), f, 30f, 14f, 13.8f, 0f, 25.6f, 2.5f, 2f);
            }

            var leaf = new[] { new Color(0.28f, 0.34f, 0.24f), new Color(0.24f, 0.31f, 0.22f) };
            var bark = new Color(0.22f, 0.2f, 0.18f);
            for (float x = -250f; x < 250f; x += 20f)
                for (float z = 84f; z < 300f; z += 20f)
                {
                    var p = new Vector3(x + ((float)rnd.NextDouble() - 0.5f) * 12f, G, z + ((float)rnd.NextDouble() - 0.5f) * 12f);
                    float h = 14f + (float)rnd.NextDouble() * 7f;
                    var t = leaf[rnd.Next(2)];
                    if (Mathf.Abs(p.x) < 20f || !Vista.InDome(p, 6f)) continue;
                    v.Tree(v.At(p + Vector3.up * h * 0.6f, t), v.At(p, bark), p, h, 10f, (int)(x * 31f + z));
                }

            var vc = new Vector3(0f, G, 296f);
            if (Vista.InDome(vc, 12f))
            {
                var red = v.At(vc + Vector3.up * 10f, new Color(0.6f, 0.45f, 0.42f));
                var pale = v.At(vc + Vector3.up * 30f, new Color(0.82f, 0.76f, 0.64f));
                var gilt = v.Mat(new Color(0.9f, 0.72f, 0.36f), v.Context, v.FogK(vc + Vector3.up * 50f), new Color(0.15f, 0.1f, 0.03f));
                v.B(red).AddFrustum(v.L(vc), 11f, 10f, 7f, 8, false, true, 3f);
                v.B(red).AddCylinder(v.L(vc + Vector3.up * 7f), 6f, 8f, 16, 3f);
                v.B(pale).AddFrustum(v.L(vc + Vector3.up * 15f), 2.9f, 2.5f, 38f, 16, false, true, 3f);
                foreach (float y in new[] { 24f, 34f, 44f }) v.B(gilt).AddCylinder(v.L(vc + Vector3.up * y), 3.1f, 1.4f, 16, 2f);
                v.B(pale).AddFrustum(v.L(vc + Vector3.up * 53f), 3.4f, 2.4f, 2f, 16, true, true, 2f);
                v.B(gilt).AddFrustum(v.L(vc + Vector3.up * 55f), 1.2f, 0.6f, 6f, 8, true, true, 2f);
                v.B(gilt).AddSphere(v.L(vc + Vector3.up * 61.5f), 1f, 8, 5, 1f);
            }

            Blocks(v, -310f, 40f, 310f, 310f, G, 28f, 16f, 32f, tints, 0f, 3f, rnd,
                (p, r) => new Vector2(p.x, p.z).magnitude < 235f || !Vista.InDome(p, r) || (Mathf.Abs(p.x) < 26f + r && p.z > 0f));
        }

        // ================================================================== Egypt
        public static GameObject Pyramids(Transform parent, Vector3 pos, float yaw, Material mat)
        {
            var go = Root(parent, "Pyramids", pos, yaw, 1f);
            var v = Vista.ForLandmark(go, mat, 0.3f);
            v.Context = v.CellLight(0.75f, 1f);
            var stone = v.Mat(new Color(0.86f, 0.74f, 0.56f), v.Context, v.FogAt(62f));
            var mb = new MeshBuilder();
            void Pyr(Vector3 c, float half, float h)
            {
                var top = c + Vector3.up * h;
                Vector3 a = c + new Vector3(-half, 0, -half), b = c + new Vector3(half, 0, -half), d = c + new Vector3(half, 0, half), e = c + new Vector3(-half, 0, half);
                mb.AddTriangle(a, top, b, Vector2.zero, new Vector2(0.5f, 1), new Vector2(1, 0));
                mb.AddTriangle(b, top, d, Vector2.zero, new Vector2(0.5f, 1), new Vector2(1, 0));
                mb.AddTriangle(d, top, e, Vector2.zero, new Vector2(0.5f, 1), new Vector2(1, 0));
                mb.AddTriangle(e, top, a, Vector2.zero, new Vector2(0.5f, 1), new Vector2(1, 0));
            }
            Pyr(new Vector3(0, 0, 0), 46f, 62f);
            Pyr(new Vector3(-95f, 0, 40f), 40f, 54f);
            Pyr(new Vector3(-158f, 0, -18f), 22f, 30f);        // the third, inside the sky dome
            foreach (float z in new[] { -22f, 0f, 22f }) Pyr(new Vector3(62f, 0, z), 8f, 12f); // queens' pyramids
            for (float x = 80f; x < 140f; x += 14f)              // a field of mastaba tombs
                for (float z = -56f; z < 44f; z += 12f) mb.AddBox(new Vector3(x, 1.4f, z), new Vector3(10f, 3.2f, 6f));
            MeshFactory.FromBuilder("Pyramids", mb, stone, go.transform);
            EgyptSite(v, new[] { OnRoot(go, Vector3.zero), OnRoot(go, new Vector3(-95f, 0, 40f)), OnRoot(go, new Vector3(-158f, 0, -18f)), OnRoot(go, new Vector3(108f, 0, -6f)) });
            ClaimSite(parent, pos, 70f);
            { var sp = v.Origin + v.ToViewer * 70f; sp.x += 0.0f; sp.y = -0.08f + 1.5f; v.StampSpot(sp); } // the sand before the great pyramid
            v.Finish(-0.08f);
            NoShadows(go);
            return go;
        }

        /// <summary>Desert past the cell's sand floor, low dunes that break the horizon (clear of the monuments), and heat haze.</summary>
        static void EgyptSite(Vista v, Vector3[] monuments)
        {
            const float G = -0.08f;
            var sandTex = Tex.Albedo("sand_ripples");
            var sand = new Color(0.86f, 0.73f, 0.54f);
            // Continuous with the court's sand floor (top -0.02, z up to 38): a 6 cm step down at its edge, sand underfoot.
            void Sand(Vector3 at, float x0, float z0, float x1, float z1)
            {
                var m = v.At(at, sand, default, sandTex);
                v.Footsteps(m, "sand");
                v.GroundRect(m, x0, z0, x1, z1, G, 5f);
            }
            Sand(new Vector3(0f, G, 90f), -330f, 38f, 330f, 140f);
            Sand(new Vector3(0f, G, 230f), -330f, 140f, 330f, 330f);
            Sand(new Vector3(150f, G, -50f), 30f, -330f, 330f, 38f);
            Sand(new Vector3(-150f, G, -50f), -330f, -330f, -30f, 38f);
            var rnd = new System.Random(7);
            var dune = new Color(0.9f, 0.77f, 0.58f);
            for (int i = 0; i < 18; i++)
            {
                float a = (-110f + (float)rnd.NextDouble() * 220f) * Mathf.Deg2Rad;
                float d = 90f + (float)rnd.NextDouble() * 200f;
                var p = new Vector3(Mathf.Sin(a) * d, G - 0.5f, Mathf.Cos(a) * d);
                float len = 40f + (float)rnd.NextDouble() * 80f, wid = 20f + (float)rnd.NextDouble() * 30f, hi = 3f + (float)rnd.NextDouble() * 8f;
                float spin = (float)rnd.NextDouble() * 180f;
                bool clear = Vista.InDome(p, len * 0.5f) && v.FromSightLine(p) > 30f + wid;
                foreach (var m in monuments) if (Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(m.x, 0f, m.z)) < 70f + len * 0.5f) clear = false;
                if (!clear) continue;
                var b = new MeshBuilder();
                b.AddSphere(Vector3.zero, 1f, 18, 6, 1f, 0f, 90f);
                var go = v.Piece("Dune", b, v.At(p, dune), p, new Vector3(len, hi, wid));
                go.transform.localRotation = Quaternion.Euler(0f, spin, 0f);
            }
            v.Mist(new Vector3(-20f, 6f, 230f), 260f, 16f, 0.22f);
            v.Mist(new Vector3(120f, 4f, 160f), 180f, 10f, 0.18f);
        }

        // ================================================================== skylines
        /// <summary>
        /// A ring of towers: setbacks, spires, a facade of windows (lit at night when <paramref name="windowLights"/> is given,
        /// with red beacons on the tallest), under the same gentle haze as the landmark vistas. Towers that would land on a
        /// landmark's site in the same cell are left out.
        /// </summary>
        public static GameObject Skyline(Transform parent, Vector3 center, float radius, float arcDegrees, float yaw, int count, Material mat, Material windowLights, int seed)
        {
            var go = Root(parent, "Skyline", center, yaw, 1f);
            var rnd = new System.Random(seed);
            var v = new Vista(go, mat, Vista.Rate(0.55f, 200f));
            bool night = windowLights != null;
            var tint = mat.GetColor("_BaseColor");
            var tints = new[] { tint, tint * 0.85f, tint * 1.18f };
            var turn = Quaternion.Euler(0f, yaw, 0f);
            for (int i = 0; i < count; i++)
            {
                float a = (-arcDegrees / 2 + arcDegrees * i / Mathf.Max(1, count - 1)) * Mathf.Deg2Rad;
                float r = radius + (float)rnd.NextDouble() * 40f;
                float h = 25f + (float)rnd.NextDouble() * 85f;
                float w = 10f + (float)rnd.NextDouble() * 16f;
                float spin = (float)rnd.NextDouble() * 90f;
                double roll = rnd.NextDouble();
                var p = center + turn * new Vector3(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r);
                if (OnSite(parent, p, w)) continue;
                Tower(v, p, w, h, spin, tints[i % 3], night, roll);
            }
            v.Finish(center.y);
            NoShadows(go);
            return go;
        }

        static void Tower(Vista v, Vector3 at, float w, float h, float yaw, Color tint, bool night, double roll)
        {
            var walls = v.At(at + Vector3.up * h * 0.5f, tint, default, VistaTex.Facade);
            var roof = v.At(at + Vector3.up * h, tint * 0.55f);
            float lights = night ? 0.9f : 0f;
            if (h > 55f && roll < 0.65)
            {
                float h1 = Mathf.Round(h * 0.6f / 3.2f) * 3.2f, h2 = Mathf.Round(h * 0.25f / 3.2f) * 3.2f;
                v.Building(walls, roof, at, w, w, h1, yaw, 25.6f, 0f, 0f, lights);
                v.Building(walls, roof, at + Vector3.up * h1, w * 0.78f, w * 0.78f, h2, yaw, 25.6f, 0f, 0f, lights);
                v.Building(walls, roof, at + Vector3.up * (h1 + h2), w * 0.52f, w * 0.52f, h - h1 - h2, yaw, 25.6f, 0f, 0f, lights);
            }
            else v.Building(walls, roof, at, w, w, h, yaw, 25.6f, 0f, 0f, lights);
            float top = h;
            if (roll > 0.75)
            {
                v.B(roof).AddFrustum(v.L(at + Vector3.up * h), w * 0.16f, 0.12f, h * 0.24f, 6, false, false, 4f);
                top = h * 1.24f;
            }
            if (night && top > 70f) v.Halo(at + Vector3.up * (top + 0.5f), 2.2f, new Color(1f, 0.12f, 0.08f));
        }

        // ================================================================== procedural fallbacks (no model file)
        static void EiffelShapes(GameObject go, Material mat, Material lightMat)
        {
            var mb = new MeshBuilder();
            float H = 100f;
            // Four curved legs as stacked segments leaning inward.
            for (int leg = 0; leg < 4; leg++)
            {
                float ang = (45f + leg * 90f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                var pts = new List<Vector3>();
                for (int i = 0; i <= 10; i++)
                {
                    float t = i / 10f;
                    float y = t * H * 0.36f;
                    float r = Mathf.Lerp(17f, 6.5f, Mathf.Pow(t, 0.75f));
                    pts.Add(dir * r + Vector3.up * y);
                }
                mb.AddTube(pts, 1.6f, 4, false);
            }
            // First and second platforms.
            mb.AddBox(new Vector3(0, H * 0.18f, 0), new Vector3(26f, 1.6f, 26f));
            mb.AddBox(new Vector3(0, H * 0.36f, 0), new Vector3(14f, 1.4f, 14f));
            // Arches between legs at the base.
            for (int side = 0; side < 4; side++)
                mb.AddArch(Quaternion.Euler(0, side * 90f, 0) * new Vector3(0, H * 0.06f, 15.5f), 8f, 9.2f, 0.9f, 10, Quaternion.Euler(0, side * 90f, 0));
            // Upper shaft tapering to the top.
            var shaft = new List<Vector2> { new Vector2(6.5f, H * 0.36f), new Vector2(4.2f, H * 0.55f), new Vector2(2.4f, H * 0.78f), new Vector2(1.1f, H * 0.93f), new Vector2(0.4f, H * 1.0f), new Vector2(0.15f, H * 1.08f) };
            mb.AddLathe(Vector3.zero, shaft, 4, 1f, 45f, 405f);
            // Lattice suggestion: horizontal bands.
            for (int i = 0; i < 9; i++)
            {
                float y = H * (0.4f + i * 0.065f);
                float r = Mathf.Lerp(6.2f, 0.9f, (y - H * 0.36f) / (H * 0.62f));
                mb.AddBox(new Vector3(0, y, 0), new Vector3(r * 2f, 0.45f, r * 2f));
            }
            MeshFactory.FromBuilder("Tower", mb, mat, go.transform);
            if (lightMat != null) MeshFactory.Sphere("Beacon", go.transform, new Vector3(0, H * 1.08f, 0), 0.9f, lightMat);
        }

        static void LibertyShapes(GameObject go, Material mat, Material torch)
        {
            var mb = new MeshBuilder();
            mb.AddBox(new Vector3(0, 10, 0), new Vector3(16, 20, 16));                  // pedestal base
            mb.AddBox(new Vector3(0, 26, 0), new Vector3(11, 12, 11));                  // pedestal top
            mb.AddFrustum(new Vector3(0, 32, 0), 5f, 3.2f, 20f, 10);                    // robe
            mb.AddFrustum(new Vector3(0, 52, 0), 3.2f, 2.2f, 6f, 10);                   // shoulders
            mb.AddSphere(new Vector3(0, 60, 0), 2.1f, 10, 8);                           // head
            for (int i = 0; i < 7; i++)                                                 // crown rays
            {
                float a = (-60f + i * 20f) * Mathf.Deg2Rad;
                mb.AddBox(new Vector3(Mathf.Sin(a) * 2.6f, 62f, -Mathf.Cos(a) * 0.5f), new Vector3(0.35f, 3f, 0.35f), Quaternion.Euler(0, 0, -a * Mathf.Rad2Deg * 0.6f));
            }
            mb.AddBox(new Vector3(2.6f, 62, 0), new Vector3(1.2f, 14, 1.2f), Quaternion.Euler(0, 0, -12));   // raised arm
            mb.AddBox(new Vector3(-2.8f, 47, -1.2f), new Vector3(2.2f, 4f, 0.8f));                            // tablet
            MeshFactory.FromBuilder("Liberty", mb, mat, go.transform);
            MeshFactory.Sphere("TorchFlame", go.transform, new Vector3(4.1f, 70f, 0), 1.3f, torch);
        }

        static void PagodaShapes(GameObject go, Material mat, Material roof)
        {
            float y = 0;
            for (int i = 0; i < 5; i++)
            {
                float w = 14f - i * 2.2f;
                MeshFactory.Box("Tier", go.transform, new Vector3(0, y + 2.5f, 0), new Vector3(w * 0.7f, 5f, w * 0.7f), mat, false);
                var mb = new MeshBuilder();
                var prof = new List<Vector2> { new Vector2(w * 0.85f, 0f), new Vector2(w * 0.6f, 1.2f), new Vector2(w * 0.35f, 1.8f) };
                mb.AddLathe(Vector3.zero, prof, 4, 1f, 45f, 405f);
                var r = MeshFactory.FromBuilder("Roof", mb, roof, go.transform);
                r.transform.localPosition = new Vector3(0, y + 5f, 0);
                y += 6.6f;
            }
            MeshFactory.Cylinder("Spire", go.transform, new Vector3(0, y, 0), 0.4f, 9f, roof, 8, false);
        }

        static void ArchTowerShapes(GameObject go, Material mat, Material lights)
        {
            // Outer profile (half), then the inner parabolic cut-out as a separate prism pair.
            float H = 150f, W = 30f;
            var mb = new MeshBuilder();
            var lower = new List<Vector2> { new Vector2(-W / 2, 0), new Vector2(W / 2, 0), new Vector2(W / 2 * 0.95f, H * 0.62f), new Vector2(-W / 2 * 0.95f, H * 0.62f) };
            mb.AddExtrudedXY(lower, 18f, Vector3.zero, Quaternion.identity, 4f);
            // Two horns with the parabolic gap between them.
            for (int side = -1; side <= 1; side += 2)
            {
                var horn = new List<Vector2>();
                for (int i = 0; i <= 12; i++)
                {
                    float t = i / 12f;
                    float y = H * 0.62f + t * H * 0.38f;
                    horn.Add(new Vector2(side * Mathf.Lerp(W / 2 * 0.95f, W * 0.18f, t * t), y));
                }
                for (int i = 12; i >= 0; i--)
                {
                    float t = i / 12f;
                    float y = H * 0.62f + t * H * 0.38f;
                    horn.Add(new Vector2(side * Mathf.Lerp(W * 0.12f, W * 0.17f, t) * Mathf.Sqrt(Mathf.Max(0.05f, t)), y));
                }
                if (side > 0) horn.Reverse();
                mb.AddExtrudedXY(horn, 14f, Vector3.zero, Quaternion.identity, 4f);
            }
            mb.AddBox(new Vector3(0, H * 0.86f, 0), new Vector3(W * 0.42f, 3f, 6f)); // sky bridge
            MeshFactory.FromBuilder("ArchTower", mb, mat, go.transform);
            MeshFactory.Box("BridgeGlow", go.transform, new Vector3(0, H * 0.86f, -3.1f), new Vector3(W * 0.4f, 1.2f, 0.2f), lights, false);
        }

        static void PavilionShapes(GameObject go, Material mat)
        {
            var mb = new MeshBuilder();
            mb.AddBox(new Vector3(0, 3, 0), new Vector3(70, 6, 70));                  // plinth
            mb.AddBox(new Vector3(0, 19, 0), new Vector3(40, 26, 40));                // main hall
            var dome = new List<Vector2>();
            for (int i = 0; i <= 14; i++)
            {
                float t = i / 14f;
                float a = t * Mathf.PI * 0.62f;
                float r = Mathf.Sin(a + 0.45f) * 15.5f * (1f - t * 0.25f);
                dome.Add(new Vector2(Mathf.Max(0.2f, r * (1f - Mathf.Pow(t, 6f))), 32f + t * 30f));
            }
            dome.Add(new Vector2(0.05f, 63.5f));
            mb.AddLathe(Vector3.zero, dome, 24);
            mb.AddCylinder(new Vector3(0, 32, 0), 12f, 4f, 24);                       // drum
            foreach (var c in new[] { new Vector3(32, 6, 32), new Vector3(-32, 6, 32), new Vector3(32, 6, -32), new Vector3(-32, 6, -32) })
            {
                mb.AddFrustum(c, 2.3f, 1.8f, 40f, 12);
                mb.AddSphere(c + Vector3.up * 42f, 2.4f, 10, 6, 1f, 0f, 90f);
            }
            foreach (var c in new[] { new Vector3(13, 32, 13), new Vector3(-13, 32, 13), new Vector3(13, 32, -13), new Vector3(-13, 32, -13) })
                mb.AddSphere(c + Vector3.up * 2f, 4f, 12, 6, 1f, 0f, 90f);
            MeshFactory.FromBuilder("Pavilion", mb, mat, go.transform);
        }

        static void ClockTowerShapes(GameObject go, Material mat, Material face)
        {
            var mb = new MeshBuilder();
            mb.AddBox(new Vector3(0, 30, 0), new Vector3(12, 60, 12));
            mb.AddBox(new Vector3(0, 66, 0), new Vector3(14, 12, 14));
            mb.AddBox(new Vector3(0, 74, 0), new Vector3(11, 4, 11));
            var spire = new List<Vector2> { new Vector2(7.5f, 76f), new Vector2(5.5f, 84f), new Vector2(2f, 96f), new Vector2(0.3f, 104f) };
            mb.AddLathe(Vector3.zero, spire, 4, 1f, 45f, 405f);
            MeshFactory.FromBuilder("Tower", mb, mat, go.transform);
            foreach (var r in new[] { 0f, 90f, 180f, 270f })
            {
                var q = Quaternion.Euler(0, r, 0);
                MeshFactory.Make("Face", MeshFactory.QuadMesh(9f, 9f), face, go.transform, q * new Vector3(0, 66, -7.05f), q);
            }
        }

        static void GateShapes(GameObject go, Material mat)
        {
            var mb = new MeshBuilder();
            for (int i = 0; i < 6; i++)
            {
                float x = -12.5f + i * 5f;
                mb.AddBox(new Vector3(x, 0.6f, 0), new Vector3(2.6f, 1.2f, 2.6f));
                mb.AddCylinder(new Vector3(x, 1.2f, 0), 0.95f, 13f, 16);
                mb.AddBox(new Vector3(x, 14.4f, 0), new Vector3(2.4f, 0.6f, 2.4f));
            }
            mb.AddBox(new Vector3(0, 15.8f, 0), new Vector3(30f, 2.2f, 5f));
            mb.AddBox(new Vector3(0, 17.9f, 0), new Vector3(28f, 2f, 4.5f));
            mb.AddBox(new Vector3(0, 19.8f, 0), new Vector3(9f, 1.8f, 3f));
            mb.AddBox(new Vector3(0, 21.5f, 0), new Vector3(5f, 1.6f, 2f));
            // Side wings.
            mb.AddBox(new Vector3(-19f, 6f, 0), new Vector3(6f, 12f, 4f));
            mb.AddBox(new Vector3(19f, 6f, 0), new Vector3(6f, 12f, 4f));
            MeshFactory.FromBuilder("Gate", mb, mat, go.transform, ColliderKind.None);
        }
    }
}

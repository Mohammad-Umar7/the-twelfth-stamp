using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Collects boxes per material and bakes each set into one mesh: an authored-looking facade is a few hundred parts,
    /// which as separate objects would cost a few hundred draw calls. Colliders are added separately by the caller.
    /// </summary>
    public class KitBatch
    {
        readonly Dictionary<Material, MeshBuilder> m_Sets = new Dictionary<Material, MeshBuilder>();

        public MeshBuilder Of(Material m)
        {
            if (!m_Sets.TryGetValue(m, out var b)) { b = new MeshBuilder(); m_Sets[m] = b; }
            return b;
        }

        public void Box(Material m, Vector3 center, Vector3 size, float uv = 1f) => Of(m).AddBox(center, size, Quaternion.identity, uv);
        public void Box(Material m, Vector3 center, Vector3 size, Quaternion rot, float uv = 1f) => Of(m).AddBox(center, size, rot, uv);

        /// <summary>One child per material under <paramref name="parent"/>. Emissive glass casts no shadow.</summary>
        public void Bake(Transform parent, string name, bool shadows = true)
        {
            foreach (var kv in m_Sets)
            {
                var go = MeshFactory.FromBuilder(name + "_" + kv.Key.name, kv.Value, kv.Key, parent);
                var r = go.GetComponent<MeshRenderer>();
                bool glow = kv.Key.IsKeywordEnabled("_EMISSION") && kv.Key.HasProperty("_EmissionColor") && kv.Key.GetColor("_EmissionColor").maxColorComponent > 0.2f;
                r.shadowCastingMode = shadows && !glow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            m_Sets.Clear();
        }
    }

    /// <summary>
    /// The urban kit (Paris first, reusable for London and Berlin): facades with depth, doors, windows, balconies and
    /// roofs; pavement edges, railings, festival booths and selective clutter. Every builder faces local -Z (front) like
    /// Kit.Facade and keeps gameplay colliders to a few simple boxes.
    /// </summary>
    public static class UrbanKit
    {
        public class FacadeMats
        {
            public Material Stone, StoneBase, Trim, Zinc, Iron, Frame, GlassDark, GlassLit, Chimney, ShopGlow;
            public Material[] Fascias, Awnings;
            public Material[] Goods;           // coloured stock on the shop shelves (optional)
            public Material ShopLamp, ShopGlass; // strip light and pendants inside, the glass pane in front (optional)
            public float StoneUv = 4f;   // metres per texture repeat: ~1 m x 0.5 m ashlar blocks
        }

        /// <summary>
        /// A Haussmann-style block: a rusticated ground floor with lit shopfronts, fascias and canvas awnings; four stone
        /// floors with framed French windows, wrought-iron balconettes and continuous balconies on the second and fifth
        /// floors; a deep cornice; a zinc mansard with dormers and chimney stacks. Returns the root. The storeys above
        /// the first floor are a separate child (<paramref name="upper"/>) so a cell can animate them as one piece.
        /// </summary>
        public static GameObject Haussmann(Transform parent, string name, Vector3 pos, float yaw, int bays, FacadeMats m, System.Random rnd, string[] shopNames, out Transform upper, out List<Vector3> shopLights)
        {
            const float bay = 2.4f, ground = 4.4f, floorH = 3.0f;
            float w = bays * bay;
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            root.localRotation = Quaternion.Euler(0, yaw, 0);
            var lower = new KitBatch();
            var top = new KitBatch();
            upper = new GameObject("Upper").transform;
            upper.SetParent(root, false);
            upper.localPosition = new Vector3(0f, ground + floorH, 0f); // pivot at the second-floor line
            shopLights = new List<Vector3>();

            // ---- ground floor: rusticated base, shopfronts, fascias, awnings
            lower.Box(m.StoneBase, new Vector3(0, ground / 2, 0.3f), new Vector3(w, ground, 0.6f), m.StoneUv);
            for (int k = 1; k < 6; k++) // rustication grooves
                lower.Box(m.Frame, new Vector3(0, k * ground / 6f, -0.006f), new Vector3(w, 0.025f, 0.012f));
            int shop = 0;
            for (int b = 0; b < bays; b++)
            {
                float x = -w / 2 + (b + 0.5f) * bay;
                bool door = b % 3 == 1;
                if (door)
                {
                    lower.Box(m.Frame, new Vector3(x, 1.45f, -0.03f), new Vector3(1.3f, 2.9f, 0.06f));
                    lower.Box(m.GlassDark, new Vector3(x, 2.15f, -0.065f), new Vector3(1.0f, 1.2f, 0.02f));
                    lower.Box(m.Trim, new Vector3(x, 3.05f, -0.06f), new Vector3(1.5f, 0.18f, 0.12f));
                    continue;
                }
                // Shop window: a lit room seen through glass. A dim warm back wall; a strip light and two pendants at
                // the top; two shelves of coloured stock; a counter in silhouette; a window display on the stall riser;
                // mullions and a faint glass sheen in front. (Playtest: the old single glowing panel read as blank.)
                var goods = m.Goods != null && m.Goods.Length > 0 ? m.Goods : new[] { m.Frame };
                var lamp = m.ShopLamp ?? m.GlassLit;
                lower.Box(m.ShopGlow, new Vector3(x, 1.75f, -0.012f), new Vector3(2.0f, 2.3f, 0.02f));
                lower.Box(lamp, new Vector3(x, 2.8f, -0.035f), new Vector3(1.7f, 0.045f, 0.03f));
                foreach (float px in new[] { -0.5f, 0.5f })
                {
                    lower.Box(m.Frame, new Vector3(x + px, 2.67f, -0.06f), new Vector3(0.012f, 0.22f, 0.012f));
                    lower.Box(lamp, new Vector3(x + px, 2.53f, -0.06f), new Vector3(0.17f, 0.08f, 0.06f));
                }
                for (int sh = 0; sh < 2; sh++)
                {
                    float sy = 1.45f + sh * 0.55f;
                    lower.Box(m.Frame, new Vector3(x, sy, -0.05f), new Vector3(1.9f, 0.035f, 0.07f));
                    float gx = -0.88f;
                    while (gx < 0.82f)
                    {
                        float gw = 0.06f + (float)rnd.NextDouble() * 0.13f, gh = 0.1f + (float)rnd.NextDouble() * 0.24f;
                        if (rnd.NextDouble() < 0.85)
                            lower.Box(goods[rnd.Next(goods.Length)], new Vector3(x + gx + gw / 2f, sy + 0.018f + gh / 2f, -0.05f), new Vector3(gw, gh, 0.05f));
                        gx += gw + 0.02f + (float)rnd.NextDouble() * 0.05f;
                    }
                }
                lower.Box(m.Frame, new Vector3(x - 0.35f, 0.95f, -0.03f), new Vector3(0.9f, 0.55f, 0.02f));
                lower.Box(m.Frame, new Vector3(x, 0.32f, -0.07f), new Vector3(2.1f, 0.64f, 0.14f));
                for (int d = 0; d < 3; d++)
                {
                    float dx = -0.6f + d * 0.6f + (float)(rnd.NextDouble() - 0.5) * 0.2f, dh = 0.1f + (float)rnd.NextDouble() * 0.26f;
                    lower.Box(goods[rnd.Next(goods.Length)], new Vector3(x + dx, 0.64f + dh / 2f, -0.06f), new Vector3(0.22f, dh, 0.08f));
                }
                foreach (float mx in new[] { -1.02f, -0.34f, 0.34f, 1.02f })
                    lower.Box(m.Frame, new Vector3(x + mx, 1.75f, -0.11f), new Vector3(0.05f, 2.3f, 0.04f));
                lower.Box(m.Frame, new Vector3(x, 2.92f, -0.11f), new Vector3(2.1f, 0.06f, 0.04f));
                if (m.ShopGlass != null) lower.Box(m.ShopGlass, new Vector3(x, 1.75f, -0.125f), new Vector3(2.0f, 2.3f, 0.004f));
                var fascia = m.Fascias[(shop + b) % m.Fascias.Length];
                lower.Box(fascia, new Vector3(x, 3.32f, -0.07f), new Vector3(2.3f, 0.5f, 0.12f));
                var awn = m.Awnings[(shop + b) % m.Awnings.Length];
                lower.Box(awn, new Vector3(x, 2.98f, -0.62f), new Vector3(2.2f, 0.035f, 1.2f), Quaternion.Euler(-18f, 0, 0));
                lower.Box(awn, new Vector3(x, 2.79f, -1.18f), new Vector3(2.2f, 0.22f, 0.02f));
                if (shopNames != null && shop < shopNames.Length && !string.IsNullOrEmpty(shopNames[shop]))
                {
                    var t = TextFactory.World(root, shopNames[shop], 0.16f, new Color(0.86f, 0.72f, 0.42f), TextAlignmentOptions.Center, true, 2.2f);
                    t.transform.localPosition = new Vector3(x, 3.33f, -0.14f); // TMP reads from -Z, the street side
                    t.fontStyle = FontStyles.SmallCaps;
                }
                shopLights.Add(root.TransformPoint(new Vector3(x, 2.4f, -1.1f)) - parent.position);
                shop++;
            }
            lower.Box(m.Trim, new Vector3(0, ground - 0.1f, -0.12f), new Vector3(w + 0.1f, 0.22f, 0.26f), 2f); // entresol cornice

            // ---- first floor (on the lower piece so the street wall never moves)
            Floor(lower, m, w, bays, bay, ground, floorH, 1, rnd, Vector3.zero);
            // ---- floors two to four and the attic, on the upper piece (local origin = second-floor line)
            var o = new Vector3(0f, -(ground + floorH), 0f);
            for (int f = 2; f <= 4; f++) Floor(top, m, w, bays, bay, ground + (f - 1) * floorH, floorH, f, rnd, o);
            float corniceY = ground + 4 * floorH;
            top.Box(m.Trim, new Vector3(0, corniceY + 0.18f, -0.22f) + o, new Vector3(w + 0.3f, 0.36f, 0.5f), 2f);
            top.Box(m.Trim, new Vector3(0, corniceY + 0.02f, -0.1f) + o, new Vector3(w + 0.1f, 0.12f, 0.24f), 2f);
            // Attic storey behind a continuous balcony.
            float atticY = corniceY + 0.36f, atticH = 2.6f;
            top.Box(m.Stone, new Vector3(0, atticY + atticH / 2, 0.45f) + o, new Vector3(w, atticH, 0.5f), m.StoneUv);
            Balcony(top, m, new Vector3(0, atticY, -0.1f) + o, w, 0.55f);
            for (int b = 0; b < bays; b++)
            {
                float x = -w / 2 + (b + 0.5f) * bay;
                Window(top, m, new Vector3(x, atticY + 0.15f, 0.2f) + o, 1.0f, 1.9f, rnd.NextDouble() < 0.3);
            }
            // Zinc mansard with dormers and chimneys: the silhouette that says Paris against a lighter sky.
            float roofY = atticY + atticH;
            var slope = Quaternion.Euler(-24f, 0, 0);
            top.Box(m.Zinc, new Vector3(0, roofY + 1.25f, 0.95f) + o, new Vector3(w + 0.1f, 2.7f, 0.08f), slope, 2f);
            top.Box(m.Zinc, new Vector3(0, roofY + 2.55f, 2.3f) + o, new Vector3(w + 0.1f, 0.08f, 2.4f), 2f);
            top.Box(m.Trim, new Vector3(0, roofY + 0.05f, 0.42f) + o, new Vector3(w + 0.2f, 0.12f, 0.2f));
            for (int b = 0; b < bays; b++)
            {
                float x = -w / 2 + (b + 0.5f) * bay;
                var d = new Vector3(x, roofY + 0.4f, 0.55f) + o;
                top.Box(m.Zinc, d + new Vector3(0, 0.62f, 0.25f), new Vector3(0.95f, 1.25f, 0.7f));
                top.Box(rnd.NextDouble() < 0.35 ? m.GlassLit : m.GlassDark, d + new Vector3(0, 0.58f, -0.11f), new Vector3(0.6f, 0.85f, 0.02f));
                top.Box(m.Frame, d + new Vector3(0, 0.58f, -0.12f), new Vector3(0.04f, 0.85f, 0.03f));
                top.Box(m.Zinc, d + new Vector3(-0.27f, 1.38f, 0.25f), new Vector3(0.62f, 0.05f, 0.78f), Quaternion.Euler(0, 0, 32f));
                top.Box(m.Zinc, d + new Vector3(0.27f, 1.38f, 0.25f), new Vector3(0.62f, 0.05f, 0.78f), Quaternion.Euler(0, 0, -32f));
            }
            foreach (float cx in new[] { -w / 2 + 0.6f, w / 2 - 0.9f })
            {
                var c = new Vector3(cx, roofY + 3.2f, 1.9f) + o;
                top.Box(m.Chimney, c, new Vector3(0.7f, 1.6f, 0.45f));
                top.Box(m.Trim, c + new Vector3(0, 0.84f, 0), new Vector3(0.8f, 0.1f, 0.55f));
                for (int p = 0; p < 3; p++) top.Box(m.Chimney, c + new Vector3(-0.22f + p * 0.22f, 1.05f, 0), new Vector3(0.12f, 0.32f, 0.12f));
            }

            lower.Bake(root, name + "_Lower");
            top.Bake(upper, name + "_Upper");
            // One collider for the street wall (nobody climbs the facade).
            var col = root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, ground / 2, 0.3f);
            col.size = new Vector3(w, ground, 0.6f);
            return root.gameObject;
        }

        static void Floor(KitBatch k, FacadeMats m, float w, int bays, float bay, float y, float h, int index, System.Random rnd, Vector3 o)
        {
            k.Box(m.Stone, new Vector3(0, y + h / 2, 0.3f) + o, new Vector3(w, h, 0.6f), m.StoneUv);
            k.Box(m.Trim, new Vector3(0, y + 0.04f, -0.05f) + o, new Vector3(w + 0.04f, 0.1f, 0.12f), 2f); // string course
            if (index == 2) Balcony(k, m, new Vector3(0, y, -0.08f) + o, w, 0.6f);
            for (int b = 0; b < bays; b++)
            {
                float x = -w / 2 + (b + 0.5f) * bay;
                Window(k, m, new Vector3(x, y + 0.25f, 0f) + o, 1.1f, 2.05f, rnd.NextDouble() < 0.32);
                if (index != 2) Railing(k, m.Iron, new Vector3(x - 0.62f, y + 0.25f, -0.16f) + Vector3.zero, new Vector3(x + 0.62f, y + 0.25f, -0.16f), 0.75f, o);
                if (index == 2) k.Box(m.Trim, new Vector3(x, y + 2.55f, -0.09f) + o, new Vector3(1.5f, 0.32f, 0.16f)); // pediment
            }
        }

        static void Window(KitBatch k, FacadeMats m, Vector3 sill, float ww, float wh, bool lit)
        {
            // Surround (lintel, sill, jambs) proud of the wall, glass slightly behind it, a central mullion and transom.
            k.Box(lit ? m.GlassLit : m.GlassDark, sill + new Vector3(0, wh / 2, -0.01f), new Vector3(ww, wh, 0.02f));
            k.Box(m.Trim, sill + new Vector3(0, wh + 0.09f, -0.05f), new Vector3(ww + 0.32f, 0.18f, 0.1f));
            k.Box(m.Trim, sill + new Vector3(0, -0.04f, -0.07f), new Vector3(ww + 0.22f, 0.08f, 0.14f));
            k.Box(m.Trim, sill + new Vector3(-ww / 2 - 0.07f, wh / 2, -0.04f), new Vector3(0.14f, wh, 0.08f));
            k.Box(m.Trim, sill + new Vector3(ww / 2 + 0.07f, wh / 2, -0.04f), new Vector3(0.14f, wh, 0.08f));
            k.Box(m.Frame, sill + new Vector3(0, wh / 2, -0.025f), new Vector3(0.05f, wh, 0.03f));
            k.Box(m.Frame, sill + new Vector3(0, wh * 0.72f, -0.025f), new Vector3(ww, 0.04f, 0.03f));
        }

        static void Balcony(KitBatch k, FacadeMats m, Vector3 baseCenter, float width, float depth)
        {
            k.Box(m.Trim, baseCenter + new Vector3(0, 0.07f, -depth / 2), new Vector3(width + 0.1f, 0.14f, depth), 2f);
            float zf = baseCenter.z - depth + 0.04f;
            Railing(k, m.Iron, new Vector3(-width / 2, baseCenter.y + 0.14f, zf), new Vector3(width / 2, baseCenter.y + 0.14f, zf), 0.95f, Vector3.zero);
        }

        /// <summary>Wrought iron: posts every 12 cm with a top rail, a bottom rail and a mid band (batched).</summary>
        public static void Railing(KitBatch k, Material iron, Vector3 a, Vector3 b, float height, Vector3 offset)
        {
            a += offset; b += offset;
            float len = Vector3.Distance(a, b);
            int n = Mathf.Max(2, Mathf.RoundToInt(len / 0.12f));
            for (int i = 0; i <= n; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)n);
                k.Box(iron, p + Vector3.up * height / 2, new Vector3(0.016f, height, 0.016f));
            }
            var mid = (a + b) / 2;
            var rot = Quaternion.LookRotation((b - a).normalized, Vector3.up);
            k.Box(iron, mid + Vector3.up * height, new Vector3(0.045f, 0.035f, len), rot);
            k.Box(iron, mid + Vector3.up * 0.06f, new Vector3(0.03f, 0.025f, len), rot);
            k.Box(iron, mid + Vector3.up * height * 0.78f, new Vector3(0.02f, 0.02f, len), rot);
        }

        /// <summary>
        /// The festival's freestanding poster board (the Paris seam lives on its face): a framed panel on two posts with a
        /// little roof, pasted with seal posters. Front faces local +Z of the returned root.
        /// </summary>
        public static GameObject PosterBoard(Transform parent, Vector3 pos, float yaw, Material frame, Material roof, Material paper, Texture2D[] posters, string header = null, string footer = null)
        {
            var root = new GameObject("FestivalPosterBoard").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            root.localRotation = Quaternion.Euler(0, yaw, 0);
            var k = new KitBatch();
            foreach (float x in new[] { -0.82f, 0.82f })
            {
                k.Box(frame, new Vector3(x, 1.2f, 0f), new Vector3(0.09f, 2.4f, 0.09f));
                k.Box(frame, new Vector3(x, 0.04f, 0f), new Vector3(0.28f, 0.08f, 0.32f));
            }
            k.Box(frame, new Vector3(0, 1.45f, 0f), new Vector3(1.66f, 1.9f, 0.07f));
            k.Box(paper, new Vector3(0, 1.45f, 0.037f), new Vector3(1.52f, 1.76f, 0.005f));
            k.Box(roof, new Vector3(0, 2.5f, 0.02f), new Vector3(1.86f, 0.06f, 0.36f), Quaternion.Euler(-8f, 0, 0));
            k.Bake(root, "Board");
            var col = root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 1.3f, 0f);
            col.size = new Vector3(1.8f, 2.6f, 0.12f);
            // Travel posters (landscape illustrations) pasted slightly askew under a printed festival header; each on its own
            // quad so the art stays crisp. (The old stamp seals had transparent backgrounds and read as solid red squares.)
            var spots = new[] { new Vector3(-0.38f, 1.86f, 0f), new Vector3(0.38f, 1.83f, 0f), new Vector3(-0.37f, 1.3f, 0f), new Vector3(0.39f, 1.27f, 0f) };
            for (int i = 0; i < spots.Length && posters != null && i < posters.Length; i++)
            {
                if (posters[i] == null) continue;
                var q = MeshFactory.Quad("Poster", root, 0.7f, 0.44f, Mats.Printed(posters[i], new Color(0.9f, 0.87f, 0.8f), 0.15f),
                    spots[i] + new Vector3(0, 0, 0.042f), Quaternion.Euler(0, 180f, (i % 2 == 0 ? 1 : -1) * (1.5f + i * 0.7f)));
                q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (!string.IsNullOrEmpty(header))
            {
                var t = TextFactory.World(root, header, 0.08f, new Color(0.55f, 0.1f, 0.08f), TextAlignmentOptions.Center, true, 1.45f);
                t.transform.localPosition = new Vector3(0f, 2.23f, 0.043f);
                t.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                t.fontStyle = FontStyles.Bold;
            }
            if (!string.IsNullOrEmpty(footer))
            {
                var t = TextFactory.World(root, footer, 0.04f, new Color(0.16f, 0.16f, 0.2f), TextAlignmentOptions.Center, false, 1.45f);
                t.transform.localPosition = new Vector3(0f, 0.86f, 0.043f);
                t.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
            return root.gameObject;
        }

        /// <summary>A Paris advertising column: green iron plinth and cornice, a drum of pasted posters, a dome and finial.</summary>
        public static GameObject MorrisColumn(Transform parent, Vector3 pos, Material iron, Material core, Material[] posters, System.Random rnd, string[] bills = null)
        {
            var root = new GameObject("MorrisColumn").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            var k = new KitBatch();
            k.Of(iron).AddCylinder(Vector3.zero, 0.62f, 0.16f, 24);
            k.Of(iron).AddCylinder(new Vector3(0f, 0.16f, 0f), 0.55f, 0.24f, 24);
            k.Of(core).AddCylinder(new Vector3(0f, 0.4f, 0f), 0.49f, 2.5f, 24);
            const int panels = 14;
            for (int i = 0; i < panels; i++)
            {
                var rot = Quaternion.Euler(0f, i * 360f / panels + (float)rnd.NextDouble() * 4f, 0f);
                float h = 0.9f + (float)rnd.NextDouble() * 1.4f, y0 = 0.45f + (float)rnd.NextDouble() * (2.4f - h);
                k.Box(posters[rnd.Next(posters.Length)], rot * new Vector3(0f, 0f, 0.5f) + Vector3.up * (y0 + h / 2f), new Vector3(0.24f, h, 0.012f), rot);
            }
            k.Of(iron).AddFrustum(new Vector3(0f, 2.9f, 0f), 0.5f, 0.62f, 0.14f, 24);
            k.Of(iron).AddCylinder(new Vector3(0f, 3.04f, 0f), 0.62f, 0.08f, 24);
            k.Of(iron).AddSphere(new Vector3(0f, 3.12f, 0f), 0.56f, 20, 12, 1f, 0f, 90f);
            k.Of(iron).AddCylinder(new Vector3(0f, 3.66f, 0f), 0.05f, 0.14f, 10);
            k.Of(iron).AddSphere(new Vector3(0f, 3.86f, 0f), 0.07f, 10, 6);
            k.Bake(root, "Morris");
            if (bills != null)
                for (int i = 0; i < bills.Length; i++)
                {
                    var rot = Quaternion.Euler(0f, 200f + i * 70f, 0f);
                    var t = TextFactory.World(root, bills[i], 0.065f, new Color(0.12f, 0.1f, 0.1f), TextAlignmentOptions.Center, true, 0.55f);
                    t.transform.localPosition = rot * new Vector3(0f, 0f, 0.52f) + Vector3.up * (2.55f - i * 0.5f);
                    t.transform.localRotation = rot * Quaternion.Euler(0f, 180f, 0f); // TMP reads from local -Z: face outward
                }
            var col = root.gameObject.AddComponent<CapsuleCollider>();
            col.radius = 0.6f; col.height = 3.4f; col.center = new Vector3(0f, 1.7f, 0f);
            return root.gameObject;
        }

        /// <summary>A cast-iron drinking fountain: plinth, four slender columns under a small dome, a thin stream of water.</summary>
        public static GameObject WallaceFountain(Transform parent, Vector3 pos, Material iron, Material water)
        {
            var root = new GameObject("WallaceFountain").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            var k = new KitBatch();
            k.Of(iron).AddCylinder(Vector3.zero, 0.38f, 0.22f, 8);
            k.Of(iron).AddFrustum(new Vector3(0f, 0.22f, 0f), 0.3f, 0.22f, 0.5f, 8);
            k.Of(iron).AddCylinder(new Vector3(0f, 0.72f, 0f), 0.27f, 0.1f, 16);
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + i * 90f) * Mathf.Deg2Rad;
                var c = new Vector3(Mathf.Cos(a) * 0.17f, 0.82f, Mathf.Sin(a) * 0.17f);
                k.Of(iron).AddCylinder(c, 0.032f, 0.95f, 8);
                k.Of(iron).AddSphere(c + Vector3.up * 0.5f, 0.05f, 8, 6);
            }
            k.Of(iron).AddCylinder(new Vector3(0f, 1.77f, 0f), 0.28f, 0.07f, 16);
            k.Of(iron).AddSphere(new Vector3(0f, 1.84f, 0f), 0.25f, 16, 10, 1f, 0f, 90f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                k.Of(iron).AddFrustum(new Vector3(Mathf.Cos(a) * 0.27f, 1.84f, Mathf.Sin(a) * 0.27f), 0.02f, 0.002f, 0.09f, 6);
            }
            k.Of(iron).AddCylinder(new Vector3(0f, 2.09f, 0f), 0.03f, 0.12f, 8);
            k.Of(iron).AddSphere(new Vector3(0f, 2.24f, 0f), 0.045f, 8, 6);
            k.Of(water).AddCylinder(new Vector3(0f, 0.82f, 0f), 0.012f, 0.95f, 8);
            k.Bake(root, "Wallace");
            var col = root.gameObject.AddComponent<CapsuleCollider>();
            col.radius = 0.36f; col.height = 2.3f; col.center = new Vector3(0f, 1.15f, 0f);
            return root.gameObject;
        }

        /// <summary>A riverside bookseller's box on the parapet, shut and padlocked for the night (front faces local -Z).</summary>
        public static GameObject Bouquiniste(Transform parent, Vector3 pos, float yaw, Material green, Material metal)
        {
            var root = new GameObject("Bouquiniste").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var k = new KitBatch();
            k.Box(green, new Vector3(0f, 0.25f, 0f), new Vector3(1.9f, 0.5f, 0.5f));
            k.Box(green, new Vector3(0f, 0.56f, 0.02f), new Vector3(1.96f, 0.12f, 0.56f), Quaternion.Euler(-6f, 0f, 0f)); // sloped lid
            for (int i = -2; i <= 2; i++) k.Box(green, new Vector3(i * 0.42f, 0.25f, -0.256f), new Vector3(0.04f, 0.46f, 0.012f)); // ribs
            k.Box(metal, new Vector3(-0.5f, 0.38f, -0.27f), new Vector3(0.06f, 0.08f, 0.03f));
            k.Box(metal, new Vector3(0.5f, 0.38f, -0.27f), new Vector3(0.06f, 0.08f, 0.03f));
            k.Bake(root, "Bouquiniste");
            return root.gameObject;
        }

        /// <summary>Festival bunting: a sagging cord with small cloth pennants in the festival colours.</summary>
        public static GameObject Bunting(Transform parent, Vector3 a, Vector3 b, float sag, Material cord, Material[] cloth, int count = 18)
        {
            var root = new GameObject("Bunting").transform;
            root.SetParent(parent, false);
            var k = new KitBatch();
            Vector3 P(float t) => Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1f - t);
            int segs = 16;
            for (int i = 0; i < segs; i++)
            {
                var p0 = P(i / (float)segs); var p1 = P((i + 1) / (float)segs);
                k.Box(cord, (p0 + p1) / 2, new Vector3(0.012f, 0.012f, Vector3.Distance(p0, p1)), Quaternion.LookRotation((p1 - p0).normalized, Vector3.up));
            }
            var side = Vector3.Cross(Vector3.up, (b - a).normalized);
            for (int i = 1; i < count; i++)
            {
                var p = P(i / (float)count);
                var m = cloth[i % cloth.Length];
                k.Box(m, p + Vector3.down * 0.16f, new Vector3(0.2f, 0.3f, 0.006f), Quaternion.LookRotation(side, Vector3.up) * Quaternion.Euler(0, 0, (i % 2 == 0 ? 6f : -5f)));
            }
            k.Bake(root, "Bunting", false);
            return root.gameObject;
        }

        /// <summary>A café table with two chairs (wicker-coloured), for a terrace under a lit window.</summary>
        public static void CafeSet(Transform parent, Vector3 pos, float yaw, Material top, Material metal, Material wicker)
        {
            var root = new GameObject("CafeSet").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            root.localRotation = Quaternion.Euler(0, yaw, 0);
            var b = new MeshBuilder();
            b.AddCylinder(new Vector3(0, 0.72f, 0), 0.32f, 0.03f, 20);
            MeshFactory.FromBuilder("TableTop", b, top, root);
            MeshFactory.Cylinder("TableLeg", root, Vector3.zero, 0.025f, 0.72f, metal, 8, false);
            MeshFactory.Cylinder("TableFoot", root, Vector3.zero, 0.2f, 0.02f, metal, 16, false);
            var k = new KitBatch();
            foreach (var (x, ry) in new[] { (-0.55f, 90f), (0.55f, -90f) })
            {
                var r = Quaternion.Euler(0, ry, 0);
                var c = new Vector3(x, 0, 0.05f);
                k.Box(wicker, c + new Vector3(0, 0.45f, 0), new Vector3(0.42f, 0.05f, 0.42f), r);
                k.Box(wicker, c + r * new Vector3(0, 0.72f, 0.2f), new Vector3(0.42f, 0.5f, 0.04f), r);
                foreach (var lx in new[] { -0.18f, 0.18f }) foreach (var lz in new[] { -0.18f, 0.18f })
                    k.Box(metal, c + r * new Vector3(lx, 0.22f, lz), new Vector3(0.025f, 0.45f, 0.025f), r);
            }
            k.Bake(root, "Chairs");
            var col = root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.45f, 0.02f); col.size = new Vector3(1.5f, 0.9f, 0.7f);
        }

        /// <summary>Selective wet: a few dark glossy puddles instead of a mirror-wet street.</summary>
        public static void Puddles(Transform parent, Vector3 center, Vector2 area, int count, int seed, Material water)
        {
            var rnd = new System.Random(seed);
            var b = new MeshBuilder();
            for (int i = 0; i < count; i++)
            {
                var p = center + new Vector3((float)(rnd.NextDouble() - 0.5) * area.x, 0.004f, (float)(rnd.NextDouble() - 0.5) * area.y);
                float rx = 0.35f + (float)rnd.NextDouble() * 0.9f, rz = 0.25f + (float)rnd.NextDouble() * 0.6f;
                var poly = new List<Vector2>();
                for (int k = 0; k < 14; k++)
                {
                    float a = k / 14f * Mathf.PI * 2f;
                    float j = 0.75f + 0.25f * Mathf.Sin(a * 3f + i);
                    poly.Add(new Vector2(Mathf.Cos(a) * rx * j, Mathf.Sin(a) * rz * j));
                }
                b.AddPrism(poly, 0f, 0.002f, 1f, true, p, Quaternion.Euler(0, (float)rnd.NextDouble() * 180f, 0));
            }
            var go = MeshFactory.FromBuilder("Puddles", b, water, parent);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Far-bank buildings: fogged backdrop blocks with a few warm windows (cheap, unlit, dimmed for the fog).</summary>
        public static void FarBank(Transform parent, Vector3 origin, Vector3 along, int count, int seed, Material block, Material windows)
        {
            var rnd = new System.Random(seed);
            var k = new KitBatch();
            var wb = new MeshBuilder();
            var side = Vector3.Cross(Vector3.up, along.normalized);
            float x = 0f;
            for (int i = 0; i < count; i++)
            {
                float w = 8f + (float)rnd.NextDouble() * 9f, h = 14f + (float)rnd.NextDouble() * 10f;
                var c = origin + along.normalized * (x + w / 2) + Vector3.up * h / 2;
                k.Box(block, c, new Vector3(w, h, 9f), Quaternion.LookRotation(side, Vector3.up));
                // A mansard cap on every other block keeps the skyline Parisian.
                if (i % 2 == 0) k.Box(block, c + Vector3.up * (h / 2 + 1.2f), new Vector3(w * 0.92f, 2.4f, 7f), Quaternion.LookRotation(side, Vector3.up));
                int cols = Mathf.FloorToInt(w / 2.4f), rows = Mathf.FloorToInt(h / 3f) - 1;
                for (int r = 0; r < rows; r++)
                    for (int cc = 0; cc < cols; cc++)
                    {
                        if (rnd.NextDouble() > 0.22) continue;
                        var p = c - side * 4.55f + along.normalized * (-w / 2 + (cc + 0.5f) * w / cols) + Vector3.up * (-h / 2 + 2f + r * 3f);
                        var q = Quaternion.LookRotation(-side, Vector3.up);
                        wb.AddBox(p, new Vector3(0.9f, 1.4f, 0.05f), q);
                    }
                x += w + 1f + (float)rnd.NextDouble() * 2f;
            }
            k.Bake(parent, "FarBank", false);
            var go = MeshFactory.FromBuilder("FarBankWindows", wb, windows, parent);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>A river bridge silhouette: deck, cutwater piers and a line of lamps (backdrop materials).</summary>
        public static void Bridge(Transform parent, Vector3 a, Vector3 b, float deckY, Material stone, Material lamps)
        {
            var k = new KitBatch();
            var dir = (b - a);
            float len = dir.magnitude;
            var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            var mid = (a + b) / 2;
            k.Box(stone, mid + Vector3.up * deckY, new Vector3(3.2f, 1.6f, len), rot);
            k.Box(stone, mid + Vector3.up * (deckY + 1.2f), new Vector3(3.4f, 0.8f, len), rot);
            int piers = Mathf.Max(2, Mathf.RoundToInt(len / 14f));
            for (int i = 1; i < piers; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)piers);
                k.Box(stone, p + Vector3.up * (deckY / 2 - 0.5f), new Vector3(4.2f, deckY + 1f, 3.2f), rot);
            }
            var lb = new MeshBuilder();
            for (int i = 0; i <= piers * 2; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)(piers * 2)) + Vector3.up * (deckY + 3.4f);
                lb.AddBox(p, new Vector3(0.5f, 0.7f, 0.5f), rot);
            }
            k.Bake(parent, "Bridge", false);
            var go = MeshFactory.FromBuilder("BridgeLamps", lb, lamps, parent);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>A cloth festival banner with gold lettering (fictional event; no partner or real-world marks).</summary>
        public static GameObject Banner(Transform parent, Vector3 pos, float yaw, float width, float height, Material cloth, Material pole, string line1, string line2)
        {
            var root = new GameObject("FestivalBanner").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            root.localRotation = Quaternion.Euler(0, yaw, 0);
            var k = new KitBatch();
            k.Box(cloth, new Vector3(0, height / 2, 0), new Vector3(width, height, 0.02f));
            k.Box(pole, new Vector3(0, height + 0.03f, 0), new Vector3(width + 0.2f, 0.04f, 0.04f));
            k.Bake(root, "Banner", false);
            var gold = new Color(0.9f, 0.76f, 0.45f);
            var t1 = TextFactory.World(root, line1, 0.15f, gold, TextAlignmentOptions.Center, true, width - 0.2f);
            t1.transform.localPosition = new Vector3(0, height * 0.62f, -0.015f);
            var t2 = TextFactory.World(root, line2, 0.09f, gold, TextAlignmentOptions.Center, true, width - 0.2f);
            t2.transform.localPosition = new Vector3(0, height * 0.3f, -0.015f);
            t2.characterSpacing = 8f;
            return root.gameObject;
        }

        /// <summary>Paper scattered on the ground: discarded festival programmes and torn seal stickers.</summary>
        public static void Litter(Transform parent, Vector3 center, Vector2 area, int count, int seed, Texture2D[] prints)
        {
            var rnd = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                var tex = prints[rnd.Next(prints.Length)];
                var p = center + new Vector3((float)(rnd.NextDouble() - 0.5) * area.x, 0.006f + i * 0.0004f, (float)(rnd.NextDouble() - 0.5) * area.y);
                float s = 0.16f + (float)rnd.NextDouble() * 0.12f;
                var q = MeshFactory.Quad("Programme", parent, s, s * 1.35f, Mats.Printed(tex, new Color(0.7f, 0.68f, 0.62f), 0.35f), p,
                    Quaternion.Euler(90f, (float)rnd.NextDouble() * 360f, 0));
                q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>Materials of the Agra garden detail: red sandstone, white marble with pietra-dura inlay, brass, wood.</summary>
    class GardenMats
    {
        public Material Marble, Sandstone, Recess, Brass, Wood;
        public Material Carnelian, Jade, Lapis, Onyx;
        public Material[] Inlay => new[] { Carnelian, Jade, Lapis };
    }

    public partial class AgraCell
    {
        // Bulbous Mughal dome, radius 1 at the base (bottom to top, so the lathe faces outward).
        static readonly Vector2[] s_Dome =
        {
            new Vector2(0.98f, 0f), new Vector2(1.07f, 0.1f), new Vector2(1.12f, 0.26f), new Vector2(1.09f, 0.44f), new Vector2(0.98f, 0.62f),
            new Vector2(0.8f, 0.8f), new Vector2(0.56f, 0.97f), new Vector2(0.32f, 1.12f), new Vector2(0.14f, 1.27f), new Vector2(0.05f, 1.38f),
            new Vector2(0.001f, 1.44f),
        };

        /// <summary>
        /// The garden's enclosure, seen past the cypress rows and behind the arrival: red sandstone walls with blind
        /// arched niches outlined in marble, a marble coping and a stepped parapet; a pavilion at each end of the cross
        /// axis; octagonal corner towers that frame the far vista; and the gateway the player arrives through. All of it
        /// stands outside the playable bounds, has no colliders and goes in the caller's no-shadow batch (a low sun would
        /// put the whole garden in the walls' shade).
        /// </summary>
        void BuildGardenWalls(KitBatch k, GardenMats m)
        {
            const float wallH = 3.0f;
            // Side walls (inner faces at x = +-8.9) with niches, the cross-axis pavilions and the far corner towers; all of
            // it stops short of z = 16, where the vista begins.
            const float farEnd = 14.9f;
            foreach (float sx in new[] { -1f, 1f })
            {
                float zc = (-7.9f + farEnd) * 0.5f, len = farEnd + 7.9f;
                var normal = new Vector3(-sx, 0f, 0f);
                float face = sx * 8.9f;
                k.Box(m.Sandstone, new Vector3(sx * 9.2f, wallH * 0.5f, zc), new Vector3(0.6f, wallH, len), 2.5f);
                k.Box(m.Marble, new Vector3(face - sx * 0.02f, 0.22f, zc), new Vector3(0.04f, 0.44f, len));          // dado
                k.Box(m.Recess, new Vector3(face - sx * 0.04f, 2.85f, zc), new Vector3(0.08f, 0.1f, len));           // string course
                k.Box(m.Marble, new Vector3(sx * 9.2f, wallH + 0.05f, zc), new Vector3(0.74f, 0.1f, len));           // coping
                Parapet(k, m, new Vector3(sx * 9.0f, wallH + 0.1f, -7.6f), new Vector3(sx * 9.0f, wallH + 0.1f, 4.2f));
                Parapet(k, m, new Vector3(sx * 9.0f, wallH + 0.1f, 6.8f), new Vector3(sx * 9.0f, wallH + 0.1f, farEnd - 1.0f));
                for (int i = 0; i < 10; i++)
                {
                    float z = -6.6f + i * 2.4f;
                    if (Mathf.Abs(z - 5.5f) < 1.6f || z > farEnd - 1.4f) continue; // the pavilion bay and the corner tower
                    Niche(k, m, new Vector3(face, 0.46f, z), normal, 1.3f, 1.3f, true);
                }
                // Cross-axis pavilion: a projecting bay with a deep niche, a chhatri on top.
                var bay = new Vector3(sx * 8.85f, 0f, 5.5f);
                k.Box(m.Sandstone, bay + new Vector3(0f, 1.75f, 0f), new Vector3(1.3f, 3.5f, 2.2f), 2.5f);
                k.Box(m.Marble, bay + new Vector3(0f, 3.53f, 0f), new Vector3(1.4f, 0.08f, 2.3f));
                Niche(k, m, new Vector3(sx * 8.2f, 0.3f, 5.5f), normal, 1.25f, 1.7f, true);
                Chhatri(k, m, bay + new Vector3(0f, 3.57f, 0f), 0.68f);
                // Octagonal corner tower closing the far end of each wall, framing the domed pavilion in the vista.
                var tw = new Vector3(sx * 9.2f, 0f, farEnd);
                var oct = Quaternion.Euler(0f, 22.5f, 0f);
                k.Of(m.Sandstone).AddCylinder(tw, 1.05f, 4.2f, 8, 2.5f, oct);
                foreach (float y in new[] { 0f, 1.35f, 2.95f })
                    k.Of(y == 0f ? m.Marble : m.Recess).AddCylinder(tw + Vector3.up * (y == 0f ? 0f : y), 1.09f, y == 0f ? 0.44f : 0.12f, 8, 1f, oct);
                k.Of(m.Marble).AddCylinder(tw + Vector3.up * 4.2f, 1.22f, 0.16f, 8, 1f, oct);
                for (int s = 0; s < 8; s++)
                {
                    // A small blind arch on each face of the octagon.
                    float a = (s * 45f) * Mathf.Deg2Rad;
                    var n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    if (Vector3.Dot(n, normal) < -0.2f) continue; // faces turned away from the garden
                    Niche(k, m, tw + n * 0.99f + Vector3.up * 1.6f, n, 0.5f, 0.75f, false);
                }
                Chhatri(k, m, tw + Vector3.up * 4.36f, 0.86f);
            }
            // South wall behind the arrival, with the gateway on the garden axis.
            {
                var normal = Vector3.forward;
                k.Box(m.Sandstone, new Vector3(0f, wallH * 0.5f, -7.6f), new Vector3(19.0f, wallH, 0.6f), 2.5f);
                k.Box(m.Marble, new Vector3(0f, 0.22f, -7.28f), new Vector3(19.0f, 0.44f, 0.04f));
                k.Box(m.Recess, new Vector3(0f, 2.85f, -7.26f), new Vector3(19.0f, 0.1f, 0.08f));
                k.Box(m.Marble, new Vector3(0f, wallH + 0.05f, -7.6f), new Vector3(19.1f, 0.1f, 0.74f));
                Parapet(k, m, new Vector3(-9.3f, wallH + 0.1f, -7.45f), new Vector3(-3.1f, wallH + 0.1f, -7.45f));
                Parapet(k, m, new Vector3(3.1f, wallH + 0.1f, -7.45f), new Vector3(9.3f, wallH + 0.1f, -7.45f));
                foreach (float x in new[] { -7.4f, -5.0f, 5.0f, 7.4f })
                    Niche(k, m, new Vector3(x, 0.46f, -7.3f), normal, 1.3f, 1.3f, true);
                Gateway(k, m);
            }
        }

        /// <summary>
        /// Footing for the enclosure now that the play area runs north past it to the domed pavilion: the side walls (and the
        /// strip outside them, out past the play bounds), the corner towers and the pavilion bays stop the player, as the
        /// bounds used to. The opening between the towers (x -8.15..8.15) stays clear.
        /// </summary>
        void AddEnclosureColliders(Transform g)
        {
            var root = new GameObject("EnclosureColliders").transform;
            root.SetParent(g, false);
            T Part<T>(string name, Vector3 at) where T : Collider
            {
                var go = new GameObject(name);
                go.transform.SetParent(root, false);
                go.transform.localPosition = at;
                return go.AddComponent<T>();
            }
            foreach (float sx in new[] { -1f, 1f })
            {
                Part<BoxCollider>("SideWall", new Vector3(sx * 10.75f, 1.5f, 4f)).size = new Vector3(3.7f, 3f, 23.9f); // x 8.9..12.6, z -7.95..15.95
                Part<BoxCollider>("PavilionBay", new Vector3(sx * 8.85f, 1.75f, 5.5f)).size = new Vector3(1.3f, 3.5f, 2.2f);
                var tower = Part<CapsuleCollider>("CornerTower", new Vector3(sx * 9.2f, 2.1f, 14.9f));
                tower.radius = 1.05f;
                tower.height = 4.2f;
            }
        }

        /// <summary>The arrival gateway: a tall pishtaq with a pointed arch over closed studded doors, a jali screen,
        /// inlaid roundels, a stepped parapet, corner pinnacles and two small chhatris. Faces the garden (+Z).</summary>
        void Gateway(KitBatch k, GardenMats m)
        {
            const float face = -7.18f, H = 6.6f;
            k.Box(m.Sandstone, new Vector3(0f, H * 0.5f, -7.75f), new Vector3(5.8f, H, 1.15f), 2.5f);
            k.Box(m.Marble, new Vector3(0f, H + 0.05f, -7.75f), new Vector3(5.95f, 0.1f, 1.3f));
            Parapet(k, m, new Vector3(-1.7f, H + 0.1f, -7.3f), new Vector3(1.7f, H + 0.1f, -7.3f));
            // Rectangular frame (alfiz) of marble with an inner line of inlay.
            Vector3 W(float x, float y) => new Vector3(x, y, face);
            foreach (var (w, mat, d) in new[] { (2.35f, m.Marble, 0.03f), (2.18f, m.Onyx, 0.035f) })
            {
                float wd = mat == m.Marble ? 0.16f : 0.035f;
                DetailShapes.PlaneStrip(k, mat, W(-w, 0.2f), W(-w, 6.0f), Vector3.forward, wd, d);
                DetailShapes.PlaneStrip(k, mat, W(w, 0.2f), W(w, 6.0f), Vector3.forward, wd, d);
                DetailShapes.PlaneStrip(k, mat, W(-w - wd * 0.5f, 6.0f), W(w + wd * 0.5f, 6.0f), Vector3.forward, wd, d);
            }
            // The arch: a darker recess with a marble outline, studded doors and a jali screen under the apex.
            var arch = DetailShapes.PointedArch(3.3f, 3.0f, 0.32f, 9);
            var rot = Quaternion.LookRotation(-Vector3.forward, Vector3.up);
            k.Of(m.Recess).AddExtrudedXY(arch, 0.03f, new Vector3(0f, 0f, face + 0.01f), rot, 1.5f);
            DetailShapes.Outline(k, m.Marble, arch, v => W(v.x, v.y) + Vector3.forward * 0.03f, Vector3.forward, 0.14f, 0.05f);
            DetailShapes.Outline(k, m.Lapis, Inset(arch, 0.12f), v => W(v.x, v.y) + Vector3.forward * 0.035f, Vector3.forward, 0.03f, 0.04f);
            k.Box(m.Wood, new Vector3(0f, 1.6f, face + 0.05f), new Vector3(2.4f, 3.2f, 0.06f), 1.2f);
            k.Box(m.Onyx, new Vector3(0f, 1.6f, face + 0.085f), new Vector3(0.03f, 3.2f, 0.02f));
            for (int leaf = -1; leaf <= 1; leaf += 2)
            {
                for (int r = 0; r < 8; r++)
                    for (int c = 0; c < 4; c++)
                        k.Box(m.Brass, new Vector3(leaf * (0.18f + c * 0.27f), 0.35f + r * 0.4f, face + 0.09f), new Vector3(0.05f, 0.05f, 0.03f));
                k.Of(m.Brass).AddTorus(new Vector3(leaf * 0.16f, 1.45f, face + 0.11f), 0.07f, 0.012f, 12, 5, Quaternion.Euler(90f, 0f, 0f));
                k.Box(m.Brass, new Vector3(leaf * (1.2f - 0.02f), 1.6f, face + 0.085f), new Vector3(0.04f, 3.2f, 0.02f));
            }
            k.Box(m.Marble, new Vector3(0f, 3.9f, face + 0.04f), new Vector3(1.9f, 1.3f, 0.05f));
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 7; c++)
                    k.Box(m.Recess, new Vector3(-0.72f + c * 0.24f, 3.42f + r * 0.32f, face + 0.07f), new Vector3(0.15f, 0.2f, 0.02f), Quaternion.Euler(0f, 0f, 45f));
            // Inlaid roundels in the spandrels and a band of lozenges above the arch.
            foreach (float x in new[] { -1.85f, 1.85f })
            {
                var c = new Vector3(x, 5.35f, face + 0.02f);
                k.Of(m.Marble).AddDisc(c, Vector3.forward, 0.3f, 18);
                k.Of(m.Lapis).AddDisc(c + Vector3.forward * 0.006f, Vector3.forward, 0.2f, 16);
                k.Of(m.Carnelian).AddDisc(c + Vector3.forward * 0.012f, Vector3.forward, 0.09f, 12);
            }
            for (int i = 0; i < 11; i++)
                k.Box(i % 2 == 0 ? m.Marble : m.Jade, new Vector3(-2.0f + i * 0.4f, 5.78f, face + 0.02f), new Vector3(0.17f, 0.17f, 0.03f), Quaternion.Euler(0f, 0f, 45f));
            // Corner pinnacles (guldasta) and two chhatris on the parapet.
            foreach (float x in new[] { -2.95f, 2.95f })
            {
                var b = new Vector3(x, 0f, -7.2f);
                k.Of(m.Sandstone).AddCylinder(b, 0.17f, 7.3f, 8, 2f, Quaternion.Euler(0f, 22.5f, 0f));
                foreach (float y in new[] { 1.4f, 3.3f, 5.2f, 6.7f }) k.Of(m.Marble).AddCylinder(b + Vector3.up * y, 0.2f, 0.07f, 8, 1f, Quaternion.Euler(0f, 22.5f, 0f));
                var top = new List<Vector2>();
                foreach (var p in s_Dome) top.Add(p * 0.2f);
                k.Of(m.Marble).AddLathe(b + Vector3.up * 7.3f, top, 10);
                Chhatri(k, m, new Vector3(x * 0.83f, H + 0.1f, -7.75f), 0.52f);
            }
        }

        /// <summary>
        /// A chhatri (small domed pavilion) standing on <paramref name="p"/>: a stepped plinth, four octagonal columns
        /// with bases and capitals, an architrave, the deep sloping eave (chhajja), an inlaid frieze, a bulbous marble
        /// dome on a neck, a brass finial and corner pinnacles. About 2 m square and 4.4 m tall at scale 1.
        /// </summary>
        static void Chhatri(KitBatch k, GardenMats m, Vector3 p, float s)
        {
            var oct = Quaternion.Euler(0f, 22.5f, 0f);
            k.Box(m.Sandstone, p + Vector3.up * 0.12f * s, new Vector3(2.0f, 0.24f, 2.0f) * s, 2f);
            k.Box(m.Marble, p + Vector3.up * 0.255f * s, new Vector3(2.06f, 0.03f, 2.06f) * s);
            foreach (float cx in new[] { -0.72f, 0.72f })
                foreach (float cz in new[] { -0.72f, 0.72f })
                {
                    var c = p + new Vector3(cx, 0f, cz) * s;
                    k.Box(m.Sandstone, c + Vector3.up * 0.34f * s, new Vector3(0.26f, 0.14f, 0.26f) * s);
                    k.Of(m.Sandstone).AddCylinder(c + Vector3.up * 0.41f * s, 0.085f * s, 1.35f * s, 8, 1f, oct);
                    k.Box(m.Marble, c + Vector3.up * 1.03f * s, new Vector3(0.2f, 0.04f, 0.2f) * s);
                    k.Box(m.Sandstone, c + Vector3.up * 1.81f * s, new Vector3(0.22f, 0.1f, 0.22f) * s);
                    k.Box(m.Sandstone, c + Vector3.up * 1.88f * s, new Vector3(0.3f, 0.05f, 0.3f) * s);
                }
            // Architrave and the chhajja eave sloping out on all four sides.
            k.Box(m.Sandstone, p + new Vector3(0f, 1.99f, 0.72f) * s, new Vector3(1.74f, 0.18f, 0.2f) * s);
            k.Box(m.Sandstone, p + new Vector3(0f, 1.99f, -0.72f) * s, new Vector3(1.74f, 0.18f, 0.2f) * s);
            k.Box(m.Sandstone, p + new Vector3(0.72f, 1.99f, 0f) * s, new Vector3(0.2f, 0.18f, 1.74f) * s);
            k.Box(m.Sandstone, p + new Vector3(-0.72f, 1.99f, 0f) * s, new Vector3(0.2f, 0.18f, 1.74f) * s);
            k.Box(m.Sandstone, p + new Vector3(0f, 2.06f, 0.98f) * s, new Vector3(2.45f, 0.05f, 0.58f) * s, Quaternion.Euler(18f, 0f, 0f));
            k.Box(m.Sandstone, p + new Vector3(0f, 2.06f, -0.98f) * s, new Vector3(2.45f, 0.05f, 0.58f) * s, Quaternion.Euler(-18f, 0f, 0f));
            k.Box(m.Sandstone, p + new Vector3(0.98f, 2.06f, 0f) * s, new Vector3(0.58f, 0.05f, 2.45f) * s, Quaternion.Euler(0f, 0f, -18f));
            k.Box(m.Sandstone, p + new Vector3(-0.98f, 2.06f, 0f) * s, new Vector3(0.58f, 0.05f, 2.45f) * s, Quaternion.Euler(0f, 0f, 18f));
            // Frieze with a marble band and a line of inlay, then the dome on its neck.
            k.Box(m.Sandstone, p + Vector3.up * 2.32f * s, new Vector3(1.6f, 0.36f, 1.6f) * s);
            k.Box(m.Marble, p + Vector3.up * 2.32f * s, new Vector3(1.62f, 0.12f, 1.62f) * s);
            k.Box(m.Carnelian, p + Vector3.up * 2.32f * s, new Vector3(1.635f, 0.03f, 1.635f) * s);
            k.Of(m.Marble).AddCylinder(p + Vector3.up * 2.5f * s, 0.7f * s, 0.1f * s, 16);
            var dome = new List<Vector2>(s_Dome.Length);
            foreach (var d in s_Dome) dome.Add(d * (0.66f * s));
            k.Of(m.Marble).AddLathe(p + Vector3.up * 2.6f * s, dome, 16);
            var top = p + Vector3.up * (2.6f + 1.44f * 0.66f) * s;
            k.Of(m.Brass).AddSphere(top + Vector3.up * 0.05f * s, 0.07f * s, 8, 5);
            k.Of(m.Brass).AddCylinder(top + Vector3.up * 0.1f * s, 0.022f * s, 0.12f * s, 6);
            k.Of(m.Brass).AddSphere(top + Vector3.up * 0.25f * s, 0.045f * s, 8, 5);
            k.Of(m.Brass).AddFrustum(top + Vector3.up * 0.28f * s, 0.018f * s, 0f, 0.22f * s, 6);
            foreach (float cx in new[] { -0.76f, 0.76f })
                foreach (float cz in new[] { -0.76f, 0.76f })
                {
                    var c = p + new Vector3(cx, 2.5f, cz) * s;
                    k.Of(m.Marble).AddCylinder(c, 0.035f * s, 0.3f * s, 6);
                    k.Of(m.Marble).AddSphere(c + Vector3.up * 0.33f * s, 0.05f * s, 6, 4);
                }
        }

        /// <summary>A blind niche on a wall face: a darker pointed-arch panel, a marble outline and (optionally) a frame.</summary>
        static void Niche(KitBatch k, GardenMats m, Vector3 baseCenter, Vector3 normal, float width, float spring, bool frame)
        {
            var right = Vector3.Cross(Vector3.up, -normal);
            var rot = Quaternion.LookRotation(-normal, Vector3.up);
            var arch = DetailShapes.PointedArch(width, spring, 0.3f, 7);
            k.Of(m.Recess).AddExtrudedXY(arch, 0.02f, baseCenter + normal * 0.012f, rot, 1.5f);
            Vector3 W(Vector2 v) => baseCenter + right * v.x + Vector3.up * v.y + normal * 0.03f;
            DetailShapes.Outline(k, m.Marble, arch, W, normal, 0.07f, 0.03f);
            if (!frame) return;
            float apex = DetailShapes.ArchApex(width, spring, 0.3f), hw = width * 0.5f + 0.16f;
            DetailShapes.PlaneStrip(k, m.Marble, W(new Vector2(-hw, 0f)), W(new Vector2(-hw, apex + 0.16f)), normal, 0.05f, 0.025f);
            DetailShapes.PlaneStrip(k, m.Marble, W(new Vector2(hw, 0f)), W(new Vector2(hw, apex + 0.16f)), normal, 0.05f, 0.025f);
            DetailShapes.PlaneStrip(k, m.Marble, W(new Vector2(-hw - 0.025f, apex + 0.16f)), W(new Vector2(hw + 0.025f, apex + 0.16f)), normal, 0.05f, 0.025f);
        }

        /// <summary>A stepped parapet of small merlons (kangura) along a wall top.</summary>
        static void Parapet(KitBatch k, GardenMats m, Vector3 a, Vector3 b)
        {
            float len = Vector3.Distance(a, b);
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 0.55f));
            var rot = Quaternion.LookRotation((b - a).normalized, Vector3.up);
            for (int i = 0; i <= n; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)n);
                k.Box(m.Sandstone, p + Vector3.up * 0.16f, new Vector3(0.2f, 0.32f, 0.3f), rot);
                k.Box(m.Sandstone, p + Vector3.up * 0.38f, new Vector3(0.14f, 0.12f, 0.18f), rot);
            }
        }

        /// <summary>The same arch shrunk by <paramref name="d"/> (for an inner line of inlay).</summary>
        static List<Vector2> Inset(List<Vector2> arch, float d)
        {
            var o = new List<Vector2>(arch.Count);
            float minY = float.MaxValue, maxY = float.MinValue, maxX = 0f;
            foreach (var p in arch) { minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); maxX = Mathf.Max(maxX, Mathf.Abs(p.x)); }
            float sx = (maxX - d) / maxX, sy = (maxY - d) / maxY;
            foreach (var p in arch) o.Add(new Vector2(p.x * sx, p.y * sy));
            return o;
        }
    }
}

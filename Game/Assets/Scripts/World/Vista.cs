using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TwelfthStamp
{
    /// <summary>
    /// The place around a distant landmark (ground, buildings, trees, lamps, water, mist), so the landmark stands somewhere
    /// instead of hanging in the fog. Everything is a copy of the cell's backdrop material (TS_PocketLit) with its own light
    /// and a gentler fog than the cell's: an exponential haze, 1 - e^(-rate d), that hides a set share of the landmark and
    /// less of anything nearer the player. The shader's fog is 1 - e^(-(d k)^2), so each distance band gets its own density.
    /// Geometry is merged into one mesh per material, with no colliders and no shadows. Positions are cell coordinates: the
    /// vista lives on a child of the landmark root that undoes the root's rotation and scale, so the autotest's no-fog
    /// review shot covers it too. Lights, window glow and mist are additive or alpha sprites that face the player's spot.
    /// </summary>
    public sealed class Vista
    {
        /// <summary>The light on one group of materials: a key light (direction to it, colour) and a sky/ground ambient.</summary>
        public struct Rig
        {
            public Vector3 Dir;
            public Color Key, Sky, Ground;

            public Rig(Vector3 dir, Color key, Color sky, Color ground)
            {
                Dir = dir.normalized;
                Key = key;
                Sky = sky;
                Ground = ground;
            }

            public override string ToString() => Dir.ToString("F2") + Key.ToString("F3") + Sky.ToString("F3") + Ground.ToString("F3");
        }

        /// <summary>Things beyond this many metres from the cell origin would sit outside the cell's sky dome (320 m).</summary>
        public const float DomeRadius = 312f;
        static readonly Vector3 s_Eye = new Vector3(0f, 1.6f, 0f);

        public readonly Transform Root;
        /// <summary>The root's position in the cell (the landmark's base).</summary>
        public readonly Vector3 Origin;
        /// <summary>Horizontal distance from the player's spot (the cell origin) to the root.</summary>
        public readonly float Distance;
        /// <summary>Horizontal unit vector from the root toward the player's spot.</summary>
        public readonly Vector3 ToViewer;
        /// <summary>The light for surroundings made with <see cref="At"/>. Starts as the cell's own sun and ambient.</summary>
        public Rig Context;

        readonly Transform m_Site;
        readonly Material m_Cell;
        readonly float m_Rate;
        readonly Dictionary<string, Material> m_Mats = new Dictionary<string, Material>();
        readonly Dictionary<Material, MeshBuilder> m_Batches = new Dictionary<Material, MeshBuilder>();
        readonly List<Material> m_Order = new List<Material>();
        readonly HashSet<Material> m_Solid = new HashSet<Material>();
        readonly Dictionary<Material, string> m_Footsteps = new Dictionary<Material, string>();
        readonly HashSet<Material> m_NotFloor = new HashSet<Material>();
        Vector3 m_Stamp;
        bool m_HasStamp;
        Material m_Glow, m_GroundGlow, m_Mist;

        /// <summary>Haze rate that hides <paramref name="share"/> of something <paramref name="distance"/> metres away.</summary>
        public static float Rate(float share, float distance) => -Mathf.Log(1f - Mathf.Clamp(share, 0.01f, 0.95f)) / Mathf.Max(1f, distance);

        /// <summary>A vista round a landmark root: <paramref name="share"/> of the landmark itself is hidden by haze.</summary>
        public static Vista ForLandmark(GameObject root, Material cellBackdrop, float share)
        {
            var p = root.transform.localPosition;
            return new Vista(root, cellBackdrop, Rate(share, new Vector2(p.x, p.z).magnitude));
        }

        public Vista(GameObject root, Material cellBackdrop, float hazeRate)
        {
            Root = root.transform;
            Origin = Root.localPosition;
            var flat = new Vector3(-Origin.x, 0f, -Origin.z);
            Distance = flat.magnitude;
            ToViewer = Distance > 0.01f ? flat / Distance : Vector3.back;
            m_Cell = cellBackdrop;
            m_Rate = hazeRate;
            m_Site = new GameObject("Vista").transform;
            m_Site.SetParent(Root, false);
            m_Site.localRotation = Quaternion.Inverse(Root.localRotation);
            var s = Root.localScale;
            m_Site.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
            Context = CellLight();
        }

        // ------------------------------------------------------------------ haze and materials
        public float DistanceTo(Vector3 cell) => Vector3.Distance(s_Eye, cell);
        /// <summary>Share of colour hidden by haze at this cell position (0..1).</summary>
        public float Haze(Vector3 cell) => 1f - Mathf.Exp(-m_Rate * DistanceTo(cell));
        /// <summary>What is left of an additive light after the haze (sprites are not fogged by the shader).</summary>
        public float Clear(Vector3 cell) => Mathf.Exp(-m_Rate * DistanceTo(cell));

        /// <summary>TS_PocketLit fog density giving this vista's haze at distance d (30 m bands, so nearby things share materials).</summary>
        public float FogK(float d)
        {
            d = Mathf.Max(20f, Mathf.Round(d / 30f) * 30f);
            return Mathf.Sqrt(m_Rate / d);
        }

        public float FogK(Vector3 cell) => FogK(DistanceTo(cell));
        /// <summary>Fog density for the landmark itself, judged at a point about a third of the way up.</summary>
        public float FogAt(float height) => FogK(Origin + Vector3.up * height * 0.35f);
        public Color FogColor => m_Cell.GetColor("_FogColor");

        /// <summary>The cell's own sun and ambient, scaled.</summary>
        public Rig CellLight(float key = 1f, float ambient = 1f) => new Rig(m_Cell.GetVector("_SunDir"), m_Cell.GetColor("_SunColor") * key,
            m_Cell.GetColor("_SkyAmb") * ambient, m_Cell.GetColor("_GroundAmb") * ambient);

        /// <summary>Floodlights from the player's side: <paramref name="side"/> degrees round, <paramref name="below"/> degrees under the horizon (uplight).</summary>
        public Rig Flood(Color key, float side, float below, Color sky, Color ground)
        {
            var h = Quaternion.Euler(0f, side, 0f) * ToViewer;
            float b = below * Mathf.Deg2Rad;
            return new Rig(h * Mathf.Cos(b) + Vector3.down * Mathf.Sin(b), key, sky, ground);
        }

        /// <summary>No key light and an even ambient, so a material shows its own colour.</summary>
        public static Rig Even(float level) => new Rig(Vector3.up, Color.black, Color.white * level, Color.white * level);

        public Material Mat(Color color, Rig rig, float fogK, Color emission = default, Texture tex = null)
        {
            var key = color.ToString("F3") + emission.ToString("F3") + (tex != null ? tex.name : "-") + rig + fogK.ToString("F5");
            if (m_Mats.TryGetValue(key, out var m)) return m;
            m = new Material(m_Cell) { name = "Vista" + m_Mats.Count };
            m.SetColor("_BaseColor", color);
            m.SetTexture("_BaseMap", tex != null ? tex : Tex.White);
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetColor("_EmissionColor", emission);
            m.SetVector("_SunDir", rig.Dir);
            m.SetColor("_SunColor", rig.Key);
            m.SetColor("_SkyAmb", rig.Sky);
            m.SetColor("_GroundAmb", rig.Ground);
            m.SetFloat("_FogDensity", fogK);
            m_Mats[key] = m;
            return m;
        }

        /// <summary>A surroundings material for something at <paramref name="cell"/>, in the <see cref="Context"/> light.</summary>
        public Material At(Vector3 cell, Color color, Color emission = default, Texture tex = null) => Mat(color, Context, FogK(cell), emission, tex);

        /// <summary>Unlit glow (lamp globes, lit glass), fogged like its surroundings.</summary>
        public Material Glow(Vector3 cell, Color emission) => Mat(Color.black, Even(0f), FogK(cell), emission);

        // ------------------------------------------------------------------ geometry
        public Vector3 L(Vector3 cell) => cell - Origin;
        public static bool InDome(Vector3 cell, float margin = 0f) => new Vector2(cell.x, cell.z).magnitude + margin < DomeRadius;

        /// <summary>Distance from <paramref name="cell"/> to the sight line between the player's spot and the root (horizontal).</summary>
        public float FromSightLine(Vector3 cell)
        {
            var p = new Vector2(cell.x, cell.z);
            var a = Vector2.zero;
            var b = new Vector2(Origin.x, Origin.z);
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        public MeshBuilder B(Material m)
        {
            if (!m_Batches.TryGetValue(m, out var b))
            {
                b = new MeshBuilder();
                m_Batches[m] = b;
                m_Order.Add(m);
            }
            return b;
        }

        public void Box(Material m, Vector3 center, Vector3 size, float yaw = 0f, float uv = 4f)
        {
            if (!InDome(center)) return;
            m_Solid.Add(m);
            B(m).AddBox(L(center), size, Quaternion.Euler(0f, yaw, 0f), uv);
        }

        /// <summary>Flat ground (top face only) centred on <paramref name="center"/>, turned by yaw.</summary>
        public void Ground(Material m, Vector3 center, Vector2 size, float yaw = 0f, float uv = 6f, bool solid = true)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            var c = L(center);
            float hx = size.x / 2f, hz = size.y / 2f;
            if (solid) m_Solid.Add(m);
            B(m).AddQuadWorldUV(c + q * new Vector3(-hx, 0f, -hz), c + q * new Vector3(-hx, 0f, hz), c + q * new Vector3(hx, 0f, hz), c + q * new Vector3(hx, 0f, -hz), uv);
        }

        /// <summary>Flat ground over a rectangle; solid = false for shallow water the player wades through onto the ground below.</summary>
        public void GroundRect(Material m, float x0, float z0, float x1, float z1, float y, float uv = 6f, bool solid = true)
            => Ground(m, new Vector3((x0 + x1) / 2f, y, (z0 + z1) / 2f), new Vector2(x1 - x0, z1 - z0), 0f, uv, solid);

        /// <summary>A flat polygon (x, z points in either winding) at height y, as a thin slab from y - depth to y.</summary>
        public void Slab(Material m, IList<Vector2> poly, float y, float depth, float uv = 6f)
        {
            var pts = new List<Vector2>(poly);
            float area = 0f;
            for (int i = 0; i < pts.Count; i++) { var a = pts[i]; var b = pts[(i + 1) % pts.Count]; area += a.x * b.y - b.x * a.y; }
            if (area < 0f) pts.Reverse();
            for (int i = 0; i < pts.Count; i++) pts[i] -= new Vector2(Origin.x, Origin.z);
            m_Solid.Add(m);
            B(m).AddPrism(pts, y - depth - Origin.y, y - Origin.y, uv);
        }

        /// <summary>
        /// A block: four walls with a facade texture (one tile = <paramref name="tile"/> metres, floors aligned to the base), an
        /// additive overlay of lit windows when <paramref name="lights"/> &gt; 0, and a flat or hipped/mansard roof.
        /// </summary>
        public void Building(Material walls, Material roof, Vector3 basePos, float w, float d, float h, float yaw, float tile,
            float roofH = 0f, float roofInset = 0f, float lights = 0f, Texture lightsTex = null)
        {
            if (!InDome(basePos, Mathf.Max(w, d) * 0.5f)) return;
            var c = L(basePos);
            var q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 P(float x, float y, float z) => c + q * new Vector3(x, y, z);
            float hw = w / 2f, hd = d / 2f;
            int hash = ((Mathf.RoundToInt(basePos.x * 7.3f) * 73856093) ^ (Mathf.RoundToInt(basePos.z * 5.1f) * 19349663)) & 0x7fffffff;
            var uvo = new Vector2((hash % 97) / 13f, (hash / 97 % 8) / 8f);
            var walls4 = new[]
            {
                (P(-hw, 0f, hd), P(hw, 0f, hd), q * Vector3.forward),
                (P(hw, 0f, -hd), P(-hw, 0f, -hd), q * Vector3.back),
                (P(hw, 0f, hd), P(hw, 0f, -hd), q * Vector3.right),
                (P(-hw, 0f, -hd), P(-hw, 0f, hd), q * Vector3.left),
            };
            var wb = B(walls);
            m_Solid.Add(walls);
            var up = Vector3.up * h;
            foreach (var (a, b, n) in walls4) wb.AddQuadWorldUV(a, b, b + up, a + up, tile, uvo);
            if (lights > 0f)
            {
                var lb = B(Overlay(lightsTex != null ? lightsTex : VistaTex.Lights));
                var prev = lb.VertexColor;
                float f = lights * Clear(basePos + Vector3.up * h * 0.5f);
                lb.VertexColor = new Color(f, f, f, 1f);
                foreach (var (a, b, n) in walls4) lb.AddQuadWorldUV(a + n * 0.12f, b + n * 0.12f, b + n * 0.12f + up, a + n * 0.12f + up, tile, uvo);
                lb.VertexColor = prev;
            }
            if (roof == null) return;
            if (roofH <= 0f) B(roof).AddQuadWorldUV(P(-hw, h, hd), P(hw, h, hd), P(hw, h, -hd), P(-hw, h, -hd), 4f);
            else Roof(roof, basePos + Vector3.up * h, w, d, yaw, roofH, roofInset);
        }

        /// <summary>A hipped (or mansard) roof whose eaves rectangle w x d sits at <paramref name="eaves"/>; inset is how far the ridge stops short of each eave.</summary>
        public void Roof(Material roof, Vector3 eaves, float w, float d, float yaw, float roofH, float inset)
        {
            if (!InDome(eaves, Mathf.Max(w, d) * 0.5f)) return;
            var c = L(eaves);
            var q = Quaternion.Euler(0f, yaw, 0f);
            Vector3 P(float x, float y, float z) => c + q * new Vector3(x, y, z);
            float hw = w / 2f, hd = d / 2f;
            float i = Mathf.Min(inset, Mathf.Min(hw, hd) - 0.2f), t = roofH;
            Vector3 B0 = P(-hw, 0f, -hd), B1 = P(hw, 0f, -hd), B2 = P(hw, 0f, hd), B3 = P(-hw, 0f, hd);
            Vector3 T0 = P(-hw + i, t, -hd + i), T1 = P(hw - i, t, -hd + i), T2 = P(hw - i, t, hd - i), T3 = P(-hw + i, t, hd - i);
            var rb = B(roof);
            m_Solid.Add(roof);
            Quad(rb, B1, B0, T0, T1);
            Quad(rb, B3, B2, T2, T3);
            Quad(rb, B2, B1, T1, T2);
            Quad(rb, B0, B3, T3, T0);
            Quad(rb, T3, T2, T1, T0);
            Quad(rb, B0, B1, B2, B3); // underside of the eaves
        }

        static void Quad(MeshBuilder b, Vector3 a, Vector3 bb, Vector3 c, Vector3 d)
            => b.AddQuad(a, bb, c, d, new Vector2(a.x + a.z, a.y) / 4f, new Vector2(bb.x + bb.z, bb.y) / 4f, new Vector2(c.x + c.z, c.y) / 4f, new Vector2(d.x + d.z, d.y) / 4f);

        /// <summary>A broadleaf tree: trunk and a crown of three blobs.</summary>
        public void Tree(Material leaves, Material trunk, Vector3 at, float height, float width, int seed)
        {
            if (!InDome(at, width)) return;
            var rnd = new System.Random(seed);
            var c = L(at);
            float trunkH = height * 0.32f;
            if (trunk != null) B(trunk).AddCylinder(c, Mathf.Max(0.15f, width * 0.045f), trunkH + width * 0.2f, 5, 2f);
            var b = B(leaves);
            float r = width * 0.5f;
            for (int k = 0; k < 3; k++)
            {
                float rr = r * (0.6f + 0.2f * (float)rnd.NextDouble());
                float y = trunkH + rr + Mathf.Max(0f, height - trunkH - 2f * rr) * (k / 2f);
                var o = new Vector3(((float)rnd.NextDouble() - 0.5f) * r * 0.8f, y, ((float)rnd.NextDouble() - 0.5f) * r * 0.8f);
                b.AddSphere(c + o, rr, 7, 5, 4f);
            }
        }

        /// <summary>A conifer: stacked cones on a short trunk.</summary>
        public void Conifer(Material leaves, Material trunk, Vector3 at, float height, float width, int tiers = 3)
        {
            if (!InDome(at, width)) return;
            var c = L(at);
            if (trunk != null) B(trunk).AddCylinder(c, width * 0.05f, height * 0.3f, 5, 2f);
            var b = B(leaves);
            for (int k = 0; k < tiers; k++)
            {
                float y0 = height * (0.16f + 0.22f * k);
                b.AddFrustum(c + Vector3.up * y0, width * 0.5f * (1f - 0.24f * k), 0.05f, height - y0, 7, true, false, 4f);
            }
        }

        /// <summary>A slim cypress (a pointed spindle), as in Mughal gardens.</summary>
        public void Cypress(Material leaves, Vector3 at, float height, float width)
        {
            if (!InDome(at, width)) return;
            var prof = new List<Vector2>
            {
                new Vector2(0.05f, 0f), new Vector2(width * 0.42f, height * 0.08f), new Vector2(width * 0.5f, height * 0.3f),
                new Vector2(width * 0.44f, height * 0.58f), new Vector2(width * 0.26f, height * 0.84f), new Vector2(0.03f, height),
            };
            B(leaves).AddLathe(L(at), prof, 8, 4f);
        }

        /// <summary>A date palm: tapered trunk and a crown of drooping fronds.</summary>
        public void Palm(Material leaves, Material trunk, Vector3 at, float height, int seed)
        {
            if (!InDome(at, 4f)) return;
            var c = L(at);
            B(trunk).AddFrustum(c, 0.35f, 0.22f, height, 6, false, true, 2f);
            var b = B(leaves);
            var top = c + Vector3.up * height;
            float spin = seed * 37f;
            for (int k = 0; k < 8; k++)
            {
                var q = Quaternion.Euler(0f, spin + k * 45f, 0f) * Quaternion.Euler(28f + (k % 2) * 14f, 0f, 0f);
                b.AddBox(top + q * new Vector3(0f, 0f, 2.1f), new Vector3(0.9f, 0.12f, 4.4f), q, 2f);
            }
            b.AddSphere(top, 0.7f, 6, 4, 2f);
        }

        /// <summary>A street lamp: pole, glowing globe and a halo.</summary>
        public void Lamp(Material pole, Vector3 at, float height, Color light, float halo = 3f)
        {
            if (!InDome(at)) return;
            var c = L(at);
            var top = at + Vector3.up * height;
            if (pole != null) B(pole).AddCylinder(c, 0.1f, height, 4, 2f);
            B(Glow(top, light * 1.6f)).AddSphere(L(top), 0.32f, 6, 4, 1f);
            Halo(top, halo, light);
        }

        // ------------------------------------------------------------------ sprites
        Material GlowSprite => m_Glow != null ? m_Glow : m_Glow = NearFaded(Mats.Particle(Tex.SoftDot, true, Color.white, 1.4f), "VistaHalo", 1.5f, 3f);
        Material GroundGlow => m_GroundGlow != null ? m_GroundGlow : m_GroundGlow = Mats.Particle(Tex.SoftDot, true, Color.white, 1.4f);
        // Mist is invisible within 10 m and full by 35 m, so walking into a bank never pops it on or off.
        Material MistSprite => m_Mist != null ? m_Mist : m_Mist = NearFaded(Mats.Particle(Tex.SoftDot, false, FogColor, 1f), "VistaMist", 10f, 25f);

        static Material NearFaded(Material shared, string name, float start, float range)
        {
            var m = new Material(shared) { name = name };
            m.SetVector("_NearFade", new Vector4(start, range, 0f, 0f));
            return m;
        }
        static Material Overlay(Texture tex) => Mats.Particle(tex, true, Color.white, 1.5f);

        /// <summary>A soft additive light facing the player's spot (faded by the haze).</summary>
        public void Halo(Vector3 at, float size, Color color)
        {
            if (!InDome(at)) return;
            Crossed(B(GlowSprite), at, size, size, color * (Clear(at) * 0.55f));
        }

        /// <summary>A light's reflection: a soft streak lying on the water, running from below the light toward the player.</summary>
        public void Streak(Vector3 waterAt, float width, float length, Color color)
        {
            if (!InDome(waterAt)) return;
            var n = new Vector3(-waterAt.x, 0f, -waterAt.z).normalized;
            var r = Vector3.Cross(Vector3.up, n);
            var c = L(waterAt);
            var b = B(GroundGlow);
            var prev = b.VertexColor;
            b.VertexColor = color * Clear(waterAt);
            b.AddQuad(c - r * width / 2f, c - r * width / 2f + n * length, c + r * width / 2f + n * length, c + r * width / 2f,
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));
            b.VertexColor = prev;
        }

        /// <summary>A pool of light lying on the ground (additive).</summary>
        public void Pool(Vector3 at, float radius, Color color)
        {
            if (!InDome(at)) return;
            var c = L(at) + Vector3.up * 0.06f;
            var b = B(GroundGlow);
            var prev = b.VertexColor;
            b.VertexColor = color * Clear(at);
            b.AddQuad(c + new Vector3(-radius, 0f, -radius), c + new Vector3(-radius, 0f, radius), c + new Vector3(radius, 0f, radius), c + new Vector3(radius, 0f, -radius),
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));
            b.VertexColor = prev;
        }

        /// <summary>A bank of mist in the fog colour, facing the player's spot.</summary>
        public void Mist(Vector3 at, float width, float height, float alpha)
        {
            if (!InDome(at)) return;
            Facing(B(MistSprite), at, width, height, new Color(1f, 1f, 1f, alpha));
        }

        /// <summary>Three vertical quads crossed at 60 degrees round the point: a soft glow that reads from any side.</summary>
        void Crossed(MeshBuilder b, Vector3 at, float w, float h, Color color)
        {
            var c = L(at);
            var prev = b.VertexColor;
            b.VertexColor = color;
            for (int i = 0; i < 3; i++)
            {
                var r = Quaternion.Euler(0f, i * 60f, 0f) * Vector3.right;
                b.AddQuad(c - r * w / 2f - Vector3.up * h / 2f, c - r * w / 2f + Vector3.up * h / 2f, c + r * w / 2f + Vector3.up * h / 2f, c + r * w / 2f - Vector3.up * h / 2f,
                    new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));
            }
            b.VertexColor = prev;
        }

        void Facing(MeshBuilder b, Vector3 at, float w, float h, Color color)
        {
            var n = new Vector3(-at.x, 0f, -at.z).normalized;
            var r = Vector3.Cross(Vector3.up, n);
            var c = L(at);
            var prev = b.VertexColor;
            b.VertexColor = color;
            b.AddQuad(c - r * w / 2f - Vector3.up * h / 2f, c - r * w / 2f + Vector3.up * h / 2f, c + r * w / 2f + Vector3.up * h / 2f, c + r * w / 2f - Vector3.up * h / 2f,
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));
            b.VertexColor = prev;
        }

        /// <summary>Pieces made with <paramref name="m"/> still block, but sit off the Floor layer: no VR teleport onto wall tops.</summary>
        public void NotFloor(Material m) => m_NotFloor.Add(m);

        /// <summary>The footstep sound for walking on pieces made with <paramref name="m"/> ("stone" unless set: wood, stone, tile or sand).</summary>
        public void Footsteps(Material m, string surface) => m_Footsteps[m] = surface;

        /// <summary>Where this landmark's stamp can hang (cell coordinates, about 1.5 m above walkable ground).</summary>
        public void StampSpot(Vector3 cell)
        {
            m_Stamp = cell;
            m_HasStamp = true;
        }

        /// <summary>
        /// Builds one mesh per material under the vista (no shadows) and records the ground height and the stamp spot. Solid
        /// pieces (ground, walls, roofs, quays, bridges) get mesh colliders on the Floor layer with a stone footstep surface, so
        /// the place can be walked (and teleported onto) once a cell opens its play area to it; foliage, lamps and sprites
        /// stay render-only.
        /// </summary>
        public void Finish(float groundY)
        {
            foreach (var m in m_Order)
            {
                var b = m_Batches[m];
                if (b.VertexCount == 0) continue;
                bool solid = m_Solid.Contains(m);
                bool floor = solid && !m_NotFloor.Contains(m);
                var go = MeshFactory.FromBuilder("Vista_" + m.name, b, m, m_Site, solid ? ColliderKind.Mesh : ColliderKind.None, floor ? Layers.Floor : Layers.Default);
                if (solid) go.AddComponent<Surface>().Kind = m_Footsteps.TryGetValue(m, out var kind) ? kind : "stone";
                var r = go.GetComponent<MeshRenderer>();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            m_Batches.Clear();
            m_Order.Clear();
            var info = Root.gameObject.GetComponent<VistaInfo>();
            if (info == null) info = Root.gameObject.AddComponent<VistaInfo>();
            info.GroundY = groundY;
            info.HasStampSpot = m_HasStamp;
            info.StampLocal = m_Stamp;
            m_Solid.Clear();
            m_Footsteps.Clear();
            m_NotFloor.Clear();
        }

        /// <summary>A separate object (for shapes that need a non-uniform scale, such as dunes), under the vista.</summary>
        public GameObject Piece(string name, MeshBuilder b, Material m, Vector3 cell, Vector3 scale)
        {
            var go = MeshFactory.FromBuilder(name, b, m, m_Site);
            go.transform.localPosition = L(cell);
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }
    }

    /// <summary>
    /// Marks a landmark root that has a vista. GroundY is the cell height a person would stand at out there; StampLocal is
    /// where its country's stamp can hang (cell coordinates). Reachable is set by the cell once its play area opens to the
    /// site (Landmarks.MarkReachable); until then Landmarks.TryStampSpot says no.
    /// </summary>
    public class VistaInfo : MonoBehaviour
    {
        public float GroundY;
        public bool HasStampSpot;
        public Vector3 StampLocal;
        public bool Reachable;
    }

    /// <summary>Facade and window-light textures for vista buildings, generated once with fixed seeds.</summary>
    public static class VistaTex
    {
        static Texture2D s_Facade, s_Lights, s_Gothic, s_GothicLights;

        /// <summary>8 x 8 windows per tile: white wall (the material tints it), dark glass and a sill under each window.</summary>
        public static Texture2D Facade => s_Facade != null ? s_Facade : s_Facade = Grid("TS_VistaFacade", 8, 8, 0.36f, 0.62f, false, 3, false);
        /// <summary>The same grid on black, about a third of the windows lit warm or cool: an additive overlay.</summary>
        public static Texture2D Lights => s_Lights != null ? s_Lights : s_Lights = Grid("TS_VistaLights", 8, 8, 0.36f, 0.62f, false, 5, true);
        /// <summary>16 x 4 tall pointed windows per tile, for Gothic facades.</summary>
        public static Texture2D Gothic => s_Gothic != null ? s_Gothic : s_Gothic = Grid("TS_VistaGothic", 16, 4, 0.36f, 0.62f, true, 7, false);
        public static Texture2D GothicLights => s_GothicLights != null ? s_GothicLights : s_GothicLights = Grid("TS_VistaGothicLights", 16, 4, 0.36f, 0.62f, true, 9, true);

        static Texture2D Grid(string name, int cols, int rows, float winW, float winH, bool pointed, int seed, bool lights)
        {
            const int N = 256;
            var px = new Color32[N * N];
            var rnd = new System.Random(seed);
            int cw = N / cols, ch = N / rows;
            for (int cy = 0; cy < rows; cy++)
            {
                float busy = (float)rnd.NextDouble();
                for (int cx = 0; cx < cols; cx++)
                {
                    double r = rnd.NextDouble();
                    Color32 win;
                    if (lights)
                    {
                        float p = 0.16f + 0.34f * busy;
                        float k = 0.55f + 0.45f * (float)rnd.NextDouble();
                        if (r < p * 0.82f) win = C(1f * k, 0.72f * k, 0.42f * k);
                        else if (r < p) win = C(0.7f * k, 0.8f * k, 1f * k);
                        else if (r < p + 0.1f) win = C(0.32f, 0.21f, 0.12f);
                        else win = C(0f, 0f, 0f);
                    }
                    else
                    {
                        float g = 0.09f + 0.07f * (float)r;
                        win = C(g, g * 1.08f, g * 1.25f);
                    }
                    int x0 = cx * cw, y0 = cy * ch;
                    int wx0 = x0 + Mathf.RoundToInt(cw * (1f - winW) / 2f), wx1 = x0 + Mathf.RoundToInt(cw * (1f + winW) / 2f);
                    int wy0 = y0 + Mathf.RoundToInt(ch * 0.2f), wy1 = wy0 + Mathf.RoundToInt(ch * winH);
                    float half = (wx1 - wx0) / 2f, mid = (wx0 + wx1) / 2f, archFrom = wy1 - half * 1.3f;
                    for (int y = y0; y < y0 + ch; y++)
                        for (int x = x0; x < x0 + cw; x++)
                        {
                            bool inWin = x >= wx0 && x < wx1 && y >= wy0 && y < wy1;
                            if (inWin && pointed && y > archFrom)
                                inWin = Mathf.Abs(x + 0.5f - mid) < half * (1f - (y - archFrom) / Mathf.Max(1f, wy1 - archFrom));
                            Color32 c;
                            if (lights) c = inWin ? win : C(0f, 0f, 0f);
                            else if (inWin) c = win;
                            else if ((y == wy0 - 1 || y == wy0 - 2) && x >= wx0 - 1 && x <= wx1) c = C(0.84f, 0.84f, 0.84f);
                            else if (y == y0 || (pointed && x == x0)) c = C(0.78f, 0.78f, 0.78f);
                            else c = C(1f, 1f, 1f);
                            px[y * N + x] = c;
                        }
                }
            }
            var t = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }

        static Color32 C(float r, float g, float b) => new Color32((byte)(Mathf.Clamp01(r) * 255f), (byte)(Mathf.Clamp01(g) * 255f), (byte)(Mathf.Clamp01(b) * 255f), 255);
    }
}

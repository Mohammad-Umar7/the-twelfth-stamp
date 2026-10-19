using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// London expansion: Lamplighter Mews. Halfway up the dark northern stretch of the promenade a lit doorway opens in the
    /// embankment wall (z 42..44.2); two steps lead into a sooty brick vault under the terrace (the old arches) and out
    /// into a cobbled mews behind it: painted coach houses with hanging baskets and window boxes, a corner pub with a
    /// swinging sign, festival bunting, pigeons, a gated lane running on into darkness, and a red telephone box that rings
    /// as you come near. Switchable lanterns light the mews' dark corners and the promenade on the way to the bridge.
    /// Layout (cell-local metres): the vault x -4.4..-9.8 (floor y 0.46), the lane x -15.8..-9.8, z 33..55 (y 0.46), the
    /// terrace's back wall at x -9.8 (to y 16.8), the coach houses x -21..-15.8. Everything is under the cell's child
    /// "Expand_UK"; the embankment wall's long brick box is replaced by pieces round the doorway and the west bound gets a
    /// gap the vault's own walls close.
    /// </summary>
    public static class Expand_UK
    {
        const float WallX = -4.4f, WallBack = -5.0f;                       // the embankment wall's faces
        const float OZ0 = 42.0f, OZ1 = 44.2f, OZC = 43.1f, OTop = 2.9f;    // the doorway (below the string course at 2.95)
        const float Y0 = 0.46f;                                            // vault and mews floor
        const float RWX = -9.8f, RWB = -9.5f, RH = 16.8f;                  // the terrace's back wall: face, back, top
        const float MX = -15.8f, MB = -21f, MH = 7.26f;                    // coach houses: front, back, parapet top
        const float LZ0 = 33f, LZ1 = 55f;                                  // the lane's ends
        const float VSpring = 2.46f, VR = 1.1f;                            // the vault
        static readonly Vector3 PhoneAt = new Vector3(-13.6f, Y0, 33.85f); // against the south end, clear of the end house's door

        sealed class Pal
        {
            public Material Brick, Stock, Soot, BrickDark, Stone, Granite, Setts, Slate, Iron, Paint, Red, Glow, PhoneGlass, SignWhite;
            public Material WindowLit, WindowDark, Void, Gold, Leaf, Pot, Soil, Moss, PubGreen, PubGlow, Wood, Puddle, Black;
            public Material[] Stucco, Doors, Flowers;
        }

        readonly struct Face
        {
            public readonly Vector3 O, R, N;
            public readonly Quaternion Q;
            public Face(Vector3 origin, Vector3 normal)
            {
                O = origin; N = normal;
                Q = Quaternion.LookRotation(normal, Vector3.up);
                R = Q * Vector3.right;
            }
            public Vector3 P(float u, float v, float d) => O + R * u + Vector3.up * v + N * d;
            public Quaternion TextRot => Quaternion.LookRotation(-N, Vector3.up);
        }

        static void B(KitBatch k, Material m, Face f, float u, float v, float d, float w, float h, float t, float uv = 1f)
            => k.Box(m, f.P(u, v, d), new Vector3(w, h, t), f.Q, uv);

        public static void Build(LondonCell cell, Transform root)
        {
            var ex = new GameObject("Expand_UK").transform;
            ex.SetParent(cell.transform, false);
            var p = Palette();
            var rnd = new System.Random(1859 + 12);
            OpenEmbankment(cell, root, ex, p);
            Doorway(ex, p);
            Vault(ex, p);
            BackWall(ex, p, rnd);
            CoachHouses(ex, p, rnd);
            EndWalls(ex, p);
            PhoneBox(ex, p);
            Dressing(ex, p);
            Life(ex);
            Lamps(ex);
            Postcards(ex);
            var shot = new GameObject("ShotPoint").transform;
            shot.SetParent(ex, false);
            shot.localPosition = new Vector3(-12.2f, Y0, 34.6f);
            shot.localRotation = Quaternion.Euler(0f, -6f, 0f); // up the lane from the phone box end
        }

        static Pal Palette() => new Pal
        {
            Brick = Mats.Lit(new Color(0.55f, 0.38f, 0.3f), 0.3f, 0f, "brick_london"),
            Stock = Mats.Lit(new Color(0.72f, 0.63f, 0.52f), 0.3f, 0f, "brick_london"),
            Soot = Mats.Lit(new Color(0.42f, 0.36f, 0.31f), 0.3f, 0f, "brick_london"),
            BrickDark = Mats.Lit(new Color(0.46f, 0.22f, 0.16f), 0.3f, 0f, "brick_london"),
            Stone = Mats.Lit(new Color(0.78f, 0.75f, 0.68f), 0.3f, 0f, "limestone_blocks"),
            Granite = Mats.Lit(new Color(0.52f, 0.52f, 0.53f), 0.45f, 0f, "granite_pavers"),
            Setts = Mats.Lit(new Color(0.68f, 0.67f, 0.68f), 0.6f, 0f, "cobble_wet"),
            Slate = Mats.Lit(new Color(0.2f, 0.21f, 0.23f), 0.45f, 0f, "stone_dark_slate"),
            Iron = Mats.Lit(new Color(0.04f, 0.045f, 0.045f), 0.45f, 0.6f),
            Paint = Mats.Lit(new Color(0.86f, 0.85f, 0.8f), 0.4f),
            Red = Mats.Lit(new Color(0.62f, 0.04f, 0.03f), 0.55f),
            Glow = Mats.Emissive(new Color(0.55f, 0.45f, 0.3f), new Color(1.3f, 0.92f, 0.5f)),
            PhoneGlass = Mats.Emissive(new Color(0.7f, 0.66f, 0.52f), new Color(0.72f, 0.64f, 0.44f)),
            SignWhite = Mats.Emissive(new Color(0.9f, 0.9f, 0.86f), new Color(0.7f, 0.7f, 0.64f)),
            WindowLit = Mats.Emissive(new Color(0.45f, 0.32f, 0.18f), new Color(0.85f, 0.52f, 0.24f)),
            WindowDark = Mats.Lit(new Color(0.035f, 0.04f, 0.05f), 0.92f, 0.2f),
            Void = Mats.Lit(new Color(0.008f, 0.008f, 0.01f), 0.1f),
            Gold = Mats.Lit(new Color(0.78f, 0.6f, 0.3f), 0.65f, 0.9f),
            Leaf = Mats.Lit(new Color(0.16f, 0.27f, 0.12f), 0.3f),
            Pot = Mats.Lit(new Color(0.6f, 0.32f, 0.2f), 0.3f, 0f, "plaster_terracotta"),
            Soil = Mats.Lit(new Color(0.1f, 0.08f, 0.06f), 0.2f),
            Moss = Mats.Lit(new Color(0.22f, 0.26f, 0.14f), 0.2f),
            PubGreen = Mats.Lit(new Color(0.03f, 0.1f, 0.07f), 0.6f),
            PubGlow = Mats.Emissive(new Color(0.4f, 0.28f, 0.14f), new Color(1.0f, 0.6f, 0.26f)),
            Wood = Mats.Lit(new Color(0.92f, 0.82f, 0.72f), 0.4f, 0f, "wood_dark"),
            Puddle = Mats.Lit(new Color(0.02f, 0.025f, 0.03f), 0.97f),
            Black = Mats.Lit(new Color(0.03f, 0.03f, 0.035f), 0.6f),
            Stucco = new[]
            {
                Mats.Lit(new Color(0.88f, 0.84f, 0.74f), 0.3f, 0f, "plaster_warm"), Mats.Lit(new Color(0.88f, 0.74f, 0.72f), 0.3f, 0f, "plaster_warm"),
                Mats.Lit(new Color(0.72f, 0.8f, 0.86f), 0.3f, 0f, "plaster_warm"), Mats.Lit(new Color(0.9f, 0.86f, 0.7f), 0.3f, 0f, "plaster_warm"),
                Mats.Lit(new Color(0.76f, 0.86f, 0.78f), 0.3f, 0f, "plaster_warm"),
            },
            Doors = new[]
            {
                Mats.Lit(new Color(0.03f, 0.03f, 0.035f), 0.7f), Mats.Lit(new Color(0.42f, 0.05f, 0.04f), 0.6f), Mats.Lit(new Color(0.06f, 0.1f, 0.25f), 0.6f),
                Mats.Lit(new Color(0.62f, 0.44f, 0.12f), 0.55f), Mats.Lit(new Color(0.05f, 0.17f, 0.1f), 0.6f),
            },
            Flowers = new[]
            {
                Mats.Lit(new Color(0.75f, 0.08f, 0.1f), 0.3f), Mats.Lit(new Color(0.88f, 0.48f, 0.66f), 0.3f),
                Mats.Lit(new Color(0.45f, 0.25f, 0.62f), 0.3f), Mats.Lit(new Color(0.92f, 0.9f, 0.84f), 0.3f),
            },
        };

        // ------------------------------------------------------------------ the doorway in the embankment wall
        /// <summary>
        /// Replaces the embankment wall's one long brick box with pieces round the doorway (its baked piers, coping and
        /// railing are untouched; the doorway sits between two piers and under the string course) and splits the west bound
        /// so the vault's walls close the gap instead.
        /// </summary>
        static void OpenEmbankment(LondonCell cell, Transform g, Transform ex, Pal p)
        {
            var wall = g != null ? g.Find("EmbankmentWall") : null;
            if (wall != null)
            {
                var r = wall.GetComponent<MeshRenderer>();
                var mf = wall.GetComponent<MeshFilter>();
                var mat = r != null ? r.sharedMaterial : p.Brick;
                var size = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.size : new Vector3(0.6f, 4f, 134.6f);
                var c = wall.localPosition;
                float z0 = c.z - size.z * 0.5f, z1 = c.z + size.z * 0.5f, y0 = c.y - size.y * 0.5f, y1 = c.y + size.y * 0.5f;
                if (z0 < OZ0 && z1 > OZ1)
                {
                    Object.DestroyImmediate(wall.gameObject);
                    MeshFactory.Box("EmbankmentWall", ex, new Vector3(c.x, c.y, (z0 + OZ0) * 0.5f), new Vector3(size.x, size.y, OZ0 - z0), mat, false, 1.4f);
                    MeshFactory.Box("EmbankmentWall", ex, new Vector3(c.x, c.y, (OZ1 + z1) * 0.5f), new Vector3(size.x, size.y, z1 - OZ1), mat, false, 1.4f);
                    MeshFactory.Box("EmbankmentWallHead", ex, new Vector3(c.x, (OTop + y1) * 0.5f, OZC), new Vector3(size.x, y1 - OTop, OZ1 - OZ0), mat, false, 1.4f);
                    if (y0 < -0.01f) MeshFactory.Box("EmbankmentWallFoot", ex, new Vector3(c.x, y0 * 0.5f, OZC), new Vector3(size.x, -y0, OZ1 - OZ0), mat, false, 1.4f);
                }
            }
            var bounds = cell.transform.Find("Bounds");
            if (bounds == null) return;
            Transform west = null;
            foreach (Transform b in bounds)
                if (b.GetComponent<BoxCollider>() != null && (west == null || b.localPosition.x < west.localPosition.x)) west = b;
            if (west == null) return;
            var bc = west.GetComponent<BoxCollider>();
            float bz0 = west.localPosition.z + bc.center.z - bc.size.z * 0.5f, bz1 = west.localPosition.z + bc.center.z + bc.size.z * 0.5f;
            if (bz0 >= OZ0 || bz1 <= OZ1) return;
            var lp = west.localPosition;
            west.localPosition = new Vector3(lp.x, lp.y, (bz0 + OZ0) * 0.5f - bc.center.z);
            bc.size = new Vector3(bc.size.x, bc.size.y, OZ0 - bz0);
            var north = new GameObject("Bound");
            north.transform.SetParent(bounds, false);
            north.transform.localPosition = new Vector3(lp.x, lp.y, (OZ1 + bz1) * 0.5f);
            var nc = north.AddComponent<BoxCollider>();
            nc.center = new Vector3(bc.center.x, bc.center.y, 0f);
            nc.size = new Vector3(bc.size.x, bc.size.y, bz1 - OZ1);
        }

        /// <summary>The doorway from the promenade: two granite steps, a Portland stone surround with a keystone, the mews'
        /// street plate over it and a bracket lantern beside it (the stretch's only real light).</summary>
        static void Doorway(Transform ex, Pal p)
        {
            var k = new KitBatch();
            k.Box(p.Granite, new Vector3(-3.81f, 0.115f, OZC), new Vector3(0.38f, 0.23f, OZ1 - OZ0 + 0.2f), 1.2f);
            k.Box(p.Granite, new Vector3(-4.2f, 0.23f, OZC), new Vector3(0.4f, 0.46f, OZ1 - OZ0 + 0.2f), 1.2f);
            Floor(ex, "DoorStep1", new Vector3(-3.81f, 0.115f, OZC), new Vector3(0.38f, 0.23f, OZ1 - OZ0));
            Floor(ex, "DoorStep2", new Vector3(-4.2f, 0.23f, OZC), new Vector3(0.4f, 0.46f, OZ1 - OZ0));
            foreach (float z in new[] { OZ0 - 0.1f, OZ1 + 0.1f }) k.Box(p.Stone, new Vector3(WallX + 0.05f, OTop * 0.5f, z), new Vector3(0.1f, OTop, 0.2f), 2f);
            k.Box(p.Stone, new Vector3(WallX + 0.06f, OTop + 0.03f, OZC), new Vector3(0.12f, 0.1f, OZ1 - OZ0 + 0.4f), 2f);
            k.Box(p.Stone, new Vector3(WallX + 0.08f, OTop + 0.12f, OZC), new Vector3(0.16f, 0.5f, 0.36f), 2f);
            // The street plate: white enamel, black letters, the district in red (no crowns or ciphers).
            k.Box(p.Paint, new Vector3(WallX + 0.015f, 3.45f, OZC), new Vector3(0.03f, 0.28f, 1.5f));
            k.Box(p.Iron, new Vector3(WallX + 0.031f, 3.57f, OZC), new Vector3(0.004f, 0.012f, 1.44f));
            k.Box(p.Iron, new Vector3(WallX + 0.031f, 3.33f, OZC), new Vector3(0.004f, 0.012f, 1.44f));
            Text(ex, "LAMPLIGHTER MEWS  <color=#B3261E>SW1</color>", 0.075f, new Color(0.08f, 0.08f, 0.08f), new Vector3(WallX + 0.035f, 3.45f, OZC), Quaternion.Euler(0f, -90f, 0f), 1.45f, false);
            WallLantern(k, p, new Vector3(WallX, 2.3f, 44.85f), 1f);
            k.Bake(ex, "MewsDoorway");
            MakeLight(ex, new Vector3(-3.85f, 2.6f, 44.85f), new Color(1f, 0.78f, 0.5f), 1.4f, 6.5f);
        }

        /// <summary>
        /// The vault under the terrace: sooty brick walls and barrel vault, granite floor, a caged bulb, a torn festival
        /// poster, a shelf, and eleven scratched tally marks with a twelfth set apart.
        /// </summary>
        static void Vault(Transform ex, Pal p)
        {
            var k = new KitBatch();
            float x0 = WallBack, x1 = RWB, xc = (x0 + x1) * 0.5f, len = x0 - x1;
            Floor(ex, "MewsVaultFloor", new Vector3((WallX + RWX) * 0.5f, Y0 - 0.15f, OZC), new Vector3(WallX - RWX, 0.3f, OZ1 - OZ0), p.Granite);
            k.Box(p.Soot, new Vector3(xc, (Y0 + VSpring) * 0.5f, OZ0 - 0.125f), new Vector3(len, VSpring - Y0, 0.25f), 1.4f);
            k.Box(p.Soot, new Vector3(xc, (Y0 + VSpring) * 0.5f, OZ1 + 0.125f), new Vector3(len, VSpring - Y0, 0.25f), 1.4f);
            k.Of(p.Soot).AddArch(new Vector3((WallBack + RWX) * 0.5f, VSpring, OZC), VR, VR + 0.2f, WallBack - RWX, 18, Quaternion.Euler(0f, 90f, 0f), 1.4f);
            // Walls through the bound's thickness to the back wall, so nobody slips along inside the embankment wall.
            foreach (float z in new[] { OZ0 - 0.125f, OZ1 + 0.125f }) Col(ex, "VaultWall", new Vector3((-4.25f + RWX) * 0.5f, 2f, z), new Vector3(-4.25f - RWX, 4f, 0.25f));
            // Caged bulb at the crown.
            var bulb = new Vector3(-7.4f, VSpring + VR - 0.14f, OZC);
            k.Box(p.Iron, bulb + new Vector3(0f, 0.08f, 0f), new Vector3(0.012f, 0.12f, 0.012f));
            k.Of(p.Glow).AddSphere(bulb - new Vector3(0f, 0.04f, 0f), 0.045f, 8, 6);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f * Mathf.Deg2Rad;
                k.Box(p.Iron, bulb + new Vector3(Mathf.Cos(a) * 0.065f, -0.04f, Mathf.Sin(a) * 0.065f), new Vector3(0.008f, 0.16f, 0.008f));
            }
            k.Of(p.Iron).AddTorus(bulb - new Vector3(0f, 0.04f, 0f), 0.068f, 0.006f, 10, 3, Quaternion.identity);
            var l = MakeLight(ex, bulb - new Vector3(0f, 0.12f, 0f), new Color(1f, 0.82f, 0.6f), 0.85f, 4.5f);
            Flicker.Add(l, 0.1f, 1.6f);
            // A shelf under the postcard and the tally marks beside it (on the south wall, facing into the vault).
            k.Box(p.Iron, new Vector3(-7.0f, 1.28f, OZ0 + 0.06f), new Vector3(0.3f, 0.02f, 0.12f));
            for (int i = 0; i < 11; i++)
            {
                float x = -7.75f + (i % 5) * 0.045f + (i / 5) * 0.3f;
                bool strike = i % 5 == 4;
                if (strike) k.Box(p.Paint, new Vector3(x - 0.09f, 1.55f, OZ0 + 0.003f), new Vector3(0.22f, 0.008f, 0.004f), Quaternion.Euler(0f, 0f, 28f));
                else k.Box(p.Paint, new Vector3(x, 1.55f, OZ0 + 0.003f), new Vector3(0.008f, 0.18f, 0.004f));
            }
            k.Box(p.BrickDark, new Vector3(-7.75f + 0.3f * 2f + 0.25f, 1.53f, OZ0 + 0.003f), new Vector3(0.012f, 0.22f, 0.004f), Quaternion.Euler(0f, 0f, -6f));
            k.Bake(ex, "MewsVault");
            var tex = Tex.Art("postcard_3");
            if (tex != null)
            {
                var q = MeshFactory.Quad("Poster", ex, 0.6f, 0.38f, Mats.Printed(tex, new Color(0.6f, 0.58f, 0.54f), 0.2f), new Vector3(-6.2f, 1.7f, OZ1 - 0.01f), Quaternion.identity);
                q.transform.localRotation = Quaternion.Euler(0f, 0f, 3f);
                q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            UrbanKit.Puddles(ex, new Vector3(-6.8f, Y0, OZC), new Vector2(2.5f, 1.2f), 2, 4242, p.Puddle);
        }

        // ------------------------------------------------------------------ the terrace's back wall
        /// <summary>
        /// The terrace seen from behind, in yellow stock brick: blind arches at lane level (two with old doors), the vault's
        /// arch, a string course at the terrace's floor, three storeys of sash windows and attic lights over each house,
        /// downpipes at the party walls, a coping at the roofline.
        /// </summary>
        static void BackWall(Transform ex, Pal p, System.Random rnd)
        {
            var k = new KitBatch();
            float t = RWB - RWX, xc = (RWX + RWB) * 0.5f;
            Solid(k, ex, p.Stock, new Vector3(xc, (Y0 + RH) * 0.5f, (LZ0 + OZ0) * 0.5f), new Vector3(t, RH - Y0, OZ0 - LZ0), 1.4f);
            Solid(k, ex, p.Stock, new Vector3(xc, (Y0 + RH) * 0.5f, (OZ1 + LZ1) * 0.5f), new Vector3(t, RH - Y0, LZ1 - OZ1), 1.4f);
            float top = VSpring + VR + 0.3f;
            Solid(k, ex, p.Stock, new Vector3(xc, (top + RH) * 0.5f, OZC), new Vector3(t, RH - top, OZ1 - OZ0), 1.4f);
            var poly = new List<Vector2>();
            for (int i = 0; i <= 16; i++)
            {
                float a = Mathf.PI * (1f - i / 16f);
                poly.Add(new Vector2(-OZC + Mathf.Cos(a) * VR, VSpring + Mathf.Sin(a) * VR)); // polygon x = -z (extruded along x)
            }
            poly.Add(new Vector2(-OZ0, top));
            poly.Add(new Vector2(-OZ1, top));
            k.Of(p.Stock).AddExtrudedXY(poly, t, new Vector3(xc, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), 1.4f);

            var f = new Face(new Vector3(RWX, 0f, LZ0), Vector3.left); // u = z - 33
            float vu = OZC - LZ0;
            k.Of(p.Stone).AddArch(f.P(vu, VSpring, 0.04f), VR, VR + 0.26f, 0.08f, 16, f.Q, 2f);
            B(k, p.Stone, f, vu, VSpring + VR + 0.2f, 0.06f, 0.36f, 0.5f, 0.12f, 2f);
            B(k, p.Paint, f, vu, 4.42f, 0.02f, 0.9f, 0.22f, 0.02f);
            Text(ex, "TO THE RIVER", 0.06f, new Color(0.08f, 0.08f, 0.08f), f.P(vu, 4.42f, 0.035f), f.TextRot, 0.86f, false);
            foreach (float zc in new[] { 34f, 38.5f, 47.5f, 52f })
            {
                float u = zc - LZ0;
                var fill = new List<Vector2>();
                for (int i = 0; i <= 12; i++) { float a = Mathf.PI * i / 12f; fill.Add(new Vector2(Mathf.Cos(a) * 1.05f, Mathf.Sin(a) * 1.05f)); }
                k.Of(p.Soot).AddExtrudedXY(fill, 0.02f, f.P(u, Y0 + 1.9f, 0.006f), f.Q, 1.4f);
                B(k, p.Soot, f, u, Y0 + 0.95f, 0.006f, 2.1f, 1.9f, 0.012f, 1.4f);
                k.Of(p.Stone).AddArch(f.P(u, Y0 + 1.9f, 0.03f), 1.05f, 1.27f, 0.06f, 14, f.Q, 2f);
                B(k, p.Stone, f, u - 1.16f, Y0 + 1.86f, 0.035f, 0.34f, 0.1f, 0.07f, 2f);
                B(k, p.Stone, f, u + 1.16f, Y0 + 1.86f, 0.035f, 0.34f, 0.1f, 0.07f, 2f);
                if (zc == 38.5f || zc == 52f)
                {
                    B(k, p.Doors[4], f, u, Y0 + 1.05f, 0.025f, 1.2f, 2.1f, 0.04f);
                    foreach (float y in new[] { 0.45f, 1.6f }) B(k, p.Iron, f, u - 0.2f, Y0 + y, 0.05f, 0.75f, 0.05f, 0.012f);
                    k.Of(p.Iron).AddSphere(f.P(u + 0.42f, Y0 + 1.05f, 0.06f), 0.03f, 6, 4);
                }
            }
            B(k, p.Stone, f, 11f, 4.12f, 0.04f, 22f, 0.16f, 0.08f, 2f);
            // Back windows: three storeys and an attic light over each house (houses every 4.5 m centred on z = -20 + 4.5 i).
            foreach (float zc in new[] { 34f, 38.5f, 43f, 47.5f, 52f })
                foreach (float o in new[] { -1.1f, 1.1f })
                {
                    float u = zc - LZ0 + o;
                    if (u < 0.6f || u > 21.4f) continue;
                    foreach (var (sill, h) in new[] { (5.0f, 1.8f), (8.0f, 2.0f), (11.0f, 1.6f) })
                        Sash(k, p, f, u, sill, 0.95f, h, rnd.NextDouble() < 0.3);
                    Sash(k, p, f, u, 14.2f, 0.8f, 1.15f, rnd.NextDouble() < 0.2);
                }
            foreach (float zp in new[] { 36.25f, 40.75f, 45.25f, 49.75f, 54.25f })
            {
                float u = zp - LZ0;
                k.Of(p.Iron).AddCylinder(f.P(u, Y0, 0.08f), 0.045f, RH - 0.3f - Y0, 8);
                B(k, p.Iron, f, u, RH - 0.35f, 0.1f, 0.2f, 0.22f, 0.18f);
                B(k, p.Iron, f, u, Y0 + 0.08f, 0.12f, 0.1f, 0.08f, 0.18f);
            }
            B(k, p.Stone, f, 11f, RH + 0.06f, 0.02f, 22f, 0.12f, 0.42f, 2f);
            k.Bake(ex, "MewsBackWall");
        }

        /// <summary>A sash window on a face: glass, white sashes with glazing bars, a stone sill and a gauged-brick lintel.</summary>
        static void Sash(KitBatch k, Pal p, Face f, float u, float sill, float w, float h, bool lit)
        {
            float yc = sill + h * 0.5f;
            B(k, lit ? p.WindowLit : p.WindowDark, f, u, yc, 0.005f, w, h, 0.01f);
            B(k, p.Paint, f, u, sill + 0.025f, 0.02f, w, 0.05f, 0.02f);
            B(k, p.Paint, f, u, sill + h - 0.025f, 0.02f, w, 0.05f, 0.02f);
            B(k, p.Paint, f, u - w * 0.5f + 0.025f, yc, 0.02f, 0.05f, h, 0.02f);
            B(k, p.Paint, f, u + w * 0.5f - 0.025f, yc, 0.02f, 0.05f, h, 0.02f);
            B(k, p.Paint, f, u, yc, 0.025f, w, 0.045f, 0.02f);
            B(k, p.Paint, f, u, yc, 0.018f, 0.018f, h, 0.015f);
            foreach (float q in new[] { 0.25f, 0.75f }) B(k, p.Paint, f, u, sill + q * h, 0.018f, w, 0.018f, 0.015f);
            B(k, p.Stone, f, u, sill - 0.035f, 0.06f, w + 0.24f, 0.07f, 0.12f);
            B(k, p.BrickDark, f, u, sill + h + 0.12f, 0.02f, w + 0.24f, 0.24f, 0.04f);
        }

        // ------------------------------------------------------------------ the coach houses
        /// <summary>
        /// Five two-storey coach houses facing the lane, each in its own pastel stucco: coach doors, painted front doors
        /// with fanlights, window boxes, hanging baskets that sway; the north one is a pub, the Minute Hand, with lit windows,
        /// gold lettering and a sign swinging on its bracket. Slate roofs behind a parapet, chimneys on the party walls.
        /// </summary>
        static void CoachHouses(Transform ex, Pal p, System.Random rnd)
        {
            var k = new KitBatch();
            Solid(k, ex, p.Stucco[0], new Vector3((MB + MX) * 0.5f, (Y0 + MH) * 0.5f, (LZ0 + LZ1) * 0.5f), new Vector3(MX - MB, MH - Y0, LZ1 - LZ0), 2.5f);
            k.Of(p.Slate).AddExtrudedXY(new List<Vector2> { new Vector2(-21.1f, 7.0f), new Vector2(-16.05f, 7.0f), new Vector2(-16.05f, 7.1f),
                new Vector2(-18.4f, 9.3f), new Vector2(-18.7f, 9.3f), new Vector2(-21.1f, 7.1f) }, LZ1 - LZ0, new Vector3(0f, 0f, (LZ0 + LZ1) * 0.5f), Quaternion.identity, 1.5f);
            var f = new Face(new Vector3(MX, 0f, LZ1), Vector3.right); // u = 55 - z
            for (int h = 0; h < 5; h++)
            {
                float u0 = h * 4.4f, uc = u0 + 2.2f;
                var stucco = p.Stucco[h];
                B(k, stucco, f, uc, (Y0 + 6.7f) * 0.5f, 0.02f, 4.4f, 6.7f - Y0, 0.04f, 2.5f);
                B(k, stucco, f, uc, 6.98f, 0.1f, 4.4f, 0.56f, 0.2f, 2.5f);
                B(k, p.Stone, f, uc, MH + 0.04f, 0.12f, 4.42f, 0.08f, 0.3f, 2f);
                B(k, p.Stone, f, uc, 6.62f, 0.08f, 4.42f, 0.12f, 0.16f, 2f);
                B(k, stucco, f, uc, 3.66f, 0.06f, 4.4f, 0.1f, 0.1f, 2.5f);
                if (h > 0) B(k, p.Paint, f, u0, (Y0 + MH) * 0.5f, 0.07f, 0.22f, MH - Y0, 0.1f);
                bool pub = h == 0;
                foreach (float o in new[] { 1.2f, 3.3f }) MewsWindow(k, p, f, u0 + o, 4.26f, 1.0f, 1.5f, pub || rnd.NextDouble() < 0.35, !pub || o > 2f, rnd);
                if (pub) continue;
                // Coach door (two boarded leaves with top lights), the front door with fanlight and knocker, a step, a lamp.
                var door = p.Doors[h % p.Doors.Length];
                var coach = h % 2 == 0 ? p.Wood : door;
                B(k, coach, f, u0 + 1.4f, Y0 + 1.25f, 0.03f, 2.3f, 2.5f, 0.05f);
                for (int i = 1; i < 12; i++) if (i != 6) B(k, p.Black, f, u0 + 0.25f + i * 2.3f / 12f, Y0 + 0.95f, 0.058f, 0.008f, 1.8f, 0.006f);
                B(k, p.Black, f, u0 + 1.4f, Y0 + 1.25f, 0.06f, 0.025f, 2.5f, 0.01f);
                foreach (float lu in new[] { 0.75f, 2.05f }) B(k, p.WindowDark, f, u0 + lu, Y0 + 2.15f, 0.058f, 0.9f, 0.4f, 0.01f);
                B(k, p.Paint, f, u0 + 1.4f, Y0 + 2.58f, 0.06f, 2.5f, 0.12f, 0.08f);
                float du = u0 + 3.55f;
                B(k, door, f, du, Y0 + 1.075f, 0.03f, 0.95f, 2.15f, 0.05f);
                foreach (float pu in new[] { -0.22f, 0.22f })
                    foreach (var (py, ph) in new[] { (0.55f, 0.7f), (1.5f, 0.85f) })
                        B(k, door, f, du + pu, Y0 + py, 0.062f, 0.34f, ph, 0.014f);
                k.Of(p.Gold).AddTorus(f.P(du, Y0 + 1.45f, 0.075f), 0.05f, 0.008f, 12, 4, f.Q * Quaternion.Euler(90f, 0f, 0f));
                k.Of(p.Gold).AddSphere(f.P(du + 0.33f, Y0 + 1.0f, 0.075f), 0.025f, 6, 4);
                var fan = new List<Vector2>();
                for (int i = 0; i <= 10; i++) { float a = Mathf.PI * i / 10f; fan.Add(new Vector2(Mathf.Cos(a) * 0.475f, Mathf.Sin(a) * 0.475f)); }
                k.Of(p.WindowLit).AddExtrudedXY(fan, 0.01f, f.P(du, Y0 + 2.2f, 0.02f), f.Q);
                k.Of(p.Paint).AddArch(f.P(du, Y0 + 2.2f, 0.04f), 0.475f, 0.56f, 0.06f, 10, f.Q, 1f);
                B(k, p.Stone, f, du, Y0 + 0.07f, 0.16f, 1.25f, 0.14f, 0.3f, 2f);
                WallLamp(k, p, f, du + 0.75f, Y0 + 2.35f);
                Text(ex, (h + 1).ToString(), 0.08f, new Color(0.92f, 0.86f, 0.6f), f.P(du, Y0 + 2.45f, 0.05f), f.TextRot, 0.3f, true);
                if (h == 1 || h == 2)
                {
                    var pot = f.P(du - 0.75f, Y0, 0.35f);
                    k.Box(p.Black, pot + Vector3.up * 0.22f, new Vector3(0.4f, 0.44f, 0.4f));
                    k.Of(p.Leaf).AddSphere(pot + Vector3.up * 0.85f, 0.32f, 10, 7);
                    k.Of(p.Wood).AddCylinder(pot + Vector3.up * 0.44f, 0.025f, 0.3f, 6);
                    Col(ex, "BayTree", pot + Vector3.up * 0.5f, new Vector3(0.44f, 1.0f, 0.44f));
                }
                HangingBasket(ex, p, f, u0 + 2.85f, Y0 + 2.75f, h);
            }
            Pub(ex, k, p, f);
            foreach (float z in new[] { 37.4f, 41.8f, 46.2f, 50.6f })
            {
                var c = new Vector3(-18.55f, 9.0f, z);
                k.Box(p.Brick, c + Vector3.up * 0.75f, new Vector3(0.7f, 1.5f, 0.9f), 1.4f);
                k.Box(p.Stone, c + Vector3.up * 1.53f, new Vector3(0.8f, 0.1f, 1.0f));
                for (int i = 0; i < 3; i++) k.Of(p.Pot).AddCylinder(c + new Vector3(0f, 1.58f, -0.25f + i * 0.25f), 0.07f, 0.34f, 8);
            }
            k.Bake(ex, "MewsCoachHouses");
        }

        static void MewsWindow(KitBatch k, Pal p, Face f, float u, float sill, float w, float h, bool lit, bool box, System.Random rnd)
        {
            Sash(k, p, f, u, sill, w, h, lit);
            if (!box) return;
            B(k, p.Black, f, u, sill - 0.12f, 0.14f, w, 0.18f, 0.2f);
            for (int i = 0; i < 5; i++)
            {
                float o = -0.4f + i * 0.2f;
                k.Of(p.Leaf).AddSphere(f.P(u + o, sill - 0.02f, 0.16f), 0.08f, 6, 4);
                k.Of(p.Flowers[rnd.Next(p.Flowers.Length)]).AddSphere(f.P(u + o + 0.05f, sill + 0.03f, 0.18f), 0.055f, 6, 4);
            }
        }

        static void WallLamp(KitBatch k, Pal p, Face f, float u, float y)
        {
            B(k, p.Iron, f, u, y, 0.02f, 0.08f, 0.16f, 0.04f);
            B(k, p.Iron, f, u, y + 0.04f, 0.12f, 0.03f, 0.03f, 0.2f);
            B(k, p.Glow, f, u, y - 0.05f, 0.2f, 0.12f, 0.2f, 0.12f);
            B(k, p.Iron, f, u, y + 0.07f, 0.2f, 0.16f, 0.03f, 0.16f);
        }

        /// <summary>A basket of trailing flowers on three chains from a bracket; it swings a little in the wind.</summary>
        static void HangingBasket(Transform ex, Pal p, Face f, float u, float y, int seed)
        {
            var bk = new KitBatch();
            bk.Box(p.Iron, f.P(u, y, 0.24f), new Vector3(0.025f, 0.025f, 0.48f), f.Q);
            bk.Of(p.Iron).AddTube(new List<Vector3> { f.P(u, y - 0.3f, 0.02f), f.P(u, y - 0.12f, 0.2f), f.P(u, y - 0.01f, 0.42f) }, 0.01f, 5);
            bk.Bake(ex, "BasketBracket");
            var pivot = new GameObject("HangingBasket").transform;
            pivot.SetParent(ex, false);
            pivot.localPosition = f.P(u, y - 0.01f, 0.44f);
            var k = new KitBatch();
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                var lo = new Vector3(Mathf.Cos(a) * 0.17f, -0.34f, Mathf.Sin(a) * 0.17f);
                k.Box(p.Iron, lo * 0.5f, new Vector3(0.006f, 0.38f, 0.006f), Quaternion.FromToRotation(Vector3.up, -lo.normalized));
            }
            k.Of(p.Moss).AddSphere(new Vector3(0f, -0.42f, 0f), 0.2f, 10, 6, 1f, -90f, 10f);
            var rnd = new System.Random(300 + seed);
            for (int i = 0; i < 9; i++)
            {
                float a = i * 0.7f;
                var c = new Vector3(Mathf.Cos(a) * 0.17f, -0.36f + (float)rnd.NextDouble() * 0.06f, Mathf.Sin(a) * 0.17f);
                k.Of(p.Flowers[(i + seed) % p.Flowers.Length]).AddSphere(c, 0.06f, 6, 4);
                k.Of(p.Leaf).AddSphere(c * 1.15f + Vector3.down * (0.12f + (i % 3) * 0.08f), 0.05f, 5, 4);
            }
            k.Bake(pivot, "Basket", false);
            WindSway.Add(pivot, 5f, 2.3f);
        }

        /// <summary>The Minute Hand: a dark green pub front with lit windows and a glazed door, gold letters on the fascia,
        /// baskets either side of the door, two barrels for tables, and a pictorial sign (a clock at a minute to twelve)
        /// swinging over the lane.</summary>
        static void Pub(Transform ex, KitBatch k, Pal p, Face f)
        {
            B(k, p.PubGreen, f, 2.2f, (Y0 + 3.6f) * 0.5f, 0.05f, 4.4f, 3.6f - Y0, 0.1f);
            foreach (var (uc, w) in new[] { (1.05f, 1.5f), (3.55f, 1.1f) })
            {
                B(k, p.PubGlow, f, uc, Y0 + 1.55f, 0.105f, w, 1.7f, 0.01f);
                for (int i = 1; i < 3; i++) B(k, p.PubGreen, f, uc - w * 0.5f + i * w / 3f, Y0 + 1.55f, 0.115f, 0.05f, 1.7f, 0.03f);
                B(k, p.PubGreen, f, uc, Y0 + 1.95f, 0.115f, w, 0.04f, 0.03f);
                B(k, p.Black, f, uc, Y0 + 1.0f, 0.112f, w - 0.1f, 0.03f, 0.01f);
                for (int i = 0; i < 5; i++) B(k, p.Black, f, uc - w * 0.35f + i * w * 0.17f, Y0 + 1.12f, 0.112f, 0.05f, 0.16f + (i % 2) * 0.05f, 0.01f);
            }
            B(k, p.PubGlow, f, 2.4f, Y0 + 1.6f, 0.105f, 0.76f, 1.0f, 0.01f);
            B(k, p.PubGreen, f, 2.4f, Y0 + 0.55f, 0.11f, 0.86f, 1.1f, 0.03f);
            k.Of(p.Gold).AddSphere(f.P(2.68f, Y0 + 1.05f, 0.13f), 0.03f, 6, 4);
            B(k, p.Black, f, 2.2f, 3.27f, 0.14f, 4.4f, 0.56f, 0.08f);
            B(k, p.Gold, f, 2.2f, 3.0f, 0.18f, 4.4f, 0.03f, 0.02f);
            B(k, p.Gold, f, 2.2f, 3.54f, 0.18f, 4.4f, 0.03f, 0.02f);
            Text(ex, "THE MINUTE HAND", 0.2f, new Color(0.9f, 0.74f, 0.4f), f.P(2.2f, 3.27f, 0.19f), f.TextRot, 4.2f, true);
            foreach (float bu in new[] { 1.0f, 3.6f })
            {
                var c = f.P(bu, Y0, 0.85f);
                k.Of(p.Wood).AddLathe(c, new List<Vector2> { new Vector2(0.26f, 0f), new Vector2(0.3f, 0.45f), new Vector2(0.26f, 0.9f), new Vector2(0.001f, 0.9f) }, 14, 1f);
                foreach (float y in new[] { 0.12f, 0.78f }) k.Of(p.Iron).AddTorus(c + Vector3.up * y, 0.28f, 0.012f, 16, 3, Quaternion.identity);
                var col = new GameObject("Barrel");
                col.transform.SetParent(ex, false);
                col.transform.localPosition = c + Vector3.up * 0.45f;
                var cap = col.AddComponent<CapsuleCollider>();
                cap.radius = 0.3f; cap.height = 0.9f;
            }
            HangingBasket(ex, p, f, 1.9f, Y0 + 2.6f, 7);
            HangingBasket(ex, p, f, 2.95f, Y0 + 2.6f, 9);
            // The sign: a bracket off the facade at the pub's south end, the board hanging across the lane's axis.
            var bk = new KitBatch();
            bk.Box(p.Iron, f.P(4.15f, 3.95f, 0.5f), new Vector3(0.03f, 0.03f, 1.0f), f.Q);
            bk.Of(p.Iron).AddTube(new List<Vector3> { f.P(4.15f, 3.5f, 0.03f), f.P(4.15f, 3.8f, 0.4f), f.P(4.15f, 3.94f, 0.85f) }, 0.014f, 5);
            bk.Bake(ex, "PubSignBracket");
            var pivot = new GameObject("PubSign").transform;
            pivot.SetParent(ex, false);
            pivot.localPosition = f.P(4.15f, 3.93f, 0.62f);
            pivot.localRotation = Quaternion.LookRotation(f.R, Vector3.up); // the board's faces look up and down the lane
            var sk = new KitBatch();
            foreach (float sx in new[] { -0.25f, 0.25f }) sk.Box(p.Iron, new Vector3(sx, -0.06f, 0f), new Vector3(0.008f, 0.12f, 0.008f));
            sk.Box(p.PubGreen, new Vector3(0f, -0.45f, 0f), new Vector3(0.66f, 0.66f, 0.04f));
            sk.Box(p.Gold, new Vector3(0f, -0.45f, 0f), new Vector3(0.7f, 0.7f, 0.025f));
            var dial = Mats.Lit(new Color(0.88f, 0.84f, 0.72f), 0.4f);
            foreach (float side in new[] { 1f, -1f })
            {
                sk.Of(dial).AddDisc(new Vector3(0f, -0.42f, side * 0.022f), new Vector3(0f, 0f, side), 0.2f, 24);
                var sf = new Face(new Vector3(0f, -0.42f, 0f), new Vector3(0f, 0f, side));
                foreach (var (deg, len, wid) in new[] { (-0.5f, 0.11f, 0.018f), (-6f, 0.17f, 0.012f) })
                {
                    float a = deg * Mathf.Deg2Rad;
                    var dir = sf.R * -Mathf.Sin(a) + Vector3.up * Mathf.Cos(a);
                    sk.Box(p.Black, sf.P(0f, 0f, 0.026f) + dir * len * 0.5f, new Vector3(wid, len, 0.005f), Quaternion.LookRotation(sf.N, dir));
                }
            }
            sk.Bake(pivot, "Board", false);
            Text(pivot, "THE MINUTE HAND", 0.045f, new Color(0.92f, 0.76f, 0.42f), new Vector3(0f, -0.71f, -0.024f), Quaternion.identity, 0.62f, true);
            Text(pivot, "THE MINUTE HAND", 0.045f, new Color(0.92f, 0.76f, 0.42f), new Vector3(0f, -0.71f, 0.024f), Quaternion.Euler(0f, 180f, 0f), 0.62f, true);
            WindSway.Add(pivot, 6f, 2.6f, Vector3.right);
            // A festival banner on a short pole between the pub's upper windows.
            var flagMat = Mats.Lit(new Color(0.8f, 0.14f, 0.12f), 0.25f, 0f, "fabric_linen");
            var pk = new KitBatch();
            pk.Of(p.Iron).AddCylinder(f.P(2.25f, 5.35f, 0f), 0.02f, 1.0f, 6, 1f, f.Q * Quaternion.Euler(90f, 0f, 0f));
            pk.Of(p.Gold).AddSphere(f.P(2.25f, 5.35f, 1.0f), 0.04f, 6, 4);
            pk.Bake(ex, "BannerPole");
            var banner = ClothFlag.Create(ex, "PubBanner", f.P(2.25f, 5.32f, 0.08f), 1.3f, 0.82f, flagMat, 0f, 0.08f);
            banner.transform.localRotation = Quaternion.LookRotation(Vector3.Cross(f.N, Vector3.down), -f.N);
        }

        // ------------------------------------------------------------------ the ends of the lane
        /// <summary>Tall brick ends close the lane: to the south a locked door, the street plate and a lamp over the phone
        /// box; to the north an archway with a padlocked iron gate and the lane running on into darkness behind it.</summary>
        static void EndWalls(Transform ex, Pal p)
        {
            var k = new KitBatch();
            const float t = 0.3f;
            Solid(k, ex, p.Brick, new Vector3((MB + RWB) * 0.5f, (Y0 + RH) * 0.5f, LZ0 - t * 0.5f), new Vector3(RWB - MB, RH - Y0, t), 1.4f);
            var s = new Face(new Vector3(MB, 0f, LZ0), Vector3.forward); // u = x + 21
            B(k, p.Doors[2], s, 9.5f, Y0 + 1.1f, 0.025f, 1.0f, 2.2f, 0.05f);
            B(k, p.Stone, s, 9.5f, Y0 + 2.35f, 0.04f, 1.3f, 0.14f, 0.08f, 2f);
            WallLamp(k, p, s, 9.5f, Y0 + 2.75f);
            B(k, p.Paint, s, 7.6f, Y0 + 3.25f, 0.015f, 1.4f, 0.26f, 0.03f); // above the telephone kiosk's dome
            Text(ex, "LAMPLIGHTER MEWS  <color=#B3261E>SW1</color>", 0.07f, new Color(0.08f, 0.08f, 0.08f), s.P(7.6f, Y0 + 3.25f, 0.035f), s.TextRot, 1.35f, false);
            foreach (float y in new[] { 6.4f, 9.9f, 13.4f }) Sash(k, p, s, 8.2f, y, 1.0f, 1.6f, false);
            // North: the end with its gateway (the wall in pieces round it), the gate, the dark lane beyond.
            const float gu0 = 2.0f, gu1 = 4.6f, gh = 3.2f;
            var n = new Face(new Vector3(RWB, 0f, LZ1), Vector3.back); // u = -9.5 - x
            float W = RWB - MB;
            void NorthPiece(float u0, float u1, float y0, float y1)
            {
                var c = n.P((u0 + u1) * 0.5f, (y0 + y1) * 0.5f, -t * 0.5f);
                Solid(k, ex, p.Brick, c, new Vector3(u1 - u0, y1 - y0, t), 1.4f);
            }
            NorthPiece(0f, gu0, Y0, RH);
            NorthPiece(gu1, W, Y0, RH);
            NorthPiece(gu0, gu1, Y0 + gh, RH);
            float gc = (gu0 + gu1) * 0.5f;
            B(k, p.Stone, n, gc, Y0 + gh + 0.1f, 0.04f, gu1 - gu0 + 0.4f, 0.2f, 0.08f, 2f);
            foreach (float u in new[] { gu0 - 0.1f, gu1 + 0.1f }) B(k, p.Stone, n, u, Y0 + gh * 0.5f, 0.04f, 0.2f, gh, 0.08f, 2f);
            B(k, p.Stone, n, gc, Y0 + gh + 0.22f, 0.06f, 0.34f, 0.44f, 0.12f, 2f);
            for (float u = gu0 + 0.08f; u < gu1 - 0.05f; u += 0.11f)
            {
                B(k, p.Iron, n, u, Y0 + 1.45f, 0.08f, 0.018f, 2.9f, 0.018f);
                k.Of(p.Iron).AddFrustum(n.P(u, Y0 + 2.9f, 0.08f), 0.025f, 0f, 0.1f, 4);
            }
            foreach (float y in new[] { 0.12f, 1.4f, 2.75f }) B(k, p.Iron, n, gc, Y0 + y, 0.08f, gu1 - gu0, 0.035f, 0.025f);
            B(k, p.Iron, n, gc + 0.05f, Y0 + 1.25f, 0.12f, 0.09f, 0.11f, 0.05f);
            Col(ex, "MewsGate", n.P(gc, Y0 + 1.75f, 0.08f), new Vector3(gu1 - gu0, 3.5f, 0.12f));
            B(k, p.Paint, n, gc - 0.6f, Y0 + 1.9f, 0.1f, 0.42f, 0.16f, 0.01f);
            Text(ex, "PRIVATE", 0.05f, new Color(0.1f, 0.1f, 0.1f), n.P(gc - 0.6f, Y0 + 1.9f, 0.108f), n.TextRot, 0.4f, false);
            // Beyond the gate: walls, a ceiling and a black end, so the lane seems to run on into the dark.
            foreach (float u in new[] { gu0 - 0.1f, gu1 + 0.1f }) B(k, p.Soot, n, u, Y0 + gh * 0.5f, -1.2f, 0.2f, gh, 1.8f, 1.4f);
            B(k, p.Soot, n, gc, Y0 + gh + 0.1f, -1.2f, gu1 - gu0 + 0.4f, 0.2f, 1.8f, 1.4f);
            B(k, p.Void, n, gc, Y0 + gh * 0.5f, -2.15f, gu1 - gu0 + 0.4f, gh + 0.2f, 0.1f);
            B(k, p.Void, n, gc, Y0 - 0.01f, -1.2f, gu1 - gu0, 0.02f, 1.8f);
            foreach (float y in new[] { 6.4f, 9.9f, 13.4f }) Sash(k, p, n, 7.5f, y, 1.0f, 1.6f, y > 9f);
            k.Bake(ex, "MewsEnds");
        }

        // ------------------------------------------------------------------ the telephone box
        /// <summary>
        /// A red telephone kiosk at the lane's south end, its door propped open toward the lane: lit glazing behind a red
        /// grid, TELEPHONE on its header, a black telephone on the back wall with its receiver on the cradle. The receiver is
        /// <see cref="LondonMewsPhone"/>: it rings as you come into the mews; lift it and something whispers.
        /// </summary>
        static void PhoneBox(Transform ex, Pal p)
        {
            var root = new GameObject("TelephoneKiosk").transform;
            root.SetParent(ex, false);
            root.localPosition = PhoneAt;
            var k = new KitBatch();
            var red = p.Red;
            k.Box(red, new Vector3(0f, 0.06f, 0f), new Vector3(0.95f, 0.12f, 0.95f));
            foreach (float cx in new[] { -0.42f, 0.42f }) foreach (float cz in new[] { -0.42f, 0.42f }) k.Box(red, new Vector3(cx, 1.16f, cz), new Vector3(0.11f, 2.08f, 0.11f));
            k.Box(red, new Vector3(0f, 2.32f, 0f), new Vector3(0.95f, 0.24f, 0.95f));
            k.Box(red, new Vector3(0f, 2.47f, 0f), new Vector3(1.0f, 0.06f, 1.0f));
            k.Of(red).AddLathe(new Vector3(0f, 2.5f, 0f), new[] { new Vector2(0.47f, 0f), new Vector2(0.44f, 0.06f), new Vector2(0.36f, 0.12f), new Vector2(0.22f, 0.16f), new Vector2(0.01f, 0.175f) }, 16);
            k.Box(red, new Vector3(0f, 2.18f, 0f), new Vector3(0.86f, 0.02f, 0.86f));
            foreach (var n in new[] { Vector3.right, Vector3.left, Vector3.back })
            {
                var f = new Face(n * 0.43f, n);
                GlazedSide(k, p, f, 0f);
                B(k, p.SignWhite, f, 0f, 2.32f, 0.05f, 0.62f, 0.11f, 0.01f);
                Text(root, "TELEPHONE", 0.065f, new Color(0.05f, 0.05f, 0.05f), f.P(0f, 2.32f, 0.058f), f.TextRot, 0.6f, false);
            }
            var front = new Face(Vector3.forward * 0.43f, Vector3.forward);
            B(k, p.SignWhite, front, 0f, 2.32f, 0.05f, 0.62f, 0.11f, 0.01f);
            Text(root, "TELEPHONE", 0.065f, new Color(0.05f, 0.05f, 0.05f), front.P(0f, 2.32f, 0.058f), front.TextRot, 0.6f, false);
            B(k, red, front, 0f, 2.13f, 0f, 0.73f, 0.05f, 0.05f);
            // The telephone on the back wall: the unit, the coin box, a shelf with a directory.
            k.Box(p.Black, new Vector3(0f, 1.35f, -0.35f), new Vector3(0.24f, 0.32f, 0.1f));
            k.Box(p.Black, new Vector3(0f, 1.08f, -0.36f), new Vector3(0.2f, 0.18f, 0.08f));
            k.Box(p.Gold, new Vector3(0.06f, 1.12f, -0.318f), new Vector3(0.04f, 0.01f, 0.004f));
            k.Box(p.Iron, new Vector3(0f, 0.92f, -0.3f), new Vector3(0.5f, 0.02f, 0.2f));
            k.Box(Mats.Lit(new Color(0.8f, 0.72f, 0.4f), 0.3f, 0f, "paper_ivory"), new Vector3(-0.12f, 0.96f, -0.3f), new Vector3(0.18f, 0.06f, 0.14f));
            k.Box(p.Black, new Vector3(0f, 1.52f, -0.33f), new Vector3(0.2f, 0.03f, 0.05f)); // the cradle
            k.Of(p.Black).AddTube(new List<Vector3> { new Vector3(0.12f, 1.3f, -0.32f), new Vector3(0.16f, 1.2f, -0.28f), new Vector3(0.14f, 1.42f, -0.27f), new Vector3(0.1f, 1.53f, -0.31f) }, 0.006f, 4);
            k.Bake(root, "Kiosk");
            // The door, propped open on its east hinge.
            var hinge = new GameObject("KioskDoor").transform;
            hinge.SetParent(root, false);
            hinge.localPosition = new Vector3(0.42f, 0f, 0.45f);
            hinge.localRotation = Quaternion.Euler(0f, 72f, 0f);
            var dk = new KitBatch();
            var df = new Face(new Vector3(-0.42f, 0f, 0f), Vector3.forward);
            GlazedSide(dk, p, df, 0f);
            dk.Box(red, new Vector3(-0.78f, 1.2f, 0.04f), new Vector3(0.03f, 0.4f, 0.03f));
            dk.Bake(hinge, "Door");
            var doorCol = new GameObject("KioskDoorSolid");
            doorCol.transform.SetParent(hinge, false);
            doorCol.transform.localPosition = new Vector3(-0.42f, 1.16f, 0f);
            doorCol.AddComponent<BoxCollider>().size = new Vector3(0.8f, 2.2f, 0.06f);
            foreach (var (c, s) in new[] { (new Vector3(0.45f, 1.25f, 0f), new Vector3(0.06f, 2.5f, 0.95f)), (new Vector3(-0.45f, 1.25f, 0f), new Vector3(0.06f, 2.5f, 0.95f)), (new Vector3(0f, 1.25f, -0.45f), new Vector3(0.95f, 2.5f, 0.06f)) })
            {
                var w = new GameObject("KioskSolid");
                w.transform.SetParent(root, false);
                w.transform.localPosition = c;
                w.AddComponent<BoxCollider>().size = s;
            }
            // The receiver, then the switch: a trigger filling the kiosk, so looking or reaching in finds it.
            var handset = new GameObject("Receiver").transform;
            handset.SetParent(root, false);
            handset.localPosition = new Vector3(0f, 1.56f, -0.33f);
            var hk = new KitBatch();
            hk.Of(p.Black).AddCylinder(new Vector3(0.1f, 0f, 0f), 0.032f, 0.05f, 10, 1f, Quaternion.Euler(0f, 0f, 90f));
            hk.Of(p.Black).AddCylinder(new Vector3(-0.05f, 0f, 0f), 0.032f, 0.05f, 10, 1f, Quaternion.Euler(0f, 0f, 90f));
            hk.Box(p.Black, new Vector3(0f, 0.03f, 0f), new Vector3(0.2f, 0.025f, 0.035f));
            hk.Bake(handset, "Handset", false);
            var use = new GameObject("PhoneSwitch");
            use.transform.SetParent(root, false);
            use.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var trig = use.AddComponent<BoxCollider>();
            trig.size = new Vector3(0.78f, 2.0f, 0.8f);
            trig.isTrigger = true;
            var phone = use.AddComponent<LondonMewsPhone>();
            phone.Receiver = handset;
            phone.Kiosk = root;
            phone.LaneMin = new Vector3(MX, 0f, LZ0);
            phone.LaneMax = new Vector3(RWX + 0.2f, 0f, LZ1);
            phone.MarkerPoint = handset;
        }

        /// <summary>One glazed kiosk side (or the door) on a face: red kick panel, lit glazing, a three-by-eight red grid.</summary>
        static void GlazedSide(KitBatch k, Pal p, Face f, float d)
        {
            B(k, p.Red, f, 0f, 0.245f, d, 0.73f, 0.25f, 0.04f);
            B(k, p.PhoneGlass, f, 0f, 1.24f, d - 0.005f, 0.73f, 1.74f, 0.012f);
            foreach (float o in new[] { -0.122f, 0.122f }) B(k, p.Red, f, o, 1.24f, d + 0.01f, 0.035f, 1.74f, 0.05f);
            for (int r = 1; r < 8; r++) B(k, p.Red, f, 0f, 0.37f + r * 1.74f / 8f, d + 0.01f, 0.73f, 0.03f, 0.05f);
            B(k, p.Red, f, 0f, 2.13f, d + 0.01f, 0.73f, 0.05f, 0.05f);
        }

        // ------------------------------------------------------------------ dressing, life, light, postcards
        static void Dressing(Transform ex, Pal p)
        {
            Floor(ex, "MewsLane", new Vector3((MX + RWX) * 0.5f, Y0 - 0.15f, (LZ0 + LZ1) * 0.5f), new Vector3(RWX - MX, 0.3f, LZ1 - LZ0), p.Setts, 1f);
            var k = new KitBatch();
            k.Box(p.Granite, new Vector3(-12.8f, Y0, (LZ0 + LZ1) * 0.5f), new Vector3(0.4f, 0.008f, LZ1 - LZ0), 1.2f);
            foreach (float z in new[] { 38f, 48f }) k.Box(p.Iron, new Vector3(-12.8f, Y0 + 0.005f, z), new Vector3(0.36f, 0.008f, 0.5f));
            // A gas lantern on a bracket at the party wall between the second and third houses: the lane's lamp.
            var bracketAt = new Vector3(MX + 0.02f, Y0 + 3.05f, 46.2f);
            k.Box(p.Iron, bracketAt + new Vector3(0.02f, 0f, 0f), new Vector3(0.04f, 0.3f, 0.14f));
            k.Of(p.Iron).AddTube(new List<Vector3> { bracketAt + new Vector3(0.03f, 0.05f, 0f), bracketAt + new Vector3(0.25f, 0.1f, 0f), bracketAt + new Vector3(0.44f, 0.18f, 0f), bracketAt + new Vector3(0.46f, 0.08f, 0f) }, 0.018f, 6);
            Lantern(k, p, bracketAt + new Vector3(0.46f, 0.4f, 0f), 1.15f);
            k.Bake(ex, "MewsDressing");
            var gas = MakeLight(ex, bracketAt + new Vector3(0.5f, 0.3f, 0f), new Color(1f, 0.8f, 0.55f), 1.35f, 7.5f);
            Flicker.Add(gas, 0.05f, 1.4f);
            UrbanKit.Puddles(ex, new Vector3(-12.6f, Y0 + 0.012f, 44f), new Vector2(4.5f, 18f), 6, 1888, p.Puddle); // clear of the gutter strip and gratings
            // Festival bunting across the lane.
            var cord = Mats.Lit(new Color(0.08f, 0.07f, 0.06f), 0.3f);
            var cloth = new[] { Mats.Lit(new Color(0.9f, 0.3f, 0.25f), 0.25f, 0f, "fabric_linen"), Mats.Lit(new Color(1f, 0.92f, 0.7f), 0.25f, 0f, "fabric_linen"), Mats.Lit(new Color(0.35f, 0.5f, 0.9f), 0.25f, 0f, "fabric_linen") };
            UrbanKit.Bunting(ex, new Vector3(RWX - 0.05f, 6.1f, 40.75f), new Vector3(MX + 0.12f, 5.9f, 41.8f), 0.6f, cord, cloth, 14);
            UrbanKit.Bunting(ex, new Vector3(RWX - 0.05f, 6.0f, 49.75f), new Vector3(MX + 0.12f, 6.0f, 50.6f), 0.55f, cord, cloth, 14);
        }

        static void Life(Transform ex)
        {
            AmbientBirds.Create(ex, new Vector3(-12.6f, Y0, 46.6f), 1.6f, 6, AmbientBirds.Kind.Pigeon); // the lane
            AmbientBirds.Create(ex, new Vector3(2.6f, 0f, 27.0f), 1.5f, 5, AmbientBirds.Kind.Gull);     // the promenade by the river
            AmbientBirds.Create(ex, new Vector3(-5.8f, 4.12f, 64.2f), 0.55f, 4, AmbientBirds.Kind.Pigeon); // on the raised walk over the wall
        }

        static void Lamps(Transform ex)
        {
            var c = new Color(1f, 0.8f, 0.52f);
            SwitchLamp.Create(ex, new Vector3(-10.45f, Y0, 34.3f), c, false, 2.6f); // the mews, south-east corner
            SwitchLamp.Create(ex, new Vector3(-10.45f, Y0, 54.2f), c, false, 2.6f); // the mews, by the gate
            SwitchLamp.Create(ex, new Vector3(3.5f, 0f, 34.0f), c, false, 2.6f);    // the promenade on the way to the bridge
            SwitchLamp.Create(ex, new Vector3(3.5f, 0f, 70.0f), c, false, 2.6f);
        }

        static void Postcards(Transform ex)
        {
            Postcard.Create(ex, CountryId.UnitedKingdom, 1, new Vector3(-15.25f, Y0 + 1.1f, 51.4f),
                "The pub in the mews is always just closing. Nobody would look at my passport.");
            Postcard.Create(ex, CountryId.UnitedKingdom, 2, new Vector3(2.9f, 1.1f, 101.4f),
                "Waited three nights on the embankment for midnight. The clock stayed a minute short.");
            Postcard.Create(ex, CountryId.UnitedKingdom, 3, new Vector3(-7.0f, Y0 + 1.1f, OZ0 + 0.32f),
                "Someone scratched eleven lines in here. I added the twelfth. Then the phone rang.");
        }

        // ------------------------------------------------------------------ helpers
        static void WallLantern(KitBatch k, Pal p, Vector3 m, float s)
        {
            k.Box(p.Iron, m + new Vector3(0.02f, 0f, 0f), new Vector3(0.04f, 0.3f, 0.14f));
            k.Of(p.Iron).AddTube(new[] { m + new Vector3(0.03f, 0.05f, 0f), m + new Vector3(0.2f, 0.08f, 0f), m + new Vector3(0.34f, 0.15f, 0f), m + new Vector3(0.36f, 0.06f, 0f) }, 0.018f, 6);
            Lantern(k, p, m + new Vector3(0.36f, 0.36f, 0f), s);
        }

        /// <summary>A square iron lantern: glazed body, corner posts, pyramid cap and finial, a cone below.</summary>
        static void Lantern(KitBatch k, Pal p, Vector3 c, float s)
        {
            var r45 = Quaternion.Euler(0f, 45f, 0f);
            k.Box(p.Iron, c + new Vector3(0f, -0.2f, 0f) * s, new Vector3(0.22f, 0.03f, 0.22f) * s);
            k.Of(p.Iron).AddFrustum(c + new Vector3(0f, -0.3f, 0f) * s, 0.03f * s, 0.12f * s, 0.09f * s, 4, true, true, 1f, r45);
            k.Box(p.Glow, c, new Vector3(0.18f, 0.36f, 0.18f) * s);
            foreach (var cx in new[] { -0.1f, 0.1f })
                foreach (var cz in new[] { -0.1f, 0.1f })
                    k.Box(p.Iron, c + new Vector3(cx, 0f, cz) * s, new Vector3(0.025f, 0.4f, 0.025f) * s);
            k.Box(p.Iron, c + new Vector3(0f, 0.2f, 0f) * s, new Vector3(0.24f, 0.03f, 0.24f) * s);
            k.Of(p.Iron).AddFrustum(c + new Vector3(0f, 0.215f, 0f) * s, 0.19f * s, 0.03f * s, 0.16f * s, 4, true, true, 1f, r45);
            k.Of(p.Iron).AddSphere(c + new Vector3(0f, 0.41f, 0f) * s, 0.025f * s, 6, 4);
        }

        static void Solid(KitBatch k, Transform parent, Material m, Vector3 c, Vector3 s, float uv)
        {
            k.Box(m, c, s, uv);
            Col(parent, "Wall", c, s);
        }

        static GameObject Col(Transform parent, string name, Vector3 c, Vector3 s)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = c;
            go.AddComponent<BoxCollider>().size = s;
            return go;
        }

        /// <summary>A walkable box: a visible slab when a material is given, always a Floor-layer collider with stone steps.</summary>
        static void Floor(Transform parent, string name, Vector3 c, Vector3 s, Material m = null, float uv = 1.2f)
        {
            GameObject go;
            if (m != null) go = MeshFactory.Box(name, parent, c, s, m, true, uv, null, Layers.Floor);
            else
            {
                go = new GameObject(name) { layer = Layers.Floor };
                go.transform.SetParent(parent, false);
                go.transform.localPosition = c;
                go.AddComponent<BoxCollider>().size = s;
            }
            go.AddComponent<Surface>().Kind = "stone";
        }

        static Light MakeLight(Transform parent, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject("PointLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        static TextMeshPro Text(Transform parent, string text, float size, Color color, Vector3 pos, Quaternion rot, float width, bool serif)
        {
            var t = TextFactory.World(parent, text, size, color, TextAlignmentOptions.Center, serif, width);
            t.transform.localPosition = pos;
            t.transform.localRotation = rot;
            return t;
        }
    }

    /// <summary>
    /// The mews' telephone (London). It starts ringing when you come into the lane (and again later, after you've left
    /// and come back). Lift the receiver: the ringing stops, a click, then a faint whisper close to the ear and a caption,
    /// and the line goes dead; the receiver goes back on its cradle by itself. Lift it when it isn't ringing and someone is
    /// breathing on the line. Creepy, never a jump scare; it changes nothing else.
    /// </summary>
    public class LondonMewsPhone : Interactable
    {
        public Transform Receiver, Kiosk;
        public Vector3 LaneMin, LaneMax;

        static readonly string[] Whispers =
        {
            "a whisper on the line: “you're standing where I stood”",
            "a whisper on the line: “don't let it stamp the last page”",
            "a whisper on the line: “it's a minute to midnight here too”",
        };

        enum St { Idle, Ringing, Answered }
        St m_State;
        Cell m_Cell;
        Vector3 m_RestPos;
        Quaternion m_RestRot;
        float m_Timer, m_Cool;
        int m_Rings, m_Calls;
        bool m_Armed = true, m_HasRest;

        const float RingEvery = 2.3f;

        void Start()
        {
            m_Cell = GetComponentInParent<Cell>();
            CaptureRest();
        }

        void CaptureRest()
        {
            if (m_HasRest || Receiver == null) return;
            m_RestPos = Receiver.localPosition;
            m_RestRot = Receiver.localRotation;
            m_HasRest = true;
        }

        bool Live => m_Cell != null && m_Cell.Mode == CellMode.Active;
        Vector3 Ear => Receiver != null ? Receiver.position : transform.position;

        // Hidden mid-call (the player left the cell): the coroutine is gone; put the receiver back and start fresh.
        void OnDisable()
        {
            StopAllCoroutines();
            m_State = St.Idle;
            ResetReceiver();
        }

        public override string GetPrompt(Interactor who) => m_State == St.Ringing ? "Answer the phone" : "Lift the receiver";

        public override bool CanUse(Interactor who) => base.CanUse(who) && m_State != St.Answered && Receiver != null;

        public override void Use(Interactor who)
        {
            if (!CanUse(who)) return;
            base.Use(who);
            bool ringing = m_State == St.Ringing;
            m_State = St.Answered;
            StartCoroutine(Answer(ringing, who));
        }

        void Update()
        {
            if (Receiver == null || Kiosk == null) return;
            if (!Live)
            {
                if (m_State == St.Ringing) { m_State = St.Idle; ResetReceiver(); }
                return;
            }
            if (Game.Paused) return;
            float dt = Time.deltaTime;
            m_Cool -= dt;
            var rig = Game.Rig;
            if (rig == null) return;
            var local = m_Cell.transform.InverseTransformPoint(rig.Feet);
            bool inLane = local.x > LaneMin.x && local.x < LaneMax.x && local.z > LaneMin.z && local.z < LaneMax.z;
            float dist = Vector3.Distance(rig.Feet, Kiosk.position);
            if (!inLane && dist > 14f) m_Armed = true;

            switch (m_State)
            {
                case St.Idle:
                    if (m_Armed && m_Cool <= 0f && inLane && dist < 12f)
                    {
                        m_State = St.Ringing;
                        m_Armed = false;
                        m_Rings = 0;
                        m_Timer = 0.4f;
                    }
                    break;
                case St.Ringing:
                    m_Timer -= dt;
                    if (m_Timer <= 0f)
                    {
                        if (m_Rings >= 6) { m_State = St.Idle; m_Cool = 20f; ResetReceiver(); break; }
                        AudioDirector.I?.Play("phone_buzz", Ear, 0.75f);
                        m_Rings++;
                        m_Timer = RingEvery;
                    }
                    // The receiver rattles on its cradle while the bell buzzes (two bursts at the start of each ring).
                    float since = RingEvery - m_Timer;
                    bool buzz = m_Rings > 0 && since < 1f && (since < 0.42f || (since > 0.55f && since < 0.97f));
                    Receiver.localPosition = m_RestPos + (buzz ? Vector3.up * (0.0025f + 0.0025f * Mathf.Sin(Time.time * 90f)) : Vector3.zero);
                    Receiver.localRotation = m_RestRot * (buzz ? Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 70f) * 2f) : Quaternion.identity);
                    break;
            }
        }

        IEnumerator Answer(bool wasRinging, Interactor who)
        {
            ResetReceiver();
            AudioDirector.Haptic(who != null ? who.Side : HandSide.Desktop, 0.3f, 0.06f);
            // Lift: up off the cradle and turned toward the door, as if held to an ear.
            var liftPos = m_RestPos + new Vector3(0.02f, 0.14f, 0.12f);
            var liftRot = m_RestRot * Quaternion.Euler(-25f, 0f, 35f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
            {
                float k = Mathf.SmoothStep(0f, 1f, t);
                Receiver.localPosition = Vector3.Lerp(m_RestPos, liftPos, k);
                Receiver.localRotation = Quaternion.Slerp(m_RestRot, liftRot, k);
                yield return null;
            }
            Receiver.localPosition = liftPos;
            Receiver.localRotation = liftRot;
            if (Live) AudioDirector.I?.Play("dial_click", Ear, 0.55f, 0.85f);
            yield return new WaitForSeconds(0.8f);
            if (Live)
            {
                if (wasRinging)
                {
                    AudioDirector.I?.Play("pass_whisper", Ear, 0.5f, 0.93f);
                    Hud.I?.ShowCaption(Whispers[m_Calls % Whispers.Length], 4.5f);
                    m_Calls++;
                }
                else
                {
                    AudioDirector.I?.Play("pass_breath", Ear, 0.3f, 1.05f);
                    Hud.I?.ShowCaption("someone is breathing on the line", 3f);
                }
            }
            yield return new WaitForSeconds(wasRinging ? 2.9f : 2.3f);
            if (Live)
            {
                AudioDirector.I?.Play("dial_click", Ear, 0.5f, 0.6f);
                if (wasRinging) Hud.I?.ShowCaption("the line goes dead", 2f);
            }
            yield return new WaitForSeconds(1.2f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
            {
                float k = Mathf.SmoothStep(0f, 1f, t);
                Receiver.localPosition = Vector3.Lerp(liftPos, m_RestPos, k);
                Receiver.localRotation = Quaternion.Slerp(liftRot, m_RestRot, k);
                yield return null;
            }
            ResetReceiver();
            if (Live) AudioDirector.I?.Play("drop_soft", Ear, 0.4f, 1.4f);
            m_State = St.Idle;
            m_Cool = 25f;
        }

        void ResetReceiver()
        {
            if (Receiver == null || !m_HasRest) return; // never moved before its rest pose was taken
            Receiver.localPosition = m_RestPos;
            Receiver.localRotation = m_RestRot;
        }
    }
}

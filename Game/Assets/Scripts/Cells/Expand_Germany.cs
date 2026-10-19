using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Berlin expansion (kept modest: the finale stays in the hall): the Festivalhof. A glass door opens in the pavilion's
    /// north wall between two mullions (x 3.45..5.55, in the half away from the blank wall and the guarded corner) onto a
    /// small festival courtyard on the square: three stalls (pretzels, stamps and seals, pinwheels that spin in the wind), a
    /// beer garden under strings of bulbs, a parasol table by a box hedge, banners rippling off the neighbouring house, and
    /// sparrows working the crumbs. The stamp stall's power is off; throw its switch (<see cref="BerlinBoothPower"/>) and the
    /// sign hums to life. Layout (cell-local metres): the courtyard x 2.6..13.6, z 9..21 (floor y 0); the neighbour block
    /// x 9.25..18.6 (z 3..9) and x 13.6..18.6 (z 9..23). Everything is under the cell's child "Expand_Germany"; the glass
    /// wall, its collider and the hall's north bound are split round the door.
    /// </summary>
    public static class Expand_Germany
    {
        const float DX0 = 3.45f, DX1 = 5.55f, DXC = 4.5f, DH = 2.75f, GZ = 9.1f;  // the door in the glass wall
        const float CX0 = 2.6f, CX1 = 13.6f, CZ0 = 9.0f, CZ1 = 21.0f;              // the courtyard
        const float NX0 = 9.25f, NX1 = 18.6f, NZ0 = 3.0f, NZ1 = 23.0f, NH = 14f;  // the neighbour block
        const float StallZ = 19.25f;                                                // stall counters (fronts face -Z)

        sealed class Pal
        {
            public Material Granite, Concrete, Steel, Black, Oak, Panel, Brass, Red, Navy, Gold, Leaf, Soil, WarmGlow, Bulb;
            public Material Stucco, Trim, Plinth, Glass, Zinc, Pretzel, Ink, Passport, Deck, Hedge;
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

        public static void Build(BerlinCell cell, Transform root)
        {
            var ex = new GameObject("Expand_Germany").transform;
            ex.SetParent(cell.transform, false);
            var p = Palette();
            OpenGlassWall(cell, root, ex, p);
            Doorway(ex, p);
            Ground(ex, p);
            Neighbour(ex, p);
            Stalls(ex, p);
            Garden(ex, p);
            Life(ex);
            Lamps(ex);
            Postcards(ex);
            var shot = new GameObject("ShotPoint").transform;
            shot.SetParent(ex, false);
            shot.localPosition = new Vector3(DXC, 0f, 10.0f);
            shot.localRotation = Quaternion.Euler(0f, 32f, 0f); // from just outside the door across the beer garden to the stalls
        }

        static Pal Palette() => new Pal
        {
            Granite = Mats.Lit(new Color(0.9f, 0.9f, 0.91f), 0.45f, 0f, "granite_pavers"),
            Concrete = Mats.Lit(new Color(0.68f, 0.68f, 0.68f), 0.2f, 0f, "concrete_panels"),
            Steel = Mats.Lit(new Color(0.55f, 0.57f, 0.6f), 0.6f, 0.9f),
            Black = Mats.Lit(new Color(0.03f, 0.03f, 0.035f), 0.5f),
            Oak = Mats.Lit(new Color(0.7f, 0.52f, 0.34f), 0.4f, 0f, "wood_oak_floor"),
            Panel = Mats.Lit(new Color(0.86f, 0.85f, 0.82f), 0.35f),
            Brass = Mats.Lit(new Color(0.78f, 0.6f, 0.32f), 0.65f, 0.9f, "metal_brushed"),
            Red = Mats.Lit(new Color(0.55f, 0.12f, 0.12f), 0.3f, 0f, "fabric_linen"),
            Navy = Mats.Lit(new Color(0.12f, 0.2f, 0.38f), 0.3f, 0f, "fabric_linen"),
            Gold = Mats.Lit(new Color(0.78f, 0.63f, 0.3f), 0.3f, 0f, "fabric_linen"),
            Leaf = Mats.Lit(new Color(0.17f, 0.29f, 0.14f), 0.35f),
            Soil = Mats.Lit(new Color(0.1f, 0.08f, 0.06f), 0.2f),
            WarmGlow = Mats.Emissive(new Color(0.3f, 0.24f, 0.17f), new Color(1.1f, 0.72f, 0.38f)),
            Bulb = Mats.Emissive(new Color(1f, 0.9f, 0.7f), new Color(1.5f, 1.15f, 0.7f)),
            Stucco = Mats.Lit(new Color(0.86f, 0.8f, 0.69f), 0.3f, 0f, "plaster_warm"),
            Trim = Mats.Lit(new Color(0.92f, 0.9f, 0.84f), 0.3f, 0f, "plaster_warm"),
            Plinth = Mats.Lit(new Color(0.62f, 0.6f, 0.58f), 0.3f, 0f, "concrete_panels"),
            Glass = Mats.Lit(new Color(0.06f, 0.07f, 0.09f), 0.93f, 0.2f),
            Zinc = Mats.Lit(new Color(0.78f, 0.82f, 0.88f), 0.5f, 0.6f, "metal_brushed"),
            Pretzel = Mats.Lit(new Color(0.55f, 0.3f, 0.12f), 0.5f),
            Ink = Mats.Lit(new Color(0.45f, 0.06f, 0.06f), 0.5f),
            Passport = Mats.Lit(new Color(0.1f, 0.14f, 0.3f), 0.4f, 0f, "leather_navy"),
            Deck = Mats.Lit(new Color(0.85f, 0.72f, 0.56f), 0.35f, 0f, "wood_oak_floor"),
            Hedge = Mats.Lit(new Color(0.2f, 0.32f, 0.15f), 0.25f, 0f, "grass_short"),
        };

        // ------------------------------------------------------------------ the door
        /// <summary>Splits the glass wall's pane, its collider and the hall's north bound round the door.</summary>
        static void OpenGlassWall(BerlinCell cell, Transform g, Transform ex, Pal p)
        {
            var pane = g != null ? g.Find("GlassWall") : null;
            if (pane != null)
            {
                var r = pane.GetComponent<MeshRenderer>();
                var mf = pane.GetComponent<MeshFilter>();
                var mat = r != null ? r.sharedMaterial : Mats.Glass(new Color(0.75f, 0.82f, 0.88f, 0.12f));
                var size = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.size : new Vector3(18.4f, 6f, 0.04f);
                var c = pane.localPosition;
                float x0 = c.x - size.x * 0.5f, x1 = c.x + size.x * 0.5f, y1 = c.y + size.y * 0.5f, y0 = c.y - size.y * 0.5f;
                if (x0 < DX0 && x1 > DX1)
                {
                    Object.DestroyImmediate(pane.gameObject);
                    foreach (var (a, b, lo) in new[] { (x0, DX0, y0), (DX1, x1, y0), (DX0, DX1, DH) })
                    {
                        var go = MeshFactory.Box("GlassWall", ex, new Vector3((a + b) * 0.5f, (lo + y1) * 0.5f, c.z), new Vector3(b - a, y1 - lo, size.z), mat, false);
                        go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                }
            }
            var gwc = g != null ? g.Find("GlassWallCollider") : null;
            var bc = gwc != null ? gwc.GetComponent<BoxCollider>() : null;
            if (bc != null) SplitWall(gwc, bc, true);
            var bounds = cell.transform.Find("Bounds");
            if (bounds == null) return;
            Transform north = null;
            foreach (Transform b in bounds)
                if (b.GetComponent<BoxCollider>() != null && (north == null || b.localPosition.z > north.localPosition.z)) north = b;
            if (north != null) SplitWall(north, north.GetComponent<BoxCollider>(), false);
        }

        /// <summary>Cuts a wall collider running along x at the door: keeps the west part, adds the east part (and a transom).</summary>
        static void SplitWall(Transform t, BoxCollider c, bool transom)
        {
            float x0 = t.localPosition.x + c.center.x - c.size.x * 0.5f, x1 = t.localPosition.x + c.center.x + c.size.x * 0.5f;
            if (x0 >= DX0 || x1 <= DX1) return;
            float y0 = t.localPosition.y + c.center.y - c.size.y * 0.5f, y1 = t.localPosition.y + c.center.y + c.size.y * 0.5f;
            var lp = t.localPosition;
            t.localPosition = new Vector3((x0 + DX0) * 0.5f - c.center.x, lp.y, lp.z);
            c.size = new Vector3(DX0 - x0, c.size.y, c.size.z);
            void Add(float a, float b, float lo, float hi)
            {
                var go = new GameObject(t.name);
                go.transform.SetParent(t.parent, false);
                go.transform.localPosition = new Vector3((a + b) * 0.5f, (lo + hi) * 0.5f, lp.z + c.center.z);
                go.AddComponent<BoxCollider>().size = new Vector3(b - a, hi - lo, c.size.z);
            }
            Add(DX1, x1, y0, y1);
            if (transom) Add(DX0, DX1, DH, y1);
        }

        /// <summary>A steel door frame, two glass leaves slid open, a mat, and a sign hung from the truss each side.</summary>
        static void Doorway(Transform ex, Pal p)
        {
            var k = new KitBatch();
            foreach (float x in new[] { DX0 - 0.05f, DX1 + 0.05f }) k.Box(p.Steel, new Vector3(x, DH * 0.5f, GZ), new Vector3(0.1f, DH, 0.16f));
            k.Box(p.Steel, new Vector3(DXC, DH + 0.06f, GZ), new Vector3(DX1 - DX0 + 0.2f, 0.12f, 0.16f));
            var leaf = Mats.Glass(new Color(0.75f, 0.82f, 0.88f, 0.16f));
            foreach (float x in new[] { DX0 - 0.55f, DX1 + 0.55f })
            {
                var go = MeshFactory.Box("SlidingLeaf", ex, new Vector3(x, 1.32f, GZ - 0.14f), new Vector3(1.0f, 2.6f, 0.03f), leaf, false);
                go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                foreach (float e in new[] { -0.49f, 0.49f }) k.Box(p.Steel, new Vector3(x + e, 1.32f, GZ - 0.14f), new Vector3(0.03f, 2.6f, 0.05f));
                k.Box(p.Steel, new Vector3(x, 0.03f, GZ - 0.14f), new Vector3(1.0f, 0.06f, 0.05f));
                k.Box(p.Steel, new Vector3(x, 2.61f, GZ - 0.14f), new Vector3(1.0f, 0.04f, 0.05f));
            }
            k.Box(p.Black, new Vector3(DXC, 0.003f, GZ), new Vector3(DX1 - DX0, 0.006f, 1.2f));
            var sign = new Vector3(DXC, 3.3f, GZ - 0.25f);
            k.Box(p.Black, sign, new Vector3(1.9f, 0.42f, 0.06f));
            k.Box(p.Gold, sign + new Vector3(0f, -0.2f, 0f), new Vector3(1.9f, 0.025f, 0.065f));
            foreach (float sx in new[] { -0.8f, 0.8f }) k.Box(p.Steel, sign + new Vector3(sx, 1.25f, 0f), new Vector3(0.015f, 2.1f, 0.015f));
            k.Bake(ex, "HofDoor");
            Text(ex, "FESTIVALHOF\n<size=50%><color=#E8E6DE>Hof · Courtyard</color></size>", 0.1f, new Color(0.95f, 0.76f, 0.2f), sign + new Vector3(0f, 0.01f, -0.035f), Quaternion.identity, 1.8f);
            Text(ex, "PAVILLON\n<size=50%><color=#E8E6DE>Halle · Hall</color></size>", 0.1f, new Color(0.95f, 0.76f, 0.2f), sign + new Vector3(0f, 0.01f, 0.035f), Quaternion.Euler(0f, 180f, 0f), 1.8f);
        }

        // ------------------------------------------------------------------ the courtyard floor and its edges
        static void Ground(Transform ex, Pal p)
        {
            MeshFactory.Floor("HofFloor", ex, new Vector3((CX0 + CX1) * 0.5f, 0f, (CZ0 + CZ1) * 0.5f), new Vector2(CX1 - CX0, CZ1 - CZ0), p.Granite, "stone", 0.2f, 1.8f);
            var k = new KitBatch();
            k.Box(p.Deck, new Vector3(9.2f, 0.004f, 14.4f), new Vector3(6.4f, 0.008f, 6.2f), 1.5f);
            // A box hedge in concrete troughs along the west edge (the square's axis stays open beyond it).
            k.Box(p.Concrete, new Vector3(CX0 + 0.3f, 0.28f, (9.6f + CZ1) * 0.5f), new Vector3(0.6f, 0.56f, CZ1 - 9.6f), 2f);
            k.Box(p.Soil, new Vector3(CX0 + 0.3f, 0.55f, (9.6f + CZ1) * 0.5f), new Vector3(0.52f, 0.02f, CZ1 - 9.7f));
            for (float z = 9.9f; z < CZ1 - 0.2f; z += 0.9f) k.Box(p.Hedge, new Vector3(CX0 + 0.3f, 0.8f, z + 0.42f), new Vector3(0.5f, 0.5f, 0.88f), 1f);
            Col(ex, "Hedge", new Vector3(CX0 + 0.3f, 0.55f, (9.6f + CZ1) * 0.5f), new Vector3(0.6f, 1.1f, CZ1 - 9.6f));
            k.Bake(ex, "HofGround");
            // Invisible walls round the open sides (tall, so a teleport arc cannot clear them either).
            Col(ex, "HofBound", new Vector3(CX0 - 0.15f, 3f, (CZ0 + CZ1) * 0.5f), new Vector3(0.3f, 6f, CZ1 - CZ0 + 0.4f));
            Col(ex, "HofBound", new Vector3((CX0 + CX1) * 0.5f, 3f, CZ1 + 0.15f), new Vector3(CX1 - CX0 + 0.6f, 6f, 0.3f));
            // The square's lamp and flagpole that now stand in the courtyard get colliders.
            Col(ex, "SquareLamp", new Vector3(7f, 2.15f, 14f), new Vector3(0.16f, 4.3f, 0.16f));
            Col(ex, "SquareFlag", new Vector3(10f, 1.45f, 16f), new Vector3(0.5f, 3f, 0.5f));
        }

        // ------------------------------------------------------------------ the neighbouring house
        /// <summary>A pale stucco house closing the courtyard's east and south-east: plinth, three storeys of tall windows
        /// with surrounds, a cornice and a zinc roof edge; a back door; two banner arms with cloth banners.</summary>
        static void Neighbour(Transform ex, Pal p)
        {
            var k = new KitBatch();
            Solid(k, ex, p.Stucco, new Vector3((NX0 + NX1) * 0.5f, NH * 0.5f, (NZ0 + CZ0) * 0.5f), new Vector3(NX1 - NX0, NH, CZ0 - NZ0), 2.5f);
            Solid(k, ex, p.Stucco, new Vector3((CX1 + NX1) * 0.5f, NH * 0.5f, (CZ0 + NZ1) * 0.5f), new Vector3(NX1 - CX1, NH, NZ1 - CZ0), 2.5f);
            var west = new Face(new Vector3(CX1, 0f, CZ0), Vector3.left);   // u = z - 9
            var north = new Face(new Vector3(NX0, 0f, CZ0), Vector3.forward); // u = x - 9.25
            Facade(k, p, west, 0f, NZ1 - CZ0, new[] { 1.6f, 4.6f, 7.6f, 10.6f, 13.0f });
            Facade(k, p, north, 0f, CX1 - NX0, new[] { 2.2f });
            // Ground floor of the courtyard side: a back door, two barred windows.
            B(k, p.Oak, west, 3.0f, 1.15f, 0.03f, 1.1f, 2.3f, 0.05f, 1.2f);
            B(k, p.Trim, west, 3.0f, 2.4f, 0.04f, 1.4f, 0.16f, 0.08f, 2f);
            k.Of(p.Brass).AddSphere(west.P(3.38f, 1.1f, 0.07f), 0.03f, 6, 4);
            foreach (float u in new[] { 6.4f, 9.6f })
            {
                B(k, p.Glass, west, u, 1.9f, 0.01f, 1.0f, 1.4f, 0.02f);
                B(k, p.Trim, west, u, 2.68f, 0.04f, 1.2f, 0.14f, 0.08f, 2f);
                B(k, p.Trim, west, u, 1.15f, 0.06f, 1.2f, 0.08f, 0.12f, 2f);
                for (int i = 0; i < 5; i++) B(k, p.Steel, west, u - 0.4f + i * 0.2f, 1.9f, 0.05f, 0.014f, 1.4f, 0.014f);
            }
            B(k, p.Glass, north, 2.2f, 1.9f, 0.01f, 1.0f, 1.4f, 0.02f);
            B(k, p.Trim, north, 2.2f, 2.68f, 0.04f, 1.2f, 0.14f, 0.08f, 2f);
            // Roof edges (zinc slopes behind the cornice).
            k.Of(p.Zinc).AddExtrudedXY(new List<Vector2> { new Vector2(CX1 - 0.1f, NH), new Vector2(NX1 + 0.1f, NH), new Vector2(NX1 + 0.1f, NH + 0.4f),
                new Vector2(16.6f, NH + 2.2f), new Vector2(15.6f, NH + 2.2f), new Vector2(CX1 - 0.1f, NH + 0.4f) }, NZ1 - NZ0, new Vector3(0f, 0f, (NZ0 + NZ1) * 0.5f), Quaternion.identity, 2f);
            k.Of(p.Zinc).AddExtrudedXY(new List<Vector2> { new Vector2(-CZ0 - 0.1f, NH), new Vector2(-NZ0 + 0.1f, NH), new Vector2(-NZ0 + 0.1f, NH + 0.4f),
                new Vector2(-5.2f, NH + 2.2f), new Vector2(-6.6f, NH + 2.2f), new Vector2(-CZ0 - 0.1f, NH + 0.4f) }, CX1 - NX0, new Vector3((NX0 + CX1) * 0.5f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), 2f);
            // Banner arms on the courtyard face.
            foreach (float u in new[] { 3.0f, 9.0f }) k.Of(p.Steel).AddCylinder(west.P(u, 6.6f, 0f), 0.025f, 1.05f, 6, 1f, west.Q * Quaternion.Euler(90f, 0f, 0f));
            k.Bake(ex, "HofNeighbour");
            var cloths = new[] { p.Red, p.Navy };
            int ci = 0;
            foreach (float u in new[] { 3.0f, 9.0f })
            {
                var banner = ClothFlag.Create(ex, "HofBanner", west.P(u, 6.57f, 0.08f), 2.4f, 0.92f, cloths[ci++ % 2], 0f, 0.09f);
                banner.transform.localRotation = Quaternion.LookRotation(Vector3.Cross(west.N, Vector3.down), -west.N);
            }
        }

        /// <summary>Plinth, three storeys of windows (sills at 4.8, 8.0, 11.2) with surrounds, string courses, cornice.</summary>
        static void Facade(KitBatch k, Pal p, Face f, float u0, float u1, float[] cols)
        {
            float w = u1 - u0, uc = (u0 + u1) * 0.5f;
            B(k, p.Plinth, f, uc, 0.3f, 0.02f, w, 0.6f, 0.04f, 2f);
            B(k, p.Trim, f, uc, 4.2f, 0.04f, w, 0.14f, 0.08f, 2f);
            for (int fl = 0; fl < 3; fl++)
            {
                float sill = 4.8f + fl * 3.2f;
                B(k, p.Trim, f, uc, sill - 0.08f, 0.03f, w, 0.06f, 0.06f, 2f);
                foreach (float u in cols)
                {
                    B(k, p.Glass, f, u, sill + 1.0f, 0.01f, 1.1f, 2.0f, 0.02f);
                    B(k, p.Panel, f, u, sill + 1.0f, 0.025f, 0.05f, 2.0f, 0.02f);
                    B(k, p.Panel, f, u, sill + 1.45f, 0.025f, 1.1f, 0.05f, 0.02f);
                    B(k, p.Trim, f, u, sill + 2.12f, 0.04f, 1.4f, 0.2f, 0.08f, 2f);
                    B(k, p.Trim, f, u, sill - 0.03f, 0.06f, 1.3f, 0.08f, 0.12f, 2f);
                    B(k, p.Trim, f, u - 0.62f, sill + 1.0f, 0.03f, 0.12f, 2.0f, 0.06f, 2f);
                    B(k, p.Trim, f, u + 0.62f, sill + 1.0f, 0.03f, 0.12f, 2.0f, 0.06f, 2f);
                    if (fl == 0) B(k, p.Trim, f, u, sill + 2.42f, 0.06f, 1.5f, 0.12f, 0.12f, 2f);
                }
            }
            B(k, p.Trim, f, uc, NH - 0.3f, 0.18f, w + 0.2f, 0.4f, 0.36f, 2f);
            B(k, p.Trim, f, uc, NH - 0.6f, 0.06f, w, 0.12f, 0.12f, 2f);
        }

        // ------------------------------------------------------------------ the stalls
        /// <summary>
        /// Three timber stalls along the north edge with fence panels between: pretzels (striped awning), stamps and seals
        /// (the power switch, a lightbox sign, bulbs, a desk fan), and pinwheels spinning on their sticks.
        /// </summary>
        static void Stalls(Transform ex, Pal p)
        {
            var k = new KitBatch();
            Stall(k, ex, p, 3.4f, 6.0f, p.Red);
            Stall(k, ex, p, 6.8f, 9.4f, p.Navy);
            Stall(k, ex, p, 10.2f, 12.8f, p.Gold);
            foreach (var (a, b) in new[] { (CX0 + 0.6f, 3.4f), (6.0f, 6.8f), (9.4f, 10.2f), (12.8f, CX1) })
            {
                float xc = (a + b) * 0.5f, w = b - a;
                foreach (float x in new[] { a + 0.03f, b - 0.03f }) k.Box(p.Steel, new Vector3(x, 1.0f, CZ1 - 0.05f), new Vector3(0.04f, 2.0f, 0.04f));
                k.Box(p.Red, new Vector3(xc, 1.15f, CZ1 - 0.05f), new Vector3(w - 0.06f, 1.4f, 0.015f));
                k.Box(p.Gold, new Vector3(xc, 1.82f, CZ1 - 0.06f), new Vector3(w - 0.06f, 0.06f, 0.016f));
                k.Box(p.Concrete, new Vector3(xc, 0.06f, CZ1 - 0.05f), new Vector3(w, 0.12f, 0.3f));
                Col(ex, "FencePanel", new Vector3(xc, 1.0f, CZ1 - 0.05f), new Vector3(w, 2.0f, 0.2f));
            }
            // 1. Pretzels: a striped awning, pretzels on a rod and in a basket.
            var stripes = Stripes(new Color(0.72f, 0.14f, 0.12f), new Color(0.94f, 0.9f, 0.8f), 1f / 0.5f);
            k.Box(stripes, new Vector3(4.7f, 2.35f, StallZ - 0.45f), new Vector3(2.7f, 0.03f, 1.0f), Quaternion.Euler(-14f, 0f, 0f)); // sloping down off the stall
            k.Box(p.Steel, new Vector3(4.7f, 1.75f, StallZ + 0.2f), new Vector3(2.3f, 0.02f, 0.02f));
            for (int i = 0; i < 7; i++)
            {
                var c = new Vector3(3.75f + i * 0.32f, 1.62f, StallZ + 0.2f);
                k.Of(p.Pretzel).AddTorus(c, 0.075f, 0.025f, 12, 5, Quaternion.Euler(90f, 0f, 0f));
                k.Box(p.Pretzel, c + new Vector3(0f, -0.02f, 0f), new Vector3(0.1f, 0.025f, 0.03f), Quaternion.Euler(0f, 0f, 35f));
            }
            k.Of(p.Oak).AddFrustum(new Vector3(4.2f, 1.05f, StallZ + 0.1f), 0.2f, 0.26f, 0.12f, 12);
            for (int i = 0; i < 4; i++) k.Of(p.Pretzel).AddTorus(new Vector3(4.12f + (i % 2) * 0.16f, 1.19f + (i / 2) * 0.04f, StallZ + 0.06f + (i % 2) * 0.08f), 0.07f, 0.024f, 12, 5, Quaternion.Euler(10f * i, 30f * i, 0f));
            SignText(ex, 4.7f, "BREZELN", new Color(0.95f, 0.88f, 0.7f));
            // 3. Pinwheels: a block on the counter with sticks; the wheels spin.
            k.Box(p.Oak, new Vector3(11.5f, 1.2f, StallZ + 0.15f), new Vector3(1.6f, 0.3f, 0.25f));
            SignText(ex, 11.5f, "WINDRÄDER", new Color(0.18f, 0.16f, 0.12f));
            var blades = new[] { p.Red, p.Navy, p.Gold, Mats.Lit(new Color(0.2f, 0.5f, 0.3f), 0.3f, 0f, "fabric_linen") };
            for (int i = 0; i < 8; i++)
            {
                var top = new Vector3(10.85f + (i % 4) * 0.42f + (i / 4) * 0.2f, 1.75f + (i / 4) * 0.32f + (i % 2) * 0.06f, StallZ + 0.1f - (i / 4) * 0.06f);
                k.Box(p.Oak, new Vector3(top.x, (1.35f + top.y) * 0.5f, top.z + 0.02f), new Vector3(0.012f, top.y - 1.35f, 0.012f));
                var hub = new GameObject("Pinwheel").transform;
                hub.SetParent(ex, false);
                hub.localPosition = top;
                var b = new MeshBuilder();
                for (int j = 0; j < 4; j++)
                {
                    var q = Quaternion.Euler(0f, 0f, j * 90f) * Quaternion.Euler(28f, 0f, 0f);
                    b.AddBox(Quaternion.Euler(0f, 0f, j * 90f) * new Vector3(0.06f, 0f, 0f), new Vector3(0.12f, 0.055f, 0.004f), q);
                }
                b.AddSphere(Vector3.zero, 0.012f, 6, 4);
                var go = MeshFactory.FromBuilder("Blades", b, blades[i % blades.Length], hub);
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Rotor.Add(hub, (i % 2 == 0 ? 1f : -1f) * (140f + i * 23f), Vector3.forward);
            }
            k.Bake(ex, "HofStalls");
            StampStall(ex, p);
        }

        /// <summary>A stall frame: back, sides, counter, front posts, a roof sloping to the back, a sign board on top.</summary>
        static void Stall(KitBatch k, Transform ex, Pal p, float x0, float x1, Material trim)
        {
            float xc = (x0 + x1) * 0.5f, w = x1 - x0, zb = CZ1 - 0.12f;
            k.Box(p.Oak, new Vector3(xc, 1.2f, zb), new Vector3(w, 2.4f, 0.06f), 1.2f);
            foreach (float x in new[] { x0 + 0.03f, x1 - 0.03f }) k.Box(p.Oak, new Vector3(x, 1.2f, (StallZ + zb) * 0.5f), new Vector3(0.06f, 2.4f, zb - StallZ), 1.2f);
            k.Box(p.Oak, new Vector3(xc, 0.5f, StallZ), new Vector3(w, 1.0f, 0.06f), 1.2f);
            k.Box(trim, new Vector3(xc, 0.75f, StallZ - 0.035f), new Vector3(w, 0.14f, 0.01f));
            k.Box(p.Oak, new Vector3(xc, 1.03f, StallZ + 0.12f), new Vector3(w + 0.06f, 0.05f, 0.42f), 1.2f);
            foreach (float x in new[] { x0 + 0.05f, x1 - 0.05f }) k.Box(p.Oak, new Vector3(x, 1.3f, StallZ), new Vector3(0.1f, 2.6f, 0.1f), 1.2f);
            k.Box(p.Oak, new Vector3(xc, 2.62f, (StallZ + CZ1) * 0.5f), new Vector3(w + 0.2f, 0.05f, CZ1 - StallZ + 0.4f), Quaternion.Euler(7f, 0f, 0f), 1.2f); // high at the front
            k.Box(trim, new Vector3(xc, 2.95f, StallZ - 0.12f), new Vector3(w - 0.2f, 0.46f, 0.05f));
            Col(ex, "Stall", new Vector3(xc, 1.3f, (StallZ - 0.1f + CZ1) * 0.5f), new Vector3(w, 2.6f, CZ1 - StallZ + 0.1f));
        }

        static void SignText(Transform ex, float x, string text, Color c)
            => Text(ex, text, 0.16f, c, new Vector3(x, 2.95f, StallZ - 0.15f), Quaternion.identity, 2.2f);

        /// <summary>The stamp stall: stamps, ink pads and souvenir passports on the counter, a lightbox sign, bulbs along the
        /// front, a desk fan, and the fuse box with its lever on the front post. All dark until the lever is thrown.</summary>
        static void StampStall(Transform ex, Pal p)
        {
            const float xc = 8.1f;
            var k = new KitBatch();
            for (int i = 0; i < 6; i++)
            {
                var c = new Vector3(7.2f + i * 0.17f, 1.055f, StallZ + 0.08f);
                k.Of(p.Black).AddCylinder(c, 0.032f, 0.03f, 10);
                k.Of(p.Oak).AddCylinder(c + Vector3.up * 0.03f, 0.014f, 0.07f, 8);
                k.Of(p.Oak).AddSphere(c + Vector3.up * 0.115f, 0.025f, 8, 6);
            }
            foreach (float x in new[] { 8.35f, 8.6f }) k.Box(p.Ink, new Vector3(x, 1.065f, StallZ + 0.1f), new Vector3(0.16f, 0.02f, 0.11f));
            for (int i = 0; i < 5; i++) k.Box(p.Passport, new Vector3(8.75f + i * 0.08f, 1.12f, StallZ + 0.24f), new Vector3(0.012f, 0.125f, 0.09f), Quaternion.Euler(0f, 0f, -6f + i * 3f));
            // The lightbox sign and its bulbs (own materials: they light up), the fan, the fuse box.
            var signMat = new Material(Mats.Emissive(new Color(0.84f, 0.83f, 0.8f), Color.black)) { name = "BoothSign" };
            var bulbMat = new Material(Mats.Emissive(new Color(0.55f, 0.52f, 0.46f), Color.black)) { name = "BoothBulbs" };
            var sk = new KitBatch();
            sk.Box(p.Black, new Vector3(xc, 2.95f, StallZ - 0.2f), new Vector3(2.0f, 0.5f, 0.14f));
            sk.Box(signMat, new Vector3(xc, 2.95f, StallZ - 0.272f), new Vector3(1.88f, 0.4f, 0.01f));
            for (int i = 0; i < 9; i++) sk.Of(bulbMat).AddSphere(new Vector3(6.95f + i * 0.29f, 2.48f, StallZ - 0.12f), 0.035f, 8, 6);
            sk.Bake(ex, "BoothLights", false);
            Text(ex, "STEMPEL · SIEGEL", 0.13f, new Color(0.5f, 0.07f, 0.06f), new Vector3(xc, 2.95f, StallZ - 0.28f), Quaternion.identity, 1.85f);
            k.Box(p.Black, new Vector3(xc, 2.53f, StallZ - 0.12f), new Vector3(2.5f, 0.008f, 0.008f));
            var fanAt = new Vector3(9.05f, 1.055f, StallZ + 0.28f);
            k.Of(p.Black).AddCylinder(fanAt, 0.07f, 0.02f, 12);
            k.Of(p.Steel).AddCylinder(fanAt, 0.012f, 0.22f, 6);
            k.Of(p.Steel).AddTorus(fanAt + Vector3.up * 0.3f, 0.11f, 0.006f, 16, 3, Quaternion.Euler(90f, 0f, 0f));
            var hub = new GameObject("BoothFan").transform;
            hub.SetParent(ex, false);
            hub.localPosition = fanAt + new Vector3(0f, 0.3f, -0.01f);
            hub.localRotation = Quaternion.identity;
            var fb = new MeshBuilder();
            for (int j = 0; j < 4; j++) fb.AddBox(Quaternion.Euler(0f, 0f, j * 90f) * new Vector3(0.05f, 0f, 0f), new Vector3(0.09f, 0.035f, 0.004f), Quaternion.Euler(0f, 0f, j * 90f) * Quaternion.Euler(20f, 0f, 0f));
            fb.AddSphere(Vector3.zero, 0.018f, 6, 4);
            MeshFactory.FromBuilder("Blades", fb, p.Steel, hub);
            // The fuse box on the right front post, its knife-switch lever pointing up (off).
            var box = new Vector3(9.3f, 1.35f, StallZ - 0.12f);
            k.Box(p.Concrete, box, new Vector3(0.28f, 0.38f, 0.12f));
            k.Box(p.Black, box + new Vector3(0f, 0.12f, -0.065f), new Vector3(0.2f, 0.06f, 0.01f));
            k.Box(Mats.Lit(new Color(0.85f, 0.7f, 0.1f), 0.4f), box + new Vector3(0f, 0.12f, -0.071f), new Vector3(0.14f, 0.035f, 0.004f));
            k.Bake(ex, "HofStampStall");
            var lever = new GameObject("PowerLever").transform;
            lever.SetParent(ex, false);
            lever.localPosition = box + new Vector3(0f, -0.04f, -0.07f);
            var lk = new KitBatch();
            lk.Of(p.Steel).AddCylinder(new Vector3(-0.05f, 0f, 0f), 0.018f, 0.1f, 8, 1f, Quaternion.Euler(0f, 0f, -90f));
            lk.Box(p.Steel, new Vector3(0f, 0.1f, 0f), new Vector3(0.025f, 0.2f, 0.02f));
            lk.Of(Mats.Lit(new Color(0.65f, 0.06f, 0.05f), 0.5f)).AddCylinder(new Vector3(-0.05f, 0.2f, 0f), 0.022f, 0.1f, 8, 1f, Quaternion.Euler(0f, 0f, -90f));
            lk.Bake(lever, "Lever", false);
            var light = new GameObject("SignLight");
            light.transform.SetParent(ex, false);
            light.transform.localPosition = new Vector3(xc, 2.7f, StallZ - 0.9f);
            var l = light.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.86f, 0.62f);
            l.intensity = 0f;
            l.range = 4f;
            l.shadows = LightShadows.None;
            l.enabled = false; // the cell caches lights as built: off until the switch is thrown
            var use = new GameObject("PowerSwitch");
            use.transform.SetParent(ex, false);
            use.transform.localPosition = box + new Vector3(0f, 0.02f, -0.12f);
            var sc = use.AddComponent<SphereCollider>();
            sc.radius = 0.3f;
            sc.isTrigger = true;
            var power = use.AddComponent<BerlinBoothPower>();
            power.Lever = lever;
            power.SignMat = signMat;
            power.BulbMat = bulbMat;
            power.SignLight = l;
            power.Fan = hub;
            power.MarkerPoint = lever;
        }

        // ------------------------------------------------------------------ the beer garden
        /// <summary>Two long tables with benches, a parasol table by the hedge, strings of bulbs from poles in the hedge to the
        /// neighbour's wall, crates stacked in the corner.</summary>
        static void Garden(Transform ex, Pal p)
        {
            var k = new KitBatch();
            foreach (float z in new[] { 13.0f, 15.8f })
            {
                var c = new Vector3(8.4f, 0f, z);
                k.Box(p.Oak, c + new Vector3(0f, 0.75f, 0f), new Vector3(2.2f, 0.04f, 0.55f), 1.2f);
                foreach (float s in new[] { -0.55f, 0.55f }) k.Box(p.Oak, c + new Vector3(0f, 0.45f, s), new Vector3(2.2f, 0.035f, 0.26f), 1.2f);
                foreach (float x in new[] { -0.85f, 0.85f })
                {
                    k.Box(p.Steel, c + new Vector3(x, 0.37f, 0f), new Vector3(0.03f, 0.74f, 0.45f), Quaternion.Euler(0f, 0f, 0f));
                    foreach (float s in new[] { -0.55f, 0.55f }) k.Box(p.Steel, c + new Vector3(x, 0.22f, s), new Vector3(0.03f, 0.44f, 0.22f));
                }
                Col(ex, "BeerTable", c + new Vector3(0f, 0.4f, 0f), new Vector3(2.2f, 0.8f, 1.36f));
            }
            k.Box(p.Panel, new Vector3(8.1f, 0.785f, 13.05f), new Vector3(0.14f, 0.02f, 0.1f));
            k.Of(p.Gold).AddFrustum(new Vector3(8.9f, 0.77f, 15.75f), 0.035f, 0.045f, 0.16f, 10);
            k.Of(p.Pretzel).AddTorus(new Vector3(7.6f, 0.785f, 15.9f), 0.07f, 0.024f, 12, 5, Quaternion.identity);
            // Parasol table by the hedge, its canopy striped.
            var t = new Vector3(5.3f, 0f, 12.9f);
            k.Of(p.Steel).AddCylinder(t, 0.25f, 0.03f, 14);
            k.Of(p.Steel).AddCylinder(t, 0.03f, 0.74f, 8);
            k.Of(p.Oak).AddCylinder(t + Vector3.up * 0.74f, 0.45f, 0.03f, 18);
            k.Of(p.Steel).AddCylinder(t + Vector3.up * 0.77f, 0.022f, 1.6f, 8);
            var canopy = Stripes(new Color(0.12f, 0.2f, 0.38f), new Color(0.92f, 0.9f, 0.84f), 1f);
            k.Of(canopy).AddFrustum(t + Vector3.up * 2.12f, 1.35f, 0.05f, 0.5f, 16, true, false, 0.42f);
            foreach (float a in new[] { 0f, 120f, 240f })
            {
                var d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                k.Box(p.Steel, t + d * 0.75f + Vector3.up * 0.45f, new Vector3(0.36f, 0.03f, 0.36f), Quaternion.Euler(0f, a, 0f));
                k.Box(p.Steel, t + d * 0.75f + Vector3.up * 0.65f + d * 0.16f, new Vector3(0.36f, 0.4f, 0.03f), Quaternion.Euler(0f, a, 0f));
            }
            var col = new GameObject("ParasolTable");
            col.transform.SetParent(ex, false);
            col.transform.localPosition = t + Vector3.up * 0.4f;
            var cap = col.AddComponent<CapsuleCollider>();
            cap.radius = 0.95f; cap.height = 0.8f;
            // Bulb strings from poles in the hedge to the neighbour's wall.
            foreach (float z in new[] { 11.6f, 15.1f, 18.4f })
            {
                var a = new Vector3(CX0 + 0.3f, 3.8f, z);
                var b = new Vector3(CX1 - 0.05f, 4.4f, z + 0.3f);
                k.Of(p.Steel).AddCylinder(new Vector3(a.x, 0.56f, z), 0.04f, 3.3f, 8);
                k.Of(p.Steel).AddSphere(a + Vector3.up * 0.05f, 0.05f, 6, 4);
                var pts = new List<Vector3>();
                for (int i = 0; i <= 16; i++) { float s = i / 16f; pts.Add(Vector3.Lerp(a, b, s) + Vector3.down * 0.75f * 4f * s * (1f - s)); }
                k.Of(p.Black).AddTube(pts, 0.006f, 4);
                int n = Mathf.RoundToInt((b - a).magnitude / 0.5f);
                for (int i = 1; i < n; i++)
                {
                    float s = i / (float)n;
                    k.Of(p.Bulb).AddSphere(Vector3.Lerp(a, b, s) + Vector3.down * (0.75f * 4f * s * (1f - s) + 0.05f), 0.035f, 6, 4);
                }
            }
            // Crates stacked in the far corner.
            for (int i = 0; i < 4; i++)
            {
                var c = new Vector3(13.0f - (i % 2) * 0.45f, 0.17f + (i / 2) * 0.34f, 9.6f + (i % 3 == 2 ? 0.05f : 0f));
                k.Box(p.Red, c, new Vector3(0.42f, 0.32f, 0.3f));
            }
            Col(ex, "Crates", new Vector3(12.78f, 0.35f, 9.6f), new Vector3(0.9f, 0.7f, 0.4f));
            k.Bake(ex, "HofGarden");
        }

        // ------------------------------------------------------------------ life, light, postcards
        static void Life(Transform ex)
        {
            AmbientBirds.Create(ex, new Vector3(8.6f, 0f, 14.4f), 1.6f, 8, AmbientBirds.Kind.Sparrow); // under the beer tables
            AmbientBirds.Create(ex, new Vector3(4.7f, 0f, 17.9f), 0.9f, 5, AmbientBirds.Kind.Sparrow); // crumbs at the pretzel stall
        }

        static void Lamps(Transform ex)
        {
            var c = new Color(1f, 0.82f, 0.56f);
            SwitchLamp.Create(ex, new Vector3(3.6f, 0f, 17.4f), c, false, 2.6f);  // by the hedge, toward the stalls
            SwitchLamp.Create(ex, new Vector3(12.95f, 0f, 11.2f), c, false, 2.6f); // the corner against the neighbour
        }

        static void Postcards(Transform ex)
        {
            Postcard.Create(ex, CountryId.Germany, 1, new Vector3(5.75f, 1.1f, 12.3f),
                "Bought a pretzel in the courtyard. The vendor stamped my passport for free.");
            Postcard.Create(ex, CountryId.Germany, 2, new Vector3(6.2f, 1.1f, -5.6f),
                "Home is boarding at gate twelve, says the board. Nobody I asked ever came back.");
            Postcard.Create(ex, CountryId.Germany, 3, new Vector3(13.1f, 1.1f, 20.3f),
                "Eleven stamps, all real. I hid the twelfth page here, so it can't find me.");
        }

        // ------------------------------------------------------------------ helpers
        static Material Stripes(Color a, Color b, float tileU)
        {
            var tex = new Texture2D(64, 4, TextureFormat.RGBA32, true) { name = "Stripes", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color32[64 * 4];
            for (int y = 0; y < 4; y++) for (int x = 0; x < 64; x++) px[y * 64 + x] = x < 32 ? (Color32)a : (Color32)b;
            tex.SetPixels32(px);
            tex.Apply(true, true);
            var m = new Material(Mats.Lit(Color.white, 0.25f)) { name = "Stripes" };
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", new Vector2(tileU, 1f));
            return m;
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

        static TextMeshPro Text(Transform parent, string text, float size, Color color, Vector3 pos, Quaternion rot, float width)
        {
            var t = TextFactory.World(parent, text, size, color, TextAlignmentOptions.Center, false, width);
            t.transform.localPosition = pos;
            t.transform.localRotation = rot;
            return t;
        }
    }

    /// <summary>
    /// The stamp stall's power switch (Berlin). Throw it and the lever clunks down, the booth's power comes on with a hum,
    /// the lightbox sign and the string of bulbs warm up over a second or so (two soft dips, never a flash), a small light
    /// spills onto the counter and the desk fan spins up. Throw it again to switch it all off. Changes nothing else.
    /// </summary>
    public class BerlinBoothPower : Interactable
    {
        public Transform Lever, Fan;
        public Material SignMat, BulbMat;
        public Light SignLight;

        static readonly Color SignOn = new Color(1.5f, 1.38f, 1.12f), BulbOn = new Color(2.0f, 1.45f, 0.8f);
        const float LightOn = 1.1f, LeverUp = -28f, LeverDown = -152f; // swings out toward the player, up (off) to down (on)

        public bool On { get; private set; }

        Cell m_Cell;
        AudioSource m_Hum;
        float m_Level, m_FanSpeed, m_LeverAngle = LeverUp;
        Coroutine m_Ramp;

        void Start()
        {
            m_Cell = GetComponentInParent<Cell>();
            Apply(0f);
        }

        bool Live => m_Cell == null || m_Cell.Mode == CellMode.Active;

        // Hidden mid-ramp (the player left the cell): finish the switch where it was heading.
        void OnDisable()
        {
            if (m_Ramp != null) { StopCoroutine(m_Ramp); m_Ramp = null; }
            Apply(On ? 1f : 0f);
        }

        public override string GetPrompt(Interactor who) => On ? "Switch the stall off" : "Throw the stall's power switch";

        public override void Use(Interactor who)
        {
            if (!CanUse(who)) return;
            base.Use(who);
            On = !On;
            AudioDirector.Haptic(who != null ? who.Side : HandSide.Desktop, 0.45f, 0.08f);
            if (m_Ramp != null) StopCoroutine(m_Ramp);
            m_Ramp = StartCoroutine(On ? PowerUp() : PowerDown());
        }

        IEnumerator PowerUp()
        {
            if (Live)
            {
                AudioDirector.I?.Play("booth_power", transform.position, 0.85f);
                Hud.I?.ShowCaption("the stall's sign hums to life", 2.5f);
            }
            float from = m_Level;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 1.4f)
            {
                // A warm-up with two gentle dips, eased so nothing strobes.
                float k = Mathf.SmoothStep(from, 1f, t);
                float dip = 1f - 0.4f * Bump(t, 0.32f, 0.07f) - 0.3f * Bump(t, 0.62f, 0.06f);
                Apply(k * dip);
                yield return null;
            }
            Apply(1f);
            m_Ramp = null;
        }

        IEnumerator PowerDown()
        {
            if (Live) AudioDirector.I?.Play("fridge_stop", transform.position, 0.7f, 1.2f);
            float from = m_Level;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.6f)
            {
                Apply(Mathf.Lerp(from, 0f, Mathf.SmoothStep(0f, 1f, t)));
                yield return null;
            }
            Apply(0f);
            m_Ramp = null;
        }

        static float Bump(float t, float at, float width) => Mathf.Clamp01(1f - Mathf.Abs(t - at) / width);

        void Apply(float level)
        {
            m_Level = level;
            if (SignMat != null) SignMat.SetColor("_EmissionColor", SignOn * level);
            if (BulbMat != null) BulbMat.SetColor("_EmissionColor", BulbOn * level);
            if (SignLight != null) { SignLight.intensity = LightOn * level; SignLight.enabled = level > 0.01f; }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            m_LeverAngle = Mathf.MoveTowards(m_LeverAngle, On ? LeverDown : LeverUp, dt * 600f);
            if (Lever != null) Lever.localRotation = Quaternion.Euler(m_LeverAngle, 0f, 0f);
            m_FanSpeed = Mathf.MoveTowards(m_FanSpeed, On ? 900f : 0f, dt * (On ? 500f : 260f));
            if (Fan != null && m_FanSpeed > 0.5f) Fan.localRotation *= Quaternion.AngleAxis(m_FanSpeed * dt, Vector3.forward);
            if (Game.Paused) return;
            bool want = On && Live;
            if (want && (m_Hum == null || !m_Hum.isPlaying))
            {
                if (m_Hum != null) AudioDirector.I?.FadeOutAndStop(m_Hum, 0.05f);
                m_Hum = AudioDirector.I?.Loop("fridge_hum_loop", transform, 0.14f, Bus.SFX, 1f, 1f, 7f);
                if (m_Hum != null) m_Hum.pitch = 1.35f;
            }
            else if (!want && m_Hum != null)
            {
                AudioDirector.I?.FadeOutAndStop(m_Hum, 0.4f);
                m_Hum = null;
            }
        }
    }
}

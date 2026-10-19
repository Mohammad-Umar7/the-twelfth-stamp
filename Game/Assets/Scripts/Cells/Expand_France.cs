using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Paris expansion: the Cour du Douzième. The festival street used to stop at an invisible wall just south of the
    /// arrival; now a limestone block closes its south end with a porte-cochère, and a short vaulted passage under it leads
    /// into a rainy courtyard: a café terrace under a striped awning, a stone fountain, potted trees, laundry strung between
    /// the windows, pigeons, and a street organ that wheezes out a waltz when its crank is turned. Switchable lanterns light
    /// the courtyard's dark corners and two dark stretches of the bridge on the way to the tower.
    /// Layout (cell-local metres): the new street facade at z -10.05 (x -11..4.15, seen from x -4.35), the passage
    /// x -1.3..1.5 (z -10.05..-13.4), the courtyard x -9.2..2.8, z -13.4..-27.4, the river front at x 4.15. Everything is
    /// under the cell's child "Expand_France"; the street's old south bound is shortened to the river (x 4.45 on).
    /// </summary>
    public static class Expand_France
    {
        const float FZ = -10.05f, CZN = -13.4f, CZS = -27.4f, SZ = -29.2f;
        const float CXW = -9.2f, CXE = 2.8f, RX = 4.15f, WX = -11f;
        const float PX0 = -1.3f, PX1 = 1.5f, PXC = 0.1f, Spring = 2.4f, ArchR = 1.4f, ArchTop = 4f;
        const float H = 16.4f, StreetL = -4.35f;
        static readonly Vector3 FountainAt = new Vector3(-4.4f, 0f, -20f);
        static readonly Vector3 OrganAt = new Vector3(1.9f, 0f, -18.4f);
        static readonly Color Warm = new Color(1f, 0.76f, 0.48f);

        sealed class Pal
        {
            public Material Stone, StoneBase, Trim, Plaster, Zinc, Iron, Frame, Paint, GlassDark, GlassLit, Chimney, Cobble, Pavers;
            public Material Shutter, ShutterB, DoorGreen, DoorRed, DoorBlue, Terracotta, Soil, Bark, Leaf, Flower, FlowerB, Curtain;
            public Material Puddle, Brass, LampGlow, ShopGlow, CafeGlow, CafeRed, Quay, Wood, Enamel, EnamelEdge, White, Black, Card, Lacquer;
        }

        /// <summary>A wall plane: u runs along it (R), v is height, d is out from the wall (N).</summary>
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
            public Quaternion TextRot => Quaternion.LookRotation(-N, Vector3.up); // TMP reads from its local -Z
        }

        static void B(KitBatch k, Material m, Face f, float u, float v, float d, float w, float h, float t, float uv = 1f)
            => k.Box(m, f.P(u, v, d), new Vector3(w, h, t), f.Q, uv);

        public static void Build(ParisCell cell, Transform root)
        {
            var ex = new GameObject("Expand_France").transform;
            ex.SetParent(cell.transform, false);
            var p = Palette();
            var rnd = new System.Random(1212);
            OpenStreetEnd(cell);
            Masses(ex, p);
            Floors(ex, p);
            StreetFacade(ex, p, rnd);
            Passage(ex, p);
            Courtyard(ex, p, rnd);
            RiverFront(ex, p, rnd);
            Roofs(ex, p);
            Fountain(ex, p);
            Cafe(ex, p);
            Plants(ex, p);
            Laundry(ex, p);
            Organ(ex, p);
            Life(ex);
            Lamps(ex);
            Postcards(ex);
            var shot = new GameObject("ShotPoint").transform;
            shot.SetParent(ex, false);
            shot.localPosition = new Vector3(0.6f, 0f, -14.3f);
            shot.localRotation = Quaternion.Euler(0f, 195f, 0f); // from the passage mouth across the fountain to the café
        }

        static Pal Palette() => new Pal
        {
            Stone = Mats.Lit(new Color(0.8f, 0.76f, 0.68f), 0.3f, 0f, "limestone_blocks"),
            StoneBase = Mats.Lit(new Color(0.7f, 0.66f, 0.59f), 0.3f, 0f, "limestone_blocks"),
            Trim = Mats.Lit(new Color(0.86f, 0.82f, 0.74f), 0.3f, 0f, "limestone_blocks"),
            Plaster = Mats.Lit(new Color(0.84f, 0.78f, 0.68f), 0.3f, 0f, "plaster_warm"),
            Zinc = Mats.Lit(new Color(0.85f, 0.9f, 0.97f), 0.55f, 0.6f, "metal_brushed"),
            Iron = Mats.Lit(new Color(0.03f, 0.03f, 0.032f), 0.45f, 0.7f),
            Frame = Mats.Lit(new Color(0.1f, 0.13f, 0.12f), 0.4f, 0f),
            Paint = Mats.Lit(new Color(0.82f, 0.81f, 0.77f), 0.4f),
            GlassDark = Mats.Lit(new Color(0.04f, 0.05f, 0.065f), 0.92f, 0.2f),
            GlassLit = Mats.Emissive(new Color(0.3f, 0.25f, 0.18f), new Color(0.75f, 0.47f, 0.25f)),
            Chimney = Mats.Lit(new Color(1f, 0.92f, 0.88f), 0.3f, 0f, "brick_london"),
            Cobble = Mats.Lit(new Color(0.95f, 0.95f, 1f), 0.6f, 0f, "cobble_wet"),
            Pavers = Mats.Lit(new Color(0.9f, 0.89f, 0.87f), 0.5f, 0f, "granite_pavers"),
            Shutter = Mats.Lit(new Color(0.3f, 0.4f, 0.37f), 0.35f),
            ShutterB = Mats.Lit(new Color(0.4f, 0.45f, 0.52f), 0.35f),
            DoorGreen = Mats.Lit(new Color(0.06f, 0.16f, 0.12f), 0.5f),
            DoorRed = Mats.Lit(new Color(0.3f, 0.08f, 0.06f), 0.5f),
            DoorBlue = Mats.Lit(new Color(0.12f, 0.17f, 0.27f), 0.45f),
            Terracotta = Mats.Lit(new Color(0.62f, 0.36f, 0.24f), 0.3f, 0f, "plaster_terracotta"),
            Soil = Mats.Lit(new Color(0.1f, 0.08f, 0.06f), 0.2f),
            Bark = Mats.Lit(new Color(0.75f, 0.7f, 0.62f), 0.3f, 0f, "wood_dark"),
            Leaf = Mats.Lit(new Color(0.16f, 0.24f, 0.13f), 0.3f),
            Flower = Mats.Lit(new Color(0.72f, 0.09f, 0.09f), 0.3f),
            FlowerB = Mats.Lit(new Color(0.85f, 0.45f, 0.6f), 0.3f),
            Curtain = Mats.Lit(new Color(0.62f, 0.2f, 0.15f), 0.2f, 0f, "fabric_linen"),
            Puddle = Mats.Lit(new Color(0.03f, 0.035f, 0.045f), 0.96f, 0f),
            Brass = Mats.Lit(new Color(0.75f, 0.58f, 0.3f), 0.6f, 0.9f),
            LampGlow = Mats.Emissive(new Color(0.3f, 0.24f, 0.16f), new Color(1.25f, 0.82f, 0.42f)),
            ShopGlow = Mats.Emissive(new Color(0.24f, 0.17f, 0.11f), new Color(0.3f, 0.18f, 0.085f)),
            CafeGlow = Mats.Emissive(new Color(0.35f, 0.25f, 0.15f), new Color(0.95f, 0.6f, 0.3f)),
            CafeRed = Mats.Lit(new Color(0.32f, 0.07f, 0.06f), 0.5f),
            Quay = Mats.Lit(new Color(0.58f, 0.55f, 0.5f), 0.45f, 0f, "limestone_blocks"),
            Wood = Mats.Lit(new Color(1f, 0.92f, 0.85f), 0.45f, 0f, "wood_dark"),
            Enamel = Mats.Lit(new Color(0.06f, 0.14f, 0.4f), 0.75f),
            EnamelEdge = Mats.Lit(new Color(0.12f, 0.36f, 0.28f), 0.7f),
            White = Mats.Lit(new Color(0.88f, 0.87f, 0.82f), 0.35f),
            Black = Mats.Lit(new Color(0.03f, 0.03f, 0.035f), 0.4f),
            Card = Mats.Lit(new Color(0.86f, 0.8f, 0.66f), 0.2f, 0f, "paper_ivory"),
            Lacquer = Mats.Lit(new Color(0.4f, 0.07f, 0.06f), 0.65f),
        };

        // ------------------------------------------------------------------ the street end
        /// <summary>The street's south bound ran across the street at z -8.25; the new block closes it now, so keep only the
        /// part over the river (from just east of the river front).</summary>
        static void OpenStreetEnd(ParisCell cell)
        {
            var bounds = cell.transform.Find("Bounds");
            if (bounds == null) return;
            Transform south = null;
            foreach (Transform b in bounds)
                if (b.GetComponent<BoxCollider>() != null && (south == null || b.localPosition.z < south.localPosition.z)) south = b;
            if (south == null) return;
            var c = south.GetComponent<BoxCollider>();
            float x0 = south.localPosition.x + c.center.x - c.size.x * 0.5f, x1 = south.localPosition.x + c.center.x + c.size.x * 0.5f;
            const float cut = RX + 0.3f;
            if (x0 >= cut || x1 <= cut) return;
            south.localPosition = new Vector3((cut + x1) * 0.5f - c.center.x, south.localPosition.y, south.localPosition.z);
            c.size = new Vector3(x1 - cut, c.size.y, c.size.z);
        }

        /// <summary>The four blocks round the courtyard as solid boxes with colliders; the north block has the passage
        /// through it (its vault is the underside of the arch infill).</summary>
        static void Masses(Transform ex, Pal p)
        {
            var k = new KitBatch();
            float zc = (CZN + FZ) * 0.5f, dz = FZ - CZN;
            Solid(k, ex, p.Stone, new Vector3((WX + PX0) * 0.5f, H * 0.5f, zc), new Vector3(PX0 - WX, H, dz), 4f);
            Solid(k, ex, p.Stone, new Vector3((PX1 + RX) * 0.5f, H * 0.5f, zc), new Vector3(RX - PX1, H, dz), 4f);
            Solid(k, ex, p.Stone, new Vector3(PXC, (ArchTop + H) * 0.5f, zc), new Vector3(PX1 - PX0, H - ArchTop, dz), 4f);
            var poly = new List<Vector2>();
            for (int i = 0; i <= 18; i++)
            {
                float a = Mathf.PI * (1f - i / 18f);
                poly.Add(new Vector2(PXC + Mathf.Cos(a) * ArchR, Spring + Mathf.Sin(a) * ArchR));
            }
            poly.Add(new Vector2(PX1, ArchTop));
            poly.Add(new Vector2(PX0, ArchTop));
            k.Of(p.StoneBase).AddExtrudedXY(poly, dz, new Vector3(0f, 0f, zc), Quaternion.identity, 1.5f);
            Solid(k, ex, p.Plaster, new Vector3((CXE + RX) * 0.5f, H * 0.5f, (CZS + CZN) * 0.5f), new Vector3(RX - CXE, H, CZN - CZS), 2.5f);
            Solid(k, ex, p.Plaster, new Vector3((WX + RX) * 0.5f, H * 0.5f, (SZ + CZS) * 0.5f), new Vector3(RX - WX, H, CZS - SZ), 2.5f);
            Solid(k, ex, p.Plaster, new Vector3((WX + CXW) * 0.5f, H * 0.5f, (CZS + CZN) * 0.5f), new Vector3(CXW - WX, H, CZN - CZS), 2.5f);
            k.Bake(ex, "CourMasses");
        }

        static void Floors(Transform ex, Pal p)
        {
            MeshFactory.Floor("PassageFloor", ex, new Vector3(PXC, 0f, -11.7f), new Vector2(PX1 - PX0, 3.4f), p.Cobble, "stone", 0.3f, 1f);
            MeshFactory.Floor("CourtFloor", ex, new Vector3((CXW + CXE) * 0.5f, 0f, (CZN + CZS) * 0.5f), new Vector2(CXE - CXW, CZN - CZS), p.Cobble, "stone", 0.3f, 1f);
            var k = new KitBatch();
            float xc = (CXW + CXE) * 0.5f, zc = (CZN + CZS) * 0.5f;
            k.Box(p.Pavers, new Vector3(xc, 0f, CZN - 0.3f), new Vector3(CXE - CXW, 0.008f, 0.6f), 1.2f);
            k.Box(p.Pavers, new Vector3(xc, 0f, CZS + 0.3f), new Vector3(CXE - CXW, 0.008f, 0.6f), 1.2f);
            k.Box(p.Pavers, new Vector3(CXW + 0.3f, 0f, zc), new Vector3(0.6f, 0.008f, CZN - CZS - 1.2f), 1.2f);
            k.Box(p.Pavers, new Vector3(CXE - 0.3f, 0f, zc), new Vector3(0.6f, 0.008f, CZN - CZS - 1.2f), 1.2f);
            k.Box(p.Iron, new Vector3(-1.6f, 0.004f, -21.4f), new Vector3(0.42f, 0.008f, 0.42f));
            k.Bake(ex, "CourPaving", false);
            UrbanKit.Puddles(ex, new Vector3(-2.6f, 0.008f, -20.6f), new Vector2(9.5f, 11.5f), 8, 2012, p.Puddle); // over the pavers and the drain
            // The street's rain stops at the arch; the courtyard has its own.
            Kit.Weather(ex, new Vector3(xc, 9f, zc), new Vector3(CXE - CXW - 0.6f, 0.5f, CZN - CZS - 0.6f), true, new Color(0.75f, 0.8f, 0.9f, 0.4f), 360f, 9f);
        }

        // ------------------------------------------------------------------ the new street facade
        /// <summary>
        /// The block that closes the street (seen between the west block at x -4.35 and the river): a rusticated ground
        /// floor with the porte-cochère, a watchmaker's shut for the night (clocks at a minute to twelve behind the bars and a
        /// round sign swinging on its bracket), a front door with the court's enamel plaque, four storeys of French windows
        /// with a continuous second-floor balcony, the cornice, an attic and a zinc mansard.
        /// </summary>
        static void StreetFacade(Transform ex, Pal p, System.Random rnd)
        {
            var k = new KitBatch();
            var f = new Face(new Vector3(0f, 0f, FZ), Vector3.forward);
            const float L = StreetL, R = RX;
            B(k, p.StoneBase, f, (L + PX0) * 0.5f, 2.2f, 0.02f, PX0 - L, 4.4f, 0.04f, 4f);
            B(k, p.StoneBase, f, (PX1 + R) * 0.5f, 2.2f, 0.02f, R - PX1, 4.4f, 0.04f, 4f);
            B(k, p.StoneBase, f, PXC, (ArchTop + 4.4f) * 0.5f, 0.02f, PX1 - PX0, 4.4f - ArchTop, 0.04f, 4f);
            // Rustication grooves on the solid parts only.
            for (int g = 1; g < 6; g++)
            {
                float y = g * 4.4f / 6f;
                B(k, p.Frame, f, -4.15f, y, 0.045f, 0.4f, 0.025f, 0.012f);
                B(k, p.Frame, f, -1.525f, y, 0.045f, 0.45f, 0.025f, 0.012f);
                B(k, p.Frame, f, 1.85f, y, 0.045f, 0.7f, 0.025f, 0.012f);
                B(k, p.Frame, f, 3.8f, y, 0.045f, 0.7f, 0.025f, 0.012f);
                if (y > 3.3f) B(k, p.Frame, f, 2.85f, y, 0.045f, 1.2f, 0.025f, 0.012f);
            }
            // The arch: voussoirs with joints, keystone, imposts, jamb blocks, wheel guards inside the mouth.
            k.Of(p.Trim).AddArch(new Vector3(PXC, Spring, FZ + 0.06f), ArchR, ArchR + 0.42f, 0.12f, 18, Quaternion.identity, 1.5f);
            for (int i = 1; i < 8; i++)
            {
                float a = i * Mathf.PI / 8f;
                k.Box(p.Frame, new Vector3(PXC + Mathf.Cos(a) * (ArchR + 0.21f), Spring + Mathf.Sin(a) * (ArchR + 0.21f), FZ + 0.123f),
                    new Vector3(0.012f, 0.42f, 0.006f), Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg - 90f));
            }
            B(k, p.Trim, f, PXC, Spring + ArchR + 0.25f, 0.09f, 0.44f, 0.66f, 0.18f);
            B(k, p.Trim, f, PX0 - 0.08f, Spring - 0.07f, 0.07f, 0.44f, 0.14f, 0.14f);
            B(k, p.Trim, f, PX1 + 0.08f, Spring - 0.07f, 0.07f, 0.44f, 0.14f, 0.14f);
            B(k, p.Trim, f, PX0 - 0.13f, (Spring - 0.14f) * 0.5f, 0.045f, 0.26f, Spring - 0.14f, 0.09f);
            B(k, p.Trim, f, PX1 + 0.13f, (Spring - 0.14f) * 0.5f, 0.045f, 0.26f, Spring - 0.14f, 0.09f);
            foreach (float x in new[] { PX0 + 0.2f, PX1 - 0.2f })
            {
                var foot = new Vector3(x, 0f, FZ - 0.22f);
                k.Of(p.StoneBase).AddFrustum(foot, 0.2f, 0.09f, 0.5f, 10);
                k.Of(p.StoneBase).AddSphere(foot + Vector3.up * 0.5f, 0.09f, 8, 5, 1f, 0f, 90f);
                Col(ex, "WheelGuard", foot + Vector3.up * 0.3f, new Vector3(0.36f, 0.6f, 0.36f));
            }

            // The watchmaker's (shut): riser, a dim glow behind bars, three clocks at a minute to twelve, the fascia.
            const float su = -2.85f;
            B(k, p.Frame, f, su, 0.28f, 0.07f, 2.3f, 0.56f, 0.14f);
            B(k, p.ShopGlow, f, su, 1.78f, 0.045f, 2.2f, 2.44f, 0.01f);
            var face = Mats.Emissive(new Color(0.8f, 0.78f, 0.7f), new Color(0.45f, 0.4f, 0.3f));
            foreach (var (u, y, r) in new[] { (-3.45f, 1.35f, 0.16f), (-2.75f, 2.2f, 0.22f), (-2.1f, 1.6f, 0.13f) })
            {
                k.Of(face).AddDisc(f.P(u, y, 0.055f), f.N, r, 20);
                k.Of(p.Black).AddTorus(f.P(u, y, 0.055f), r, 0.012f, 20, 4, f.Q * Quaternion.Euler(90f, 0f, 0f));
                ClockHands(k, p.Black, f, u, y, 0.058f, r, -0.5f, -6f);
            }
            for (float u = -3.9f; u <= -1.79f; u += 0.11f) B(k, p.Iron, f, u, 1.78f, 0.09f, 0.016f, 2.44f, 0.016f);
            foreach (float y in new[] { 0.7f, 2.9f }) B(k, p.Iron, f, su, y, 0.09f, 2.2f, 0.03f, 0.02f);
            B(k, p.DoorGreen, f, su, 3.42f, 0.07f, 2.6f, 0.5f, 0.12f);
            Text(ex, "Horloger", 0.17f, new Color(0.86f, 0.72f, 0.42f), f.P(su, 3.42f, 0.135f), f.TextRot, 2.4f, true);

            // A front door, a fanlight, the house number and the court's enamel plaque.
            const float du = 2.85f;
            B(k, p.DoorGreen, f, du, 1.3f, 0.03f, 1.1f, 2.6f, 0.04f);
            foreach (float pu in new[] { -0.26f, 0.26f })
                foreach (var (py, ph) in new[] { (0.62f, 0.8f), (1.75f, 1.15f) })
                    B(k, p.DoorGreen, f, du + pu, py, 0.056f, 0.4f, ph, 0.02f);
            k.Of(p.Brass).AddSphere(f.P(du, 1.32f, 0.08f), 0.045f, 8, 6);
            B(k, p.GlassDark, f, du, 2.9f, 0.02f, 1.1f, 0.5f, 0.02f);
            for (int i = 1; i < 4; i++) B(k, p.Iron, f, du - 0.55f + i * 0.275f, 2.9f, 0.035f, 0.015f, 0.5f, 0.012f);
            B(k, p.Trim, f, du, 3.24f, 0.05f, 1.42f, 0.14f, 0.1f);
            B(k, p.Trim, f, du - 0.63f, 1.62f, 0.04f, 0.16f, 3.24f, 0.08f);
            B(k, p.Trim, f, du + 0.63f, 1.62f, 0.04f, 0.16f, 3.24f, 0.08f);
            B(k, p.Enamel, f, du, 3.55f, 0.02f, 0.24f, 0.17f, 0.015f);
            Text(ex, "12", 0.085f, new Color(0.95f, 0.95f, 0.92f), f.P(du, 3.55f, 0.03f), f.TextRot, 0.22f, false);
            B(k, p.EnamelEdge, f, du, 3.95f, 0.02f, 1.14f, 0.34f, 0.014f);
            B(k, p.Enamel, f, du, 3.95f, 0.028f, 1.08f, 0.28f, 0.006f);
            Text(ex, "COUR DU DOUZIÈME", 0.062f, new Color(0.95f, 0.95f, 0.92f), f.P(du, 3.95f, 0.033f), f.TextRot, 1.04f, false);

            // Storeys: string courses, French windows with surrounds, balconettes and the second-floor balcony.
            B(k, p.Trim, f, (L + R) * 0.5f, 4.3f, 0.13f, R - L + 0.1f, 0.22f, 0.26f, 2f);
            var cols = new[] { -2.9f, 0.1f, 2.9f };
            for (int fl = 1; fl <= 4; fl++)
            {
                float y = 4.4f + (fl - 1) * 3f;
                B(k, p.Trim, f, (L + R) * 0.5f, y + 0.04f, 0.05f, R - L, 0.1f, 0.12f, 2f);
                if (fl == 2)
                {
                    B(k, p.Trim, f, (L + R) * 0.5f, y + 0.07f, 0.3f, R - L, 0.14f, 0.6f, 2f);
                    UrbanKit.Railing(k, p.Iron, f.P(L + 0.05f, y + 0.14f, 0.56f), f.P(R - 0.05f, y + 0.14f, 0.56f), 0.95f, Vector3.zero);
                }
                foreach (float u in cols)
                {
                    HaussWindow(k, p, f, u, y + 0.25f, 1.1f, 2.05f, rnd.NextDouble() < 0.34);
                    if (fl != 2) UrbanKit.Railing(k, p.Iron, f.P(u - 0.62f, y + 0.25f, 0.16f), f.P(u + 0.62f, y + 0.25f, 0.16f), 0.75f, Vector3.zero);
                    else B(k, p.Trim, f, u, y + 2.55f, 0.09f, 1.5f, 0.32f, 0.16f);
                }
            }
            B(k, p.Trim, f, (L + R) * 0.5f, 16.58f, 0.25f, R - L + 0.3f, 0.36f, 0.5f, 2f);
            B(k, p.Trim, f, (L + R) * 0.5f, 16.42f, 0.12f, R - L + 0.1f, 0.12f, 0.24f, 2f);
            // Attic behind a continuous balcony, set back over the cornice; dormers on the mansard above.
            B(k, p.Stone, f, (WX + R) * 0.5f, 18.03f, -0.575f, R - WX, 2.54f, 0.25f, 4f);
            UrbanKit.Railing(k, p.Iron, f.P(L, 16.76f, 0.2f), f.P(R, 16.76f, 0.2f), 0.9f, Vector3.zero);
            foreach (float u in cols)
            {
                B(k, rnd.NextDouble() < 0.3 ? p.GlassLit : p.GlassDark, f, u, 17.95f, -0.44f, 1.0f, 1.9f, 0.02f);
                B(k, p.Trim, f, u, 19.0f, -0.41f, 1.3f, 0.16f, 0.08f);
                B(k, p.Frame, f, u, 17.95f, -0.425f, 0.05f, 1.9f, 0.03f);
                Dormer(k, p, u, 19.55f, FZ - 0.9f, rnd.NextDouble() < 0.35);
            }
            k.Bake(ex, "CourStreetFacade");

            // The watchmaker's round sign on a bracket, swinging (hinge along the facade).
            var hook = new GameObject("WatchmakerBracket").transform;
            hook.SetParent(ex, false);
            hook.localPosition = f.P(-1.55f, 3.92f, 0.42f);
            var bracket = new KitBatch();
            bracket.Box(p.Iron, new Vector3(0f, 0.04f, -0.21f), new Vector3(0.03f, 0.03f, 0.46f));
            bracket.Box(p.Iron, new Vector3(0f, 0.02f, 0f), new Vector3(0.34f, 0.02f, 0.02f));
            bracket.Bake(hook, "Bracket");
            var pivot = new GameObject("WatchmakerSign").transform;
            pivot.SetParent(ex, false);
            pivot.localPosition = hook.localPosition;
            var sign = new KitBatch();
            foreach (float sx in new[] { -0.12f, 0.12f }) sign.Box(p.Iron, new Vector3(sx, -0.06f, 0f), new Vector3(0.008f, 0.12f, 0.008f));
            sign.Of(p.DoorGreen).AddCylinder(new Vector3(0f, -0.34f, -0.015f), 0.21f, 0.03f, 24, 1f, Quaternion.Euler(90f, 0f, 0f));
            sign.Of(p.Brass).AddTorus(new Vector3(0f, -0.34f, 0f), 0.21f, 0.012f, 24, 4, Quaternion.Euler(90f, 0f, 0f));
            foreach (float side in new[] { 1f, -1f })
            {
                sign.Of(face).AddDisc(new Vector3(0f, -0.34f, side * 0.017f), new Vector3(0f, 0f, side), 0.16f, 20);
                var sf = new Face(new Vector3(0f, -0.34f, 0f), new Vector3(0f, 0f, side));
                ClockHands(sign, p.Black, sf, 0f, 0f, 0.02f, 0.16f, -0.5f, -6f);
            }
            sign.Bake(pivot, "Sign", false);
            WindSway.Add(pivot, 6f, 2.7f, Vector3.right);
        }

        static void ClockHands(KitBatch k, Material m, Face f, float u, float y, float d, float r, float hourDeg, float minuteDeg)
        {
            foreach (var (deg, len, wid) in new[] { (hourDeg, r * 0.55f, r * 0.09f), (minuteDeg, r * 0.85f, r * 0.06f) })
            {
                float a = deg * Mathf.Deg2Rad;
                var dir = f.R * -Mathf.Sin(a) + Vector3.up * Mathf.Cos(a);
                k.Box(m, f.P(u, y, d) + dir * len * 0.5f, new Vector3(wid, len, 0.006f), Quaternion.LookRotation(f.N, dir));
            }
        }

        static void HaussWindow(KitBatch k, Pal p, Face f, float u, float sill, float w, float h, bool lit)
        {
            B(k, lit ? p.GlassLit : p.GlassDark, f, u, sill + h * 0.5f, 0.01f, w, h, 0.02f);
            B(k, p.Trim, f, u, sill + h + 0.09f, 0.05f, w + 0.32f, 0.18f, 0.1f);
            B(k, p.Trim, f, u, sill - 0.04f, 0.07f, w + 0.22f, 0.08f, 0.14f);
            B(k, p.Trim, f, u - w * 0.5f - 0.07f, sill + h * 0.5f, 0.04f, 0.14f, h, 0.08f);
            B(k, p.Trim, f, u + w * 0.5f + 0.07f, sill + h * 0.5f, 0.04f, 0.14f, h, 0.08f);
            B(k, p.Frame, f, u, sill + h * 0.5f, 0.025f, 0.05f, h, 0.03f);
            B(k, p.Frame, f, u, sill + h * 0.72f, 0.025f, w, 0.04f, 0.03f);
        }

        /// <summary>A zinc dormer on the street-side mansard at x, its window facing the street.</summary>
        static void Dormer(KitBatch k, Pal p, float x, float y, float z, bool lit)
        {
            k.Box(p.Zinc, new Vector3(x, y + 0.62f, z), new Vector3(0.95f, 1.25f, 0.7f));
            k.Box(lit ? p.GlassLit : p.GlassDark, new Vector3(x, y + 0.58f, z + 0.36f), new Vector3(0.6f, 0.85f, 0.02f));
            k.Box(p.Frame, new Vector3(x, y + 0.58f, z + 0.37f), new Vector3(0.04f, 0.85f, 0.03f));
            k.Box(p.Zinc, new Vector3(x - 0.27f, y + 1.38f, z), new Vector3(0.62f, 0.05f, 0.78f), Quaternion.Euler(0f, 0f, 32f));
            k.Box(p.Zinc, new Vector3(x + 0.27f, y + 1.38f, z), new Vector3(0.62f, 0.05f, 0.78f), Quaternion.Euler(0f, 0f, -32f));
        }

        // ------------------------------------------------------------------ the passage
        /// <summary>The vaulted passage: carriage doors pinned open, plinths, letterboxes, the concierge's lit window, two
        /// festival posters, and a lantern swinging gently under the crown of the vault.</summary>
        static void Passage(Transform ex, Pal p)
        {
            var k = new KitBatch();
            const float zc = (FZ + CZN) * 0.5f, len = FZ - CZN;
            k.Box(p.StoneBase, new Vector3(PX0 + 0.02f, 0.3f, zc), new Vector3(0.04f, 0.6f, len), 2f);
            k.Box(p.StoneBase, new Vector3(PX1 - 0.02f, 0.3f, zc), new Vector3(0.04f, 0.6f, len), 2f);
            foreach (var (x, s) in new[] { (PX0 + 0.08f, 1f), (PX1 - 0.08f, -1f) })
            {
                var c = new Vector3(x, 1.4f, FZ - 0.86f);
                k.Box(p.DoorGreen, c, new Vector3(0.07f, 2.8f, 1.36f));
                foreach (float dz in new[] { -0.33f, 0.33f })
                    foreach (var (dy, ph) in new[] { (-0.75f, 1.0f), (0.55f, 1.2f) })
                        k.Box(p.DoorGreen, c + new Vector3(s * 0.045f, dy, dz), new Vector3(0.02f, ph, 0.5f));
                k.Of(p.Brass).AddSphere(c + new Vector3(s * 0.065f, -0.25f, -0.6f), 0.035f, 8, 6);
                Col(ex, "CarriageDoor", c, new Vector3(0.1f, 2.8f, 1.36f));
            }
            for (int i = 0; i < 6; i++)
            {
                var c = new Vector3(PX0 + 0.06f, 1.35f + (i / 3) * 0.32f, -12.45f - (i % 3) * 0.24f);
                k.Box(p.Wood, c, new Vector3(0.1f, 0.28f, 0.2f));
                k.Box(p.Brass, c + new Vector3(0.052f, 0.06f, 0f), new Vector3(0.006f, 0.02f, 0.14f));
            }
            // The concierge's window, lit behind a half curtain.
            k.Box(p.GlassLit, new Vector3(PX1 - 0.006f, 1.55f, -12.5f), new Vector3(0.012f, 1.0f, 0.9f));
            k.Box(p.White, new Vector3(PX1 - 0.014f, 1.3f, -12.5f), new Vector3(0.006f, 0.5f, 0.86f));
            k.Box(p.Trim, new Vector3(PX1 - 0.03f, 2.1f, -12.5f), new Vector3(0.06f, 0.1f, 1.1f));
            k.Box(p.Trim, new Vector3(PX1 - 0.04f, 1.0f, -12.5f), new Vector3(0.08f, 0.06f, 1.06f));
            k.Box(p.Frame, new Vector3(PX1 - 0.016f, 1.55f, -12.5f), new Vector3(0.02f, 1.0f, 0.03f));
            Text(ex, "LOGE", 0.06f, new Color(0.86f, 0.72f, 0.42f), new Vector3(PX1 - 0.02f, 2.28f, -12.5f), Quaternion.LookRotation(Vector3.right), 0.6f, true);
            k.Bake(ex, "CourPassage");
            foreach (var (art, z) in new[] { ("postcard_2", -11.2f), ("postcard_4", -13.05f) })
            {
                var tex = Tex.Art(art);
                if (tex == null) continue;
                var q = MeshFactory.Quad("Poster", ex, 0.62f, 0.4f, Mats.Printed(tex, new Color(0.82f, 0.8f, 0.74f), 0.2f),
                    new Vector3(PX0 + 0.012f, 1.75f, z), Quaternion.LookRotation(Vector3.left));
                q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            var pivot = new GameObject("PassageLantern").transform;
            pivot.SetParent(ex, false);
            pivot.localPosition = new Vector3(PXC, Spring + ArchR - 0.05f, -11.75f);
            var lk = new KitBatch();
            lk.Box(p.Iron, new Vector3(0f, -0.2f, 0f), new Vector3(0.014f, 0.4f, 0.014f));
            lk.Of(p.Iron).AddFrustum(new Vector3(0f, -0.48f, 0f), 0.16f, 0.03f, 0.1f, 6);
            lk.Of(p.LampGlow).AddFrustum(new Vector3(0f, -0.8f, 0f), 0.1f, 0.15f, 0.32f, 6);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                lk.Box(p.Iron, new Vector3(Mathf.Cos(a) * 0.13f, -0.64f, Mathf.Sin(a) * 0.13f), new Vector3(0.016f, 0.34f, 0.016f));
            }
            lk.Of(p.Iron).AddCylinder(new Vector3(0f, -0.84f, 0f), 0.09f, 0.04f, 8);
            lk.Bake(pivot, "Lantern", false);
            var l = MakeLight(pivot, new Vector3(0f, -0.65f, 0f), Warm, 1.25f, 5.5f);
            Flicker.Add(l, 0.07f, 2f);
            WindSway.Add(pivot, 3.5f, 3.1f);
        }

        // ------------------------------------------------------------------ the courtyard
        static void Courtyard(Transform ex, Pal p, System.Random rnd)
        {
            var k = new KitBatch();
            var north = new Face(new Vector3(CXE, 0f, CZN), Vector3.back);   // u = 2.8 - x
            var west = new Face(new Vector3(CXW, 0f, CZN), Vector3.right);   // u = -13.4 - z
            var south = new Face(new Vector3(CXW, 0f, CZS), Vector3.forward); // u = x + 9.2
            var east = new Face(new Vector3(CXE, 0f, CZS), Vector3.left);    // u = z + 27.4
            CourtFacade(k, p, north, 12f, new[] { 2.7f, 5.6f, 8.4f, 11.0f }, rnd, CXE - PX1, CXE - PX0); // gap: the passage
            CourtFacade(k, p, west, 14f, new[] { 1.7f, 4.5f, 7.3f, 10.1f, 12.7f }, rnd);
            CourtFacade(k, p, south, 12f, new[] { 1.4f, 4.2f, 7.0f, 9.8f }, rnd);
            CourtFacade(k, p, east, 14f, new[] { 2.0f, 4.8f, 7.6f, 10.4f, 13.0f }, rnd);
            foreach (var (f, w) in new[] { (north, 12f), (west, 14f), (south, 12f), (east, 14f) })
            {
                Downpipe(k, p, f, 0.35f);
                Downpipe(k, p, f, w - 0.35f);
            }
            // The arch on the courtyard side.
            k.Of(p.Trim).AddArch(new Vector3(PXC, Spring, CZN - 0.06f), ArchR, ArchR + 0.36f, 0.12f, 18, Quaternion.identity, 1.5f);
            k.Box(p.Trim, new Vector3(PXC, Spring + ArchR + 0.2f, CZN - 0.09f), new Vector3(0.4f, 0.56f, 0.18f));

            // Ground floors: doors, barred windows, a painted atelier, bins, a bicycle against the wall.
            CourtDoor(k, p, north, 6.4f, 1.1f, 2.4f, p.DoorRed, true);
            Text(ex, "ESCALIER A", 0.07f, new Color(0.9f, 0.9f, 0.86f), north.P(6.4f, 3.25f, 0.03f), north.TextRot, 1.0f, false);
            B(k, p.Enamel, north, 6.4f, 3.25f, 0.015f, 0.9f, 0.18f, 0.012f);
            GrilleWindow(k, p, north, 9.2f, 1.0f, 0.9f, 1.4f);
            // Atelier: glazed double doors under a painted name.
            float au = 6.4f;
            B(k, p.DoorBlue, west, au, 1.4f, 0.03f, 2.0f, 2.8f, 0.05f);
            foreach (float pu in new[] { -0.5f, 0.5f }) B(k, p.GlassDark, west, au + pu, 2.0f, 0.058f, 0.78f, 1.2f, 0.01f);
            B(k, p.DoorBlue, west, au, 1.4f, 0.062f, 0.05f, 2.8f, 0.02f);
            B(k, p.Trim, west, au, 2.9f, 0.05f, 2.3f, 0.14f, 0.1f);
            Text(ex, "ATELIER", 0.16f, new Color(0.28f, 0.2f, 0.14f), west.P(au, 3.35f, 0.01f), west.TextRot, 2.2f, true);
            GrilleWindow(k, p, west, 9.0f, 1.0f, 0.9f, 1.4f);
            CourtDoor(k, p, west, 12.0f, 1.0f, 2.3f, p.DoorGreen, false);
            CourtDoor(k, p, south, 2.4f, 1.0f, 2.3f, p.DoorGreen, true);
            GrilleWindow(k, p, south, 4.4f, 1.0f, 0.9f, 1.4f);
            CourtDoor(k, p, east, 4.6f, 1.0f, 2.3f, p.DoorBlue, false);
            CourtWindow(k, p, east, 7.6f, 1.0f, 0.9f, 1.4f, true, 1, true);
            foreach (float u in new[] { 5.7f, 6.5f })
            {
                var c = east.P(u, 0f, 0.4f);
                k.Box(p.DoorGreen, c + Vector3.up * 0.47f, new Vector3(0.6f, 0.94f, 0.62f));
                k.Box(p.DoorGreen, c + new Vector3(0.02f, 0.97f, 0f), new Vector3(0.66f, 0.06f, 0.68f));
            }
            Col(ex, "Bins", east.P(6.1f, 0.5f, 0.4f), new Vector3(0.7f, 1.0f, 1.5f));
            Bicycle(k, p, new Vector3(CXW + 0.32f, 0f, -17.4f));
            Col(ex, "Bicycle", new Vector3(CXW + 0.3f, 0.55f, -17.4f), new Vector3(0.35f, 1.1f, 1.75f));
            k.Bake(ex, "CourFacades");
        }

        /// <summary>Plinth, string courses, four storeys of shuttered windows (some lit, some with geraniums), cornice.</summary>
        static void CourtFacade(KitBatch k, Pal p, Face f, float width, float[] cols, System.Random rnd, float gap0 = 0f, float gap1 = 0f)
        {
            if (gap1 > gap0) // the plinth stops either side of an opening at ground level
            {
                B(k, p.StoneBase, f, gap0 * 0.5f, 0.25f, 0.015f, gap0, 0.5f, 0.03f, 2f);
                B(k, p.StoneBase, f, (gap1 + width) * 0.5f, 0.25f, 0.015f, width - gap1, 0.5f, 0.03f, 2f);
            }
            else B(k, p.StoneBase, f, width * 0.5f, 0.25f, 0.015f, width, 0.5f, 0.03f, 2f);
            for (int fl = 1; fl <= 4; fl++)
            {
                float y = 4f + (fl - 1) * 3f;
                B(k, p.Trim, f, width * 0.5f, y, 0.03f, width, 0.08f, 0.06f, 2f);
                foreach (float u in cols)
                {
                    double r = rnd.NextDouble();
                    int shut = r < 0.2 ? 0 : r < 0.55 ? 1 : r < 0.8 ? 2 : 3;
                    bool lit = shut != 2 && rnd.NextDouble() < 0.32;
                    CourtWindow(k, p, f, u, y + 0.55f, 0.95f, 1.85f, lit, shut, rnd.NextDouble() < 0.3, fl % 2 == 0);
                }
            }
            B(k, p.Trim, f, width * 0.5f, 16.2f, 0.17f, width, 0.4f, 0.34f, 2f);
            B(k, p.Trim, f, width * 0.5f, 15.95f, 0.08f, width, 0.1f, 0.16f, 2f);
        }

        /// <summary>A courtyard window: glass (lit ones with curtains), white frame, stone surround, shutters (0 none, 1 open,
        /// 2 closed, 3 one of each), and sometimes a box of geraniums on the sill.</summary>
        static void CourtWindow(KitBatch k, Pal p, Face f, float u, float sill, float w, float h, bool lit, int shut, bool flowers, bool altShutter = false)
        {
            float yc = sill + h * 0.5f;
            B(k, lit ? p.GlassLit : p.GlassDark, f, u, yc, 0.005f, w, h, 0.01f);
            if (lit)
            {
                B(k, p.Curtain, f, u - w * 0.5f + 0.11f, yc, 0.013f, 0.2f, h - 0.08f, 0.006f);
                B(k, p.Curtain, f, u + w * 0.5f - 0.11f, yc, 0.013f, 0.2f, h - 0.08f, 0.006f);
            }
            B(k, p.Paint, f, u, yc, 0.02f, 0.04f, h, 0.025f);
            B(k, p.Paint, f, u, sill + h * 0.72f, 0.02f, w, 0.035f, 0.025f);
            B(k, p.Trim, f, u, sill + h + 0.07f, 0.035f, w + 0.22f, 0.14f, 0.07f);
            B(k, p.Trim, f, u, sill - 0.04f, 0.06f, w + 0.18f, 0.07f, 0.12f);
            B(k, p.Trim, f, u - w * 0.5f - 0.05f, yc, 0.025f, 0.1f, h, 0.05f);
            B(k, p.Trim, f, u + w * 0.5f + 0.05f, yc, 0.025f, 0.1f, h, 0.05f);
            if (shut > 0)
            {
                var m = altShutter ? p.ShutterB : p.Shutter;
                float sw = w * 0.5f + 0.03f;
                for (int side = -1; side <= 1; side += 2)
                {
                    bool closed = shut == 2 || (shut == 3 && side > 0);
                    float su = closed ? u + side * (sw * 0.5f - 0.015f) : u + side * (w * 0.5f + 0.1f + sw * 0.5f);
                    float sd = closed ? 0.045f : 0.025f;
                    B(k, m, f, su, yc, sd, sw, h + 0.04f, 0.03f);
                    for (int s = 0; s < 4; s++) B(k, m, f, su, sill + 0.25f + s * (h - 0.4f) / 3f, sd + 0.018f, sw - 0.06f, 0.02f, 0.008f);
                }
            }
            if (flowers)
            {
                B(k, p.Terracotta, f, u, sill - 0.13f, 0.14f, w - 0.05f, 0.16f, 0.18f);
                for (int i = 0; i < 4; i++)
                {
                    float o = -0.33f + i * 0.22f;
                    k.Of(p.Leaf).AddSphere(f.P(u + o + 0.05f, sill - 0.01f, 0.15f), 0.08f, 6, 4);
                    k.Of(i % 2 == 0 ? p.Flower : p.FlowerB).AddSphere(f.P(u + o, sill + 0.04f, 0.16f), 0.06f, 6, 4);
                }
            }
        }

        static void CourtDoor(KitBatch k, Pal p, Face f, float u, float w, float h, Material leaf, bool litTransom)
        {
            B(k, leaf, f, u, h * 0.5f, 0.025f, w, h, 0.05f);
            foreach (float pu in new[] { -w * 0.24f, w * 0.24f })
                foreach (var (py, ph) in new[] { (h * 0.27f, h * 0.36f), (h * 0.7f, h * 0.38f) })
                    B(k, leaf, f, u + pu, py, 0.056f, w * 0.38f, ph, 0.016f);
            k.Of(p.Brass).AddSphere(f.P(u + w * 0.36f, 1.05f, 0.075f), 0.035f, 8, 6);
            B(k, litTransom ? p.GlassLit : p.GlassDark, f, u, h + 0.25f, 0.02f, w, 0.42f, 0.02f);
            B(k, p.Paint, f, u, h + 0.04f, 0.03f, w, 0.06f, 0.03f);
            B(k, p.Trim, f, u, h + 0.53f, 0.04f, w + 0.32f, 0.14f, 0.08f);
            B(k, p.Trim, f, u - w * 0.5f - 0.08f, (h + 0.46f) * 0.5f, 0.03f, 0.16f, h + 0.46f, 0.06f);
            B(k, p.Trim, f, u + w * 0.5f + 0.08f, (h + 0.46f) * 0.5f, 0.03f, 0.16f, h + 0.46f, 0.06f);
        }

        static void GrilleWindow(KitBatch k, Pal p, Face f, float u, float sill, float w, float h)
        {
            B(k, p.GlassDark, f, u, sill + h * 0.5f, 0.005f, w, h, 0.01f);
            B(k, p.Trim, f, u, sill + h + 0.06f, 0.035f, w + 0.2f, 0.12f, 0.07f);
            B(k, p.Trim, f, u, sill - 0.04f, 0.05f, w + 0.16f, 0.07f, 0.1f);
            for (int i = 0; i < 6; i++) B(k, p.Iron, f, u - w * 0.5f + 0.08f + i * (w - 0.16f) / 5f, sill + h * 0.5f, 0.07f, 0.016f, h, 0.016f);
            foreach (float y in new[] { 0.15f, h - 0.15f }) B(k, p.Iron, f, u, sill + y, 0.07f, w, 0.025f, 0.02f);
        }

        static void Downpipe(KitBatch k, Pal p, Face f, float u)
        {
            k.Of(p.Zinc).AddCylinder(f.P(u, 0.22f, 0.09f), 0.05f, 15.6f, 8);
            B(k, p.Zinc, f, u, 15.95f, 0.1f, 0.22f, 0.25f, 0.2f);
            B(k, p.Zinc, f, u, 0.15f, 0.13f, 0.12f, 0.08f, 0.2f);
        }

        /// <summary>A bicycle leaning on the west wall (wheels along z), frame red, a wicker basket.</summary>
        static void Bicycle(KitBatch k, Pal p, Vector3 at)
        {
            var frame = Mats.Lit(new Color(0.42f, 0.07f, 0.05f), 0.5f, 0.3f);
            var lean = Quaternion.Euler(0f, 0f, 8f);
            Vector3 P(float z, float y) => at + lean * new Vector3(0f, y, z);
            foreach (float z in new[] { -0.58f, 0.58f })
            {
                k.Of(p.Black).AddTorus(P(z, 0.34f), 0.32f, 0.022f, 20, 5, lean * Quaternion.Euler(0f, 0f, 90f));
                k.Of(p.Iron).AddTube(new List<Vector3> { P(z, 0.06f), P(z, 0.62f) }, 0.006f, 4);
                k.Of(p.Iron).AddTube(new List<Vector3> { P(z - 0.28f, 0.34f), P(z + 0.28f, 0.34f) }, 0.006f, 4);
            }
            k.Of(frame).AddTube(new List<Vector3> { P(-0.58f, 0.34f), P(-0.05f, 0.34f), P(0.42f, 0.78f), P(-0.12f, 0.8f), P(-0.05f, 0.34f) }, 0.018f, 6);
            k.Of(frame).AddTube(new List<Vector3> { P(-0.58f, 0.34f), P(-0.12f, 0.8f) }, 0.014f, 6);
            k.Of(frame).AddTube(new List<Vector3> { P(0.58f, 0.34f), P(0.42f, 0.78f), P(0.38f, 0.98f) }, 0.016f, 6);
            k.Of(p.Black).AddTube(new List<Vector3> { P(0.3f, 0.98f), P(0.38f, 1.0f), P(0.46f, 0.98f) }, 0.015f, 6);
            k.Of(p.Black).AddTube(new List<Vector3> { P(-0.12f, 0.8f), P(-0.15f, 0.92f) }, 0.014f, 6);
            k.Box(p.Black, P(-0.16f, 0.94f), new Vector3(0.12f, 0.05f, 0.24f), lean);
            k.Box(Mats.Lit(new Color(0.75f, 0.6f, 0.38f), 0.3f, 0f, "fabric_linen"), P(0.62f, 0.88f), new Vector3(0.3f, 0.22f, 0.26f), lean);
        }

        // ------------------------------------------------------------------ the river front
        /// <summary>The block's face to the Seine (seen from the bridge and the far bank): limestone, small barred windows
        /// below, four storeys of windows, the cornice, on a quay wall rising out of the water.</summary>
        static void RiverFront(Transform ex, Pal p, System.Random rnd)
        {
            var k = new KitBatch();
            var f = new Face(new Vector3(RX, 0f, FZ), Vector3.right); // u = -10.05 - z
            const float W = FZ - SZ;
            B(k, p.Stone, f, W * 0.5f, H * 0.5f, 0.02f, W, H, 0.04f, 4f);
            B(k, p.Quay, f, W * 0.5f, -1.2f, 0.15f, W, 2.4f, 0.3f, 2.5f);
            B(k, p.Trim, f, W * 0.5f, 0f, 0.19f, W, 0.1f, 0.38f, 2f);
            foreach (float u in new[] { 2.0f, 6.0f, 10.0f, 14.0f, 17.6f }) GrilleWindow(k, p, f, u + 0f, 1.3f, 0.8f, 1.2f);
            B(k, p.Trim, f, W * 0.5f, 4.3f, 0.13f, W, 0.22f, 0.26f, 2f);
            for (int fl = 1; fl <= 4; fl++)
            {
                float y = 4.4f + (fl - 1) * 3f;
                B(k, p.Trim, f, W * 0.5f, y + 0.04f, 0.06f, W, 0.1f, 0.12f, 2f);
                foreach (float u in new[] { 1.6f, 4.8f, 8.0f, 11.2f, 14.4f, 17.6f })
                {
                    HaussWindow(k, p, f, u, y + 0.3f, 1.0f, 1.95f, rnd.NextDouble() < 0.28);
                    if (fl == 1 || fl == 3) UrbanKit.Railing(k, p.Iron, f.P(u - 0.58f, y + 0.3f, 0.15f), f.P(u + 0.58f, y + 0.3f, 0.15f), 0.75f, Vector3.zero);
                }
            }
            B(k, p.Trim, f, W * 0.5f, 16.58f, 0.25f, W + 0.2f, 0.36f, 0.5f, 2f);
            k.Bake(ex, "CourRiverFront");
        }

        // ------------------------------------------------------------------ roofs
        /// <summary>Zinc roofs as closed prisms: the street block's mansard behind its attic, and lower zinc slopes over the
        /// courtyard blocks; brick chimney stacks with pots on the ridges.</summary>
        static void Roofs(Transform ex, Pal p)
        {
            var k = new KitBatch();
            var yaw90 = Quaternion.Euler(0f, 90f, 0f);
            // Along x (polygon x = -z, extruded along x): the street block and the south block.
            k.Of(p.Zinc).AddExtrudedXY(new List<Vector2> { new Vector2(10.75f, 16.4f), new Vector2(13.45f, 16.4f), new Vector2(13.45f, 16.95f),
                new Vector2(12.65f, 21.5f), new Vector2(11.45f, 21.5f), new Vector2(10.75f, 19.3f) }, RX - WX, new Vector3((WX + RX) * 0.5f, 0f, 0f), yaw90, 2f);
            k.Of(p.Zinc).AddExtrudedXY(new List<Vector2> { new Vector2(27.3f, 16.4f), new Vector2(29.3f, 16.4f), new Vector2(29.3f, 16.95f),
                new Vector2(28.5f, 18.8f), new Vector2(28.15f, 18.8f), new Vector2(27.3f, 16.95f) }, RX - WX, new Vector3((WX + RX) * 0.5f, 0f, 0f), yaw90, 2f);
            // Along z: the east block (over the river) and the west block.
            const float rz0 = -12.6f, rz1 = -28.3f;
            k.Of(p.Zinc).AddExtrudedXY(new List<Vector2> { new Vector2(2.7f, 16.4f), new Vector2(4.25f, 16.4f), new Vector2(4.25f, 16.95f),
                new Vector2(3.62f, 18.6f), new Vector2(3.32f, 18.6f), new Vector2(2.7f, 16.95f) }, rz0 - rz1, new Vector3(0f, 0f, (rz0 + rz1) * 0.5f), Quaternion.identity, 2f);
            k.Of(p.Zinc).AddExtrudedXY(new List<Vector2> { new Vector2(-11.1f, 16.4f), new Vector2(-9.1f, 16.4f), new Vector2(-9.1f, 16.95f),
                new Vector2(-9.88f, 18.8f), new Vector2(-10.25f, 18.8f), new Vector2(-11.1f, 16.95f) }, rz0 - rz1, new Vector3(0f, 0f, (rz0 + rz1) * 0.5f), Quaternion.identity, 2f);
            void Chimney(Vector3 at, bool alongX)
            {
                var s = alongX ? new Vector3(0.7f, 1.6f, 0.45f) : new Vector3(0.45f, 1.6f, 0.7f);
                k.Box(p.Chimney, at + Vector3.up * 0.5f, s);
                k.Box(p.Trim, at + Vector3.up * 1.34f, s + new Vector3(0.1f, -1.5f, 0.1f));
                for (int i = 0; i < 3; i++)
                {
                    var o = alongX ? new Vector3(-0.2f + i * 0.2f, 0f, 0f) : new Vector3(0f, 0f, -0.2f + i * 0.2f);
                    k.Of(p.Terracotta).AddCylinder(at + o + Vector3.up * 1.39f, 0.055f, 0.3f, 8);
                }
            }
            foreach (float x in new[] { -9.5f, -3.6f, 3.2f }) Chimney(new Vector3(x, 21.5f, -12.05f), true);
            foreach (float x in new[] { -6.5f, -0.5f }) Chimney(new Vector3(x, 18.8f, -28.33f), true);
            foreach (float z in new[] { -17f, -24f }) Chimney(new Vector3(3.47f, 18.6f, z), false);
            foreach (float z in new[] { -18f, -24.5f }) Chimney(new Vector3(-10.07f, 18.8f, z), false);
            k.Bake(ex, "CourRoofs");
        }

        // ------------------------------------------------------------------ the fountain
        /// <summary>A round stone basin with a pedestal and an upper bowl: water rippling in both, a small jet, a ring of
        /// water spilling from the bowl's lip and dimpling the basin.</summary>
        static void Fountain(Transform ex, Pal p)
        {
            var root = new GameObject("Fountain").transform;
            root.SetParent(ex, false);
            root.localPosition = FountainAt;
            var k = new KitBatch();
            k.Of(p.Trim).AddLathe(Vector3.zero, new List<Vector2>
            {
                new Vector2(1.35f, 0f), new Vector2(1.35f, 0.4f), new Vector2(1.42f, 0.44f), new Vector2(1.42f, 0.52f), new Vector2(1.22f, 0.56f),
                new Vector2(1.15f, 0.5f), new Vector2(1.15f, 0.14f), new Vector2(0.6f, 0.12f), new Vector2(0.001f, 0.12f),
            }, 28, 1.5f);
            k.Of(p.Trim).AddLathe(Vector3.zero, new List<Vector2>
            {
                new Vector2(0.24f, 0.12f), new Vector2(0.2f, 0.26f), new Vector2(0.15f, 0.33f), new Vector2(0.14f, 0.85f), new Vector2(0.2f, 0.92f),
                new Vector2(0.45f, 1.02f), new Vector2(0.53f, 1.1f), new Vector2(0.53f, 1.15f), new Vector2(0.47f, 1.16f), new Vector2(0.44f, 1.1f),
                new Vector2(0.06f, 1.08f), new Vector2(0.001f, 1.08f),
            }, 20, 1f);
            k.Of(p.Brass).AddCylinder(new Vector3(0f, 1.06f, 0f), 0.026f, 0.22f, 8);
            k.Bake(root, "Basin");
            var water = WaterMat();
            var wb = new MeshBuilder();
            wb.AddDisc(new Vector3(0f, 0.4f, 0f), Vector3.up, 1.16f, 28);
            wb.AddDisc(new Vector3(0f, 1.12f, 0f), Vector3.up, 0.455f, 20);
            var wgo = MeshFactory.FromBuilder("Water", wb, water, root);
            wgo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (water.HasProperty("_BumpMap") && water.GetTexture("_BumpMap") != null) wgo.AddComponent<WaterDrift>();
            var cb = new MeshBuilder();
            cb.AddCylinder(Vector3.zero, 1.42f, 0.62f, 10);
            var col = new GameObject("FountainSolid");
            col.transform.SetParent(root, false);
            var mc = col.AddComponent<MeshCollider>();
            mc.sharedMesh = cb.ToMesh("FountainCollider");
            mc.convex = true;

            var jet = Particles(root, "Jet", new Vector3(0f, 1.28f, 0f), Quaternion.Euler(-90f, 0f, 0f), true, 0.55f);
            {
                var m = jet.main; m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(0.42f, 0.55f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(2.1f, 2.5f); m.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.034f);
                m.startColor = new Color(0.7f, 0.8f, 0.9f, 0.4f); m.gravityModifier = 1f; m.maxParticles = 160;
                m.simulationSpace = ParticleSystemSimulationSpace.Local;
                var e = jet.emission; e.rateOverTime = 110f;
                var s = jet.shape; s.shapeType = ParticleSystemShapeType.Cone; s.angle = 4f; s.radius = 0.008f;
            }
            var spill = Particles(root, "Spill", new Vector3(0f, 1.13f, 0f), Quaternion.Euler(-90f, 0f, 0f), true, 0.5f);
            {
                var m = spill.main; m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(0.36f, 0.4f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.22f, 0.38f); m.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.032f);
                m.startColor = new Color(0.66f, 0.76f, 0.86f, 0.32f); m.gravityModifier = 1f; m.maxParticles = 200;
                m.simulationSpace = ParticleSystemSimulationSpace.Local;
                var e = spill.emission; e.rateOverTime = 150f;
                var s = spill.shape; s.shapeType = ParticleSystemShapeType.Circle; s.radius = 0.53f; s.radiusThickness = 0f;
            }
            var dimple = Particles(root, "Dimples", new Vector3(0f, 0.41f, 0f), Quaternion.Euler(-90f, 0f, 0f), true, 0.45f);
            {
                var m = dimple.main; m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.28f);
                m.startSpeed = 0f; m.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
                m.startColor = new Color(0.8f, 0.86f, 0.92f, 0.35f); m.gravityModifier = 1f; m.maxParticles = 80;
                m.simulationSpace = ParticleSystemSimulationSpace.Local;
                var e = dimple.emission; e.rateOverTime = 60f;
                var s = dimple.shape; s.shapeType = ParticleSystemShapeType.Circle; s.radius = 0.62f; s.radiusThickness = 0.2f;
                var v = dimple.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
                v.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f); v.y = new ParticleSystem.MinMaxCurve(0.3f, 0.6f); v.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            }
        }

        static Material WaterMat()
        {
            var n = Tex.Normal("water_ripple");
            if (n == null) return Mats.Lit(new Color(0.05f, 0.07f, 0.08f), 0.94f);
            var m = new Material(Mats.Template("Lit_Normal")) { name = "FountainWater" };
            m.SetColor("_BaseColor", new Color(0.06f, 0.08f, 0.09f));
            m.SetTexture("_BumpMap", n);
            m.SetTextureScale("_BumpMap", new Vector2(1.6f, 1.6f));
            m.SetFloat("_BumpScale", 0.45f);
            m.SetFloat("_Smoothness", 0.94f);
            return m;
        }

        // ------------------------------------------------------------------ the café
        /// <summary>
        /// Le Petit Timbre, on the south side: a lit front of small panes over a red base, a bar and pendants inside, the
        /// name in gold, a striped awning whose valance ripples, iron arms, a chalkboard, four tables, a sign swinging on a
        /// bracket from the east wall, and the light that pools on the terrace.
        /// </summary>
        static void Cafe(Transform ex, Pal p)
        {
            var k = new KitBatch();
            var f = new Face(new Vector3(CXW, 0f, CZS), Vector3.forward); // u = x + 9.2
            const float u0 = 6.2f, u1 = 11.6f, uc = (u0 + u1) * 0.5f, w = u1 - u0;
            B(k, p.CafeRed, f, uc, 0.28f, 0.05f, w, 0.56f, 0.1f);
            B(k, p.CafeGlow, f, uc, 1.72f, 0.012f, w - 0.04f, 2.3f, 0.02f);
            B(k, p.Black, f, u0 + 1.05f, 0.95f, 0.03f, 1.6f, 0.6f, 0.02f);
            for (int i = 0; i < 9; i++)
                B(k, i % 3 == 0 ? p.Flower : i % 3 == 1 ? p.Leaf : p.Brass, f, u0 + 0.4f + i * 0.14f, 1.72f, 0.03f, 0.06f, 0.22f, 0.02f);
            B(k, p.Black, f, u0 + 1.05f, 1.6f, 0.03f, 1.5f, 0.03f, 0.02f);
            foreach (float pu in new[] { u0 + 1.2f, uc + 0.9f, u1 - 0.9f })
            {
                B(k, p.Black, f, pu, 2.62f, 0.03f, 0.01f, 0.36f, 0.01f);
                B(k, p.LampGlow, f, pu, 2.4f, 0.035f, 0.2f, 0.12f, 0.02f);
            }
            for (int i = 0; i <= 5; i++) B(k, p.CafeRed, f, u0 + i * w / 5f, 1.72f, 0.04f, 0.07f, 2.32f, 0.06f);
            B(k, p.CafeRed, f, uc, 2.3f, 0.04f, w, 0.06f, 0.06f);
            B(k, p.CafeRed, f, uc, 2.9f, 0.05f, w + 0.1f, 0.08f, 0.1f);
            B(k, p.CafeRed, f, uc, 3.27f, 0.07f, w + 0.2f, 0.62f, 0.14f);
            B(k, p.Brass, f, u0 + 2.5f * w / 5f, 1.1f, 0.075f, 0.7f, 0.03f, 0.03f); // the door's push bar
            Text(ex, "Le Petit Timbre", 0.22f, new Color(0.9f, 0.76f, 0.45f), f.P(uc, 3.27f, 0.145f), f.TextRot, w, true);
            Text(ex, "ouvert", 0.055f, new Color(0.95f, 0.9f, 0.75f), f.P(u0 + 2.5f * w / 5f, 1.62f, 0.03f), f.TextRot, 0.5f, true);
            // Awning: a striped canvas sloping off the fascia, iron arms, and a striped valance (cloth) along its edge.
            var stripesU = Stripes(new Color(0.78f, 0.16f, 0.13f), new Color(0.93f, 0.88f, 0.76f), true, 1f / 0.6f, 1f);
            var stripesV = Stripes(new Color(0.78f, 0.16f, 0.13f), new Color(0.93f, 0.88f, 0.76f), false, 1f, w / 0.6f);
            const float tilt = 16f, depth = 1.7f, top = 3.62f;
            float drop = Mathf.Sin(tilt * Mathf.Deg2Rad) * depth, reach = Mathf.Cos(tilt * Mathf.Deg2Rad) * depth;
            k.Box(stripesU, f.P(uc, top - drop * 0.5f, reach * 0.5f), new Vector3(w, 0.035f, depth), f.Q * Quaternion.Euler(tilt, 0f, 0f));
            foreach (float au in new[] { u0 + 0.3f, u1 - 0.3f })
                k.Of(p.Iron).AddTube(new List<Vector3> { f.P(au, 2.98f, 0.02f), f.P(au, top - drop - 0.02f, reach - 0.08f) }, 0.014f, 6);
            var valance = ClothFlag.Create(ex, "AwningValance", f.P(u0, top - drop, reach + 0.01f), 0.26f, w, stripesV, 0f, 0.035f);
            valance.transform.localRotation = Quaternion.LookRotation(Vector3.Cross(f.R, Vector3.down), -f.R);
            // Chalkboard easel by the door.
            var easel = new Vector3(-3.6f, 0f, -26.2f);
            var board = Mats.Lit(new Color(0.05f, 0.06f, 0.06f), 0.2f);
            k.Box(p.Wood, easel + new Vector3(0f, 0.5f, 0.12f), new Vector3(0.6f, 0.96f, 0.035f), Quaternion.Euler(-12f, 0f, 0f));
            k.Box(board, easel + new Vector3(0f, 0.52f, 0.137f), new Vector3(0.5f, 0.78f, 0.01f), Quaternion.Euler(-12f, 0f, 0f));
            k.Box(p.Wood, easel + new Vector3(0f, 0.5f, -0.12f), new Vector3(0.6f, 0.96f, 0.035f), Quaternion.Euler(12f, 0f, 0f));
            var chalk = Text(ex, "CE SOIR\n<size=70%>valse à minuit</size>", 0.07f, new Color(0.9f, 0.9f, 0.86f), easel + new Vector3(0f, 0.6f, 0.15f), Quaternion.Euler(-12f, 180f, 0f), 0.46f, true);
            chalk.alignment = TextAlignmentOptions.Center;
            k.Bake(ex, "CourCafe");
            // Tables (each adds its own small collider).
            var marble = Mats.Lit(new Color(0.8f, 0.78f, 0.72f), 0.6f, 0f, "marble_white");
            var wicker = Mats.Lit(new Color(0.55f, 0.4f, 0.22f), 0.3f, 0f, "fabric_linen");
            UrbanKit.CafeSet(ex, new Vector3(-2.1f, 0f, -24.7f), 10f, marble, p.Iron, wicker);
            UrbanKit.CafeSet(ex, new Vector3(-0.2f, 0f, -23.6f), -20f, marble, p.Iron, wicker);
            UrbanKit.CafeSet(ex, new Vector3(1.7f, 0f, -24.2f), 80f, marble, p.Iron, wicker);
            UrbanKit.CafeSet(ex, new Vector3(-1.3f, 0f, -21.9f), 35f, marble, p.Iron, wicker);
            MakeLight(ex, new Vector3(-0.3f, 2.5f, -26.2f), Warm, 1.6f, 7.5f);

            // The sign on its bracket from the east wall, swinging toward and away from the passage.
            var bk = new KitBatch();
            bk.Box(p.Iron, new Vector3(CXE - 0.45f, 3.78f, -24.9f), new Vector3(0.9f, 0.035f, 0.035f));
            bk.Of(p.Iron).AddTube(new List<Vector3> { new Vector3(CXE - 0.02f, 3.35f, -24.9f), new Vector3(CXE - 0.35f, 3.6f, -24.9f), new Vector3(CXE - 0.7f, 3.77f, -24.9f) }, 0.012f, 5);
            bk.Bake(ex, "CafeSignBracket");
            var pivot = new GameObject("CafeSign").transform;
            pivot.SetParent(ex, false);
            pivot.localPosition = new Vector3(CXE - 0.5f, 3.76f, -24.9f);
            var sk = new KitBatch();
            var signGreen = Mats.Lit(new Color(0.08f, 0.22f, 0.16f), 0.45f);
            foreach (float sx in new[] { -0.24f, 0.24f }) sk.Box(p.Iron, new Vector3(sx, -0.05f, 0f), new Vector3(0.008f, 0.1f, 0.008f));
            sk.Box(signGreen, new Vector3(0f, -0.31f, 0f), new Vector3(0.64f, 0.42f, 0.035f));
            sk.Box(p.Brass, new Vector3(0f, -0.31f, 0f), new Vector3(0.66f, 0.44f, 0.02f));
            sk.Bake(pivot, "Sign", false);
            Text(pivot, "CAFÉ", 0.12f, new Color(0.9f, 0.76f, 0.45f), new Vector3(0f, -0.31f, 0.02f), Quaternion.Euler(0f, 180f, 0f), 0.6f, true);
            Text(pivot, "CAFÉ", 0.12f, new Color(0.9f, 0.76f, 0.45f), new Vector3(0f, -0.31f, -0.02f), Quaternion.identity, 0.6f, true);
            WindSway.Add(pivot, 7f, 2.4f, Vector3.right);
        }

        /// <summary>A two-colour stripe texture on a lit material: stripes across u (canvas boxes) or across v (cloth).</summary>
        static Material Stripes(Color a, Color b, bool acrossU, float tileU, float tileV)
        {
            int wpx = acrossU ? 64 : 4, hpx = acrossU ? 4 : 64;
            var tex = new Texture2D(wpx, hpx, TextureFormat.RGBA32, true) { name = "Stripes", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color32[wpx * hpx];
            for (int y = 0; y < hpx; y++)
                for (int x = 0; x < wpx; x++)
                {
                    int i = acrossU ? x : y;
                    bool first = acrossU ? i < 32 : i >= 32; // the cloth runs the other way: keep its first stripe matching the canvas
                    px[y * wpx + x] = first ? (Color32)a : (Color32)b;
                }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            var m = new Material(Mats.Lit(Color.white, 0.25f)) { name = "Stripes" };
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", new Vector2(tileU, tileV));
            return m;
        }

        // ------------------------------------------------------------------ plants
        /// <summary>Four potted trees (two at the passage mouth, two along the west wall); their crowns sway a little.</summary>
        static void Plants(Transform ex, Pal p)
        {
            var k = new KitBatch();
            int seed = 0;
            foreach (var at in new[] { new Vector3(-2.3f, 0f, -14.3f), new Vector3(2.25f, 0f, -14.3f), new Vector3(-8.45f, 0f, -16.0f), new Vector3(-8.45f, 0f, -23.6f) })
            {
                var rnd = new System.Random(77 + seed++);
                k.Of(p.Terracotta).AddFrustum(at, 0.34f, 0.42f, 0.6f, 14);
                k.Of(p.Terracotta).AddCylinder(at + Vector3.up * 0.56f, 0.45f, 0.08f, 14);
                k.Of(p.Soil).AddDisc(at + Vector3.up * 0.62f, Vector3.up, 0.4f, 14);
                k.Of(p.Bark).AddTube(new List<Vector3> { at + Vector3.up * 0.6f, at + new Vector3(0.04f, 1.1f, -0.02f), at + new Vector3(-0.02f, 1.55f, 0.02f) }, 0.045f, 6, false);
                var crown = new GameObject("Crown").transform;
                crown.SetParent(ex, false);
                crown.localPosition = at + Vector3.up * 1.5f;
                var cb = new MeshBuilder();
                for (int i = 0; i < 7; i++)
                {
                    float a = i * 2.3f, r = 0.12f + (i % 3) * 0.14f;
                    cb.AddSphere(new Vector3(Mathf.Cos(a) * r, 0.1f + (i % 4) * 0.16f + (float)rnd.NextDouble() * 0.08f, Mathf.Sin(a) * r), 0.3f + (float)rnd.NextDouble() * 0.12f, 9, 6);
                }
                MeshFactory.FromBuilder("Leaves", cb, p.Leaf, crown);
                WindSway.Add(crown, 2.2f, 3.4f);
                var c = new GameObject("PotSolid");
                c.transform.SetParent(ex, false);
                c.transform.localPosition = at + Vector3.up * 0.5f;
                var cap = c.AddComponent<CapsuleCollider>();
                cap.radius = 0.45f; cap.height = 1.0f;
            }
            k.Bake(ex, "CourPots");
        }

        // ------------------------------------------------------------------ laundry
        /// <summary>Three washing lines strung between windows on pulleys, with sheets and shirts that ripple in the wind.</summary>
        static void Laundry(Transform ex, Pal p)
        {
            var k = new KitBatch();
            var linen = new[]
            {
                Mats.Lit(new Color(0.92f, 0.91f, 0.88f), 0.25f, 0f, "fabric_linen"), Mats.Lit(new Color(0.62f, 0.72f, 0.86f), 0.25f, 0f, "fabric_linen"),
                Mats.Lit(new Color(0.88f, 0.66f, 0.64f), 0.25f, 0f, "fabric_linen"), Mats.Lit(new Color(0.24f, 0.28f, 0.46f), 0.25f, 0f, "fabric_linen"),
                Mats.Lit(new Color(0.9f, 0.85f, 0.62f), 0.25f, 0f, "fabric_linen"),
            };
            void Line(Vector3 a, Vector3 b, float sag, (float t, float len, float drop, int mat)[] items)
            {
                var pts = new List<Vector3>();
                for (int i = 0; i <= 14; i++) { float t = i / 14f; pts.Add(Vector3.Lerp(a, b, t) + Vector3.down * sag * 4f * t * (1f - t)); }
                k.Of(p.Iron).AddTube(pts, 0.008f, 4);
                foreach (var end in new[] { a, b })
                {
                    k.Of(p.Iron).AddTorus(end, 0.06f, 0.012f, 12, 4, Quaternion.Euler(0f, 0f, 90f));
                    k.Box(p.Iron, end + Vector3.up * 0.08f, new Vector3(0.03f, 0.16f, 0.03f));
                }
                var flat = b - a; flat.y = 0f;
                var dir = flat.normalized;
                float length = (b - a).magnitude;
                foreach (var (t, len, drop, mat) in items)
                {
                    float tt = Mathf.Clamp01(t);
                    var top = Vector3.Lerp(a, b, tt) + Vector3.down * (sag * 4f * tt * (1f - tt) + 0.02f);
                    var cloth = ClothFlag.Create(ex, "Laundry", top, drop, len, linen[mat % linen.Length], 0f, 0.07f);
                    cloth.transform.localRotation = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.down), -dir);
                    float t2 = Mathf.Clamp01(tt + len / length);
                    foreach (float pt in new[] { tt, t2 })
                        k.Box(p.Wood, Vector3.Lerp(a, b, pt) + Vector3.down * (sag * 4f * pt * (1f - pt) + 0.01f), new Vector3(0.015f, 0.06f, 0.015f));
                }
            }
            // Anchors sit on the wall between windows (and clear of open shutters).
            Line(new Vector3(CXW + 0.06f, 8.15f, -16.6f), new Vector3(-4.2f, 8.15f, CZN - 0.06f), 0.22f,
                new[] { (0.1f, 1.0f, 1.25f, 0), (0.45f, 0.5f, 0.68f, 1), (0.7f, 0.45f, 0.6f, 2) });
            Line(new Vector3(CXW + 0.06f, 11.05f, -22.6f), new Vector3(-3.6f, 11.05f, CZS + 0.06f), 0.3f,
                new[] { (0.08f, 0.55f, 0.72f, 3), (0.24f, 1.1f, 1.3f, 0), (0.5f, 0.5f, 0.6f, 4), (0.68f, 0.9f, 1.1f, 1) });
            Line(new Vector3(CXW + 0.06f, 13.95f, -19.4f), new Vector3(CXE - 0.06f, 13.95f, -18.2f), 0.45f,
                new[] { (0.12f, 1.2f, 1.4f, 0), (0.25f, 0.55f, 0.7f, 2), (0.4f, 1.0f, 1.2f, 4), (0.57f, 0.5f, 0.7f, 3), (0.72f, 1.2f, 1.3f, 1) });
            k.Bake(ex, "CourLaundryLines", false);
        }

        // ------------------------------------------------------------------ the street organ
        /// <summary>
        /// A barrel organ on a two-wheeled cart against the east wall, its pipes to the courtyard and its crank to the
        /// passage: lacquered case with brass trim, nine pipes in a gilt frame, a stack of punched music on top. Turning the
        /// crank (<see cref="ParisStreetOrgan"/>) plays a short waltz whose last note sags flat.
        /// </summary>
        static void Organ(Transform ex, Pal p)
        {
            var root = new GameObject("StreetOrgan").transform;
            root.SetParent(ex, false);
            root.localPosition = OrganAt;
            root.localRotation = Quaternion.Euler(0f, -90f, 0f); // front (+Z) to the courtyard, crank (+X) toward the passage
            var k = new KitBatch();
            var gold = Mats.Lit(new Color(0.85f, 0.66f, 0.3f), 0.7f, 1f);
            var wheelAxis = Quaternion.Euler(0f, 0f, 90f);
            foreach (float x in new[] { -0.48f, 0.48f })
            {
                var c = new Vector3(x, 0.3f, 0f);
                k.Of(p.Wood).AddTorus(c, 0.28f, 0.03f, 20, 6, wheelAxis);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 22.5f;
                    k.Box(p.Wood, c, new Vector3(0.02f, 0.54f, 0.02f), Quaternion.Euler(a, 0f, 0f));
                }
                k.Of(p.Iron).AddCylinder(c + new Vector3(x > 0 ? 0.05f : -0.01f, 0f, 0f), 0.05f, 0.06f, 10, 1f, wheelAxis);
            }
            k.Of(p.Iron).AddCylinder(new Vector3(0.5f, 0.3f, 0f), 0.015f, 1.0f, 6, 1f, wheelAxis);
            k.Box(p.Wood, new Vector3(0f, 0.6f, 0f), new Vector3(0.9f, 0.05f, 0.64f));
            foreach (float x in new[] { -0.36f, 0.36f })
                k.Of(p.Iron).AddTube(new List<Vector3> { new Vector3(x, 0.6f, -0.3f), new Vector3(x, 0.66f, -0.62f), new Vector3(x, 0.78f, -0.82f) }, 0.016f, 6);
            k.Of(p.Iron).AddTube(new List<Vector3> { new Vector3(0f, 0.58f, 0.27f), new Vector3(0f, 0f, 0.27f) }, 0.02f, 6);
            // The case, its gilt trim and the pipe front.
            k.Box(p.Lacquer, new Vector3(0f, 0.93f, 0f), new Vector3(0.82f, 0.6f, 0.48f));
            foreach (float y in new[] { 0.64f, 1.22f }) k.Box(gold, new Vector3(0f, y, 0f), new Vector3(0.84f, 0.03f, 0.5f));
            foreach (float x in new[] { -0.41f, 0.41f }) foreach (float z in new[] { -0.24f, 0.24f }) k.Box(gold, new Vector3(x, 0.93f, z), new Vector3(0.03f, 0.6f, 0.03f));
            k.Box(p.Black, new Vector3(0f, 0.97f, 0.242f), new Vector3(0.62f, 0.38f, 0.01f));
            for (int i = 0; i < 9; i++)
            {
                float x = -0.24f + i * 0.06f, h = 0.2f + 0.14f * (1f - Mathf.Abs(i - 4) / 4f);
                k.Of(p.Brass).AddCylinder(new Vector3(x, 0.8f, 0.262f), 0.022f + 0.008f * (1f - Mathf.Abs(i - 4) / 4f), h, 10);
                k.Box(p.Black, new Vector3(x, 0.86f, 0.288f), new Vector3(0.026f, 0.02f, 0.006f));
            }
            k.Box(gold, new Vector3(0f, 1.17f, 0.25f), new Vector3(0.66f, 0.04f, 0.03f));
            k.Box(gold, new Vector3(0f, 0.78f, 0.25f), new Vector3(0.66f, 0.03f, 0.03f));
            foreach (float x in new[] { -0.32f, 0.32f }) k.Box(gold, new Vector3(x, 0.97f, 0.25f), new Vector3(0.03f, 0.42f, 0.03f));
            // A tray of folded, punched music on top.
            k.Box(p.Wood, new Vector3(0f, 1.25f, -0.02f), new Vector3(0.5f, 0.03f, 0.38f));
            for (int i = 0; i < 6; i++) k.Box(p.Card, new Vector3((i % 2) * 0.01f, 1.272f + i * 0.012f, -0.02f), new Vector3(0.42f, 0.01f, 0.3f));
            k.Bake(root, "Organ");
            Text(root, "ORGUE", 0.045f, new Color(0.9f, 0.76f, 0.45f), new Vector3(0f, 0.7f, 0.247f), Quaternion.Euler(0f, 180f, 0f), 0.5f, true);

            // The crank (turns about the case's side axis) and the music strip that feeds out of the back while it plays.
            var crank = new GameObject("Crank").transform;
            crank.SetParent(root, false);
            crank.localPosition = new Vector3(0.43f, 0.98f, 0f);
            var ck = new KitBatch();
            ck.Of(p.Iron).AddCylinder(new Vector3(0.05f, 0f, 0f), 0.04f, 0.06f, 10, 1f, wheelAxis);
            ck.Box(p.Iron, new Vector3(0.05f, 0.1f, 0f), new Vector3(0.025f, 0.22f, 0.035f));
            ck.Of(p.Wood).AddCylinder(new Vector3(0.15f, 0.2f, 0f), 0.02f, 0.1f, 8, 1f, wheelAxis);
            ck.Bake(crank, "Crank", false);
            var book = new GameObject("MusicStrip").transform;
            book.SetParent(root, false);
            book.localPosition = new Vector3(0f, 1.0f, -0.245f);
            book.localScale = new Vector3(1f, 1f, 0.05f);
            var bkb = new KitBatch();
            bkb.Box(p.Card, new Vector3(0f, 0f, -0.16f), new Vector3(0.38f, 0.008f, 0.32f));
            bkb.Bake(book, "Strip", false);

            var motes = Particles(root, "Notes", new Vector3(0f, 1.08f, 0.3f), Quaternion.identity, true, 1.4f);
            {
                var m = motes.main; m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.2f); m.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.045f);
                m.startColor = new Color(1f, 0.84f, 0.5f, 0.9f); m.gravityModifier = -0.02f; m.maxParticles = 60;
                m.simulationSpace = ParticleSystemSimulationSpace.World;
                var e = motes.emission; e.rateOverTime = 0f;
                var s = motes.shape; s.shapeType = ParticleSystemShapeType.Box; s.scale = new Vector3(0.5f, 0.2f, 0.05f);
                var col = motes.colorOverLifetime; col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
            }

            var solid = new GameObject("OrganSolid");
            solid.transform.SetParent(root, false);
            solid.transform.localPosition = new Vector3(0f, 0.63f, -0.27f);
            solid.AddComponent<BoxCollider>().size = new Vector3(1.08f, 1.26f, 1.16f);
            var use = new GameObject("OrganCrank");
            use.transform.SetParent(root, false);
            use.transform.localPosition = new Vector3(0f, 0.7f, -0.25f);
            var trig = use.AddComponent<BoxCollider>();
            trig.size = new Vector3(1.3f, 1.45f, 1.32f);
            trig.isTrigger = true;
            var organ = use.AddComponent<ParisStreetOrgan>();
            organ.Crank = crank;
            organ.Strip = book;
            organ.Notes = motes;
            organ.MarkerPoint = crank;
        }

        // ------------------------------------------------------------------ life, light, postcards
        static void Life(Transform ex)
        {
            AmbientBirds.Create(ex, new Vector3(-0.8f, 0f, -19.2f), 1.7f, 7, AmbientBirds.Kind.Pigeon); // courtyard, clear of the fountain
            AmbientBirds.Create(ex, new Vector3(1.6f, 0f, -8.7f), 1.2f, 5, AmbientBirds.Kind.Pigeon);   // the street, in front of the arch
            AmbientBirds.Create(ex, new Vector3(5.9f, -0.6f, 19.4f), 1.0f, 4, AmbientBirds.Kind.Gull);  // the lower quay by the barge
        }

        static void Lamps(Transform ex)
        {
            var c = new Color(1f, 0.78f, 0.5f);
            SwitchLamp.Create(ex, new Vector3(-8.2f, 0f, -14.2f), c, false, 2.6f);   // courtyard, north-west corner
            SwitchLamp.Create(ex, new Vector3(-7.0f, 0f, -25.2f), c, false, 2.6f);   // courtyard, south-west
            SwitchLamp.Create(ex, new Vector3(17.5f, 4.6f, 35.6f), c, false, 2.6f);  // the bridge, a dark stretch on the south side
            SwitchLamp.Create(ex, new Vector3(44.0f, 4.6f, 40.45f), c, false, 2.6f); // the bridge, north side before the far bank
        }

        static void Postcards(Transform ex)
        {
            Postcard.Create(ex, CountryId.France, 1, new Vector3(1.25f, 1.1f, -16.8f),
                "The organ in the courtyard plays one waltz. My mother hummed it. I never told her.");
            Postcard.Create(ex, CountryId.France, 2, new Vector3(-8.4f, 1.1f, -26.6f),
                "Counted my stamps by lamplight: eleven. The twelfth page was warm, like a hand.");
            Postcard.Create(ex, CountryId.France, 3, new Vector3(7.0f, 0.5f, 11.1f),
                "Rain all week. Something keeps stamping my passport while I sleep.");
        }

        // ------------------------------------------------------------------ helpers
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

        static ParticleSystem Particles(Transform parent, string name, Vector3 pos, Quaternion rot, bool additive, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            var ps = go.AddComponent<ParticleSystem>();
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = Mats.Particle(Tex.SoftDot, additive, Color.white, intensity, Mats.StencilNotEqual);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }
    }

    /// <summary>
    /// The courtyard's street organ (Paris): turn the crank and it wheezes out a short waltz, the crank spinning, the punched
    /// music feeding out of the back, gold motes lifting off the pipes with each note. The last note sags a little flat.
    /// Plays again once it has stopped. Never blocks anything.
    /// </summary>
    public class ParisStreetOrgan : Interactable
    {
        public Transform Crank, Strip;
        public ParticleSystem Notes;

        // A waltz in G (semitones over the clip's G): three bars up, three bars home.
        static readonly int[] Tune = { 4, 7, 7, 9, 7, 4, 2, 4, 5, 4, 2, 0, 7, 5, 2, 4, 2, 0 };
        const float Beat = 0.38f;

        Cell m_Cell;
        bool m_Playing;
        float m_Speed, m_Target, m_Feed;

        void Start()
        {
            m_Cell = GetComponentInParent<Cell>();
            Prompt = "Turn the organ's crank";
        }

        bool Live => m_Cell == null || m_Cell.Mode == CellMode.Active;
        Vector3 SoundAt => Notes != null ? Notes.transform.position : transform.position;

        // Hidden mid-tune (the player left the cell): the coroutine is gone, so let it be played again on return.
        void OnDisable()
        {
            m_Playing = false;
            m_Target = 0f;
            m_Speed = 0f;
        }

        public override bool CanUse(Interactor who) => base.CanUse(who) && !m_Playing;

        public override string GetPrompt(Interactor who) => "Turn the organ's crank";

        public override void Use(Interactor who)
        {
            if (!CanUse(who)) return;
            base.Use(who);
            StartCoroutine(Play(who));
        }

        IEnumerator Play(Interactor who)
        {
            m_Playing = true;
            m_Target = 320f;
            m_Feed = 0f;
            if (Live)
            {
                Hud.I?.ShowCaption("the street organ wheezes into a little waltz", 3f);
                AudioDirector.I?.Play("dial_click", SoundAt, 0.5f, 0.7f);
                AudioDirector.Haptic(who != null ? who.Side : HandSide.Desktop, 0.25f, 0.08f);
            }
            yield return new WaitForSeconds(0.3f);
            for (int i = 0; i < Tune.Length; i++)
            {
                bool last = i == Tune.Length - 1;
                if (Live)
                {
                    float pitch = Mathf.Pow(2f, Tune[i] / 12f) * (last ? 0.955f : 1f);
                    AudioDirector.I?.Play("ui_confirm", SoundAt, last ? 0.5f : 0.4f, pitch);
                    if (i % 3 == 0) AudioDirector.I?.Play("clock_tick", SoundAt, 0.28f, 0.62f);
                    if (Notes != null) Notes.Emit(last ? 7 : 2);
                }
                m_Feed = (i + 1f) / Tune.Length;
                yield return new WaitForSeconds(last ? Beat * 3f : Beat);
            }
            m_Target = 0f;
            yield return new WaitForSeconds(1.1f);
            m_Playing = false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            m_Speed = Mathf.MoveTowards(m_Speed, m_Target, dt * (m_Target > m_Speed ? 900f : 280f));
            if (Crank != null && m_Speed > 0.5f) Crank.localRotation *= Quaternion.AngleAxis(m_Speed * dt, Vector3.right);
            if (Strip != null)
            {
                float want = m_Playing ? Mathf.Lerp(0.05f, 1f, m_Feed) : 0.05f;
                var s = Strip.localScale;
                s.z = Mathf.MoveTowards(s.z, want, dt * (m_Playing ? 0.6f : 0.25f));
                Strip.localScale = s;
            }
        }
    }
}

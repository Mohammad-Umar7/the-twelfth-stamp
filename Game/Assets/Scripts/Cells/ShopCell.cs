using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// The second-hand camera and optics shop in Marrakech. ShopNormal: warm evening prologue with an ordinary,
    /// friendly shopkeeper. ShopCorrupt: the Margin copy for the Morocco encounter — same counter and layout, but the
    /// shelves repeat into fog and the keeper is gone.
    /// </summary>
    public partial class ShopCell : Cell
    {
        public bool Corrupted;
        public Transform CounterTop, DoorPoint, DisplaySpot, TraySpot, ShopkeeperSpot, DrawerRoot;
        public Shopkeeper Keeper;
        public Light Lamp;
        public readonly List<Transform> CounterMarks = new List<Transform>();
        public TraceMark Trace;

        protected override void Build()
        {
            Lighting = Corrupted ? new LightingPreset
            {
                // Cool teal depths (the aisles that should not exist fade into them) against the warm counter lamp; the
                // shelves and counter stay legible under the corruption.
                SunColor = new Color(0.5f, 0.62f, 0.66f), SunIntensity = 0.3f, SunEuler = new Vector3(50f, 20f, 0f), ShadowStrength = 0.45f,
                AmbientSky = new Color(0.22f, 0.29f, 0.3f), AmbientEquator = new Color(0.17f, 0.2f, 0.2f), AmbientGround = new Color(0.08f, 0.09f, 0.09f),
                FogColor = new Color(0.12f, 0.17f, 0.17f), FogDensity = 0.04f,
                SkyZenith = new Color(0.02f, 0.04f, 0.05f), SkyHorizon = new Color(0.08f, 0.12f, 0.12f), SkyGround = new Color(0.03f, 0.04f, 0.04f),
                SunDiscColor = new Color(0, 0, 0, 0),
                PostExposure = 0.3f, Saturation = -30f, Contrast = 14f, ColorFilter = new Color(0.82f, 0.98f, 0.95f), Bloom = 0.6f, Vignette = 0.34f
            } : new LightingPreset
            {
                SunColor = new Color(1f, 0.7f, 0.45f), SunIntensity = 1.1f, SunEuler = new Vector3(18f, 300f, 0f), ShadowStrength = 0.8f,
                AmbientSky = new Color(0.45f, 0.42f, 0.48f), AmbientEquator = new Color(0.42f, 0.3f, 0.22f), AmbientGround = new Color(0.2f, 0.13f, 0.09f),
                FogColor = new Color(0.62f, 0.45f, 0.35f), FogDensity = 0.012f,
                SkyZenith = new Color(0.22f, 0.3f, 0.52f), SkyHorizon = new Color(0.98f, 0.66f, 0.42f), SkyGround = new Color(0.4f, 0.28f, 0.2f),
                SunDiscColor = new Color(1f, 0.7f, 0.45f, 1f), SunSize = 0.006f, CloudColor = new Color(1f, 0.75f, 0.6f, 0.3f),
                PostExposure = 0.05f, Saturation = 8f, Contrast = 8f, ColorFilter = new Color(1f, 0.95f, 0.88f), Bloom = 0.55f, Vignette = 0.2f
            };
            Ambience = Corrupted ? "amb_margin" : "amb_shop";
            AddSun();

            var terracotta = Mats.Lit(Corrupted ? new Color(0.8f, 0.78f, 0.76f) : new Color(1f, 0.86f, 0.76f), 0.2f, 0f, "plaster_terracotta");
            var zellige = Mats.Lit(Corrupted ? new Color(0.72f, 0.78f, 0.76f) : Color.white, 0.6f, 0f, "tile_zellige");
            var wood = Mats.Lit(new Color(1f, 0.9f, 0.82f), 0.45f, 0f, "wood_dark");
            var brass = Mats.Lit(new Color(0.84f, 0.64f, 0.34f), 0.68f, 0.9f);
            var glass = Mats.Glass(new Color(0.75f, 0.85f, 0.85f, 0.18f));
            var paving = Mats.Lit(new Color(0.7f, 0.6f, 0.5f), 0.3f, 0f, "plaza_stone");

            var g = Group("Shop");
            // Street outside (prologue start).
            MeshFactory.Floor("Lane", g, new Vector3(0, 0, -6.5f), new Vector2(3.4f, 7.5f), paving, "stone", 0.2f, 1f);
            MeshFactory.Box("LaneWallL", g, new Vector3(-1.85f, 2.5f, -6.5f), new Vector3(0.3f, 5f, 7.5f), terracotta, true, 1.4f);
            MeshFactory.Box("LaneWallR", g, new Vector3(1.85f, 2.5f, -6.5f), new Vector3(0.3f, 5f, 7.5f), terracotta, true, 1.4f);
            MeshFactory.Box("LaneTileL", g, new Vector3(-1.68f, 0.45f, -6.5f), new Vector3(0.04f, 0.9f, 7.5f), zellige, false, 0.6f);
            MeshFactory.Box("LaneTileR", g, new Vector3(1.68f, 0.45f, -6.5f), new Vector3(0.04f, 0.9f, 7.5f), zellige, false, 0.6f);
            // The remembered version opens onto the souk (Detail_Souk.cs); the Margin copy stays a dead end.
            if (Corrupted) MeshFactory.Box("LaneEnd", g, new Vector3(0, 2.5f, -10.3f), new Vector3(3.8f, 5f, 0.3f), terracotta, true, 1.4f);
            else BuildSouk(g);
            // Covered souk roof: timber beams under a mat of reeds, with thin gaps of evening sky between them.
            var timber = Mats.Lit(new Color(0.92f, 0.84f, 0.76f), 0.35f, 0f, "wood_dark");
            var reed = Mats.Lit(new Color(1f, 0.95f, 0.82f), 0.15f, 0f, "wood_oak_floor");
            for (int i = 0; i < 7; i++)
                MeshFactory.Box("RoofBeam", g, new Vector3(0, 4.32f, -9.9f + i * 1.1f), new Vector3(3.7f, 0.14f, 0.16f), timber, false);
            for (int i = 0; i < 20; i++)
                MeshFactory.Box("RoofReed", g, new Vector3(-1.62f + i * 0.17f, 4.43f, -6.6f), new Vector3(0.11f, 0.05f, 7.3f), reed, false, 1f, Quaternion.Euler(0f, 0f, (i % 3 - 1) * 4f));
            MeshFactory.Box("RoofEdgeL", g, new Vector3(-1.68f, 4.38f, -6.6f), new Vector3(0.08f, 0.2f, 7.4f), timber, false);
            MeshFactory.Box("RoofEdgeR", g, new Vector3(1.68f, 4.38f, -6.6f), new Vector3(0.08f, 0.2f, 7.4f), timber, false);
            // A cloth awning over the shop door.
            MeshFactory.Box("Awning", g, new Vector3(0, 3.62f, -3.45f), new Vector3(1.9f, 0.04f, 0.7f), Mats.Lit(Corrupted ? new Color(0.35f, 0.38f, 0.37f) : new Color(0.55f, 0.16f, 0.12f), 0.2f, 0f, "fabric_linen"), false, 1f, Quaternion.Euler(-14f, 0f, 0f));
            var lantern = MeshFactory.Frustum("StreetLantern", g, new Vector3(0, 3.1f, -6.2f), 0.18f, 0.1f, 0.38f, Mats.Emissive(new Color(0.4f, 0.3f, 0.15f), Corrupted ? new Color(0.3f, 0.5f, 0.45f) : new Color(1.6f, 0.9f, 0.4f)), 8);
            AddPointLight(g, new Vector3(0, 2.9f, -6.2f), Corrupted ? new Color(0.6f, 0.85f, 0.8f) : new Color(1f, 0.7f, 0.4f), 1.6f, 6f);

            // Shop front with door opening.
            MeshFactory.Box("FrontL", g, new Vector3(-1.25f, 2.5f, -3f), new Vector3(1.4f, 5f, 0.3f), terracotta, true, 1.4f);
            MeshFactory.Box("FrontR", g, new Vector3(1.25f, 2.5f, -3f), new Vector3(1.4f, 5f, 0.3f), terracotta, true, 1.4f);
            MeshFactory.Box("FrontTop", g, new Vector3(0, 3.9f, -3f), new Vector3(1.2f, 2.2f, 0.3f), terracotta, true, 1.4f);
            MeshFactory.Box("DoorFrame", g, new Vector3(0, 2.82f, -3.16f), new Vector3(1.3f, 0.1f, 0.06f), wood, false);
            MeshFactory.Box("SignBoard", g, new Vector3(0, 3.3f, -3.18f), new Vector3(1.6f, 0.36f, 0.05f), wood, false);
            var sign = TextFactory.World(g, Corrupted ? "Cameras  &  Optics  &  Cameras  &" : "Cameras  &  Optics", 0.11f, new Color(0.95f, 0.85f, 0.6f), TMPro.TextAlignmentOptions.Center, true, 1.6f);
            sign.transform.localPosition = new Vector3(0, 3.3f, -3.215f);
            DoorPoint = new GameObject("DoorPoint").transform;
            DoorPoint.SetParent(g, false);
            DoorPoint.localPosition = new Vector3(0, 0, -3f);

            // Interior.
            MeshFactory.Floor("ShopFloor", g, new Vector3(0, 0.01f, -1f), new Vector2(5f, 4.2f), zellige, "tile", 0.2f, 0.8f);
            MeshFactory.Box("Ceiling", g, new Vector3(0, 3.0f, -1f), new Vector3(5f, 0.1f, 4.2f), wood, false, 1.5f);
            MeshFactory.Box("BackWall", g, new Vector3(0, 1.5f, 1.15f), new Vector3(5f, 3f, 0.2f), terracotta, true, 1.4f);
            if (!Corrupted)
            {
                MeshFactory.Box("SideWallL", g, new Vector3(-2.55f, 1.5f, -1f), new Vector3(0.2f, 3f, 4.2f), terracotta, true, 1.4f);
                MeshFactory.Box("SideWallR", g, new Vector3(2.55f, 1.5f, -1f), new Vector3(0.2f, 3f, 4.2f), terracotta, true, 1.4f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Shelves(g, new Vector3(side * 2.25f, 0f, -1f), side, wood, brass, 3);
                    MemoryReveal.Attach(g.GetChild(g.childCount - 1).gameObject, 6.5f, 0.15f + side * 0.1f, 2.7f); // rises as you step in
                }
            }
            else
            {
                // The shelves repeat impossibly: aisles stretch away into fog on both sides.
                for (int side = -1; side <= 1; side += 2)
                    for (int k = 0; k < 12; k++)
                        Shelves(g, new Vector3(side * (2.25f + k * 1.7f), 0f, -1f), side, wood, brass, 3);
                MeshFactory.Floor("AisleFloor", g, new Vector3(0, 0.005f, -1f), new Vector2(46f, 4.2f), zellige, "tile", 0.2f, 0.8f);
                MeshFactory.Box("AisleCeiling", g, new Vector3(0, 3.0f, -1f), new Vector3(46f, 0.1f, 4.2f), wood, false, 1.5f);
                MeshFactory.Box("AisleBackL", g, new Vector3(-12f, 1.5f, 1.15f), new Vector3(19f, 3f, 0.2f), terracotta, true, 1.4f);
                MeshFactory.Box("AisleBackR", g, new Vector3(12f, 1.5f, 1.15f), new Vector3(19f, 3f, 0.2f), terracotta, true, 1.4f);
                MeshFactory.Box("AisleFrontL", g, new Vector3(-12f, 1.5f, -3.1f), new Vector3(19f, 3f, 0.2f), terracotta, true, 1.4f);
                MeshFactory.Box("AisleFrontR", g, new Vector3(12f, 1.5f, -3.1f), new Vector3(19f, 3f, 0.2f), terracotta, true, 1.4f);
            }

            // Counter with display case, brass tray and drawer.
            MeshFactory.Box("Counter", g, new Vector3(0, 0.5f, 0.15f), new Vector3(2.6f, 1f, 0.6f), wood, true);
            MeshFactory.Box("CounterTop", g, new Vector3(0, 1.02f, 0.15f), new Vector3(2.7f, 0.04f, 0.66f), wood, false);
            var display = MeshFactory.Box("DisplayGlass", g, new Vector3(-0.6f, 1.16f, 0.2f), new Vector3(0.7f, 0.24f, 0.4f), glass, false);
            display.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            CounterTop = new GameObject("CounterTop").transform;
            CounterTop.SetParent(g, false);
            CounterTop.localPosition = new Vector3(0, 1.04f, 0.05f);
            DisplaySpot = new GameObject("DisplaySpot").transform;
            DisplaySpot.SetParent(g, false);
            DisplaySpot.localPosition = new Vector3(-0.6f, 1.06f, 0.15f);
            TraySpot = new GameObject("TraySpot").transform;
            TraySpot.SetParent(g, false);
            TraySpot.localPosition = new Vector3(0.45f, 1.045f, -0.05f);
            MeshFactory.Cylinder("Tray", g, TraySpot.localPosition - Vector3.up * 0.005f, 0.09f, 0.012f, brass, 24, false);
            DrawerRoot = new GameObject("Drawer").transform;
            DrawerRoot.SetParent(g, false);
            DrawerRoot.localPosition = new Vector3(0.6f, 0.8f, -0.12f);
            MeshFactory.Box("DrawerFront", DrawerRoot, Vector3.zero, new Vector3(0.6f, 0.18f, 0.04f), wood, false);
            MeshFactory.Box("DrawerKnob", DrawerRoot, new Vector3(0, 0, -0.03f), new Vector3(0.08f, 0.03f, 0.03f), brass, false);

            BuildBackWall(g, wood, brass);
            if (Corrupted) BuildAisleLamps(g);

            // Hanging lamp and ceiling fan.
            var lampShade = MeshFactory.Frustum("LampShade", g, new Vector3(0, 2.4f, -0.4f), 0.3f, 0.12f, 0.25f, Mats.Emissive(new Color(0.3f, 0.22f, 0.12f), new Color(1.2f, 0.7f, 0.35f)), 10);
            lampShade.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // The counter lamp stays warm in the Margin copy too: the one familiar light in the cool aisles.
            Lamp = AddPointLight(g, new Vector3(0, 2.3f, -0.4f), Corrupted ? new Color(1f, 0.76f, 0.5f) : new Color(1f, 0.72f, 0.42f), Corrupted ? 2.0f : 2.4f, 6f, true);
            var fan = new GameObject("CeilingFan").transform;
            fan.SetParent(g, false);
            fan.localPosition = new Vector3(0, 2.85f, -1.6f);
            for (int i = 0; i < 4; i++) MeshFactory.Box("Blade", fan, Quaternion.Euler(0, i * 90f, 0) * new Vector3(0, 0, 0.35f), new Vector3(0.12f, 0.01f, 0.6f), wood, false, 1f, Quaternion.Euler(0, i * 90f, 0));
            fan.gameObject.AddComponent<Spin>().DegreesPerSecond = Corrupted ? 6f : 70f;

            ShopkeeperSpot = new GameObject("ShopkeeperSpot").transform;
            ShopkeeperSpot.SetParent(g, false);
            ShopkeeperSpot.localPosition = new Vector3(0.55f, 0f, 0.72f);
            if (!Corrupted)
            {
                Keeper = Shopkeeper.Create(g);
                Keeper.transform.localPosition = ShopkeeperSpot.localPosition;
                Keeper.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            }
            else
            {
                // The counter remembers where things were: faint outlines for the reconstruction puzzle.
                foreach (var p in new[] { new Vector3(-0.85f, 1.045f, -0.02f), new Vector3(0.45f, 1.045f, -0.05f), new Vector3(1.05f, 1.045f, 0.02f) })
                {
                    var m = new GameObject("CounterMark").transform;
                    m.SetParent(g, false);
                    m.localPosition = p;
                    MeshFactory.Quad("Outline", m, 0.2f, 0.16f, Mats.Unlit(new Color(0.55f, 0.95f, 0.85f, 0.25f), Tex.Ring, true), new Vector3(0, 0.002f, 0), Quaternion.Euler(90, 0, 0));
                    CounterMarks.Add(m);
                }
                Trace = TraceMark.Create(transform, CountryId.Morocco, new Vector3(-0.2f, 1.05f, 0.25f), Quaternion.identity, 0.22f, true);
            }

            AddSeam("door", new Vector3(0f, 0f, -2.7f), 0f, 1.3f);
            AddSeam("shelves", new Vector3(-1.9f, 0f, -0.6f), 90f, 1.3f);
            if (!Corrupted) foreach (var s in Seams) s.Revealed = false;
            SetArrival(Corrupted ? new Vector3(0f, 0f, -2.2f) : new Vector3(0f, 0f, SoukStartZ), 0f);
            AddBounds(new Vector3(0, 0, Corrupted ? -1f : -21f), Corrupted ? new Vector3(40f, 3f, 4.2f) : new Vector3(9f, 3f, 46f));
            Expand_Interiors.MoroccoPostcards(this); // the Margin shop is Morocco: three postcards (Cells/Expand_Interiors.cs)
            if (Corrupted) SetEncounter(gameObject.AddComponent<MoroccoEncounter>());
            else SetEncounter(gameObject.AddComponent<HomeEncounter>());
        }

        /// <summary>
        /// The wall behind the counter, which the player faces at the counter in both versions of the shop (playtest: it
        /// was a bare dark slab): a glazed cabinet of cameras and lenses between open shelves of boxed film, cupboards
        /// below, a wall clock and a cabinet light. Shallow (16-18 cm) so the shopkeeper still fits behind the counter.
        /// </summary>
        void BuildBackWall(Transform g, Material wood, Material brass)
        {
            var root = new GameObject("BackWallDisplay").transform;
            root.SetParent(g, false);
            var k = new KitBatch();
            var rng = new System.Random(Corrupted ? 23 : 17);
            var dark = Mats.Lit(new Color(0.07f, 0.07f, 0.075f), 0.45f, 0.2f);
            var chrome = Mats.Lit(new Color(0.78f, 0.79f, 0.81f), 0.85f, 1f);
            var felt = Mats.Lit(Corrupted ? new Color(0.14f, 0.22f, 0.21f) : new Color(0.34f, 0.09f, 0.08f), 0.1f, 0f, "fabric_linen");
            var paper = Mats.Lit(new Color(0.9f, 0.87f, 0.78f), 0.25f, 0f, "paper_ivory");
            var cabinetGlass = Mats.Glass(new Color(0.7f, 0.8f, 0.8f, 0.1f));
            var film = new[]
            {
                Mats.Lit(new Color(0.86f, 0.64f, 0.14f), 0.3f), Mats.Lit(new Color(0.16f, 0.3f, 0.55f), 0.3f),
                Mats.Lit(new Color(0.62f, 0.13f, 0.1f), 0.3f), paper,
            };
            const float face = 1.05f; // the back wall's front face
            // Cupboards either side of the keeper's spot, a worktop over them.
            foreach (var span in new[] { new Vector2(-1.95f, -0.15f), new Vector2(1.25f, 1.95f) })
            {
                float cx = (span.x + span.y) / 2f, w = span.y - span.x;
                k.Box(wood, new Vector3(cx, 0.45f, face - 0.09f), new Vector3(w, 0.9f, 0.18f));
                int doors = Mathf.Max(1, Mathf.RoundToInt(w / 0.6f));
                float dw = w / doors;
                for (int d = 0; d < doors; d++)
                {
                    float dx = span.x + (d + 0.5f) * dw;
                    k.Box(dark, new Vector3(dx, 0.5f, face - 0.182f), new Vector3(dw - 0.05f, 0.68f, 0.006f));
                    k.Box(wood, new Vector3(dx, 0.5f, face - 0.19f), new Vector3(dw - 0.1f, 0.6f, 0.012f));
                    k.Of(brass).AddSphere(new Vector3(dx + (d % 2 == 0 ? 0.35f : -0.35f) * dw, 0.66f, face - 0.205f), 0.014f, 8, 6);
                }
                k.Box(wood, new Vector3(cx, 0.92f, face - 0.1f), new Vector3(w + 0.04f, 0.04f, 0.22f));
            }
            // Upper shelving: felt-backed, three bays, five shelves, a cornice.
            k.Box(felt, new Vector3(0f, 1.86f, face - 0.006f), new Vector3(3.9f, 1.5f, 0.01f));
            foreach (float ux in new[] { -1.95f, -0.72f, 0.72f, 1.95f })
                k.Box(wood, new Vector3(ux, 1.86f, face - 0.08f), new Vector3(0.05f, 1.52f, 0.16f));
            float[] shelfY = { 1.12f, 1.47f, 1.82f, 2.17f, 2.52f };
            foreach (float sy in shelfY) k.Box(wood, new Vector3(0f, sy, face - 0.08f), new Vector3(3.9f, 0.03f, 0.16f));
            k.Box(wood, new Vector3(0f, 2.63f, face - 0.1f), new Vector3(4.02f, 0.09f, 0.2f));
            // Centre bay: cameras and lenses behind glazed doors.
            for (int r = 0; r < 4; r++)
            {
                float y = shelfY[r] + 0.015f;
                for (float cx = -0.58f; cx < 0.6f; cx += 0.29f)
                {
                    float jx = cx + (float)(rng.NextDouble() - 0.5) * 0.06f;
                    if ((r + Mathf.RoundToInt(cx * 10)) % 2 == 0)
                    {
                        k.Box(dark, new Vector3(jx, y + 0.04f, face - 0.09f), new Vector3(0.13f, 0.08f, 0.05f));
                        k.Box(chrome, new Vector3(jx, y + 0.085f, face - 0.09f), new Vector3(0.13f, 0.012f, 0.05f));
                        k.Of(chrome).AddCylinder(new Vector3(jx, y + 0.04f, face - 0.115f), 0.024f, 0.04f, 14, 1f, Quaternion.Euler(-90f, 0f, 0f));
                        k.Of(dark).AddDisc(new Vector3(jx, y + 0.04f, face - 0.156f), Vector3.back, 0.018f, 14);
                    }
                    else
                    {
                        float h = 0.06f + (float)rng.NextDouble() * 0.06f;
                        k.Of(dark).AddCylinder(new Vector3(jx, y, face - 0.09f), 0.032f, h, 14);
                        k.Of(chrome).AddCylinder(new Vector3(jx, y + h * 0.7f, face - 0.09f), 0.034f, 0.012f, 14);
                    }
                }
            }
            foreach (float gx in new[] { -0.355f, 0.355f })
            {
                k.Box(cabinetGlass, new Vector3(gx, 1.82f, face - 0.165f), new Vector3(0.68f, 1.4f, 0.004f));
                k.Box(wood, new Vector3(gx, 1.12f, face - 0.168f), new Vector3(0.7f, 0.04f, 0.012f));
                k.Box(wood, new Vector3(gx, 2.52f, face - 0.168f), new Vector3(0.7f, 0.04f, 0.012f));
                k.Box(wood, new Vector3(gx - 0.34f, 1.82f, face - 0.168f), new Vector3(0.03f, 1.42f, 0.012f));
                k.Box(wood, new Vector3(gx + 0.34f, 1.82f, face - 0.168f), new Vector3(0.03f, 1.42f, 0.012f));
                k.Of(brass).AddSphere(new Vector3(gx + (gx < 0 ? 0.3f : -0.3f), 1.8f, face - 0.18f), 0.011f, 8, 6);
            }
            // Side bays: boxed film, binoculars and spare lenses.
            foreach (var bay in new[] { new Vector2(-1.92f, -0.75f), new Vector2(0.75f, 1.92f) })
                for (int r = 0; r < 4; r++)
                {
                    float y = shelfY[r] + 0.015f, x = bay.x + 0.02f;
                    while (x < bay.y - 0.08f)
                    {
                        float bw = 0.05f + (float)rng.NextDouble() * 0.07f, bh = 0.07f + (float)rng.NextDouble() * 0.12f;
                        double roll = rng.NextDouble();
                        if (roll < 0.7)
                            k.Box(film[rng.Next(film.Length)], new Vector3(x + bw / 2f, y + bh / 2f, face - 0.085f), new Vector3(bw, bh, 0.1f));
                        else if (roll < 0.82)
                        {
                            k.Of(dark).AddCylinder(new Vector3(x + 0.03f, y, face - 0.08f), 0.026f, 0.11f, 12);
                            k.Of(dark).AddCylinder(new Vector3(x + 0.085f, y, face - 0.08f), 0.026f, 0.11f, 12);
                            bw = 0.12f;
                        }
                        x += bw + 0.012f + (float)rng.NextDouble() * 0.03f;
                    }
                }
            // A wall clock above the cabinet. In the Margin copy its minute hand runs slowly backwards.
            var clock = new GameObject("WallClock").transform;
            clock.SetParent(root, false);
            clock.localPosition = new Vector3(0f, 2.81f, face - 0.02f);
            var cb = new MeshBuilder();
            cb.AddTorus(Vector3.zero, 0.115f, 0.012f, 28, 6, Quaternion.Euler(90f, 0f, 0f));
            MeshFactory.FromBuilder("Rim", cb, brass, clock);
            var dial = new MeshBuilder();
            dial.AddDisc(new Vector3(0f, 0f, -0.004f), Vector3.back, 0.108f, 28);
            MeshFactory.FromBuilder("Dial", dial, paper, clock);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                MeshFactory.Box("Tick", clock, new Vector3(Mathf.Sin(a) * 0.088f, Mathf.Cos(a) * 0.088f, -0.007f), new Vector3(0.006f, i % 3 == 0 ? 0.02f : 0.011f, 0.002f), dark, false, 1f, Quaternion.Euler(0f, 0f, -i * 30f));
            }
            var hourPivot = new GameObject("HourHand").transform;
            hourPivot.SetParent(clock, false);
            hourPivot.localPosition = new Vector3(0f, 0f, -0.01f);
            hourPivot.localRotation = Quaternion.Euler(0f, 0f, Corrupted ? 0f : -200f);
            MeshFactory.Box("Hand", hourPivot, new Vector3(0f, 0.03f, 0f), new Vector3(0.008f, 0.06f, 0.003f), dark, false);
            var minutePivot = new GameObject("MinuteHandPivot").transform;
            minutePivot.SetParent(clock, false);
            minutePivot.localPosition = new Vector3(0f, 0f, -0.013f);
            minutePivot.localRotation = Quaternion.Euler(-90f, 0f, 0f); // local Y = the dial's normal, so Spin turns the hand
            var minute = MeshFactory.Box("Hand", minutePivot, new Vector3(0f, 0f, -0.042f), new Vector3(0.005f, 0.003f, 0.085f), dark, false);
            minutePivot.localRotation *= Quaternion.Euler(0f, Corrupted ? 0f : 240f, 0f);
            if (Corrupted) minutePivot.gameObject.AddComponent<Spin>().DegreesPerSecond = -9f;
            k.Bake(root, "BackWall");
            if (!Corrupted) MemoryReveal.Attach(root.gameObject, 7f, 0.35f, 3.1f); // the remembered wall builds itself as you enter

            // A small cabinet light under the cornice so the wall reads from the counter (cool in the Margin copy).
            var strip = MeshFactory.Box("CabinetLight", root, new Vector3(0f, 2.575f, face - 0.15f), new Vector3(1.3f, 0.02f, 0.025f),
                Mats.Emissive(new Color(0.9f, 0.9f, 0.85f), Corrupted ? new Color(0.5f, 0.85f, 0.8f) : new Color(1.6f, 1.25f, 0.8f)), false);
            strip.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            AddPointLight(root, new Vector3(0f, 2.4f, face - 0.45f), Corrupted ? new Color(0.62f, 0.86f, 0.82f) : new Color(1f, 0.8f, 0.55f), Corrupted ? 1.1f : 0.9f, 2.6f);
        }

        /// <summary>Pendant lamps down the impossible aisles: a few lit, most dead, so the rows read as they recede into fog.</summary>
        void BuildAisleLamps(Transform g)
        {
            var shade = Mats.Lit(new Color(0.16f, 0.18f, 0.17f), 0.5f, 0.6f);
            var lit = Mats.Emissive(new Color(0.8f, 0.9f, 0.85f), new Color(0.7f, 1.05f, 0.95f));
            var dead = Mats.Lit(new Color(0.2f, 0.22f, 0.21f), 0.6f);
            for (int side = -1; side <= 1; side += 2)
                for (int k = 0; k < 9; k++)
                {
                    float x = side * (2.25f + 0.85f + k * 1.7f);
                    bool on = k == 0 || k == 2 || k == 5;
                    var p = new Vector3(x, 2.62f, -1f);
                    MeshFactory.Box("Cord", g, p + new Vector3(0f, 0.18f, 0f), new Vector3(0.01f, 0.36f, 0.01f), shade, false);
                    var s = MeshFactory.Frustum("AisleShade", g, p, 0.16f, 0.05f, 0.14f, shade, 10);
                    s.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    MeshFactory.Sphere("Bulb", g, p + new Vector3(0f, -0.01f, 0f), 0.04f, on ? lit : dead, 8, 6);
                    if (on) AddPointLight(g, p + new Vector3(0f, -0.15f, 0f), new Color(0.6f, 0.85f, 0.8f), 0.9f, 3.6f);
                }
        }

        static void Shelves(Transform parent, Vector3 pos, int side, Material wood, Material brass, int rows)
        {
            var root = new GameObject("Shelves").transform;
            root.SetParent(parent, false);
            root.localPosition = pos;
            MeshFactory.Box("Back", root, new Vector3(side * 0.22f, 1.2f, 0), new Vector3(0.05f, 2.4f, 3.6f), wood, true);
            var rng = new System.Random((int)(pos.x * 10));
            var dark = Mats.Lit(new Color(0.08f, 0.08f, 0.09f), 0.4f, 0.2f);
            var chrome = Mats.Lit(new Color(0.75f, 0.76f, 0.78f), 0.8f, 1f);
            var boxMat = Mats.Lit(new Color(0.55f, 0.42f, 0.3f), 0.2f, 0f, "paper_ivory");
            for (int r = 0; r < rows; r++)
            {
                float y = 0.5f + r * 0.6f;
                MeshFactory.Box("Board", root, new Vector3(0, y, 0), new Vector3(0.45f, 0.04f, 3.6f), wood, true);
                for (int k = 0; k < 6; k++)
                {
                    float z = -1.5f + k * 0.6f + (float)rng.NextDouble() * 0.15f;
                    int kind = rng.Next(3);
                    if (kind == 0)
                    {
                        MeshFactory.Box("Camera", root, new Vector3(0, y + 0.07f, z), new Vector3(0.12f, 0.1f, 0.16f), dark, false);
                        MeshFactory.Cylinder("Lens", root, new Vector3(-side * 0.06f, y + 0.07f, z), 0.035f, 0.06f, chrome, 12, false).transform.localRotation = Quaternion.Euler(0, 0, side * 90f);
                    }
                    else if (kind == 1) MeshFactory.Box("Box", root, new Vector3(0, y + 0.08f, z), new Vector3(0.2f, 0.14f, 0.24f), boxMat, false);
                    else MeshFactory.Cylinder("LensBarrel", root, new Vector3(0, y + 0.02f, z), 0.04f, 0.12f, brass, 12, false);
                }
            }
        }

        public static ShopCell Make(GameObject go, bool corrupted)
        {
            var c = go.AddComponent<ShopCell>();
            c.Corrupted = corrupted;
            return c;
        }
    }

    public class Spin : MonoBehaviour
    {
        public float DegreesPerSecond = 60f;
        void Update() => transform.Rotate(0, DegreesPerSecond * Time.deltaTime, 0, Space.Self);
    }
}

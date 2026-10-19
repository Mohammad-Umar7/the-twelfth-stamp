using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>USA / New York — Spot the Impostor. The harbour terminal's arrival hall has become a space-station arcade:
    /// three cartoon crewmates on pads, two tiny station tasks, a security screen with the one clue, and a VOTE button in
    /// front of each crewmate (Cells/NewYorkImpostorGame.cs). Vote the impostor out while the figure hunts the hall.</summary>
    public partial class NewYorkCell : Cell
    {
        public TMPro.TextMeshPro Screen;
        public GameObject ScreenGlow;
        public Grabbable Noisemaker;
        public Transform Gate;
        public TraceMark Trace;
        public ImpostorGame Station;
        public GhostHunt Hunt;
        /// <summary>The statue's root in the harbour (the vista builds its island round it; the pier runs out to it).</summary>
        static readonly Vector3 LibertyRoot = new Vector3(150f, -1.5f, -70f);

        protected override void Build()
        {
            Id = CellId.NewYork;
            Lighting = new LightingPreset
            {
                // Harbour night with a city glow on the horizon: sodium lamps and terminal strip lights are the key, the moon
                // a cool fill, and the fog lighter than the shapes in it so the terminal and the statue separate. Ambient and
                // exposure lifted a step (playtest: too dark to read on a monitor); bloom eased so lit glass stays glass.
                SunColor = new Color(0.5f, 0.56f, 0.74f), SunIntensity = 0.45f, SunEuler = new Vector3(48f, 140f, 0f), ShadowStrength = 0.55f,
                AmbientSky = new Color(0.28f, 0.29f, 0.36f), AmbientEquator = new Color(0.29f, 0.25f, 0.2f), AmbientGround = new Color(0.11f, 0.1f, 0.09f),
                FogColor = new Color(0.21f, 0.21f, 0.25f), FogDensity = 0.017f,
                SkyZenith = new Color(0.06f, 0.07f, 0.12f), SkyHorizon = new Color(0.42f, 0.34f, 0.32f), SkyGround = new Color(0.08f, 0.08f, 0.09f),
                SunDiscColor = new Color(0, 0, 0, 0), Stars = 0.15f, CloudColor = new Color(0.26f, 0.23f, 0.25f, 0.4f),
                PostExposure = 0.42f, Saturation = -6f, Contrast = 10f, ColorFilter = new Color(1f, 0.94f, 0.87f), Bloom = 0.6f, Vignette = 0.22f
            };
            Ambience = "amb_ny_harbour";
            AddSun();
            // Linear colour space: a textured material shows texture x tint, so tints stay pale and the texture carries the
            // colour (dark tints on dark textures came out nearly black; playtest: "murky").
            var asphalt = Mats.Lit(new Color(0.92f, 0.92f, 0.95f), 0.7f, 0f, "asphalt_wet");
            var pavers = Mats.Lit(new Color(0.86f, 0.85f, 0.84f), 0.45f, 0f, "granite_pavers");
            var concrete = Mats.Lit(new Color(0.9f, 0.9f, 0.88f), 0.25f, 0f, "concrete_panels");
            var metal = Mats.Lit(new Color(0.7f, 0.72f, 0.76f), 0.5f, 0.6f, "metal_brushed");
            var paint = Mats.Lit(new Color(0.3f, 0.46f, 0.54f), 0.45f, 0.2f);
            var glass = Mats.Glass(new Color(0.55f, 0.65f, 0.7f, 0.22f));
            var sodium = new Color(1f, 0.62f, 0.3f);
            var g = Group("Terminal");

            var apron = MeshFactory.Floor("Apron", g, new Vector3(0, 0, 2f), new Vector2(14f, 16f), asphalt, "stone", 0.3f, 1.2f);
            // The slab keeps the collider; it is drawn as tiles on the textures' 1.2 m repeat (forward rendering lights each
            // object with its eight nearest lights, and the floor has more than that around it): a paved platform under the
            // canopy, asphalt on the open apron in front of it.
            apron.GetComponent<MeshRenderer>().enabled = false;
            foreach (var tx in new[] { new Vector2(-7f, 0.2f), new Vector2(0.2f, 7f) })
            {
                MeshFactory.Box("PlatformTile", g, new Vector3((tx.x + tx.y) / 2f, -0.15f, 4.2f), new Vector3(tx.y - tx.x, 0.3f, 11.6f), pavers, false, 1.2f);
                MeshFactory.Box("ApronTile", g, new Vector3((tx.x + tx.y) / 2f, -0.15f, -3.8f), new Vector3(tx.y - tx.x, 0.3f, 4.4f), asphalt, false, 1.2f);
            }
            // Terminal canopy on columns (in two halves for the same reason).
            foreach (var cx in new[] { -3f, 3f })
                MeshFactory.Box("Canopy", g, new Vector3(cx, 4.2f, 3f), new Vector3(6f, 0.3f, 9f), concrete, false, 2f);
            for (int i = 0; i < 4; i++) foreach (var x in new[] { -5.5f, 5.5f })
                MeshFactory.Box("Column", g, new Vector3(x, 2.1f, -1f + i * 2.8f), new Vector3(0.35f, 4.2f, 0.35f), concrete, true);
            // The back wall keeps its collider; BuildHall draws the glazed frontage of the arrivals hall in its place.
            var backWall = MeshFactory.Box("BackWall", g, new Vector3(0, 2.1f, 7.8f), new Vector3(12f, 4.2f, 0.3f), concrete, true, 2f);
            backWall.GetComponent<MeshRenderer>().enabled = false;
            MeshFactory.Box("WestWall", g, new Vector3(-6f, 2.1f, 3f), new Vector3(0.3f, 4.2f, 9.6f), concrete, true, 2f);
            // Harbour edge to the east with a railing (its gate at z -4.95..-1.12 opens onto the island pier), the water and
            // the statue on its island.
            Kit.Railing(g, new Vector3(6.5f, 0f, -1.12f), new Vector3(6.5f, 0f, 9f), 1.05f, metal, 0.18f);
            var rail = new GameObject("RailingCollider"); rail.transform.SetParent(g, false); rail.transform.localPosition = new Vector3(6.5f, 0.6f, 3.94f); rail.AddComponent<BoxCollider>().size = new Vector3(0.2f, 1.2f, 10.12f);
            Kit.Water(g, new Vector3(40f, -1.5f, 2f), new Vector2(66f, 140f), new Color(0.04f, 0.05f, 0.07f));
            Landmarks.Liberty(g, LibertyRoot, -60f, Mats.Backdrop(new Color(0.16f, 0.2f, 0.2f), Lighting), Mats.Backdrop(new Color(1f, 0.8f, 0.5f), Lighting, new Color(2f, 1.3f, 0.5f)));
            Landmarks.Skyline(g, new Vector3(0, -2f, 0), 210f, 70f, 70f, 14, Mats.Backdrop(new Color(0.1f, 0.1f, 0.13f), Lighting), Mats.Backdrop(new Color(1f, 0.8f, 0.5f), Lighting, new Color(1.2f, 0.8f, 0.45f)), 31);

            // Arrival gate (the crewmates stand in front of it) and a luggage carousel.
            Gate = new GameObject("ArrivalGate").transform;
            Gate.SetParent(g, false);
            Gate.localPosition = new Vector3(2.2f, 0, 6.2f);
            MeshFactory.Box("GateFrameL", Gate, new Vector3(-1.1f, 1.2f, 0), new Vector3(0.15f, 2.4f, 0.15f), metal, true);
            MeshFactory.Box("GateFrameR", Gate, new Vector3(1.1f, 1.2f, 0), new Vector3(0.15f, 2.4f, 0.15f), metal, true);
            MeshFactory.Box("GateTop", Gate, new Vector3(0, 2.45f, 0), new Vector3(2.4f, 0.2f, 0.2f), metal, false);
            MeshFactory.Box("GateSign", Gate, new Vector3(0, 2.8f, -0.05f), new Vector3(2f, 0.4f, 0.06f), Mats.Emissive(new Color(0.1f, 0.12f, 0.1f), new Color(0.25f, 0.6f, 0.4f)), false);
            var sign = TextFactory.World(Gate, "ARRIVALS", 0.18f, new Color(0.85f, 1f, 0.9f), TMPro.TextAlignmentOptions.Center, false, 2f);
            sign.transform.localPosition = new Vector3(0, 2.8f, -0.09f);
            // The trace stays hidden: voting the impostor out verifies the seal.
            Trace = TraceMark.Create(transform, CountryId.USA, new Vector3(2.2f, 0.02f, 5.5f), Quaternion.identity, 0.55f, true);
            var belt = Mats.Lit(new Color(0.12f, 0.12f, 0.13f), 0.4f);
            MeshFactory.Box("Carousel", g, new Vector3(-2.8f, 0.35f, 4.2f), new Vector3(1.4f, 0.7f, 5.5f), metal, true);
            MeshFactory.Box("Belt", g, new Vector3(-2.8f, 0.72f, 4.2f), new Vector3(1.2f, 0.04f, 5.3f), belt, false);
            for (int i = 0; i < 3; i++) Kit.Crate(g, new Vector3(-2.8f, 0.74f, 2.5f + i * 1.6f), i * 20f, new Vector3(0.5f, 0.32f, 0.7f), Mats.Lit(new Color(0.95f, 0.9f, 0.85f), 0.3f, 0f, "leather_navy"));

            // Security booth with its screen and the noisemaker (USA's seal grants the noisemaker; Berlin uses it).
            var booth = new GameObject("SecurityBooth").transform;
            booth.SetParent(g, false);
            booth.localPosition = new Vector3(-3.6f, 0, -1.5f);
            MeshFactory.Box("BoothBase", booth, new Vector3(0, 0.5f, 0), new Vector3(1.8f, 1f, 1.4f), paint, true);
            var bg = MeshFactory.Box("BoothGlass", booth, new Vector3(0, 1.7f, 0), new Vector3(1.8f, 1.4f, 1.4f), glass, false);
            bg.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            MeshFactory.Box("BoothRoof", booth, new Vector3(0, 2.45f, 0), new Vector3(2f, 0.1f, 1.6f), paint, false);
            MeshFactory.Box("Monitor", booth, new Vector3(0.3f, 1.25f, -0.55f), new Vector3(0.5f, 0.38f, 0.3f), Mats.Lit(new Color(0.12f, 0.12f, 0.11f), 0.3f), false);
            ScreenGlow = MeshFactory.Quad("ScreenGlow", booth, 0.42f, 0.3f, Mats.Unlit(new Color(0.35f, 0.95f, 0.6f), null, false, 0.9f), new Vector3(0.3f, 1.25f, -0.705f));
            ScreenGlow.SetActive(false);
            Screen = TextFactory.World(booth, "", 0.016f, new Color(0.1f, 0.18f, 0.12f), TMPro.TextAlignmentOptions.TopLeft, false, 0.4f);
            Screen.transform.localPosition = new Vector3(0.3f, 1.27f, -0.71f);
            Screen.rectTransform.sizeDelta = new Vector2(0.4f, 0.28f);
            var noise = new GameObject("Noisemaker");
            noise.transform.SetParent(booth, false);
            noise.transform.localPosition = new Vector3(-0.35f, 1.04f, -0.45f);
            if (!PropLibrary.NoisemakerModel(noise.transform))
            {
                MeshFactory.Box("Body", noise.transform, new Vector3(0, 0.07f, 0), new Vector3(0.22f, 0.14f, 0.09f), Mats.Lit(new Color(0.5f, 0.35f, 0.2f), 0.4f, 0f, "leather_navy"), false);
                MeshFactory.Box("Grille", noise.transform, new Vector3(-0.04f, 0.07f, -0.047f), new Vector3(0.11f, 0.1f, 0.005f), Mats.Lit(new Color(0.75f, 0.7f, 0.6f), 0.6f, 0.9f, "metal_brushed"), false);
                MeshFactory.Cylinder("Dial", noise.transform, new Vector3(0.06f, 0.07f, -0.05f), 0.025f, 0.01f, Mats.Lit(new Color(0.1f, 0.1f, 0.1f), 0.5f), 12, false).transform.localRotation = Quaternion.Euler(-90, 0, 0);
                MeshFactory.Cylinder("Antenna", noise.transform, new Vector3(0.09f, 0.14f, 0), 0.003f, 0.25f, Mats.Lit(new Color(0.7f, 0.7f, 0.72f), 0.8f, 1f), 6, false);
            }
            noise.AddComponent<BoxCollider>().size = new Vector3(0.24f, 0.16f, 0.11f);
            Noisemaker = PropLibrary.MakeGrabbable(noise, "noisemaker", "Take the noisemaker", true);
            Noisemaker.ActivatePrompt = "Switch the noisemaker on/off";
            AddSpotLight(booth, new Vector3(0, 2.3f, 0), new Vector3(90, 0, 0), new Color(0.9f, 0.95f, 1f), 1.2f, 4f, 90f);

            // Sodium key under the canopy, a wide spot aimed down so the paler concrete overhead never gets a hot spot (the
            // cast-iron pier lamps and their lights are built with the harbour edge).
            var key = AddSpotLight(g, new Vector3(0, 3.65f, 3f), new Vector3(90f, 0f, 0f), sodium, 2.6f, 10f, 140f);
            key.shadows = LightShadows.Soft;

            BuildTerminalDetail(g, concrete, metal, paint, sodium);

            // The game: the space-station arcade in the arrival hall (Cells/NewYorkImpostorGame.cs).
            Station = ImpostorGame.Build(g);
            // The figure hunts the hall while the game is played: it walks in off the apron at the south end.
            Hunt = gameObject.AddComponent<GhostHunt>();
            Hunt.Cell = this;
            Hunt.SpawnLocal = new Vector3(-5.2f, 0f, -5.4f);
            Hunt.RetreatLocal = new Vector3(5.6f, 0f, -5.2f);
            Hunt.StalkSpeed = 0.55f;
            Hunt.RushEvery = 14f;
            Hunt.RushSeconds = 2.2f;
            Hunt.LoseDistance = 12f;
            Hunt.RestSeconds = 9f;
            Hunt.Arrives = "the last arrival walks in off the apron";

            AddSeam("arrival", new Vector3(-1f, 0f, -5.4f), 0f);
            AddSeam("pier", new Vector3(6.1f, 0f, 3.5f), -90f);
            AddSeam("hall", new Vector3(-5.6f, 0f, 4.8f), 90f);
            SetArrival(new Vector3(-1f, 0f, -4.4f), 10f);
            // Bounds: BuildIslandPier keeps the terminal's old walls (with the pier gate in the east one) and adds one box
            // round the terminal and the way out to the statue.
            SetEncounter(gameObject.AddComponent<NewYorkEncounter>());
            Expand_USA.Build(this, transform); // the rec pier beside the terminal (Cells/Expand_USA.cs)
        }
    }

    public partial class NewYorkCell
    {
        /// <summary>
        /// The terminal reads as a place: canopy beams, apron markings and bollards, mooring posts and a lifebuoy on the
        /// harbour edge, a bench, a vending machine, luggage trolleys and stacked bags, and a hazard-striped electrics box on
        /// the booth. The arrivals hall, canopy fittings, baggage claim, quay, ferry and street are in Detail_NewYork.cs.
        /// </summary>
        void BuildTerminalDetail(Transform g, Material concrete, Material metal, Material paint, Color sodium)
        {
            var k = new KitBatch();
            // Pale tints on textured materials (linear colour: texture x tint); these match NyPalette so the batches merge.
            var steel = Mats.Lit(new Color(0.62f, 0.65f, 0.7f), 0.55f, 0.6f, "metal_brushed");
            var yellow = Mats.Lit(new Color(0.85f, 0.65f, 0.12f), 0.35f);
            var white = Mats.Lit(new Color(0.85f, 0.85f, 0.82f), 0.3f);
            var frame = Mats.Lit(new Color(0.24f, 0.25f, 0.27f), 0.5f, 0.5f);
            var black = Mats.Lit(new Color(0.05f, 0.05f, 0.05f), 0.4f);
            var orange = Mats.Lit(new Color(0.8f, 0.34f, 0.08f), 0.45f);
            var navy = Mats.Lit(new Color(0.12f, 0.24f, 0.42f), 0.4f);
            var red = Mats.Lit(new Color(0.62f, 0.07f, 0.05f), 0.5f);

            // Canopy structure; its light fittings hang between these beams (BuildCanopyDetail). The two cool lights are wide
            // spots aimed down, like the fittings they stand for.
            for (int i = 0; i < 5; i++) k.Box(steel, new Vector3(0, 4.0f, -1.2f + i * 2.1f), new Vector3(11.8f, 0.22f, 0.16f));
            AddSpotLight(g, new Vector3(-2.5f, 3.55f, 1.2f), new Vector3(90f, 0f, 0f), new Color(0.88f, 0.93f, 1f), 2.3f, 9f, 140f);
            AddSpotLight(g, new Vector3(2.5f, 3.55f, 4.8f), new Vector3(90f, 0f, 0f), new Color(0.88f, 0.93f, 1f), 2.3f, 9f, 140f);

            // Apron markings (the centre line only on the asphalt, the walk line by the bollards), bollards and cones.
            for (int i = 0; i < 2; i++) k.Box(yellow, new Vector3(-0.2f, 0.003f, -4.8f + i * 1.6f), new Vector3(0.12f, 0.004f, 0.9f));
            k.Box(white, new Vector3(4.6f, 0.003f, 2f), new Vector3(0.1f, 0.004f, 14f));
            k.Box(concrete, new Vector3(0f, 0.015f, -1.62f), new Vector3(14f, 0.03f, 0.16f)); // the platform's kerb
            foreach (var z in new[] { -1.2f, 1.2f, 3.6f, 6f })
            {
                k.Box(yellow, new Vector3(4.9f, 0.45f, z), new Vector3(0.18f, 0.9f, 0.18f));
                k.Box(black, new Vector3(4.9f, 0.62f, z), new Vector3(0.19f, 0.08f, 0.19f));
            }
            // Every material in this batch is one more renderer, so the props share the palette's few colours.
            foreach (var p in new[] { new Vector3(-2.2f, 0f, -2.6f), new Vector3(-2.7f, 0f, -2.1f), new Vector3(-1.8f, 0f, -2.0f) })
            {
                k.Of(orange).AddFrustum(p, 0.14f, 0.025f, 0.55f, 10);
                k.Of(orange).AddBox(p + Vector3.up * 0.02f, new Vector3(0.34f, 0.04f, 0.34f));
                k.Box(white, p + Vector3.up * 0.33f, new Vector3(0.17f, 0.06f, 0.17f)); // reflective collar
            }

            // Harbour edge: mooring posts and a lifebuoy stand (the quay, its lamps and the ferry are in BuildHarbourEdge;
            // the island pier leaves the railing at z -4.8..-1.2, so no post stands there).
            foreach (var z in new[] { -5.5f, 0.5f, 5f })
            {
                k.Box(metal, new Vector3(7.0f, 0.35f, z), new Vector3(0.32f, 0.7f, 0.32f));
                k.Box(metal, new Vector3(7.0f, 0.74f, z), new Vector3(0.44f, 0.1f, 0.44f));
            }
            k.Box(frame, new Vector3(6.75f, 0.9f, 2.6f), new Vector3(0.08f, 1.8f, 0.08f));
            k.Of(orange).AddTorus(new Vector3(6.68f, 1.25f, 2.6f), 0.26f, 0.07f, 20, 8, Quaternion.Euler(0, 0, 90));

            // A steel bench between the columns, its back to the wall under the clock (the hall seam beside it stays clear),
            // a vending machine and trolleys.
            for (int i = 0; i < 3; i++) k.Box(steel, new Vector3(-5.15f - i * 0.13f, 0.45f, 3.1f), new Vector3(0.1f, 0.04f, 1.6f));
            k.Box(steel, new Vector3(-5.47f, 0.75f, 3.1f), new Vector3(0.04f, 0.3f, 1.6f));
            foreach (var lz in new[] { 2.4f, 3.8f }) k.Box(frame, new Vector3(-5.3f, 0.22f, lz), new Vector3(0.4f, 0.45f, 0.06f));
            Solid(g, new Vector3(-5.3f, 0.45f, 3.1f), new Vector3(0.5f, 0.9f, 1.6f));
            var vend = MeshFactory.Box("VendingMachine", g, new Vector3(-5.4f, 0.95f, 0f), new Vector3(0.85f, 1.9f, 0.8f), red, true);
            MeshFactory.Box("VendingGlass", vend.transform, new Vector3(0.43f, 0.15f, 0f), new Vector3(0.02f, 1.2f, 0.6f), Mats.Emissive(new Color(0.3f, 0.32f, 0.3f), new Color(1.0f, 1.1f, 0.95f)), false);
            for (int r = 0; r < 4; r++) k.Box(frame, new Vector3(-4.955f, 1.55f - r * 0.28f, 0f), new Vector3(0.01f, 0.025f, 0.56f)); // rows of cans behind the glass
            AddPointLight(g, new Vector3(-4.6f, 1.1f, -0.4f), new Color(0.9f, 1f, 0.95f), 0.7f, 3f);
            foreach (var t in new[] { new Vector3(-1.2f, 0f, 6.0f), new Vector3(-0.4f, 0f, 6.3f) })
            {
                k.Box(metal, t + new Vector3(0, 0.25f, 0), new Vector3(0.55f, 0.04f, 0.85f));
                k.Box(metal, t + new Vector3(0, 0.6f, 0.42f), new Vector3(0.55f, 0.75f, 0.03f));
                k.Box(metal, t + new Vector3(0, 0.98f, 0.42f), new Vector3(0.6f, 0.04f, 0.04f));
            }
            // Unclaimed hard-shell cases by the wall (closed, so not the open Suitcase model from the apartment).
            var shells = new[] { navy, red, steel }; // navy, red and a brushed aluminium case
            var bagPos = new[] { new Vector3(-4.4f, 0f, 6.9f), new Vector3(-5.0f, 0f, 6.6f), new Vector3(-4.6f, 0.46f, 6.85f) };
            for (int i = 0; i < bagPos.Length; i++)
            {
                var r = Quaternion.Euler(0, 15f + i * 40f, 0);
                k.Box(shells[i], bagPos[i] + Vector3.up * 0.23f, new Vector3(0.48f, 0.46f, 0.26f), r);
                k.Box(frame, bagPos[i] + Vector3.up * 0.5f, new Vector3(0.16f, 0.03f, 0.03f), r);
            }

            // A hazard-striped electrics plate on the booth's west face (x = -4.5) with a conduit down to the floor.
            var plate = new Vector3(-4.508f, 1.1f, -1.3f);
            k.Box(yellow, plate, new Vector3(0.012f, 0.7f, 0.5f));
            foreach (var y in new[] { -0.29f, 0.27f })
                for (int i = 0; i < 5; i++) k.Box(black, plate + new Vector3(-0.008f, y, -0.2f + i * 0.1f), new Vector3(0.004f, 0.08f, 0.035f), Quaternion.Euler(35f, 0, 0));
            k.Box(metal, new Vector3(-4.53f, 0.38f, -1.3f), new Vector3(0.05f, 0.76f, 0.05f));   // conduit down the booth
            k.Box(metal, new Vector3(-4.53f, 0.04f, -1.0f), new Vector3(0.05f, 0.05f, 0.65f));   // and along the floor

            BuildPlaceDetail(g, k, concrete, metal, sodium); // canopy fittings and the west side join this batch
            k.Bake(g, "TerminalDetail");
        }
    }

    public class NewYorkEncounter : Encounter
    {
        NewYorkCell N => (NewYorkCell)Cell;
        ImpostorGame S => N.Station;
        // Read-only state for the guide (Guide/GuideBook.NewYork.cs) and the tests.
        public bool Solved => m_Solved;
        public bool NoiseOn => m_On;
        public bool Rushing => m_RushLeft > 0f || m_RushPending;
        AudioSource m_Noise;
        bool m_On, m_Solved, m_RushPending;
        float m_InvestigateCooldown, m_RushLeft;

        public override string Objective => m_Solved ? "The impostor is ejected. Run to your stamp."
            : !S.TasksDone ? "Space Station Arcade: do the two station tasks (" + (S.WiringDone ? "Fix wiring done" : "Fix wiring") + ", " + (S.CardDone ? "Swipe card done" : "Swipe card") +
                             "), then read the security screen and vote the impostor out. When the figure comes, run, lose it, then come back."
            : "The security screen says the impostor has " + ImpostorGame.ClueShort(S.Clue) + ". Press VOTE in front of that crewmate.";

        public override void OnEnter(bool firstVisit)
        {
            m_Solved = Game.Campaign.IsVerified(CountryId.USA);
            N.Noisemaker.OnActivate = (g, who) => Toggle();
            S.TaskDone -= OnTask; S.TaskDone += OnTask;
            S.Voted -= OnVoted; S.Voted += OnVoted;
            S.ImpostorGone -= OnGone; S.ImpostorGone += OnGone;
            N.Trace.Hide();
            m_RushPending = false;
            m_RushLeft = 0f;
            Game.Passenger?.Despawn();
            if (m_Solved) { S.SetSolved(); N.Hunt.Stop(); return; }
            S.ResetGame();
            Hud.I?.ShowToast("The arrival hall is a space-station arcade. One of the three crewmates is an impostor: do the two tasks, read the security screen, vote it out.", 5f);
            N.Hunt.Begin(firstVisit ? 9f : 6f);
            Hud.I?.SetObjective(Objective, false);
        }

        public override void OnExit()
        {
            S.TaskDone -= OnTask;
            S.Voted -= OnVoted;
            S.ImpostorGone -= OnGone;
            N.Hunt.Stop();
            m_RushPending = false;
            m_RushLeft = 0f;
            if (m_On) Toggle();
        }

        void OnTask(ImpostorGame g, string which)
        {
            if (!IsLive || m_Solved) return;
            if (g.TasksDone)
            {
                AudioDirector.I?.Play("camera_shutter", g.ScreenPosition, 0.9f);
                AudioDirector.I?.Play("pp_seam_shimmer", g.ScreenPosition, 0.6f);
                Hud.I?.ShowToast("Both tasks done. The security screen over the gate shows a clue: the impostor has " + ImpostorGame.ClueShort(g.Clue) + ". Vote it out.", 5f);
            }
            else Hud.I?.ShowToast((which == "wiring" ? "Wiring fixed." : "Card accepted.") + " One more task: " + (which == "wiring" ? "Swipe card." : "Fix wiring."), 3f);
            Hud.I?.SetObjective(Objective, false);
        }

        void OnVoted(ImpostorGame g, int who, bool impostor)
        {
            if (!IsLive || m_Solved) return;
            if (impostor)
            {
                m_Solved = true;
                m_RushPending = false;
                m_RushLeft = 0f;
                N.Hunt.Stop();
                Hud.I?.ShowToast(ImpostorGame.Names[who] + " was the impostor. Ejected!", 3f);
                return;
            }
            Hud.I?.ShowToast(ImpostorGame.Names[who] + " was not the impostor. Something heard the vote and comes running: get away, then vote again.", 4f);
            Rush();
        }

        void OnGone(ImpostorGame g)
        {
            if (!IsLive) return;
            AudioDirector.I?.Play("seal_verify", g.ScreenPosition, 0.8f);
            Game.Story.VerifyCountry(CountryId.USA, null);
        }

        /// <summary>A wrong vote: the figure rushes the player (it comes in first if it is not here yet).</summary>
        void Rush()
        {
            var pc = Game.Passenger;
            if (pc == null) return;
            if (pc.Visible && pc.transform.parent == Cell.transform) StartRush();
            else { m_RushPending = true; N.Hunt.Begin(0f); }
        }

        void StartRush()
        {
            m_RushPending = false;
            var pc = Game.Passenger;
            if (pc == null || !pc.Visible) return;
            pc.Threatening = true;
            pc.StartChase();
            m_RushLeft = 3f;
            AudioDirector.I?.Play("pass_sting", pc.ChestPosition, 0.8f, 1f, Bus.SFX, true);
            Hud.I?.ShowCaption("it rushes at you: run", 2f);
        }

        void Toggle()
        {
            m_On = !m_On;
            AudioDirector.I?.Play("button_press", N.Noisemaker.transform.position, 0.7f);
            if (m_On) m_Noise = AudioDirector.I?.Loop("noisemaker_loop", N.Noisemaker.transform, 0.9f, Bus.SFX, 1f, 2f, 30f);
            else if (m_Noise != null) { AudioDirector.I?.FadeOutAndStop(m_Noise, 0.2f); m_Noise = null; }
            Hud.I?.ShowCaption(m_On ? "the noisemaker crackles and warbles" : "the noisemaker falls silent", 2f);
        }

        void Update()
        {
            if (!IsLive || Game.Paused) return;
            var pc = Game.Passenger;
            if (pc == null) return;
            bool here = pc.Visible && pc.transform.parent == Cell.transform;
            if (m_RushPending && here) StartRush();
            if (m_RushLeft > 0f)
            {
                m_RushLeft -= Time.deltaTime;
                // The rush ends; the hunt takes it back to stalking.
                if (m_RushLeft <= 0f && !m_Solved && here && pc.State == PassengerState.Chase) pc.SetState(PassengerState.Stalk);
                return;
            }
            if (!here) return;
            m_InvestigateCooldown -= Time.deltaTime;
            if (m_On && m_InvestigateCooldown <= 0f && pc.State != PassengerState.Repelled && pc.State != PassengerState.Retreat && pc.State != PassengerState.Chase)
            {
                // The noisemaker still lures it to its sound, including to you if you are holding it.
                pc.Investigate(N.Noisemaker.transform.position, 14f);
                m_InvestigateCooldown = 1.5f;
            }
        }

        public void OnVerified()
        {
            if (m_On) Toggle();
            Hud.I?.ShowToast("The station lets you keep its noisemaker: its sound draws the figure. Recall it any time: " + Game.Input.Label(Intent.Recall), 4f);
        }

        public override void ResetLocal()
        {
            N.Hunt.Stop();
            Game.Passenger?.Despawn();
            m_RushPending = false;
            m_RushLeft = 0f;
            if (m_On) Toggle();
            N.Noisemaker.ReturnHome();
        }
    }
}

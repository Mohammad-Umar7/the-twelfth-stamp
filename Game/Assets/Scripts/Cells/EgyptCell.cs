using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>Egypt — The Temple Run. The player arrives at the foot of a long walled causeway east of the old exhibition
    /// courtyard (Cells/EgyptRun.cs) and runs it north toward the pyramids: three lanes, mummies bursting out of tomb
    /// doors, fallen stones to go round, passport coins to collect, and a portal at the far end where the stamp waits.
    /// Verifying Egypt still grants Egyptian sunlight for tears opened here. The courtyard, the oasis camp and the desert
    /// stay as the open world west of the run's forecourt.</summary>
    public class EgyptCell : Cell
    {
        public TraceMark Trace;
        public TempleRun Run;
        public GhostHunt Hunt;
        public Vector3 MirrorLocal = new Vector3(-2.5f, 0f, 1.5f);

        protected override void Build()
        {
            Id = CellId.Egypt;
            Lighting = new LightingPreset
            {
                SunColor = new Color(1f, 0.93f, 0.8f), SunIntensity = 2.1f, SunEuler = new Vector3(52f, 32f, 0f), ShadowStrength = 0.9f,
                AmbientSky = new Color(0.62f, 0.68f, 0.78f), AmbientEquator = new Color(0.6f, 0.52f, 0.42f), AmbientGround = new Color(0.45f, 0.36f, 0.26f),
                FogColor = new Color(0.86f, 0.78f, 0.66f), FogDensity = 0.0085f,
                SkyZenith = new Color(0.28f, 0.46f, 0.72f), SkyHorizon = new Color(0.92f, 0.84f, 0.7f), SkyGround = new Color(0.7f, 0.58f, 0.44f),
                SunDiscColor = new Color(1f, 0.95f, 0.85f, 1f), SunSize = 0.0045f, CloudColor = new Color(1f, 0.98f, 0.95f, 0.15f),
                PostExposure = -0.1f, Saturation = 4f, Contrast = 10f, ColorFilter = new Color(1f, 0.97f, 0.92f), Bloom = 0.55f, Vignette = 0.18f
            };
            Ambience = "amb_egypt";
            AddSun();

            var sandstone = Mats.Lit(new Color(0.86f, 0.74f, 0.56f), 0.18f, 0f, "sandstone_blocks");
            var trim = Mats.Lit(new Color(0.92f, 0.82f, 0.64f), 0.2f, 0f, "sandstone_blocks");
            var pavers = Mats.Lit(new Color(0.84f, 0.74f, 0.58f), 0.22f, 0f, "plaza_stone");
            var sand = Mats.Lit(new Color(0.9f, 0.76f, 0.55f), 0.12f, 0f, "sand_ripples");
            var cloth = Mats.Lit(new Color(0.78f, 0.72f, 0.62f), 0.15f, 0f, "fabric_linen");
            var wood = Mats.Lit(new Color(1f, 0.9f, 0.8f), 0.35f, 0f, "wood_dark");
            var bronze = Mats.Lit(new Color(0.82f, 0.6f, 0.32f), 0.72f, 0.95f);
            var mirrorMat = Mats.Lit(new Color(0.9f, 0.88f, 0.82f), 0.98f, 1f);

            var g = Group("Geometry");
            MeshFactory.Floor("Sand", g, new Vector3(0, -0.02f, 8f), new Vector2(60f, 60f), sand, "sand", 0.3f, 2f);
            MeshFactory.Floor("Court", g, new Vector3(0, 0.03f, 0.5f), new Vector2(13.5f, 13f), pavers, "stone", 0.3f, 1.2f);

            // Low exhibition walls (south and sides) at monumental block scale with cavetto cornices; the north side opens
            // to the desert through a pylon gateway that frames the pyramids.
            MonumentKit.Wall(g, "SouthWall", new Vector3(0, 1.3f, -6.9f), new Vector3(14.5f, 2.6f, 0.6f), sandstone, trim);
            MonumentKit.Wall(g, "WestWall", new Vector3(-7f, 1.1f, 0f), new Vector3(0.6f, 2.2f, 13.5f), sandstone, trim);
            MonumentKit.Wall(g, "EastWall", new Vector3(7f, 1.1f, -2f), new Vector3(0.6f, 2.2f, 9.5f), sandstone, trim);
            for (int i = 0; i < 4; i++) MonumentKit.Column(g, new Vector3(5.6f, 0.03f, -4.5f + i * 3.4f), 0.32f, 3.6f, sandstone, trim);
            var paint = new[] { Mats.Lit(new Color(0.2f, 0.32f, 0.5f), 0.2f), Mats.Lit(new Color(0.62f, 0.25f, 0.16f), 0.2f), Mats.Lit(new Color(0.85f, 0.75f, 0.55f), 0.2f), Mats.Lit(new Color(0.24f, 0.42f, 0.32f), 0.2f) };
            var groove = Mats.Lit(new Color(0.55f, 0.43f, 0.3f), 0.15f, 0f, "sandstone_blocks");
            var cedar = Mats.Lit(new Color(0.95f, 0.85f, 0.75f), 0.3f, 0f, "wood_dark");
            var pennantCloth = Mats.Lit(new Color(0.95f, 0.35f, 0.25f), 0.2f, 0f, "fabric_linen");
            MonumentKit.Pylon(g, new Vector3(-2.6f, 0.03f, 8.7f), 0f, 3.2f, 7.2f, 2.4f, sandstone, trim, paint, cedar, pennantCloth, groove);
            var trunk = Mats.Lit(new Color(1f, 0.9f, 0.8f), 0.15f, 0f, "wood_dark");
            var frond = Mats.Lit(new Color(0.45f, 0.62f, 0.3f), 0.25f, 0f, "fabric_linen");
            var deadFrond = Mats.Lit(new Color(0.85f, 0.7f, 0.45f), 0.15f, 0f, "fabric_linen");
            var dates = Mats.Lit(new Color(0.58f, 0.3f, 0.09f), 0.45f);
            foreach (var (pos, h, seed) in new[] { (new Vector3(-8.6f, -0.02f, 3.2f), 7.5f, 1), (new Vector3(-9.4f, -0.02f, -2.4f), 8.4f, 2),
                (new Vector3(8.7f, -0.02f, 4.6f), 7.8f, 3), (new Vector3(8.4f, -0.02f, -4.2f), 6.9f, 4), (new Vector3(3.2f, -0.02f, 10.4f), 8.8f, 5),
                (new Vector3(-7.8f, -0.02f, 9.6f), 7.2f, 6) })
                MonumentKit.Palm(g, pos, h, seed, trunk, frond, deadFrond, dates);
            // Low dunes beyond the walls give the sand depth between the courtyard and the pyramids.
            var duneMat = Mats.Lit(new Color(0.9f, 0.76f, 0.55f), 0.1f, 0f, "sand_ripples");
            foreach (var (c, sz) in new[] { (new Vector3(14f, -1.6f, 26f), new Vector3(20f, 4.5f, 14f)), (new Vector3(-15f, -1.8f, 28f), new Vector3(26f, 5f, 14f)) }) // the east one ends west of the temple run
            {
                var dune = MeshFactory.Sphere("Dune", g, c, 1f, duneMat, 24, 12);
                dune.transform.localScale = sz;
                dune.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // Walkable hills now that the desert is open (the walk to the pyramids passes them).
                dune.AddComponent<MeshCollider>();
                dune.layer = Layers.Floor;
                dune.AddComponent<Surface>().Kind = "sand";
            }
            // Shade sail and exhibit plinths with draped covers (generic, no sacred objects).
            Kit.Awning(g, new Vector3(-4.6f, 0.03f, -3.2f), 0f, 3.2f, 3.0f, 2.9f, cloth, wood);
            for (int i = 0; i < 3; i++)
            {
                var p = new Vector3(-5.4f + i * 1.6f, 0.03f, -5.2f);
                MeshFactory.Box("Plinth", g, p + Vector3.up * 0.45f, new Vector3(0.9f, 0.9f, 0.9f), sandstone, true, 1f);
                MeshFactory.Box("Drape", g, p + Vector3.up * 1.15f, new Vector3(0.7f, 0.5f, 0.6f), cloth, false, 1f, Quaternion.Euler(0, i * 12f, 0));
            }
            Landmarks.Pyramids(g, new Vector3(40f, -1f, 190f), 18f, Mats.Backdrop(new Color(0.86f, 0.72f, 0.52f), Lighting));
            BuildCourtDetail(g, sandstone, trim, wood, bronze);
            Expand_Egypt.Build(this, g); // the oasis camp east of the court (Cells/Expand_Egypt.cs)

            // The plain pillar in the courtyard (scenery now).
            MeshFactory.Box("Pillar", g, new Vector3(4.2f, 1.6f, 6.4f), new Vector3(0.5f, 3.2f, 0.5f), sandstone, true, 3.5f);
            MeshFactory.Box("PillarCap", g, new Vector3(4.2f, 3.27f, 6.4f), new Vector3(0.66f, 0.14f, 0.66f), trim, false, 3.5f);

            // The temple run, east of the oasis camp: the arrival is its forecourt and the stamp waits in its portal, so that
            // is the only stamp spot in the cell. The trace stays (hidden): crossing the finish line verifies the seal.
            Run = TempleRun.Build(transform, Lighting);
            foreach (var v in GetComponentsInChildren<VistaInfo>(true)) if (v != Run.StampSpot) v.HasStampSpot = false;
            Trace = TraceMark.Create(transform, CountryId.Egypt, TempleRun.PortalLocal + new Vector3(0f, 0.02f, -2.5f), Quaternion.identity, 0.55f, true);
            // The wrapped figure rises out of the sand by the forecourt and follows the runner slowly; the mummies are the
            // main hazard.
            Hunt = gameObject.AddComponent<GhostHunt>();
            Hunt.Cell = this;
            Hunt.SpawnLocal = new Vector3(30.5f, -0.06f, -12.5f);
            Hunt.RetreatLocal = new Vector3(30.5f, -0.06f, -2.5f);
            Hunt.SpawnState = PassengerState.Rise;
            Hunt.StalkSpeed = 0.42f;
            Hunt.RushEvery = 0f;
            Hunt.LoseDistance = 9999f;
            Hunt.Arrives = "linen rustles in the sand behind you";

            // The bronze reflector on its stand: an exhibit in the courtyard now (it no longer turns).
            var stand = new GameObject("ReflectorStand").transform;
            stand.SetParent(g, false);
            stand.localPosition = MirrorLocal;
            var standBase = MeshFactory.Cylinder("Base", stand, Vector3.zero, 0.38f, 0.12f, bronze, 18);
            var post = MeshFactory.Cylinder("Post", stand, new Vector3(0, 0.12f, 0), 0.05f, 1.0f, bronze, 10);
            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(stand, false);
            pivot.localPosition = new Vector3(0, 1.3f, 0);
            // The modelled reflector, scaled so its mirror centre (1.25 m) meets this pivot (1.3 m). Its static Base stays on
            // the stand; its Pivot (turntable, yoke, mirror) moves under the puzzle's pivot. Both turn about the same
            // vertical axis, and the mirror faces the pivot's +Z, where the reflected beam goes.
            var model = Models.Spawn("Reflector", stand, Models.Slots(("TS_Brass", bronze), ("TS_Chrome", mirrorMat)), Vector3.zero, 0f, 1.3f / 1.25f);
            var turning = Models.Child(model, "Pivot");
            if (turning != null)
            {
                turning.SetParent(pivot, true);
                Models.StripVisual(standBase);
                Models.StripVisual(post);
            }
            else
            {
                if (model != null) Object.DestroyImmediate(model);
                var frame = new MeshBuilder();
                frame.AddTorus(Vector3.zero, 0.46f, 0.035f, 36, 8, Quaternion.Euler(90, 0, 0));
                MeshFactory.FromBuilder("Frame", frame, bronze, pivot);
                var mirror = new MeshBuilder();
                mirror.AddDisc(Vector3.zero, Vector3.forward, 0.44f, 36);
                mirror.AddDisc(Vector3.zero, Vector3.back, 0.44f, 36);
                MeshFactory.FromBuilder("Mirror", mirror, mirrorMat, pivot);
                MeshFactory.Box("Yoke", pivot, new Vector3(0, -0.5f, 0), new Vector3(0.95f, 0.06f, 0.06f), bronze, false);
            }
            pivot.localRotation = Quaternion.Euler(0f, 35f, 0f); // turned toward the court's gate, as it was left
            var col = pivot.gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(1.0f, 1.0f, 0.2f);

            // Sand drifting upward over the old court, and along the run.
            Kit.Weather(g, new Vector3(0, 0.3f, 2f), new Vector3(16f, 0.2f, 16f), false, new Color(0.95f, 0.82f, 0.6f, 0.65f), 90f, 0.35f, true);
            Kit.Weather(g, new Vector3(TempleRun.RunX, 0.3f, 40f), new Vector3(6f, 0.2f, 90f), false, new Color(0.95f, 0.82f, 0.6f, 0.55f), 70f, 0.35f, true);

            // Seams: behind the arrival in the run's forecourt, in the old court, and in the portal yard at the far end.
            AddSeam("arrival", new Vector3(TempleRun.RunX, TempleRun.FloorY, -16.45f), 0f);
            AddSeam("court", new Vector3(-6.6f, 0.03f, 2.4f), 90f);
            AddSeam("finish", new Vector3(TempleRun.RunX - TempleRun.HalfW + 0.05f, TempleRun.FloorY, TempleRun.Z1 + 3.2f), 90f);
            SetArrival(TempleRun.StartLocal, 0f);
            // Open world: the run's forecourt opens west onto the sand (the court, the oasis camp and the dunes); the
            // causeway itself is walled, so the only way into the portal yard is down it.
            AddBounds(new Vector3(11f, 0, 56f), new Vector3(76f, 8f, 146f), 8f);
            Landmarks.MarkReachable(this);

            SetEncounter(gameObject.AddComponent<EgyptEncounter>());
        }

        /// <summary>
        /// The courtyard as a festival pavilion that has stood in the sun for a season: a processional path to the gate,
        /// two plain obelisks, sand drifted against the walls, fallen drums and rubble, storage jars, a rope line round the
        /// covered exhibits and the pavilion's placard.
        /// </summary>
        void BuildCourtDetail(Transform g, Material sandstone, Material trim, Material wood, Material bronze)
        {
            var k = new KitBatch();
            var rnd = new System.Random(52);
            // Processional path: larger pale slabs from the middle of the court to the pylon gate.
            for (int i = 0; i < 8; i++)
                k.Box(trim, new Vector3(-2.6f, 0.04f, -0.6f + i * 1.12f), new Vector3(2.2f, 0.025f, 1.08f), 2f);
            // Rose granite, so they stand out against the sandstone pylon behind them.
            var gilt = Mats.Lit(new Color(0.88f, 0.68f, 0.34f), 0.72f, 0.9f);
            var granite = Mats.Lit(new Color(1f, 0.78f, 0.7f), 0.45f, 0f, "granite_pavers");
            MonumentKit.Obelisk(g, new Vector3(-5.3f, 0.03f, 5.6f), 5.4f, granite, trim, gilt);
            MonumentKit.Obelisk(g, new Vector3(0.6f, 0.03f, 5.6f), 5.4f, granite, trim, gilt);

            // Sand drifted over the paving against the walls and in the corners.
            var drift = Mats.Lit(new Color(0.9f, 0.77f, 0.56f), 0.1f, 0f, "sand_ripples");
            foreach (var (c, sz) in new[]
            {
                (new Vector3(-6.3f, -0.12f, -6.2f), new Vector3(1.6f, 0.42f, 1.3f)), (new Vector3(3.6f, -0.1f, -6.35f), new Vector3(2.4f, 0.36f, 0.9f)),
                (new Vector3(6.3f, -0.1f, -6.1f), new Vector3(1.4f, 0.4f, 1.6f)), (new Vector3(-6.45f, -0.1f, -3.4f), new Vector3(0.8f, 0.3f, 1.6f)),
                (new Vector3(-6.45f, -0.1f, 5.4f), new Vector3(0.9f, 0.34f, 2.0f)), (new Vector3(6.45f, -0.1f, -0.4f), new Vector3(0.7f, 0.28f, 1.4f)),
                (new Vector3(-7.4f, -0.1f, 8.4f), new Vector3(1.8f, 0.5f, 1.4f)), (new Vector3(3.4f, -0.12f, 8.9f), new Vector3(2.2f, 0.45f, 1.1f)),
            })
            {
                var mound = MeshFactory.Sphere("SandDrift", g, c, 1f, drift, 18, 9);
                mound.transform.localScale = sz;
                mound.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Rubble along the wall feet and fallen drums on the open east side.
            foreach (var (c, s, yaw) in new[]
            {
                (new Vector3(-6.2f, 0.18f, 3.6f), new Vector3(0.6f, 0.36f, 0.45f), 18f), (new Vector3(-5.9f, 0.12f, 4.3f), new Vector3(0.35f, 0.24f, 0.3f), 40f),
                (new Vector3(4.6f, 0.15f, -6.1f), new Vector3(0.5f, 0.3f, 0.4f), -12f), (new Vector3(6.2f, 0.2f, 7.8f), new Vector3(0.7f, 0.4f, 0.5f), 25f),
                (new Vector3(1.6f, 0.1f, 8.1f), new Vector3(0.4f, 0.2f, 0.35f), 60f), (new Vector3(-6.1f, 0.14f, -5.4f), new Vector3(0.45f, 0.28f, 0.35f), -30f),
            })
                k.Box(sandstone, c, s, Quaternion.Euler((float)rnd.NextDouble() * 8f, yaw, (float)rnd.NextDouble() * 10f), 2f);
            foreach (var (c, yaw, len, r) in new[] { (new Vector3(6.3f, 0f, 6.6f), 15f, 1.1f, 0.38f), (new Vector3(4.9f, 0f, 8.2f), 78f, 0.9f, 0.34f) })
            {
                var rot = Quaternion.Euler(0f, yaw, 90f);
                var axis = rot * Vector3.up;
                k.Of(sandstone).AddCylinder(c + Vector3.up * r - axis * len / 2f, r, len, 16, 2f, rot);
            }

            // Storage jars beside the shade sail.
            var clay = Mats.Lit(new Color(1f, 0.82f, 0.7f), 0.25f, 0f, "plaster_terracotta");
            var jar = new[] { new Vector2(0f, 0f), new Vector2(0.12f, 0f), new Vector2(0.2f, 0.08f), new Vector2(0.28f, 0.3f), new Vector2(0.3f, 0.45f), new Vector2(0.26f, 0.62f), new Vector2(0.16f, 0.76f), new Vector2(0.11f, 0.82f), new Vector2(0.13f, 0.86f), new Vector2(0.12f, 0.88f), new Vector2(0f, 0.88f) };
            foreach (var (c, sc) in new[] { (new Vector3(-6.15f, 0.03f, -1.3f), 1.15f), (new Vector3(-5.6f, 0.03f, -0.75f), 0.9f), (new Vector3(-6.2f, 0.03f, -0.2f), 1.0f) })
            {
                var prof = new Vector2[jar.Length];
                for (int i = 0; i < jar.Length; i++) prof[i] = jar[i] * sc;
                k.Of(clay).AddLathe(c, prof, 16);
            }

            // A rope line in front of the covered exhibits.
            var rope = Mats.Lit(new Color(0.85f, 0.3f, 0.24f), 0.3f, 0f, "fabric_linen");
            float[] posts = { -6.0f, -4.6f, -3.2f, -1.8f };
            foreach (float x in posts)
            {
                k.Of(bronze).AddCylinder(new Vector3(x, 0.03f, -4.25f), 0.03f, 0.92f, 10);
                k.Of(bronze).AddCylinder(new Vector3(x, 0.03f, -4.25f), 0.13f, 0.03f, 14);
                k.Of(bronze).AddSphere(new Vector3(x, 0.98f, -4.25f), 0.045f, 10, 6);
            }
            for (int i = 0; i < posts.Length - 1; i++)
            {
                var path = new System.Collections.Generic.List<Vector3>();
                for (int j = 0; j <= 8; j++)
                {
                    float t = j / 8f;
                    path.Add(new Vector3(Mathf.Lerp(posts[i], posts[i + 1], t), 0.9f - 0.16f * 4f * t * (1f - t), -4.25f));
                }
                k.Of(rope).AddTube(path, 0.018f, 6, false);
            }
            k.Bake(g, "CourtDetail");

            // The pavilion's placard, angled toward the arrival point.
            var sign = new GameObject("PavilionPlacard").transform;
            sign.SetParent(g, false);
            sign.localPosition = new Vector3(1.8f, 0.03f, -4.5f);
            sign.localRotation = Quaternion.Euler(0f, 65f, 0f);
            MeshFactory.Box("Post", sign, new Vector3(0f, 0.5f, 0.05f), new Vector3(0.06f, 1.0f, 0.06f), wood, false);
            MeshFactory.Box("Board", sign, new Vector3(0f, 1.05f, 0f), new Vector3(0.78f, 0.5f, 0.04f), Mats.Lit(new Color(0.7f, 0.6f, 0.52f), 0.4f, 0f, "wood_dark"), false, 1f, Quaternion.Euler(-12f, 0f, 0f));
            var head = TextFactory.World(sign, "PAVILION OF EGYPT", 0.06f, new Color(0.92f, 0.82f, 0.58f), TMPro.TextAlignmentOptions.Center, true, 0.7f);
            head.transform.localPosition = new Vector3(0f, 1.15f, -0.045f);
            head.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            var body = TextFactory.World(sign, "Courtyard of the Sun · festival replica\nPlease keep to the paving", 0.034f, new Color(0.85f, 0.8f, 0.7f), TMPro.TextAlignmentOptions.Center, false, 0.7f);
            body.transform.localPosition = new Vector3(0f, 0.98f, -0.06f);
            body.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
        }
    }

    /// <summary>
    /// Egypt: the temple run. Crossing the finish line before the portal verifies Egypt (which grants Egyptian sunlight)
    /// and the stamp waits in the portal's ring. The wrapped figure follows slowly from behind while the run is on.
    /// </summary>
    public class EgyptEncounter : Encounter
    {
        EgyptCell E => (EgyptCell)Cell;
        bool m_Solved;

        // Read-only state for the guide (Guide/GuideBook.Egypt.cs) and the tests.
        public bool Solved => m_Solved;
        public TempleRun Run => E.Run;

        public override string Objective
        {
            get
            {
                if (m_Solved) return "Step into the portal: your stamp is waiting in the ring.";
                var r = E.Run;
                return "Temple run: run the causeway north to the portal. Change lanes to dodge the mummies that burst out of the tombs, go round the fallen stones, " +
                       "and collect passport coins (" + (r != null ? r.Coins : 0) + " / " + (r != null ? r.CoinTotal : 0) + "). Something follows you from behind: keep moving.";
            }
        }

        public override void OnEnter(bool firstVisit)
        {
            m_Solved = Game.Campaign.IsVerified(CountryId.Egypt);
            var run = E.Run;
            foreach (var v in E.GetComponentsInChildren<VistaInfo>(true)) if (v != run.StampSpot) v.HasStampSpot = false;
            Landmarks.MarkReachable(E, true);
            run.CoinTaken -= OnCoin; run.CoinTaken += OnCoin;
            run.Struck -= OnStruck; run.Struck += OnStruck;
            E.Trace.Hide();
            Game.Passenger?.Despawn();
            if (m_Solved) { run.ShowFinished(); E.Hunt.Stop(); return; }
            run.ResetRun();
            run.Live = true;
            Game.Story?.RestoreVitality(1f);
            Hud.I?.ShowToast("A temple causeway runs north to a portal. Run it: dodge the mummies, go round the stones, grab the passport coins.", 4.5f);
            E.Hunt.Begin(firstVisit ? 8f : 6f);
            Hud.I?.SetObjective(Objective, false);
        }

        public override void OnExit()
        {
            if (E.Run != null) { E.Run.Live = false; E.Run.CoinTaken -= OnCoin; E.Run.Struck -= OnStruck; }
            E.Hunt.Stop();
        }

        void OnCoin(TempleRun r)
        {
            if (!IsLive || m_Solved) return;
            Hud.I?.SetObjective(Objective, false);
            if (r.Coins == 10 || r.Coins == 20 || r.Coins == r.CoinTotal) Hud.I?.ShowToast(r.Coins + " passport coins!", 1.8f);
        }

        void OnStruck(TempleRun r)
        {
            if (!IsLive || m_Solved) return;
            if (r.Hits == 1) Hud.I?.ShowToast("A mummy's grip drains your energy. Change lanes before they reach you; the coins steady you.", 3.5f);
        }

        void Update()
        {
            if (!IsLive || m_Solved || Game.Rig == null || Game.Story == null || Game.Story.Busy || Game.Story.Failing) return;
            var feet = E.transform.InverseTransformPoint(Game.Rig.Feet);
            if (TempleRun.Finished(feet)) Solve();
        }

        void Solve()
        {
            m_Solved = true;
            E.Run.Live = false;
            E.Hunt.Stop();
            AudioDirector.I?.Play("seal_verify", E.Run.Portal.position, 0.8f);
            Hud.I?.ShowToast("You made it! " + E.Run.Coins + " of " + E.Run.CoinTotal + " passport coins. The portal is open: step into it.", 4f);
            Game.Story.VerifyCountry(CountryId.Egypt, null);
        }

        public void OnVerified()
        {
            Hud.I?.ShowToast("Egyptian sunlight will now pour through any tear you open to Egypt.", 4.5f);
        }

        public override void ResetLocal()
        {
            E.Hunt.Stop();
            Game.Passenger?.Despawn();
            if (E.Run != null) E.Run.Live = false;
        }
    }
}

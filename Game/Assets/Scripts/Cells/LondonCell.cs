using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>United Kingdom / London — Stop the Clock. Big Ben's clock face stands on the promenade at one minute to
    /// midnight, its needle spinning: press the big red button while the needle is in the green zone, three times in a row,
    /// while the figure in the clock coat hunts the river walk (run, lose it, come back).</summary>
    public partial class LondonCell : Cell
    {
        public SeamAnchor CheckpointSeam;
        public TraceMark Trace;
        public Transform MinuteHand, BellPivot;
        public StopTheClock Clock;
        public GhostHunt Hunt;

        protected override void Build()
        {
            Id = CellId.London;
            Lighting = new LightingPreset
            {
                // Midnight that reads: moonlit key, a fog lighter than the shapes in it, a horizon that separates the
                // clock tower, the far bank and the terrace roofline, and warm lamps on both sides of the promenade.
                // Ambient and exposure lifted a step (playtest: too dark to read on a monitor).
                SunColor = new Color(0.55f, 0.62f, 0.8f), SunIntensity = 0.48f, SunEuler = new Vector3(42f, 210f, 0f), ShadowStrength = 0.55f,
                AmbientSky = new Color(0.27f, 0.31f, 0.41f), AmbientEquator = new Color(0.22f, 0.22f, 0.26f), AmbientGround = new Color(0.1f, 0.1f, 0.11f),
                FogColor = new Color(0.23f, 0.27f, 0.34f), FogDensity = 0.019f,
                SkyZenith = new Color(0.05f, 0.07f, 0.12f), SkyHorizon = new Color(0.38f, 0.42f, 0.5f), SkyGround = new Color(0.08f, 0.09f, 0.11f),
                SunDiscColor = new Color(0, 0, 0, 0), CloudColor = new Color(0.24f, 0.27f, 0.33f, 0.6f),
                PostExposure = 0.4f, Saturation = -8f, Contrast = 11f, ColorFilter = new Color(0.93f, 0.96f, 1f), Bloom = 0.6f, Vignette = 0.22f
            };
            Ambience = "amb_london_river";
            AddSun();
            var granite = Mats.Lit(new Color(0.42f, 0.42f, 0.44f), 0.7f, 0f, "granite_pavers");
            var brick = Mats.Lit(new Color(0.55f, 0.38f, 0.3f), 0.3f, 0f, "brick_london");
            var metal = Mats.Lit(new Color(0.08f, 0.09f, 0.09f), 0.6f, 0.85f);
            var wood = Mats.Lit(new Color(0.3f, 0.2f, 0.13f), 0.45f, 0f, "wood_dark");
            var g = Group("Embankment");
            // The promenade slab, the river wall and the embankment wall keep their colliders; their visuals (in runs, with
            // the terrace above, the lanterns and the river lamps) are built in Detail_London.cs.
            var promenade = MeshFactory.Floor("Promenade", g, new Vector3(0, 0, 2f), new Vector2(9f, 18f), granite, "stone", 0.3f, 1.2f);
            var riverWall = MeshFactory.Box("RiverWall", g, new Vector3(4.7f, 0.5f, 2f), new Vector3(0.5f, 1f, 18f), granite, true, 1f);
            var brickWall = MeshFactory.Box("BrickWall", g, new Vector3(-4.7f, 2f, 2f), new Vector3(0.6f, 4f, 18f), brick, true, 1.4f);
            foreach (var slab in new[] { promenade, riverWall, brickWall }) slab.GetComponent<MeshRenderer>().enabled = false;
            Kit.Water(g, new Vector3(40f, -1.4f, 2f), new Vector2(70f, 120f), new Color(0.04f, 0.06f, 0.08f));
            // Suspended water: rain that hangs in the air.
            var drops = Kit.Weather(g, new Vector3(0, 2f, 2f), new Vector3(9f, 3.5f, 17f), true, new Color(0.75f, 0.85f, 0.95f, 0.5f), 0f, 0f);
            var em = drops.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 900) });
            var main = drops.main; main.startLifetime = 9999f; main.loop = false;
            // The clock tower across the river, its face showing one minute to midnight.
            var faceTex = Tex.Art("stamp_uk");
            Landmarks.ClockTower(g, new Vector3(120f, -2f, 90f), -60f, Mats.Backdrop(new Color(0.16f, 0.15f, 0.14f), Lighting), Mats.Backdrop(new Color(0.95f, 0.85f, 0.55f), Lighting, new Color(1.1f, 0.9f, 0.5f)));

            // A riverside checkpoint kiosk with a large clock face and a bell.
            var kiosk = new GameObject("Checkpoint").transform;
            kiosk.SetParent(g, false);
            kiosk.localPosition = new Vector3(-2.6f, 0, 7.5f);
            MeshFactory.Box("Kiosk", kiosk, new Vector3(0, 1.3f, 0), new Vector3(2.2f, 2.6f, 1.6f), wood, true);
            MeshFactory.Box("KioskRoof", kiosk, new Vector3(0, 2.7f, 0), new Vector3(2.5f, 0.2f, 1.9f), metal, false);
            MeshFactory.Box("KioskWindow", kiosk, new Vector3(0, 1.45f, -0.81f), new Vector3(1.4f, 0.8f, 0.02f), Mats.Emissive(new Color(0.3f, 0.24f, 0.16f), new Color(0.9f, 0.62f, 0.32f)), false);
            AddPointLight(kiosk, new Vector3(0, 1.6f, -1.3f), new Color(1f, 0.8f, 0.52f), 1.2f, 5f); // the checkpoint reads from the promenade
            MeshFactory.Cylinder("ClockBody", kiosk, new Vector3(1.15f, 2.2f, 0), 0.42f, 0.08f, metal, 24, false).transform.localRotation = Quaternion.Euler(0, 0, 90);
            MeshFactory.Make("ClockFace", MeshFactory.QuadMesh(0.76f, 0.76f), Mats.Unlit(new Color(0.95f, 0.92f, 0.82f), faceTex, false, 0.9f), kiosk, new Vector3(1.24f, 2.2f, 0), Quaternion.Euler(0, -90, 0));
            MinuteHand = new GameObject("MinuteHand").transform;
            MinuteHand.SetParent(kiosk, false);
            MinuteHand.localPosition = new Vector3(1.25f, 2.2f, 0);
            MinuteHand.localRotation = Quaternion.Euler(-6f, -90, 0);
            MeshFactory.Box("Hand", MinuteHand, new Vector3(0, 0.16f, -0.005f), new Vector3(0.02f, 0.32f, 0.01f), metal, false);
            BellPivot = new GameObject("BellPivot").transform;
            BellPivot.SetParent(kiosk, false);
            BellPivot.localPosition = new Vector3(0, 3.2f, 0);
            MeshFactory.Frustum("Bell", BellPivot, new Vector3(0, -0.45f, 0), 0.32f, 0.12f, 0.45f, Mats.Lit(new Color(0.65f, 0.5f, 0.28f), 0.7f, 0.9f), 18);
            MeshFactory.Box("BellFrame", kiosk, new Vector3(0, 3.25f, 0), new Vector3(0.9f, 0.06f, 0.06f), metal, false);

            BuildEmbankmentDetail(g, kiosk, granite, brick);

            CheckpointSeam = AddSeam("checkpoint", new Vector3(-4.3f, 0f, 3f), 90f);
            AddSeam("arrival", new Vector3(0f, 0f, -6.6f), 0f);
            // The game here (Cells/StopTheClock.cs): Big Ben's clock face on a stand in the middle of the promenade, facing
            // the arrival, with the red STOP button on a lectern in front. Stopping it verifies the seal; the trace stays hidden.
            Clock = StopTheClock.Build(g, new Vector3(0.7f, 0f, 4.3f), 0f);
            Trace = TraceMark.Create(transform, CountryId.UnitedKingdom, new Vector3(-1.2f, 0.02f, 3.0f), Quaternion.identity, 0.55f, true);
            // The figure in the clock coat comes down the river walk from the north, past the clock on the river side (in
            // view from the button, and close enough to the arrival that it does not lose a player who has not moved yet).
            Hunt = gameObject.AddComponent<GhostHunt>();
            Hunt.Cell = this;
            Hunt.SpawnLocal = new Vector3(2.6f, 0f, 11f);
            Hunt.RetreatLocal = new Vector3(2.6f, 0f, 15f);
            Hunt.StalkSpeed = 0.55f;
            Hunt.RushEvery = 14f;
            Hunt.RushSeconds = 2.2f;
            Hunt.LoseDistance = 17f;
            Hunt.RestSeconds = 9f;
            Hunt.Arrives = "the figure in the clock coat walks out of the fog";
            SetArrival(new Vector3(0f, 0f, -5.6f), 0f);
            // Bounds: BuildBridgeWalk adds one box round the promenade and the way over the bridge to the far embankment,
            // and keeps the promenade's river-side limit.
            SetEncounter(gameObject.AddComponent<LondonEncounter>());
            Expand_UK.Build(this, g); // Lamplighter Mews behind the terrace (Cells/Expand_UK.cs)
        }
    }

    public class LondonEncounter : Encounter
    {
        LondonCell L => (LondonCell)Cell;
        bool m_Solved;
        float m_RushLeft, m_BellSwing;

        // Read-only state for the guide (Guide/GuideBook.London.cs).
        public bool Solved => m_Solved;
        public int Hits => L.Clock != null ? L.Clock.Hits : 0;

        public override string Objective => m_Solved ? "The clock has stopped at midnight. Run to your stamp across the bridge."
            : "Stop the clock: press the big red button when the spinning needle is in the green zone, three times in a row. When the figure in the clock coat comes, run, lose it, then come back.";

        public override void OnEnter(bool firstVisit)
        {
            m_Solved = Game.Campaign.IsVerified(CountryId.UnitedKingdom);
            L.Clock.Pressed -= OnPressed; L.Clock.Pressed += OnPressed;
            L.Clock.Completed -= OnStopped; L.Clock.Completed += OnStopped;
            L.Trace.Hide();
            Game.Passenger?.Despawn();
            m_RushLeft = 0f;
            if (m_Solved) { L.Clock.ShowStopped(); L.Hunt.Stop(); return; }
            Narration.I?.Say("uk_01", null, false, null, false, true);
            L.Clock.ResetGame();
            Hud.I?.ShowToast("Big Ben's clock face spins on the promenade. Press the red STOP button when the needle is in the green zone: three in a row.", 4.5f);
            L.Hunt.Begin(firstVisit ? 9f : 6f);
            Hud.I?.SetObjective(Objective, false);
        }

        public override void OnExit()
        {
            L.Clock.Pressed -= OnPressed;
            L.Clock.Completed -= OnStopped;
            L.Hunt.Stop();
            m_RushLeft = 0f;
        }

        void OnPressed(StopTheClock c, bool hit)
        {
            if (!IsLive || m_Solved) return;
            if (hit)
            {
                m_BellSwing = 1.2f;
                if (c.Hits < StopTheClock.HitsToWin) Hud.I?.ShowToast("Hit! " + c.Hits + " / " + StopTheClock.HitsToWin + ". The needle speeds up and the green shrinks.", 2.5f);
                return;
            }
            Hud.I?.ShowToast("Missed! Back to 0 / " + StopTheClock.HitsToWin + ". The buzzer carries down the river...", 3f);
            // The buzzer gives you away: the figure rushes at you if it is out, or comes now if it was not.
            var pc = Game.Passenger;
            if (pc != null && pc.Visible && pc.transform.parent == L.transform && Game.Story != null && !Game.Story.Busy && !Game.Story.Failing)
            {
                pc.StartChase();
                m_RushLeft = 2.2f;
                AudioDirector.I?.Play("pass_sting", pc.ChestPosition, 0.8f);
                Hud.I?.ShowCaption("it heard the buzzer and rushes at you: run", 2.5f);
            }
            else
            {
                L.Hunt.Begin(1.2f);
                Hud.I?.ShowCaption("something down the river heard the buzzer", 2.5f);
            }
        }

        void OnStopped(StopTheClock c)
        {
            if (!IsLive || m_Solved) return;
            m_Solved = true;
            m_RushLeft = 0f;
            m_BellSwing = 3f;
            L.Hunt.Stop();
            AudioDirector.I?.Play("seal_verify", c.FaceCenter, 0.7f);
            Hud.I?.ShowToast("You stopped the clock! Midnight at last, and the river starts to move again.", 3.5f);
            Game.Story.VerifyCountry(CountryId.UnitedKingdom, null);
        }

        void Update()
        {
            if (!IsLive) return;
            float dt = Time.deltaTime;
            if (m_RushLeft > 0f)
            {
                // Only the rush a miss started is called off; the stamp chase after the win is the story's.
                m_RushLeft -= dt;
                var pc = Game.Passenger;
                if (m_RushLeft <= 0f && !m_Solved && pc != null && pc.Visible && pc.State == PassengerState.Chase) pc.SetState(PassengerState.Stalk);
            }
            // The checkpoint bell answers each hit.
            if (L.BellPivot != null)
            {
                if (m_BellSwing > 0f) m_BellSwing -= dt;
                float k = Mathf.Clamp01(m_BellSwing / 1.2f);
                L.BellPivot.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 6f) * 16f * k, 0f, 0f);
            }
        }

        public override void ResetLocal()
        {
            L.Hunt.Stop();
            m_RushLeft = 0f;
            Game.Passenger?.Despawn();
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>India / Agra — The Taj Mahal Puzzle. In the garden, with the Taj Mahal standing at the end of the axis, a
    /// picture puzzle of it lies on a table. Put the picture right while the figure from the pool hunts you (run, lose
    /// it, come back) and the way on opens.</summary>
    public partial class AgraCell : Cell
    {
        public TajJigsaw Puzzle;
        public GhostHunt Hunt;
        public TraceMark Trace;

        protected override void Build()
        {
            Id = CellId.Agra;
            Lighting = new LightingPreset
            {
                // Dusk: a low golden sun raking across the walks (the cypresses throw long bars of shade over the stone),
                // a blue-violet sky above a peach horizon, rose haze a little darker than the horizon so the domed
                // pavilion at the end of the axis still reads as a silhouette, lanterns just lit.
                SunColor = new Color(1f, 0.7f, 0.48f), SunIntensity = 1.15f, SunEuler = new Vector3(12f, 70f, 0f), ShadowStrength = 0.72f,
                AmbientSky = new Color(0.46f, 0.5f, 0.68f), AmbientEquator = new Color(0.6f, 0.47f, 0.42f), AmbientGround = new Color(0.27f, 0.23f, 0.19f),
                FogColor = new Color(0.8f, 0.62f, 0.6f), FogDensity = 0.014f,
                SkyZenith = new Color(0.27f, 0.33f, 0.6f), SkyHorizon = new Color(1f, 0.72f, 0.55f), SkyGround = new Color(0.5f, 0.42f, 0.38f),
                SunDiscColor = new Color(1f, 0.8f, 0.55f, 0.9f), SunSize = 0.008f, CloudColor = new Color(1f, 0.76f, 0.68f, 0.3f),
                PostExposure = 0.1f, Saturation = 4f, Contrast = 10f, ColorFilter = new Color(1f, 0.97f, 0.94f), Bloom = 0.65f, Vignette = 0.2f
            };
            Ambience = "amb_india_garden";
            AddSun();
            var marble = Mats.Lit(new Color(0.92f, 0.9f, 0.88f), 0.6f, 0f, "marble_white");
            var redStone = Mats.Lit(new Color(0.9f, 0.55f, 0.55f), 0.25f, 0f, "sandstone_blocks");
            var grass = Mats.Lit(new Color(0.9f, 1f, 0.72f), 0.15f, 0f, "grass_short");
            var brass = Mats.Lit(new Color(0.78f, 0.6f, 0.32f), 0.7f, 0.9f, "metal_brushed");
            var g = Group("Garden");
            // Red sandstone walks (inlaid with marble in Detail_Agra.cs); the lawns run out to the enclosure walls.
            MeshFactory.Floor("Walk", g, new Vector3(0, 0, 3.7f), new Vector2(9f, 22.6f), redStone, "stone", 0.3f, 1.6f);
            MeshFactory.Floor("LawnL", g, new Vector3(-6.9f, -0.02f, 3.85f), new Vector2(4.8f, 23.7f), grass, "grass", 0.3f, 1.5f);
            MeshFactory.Floor("LawnR", g, new Vector3(6.9f, -0.02f, 3.85f), new Vector2(4.8f, 23.7f), grass, "grass", 0.3f, 1.5f);
            // The old solid rim covered the water (and the reflected figure). It keeps its collider (same footing as
            // before); the moulded kerb around the water is built in Detail_Agra.cs, the water sits just under the figure.
            var rim = MeshFactory.Box("PoolRim", g, new Vector3(0, 0.08f, 5f), new Vector3(2.6f, 0.16f, 16.6f), marble, true);
            rim.GetComponent<MeshRenderer>().enabled = false;
            var pool = Kit.Water(g, new Vector3(0, 0.09f, 5f), new Vector2(2.2f, 16f), new Color(0.1f, 0.16f, 0.2f), 0.97f);
            pool.layer = Layers.Default;
            // Cypress rows, beds, pavilions and fountains: see Detail_Agra.cs (the trees keep simple trunk colliders).
            for (int i = 0; i < 6; i++) foreach (var x in new[] { -5.2f, 5.2f })
            {
                var trunkCol = new GameObject("CypressCollider");
                trunkCol.transform.SetParent(g, false);
                trunkCol.transform.localPosition = new Vector3(x, 1.2f, -3f + i * 3.4f);
                trunkCol.AddComponent<CapsuleCollider>().radius = 0.35f;
            }
            BuildGardenDetail(g, marble, redStone, brass);
            Landmarks.DomedPavilion(g, new Vector3(0, -2f, 230f), 0f, Mats.Backdrop(new Color(0.93f, 0.9f, 0.9f), Lighting));
            // Suspended petals (frozen in the air).
            var petals = Kit.Weather(g, new Vector3(0, 1.6f, 4f), new Vector3(9f, 2.5f, 18f), false, new Color(0.98f, 0.72f, 0.75f, 0.9f), 0f, 0f);
            var em = petals.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 350) });
            var main = petals.main; main.startLifetime = 9999f; main.loop = false;
            Trace = TraceMark.Create(transform, CountryId.India, new Vector3(2.4f, 0.02f, 0.9f), Quaternion.identity, 0.5f, true);

            // The Taj Mahal picture puzzle on a table beside the pool, the domed mausoleum straight ahead down the axis
            // (Cells/CountryGames.cs; the picture is rendered in Blender: Art/taj_puzzle.png).
            Puzzle = TajJigsaw.Build(g, new Vector3(2.4f, 0f, 2.0f), 0f);
            // The figure rises out of the pool and hunts the garden while the puzzle is worked.
            Hunt = gameObject.AddComponent<GhostHunt>();
            Hunt.Cell = this;
            Hunt.SpawnLocal = new Vector3(-0.6f, 0f, 11.5f);
            Hunt.RetreatLocal = new Vector3(0f, 0f, 16f);
            Hunt.SpawnState = PassengerState.Rise;
            Hunt.StalkSpeed = 0.6f;
            Hunt.RushEvery = 14f;
            Hunt.RushSeconds = 2.4f;
            Hunt.LoseDistance = 14f;
            Hunt.RestSeconds = 9f;
            Hunt.Arrives = "water drips onto marble: something climbs out of the pool";

            AddSeam("arrival", new Vector3(0f, 0f, -5.6f), 0f);
            AddSeam("lawn", new Vector3(-4.4f, 0f, 6f), 90f);
            SetArrival(new Vector3(0f, 0f, -4.6f), 0f);
            // Open world: the garden opens north (threshold at z 14.35..15) onto the vista's marble walk to the domed pavilion.
            // One box from the arrival end to just past the stamp spot (3, 1.45, 166): x +-12 keeps the player on the walks
            // and lawns between the vista's cypress rows and its broadleaf trees, and leaves room to walk round the raised
            // tank at z 91..109 (x +-9, 0.9 m high). Inside the garden the enclosure's colliders hold the old line.
            AddEnclosureColliders(g);
            AddBounds(new Vector3(0, 0, (-6.5f + 170f) / 2f), new Vector3(24f, 5f, 170f + 6.5f));
            SetEncounter(gameObject.AddComponent<AgraEncounter>());
            Landmarks.MarkReachable(this); // the way to the stamp spot is open
            Expand_India.Build(this, transform); // the water garden east of the long walk (Cells/Expand_India.cs)
        }
    }

    public class AgraEncounter : Encounter
    {
        AgraCell A => (AgraCell)Cell;
        bool m_Solved;
        // Read-only state for the guide (Guide/GuideBook.cs).
        public bool Solved => m_Solved;

        public override string Objective => m_Solved ? "The picture is whole. Run to your stamp at the Taj Mahal."
            : "Put the Taj Mahal picture together on the table: pick up each of the 6 pieces and bring it to its place on the board, where it snaps in. When the figure comes, run, lose it, then come back.";

        public override void OnEnter(bool firstVisit)
        {
            m_Solved = Game.Campaign.IsVerified(CountryId.India);
            A.Puzzle.Completed -= OnCompleted; A.Puzzle.Completed += OnCompleted;
            A.Trace.Hide();
            Game.Passenger?.Despawn();
            if (m_Solved) { A.Puzzle.ShowSolved(); A.Hunt.Stop(); return; }
            if (!A.Puzzle.Scrambled || A.Puzzle.Solved) A.Puzzle.Scramble();
            Hud.I?.ShowToast("A picture puzzle of the Taj Mahal lies on the table by the pool. Put it right to go on.", 4.5f);
            A.Hunt.Begin(firstVisit ? 10f : 6f);
            Hud.I?.SetObjective(Objective, false);
        }

        public override void OnExit()
        {
            A.Puzzle.Completed -= OnCompleted;
            A.Hunt.Stop();
        }

        void OnCompleted(TajJigsaw j)
        {
            if (!IsLive || m_Solved) return;
            m_Solved = true;
            A.Hunt.Stop();
            AudioDirector.I?.Play("seal_verify", j.BoardPosition, 0.8f);
            PostFx.I?.Flash(0.3f);
            Hud.I?.ShowToast("The picture is whole, and the Taj Mahal glows at the end of the garden.", 3.5f);
            Game.Story.VerifyCountry(CountryId.India, null);
        }

        public override void ResetLocal()
        {
            A.Hunt.Stop();
            Game.Passenger?.Despawn();
        }
    }

    /// <summary>Brazil / Rio — Carnival Ring Toss. The carnival is frozen except one drum; a ring toss stall stands on the
    /// avenue in front of the float (Cells/RioRingToss.cs). Land five rings on the sliding bottles while the figure hunts
    /// the avenue (run, lose it, come back) and the stall gives you your passport stamp.</summary>
    public partial class RioCell : Cell
    {
        public Transform Beater, FloatShadow;
        public RingToss Toss;
        public GhostHunt Hunt;
        public const float BeatInterval = 1.25f;
        public TraceMark Trace;

        protected override void Build()
        {
            Id = CellId.Rio;
            Lighting = new LightingPreset
            {
                // Carnival night that reads at a glance: warm floodlights and coloured spots do the key work, a lifted
                // violet ambient fills the shadows, the horizon glows with the city behind the palms, a magenta haze.
                // Colour stays saturated; only the stillness is wrong.
                SunColor = new Color(0.62f, 0.62f, 0.95f), SunIntensity = 0.35f, SunEuler = new Vector3(50f, 30f, 0f), ShadowStrength = 0.5f,
                AmbientSky = new Color(0.4f, 0.34f, 0.58f), AmbientEquator = new Color(0.46f, 0.3f, 0.4f), AmbientGround = new Color(0.17f, 0.12f, 0.15f),
                FogColor = new Color(0.3f, 0.16f, 0.3f), FogDensity = 0.012f,
                SkyZenith = new Color(0.04f, 0.04f, 0.13f), SkyHorizon = new Color(0.62f, 0.3f, 0.42f), SkyGround = new Color(0.12f, 0.08f, 0.12f),
                SunDiscColor = new Color(0, 0, 0, 0), Stars = 0.25f,
                PostExposure = 0.55f, Saturation = 12f, Contrast = 12f, ColorFilter = new Color(1f, 0.97f, 1f), Bloom = 0.8f, Vignette = 0.22f
            };
            Ambience = "amb_rio_frozen";
            AddSun();
            var paving = Mats.Lit(new Color(0.93f, 0.92f, 0.97f), 0.5f, 0f, "granite_pavers");
            var concrete = Mats.Lit(new Color(0.55f, 0.55f, 0.55f), 0.25f, 0f, "concrete_panels");
            var g = Group("Avenue");
            // Pale granite paving (the float's shadow must read on it), in three pieces so each one's lights stay local;
            // the wave mosaic on the plaza and promenades is laid over it in Detail_Rio.cs.
            for (int i = 0; i < 3; i++)
                MeshFactory.Floor("Avenue" + i, g, new Vector3(0, 0, -7f + 22f / 6f + i * 22f / 3f), new Vector2(10f, 22f / 3f), paving, "stone", 0.3f, 2.4f);
            // Bleachers on both sides.
            for (int side = -1; side <= 1; side += 2)
                for (int step = 0; step < 5; step++)
                    MeshFactory.Box("Bleacher", g, new Vector3(side * (5.6f + step * 0.6f), 0.25f + step * 0.5f, 4f), new Vector3(0.6f, 0.5f + step * 1f, 22f), concrete, true, 1.5f);
            Landmarks.Skyline(g, new Vector3(0, -6f, 0), 200f, 80f, 0f, 12, Mats.Backdrop(new Color(0.08f, 0.08f, 0.12f), Lighting), Mats.Backdrop(new Color(1f, 0.8f, 0.55f), Lighting, new Color(1f, 0.7f, 0.4f)), 51);
            // Floodlights on poles.
            foreach (var x in new[] { -4.6f, 4.6f })
                for (int i = 0; i < 2; i++)
                {
                    MeshFactory.Cylinder("Pole", g, new Vector3(x, 0, 0f + i * 9f), 0.1f, 6f, concrete, 8);
                    AddSpotLight(g, new Vector3(x, 6f, 0f + i * 9f), new Vector3(62f, x < 0 ? 60f : -60f, 0f), new Color(1f, 0.85f, 0.7f), 6.5f, 18f, 70f);
                }
            // The parade float: a desaturated platform with stylised feathers/fans and the one running drum.
            var floatRoot = new GameObject("Float").transform;
            floatRoot.SetParent(g, false);
            floatRoot.localPosition = new Vector3(0, 0, 9f);
            var colors = new[] { new Color(0.6f, 0.45f, 0.3f), new Color(0.35f, 0.5f, 0.42f), new Color(0.52f, 0.4f, 0.55f) };
            MeshFactory.Box("Platform", floatRoot, new Vector3(0, 0.6f, 0), new Vector3(4.6f, 1.2f, 3.4f), Mats.Lit(colors[0], 0.4f), true);
            for (int i = 0; i < 7; i++)
            {
                var fan = MeshFactory.Box("Fan", floatRoot, new Vector3(-2f + i * 0.66f, 2.4f, 1.2f), new Vector3(0.45f, 2.2f, 0.06f), Mats.Lit(colors[i % 3], 0.5f), false, 1f, Quaternion.Euler(0, 0, -30f + i * 10f));
            }
            // The one running drum: a chrome surdo (rims, head and rods in Detail_RioFloat.cs) and a felt mallet whose
            // pivot sits so the head meets the drum skin on the beat instead of sinking into it.
            var drum = MeshFactory.Cylinder("Drum", floatRoot, new Vector3(1.2f, 1.2f, -0.6f), 0.45f, 0.6f, Mats.Lit(new Color(0.82f, 0.83f, 0.86f), 0.8f, 0.85f), 20, true);
            Beater = new GameObject("Beater").transform;
            Beater.SetParent(floatRoot, false);
            Beater.localPosition = new Vector3(1.2f, 2.5f, -0.6f);
            MeshFactory.Box("Arm", Beater, new Vector3(0, -0.3f, 0), new Vector3(0.05f, 0.6f, 0.05f), Mats.Lit(new Color(1f, 0.9f, 0.8f), 0.4f, 0f, "wood_dark"), false);
            MeshFactory.Sphere("Head", Beater, new Vector3(0, -0.62f, 0), 0.07f, Mats.Lit(new Color(0.95f, 0.94f, 0.9f), 0.3f));
            // Frozen confetti (glinting specks; the paper pieces are geometry in Detail_Rio.cs), in carnival colours.
            var confetti = Kit.Weather(g, new Vector3(0, 2f, 4f), new Vector3(9f, 3f, 18f), false, new Color(0.9f, 0.75f, 0.55f, 0.9f), 0f, 0f);
            var em = confetti.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 500) });
            var confettiColours = new Gradient { mode = GradientMode.Fixed };
            confettiColours.SetKeys(new[]
            {
                new GradientColorKey(new Color(1f, 0.25f, 0.7f), 0.2f), new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0.4f), new GradientColorKey(new Color(0.2f, 0.9f, 0.9f), 0.6f),
                new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0.8f), new GradientColorKey(new Color(0.7f, 0.4f, 1f), 1f),
            }, new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.9f, 1f) });
            var main = confetti.main; main.startLifetime = 9999f; main.loop = false;
            main.startColor = new ParticleSystem.MinMaxGradient(confettiColours) { mode = ParticleSystemGradientMode.RandomColor };
            main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.04f);

            // The game: a carnival ring toss stall on the avenue in front of the float (Cells/RioRingToss.cs). The trace
            // stays hidden: five ringers verify the seal. No new lights: the carnival's meshes keep exactly their eight.
            Toss = RingToss.Build(g, new Vector3(-0.4f, 0f, 2.6f), 0f);
            Trace = TraceMark.Create(transform, CountryId.Brazil, new Vector3(-0.4f, 0.02f, 1.6f), Quaternion.identity, 0.3f, true);
            // The float's shadow that moves on its own (between the stall and the float).
            FloatShadow = MeshFactory.Quad("FloatShadow", g, 2.6f, 1.6f, Mats.UnlitInstance(new Color(0f, 0f, 0f, 0.65f), Tex.SoftDot, true, 1f), new Vector3(0, 0.013f, 7.4f), Quaternion.Euler(90, 0, 0)).transform;
            // The figure hunts the avenue while the stall is played: it comes in from the arrival plaza.
            Hunt = gameObject.AddComponent<GhostHunt>();
            Hunt.Cell = this;
            Hunt.SpawnLocal = new Vector3(3.9f, 0f, -3.8f);
            Hunt.RetreatLocal = new Vector3(-3.9f, 0f, 11.5f);
            Hunt.StalkSpeed = 0.55f;
            Hunt.RushEvery = 14f;
            Hunt.RushSeconds = 2.4f;
            Hunt.LoseDistance = 12f;
            Hunt.RestSeconds = 9f;
            Hunt.Arrives = "one of the frozen dancers is walking";

            BuildCarnivalDetail(g, floatRoot);
            AddSeam("arrival", new Vector3(0f, 0f, -5.6f), 0f);
            AddSeam("stand", new Vector3(4.6f, 0f, 3f), -90f);
            SetArrival(new Vector3(0f, 0f, -4.6f), 0f);
            AddBounds(new Vector3(0, 0, 4f), new Vector3(9f, 5f, 20f));
            SetEncounter(gameObject.AddComponent<RioEncounter>());
            Expand_Brazil.Build(this, transform); // the hillside lane past the parade (Cells/Expand_Brazil.cs)
        }
    }

    public class RioEncounter : Encounter
    {
        RioCell R => (RioCell)Cell;
        // Read-only state for the guide (Guide/GuideBook.Rio.cs) and the tests.
        public bool Solved => m_Solved;
        public int Score => R.Toss != null ? R.Toss.Score : 0;
        bool m_Solved;
        float m_Clock;

        public override string Objective => m_Solved ? "Five ringers! Run to your passport stamp."
            : "Carnival ring toss: aim over the counter and throw rings onto the sliding bottles (" + Score + "/" + RingToss.Target + "). When the figure comes, run, lose it, then come back.";

        public override void OnEnter(bool firstVisit)
        {
            m_Solved = Game.Campaign.IsVerified(CountryId.Brazil);
            R.Toss.Scored -= OnScored; R.Toss.Scored += OnScored;
            R.Trace.Hide();
            Game.Passenger?.Despawn();
            if (m_Solved) { R.Toss.SetSolved(); R.Hunt.Stop(); return; }
            R.Toss.ResetGame();
            Narration.I?.Say("bra_01", null, false, null, false, true); // "holding its breath, except the drum": still true
            Hud.I?.ShowToast("A ring toss stall on the frozen carnival avenue. Land five rings on the sliding bottles to win your passport stamp.", 4.5f);
            R.Hunt.Begin(firstVisit ? 10f : 6f);
            Hud.I?.SetObjective(Objective, false);
        }

        public override void OnExit()
        {
            R.Toss.Scored -= OnScored;
            R.Hunt.Stop();
        }

        void OnScored(RingToss t, int row)
        {
            if (!IsLive || m_Solved) return;
            if (!t.Won)
            {
                Hud.I?.ShowToast("Ringer! " + t.Score + " / " + RingToss.Target, 1.6f);
                Hud.I?.SetObjective(Objective, false);
                return;
            }
            m_Solved = true;
            R.Hunt.Stop();
            AudioDirector.I?.Play("seal_verify", t.TargetsPosition, 0.8f);
            AudioDirector.I?.Play("clock_chime", t.TargetsPosition, 0.6f, 1.4f);
            PostFx.I?.Flash(0.3f);
            Hud.I?.ShowToast("Five ringers! The stall's bell rings: the stamp is yours.", 3.5f);
            Game.Story.VerifyCountry(CountryId.Brazil, null);
        }

        void Update()
        {
            if (!IsLive) return;
            float dt = Time.deltaTime;
            // The one running drum keeps its beat (scenery now).
            float before = m_Clock % RioCell.BeatInterval;
            m_Clock += dt;
            float after = m_Clock % RioCell.BeatInterval;
            if (after < before) AudioDirector.I?.Play("drum_hit", R.Beater.position, 0.8f);
            float phase = after / RioCell.BeatInterval;
            R.Beater.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-35f, 0f, Mathf.Pow(phase, 4f)));
            // The float's shadow drifts on its own between the stall and the float.
            var target = new Vector3(Mathf.Sin(Time.time * 0.21f) * 2.2f, 0.013f, 7.9f + Mathf.Cos(Time.time * 0.17f) * 1.1f);
            R.FloatShadow.localPosition = Vector3.MoveTowards(R.FloatShadow.localPosition, target, dt * 0.4f);
        }

        public override void ResetLocal()
        {
            R.Hunt.Stop();
            Game.Passenger?.Despawn();
        }
    }
}

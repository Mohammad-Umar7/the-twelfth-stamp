using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>Japan — The Claw Machine. In the lantern street a claw machine holds a golden passport; win it while the
    /// lantern figure hunts you (run, lose it, come back), and the gate out of Japan opens.</summary>
    public partial class JapanCell : Cell
    {
        public readonly List<Transform> ProcessionShadows = new List<Transform>();
        public ClawMachine Claw;
        public GhostHunt Hunt;
        public TraceMark Trace;

        protected override void Build()
        {
            Id = CellId.Japan;
            Lighting = new LightingPreset
            {
                // Lantern-lit night that still reads on a monitor: warm chochin pools down the street, a cool moon raking in
                // from behind the arrival (the left-hand fronts and the gate catch it, the right-hand side stays in lantern
                // light), a lifted ambient, and a dusky horizon so roofs, poles and wires stand out against the sky.
                SunColor = new Color(0.55f, 0.62f, 0.85f), SunIntensity = 0.6f, SunEuler = new Vector3(34f, 335f, 0f), ShadowStrength = 0.6f,
                AmbientSky = new Color(0.3f, 0.31f, 0.43f), AmbientEquator = new Color(0.31f, 0.25f, 0.25f), AmbientGround = new Color(0.12f, 0.1f, 0.09f),
                FogColor = new Color(0.17f, 0.14f, 0.19f), FogDensity = 0.018f,
                SkyZenith = new Color(0.045f, 0.05f, 0.1f), SkyHorizon = new Color(0.36f, 0.24f, 0.32f), SkyGround = new Color(0.07f, 0.05f, 0.06f),
                SunDiscColor = new Color(0, 0, 0, 0), Stars = 0.45f,
                PostExposure = 0.45f, Saturation = 2f, Contrast = 10f, ColorFilter = new Color(1f, 0.94f, 0.88f), Bloom = 0.65f, Vignette = 0.24f
            };
            Ambience = "amb_japan_night";
            AddSun();
            var slate = Mats.Lit(new Color(0.64f, 0.64f, 0.68f), 0.72f, 0f, "granite_pavers");
            var wood = Mats.Lit(new Color(0.24f, 0.17f, 0.12f), 0.4f, 0f, "wood_dark");
            var postWood = Mats.Lit(new Color(0.8f, 0.78f, 0.76f), 0.35f, 0f, "wood_dark");
            var lanternMat = Mats.Lit(new Color(0.95f, 0.75f, 0.5f), 0.1f, 0f, "paper_shoji", false, new Color(1.6f, 0.75f, 0.3f));
            var g = Group("Street");
            // The street's collider and footstep surface (BuildStreetDetail draws the stone in pieces and hides this mesh).
            MeshFactory.Floor("Paving", g, new Vector3(0, 0, 3f), new Vector2(7f, 20f), slate, "stone", 0.3f, 1f);
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 4; i++)
                {
                    // Each house stays solid for the rig and the Passenger; its front, chochin and light are built in BuildStreetDetail.
                    var house = new GameObject("House").transform;
                    house.SetParent(g, false);
                    house.localPosition = new Vector3(side * 3.8f, 0, -5f + i * 4.6f);
                    var wall = house.gameObject.AddComponent<BoxCollider>();
                    wall.center = new Vector3(side * 0.3f, 1.6f, 0);
                    wall.size = new Vector3(0.6f, 3.2f, 4.4f);
                }
            // The timber gate at the end of the street (shut until the puzzle is solved) and the lane beyond it are built in
            // BuildStreetDetail / BuildRoute, with their colliders.
            Landmarks.Pagoda(g, PagodaPos, 0f, Mats.Backdrop(new Color(0.14f, 0.1f, 0.12f), Lighting), Mats.Backdrop(new Color(0.22f, 0.12f, 0.12f), Lighting));

            // Shadows of an absent procession drift across the paving.
            var shadowMat = Mats.UnlitInstance(new Color(0f, 0f, 0f, 0.55f), Tex.SoftDot, true, 1f);
            for (int i = 0; i < 6; i++)
            {
                var s = MeshFactory.Quad("AbsentShadow", g, 0.6f, 1.9f, shadowMat, new Vector3(-1.5f + i * 0.6f, 0.012f, -2f + i * 2.2f), Quaternion.Euler(90, 0, 0));
                s.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ProcessionShadows.Add(s.transform);
            }

            // The claw machine on the left of the street, facing the arrival (Cells/CountryGames.cs). The golden passport
            // inside it is the way out of Japan. The trace stays hidden: winning the cat verifies the seal.
            Claw = ClawMachine.Build(g, new Vector3(-2.0f, 0f, 8.2f), 0f);
            Trace = TraceMark.Create(transform, CountryId.Japan, new Vector3(-2.0f, 0.02f, 6.6f), Quaternion.identity, 0.6f, true);
            // The lantern figure hunts the street while the machine is played: it comes in through the end gate.
            Hunt = gameObject.AddComponent<GhostHunt>();
            Hunt.Cell = this;
            Hunt.SpawnLocal = new Vector3(0.4f, 0f, 12.4f);
            Hunt.RetreatLocal = new Vector3(0.4f, 0f, 12.6f);
            Hunt.StalkSpeed = 0.55f;
            Hunt.RushEvery = 13f;
            Hunt.RushSeconds = 2.2f;
            Hunt.LoseDistance = 12f;
            Hunt.RestSeconds = 9f;
            Hunt.Arrives = "the lantern figure steps through the gate";

            BuildStreetDetail(g, postWood);
            Expand_Japan.Build(this, g); // the tea garden off the lantern approach, street crows, lane lamps (Cells/Expand_Japan.cs)
            AddSeam("arrival", new Vector3(0f, 0f, -6.6f), 0f);
            AddSeam("exit", new Vector3(0f, 0f, 12.9f), 180f);
            SetArrival(new Vector3(0f, 0f, -5.6f), 0f);
            // Open world: one box round the street, the lane past the end gate and the precinct up to just past the stamp
            // spot on the lantern approach. The street keeps its old edges (walls of its own, BuildRoute), the lane is walled,
            // and the end gate stays shut until the lanterns tell the truth.
            AddBounds(new Vector3(-3.7f, -1.5f, 50.5f), new Vector3(16.6f, 7.5f, 113f), 7.5f);
            SetEncounter(gameObject.AddComponent<JapanEncounter>());
            Landmarks.MarkReachable(this);
        }
    }

    public class JapanEncounter : Encounter
    {
        JapanCell J => (JapanCell)Cell;
        bool m_Solved;

        // Read-only state for the guide (Guide/GuideBook.cs).
        public bool Solved => m_Solved;

        public override string Objective => m_Solved ? "The gate is open. Run to your stamp at the pagoda."
            : J.Claw.GoldenOut ? "Take the golden passport from the prize tray."
            : "Win the golden passport from the claw machine: arrows to line the claw up, then DROP. When the lantern figure comes, run, lose it, then come back.";

        public override void OnEnter(bool firstVisit)
        {
            m_Solved = Game.Campaign.IsVerified(CountryId.Japan);
            // The end gate stands open once the seal is verified; the stamp is only offered out at the pagoda while the
            // whole way there (lane, precinct gate, approach) is actually walkable.
            J.Gate?.SetOpen(m_Solved, true);
            Landmarks.MarkReachable(J, J.RouteOpen());
            J.Claw.Delivered -= OnDelivered; J.Claw.Delivered += OnDelivered;
            J.Claw.GoldenTaken -= OnTaken; J.Claw.GoldenTaken += OnTaken;
            J.Trace.Hide();
            Game.Passenger?.Despawn();
            if (m_Solved) { J.Claw.SetSolved(); J.Hunt.Stop(); return; }
            if (!J.Claw.GoldenOut) J.Claw.ResetGame();
            else J.Claw.ReturnGolden(); // won earlier but never counted: back on the tray, not left where it was dropped
            Hud.I?.ShowToast("A claw machine glows in the lantern street. The golden passport inside is your way out of Japan.", 4.5f);
            J.Hunt.Begin(firstVisit ? 9f : 6f);
            Hud.I?.SetObjective(Objective, false);
        }

        public override void OnExit()
        {
            J.Claw.Delivered -= OnDelivered;
            J.Claw.GoldenTaken -= OnTaken;
            J.Hunt.Stop();
        }

        void OnDelivered(ClawMachine m, bool golden)
        {
            if (!IsLive || m_Solved) return;
            if (golden) Hud.I?.ShowToast("The golden passport drops into the prize tray. Grab it!", 3f);
            Hud.I?.SetObjective(Objective, false);
        }

        void OnTaken(Grabbable g)
        {
            if (!IsLive || m_Solved) return;
            m_Solved = true;
            J.Hunt.Stop();
            AudioDirector.I?.Play("seal_verify", g.transform.position, 0.7f);
            Hud.I?.ShowToast("The golden passport glows, and the gate at the end of the street answers.", 3.5f);
            if (J.Gate != null && !J.Gate.IsOpen)
            {
                // The way on opens: the end gate swings out onto the lane toward the pagoda.
                J.Gate.SetOpen(true, false);
                AudioDirector.I?.Play("door_open", J.Gate.Blocker.transform.position, 0.8f);
                Hud.I?.ShowCaption("the gate at the end of the street swings open", 3f);
            }
            Landmarks.MarkReachable(J, true);
            Game.Story.VerifyCountry(CountryId.Japan, null);
        }

        void Update()
        {
            if (!IsLive) return;
            // Holding the cat is what counts, however it got into the hand.
            if (!m_Solved && J.Claw.GoldenHeld) OnTaken(J.Claw.Golden);
            // The absent procession walks slowly toward the gate.
            foreach (var s in J.ProcessionShadows)
            {
                var p = s.localPosition;
                p.z += Time.deltaTime * 0.35f;
                if (p.z > 11f) p.z = -5f;
                p.x += Mathf.Sin(Time.time * 0.7f + p.z) * Time.deltaTime * 0.05f;
                s.localPosition = p;
            }
        }

        public override void ResetLocal()
        {
            J.Hunt.Stop();
            Game.Passenger?.Despawn();
            if (!m_Solved) J.Claw.ReturnGolden();
        }
    }

    /// <summary>Saudi Arabia / Riyadh — The Maze. A walled mud-brick maze at dusk: the heat drains your energy, water
    /// glasses keep you going, the figure follows you through the walls, and a portal past the far end carries you on.</summary>
    public partial class RiyadhCell : Cell
    {
        public SandMaze Maze;
        public GhostHunt Hunt;
        public TraceMark Trace;
        public const float DrainPerSecond = 1f / 60f;

        protected override void Build()
        {
            Id = CellId.Riyadh;
            Lighting = new LightingPreset
            {
                // Dusk with the sun low behind the arrival's right shoulder: it lights the arches' faces and the west houses,
                // throws long shadows forward across the plaza, and leaves the sky warm over the skyline; lamps and lit vents
                // come on as practicals. (The east side is kept low so this sun still reaches the plaza.)
                SunColor = new Color(1f, 0.68f, 0.46f), SunIntensity = 1.15f, SunEuler = new Vector3(13f, 302f, 0f), ShadowStrength = 0.72f,
                AmbientSky = new Color(0.42f, 0.4f, 0.52f), AmbientEquator = new Color(0.52f, 0.4f, 0.33f), AmbientGround = new Color(0.24f, 0.17f, 0.13f),
                FogColor = new Color(0.56f, 0.42f, 0.38f), FogDensity = 0.011f,
                SkyZenith = new Color(0.13f, 0.16f, 0.33f), SkyHorizon = new Color(0.96f, 0.57f, 0.4f), SkyGround = new Color(0.36f, 0.25f, 0.2f),
                SunDiscColor = new Color(1f, 0.62f, 0.36f, 1f), SunSize = 0.007f, CloudColor = new Color(0.95f, 0.62f, 0.5f, 0.22f),
                PostExposure = 0.15f, Saturation = 4f, Contrast = 10f, ColorFilter = new Color(1f, 0.95f, 0.9f), Bloom = 0.6f, Vignette = 0.22f
            };
            Ambience = "amb_riyadh_wind";
            AddSun();
            var plaza = Mats.Lit(new Color(0.84f, 0.74f, 0.6f), 0.3f, 0f, "plaza_stone");
            var stone = Mats.Lit(new Color(0.82f, 0.7f, 0.56f), 0.2f, 0f, "sandstone_blocks");
            var metal = Mats.Lit(new Color(0.65f, 0.6f, 0.55f), 0.7f, 0.9f, "metal_brushed");
            var g = Group("Plaza");
            MeshFactory.Floor("Plaza", g, new Vector3(0, 0, 3f), new Vector2(16f, 16f), plaza, "stone", 0.3f, 1.6f);
            Landmarks.ArchTower(g, TowerPos, 0f, Mats.Backdrop(new Color(0.2f, 0.2f, 0.28f), Lighting), Mats.Backdrop(new Color(0.6f, 0.7f, 1f), Lighting, new Color(0.6f, 0.8f, 1.6f)));
            Landmarks.Skyline(g, new Vector3(0, -3f, 0), 230f, 60f, 0f, 12, Mats.Backdrop(new Color(0.25f, 0.22f, 0.3f), Lighting), null, 41);
            BuildPlazaDetail(g);
            Expand_Riyadh.Build(this, g); // the coffee court outside the north gatehouse, plaza doves (Cells/Expand_Riyadh.cs)

            // The game here is the maze (Cells/CountryGames.cs), built well south of the old plaza: the player arrives in it,
            // crosses it with their energy draining (water glasses on the way) while the figure follows through the walls,
            // and the portal past its north end carries them on. The stamp waits in the portal ring, so it is the only
            // stamp spot in this cell.
            Maze = SandMaze.Build(transform, new Vector3(420f, 0f, -8f), Lighting); // far out in open desert: nearer in, unseen city blocks stood inside the maze (playtest: "no correct path")
            foreach (var v in GetComponentsInChildren<VistaInfo>(true)) if (v != Maze.StampSpot) v.HasStampSpot = false;
            Trace = TraceMark.Create(transform, CountryId.SaudiArabia, Maze.PortalLocal + new Vector3(0f, 0.02f, -1.5f), Quaternion.identity, 0.6f, true);
            var start = Maze.CellCenter(Maze.Start);
            Hunt = gameObject.AddComponent<GhostHunt>();
            Hunt.Cell = this;
            Hunt.SpawnLocal = start + new Vector3(0f, 0f, -7f);
            Hunt.RetreatLocal = start + new Vector3(6f, 0f, -7f);
            Hunt.StalkSpeed = 0.4f;
            Hunt.RushEvery = 0f;           // one slow ghost, straight through the walls
            Hunt.LoseDistance = 9999f;
            Hunt.Arrives = "something walks through the maze walls behind you";
            AddSeam("arrival", start + new Vector3(0f, 0f, -1.1f), 0f);
            AddSeam("plaza", Maze.PortalLocal + new Vector3(-2.4f, 0f, -2.5f), 90f);
            SetArrival(start, 0f);
            // Open world: one box round the plaza and the run north up the tower's sight line to just past the stamp spot.
            // The plaza keeps its south and west edges; the east parapet, the north wall and the gatehouse doors (shut until
            // the honest arch is walked through) have colliders of their own (RouteColliders, NorthGate).
            AddBounds(new Vector3(5.65f, -0.5f, 81.15f), new Vector3(26.7f, 6f, 171.7f), 6f);
            SetEncounter(gameObject.AddComponent<RiyadhEncounter>());
            Landmarks.MarkReachable(this);
        }
    }

    public class RiyadhEncounter : Encounter
    {
        RiyadhCell R => (RiyadhCell)Cell;
        bool m_Solved;
        float m_DrinkCooldown;
        // Read-only state for the guide (Guide/GuideBook.cs).
        public bool Solved => m_Solved;

        public override string Objective => m_Solved ? "Step into the portal: your stamp is waiting in the ring."
            : "Cross the maze to the portal at its far end. The heat drains your energy: drink the glowing water glasses to stay alive. Something follows you through the walls.";

        public override void OnEnter(bool firstVisit)
        {
            m_Solved = Game.Campaign.IsVerified(CountryId.SaudiArabia);
            R.Gate?.SetOpen(m_Solved, true);
            foreach (var v in R.GetComponentsInChildren<VistaInfo>(true)) if (v != R.Maze.StampSpot) v.HasStampSpot = false;
            Landmarks.MarkReachable(R, true);
            R.Trace.Hide();
            Game.Passenger?.Despawn();
            if (m_Solved) { R.Hunt.Stop(); return; }
            R.Maze.Refill();
            Game.Story?.RestoreVitality(1f);
            Hud.I?.ShowToast("A maze of mud-brick walls. Your energy drains in the heat: find the water, find the portal.", 4.5f);
            R.Hunt.Begin(firstVisit ? 7f : 5f);
            Hud.I?.SetObjective(Objective, false);
        }

        public override void OnExit() => R.Hunt.Stop();

        void Update()
        {
            if (!IsLive || m_Solved || Game.Rig == null || Game.Story == null || Game.Story.Busy || Game.Story.Failing) return;
            var feet = R.transform.InverseTransformPoint(Game.Rig.Feet);
            if (R.Maze.Escaped(feet)) { Solve(); return; }
            Game.Story.DrainVitality(RiyadhCell.DrainPerSecond * Time.deltaTime);
            m_DrinkCooldown -= Time.deltaTime;
            if (m_DrinkCooldown <= 0f && R.Maze.TryDrink(feet))
            {
                m_DrinkCooldown = 0.5f;
                Game.Story.RestoreVitality(0.45f);
                AudioDirector.I?.Play("breath_relief_1", Game.Rig.Head.position, 0.8f);
                AudioDirector.I?.Play("drop_soft", Game.Rig.Feet, 0.5f, 1.4f);
                Hud.I?.ShowToast("You drink the water. Energy restored.", 2f);
            }
        }

        void Solve()
        {
            m_Solved = true;
            R.Hunt.Stop();
            AudioDirector.I?.Play("seal_verify", R.Maze.Portal.position, 0.8f);
            Hud.I?.ShowToast("Out of the maze! The portal is open: step into it.", 3.5f);
            Game.Story.VerifyCountry(CountryId.SaudiArabia, null);
        }

        public override void ResetLocal()
        {
            R.Hunt.Stop();
            Game.Passenger?.Despawn();
        }
    }
}

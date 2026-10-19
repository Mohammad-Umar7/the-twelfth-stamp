using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// In-build automated check (--autotest=demo|egypt|story|countries|finale|all). Drives the REAL systems (the same calls the
    /// input layer makes) and records pass/fail, timings and screenshots. It proves code paths in a running build;
    /// it does not replace a human playtest and does not test feel, comfort or VR rendering.
    /// </summary>
    public partial class AutoTest : MonoBehaviour
    {
        string m_Name, m_Dir;
        readonly List<string> m_Lines = new List<string>();
        int m_Pass, m_Fail;
        float m_T0;
        readonly List<float> m_FrameTimes = new List<float>();

        public void Begin(string name, string dir)
        {
            m_Name = string.IsNullOrEmpty(name) ? "all" : name;
            m_Dir = string.IsNullOrEmpty(dir) ? Path.Combine(Application.persistentDataPath, "autotest") : dir;
            Directory.CreateDirectory(m_Dir);
            m_T0 = Time.realtimeSinceStartup;
            Application.logMessageReceived += OnLog;
            Game.Input?.DesktopMap.Disable(); // stray real clicks/keys must not change an automated run
            StartCoroutine(Run());
        }

        readonly List<string> m_Errors = new List<string>();
        void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception) m_Errors.Add(type + ": " + msg);
        }

        void Update()
        {
            if (Time.unscaledDeltaTime > 0f) m_FrameTimes.Add(Time.unscaledDeltaTime * 1000f);
        }

        void Note(string s) { m_Lines.Add($"[{Time.realtimeSinceStartup - m_T0,7:0.0}s] {s}"); Debug.Log("[TS-AutoTest] " + s); }

        void Check(bool ok, string what)
        {
            if (ok) m_Pass++; else m_Fail++;
            Note((ok ? "PASS " : "FAIL ") + what);
        }

        IEnumerator Shot(string name)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(m_Dir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return null;
            Note("screenshot " + name + ".png");
        }

        IEnumerator WaitUntil(Func<bool> cond, float timeout, string what)
        {
            float t = 0f;
            while (!cond() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
            Check(cond(), what + $" (waited {t:0.0}s)");
        }

        IEnumerator Hold(Action<float> step, float seconds)
        {
            float t = 0f;
            while (t < seconds) { step(Time.deltaTime); t += Time.deltaTime; yield return null; }
        }

        IEnumerator Run()
        {
            Note("AutoTest '" + m_Name + "' mode=" + Game.Mode + " screen=" + Screen.width + "x" + Screen.height);
            if (m_Name != "models") yield return BrandingIntro();
            yield return MenuSystem.I.ShowMainMenuRoutine();
            yield return new WaitForSeconds(1.5f);
            Check(Game.Cells.Active != null && Game.Cells.Active.Id == CellId.Menu, "main menu cell active");
            Check(MenuSystem.I.IsOpen, "main menu panel open");
            Check(Branding.I != null && Branding.I.WelcomeShowing, "welcome screen shows the title logo beside the menu card");
            yield return Shot("01_menu");
            if (m_Name != "models") yield return WelcomeRoundTrip();

            if (m_Name == "all" || m_Name == "demo") yield return DemoFlow();
            if (m_Name == "all" || m_Name == "egypt") yield return EgyptFlow();
            if (m_Name == "all" || m_Name == "story") yield return StoryFlow();
            if (m_Name == "all" || m_Name == "countries") yield return CountriesFlow();
            if (m_Name == "all" || m_Name == "finale") yield return FinaleFlow();
            if (m_Name == "models") yield return ModelsFlow(); // art review of the Blender models (AutoTestModels.cs); not part of "all"
            if (m_Name == "shift") yield return ShiftFlow();   // art review of the glasses moment (AutoTestShift.cs); not part of "all"
            if (m_Name == "guide") yield return GuideFlow();   // walkthroughs, markers, maps (AutoTestGuide.cs); not part of "all"
            if (m_Name == "explore") yield return ExploreFlow(); // the expanded countries (AutoTestExplore.cs)
            if (m_Name == "vrgames") yield return VRGamesFlow(); // the claw machine, maze and Taj puzzle with VR hands (AutoTestVRGames.cs)
            if (m_Name == "deskclaw") { yield return DeskClawFlow(); yield return DeskTajFlow(); yield return MazeWalkFlow(); yield return NearStampFlow(); } // claw machine, Taj puzzle, maze walk, near stamp with desktop controls (AutoTestVRGames.cs)
            if (m_Name == "arcade_a") yield return ArcadeAFlow(); // Stop the Clock (London) and Memory Match (Morocco), desktop or VR hands (AutoTestArcadeA.cs)
            if (m_Name == "arcade_b") yield return ArcadeBFlow(); // New York impostor vote and Rio ring toss, desktop or VR (AutoTestArcadeB.cs)
            if (m_Name == "arcade_c") yield return ArcadeCFlow(); // Paris bubble shooter + Egypt temple run, desktop or VR hands (AutoTestArcadeC.cs)
            if (m_Name == "arcade_d") yield return ArcadeDFlow(); // UAE whack-a-ghost, Berlin shooting gallery, desktop or VR hands (AutoTestArcadeD.cs)
            if (m_Name == "all" || m_Name == "lifecycle") yield return LifecycleFlow(); // AutoTestLifecycle.cs
            if (m_Name == "all" || m_Name == "levels") yield return LevelsFlow();       // AutoTestLevels.cs
            if (m_Name == "persist1") yield return Persist1Flow();
            if (m_Name == "persist2") yield return Persist2Flow();

            // Report.
            var frames = m_FrameTimes.Skip(30).OrderBy(x => x).ToList();
            string perf = frames.Count > 0
                ? $"frames={frames.Count} median={frames[frames.Count / 2]:0.0}ms p95={frames[(int)(frames.Count * 0.95f)]:0.0}ms worst={frames[frames.Count - 1]:0.0}ms (desktop window, includes loading spikes; NOT a VR measurement)"
                : "no frames";
            var sb = new StringBuilder();
            sb.AppendLine("# AutoTest report: " + m_Name);
            sb.AppendLine();
            sb.AppendLine("- Date: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("- Build: " + Application.version + " | mode: " + Game.Mode + " | resolution: " + Screen.width + "x" + Screen.height);
            sb.AppendLine("- GPU: " + SystemInfo.graphicsDeviceName + " | API: " + SystemInfo.graphicsDeviceType);
            sb.AppendLine("- Result: **" + m_Pass + " passed, " + m_Fail + " failed**");
            sb.AppendLine("- Frame timing: " + perf);
            sb.AppendLine("- Errors/exceptions logged: " + m_Errors.Count);
            sb.AppendLine();
            sb.AppendLine("## Steps");
            foreach (var l in m_Lines) sb.AppendLine("    " + l);
            if (m_Errors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Logged errors");
                foreach (var e in m_Errors.Distinct().Take(40)) sb.AppendLine("- " + e.Replace("\n", " "));
            }
            File.WriteAllText(Path.Combine(m_Dir, "autotest_" + m_Name + ".md"), sb.ToString());
            Note("report written");
            yield return new WaitForSeconds(0.5f);
            Application.Quit(m_Fail == 0 ? 0 : 1);
        }

        /// <summary>The branded intro, exactly as a normal launch plays it (the bootstrap skips it for AutoTest).</summary>
        IEnumerator BrandingIntro()
        {
            Note("--- Branding: intro ---");
            Check(Branding.AllPresent, "branding textures in the build (cover, thumbnail, logo, menu art)");
            Check(Branding.TitleLogo != null, "the approved title logo is in the build (intro and welcome screen)");
            if (Branding.I == null) yield break;
            StartCoroutine(Branding.I.Intro());
            // Poll rather than sample once (a slow first frame or a stray startup input made the single sample flaky).
            bool seen = false;
            for (float t = 0f; t < 1.4f; t += Time.unscaledDeltaTime) { seen |= Branding.I.IntroShowing; yield return null; }
            Check(seen || Branding.I.IntroShowing, "intro shows the approved title logo");
            yield return Shot("00a_intro_cover");
            yield return new WaitForSecondsRealtime(1.7f);
            yield return Shot("00b_intro_logo");
            yield return WaitUntil(() => !Branding.I.IntroShowing, 5f, "intro ends by itself");
        }

        /// <summary>Welcome screen to Controls and back through the real 3D buttons (the same Press a click or laser sends).</summary>
        IEnumerator WelcomeRoundTrip()
        {
            var controls = Find<Button3D>("Button_Controls");
            Check(controls != null, "Controls button on the welcome screen");
            if (controls == null) yield break;
            controls.Press(controls.transform.position, HandSide.Desktop);
            yield return null;
            Check(MenuSystem.I.CurrentPage == "Controls" && !Branding.I.WelcomeShowing, "Controls page replaces the welcome layout");
            yield return new WaitForSeconds(0.3f);
            yield return Shot("01b_controls");
            var back = Find<Button3D>("Button_Back");
            Check(back != null, "Back button on the Controls page");
            if (back != null) back.Press(back.transform.position, HandSide.Desktop);
            yield return null;
            Check(Branding.I.WelcomeShowing && string.IsNullOrEmpty(MenuSystem.I.CurrentPage), "Back returns to the welcome screen");
        }

        // DemoFlow (France: the bubble shooter) and EgyptFlow (the temple run) live in Core/AutoTestArcadeC.cs.

        /// <summary>Screenshot of the Passenger from a couple of metres in front of it (art review, not a check).</summary>
        IEnumerator PassengerCloseup(string shot, float distance = 2.4f)
        {
            var pc = Game.Passenger;
            if (pc == null || !pc.Visible) { Note("close-up skipped: the Passenger is not visible"); yield break; }
            var fwd = pc.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();
            var pos = pc.transform.position + fwd * distance;
            var look = pc.transform.position - pos; look.y = 0f;
            Game.Rig.PlaceAt(new Vector3(pos.x, pc.transform.position.y, pos.z), Quaternion.LookRotation(look).eulerAngles.y);
            yield return new WaitForSeconds(0.4f);
            yield return Shot(shot);
        }

        static T Find<T>(string name) where T : Component
        {
            var go = GameObject.Find(name);
            return go != null ? go.GetComponent<T>() : null;
        }

        static T FindIn<T>(Transform root, string name) where T : Component
        {
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) { var c = t.GetComponent<T>(); if (c != null) return c; }
            return null;
        }

        IEnumerator StoryFlow()
        {
            Note("--- Story: home -> the bag -> Marrakech flashback -> the glasses -> the Margin -> UAE tutorial -> Egypt ---");
            Game.Story.StopAllCoroutines();
            MenuSystem.I.HideAll();
            Game.Story.StartNewGame();
            yield return WaitUntil(() => Game.Cells.Active != null && Game.Cells.Active.Id == CellId.ApartmentNormal && !Game.Story.Busy, 20f, "new story starts at home in Abu Dhabi");
            yield return new WaitForSeconds(1.5f);
            yield return Shot("20_home_bag");
            var apt = (ApartmentCell)Game.Cells.Active;
            yield return MirrorCheck(apt);
            var hand = Game.Rig.MainHand;
            Interactable bag = null;
            yield return WaitUntil(() => (bag = apt.Suitcase.GetComponent<Interactable>()) != null, 20f, "voicemail plays by itself, then the bag can be gone through");
            Check(Game.Campaign.HasFlag(StoryFlags.HomeArrived), "home arrival recorded");
            if (bag != null) bag.Use(hand);
            yield return WaitUntil(() => Game.Cells.Active != null && Game.Cells.Active.Id == CellId.ShopNormal, 12f, "the glasses case brings back Marrakech (flashback)");
            Check(Game.Campaign.HasFlag(StoryFlags.Unpacked), "the bag was gone through");
            var shop = (ShopCell)Game.Cells.Active;
            yield return WaitUntil(() => !Game.InputLocked, 6f, "the memory is a place to walk through: the player can move");
            Check(Game.Passport != null && !Game.Passport.Available && !Game.Story.TearsAllowed && !Game.Story.ThreatAllowed, "no passport, tears or threat inside the memory");
            yield return new WaitForSeconds(2.5f);
            Check(shop.transform.InverseTransformPoint(Game.Rig.Feet).y > -0.5f, "the memory starts on solid ground at the far end of the souk");
            yield return Shot("21_flashback_lane");
            // Walk the souk to the shop door for real (the movement path, colliders and all, at walking pace); the market
            // paints itself in ahead of the player.
            float zStart = shop.transform.InverseTransformPoint(Game.Rig.Feet).z, zDoor = shop.DoorPoint.localPosition.z + 0.7f, walked = 0f;
            bool midShot = false;
            var walker = Game.Rig as DesktopRig;
            if (walker != null) { walker.PlaceAt(Game.Rig.Feet, shop.transform.eulerAngles.y); walker.TestMove = new Vector2(0f, 1f); walker.TestSprint = false; }
            while (walked < 30f)
            {
                var lp = shop.transform.InverseTransformPoint(Game.Rig.Feet);
                if (lp.z >= zDoor) break;
                if (walker == null)
                {
                    lp.z = Mathf.Min(zDoor, lp.z + 2.4f * Time.deltaTime);
                    Game.Rig.PlaceAt(shop.transform.TransformPoint(new Vector3(0f, 0.02f, lp.z)), shop.transform.eulerAngles.y);
                }
                walked += Time.deltaTime;
                if (!midShot && lp.z > (zStart + zDoor) * 0.5f) { midShot = true; yield return Shot("21b_souk_reveal"); }
                yield return null;
            }
            if (walker != null) walker.TestMove = null;
            Check(shop.transform.InverseTransformPoint(Game.Rig.Feet).z >= zDoor - 0.3f, "the souk lane is walkable end to end, to the shop door (" + walked.ToString("0.0") + " s)");
            yield return new WaitForSeconds(1.5f);
            Check(MemoryReveal.Progress(shop.transform) > 0.8f, "the souk and the shop painted themselves in along the walk (" + (MemoryReveal.Progress(shop.transform) * 100f).ToString("0") + "%)");
            yield return Shot("22_flashback_shopkeeper");
            // Look at the old frames, then pay: the token into the brass tray.
            var props = shop.transform.Find("FlashbackProps");
            var frames = props != null ? props.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("Glasses")) : null;
            if (frames != null)
            {
                var toFrames = frames.position - Game.Rig.Feet; toFrames.y = 0f;
                Game.Rig.PlaceAt(Game.Rig.Feet, Quaternion.LookRotation(toFrames).eulerAngles.y);
                if (Game.Rig is DesktopRig dr) dr.SetPitch(25f);
            }
            var token = props != null ? props.GetComponentsInChildren<Grabbable>(true).FirstOrDefault(g => g.ItemId == "festival_token") : null;
            var tray = props != null ? props.GetComponentsInChildren<Socket>(true).FirstOrDefault(s => s.Accepts.Contains("festival_token")) : null;
            Check(token != null && tray != null, "the festival token and the brass tray are set out on the counter");
            yield return new WaitForSeconds(9f);
            // Playtest, 18 Oct ("there was no festival token"): the token glows on the counter until it is in hand, then the
            // glow moves to the brass tray.
            var glowT = props != null ? props.Find("ActHereGlow") : null;
            Check(glowT != null && glowT.gameObject.activeSelf, "the festival token glows on the counter while it is to be paid");
            if (token != null)
            {
                var toToken = token.transform.position - Game.Rig.Head.position;
                Game.Rig.PlaceAt(Game.Rig.Feet, Quaternion.LookRotation(new Vector3(toToken.x, 0f, toToken.z)).eulerAngles.y);
                if (Game.Rig is DesktopRig dr2) dr2.SetPitch(Mathf.Atan2(-toToken.y, new Vector2(toToken.x, toToken.z).magnitude) * Mathf.Rad2Deg);
            }
            yield return new WaitForSeconds(0.4f);
            yield return Shot("22b_flashback_token");
            if (token != null && tray != null)
            {
                hand.Grab(token);
                yield return null; yield return null;
                Check(glowT != null && (glowT.position - tray.transform.position).magnitude < 0.15f, "with the token in hand, the glow moves to the brass tray");
                hand.PlaceInto(tray);
            }
            yield return WaitUntil(() => Game.Campaign.HasFlag(StoryFlags.PrologueDone), 60f, "paying with the token completes the purchase and the memory folds away");
            Check(Game.Campaign.HasFlag(StoryFlags.ShopPurchased), "the glasses were bought in Marrakech");
            yield return WaitUntil(() => Game.Cells.Active != null && Game.Cells.Active.Id == CellId.ApartmentNormal && !Game.InputLocked, 8f, "back home in the present");
            Interactable wear = null;
            yield return WaitUntil(() =>
            {
                var hp = apt.transform.Find("HomecomingProps");
                wear = hp != null ? hp.GetComponentsInChildren<Interactable>().FirstOrDefault(i => i.Prompt == "Put on the glasses") : null;
                return wear != null;
            }, 8f, "the case opens and the glasses are ready");
            yield return new WaitForSeconds(0.6f);
            yield return Shot("23_home_glasses");
            int tearsBefore = Game.Campaign.tearsOpened;
            if (wear != null) wear.Use(hand);
            yield return WaitUntil(() => Game.Campaign.HasFlag(StoryFlags.GlassesWorn), 4f, "glasses put on (the one action at home)");
            // Playtest, 18 Oct: no Between run; the room turns into the Margin copy and its kitchen seam opens on Egypt by itself.
            yield return WaitUntil(() => Game.Tears.Active != null && Game.Tears.StoryTear && Game.Tears.Active.Destination.Id == CountryId.Egypt, 12f,
                "the glasses wake the kitchen seam: it opens on Egypt by itself");
            yield return new WaitForSeconds(0.6f);
            yield return Shot("24_seam_wakes_egypt");
            Check(!Game.Campaign.IsVisited(CountryId.Egypt) && Game.Campaign.tearsOpened == tearsBefore, "the waking seam counts for nothing (no visit, no tear)");
            yield return WaitUntil(() => Game.Cells.Active != null && Game.Cells.Active.Id == CellId.ApartmentCorrupt && Game.Cells.Active.Encounter.Entered, 20f, "the apartment becomes the Margin copy in place");
            yield return new WaitForSeconds(2f);
            yield return Shot("25_margin_apartment");

            var corrupt = (ApartmentCell)Game.Cells.Active;
            yield return StoryWhack(corrupt); // whack-a-ghost with desktop aim and E: ten stamped verify the UAE (AutoTestArcadeD.cs)
            var seam = corrupt.Seams.First(s => s.Id == "kitchen");
            var stand = seam.Center + seam.Forward * 1.3f;
            var look = seam.Center - stand; look.y = 0;
            yield return WaitUntil(() => Game.Passenger.Visible, 14f, "first controlled threat appears");
            yield return new WaitForSeconds(1f);
            yield return Shot("27_first_threat");

            Game.Passport.Open(HandSide.Desktop);
            Game.Passport.Select(CountryId.Egypt, true);
            Game.Rig.PlaceAt(new Vector3(stand.x, 0f, stand.z), Quaternion.LookRotation(look).eulerAngles.y);
            yield return null; yield return null;
            yield return Hold(dt => Game.Tears.HoldWiden(dt, HandSide.Desktop), 3.6f);
            Check(Game.Tears.Active != null && Game.Tears.Active.Size == TearSize.Doorway, "tear widened to a doorway");
            yield return Shot("28_doorway");
            Game.Tears.Traverse();
            yield return WaitUntil(() => Game.Cells.Active != null && Game.Cells.Active.Id == CellId.Egypt && !Game.Story.Busy, 8f, "stepped through the doorway into Egypt");
            Check(Game.Campaign.HasFlag(StoryFlags.TutorialDone), "tutorial complete after escaping the apartment");
            yield return new WaitForSeconds(1.5f);
            yield return Shot("29_egypt_after_escape");
        }

        // ------------------------------------------------------------------ helpers for per-country checks
        IEnumerator Enter(CellId id, Action<CampaignState> configure = null)
        {
            Game.Story.StopAllCoroutines();
            Narration.I?.StopAll();
            Game.Tears?.ForceReset();
            Game.Passport?.Close();
            Game.InputLocks = 0;
            MenuSystem.I.HideAll();
            Game.Story.StartAt(id, configure);
            yield return WaitUntil(() => Game.Cells.Active != null && Game.Cells.Active.Id == id && !Game.Story.Busy &&
                                         Game.Cells.Active.Encounter != null && Game.Cells.Active.Encounter.Entered, 20f, "enters " + id);
            yield return new WaitForSeconds(1.2f);
        }

        void AimAt(SeamAnchor seam, float distance = 3f)
        {
            var stand = seam.Center + seam.Forward * distance;
            var look = seam.Center - stand; look.y = 0;
            Game.Rig.PlaceAt(new Vector3(stand.x, seam.FloorPoint.y, stand.z), Quaternion.LookRotation(look).eulerAngles.y);
        }

        IEnumerator UseTrace(TraceMark trace, CountryId id)
        {
            var floor = Game.Cells.Active.transform.position.y;
            Game.Rig.PlaceAt(new Vector3(trace.transform.position.x, floor, trace.transform.position.z - 1.2f), 0f);
            yield return null;
            Check(trace.Revealed && trace.CanUse(Game.Rig.MainHand) && trace.GetPrompt(null) == trace.Prompt, id + " trace revealed and usable");
            trace.Use(Game.Rig.MainHand);
            yield return WaitUntil(() => Game.Campaign.IsVerified(id), 5f, id + " seal verified");
        }

        static Grabbable Item(Transform root, string id) =>
            root == null ? null : root.GetComponentsInChildren<Grabbable>(true).FirstOrDefault(g => g.ItemId == id);

        IEnumerator CountriesFlow()
        {
            Note("--- Countries: New York, Japan, Riyadh, Agra, Rio, London, Marrakech ---");
            var hand = Game.Rig.MainHand;

            // USA: the space-station arcade: two tasks, the clue, a wrong vote, the impostor voted out (AutoTestArcadeB.cs).
            yield return CountriesUsa();

            // Japan: win the golden lucky cat from the claw machine.
            yield return Enter(CellId.Japan);
            var jp = (JapanCell)Game.Cells.Active;
            yield return Shot("32_japan");
            Game.Rig.PlaceAt(jp.Claw.transform.TransformPoint(new Vector3(0f, 0f, -1.5f)), jp.Claw.transform.eulerAngles.y);
            yield return new WaitForSeconds(0.4f);
            for (int i = 0; i < jp.Claw.GoldCol; i++) { jp.Claw.Right.Use(hand); yield return new WaitForSeconds(0.45f); }
            for (int i = 0; i < jp.Claw.GoldRow; i++) { jp.Claw.Back.Use(hand); yield return new WaitForSeconds(0.45f); }
            Check(jp.Claw.ClawCol == jp.Claw.GoldCol && jp.Claw.ClawRow == jp.Claw.GoldRow, "claw lined up over the golden cat");
            jp.Claw.Drop.Use(hand);
            yield return WaitUntil(() => jp.Claw.GoldenOut, 10f, "the claw delivers the golden lucky cat to the tray");
            yield return Shot("33_japan_solved");
            hand.Grab(jp.Claw.Golden);
            yield return WaitUntil(() => Game.Campaign.IsVerified(CountryId.Japan), 6f, "taking the lucky cat verifies Japan");

            // Saudi Arabia: cross the maze (energy drains; water restores), out to the portal.
            yield return Enter(CellId.Riyadh);
            var ry = (RiyadhCell)Game.Cells.Active;
            yield return Shot("34_riyadh");
            float before = Game.Campaign.vitality;
            yield return new WaitForSeconds(1.5f);
            Check(Game.Campaign.vitality < before, "the maze drains energy");
            var glass = ry.Maze.Waters[0];
            Game.Rig.PlaceAt(ry.transform.TransformPoint(glass.localPosition), 0f);
            yield return WaitUntil(() => !SandMaze.IsFull(glass), 2f, "walking up to a water glass drinks it");
            Game.Rig.PlaceAt(ry.transform.TransformPoint(ry.Maze.PortalLocal + new Vector3(0f, 0f, -3f)), 0f);
            yield return WaitUntil(() => Game.Campaign.IsVerified(CountryId.SaudiArabia), 6f, "out of the maze verifies Saudi Arabia");
            yield return Shot("35_riyadh_solved");

            // India: put the Taj Mahal picture right.
            yield return Enter(CellId.Agra);
            var ag = (AgraCell)Game.Cells.Active;
            yield return Shot("36_agra");
            Check(ag.Puzzle.Misplaced > 0, "the Taj Mahal puzzle starts scrambled");
            for (int s = 0; s < TajJigsaw.Count; s++) { hand.Grab(ag.Puzzle.Piece(s)); hand.PlaceInto(ag.Puzzle.Slot(s)); yield return new WaitForSeconds(0.15f); }
            yield return WaitUntil(() => Game.Campaign.IsVerified(CountryId.India), 6f, "the finished picture verifies India");
            yield return Shot("37_agra_solved");

            // Brazil: five ringers at the carnival ring toss (AutoTestArcadeB.cs).
            yield return CountriesBrazil();

            // UK: Stop the Clock (AutoTestArcadeA.cs).
            yield return CountriesLondon(hand);

            // Morocco: Memory Match, then the calibration insert and the optician's notes (AutoTestArcadeA.cs).
            yield return CountriesMorocco(hand);
        }

        /// <summary>Diagnostic: where the sunbeam is, how strong, and whether anything blocks it from a point.</summary>
        void LogBeam(Vector3 point)
        {
            var t = Game.Tears.Active;
            var beam = t != null ? t.Beam : null;
            if (beam == null) { Note("beam: none"); return; }
            var local = beam.transform.InverseTransformPoint(point);
            bool blocked = Physics.Linecast(beam.transform.position + beam.transform.forward * 0.3f, point, out var hit, 1 << Layers.Default, QueryTriggerInteraction.Ignore);
            Note($"beam pos={beam.transform.position:F2} fwd={beam.transform.forward:F2} strength={beam.Strength:0.00} aperture={t.Aperture:0.00} " +
                 $"target={point:F2} local={local:F2} exposure={LightVolumes.Exposure(point, true):0.00} " +
                 $"blocked={(blocked ? hit.collider.name + " (" + LayerMask.LayerToName(hit.collider.gameObject.layer) + ") at " + hit.point.ToString("F2") : "no")}");
        }

        IEnumerator FinaleFlow()
        {
            Note("--- Finale: Berlin -> shooting gallery -> VOID -> near miss -> real home -> epilogue -> results ---");
            var hand = Game.Rig.MainHand;
            yield return Enter(CellId.Berlin, c =>
            {
                // The state a real run reaches before Berlin: ten other seals, their resources, the counterfeit seen.
                foreach (var d in CountryDatabase.All) if (d.Id != CountryId.Germany) c.Verify(d.Id);
                c.SetFlag(StoryFlags.FirstTearDone);
                c.SetFlag(StoryFlags.CounterfeitRevealed);
                c.SetFlag(StoryFlags.OpticianNotesRead);
            });
            var b = (BerlinCell)Game.Cells.Active;
            var enc = (BerlinEncounter)b.Encounter;
            yield return Shot("50_berlin");
            Check(!b.HomeSeam.Revealed, "the true return is hidden at the start");
            Game.Rig.PlaceAt(b.Plinth.transform.position + new Vector3(0, -1.07f, -1.3f), 0f);
            yield return null;
            Check(b.Plinth.GetPrompt(null) == b.Plinth.Prompt, "registration plinth accepts ten seals plus the insert");
            b.Plinth.Use(hand);
            yield return WaitUntil(() => Game.Campaign.AllElevenVerified, 5f, "eleven of eleven seals verified");
            yield return WaitUntil(() => Game.Passenger.Visible, 15f, "the mimic appears at the blank wall");
            yield return new WaitForSeconds(1.5f);
            yield return Shot("51_berlin_mimic");
            yield return PassengerCloseup("51b_berlin_mimic_close", 2.6f);

            // The ghost shooting gallery: the sun torch, five beams of light on its chest (desktop aim and E; AutoTestArcadeD.cs).
            yield return PlayGallery(b, "52_berlin", false);
            Check(enc.Exposed, "the Passenger is worn down: the forgery can be cancelled");

            hand.Grab(b.VoidStamp);
            b.VoidStamp.OnActivate?.Invoke(b.VoidStamp, hand);
            yield return WaitUntil(() => Game.Campaign.HasFlag(StoryFlags.ForgedCancelled), 3f, "forged HOME stamp cancelled with VOID");
            hand.ForceRelease(false);
            Check(Game.Tears.Active == null, "no other tear is open before the home tear");
            Check(Game.Campaign.CanOpenGenuineHome && b.HomeSeam.Revealed, "genuine home can open (11 seals, insert, forgery cancelled)");

            AimAt(b.HomeSeam, 2.4f);
            yield return null; yield return null;
            Check(Game.Tears.HasTarget && Game.Tears.Target == b.HomeSeam, "the blank wall holds the true return");
            yield return Hold(dt => Game.Tears.HoldWiden(dt, HandSide.Desktop), 3.6f);
            Check(Game.Tears.Active != null && Game.Tears.Active.Destination == CountryDatabase.Home && Game.Tears.Active.Size == TearSize.Doorway,
                  "home tear opened to the real apartment");
            yield return Shot("53_home_tear");
            Game.Tears.Traverse();
            yield return WaitUntil(() => Game.Campaign.HasFlag(StoryFlags.Escaped), 12f, "near miss survived; glasses removed (virtual gesture)");
            yield return new WaitForSeconds(2.5f);
            yield return Shot("54_real_home");
            yield return WaitUntil(() => Game.Campaign.HasFlag(StoryFlags.GlassesBroken), 10f, "glasses put down and broken");
            yield return WaitUntil(() => Game.Campaign.HasFlag(StoryFlags.CampaignComplete), 45f, "bathroom, epilogue, campaign complete");
            yield return WaitUntil(() => MenuSystem.I.IsOpen, 6f, "results shown");
            yield return new WaitForSeconds(0.8f);
            yield return Shot("55_results");
            MenuSystem.I.HideAll();
        }

        void OnDestroy() => Application.logMessageReceived -= OnLog;
    }
}

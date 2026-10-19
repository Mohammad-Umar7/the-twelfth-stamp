using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// The two arcade games of France and Egypt. --autotest=arcade_c plays both through real controls, on the desktop rig
    /// (aim the view, E) or the VR rig with --mode=vr --vr-preview (a scripted hand's aim ray, trigger): the bubble shooter
    /// over the Seine shot by shot to the win, then the temple run lane by lane (coins collected, one mummy hit taken on
    /// purpose, the rest dodged) to the portal. Also here: the judges' demo (DemoFlow), the Egypt story check (EgyptFlow)
    /// and the Paris / Egypt parts of the lifecycle, guide and models runs.
    /// </summary>
    public partial class AutoTest
    {
        // ================================================================== arcade_c
        IEnumerator ArcadeCFlow()
        {
            bool vr = Game.Rig is VRRig;
            string p = vr ? "cv" : "cd";
            Note("--- Arcade C (" + (vr ? "VR rig, scripted hands" : "desktop aim and E") + "): the Paris bubble shooter, the Egypt temple run ---");

            yield return Enter(CellId.Paris);
            var paris = (ParisCell)Game.Cells.Active;
            var board = paris.Bubbles;
            int colours = Enumerable.Range(0, BubbleBoard.MaxRows).SelectMany(r => Enumerable.Range(0, BubbleBoard.ColsIn(r)).Select(c => board.At(r, c))).Where(k => k >= 0).Distinct().Count();
            Check(board != null && board.Remaining >= 45 && board.Cleared == 0 && !board.Won && board.Surface.Usable && colours >= 3,
                "Paris: a fresh bubble board floats over the Seine (" + board.Remaining + " bubbles in " + colours + " colours, clear " + board.Target + ")");
            var eye = paris.FiringSpot.position + Vector3.up * DesktopRig.EyeHeight;
            float corner = 0f;
            foreach (var cx in new[] { -1f, 1f }) foreach (var cy in new[] { -1f, 1f })
                corner = Mathf.Max(corner, Vector3.Distance(eye, board.transform.TransformPoint(new Vector3(cx * BubbleBoard.W / 2f, cy * BubbleBoard.H / 2f, 0f))));
            Check(Vector3.Distance(eye, board.LauncherWorld) < 3.0f && Vector3.Distance(eye, board.CentreWorld) < 3.0f,
                "from the firing ring the board is in desktop reach (launcher " + Vector3.Distance(eye, board.LauncherWorld).ToString("0.00") + " m, centre " + Vector3.Distance(eye, board.CentreWorld).ToString("0.00") + " m, far corner " + corner.ToString("0.00") + " m; the ray is 3.2 m)");
            Check(paris.Hunt.Running, "the figure's hunt is armed while the board is played");
            paris.Hunt.Begin(0.2f);
            yield return WaitUntil(() => Game.Passenger != null && Game.Passenger.Visible, 4f, "the figure in the raincoat comes down the street for the player");
            Check(Game.Passenger.Threatening && !Game.Passenger.AllowChase, "it stalks (rushing only now and then) while the bubbles are played");
            paris.Hunt.Stop();
            Game.Passenger.Despawn();
            yield return PlayBubbles(paris, p + "_paris");
            yield return WaitUntil(() => Game.Campaign.IsVerified(CountryId.France), 6f, "clearing the bubbles verifies France (" + (vr ? "VR" : "desktop") + ")");
            yield return WaitUntil(() => Game.Story.WaitingStamp != null, 8f, "the France stamp is put out to collect");
            // Where it waits is the story's call (out at the Eiffel Tower, or a few steps away in the open).
            var waiting = Game.Story.WaitingStamp;
            bool atTower = Landmarks.TryStampSpot(paris, out var tower) && waiting != null && Vector3.Distance(waiting.transform.position, tower) < 0.6f;
            var toStamp = waiting != null ? waiting.transform.position - Game.Rig.Feet : Vector3.zero; toStamp.y = 0f;
            Check(waiting != null && (atTower || toStamp.magnitude < 8f), "France's stamp waits " + (atTower ? "at the Eiffel Tower" : toStamp.magnitude.ToString("0.0") + " m away"));

            yield return TempleRunGame(p + "_egypt", true);
        }

        // ================================================================== the bubble shooter
        /// <summary>Stands on the firing ring and plays the board to the win: each shot is the board's best (as a good
        /// player would pick), aimed with the real controls and fired with use. Checks each lands where it was aimed.</summary>
        IEnumerator PlayBubbles(ParisCell paris, string tag, int maxShots = 45)
        {
            var board = paris.Bubbles;
            var feet = paris.FiringSpot.position;
            Game.Rig.PlaceAt(feet, paris.FiringSpot.eulerAngles.y);
            yield return new WaitForSeconds(0.4f);
            var hand = Game.Rig.MainHand;
            int shots = 0, landedAsAimed = 0, misses = 0, pops = 0;
            bool firstChecked = false;
            while (!board.Won && shots < maxShots && misses < 4)
            {
                for (float w = 0f; board.Busy && w < 5f; w += Time.deltaTime) yield return null;
                if (!board.BestShot(out var dir, out var cell, out var score)) { Note("no shot found on the board"); break; }
                board.Predict(dir, out var expect, out int expectPops, out _);
                yield return AimAtPoint(board.AimPointFor(dir));
                if (hand.Focused != board.Surface)
                {
                    // A closer point on the same line, in case something stood in the way.
                    yield return AimAtPoint(board.AimPointFor(dir, 0.4f));
                }
                if (!firstChecked)
                {
                    firstChecked = true;
                    Check(hand.Focused == board.Surface && hand.CurrentPrompt() == "Shoot a bubble",
                        "aiming at the board focuses it, prompt \"" + hand.CurrentPrompt() + "\" (focused " + (hand.Focused != null ? hand.Focused.name : "nothing") + ")");
                    yield return Shot(tag + "_aim");
                }
                if (hand.Focused != board.Surface) { misses++; Note("board not focused for shot " + (shots + 1)); continue; }
                int before = board.Shots, clearedBefore = board.Cleared;
                hand.PressUse();
                yield return null;
                if (board.Shots == before) { misses++; Note("use did not fire shot " + (shots + 1)); continue; }
                shots++;
                if (shots == 1)
                {
                    // A second use while the bubble is still flying is ignored.
                    hand.PressUse();
                    Check(board.Shots == before + 1 && board.Busy, "a use while the bubble is in flight is ignored");
                }
                for (float w = 0f; board.Busy && w < 5f; w += Time.deltaTime) yield return null;
                if (board.LastLanded == expect || board.Refills > 0 && board.LastLanded.x < 0) landedAsAimed++;
                else Note("shot " + shots + " landed at " + board.LastLanded + ", aimed for " + expect);
                if (board.Cleared > clearedBefore) pops++;
                if (shots == 3) yield return Shot(tag + "_play");
            }
            Note("bubbles: " + shots + " shots, " + pops + " that cleared, " + board.Popped + " popped, " + board.Dropped + " dropped, " + board.Refills + " refills");
            Check(shots > 0 && landedAsAimed >= shots - 1, "each bubble flies where it was aimed and sticks in the predicted cell (" + landedAsAimed + " / " + shots + ")");
            Check(board.Popped >= 3 && pops > 0, "matching three or more pops them (" + board.Popped + " popped" + (board.Dropped > 0 ? ", " + board.Dropped + " dropped when cut loose" : "") + ")");
            Check(board.Won && board.Cleared >= board.Target || board.Won && board.Remaining == 0, "the board is cleared to its target (" + board.Cleared + " / " + board.Target + ") in " + shots + " shots");
            yield return new WaitForSeconds(1.2f);
            yield return Shot(tag + "_bravo");
        }

        /// <summary>Points the player's aim at a world point: the desktop view turned from where the eyes are, or the main VR
        /// hand held out in front with its aim ray on the point.</summary>
        IEnumerator AimAtPoint(Vector3 world)
        {
            if (Game.Rig is DesktopRig desk)
            {
                var d = world - desk.Head.position;
                desk.SetLook(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg);
                yield return null;
                d = world - desk.Head.position; // once more, after the body settled
                desk.SetLook(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg);
            }
            else if (Game.Rig is VRRig vr)
            {
                var side = Game.Input.DominantHand == HandSide.Left ? HandSide.Left : HandSide.Right;
                var hand = vr.MainHand;
                var fwd = Game.Rig.Forward;
                var holdAt = Game.Rig.Head.position + fwd * 0.35f + Vector3.down * 0.35f + Vector3.Cross(Vector3.up, fwd) * (side == HandSide.Left ? -0.18f : 0.18f);
                var grip = vr.GripOf(side);
                grip.position += holdAt - hand.HoldPoint.position;
                var aim = vr.AimOf(side);
                aim.position = hand.HoldPoint.position;
                aim.rotation = Quaternion.LookRotation(world - aim.position);
            }
            yield return null; yield return null; yield return null;
        }

        // ================================================================== the temple run
        IEnumerator TempleRunGame(string tag, bool takeHit)
        {
            yield return Enter(CellId.Egypt);
            var e = (EgyptCell)Game.Cells.Active;
            var run = e.Run;
            var arrival = e.transform.InverseTransformPoint(e.Arrival.position);
            Check((arrival - TempleRun.StartLocal).magnitude < 0.3f && Vector3.Distance(Game.Rig.Feet, e.Arrival.position) < 1.2f,
                "Egypt: the player arrives at the start of the temple run (" + arrival.ToString("F1") + ")");
            Check(run.CoinTotal >= 25 && run.Mummies.Count >= 6 && run.Obstacles.Count >= 6,
                "the causeway is set: " + run.CoinTotal + " passport coins, " + run.Mummies.Count + " tomb doors, " + run.Obstacles.Count + " fallen stones");
            Check(CausewayClear(e, out string bump), "the three lanes are flat and open from the gate to the finish, apart from the fallen stones" + bump);
            int spots = e.GetComponentsInChildren<VistaInfo>(true).Count(v => v.HasStampSpot);
            Check(spots == 1 && Landmarks.TryStampSpot(e, out var ring) && Vector3.Distance(ring, run.Portal.position + Vector3.up * 1.6f) < 0.1f,
                "the only stamp spot in Egypt is the finish portal's ring (" + spots + " spot)");
            Check(e.Hunt.Running, "the wrapped figure's hunt is armed for the run");
            e.Hunt.Begin(0.2f);
            yield return WaitUntil(() => Game.Passenger != null && Game.Passenger.Visible, 4f, "the wrapped figure rises out of the sand behind the runner");
            if (Game.Passenger.Visible)
            {
                yield return new WaitForSeconds(1.5f);
                if (!(Game.Rig is VRRig)) yield return PassengerCloseup(tag + "_figure", 2.6f);
            }
            e.Hunt.Stop();
            Game.Passenger.Despawn();
            Game.Story.RestoreVitality(1f);
            // The view down the causeway from the forecourt.
            Game.Rig.PlaceAt(e.Arrival.position, 0f);
            if (Game.Rig is VRRig) VRLook(e.Arrival.position + Vector3.up * 1.6f, e.transform.TransformPoint(new Vector3(TempleRun.RunX, 1.5f, 20f)));
            yield return new WaitForSeconds(0.5f);
            yield return Shot(tag + "_start");
            yield return RunTemple(e, takeHit, tag);
            yield return WaitUntil(() => Game.Campaign.IsVerified(CountryId.Egypt) && Game.Campaign.HasResource(ResourceId.Sunlight), 6f,
                "crossing the finish line verifies Egypt, and its seal still grants Egyptian sunlight");
            Check(run.Coins >= run.CoinTotal / 2, "passport coins collected along the lanes (" + run.Coins + " / " + run.CoinTotal + ")");
            yield return WaitUntil(() => Game.Story.WaitingStamp != null, 8f, "the Egypt stamp is put out");
            var stamp = Game.Story.WaitingStamp;
            Check(stamp != null && Vector3.Distance(stamp.transform.position, run.Portal.position + Vector3.up * 1.6f) < 0.5f, "the stamp hangs in the finish portal's ring");
            var portalW = run.Portal.position;
            if (Game.Rig is VRRig) VRLook(portalW + new Vector3(0f, 1.6f, -5f), portalW + Vector3.up * 1.6f);
            else
            {
                Game.Rig.PlaceAt(portalW + new Vector3(0f, 0f, -5f), 0f);
                yield return AimAtPoint(portalW + Vector3.up * 1.6f);
            }
            yield return new WaitForSeconds(0.4f);
            yield return Shot(tag + "_portal");
        }

        /// <summary>Down every lane every 50 cm from the gate to the finish line, the first thing below head height is the
        /// causeway's own floor (a fallen stone where one lies): nothing else pokes up through it.</summary>
        static bool CausewayClear(EgyptCell e, out string bump)
        {
            bump = "";
            var run = e.Run;
            int mask = ~((1 << Layers.PlayerBody) | (1 << Layers.MirrorOnly) | (1 << Layers.Interactable) | (1 << Layers.Passenger) | (1 << Layers.IgnoreRaycast));
            for (int lane = 0; lane < 3; lane++)
                for (float z = TempleRun.Z0 + 0.5f; z < TempleRun.Z1; z += 0.5f)
                {
                    var top = e.transform.TransformPoint(new Vector3(TempleRun.LaneX(lane), 2.4f, z));
                    if (!Physics.Raycast(top, Vector3.down, out var hit, 4f, mask, QueryTriggerInteraction.Ignore)) { bump = " (no floor in lane " + lane + " at z " + z + ")"; return false; }
                    float h = e.transform.InverseTransformPoint(hit.point).y - TempleRun.FloorY;
                    if (h < 0.05f) continue;
                    if (run.Obstacles.Any(o => o.Lane == lane && Mathf.Abs(o.Z - z) < 0.6f)) continue;
                    bump = " (" + hit.collider.name + " rises " + h.ToString("0.00") + " m in lane " + lane + " at z " + z + ")";
                    return false;
                }
            return true;
        }

        /// <summary>
        /// Runs the causeway by moving the rig along the lanes each frame (as a player would, at a run): into the lane of
        /// the next coins, round the fallen stones, out of the way of the mummies; with <paramref name="takeHit"/>, it
        /// first stands in the first mummy's lane and takes its hit (energy drained, thrown back), then runs on.
        /// </summary>
        IEnumerator RunTemple(EgyptCell e, bool takeHit, string tag)
        {
            var run = e.Run;
            var cell = e.transform;
            Vector3 World(float px, float pz) => cell.TransformPoint(new Vector3(px, TempleRun.FloorY, pz));
            float x = TempleRun.LaneX(1), z = TempleRun.Z0 - 2.5f;
            Game.Rig.PlaceAt(World(x, z), 0f);
            yield return new WaitForSeconds(0.3f);
            bool hitDone = !takeHit, midShot = false, objectiveChecked = false;
            TempleRun.Mummy victim = null;
            float vitBefore = 0f, zBefore = 0f, waited = 0f, t = 0f;
            int coins0 = run.Coins;
            bool Moving(TempleRun.Mummy m) => m.State == TempleRun.MummyState.Walking || m.State == TempleRun.MummyState.Bursting;
            bool MummyIn(int lane, float zz)
            {
                foreach (var m in run.Mummies)
                    if (Moving(m) && (TempleRun.LaneOf(m.X) == lane || m.Lane == lane) && m.Z > zz - 1.2f && m.Z < zz + 6.5f) return true;
                return false;
            }
            while (z < TempleRun.Z1 + 1.6f && t < 150f)
            {
                float dt = Time.deltaTime;
                t += dt;
                if (Game.Story.Failing) { Note("the runner was caught"); break; }
                if (run.Staggered)
                {
                    yield return null;
                    var lp = cell.InverseTransformPoint(Game.Rig.Feet);
                    x = lp.x; z = lp.z;
                    continue;
                }
                if (!hitDone)
                {
                    if (victim == null)
                        foreach (var m in run.Mummies)
                            if (m.State == TempleRun.MummyState.Walking && m.Z > z + 1f) { victim = m; vitBefore = Game.Campaign.vitality; zBefore = z; Note("standing in the way of the mummy from the tomb at z " + m.DoorZ); break; }
                    if (victim != null)
                    {
                        // Stand in its lane and let it come.
                        if (!run.ObstacleNear(victim.Lane, z - 1f, z + 1f)) x = Mathf.MoveTowards(x, TempleRun.LaneX(victim.Lane), 4f * dt);
                        waited += dt;
                        Game.Rig.PlaceAt(World(x, z), 0f);
                        yield return null;
                        if (run.Hits > 0)
                        {
                            hitDone = true;
                            yield return null;
                            for (float w = 0f; run.Staggered && w < 2f; w += Time.deltaTime) yield return null;
                            var after = cell.InverseTransformPoint(Game.Rig.Feet);
                            float lost = vitBefore - Game.Campaign.vitality;
                            Check(lost > 0.2f && lost < 0.45f && !Game.Story.Failing, "a mummy's touch drains energy, not a sudden death (" + vitBefore.ToString("0.00") + " -> " + Game.Campaign.vitality.ToString("0.00") + ")");
                            Check(zBefore - after.z > 2f, "and throws the runner back down the lane (" + (zBefore - after.z).ToString("0.0") + " m)");
                            Check(victim.State == TempleRun.MummyState.Crumbling || victim.State == TempleRun.MummyState.Gone, "the mummy crumbles to dust as it strikes");
                            x = after.x; z = after.z;
                            if (Game.Rig is VRRig) VRLook(Game.Rig.Head.position, Game.Rig.Head.position + Vector3.forward * 4f);
                            yield return Shot(tag + "_hit");
                        }
                        else if (waited > 14f || victim.State == TempleRun.MummyState.Gone) { Note("no hit came (waited " + waited.ToString("0.0") + " s)"); hitDone = true; }
                        continue;
                    }
                }
                int lane = TempleRun.LaneOf(x);
                int want = lane;
                foreach (var c in run.CoinList) if (!c.Taken && c.Z > z + 0.3f && c.Z < z + 7f) { want = c.Lane; break; }
                bool Bad(int l) => run.ObstacleNear(l, z - 0.4f, z + 2.8f) || MummyIn(l, z);
                if (Bad(want))
                {
                    int alt = -1;
                    foreach (int l in new[] { lane, (lane + 1) % 3, (lane + 2) % 3 }) if (!Bad(l)) { alt = l; break; }
                    if (alt >= 0) want = alt;
                }
                x = Mathf.MoveTowards(x, TempleRun.LaneX(want), 5f * dt);
                bool blocked = false;
                foreach (var o in run.Obstacles) if (Mathf.Abs(TempleRun.LaneX(o.Lane) - x) < 1.25f && o.Z - z > -0.2f && o.Z - z < 0.9f) blocked = true;
                if (!blocked) z += 4.2f * dt;
                Game.Rig.PlaceAt(World(x, z), 0f);
                if (!objectiveChecked && run.Coins > coins0)
                {
                    objectiveChecked = true;
                    var obj = e.Encounter.Objective;
                    Check(obj.Contains(run.Coins + " / " + run.CoinTotal), "running through a coin takes it, and the objective counts it (" + run.Coins + " / " + run.CoinTotal + ")");
                }
                if (!midShot && z > 32f)
                {
                    midShot = true;
                    if (Game.Rig is VRRig) VRLook(Game.Rig.Head.position, Game.Rig.Head.position + new Vector3(0f, -0.2f, 6f));
                    yield return Shot(tag + "_run");
                }
                yield return null;
            }
            Note("run: " + t.ToString("0") + " s, " + run.Coins + " / " + run.CoinTotal + " coins, " + run.Bursts + " mummies burst out, " + run.Hits + " hit(s), vitality " + Game.Campaign.vitality.ToString("0.00"));
            Check(z >= TempleRun.Z1 + 0.5f, "the runner reaches the finish line (z " + z.ToString("0.0") + ")");
            Check(run.Bursts >= 4, "mummies burst out of the tomb doors along the way (" + run.Bursts + ")");
            Check(run.Hits == (takeHit ? 1 : 0), "the other mummies were dodged by changing lanes (" + run.Hits + " hit" + (run.Hits == 1 ? "" : "s") + ")");
        }

        // ================================================================== the judges' demo (France)
        IEnumerator DemoFlow()
        {
            Note("--- Judges' demo flow (Paris: the bubble shooter over the Seine) ---");
            MenuSystem.I.HideAll();
            Game.Story.StartDemo();
            yield return WaitUntil(() => Game.Cells.Active != null && Game.Cells.Active.Id == CellId.Paris && !Game.Story.Busy, 20f, "demo enters Paris");
            yield return new WaitForSeconds(1.5f);
            var paris = (ParisCell)Game.Cells.Active;
            var board = paris.Bubbles;
            Check(board != null && !board.Won && board.Cleared == 0 && board.Surface.Usable, "the bubble board floats over the Seine, ready to play");
            Check(Game.IsDemo && paris.Hunt.Running, "the figure in the raincoat will hunt the street while the board is played");
            Check(paris.Encounter.Objective.Contains("Bubble shooter"), "the objective explains the bubble shooter (not the old desk puzzle)");
            yield return Shot("02_paris_arrival");
            paris.Hunt.Stop();
            Game.Passenger.Despawn();
            Note("(demo: the hunt is held off for this scripted run; arcade_c and lifecycle play it)");
            // The body: run down the street through the real movement path (arms pump, head bobs, steps and breathing).
            if (Game.Rig is DesktopRig runner)
            {
                var startFeet = runner.Feet;
                runner.PlaceAt(paris.transform.TransformPoint(new Vector3(0.6f, 0f, 3.0f)), 180f);
                yield return null;
                runner.TestMove = new Vector2(0f, 1f); runner.TestSprint = true;
                yield return new WaitForSeconds(1.1f);
                Note("run check: locks " + Game.InputLocks + ", menu " + Game.InMenu + ", paused " + Game.Paused + ", busy " + Game.Story.Busy +
                     ", moved " + (runner.Feet - startFeet).magnitude.ToString("0.00") + " m, sprinting " + runner.Sprinting);
                Check(runner.Sprinting && runner.MoveSpeed > 3f, "Shift-running: the player sprints (" + runner.MoveSpeed.ToString("0.0") + " m/s)");
                yield return Shot("02b_running_arms");
                yield return new WaitForSeconds(0.13f);
                yield return Shot("02c_running_arms");
                runner.TestSprint = false;
                yield return new WaitForSeconds(0.9f);
                yield return Shot("02d_walking_arms");
                runner.TestMove = null;
                yield return new WaitForSeconds(0.8f);
                Check(runner.MoveSpeed < 1.5f, "letting go: the player slows to a stop");
                runner.PlaceAt(startFeet, 0f);
                yield return new WaitForSeconds(0.3f);
            }

            // The passport and a tear still work here: Egyptian sunlight through a slit at the poster board's seam.
            Game.Passport.Open(HandSide.Desktop);
            Game.Passport.Select(CountryId.Egypt, true);
            yield return new WaitForSeconds(0.6f);
            Check(Game.Passport.IsOpen && Game.Passport.Selected == CountryId.Egypt, "passport open with Egypt selected");
            yield return Shot("03_passport_open");
            var seam = paris.DeskSeam;
            Game.Rig.PlaceAt(paris.TearVantage.position, paris.TearVantage.eulerAngles.y + 120f);
            yield return null; yield return null;
            yield return Hold(dt => Game.Tears.HoldWiden(dt, HandSide.Desktop), 0.3f);
            Check(Game.Tears.Active == null && !Game.Tears.HasTarget, "no tear opens when no seam is aimed at");
            var aim = seam.Center - (paris.TearVantage.position + Vector3.up * DesktopRig.EyeHeight);
            Game.Rig.PlaceAt(paris.TearVantage.position, Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg);
            if (Game.Rig is DesktopRig vantageRig) vantageRig.SetPitch(-Mathf.Atan2(aim.y, new Vector2(aim.x, aim.z).magnitude) * Mathf.Rad2Deg);
            yield return null; yield return null;
            Check(Game.Tears.HasTarget && Game.Tears.Target == seam, "the poster board's seam is targeted from the passport");
            yield return Hold(dt => Game.Tears.HoldWiden(dt, HandSide.Desktop), 0.2f);
            Check(Game.Tears.Active != null && Game.Tears.Active.Destination.Id == CountryId.Egypt, "tear opened to Egypt");
            Check(Game.Tears.Active != null && Game.Tears.Active.Beam != null, "Egyptian sunlight flows through the tear");
            yield return new WaitForSeconds(1.0f);
            yield return Shot("04_slit_sunlight");
            yield return Hold(dt => Game.Tears.HoldSeal(dt, HandSide.Desktop), 0.9f);
            yield return WaitUntil(() => Game.Tears.Active == null, 2f, "tear sealed (one active tear, cleanly closed)");
            Game.Passport.Close();
            yield return new WaitForSeconds(0.4f);

            // The game: the bubble shooter, with the real controls, to the win.
            yield return PlayBubbles(paris, "05_bubbles");
            yield return WaitUntil(() => Game.Campaign.IsVerified(CountryId.France), 4f, "France seal verified by the bubble shooter");
            yield return WaitUntil(() => Game.Campaign.HasFlag(StoryFlags.CounterfeitRevealed), 8f, "counterfeit HOME stamp revealed");
            yield return new WaitForSeconds(1.5f);
            yield return Shot("08_counterfeit_stamp");
            yield return WaitUntil(() => MenuSystem.I.IsOpen, 16f, "verifying France ends the demo: results shown");
            Check(Game.Story.WaitingStamp == null, "the demo offers no stamp run (it ends at France)");
            yield return Shot("09_demo_results");
            Check(!File.Exists(SaveSystem.PathOf(SaveSystem.CampaignFile)) || SaveSystem.WritesSuspended, "demo did not write the campaign save");
            MenuSystem.I.HideAll();
        }

        // ================================================================== Egypt (the story's first country)
        IEnumerator EgyptFlow()
        {
            Note("--- Egypt: the temple run ---");
            Game.Story.StopAllCoroutines();
            yield return TempleRunGame("10_egypt", true);
            // A revisit after the win (from another country: a start in the cell the player is already in does not re-enter
            // it): the tombs stand open and empty, no hunt.
            yield return Enter(CellId.Paris);
            yield return Enter(CellId.Egypt, c => c.Verify(CountryId.Egypt));
            var e = (EgyptCell)Game.Cells.Active;
            Check(!e.Run.Live && !e.Hunt.Running && e.Run.Mummies.All(m => m.State == TempleRun.MummyState.Gone) && e.Run.CoinList.All(c => c.Taken),
                "revisiting a verified Egypt: the run is over (tombs empty, coins gone, no hunt) (live " + e.Run.Live + ", hunt " + e.Hunt.Running +
                ", mummies " + string.Join(",", e.Run.Mummies.Select(m => m.State.ToString()[0])) + ", coins left " + e.Run.CoinList.Count(c => !c.Taken) + ", verified " + Game.Campaign.IsVerified(CountryId.Egypt) + ")");
            Check(e.Encounter.Objective.Contains("portal"), "the objective points at the portal");
        }

        // ================================================================== lifecycle (Paris)
        /// <summary>The Paris part of --autotest=lifecycle: in the demo the hunting figure catches the player (sudden death,
        /// Retry), then the negative cases; leaves the figure out on the street for the pause check that follows.</summary>
        IEnumerator ParisLifecycleCases()
        {
            Note("--- Sudden death and retry (Paris demo) ---");
            Game.Story.StartDemo();
            yield return WaitUntil(() => Game.Cells.Active != null && Game.Cells.Active.Id == CellId.Paris && !Game.Story.Busy, 20f, "demo re-enters Paris");
            yield return new WaitForSeconds(1.2f);
            var paris = (ParisCell)Game.Cells.Active;
            var board = paris.Bubbles;
            paris.Hunt.Begin(0f);
            yield return WaitUntil(() => Game.Passenger.Visible && Game.Passenger.Threatening, 4f, "the figure in the raincoat comes down the street, threatening");
            var pc = Game.Passenger;
            // It comes for the player on the open street (towards the arrival point): it runs them down, and on contact it
            // lunges and has them at once (no slow drain).
            var openSide = Game.Cells.Active.Arrival.position - pc.transform.position; openSide.y = 0f;
            openSide = openSide.sqrMagnitude > 0.01f ? openSide.normalized : -pc.transform.forward;
            var stand = pc.transform.position + openSide * 3.2f;
            Game.Rig.PlaceAt(new Vector3(stand.x, pc.transform.position.y, stand.z), Quaternion.LookRotation(-openSide).eulerAngles.y);
            Game.Campaign.vitality = 1f / StoryDirector.Hearts; // on the last heart: this catch is the end
            yield return null;
            pc.StartChase();
            float Gap() { var d = pc.transform.position - Game.Rig.Feet; d.y = 0f; return d.magnitude; }
            yield return WaitUntil(() => Gap() < pc.DrainRange, 5f, "the Passenger runs the player down");
            float caughtAt = Time.realtimeSinceStartup;
            yield return WaitUntil(() => MenuSystem.I.IsOpen && MenuSystem.I.CurrentPage == "The Margin takes hold", 4f, "reaching the player kills at once: the failure card appears");
            Check(Time.realtimeSinceStartup - caughtAt < 2.5f, "death is sudden (under 2.5 s from contact to the failure card)");
            yield return new WaitForSecondsRealtime(0.6f);
            Check(Fader.I.Alpha < 0.5f, "the failure card is not hidden behind the death fade (fade " + Fader.I.Alpha.ToString("0.00") + ")");
            yield return Shot("L1_failure_card");
            yield return Press("Retry from checkpoint");
            yield return WaitUntil(() => Game.Cells.Active != null && Game.Cells.Active.Id == CellId.Paris && !Game.Story.Busy && !MenuSystem.I.IsOpen, 10f, "Retry restarts at the embankment");
            yield return new WaitForSeconds(1.0f);
            Check(Mathf.Approximately(Game.Campaign.vitality, 1f) && !board.Won && board.Cleared == 0 && board.Shots == 0 && paris.Hunt.Running && !Game.Campaign.IsVerified(CountryId.France),
                "after Retry: full vitality, a fresh bubble board, the hunt armed again, France not verified");

            Note("--- Negative cases (Paris demo) ---");
            paris.Hunt.Stop();
            Game.Passenger.Despawn();
            var hand = Game.Rig.MainHand;
            Check(!paris.Trace.Revealed && !paris.Trace.CanUse(hand), "no trace to press in Paris: only the bubble board verifies France");
            if (Game.Rig is DesktopRig desk)
            {
                // From the arrival point the board is out of the desktop ray's reach: walk to the ring.
                desk.PlaceAt(paris.Arrival.position, 0f);
                yield return null;
                yield return AimAtPoint(board.CentreWorld);
                Check(hand.Focused != board.Surface, "from the arrival the board is out of reach (" + Vector3.Distance(desk.Head.position, board.CentreWorld).ToString("0.0") + " m): the player walks to the ring");
            }
            // A shot that matches nothing pops nothing and verifies nothing.
            Game.Rig.PlaceAt(paris.FiringSpot.position, paris.FiringSpot.eulerAngles.y);
            yield return new WaitForSeconds(0.3f);
            Vector2 dud = Vector2.up;
            bool found = false;
            for (float a = 20f; a <= 160f && !found; a += 2f)
            {
                var d = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                if (board.Predict(d, out var c, out int pops, out _) && pops == 0 && c.y < BubbleBoard.MaxRows - 1) { dud = d; found = true; }
            }
            if (found)
            {
                yield return AimAtPoint(board.AimPointFor(dud));
                int before = board.Remaining;
                hand.PressUse();
                for (float w = 0f; (board.Busy || board.Shots == 0) && w < 4f; w += Time.deltaTime) yield return null;
                Check(board.Shots == 1 && board.Cleared == 0 && board.Remaining == before + 1 && !Game.Campaign.IsVerified(CountryId.France),
                    "a bubble that matches nothing just sticks: nothing pops, France is not verified");
            }
            else Note("(no non-matching shot on this board)");
            // A shot aimed below the launcher still goes up the board.
            var down = BubbleBoard.ClampDir(new Vector2(0.3f, -1f));
            Check(down.y > 0.1f, "a shot aimed below the launcher is turned upward");

            // The hunt comes back for the pause check that follows.
            paris.Hunt.Begin(0f);
            yield return WaitUntil(() => Game.Passenger.Visible, 4f, "the hunt comes back");
        }

        /// <summary>Revisiting Paris (lifecycle): the bubble board starts fresh and the hunt is armed again.</summary>
        bool ParisFreshOnRevisit()
        {
            var paris = Game.Cells.Active as ParisCell;
            if (paris == null || paris.Bubbles == null) return false;
            var b = paris.Bubbles;
            bool verified = Game.Campaign.IsVerified(CountryId.France);
            Note("Paris revisit: board " + b.Remaining + " bubbles, cleared " + b.Cleared + ", shots " + b.Shots + ", won " + b.Won + ", hunt " + paris.Hunt.Running);
            return verified ? b.Won && !paris.Hunt.Running : !b.Won && b.Cleared == 0 && b.Shots == 0 && b.Remaining >= 45 && paris.Hunt.Running;
        }

        // ================================================================== guide (Egypt)
        /// <summary>The guide in Egypt, played through: the run step's marker keeps ahead of the runner, crossing the finish
        /// line verifies Egypt and the guide moves on to the stamp in the portal.</summary>
        IEnumerator GuideEgyptRun()
        {
            yield return Enter(CellId.Egypt);
            Guide.I.DismissBriefing();
            var e = (EgyptCell)Game.Cells.Active;
            e.Hunt.Stop();
            Game.Passenger?.Despawn();
            yield return new WaitForSeconds(0.3f);
            var g = Guide.I.Current;
            int runStep = g.Steps.FindIndex(s => s.Label == "Run");
            int stampStep = g.Steps.FindIndex(s => s.Label == "Stamp");
            Check(runStep == 0 && Guide.I.StepIndex == runStep && Guide.I.Target.HasValue, "Egypt starts on the run step, with its marker down the causeway");
            var first = Guide.I.Target ?? Vector3.zero;
            Game.Rig.PlaceAt(e.transform.TransformPoint(new Vector3(TempleRun.LaneX(1), TempleRun.FloorY, 24f)), 0f);
            yield return new WaitForSeconds(0.35f);
            var ahead = Guide.I.Target;
            Check(ahead.HasValue && e.transform.InverseTransformPoint(ahead.Value).z > e.transform.InverseTransformPoint(first).z + 10f && e.transform.InverseTransformPoint(ahead.Value).z > 24.5f,
                "the run step's marker keeps ahead of the runner");
            yield return Shot("g_Egypt_run");
            Game.Rig.PlaceAt(e.transform.TransformPoint(new Vector3(TempleRun.RunX, TempleRun.FloorY, TempleRun.Z1 + 1.5f)), 0f);
            yield return WaitUntil(() => Game.Campaign.IsVerified(CountryId.Egypt), 6f, "crossing the temple run's finish line verifies Egypt");
            yield return WaitUntil(() => Guide.I.StepIndex == stampStep && Game.Story.WaitingStamp != null, 8f, "the guide moves on to the stamp");
            yield return new WaitForSeconds(0.4f);
            yield return Shot("g_Egypt_stamp");
            Check(Guide.I.Target.HasValue && Game.Story.WaitingStamp != null && (Guide.I.Target.Value - Game.Story.WaitingStamp.transform.position).magnitude < 0.5f,
                "the marker is on the stamp");
        }

        // ================================================================== models (Egypt)
        /// <summary>The temple run for the art review: the gate from the forecourt, the causeway, a mummy out of its tomb by a
        /// fallen stone and the coins, and the portal.</summary>
        IEnumerator EgyptRunReview(EgyptCell egypt)
        {
            var run = egypt.Run;
            Check(run != null && run.Portal != null && run.Mummies.Count >= 6 && run.CoinTotal >= 25 && run.Obstacles.Count >= 6, "Egypt: the temple run is built (gate, walls, tombs, stones, coins, portal)");
            if (run == null) yield break;
            yield return Frame(egypt, TempleRun.StartLocal + new Vector3(0f, 1.6f, -2.5f), new Vector3(TempleRun.RunX, 3.2f, TempleRun.Z0), "m15b_egypt_run_gate");
            yield return Frame(egypt, new Vector3(TempleRun.LaneX(1), 1.6f, 1f), new Vector3(TempleRun.RunX, 1.1f, 26f), "m15c_egypt_causeway");
            run.PoseForReview(1);
            var m = run.Mummies[1];
            yield return Frame(egypt, new Vector3(TempleRun.LaneX(1), 1.6f, m.DoorZ - 4.2f), new Vector3(m.X, 1.2f, m.Z), "m15d_egypt_mummy");
            run.ResetRun();
            yield return Frame(egypt, TempleRun.PortalLocal + new Vector3(0f, 1.6f, -6f), TempleRun.PortalLocal + Vector3.up * 1.6f, "m15e_egypt_portal");
        }
    }
}

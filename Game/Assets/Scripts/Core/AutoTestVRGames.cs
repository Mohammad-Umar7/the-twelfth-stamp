using System.Collections;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// The three country games with the VR rig (run with --mode=vr --vr-preview, no headset): every claw button, the
    /// golden cat and the Taj Mahal pieces are worked with a VR hand the way a player does it (the hand's near-grab sphere
    /// on the control, then trigger or grip), the aim ray picks the DROP button from a step back, and the maze's energy,
    /// water and portal work for the VR rig. Not part of "all".
    /// </summary>
    public partial class AutoTest
    {
        IEnumerator VRGamesFlow()
        {
            Note("--- VR games: claw machine, maze, Taj Mahal puzzle (VR rig, scripted hands) ---");
            var vr = Game.Rig as VRRig;
            Check(vr != null, "VR rig in use (run with --mode=vr --vr-preview)");
            if (vr == null) yield break;
            var side = Game.Input.DominantHand == HandSide.Left ? HandSide.Left : HandSide.Right;
            var hand = vr.MainHand;

            // ---------------------------------------------------------------- Japan
            yield return Enter(CellId.Japan);
            var jp = (JapanCell)Game.Cells.Active;
            jp.Hunt.Begin(0.3f);
            yield return WaitUntil(() => Game.Passenger != null && Game.Passenger.Visible, 4f, "the lantern figure hunts the VR player");
            Check(!Game.Passenger.AllowChase, "it stalks (rushes only now and then) while the claw machine is played");
            jp.Hunt.Stop();
            Game.Passenger.Despawn();
            var claw = jp.Claw;
            Game.Rig.PlaceAt(claw.transform.TransformPoint(new Vector3(0f, 0f, -1.25f)), claw.transform.eulerAngles.y);
            yield return new WaitForSeconds(0.3f);
            VRLook(claw.transform.TransformPoint(new Vector3(0f, 1.6f, -1.25f)), claw.transform.TransformPoint(new Vector3(-0.1f, 1.05f, -0.6f)));
            foreach (var b in new[] { claw.Left, claw.Right, claw.Back, claw.Front, claw.Drop })
            {
                yield return TouchWith(vr, side, b.transform.position + Vector3.up * 0.03f);
                Check(hand.Focused == b, "VR hand touching '" + b.Prompt + "' focuses that button (not a neighbour)");
            }
            yield return Shot("vr01_japan_hand_on_controls");
            for (int i = 0; i < claw.GoldCol; i++) yield return Press(vr, side, claw.Right);
            for (int i = 0; i < claw.GoldRow; i++) yield return Press(vr, side, claw.Back);
            Check(claw.ClawCol == claw.GoldCol && claw.ClawRow == claw.GoldRow, "VR trigger presses line the claw up over the golden cat");
            // The aim ray from a step back picks DROP.
            yield return TouchWith(vr, side, claw.transform.TransformPoint(new Vector3(0.3f, 1.35f, -1.05f)));
            var aim = vr.AimOf(side);
            aim.position = hand.HoldPoint.position;
            aim.rotation = Quaternion.LookRotation(claw.Drop.transform.position - aim.position);
            yield return null; yield return null;
            Check(hand.Focused == claw.Drop, "VR aim ray from a step back picks the DROP button (focused " + (hand.Focused != null ? hand.Focused.name : "nothing") + ")");
            if (hand.Focused == claw.Drop) hand.PressUse(); else claw.Drop.Use(hand);
            yield return WaitUntil(() => claw.GoldenOut, 10f, "the claw delivers the golden lucky cat (VR)");
            if (!claw.GoldenOut) yield break;
            yield return TouchWith(vr, side, claw.Golden.transform.position + Vector3.up * 0.12f);
            Check(hand.Focused == claw.Golden && hand.FocusIsNear, "VR hand reaches the golden cat on the prize tray");
            yield return Shot("vr02_japan_cat_on_tray");
            hand.PressGrab();
            Check(hand.Held == claw.Golden, "VR grip takes the golden lucky cat");
            yield return WaitUntil(() => Game.Campaign.IsVerified(CountryId.Japan), 6f, "taking the cat in VR verifies Japan");
            hand.ReleaseGrab();

            // ---------------------------------------------------------------- Saudi Arabia
            yield return Enter(CellId.Riyadh);
            var ry = (RiyadhCell)Game.Cells.Active;
            var start = ry.transform.TransformPoint(ry.Maze.CellCenter(ry.Maze.Start));
            VRLook(start + Vector3.up * 1.6f, start + new Vector3(0f, 1.4f, 5f));
            yield return Shot("vr03_riyadh_maze");
            ry.Hunt.Begin(0.3f);
            yield return WaitUntil(() => Game.Passenger != null && Game.Passenger.Visible, 4f, "the figure follows the VR player into the maze");
            ry.Hunt.Stop();
            Game.Passenger.Despawn();
            float before = Game.Campaign.vitality;
            yield return new WaitForSeconds(1.5f);
            Check(Game.Campaign.vitality < before, "energy drains in the maze (VR)");
            Game.Campaign.vitality = 0.4f;
            var glass = ry.Maze.Waters[1];
            Game.Rig.PlaceAt(ry.transform.TransformPoint(glass.localPosition + new Vector3(-0.5f, 0f, -0.5f)), 45f);
            yield return WaitUntil(() => !SandMaze.IsFull(glass), 2f, "the VR player drinks a water glass by walking up to it");
            Check(Game.Campaign.vitality > 0.7f, "the water restores energy (VR)");
            var portal = ry.transform.TransformPoint(ry.Maze.PortalLocal);
            Game.Rig.PlaceAt(portal + new Vector3(0f, 0f, -4.2f), 0f);
            yield return WaitUntil(() => Game.Campaign.IsVerified(CountryId.SaudiArabia), 6f, "out of the maze verifies Saudi Arabia (VR)");
            yield return WaitUntil(() => Game.Story.WaitingStamp != null, 8f, "the stamp waits in the portal (VR)");
            Check(Game.Story.WaitingStamp != null && Vector3.Distance(Game.Story.WaitingStamp.transform.position, portal + Vector3.up * 1.6f) < 0.5f, "the stamp hangs in the portal ring");
            VRLook(portal + new Vector3(0f, 1.6f, -4.2f), portal + Vector3.up * 1.6f);
            yield return Shot("vr04_riyadh_portal");

            // ---------------------------------------------------------------- India
            yield return Enter(CellId.Agra);
            var ag = (AgraCell)Game.Cells.Active;
            ag.Hunt.Begin(0.3f);
            yield return WaitUntil(() => Game.Passenger != null && Game.Passenger.Visible, 4f, "the figure rises from the pool for the VR player");
            ag.Hunt.Stop();
            Game.Passenger.Despawn();
            var pz = ag.Puzzle;
            Game.Rig.PlaceAt(pz.transform.TransformPoint(new Vector3(0f, 0f, -0.75f)), pz.transform.eulerAngles.y);
            yield return new WaitForSeconds(0.3f);
            VRLook(pz.transform.TransformPoint(new Vector3(0f, 1.6f, -0.75f)), pz.transform.TransformPoint(new Vector3(-0.08f, 0.9f, 0.05f)));
            int placed = 0;
            for (int i = 0; i < TajJigsaw.Count; i++)
            {
                var piece = pz.Piece(i);
                yield return TouchWith(vr, side, piece.transform.position + Vector3.up * 0.02f);
                if (hand.Focused != piece) { Note("VR hand did not focus piece " + i + " (focused " + (hand.Focused != null ? hand.Focused.name : "nothing") + ")"); continue; }
                hand.PressGrab();
                if (hand.Held != piece) continue;
                if (i == 0)
                {
                    // Held over the wrong place: nothing happens.
                    yield return TouchWith(vr, side, pz.Slot(4).transform.position + pz.Slot(4).transform.up * 0.05f);
                    Check(pz.Slot(4).Current == null && hand.Held == piece, "a piece held over the wrong place does not go in");
                    yield return Shot("vr05_agra_piece_in_hand");
                }
                yield return TouchWith(vr, side, pz.Slot(i).transform.position + pz.Slot(i).transform.up * 0.04f);
                yield return null;
                if (pz.Slot(i).Current == piece) placed++;
                else Note("piece " + i + " not placed: held=" + (hand.Held == piece) + " hold-to-slot=" + Vector3.Distance(hand.HoldPoint.position, pz.Slot(i).transform.position).ToString("0.00") + " piece-to-slot=" + Vector3.Distance(piece.transform.position, pz.Slot(i).transform.position).ToString("0.00") + " parent=" + (piece.transform.parent != null ? piece.transform.parent.name : "-"));
            }
            Check(placed == TajJigsaw.Count, "VR hand: each piece taken and brought to its place snaps in (" + placed + "/" + TajJigsaw.Count + ")");
            yield return WaitUntil(() => Game.Campaign.IsVerified(CountryId.India), 6f, "the finished picture verifies India (VR)");
            yield return Shot("vr06_agra_solved");
        }

        /// <summary>The claw machine with desktop controls: aim the view at each control and press E, then take the cat.</summary>
        IEnumerator DeskClawFlow()
        {
            Note("--- Desktop claw machine: aim and E ---");
            var desk = Game.Rig as DesktopRig;
            Check(desk != null, "desktop rig in use");
            if (desk == null) yield break;
            yield return Enter(CellId.Japan);
            var jp = (JapanCell)Game.Cells.Active;
            jp.Hunt.Stop();
            Game.Passenger.Despawn();
            var claw = jp.Claw;
            var hand = desk.MainHand;
            var feet = claw.transform.TransformPoint(new Vector3(0f, 0f, -1.35f));
            IEnumerator Aim(Vector3 at)
            {
                var eye = feet + Vector3.up * DesktopRig.EyeHeight;
                var d = at - eye;
                Game.Rig.PlaceAt(feet, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
                desk.SetPitch(-Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg);
                yield return null; yield return null; yield return null;
            }
            for (int i = 0; i < claw.GoldCol; i++) { yield return Aim(claw.Right.transform.position); Check(hand.Focused == claw.Right, "desktop aim focuses the right arrow"); hand.PressUse(); yield return new WaitForSeconds(0.45f); }
            for (int i = 0; i < claw.GoldRow; i++) { yield return Aim(claw.Back.transform.position); Check(hand.Focused == claw.Back, "desktop aim focuses the back arrow"); hand.PressUse(); yield return new WaitForSeconds(0.45f); }
            yield return Aim(claw.Drop.transform.position);
            Check(hand.Focused == claw.Drop, "desktop aim focuses DROP");
            hand.PressUse();
            yield return WaitUntil(() => claw.GoldenOut, 10f, "the claw delivers the golden cat (desktop)");
            if (!claw.GoldenOut) yield break;
            // The playtest case: a grab the encounter could not take (here: not live), then the cat let go in mid-air.
            var tray = claw.Golden.transform.position;
            jp.Encounter.Entered = false;
            hand.Grab(claw.Golden);
            hand.ForceRelease(false);
            jp.Encounter.Entered = true;
            Check(!Game.Campaign.IsVerified(CountryId.Japan), "a grab the encounter missed does not verify");
            jp.Encounter.OnEnter(false);
            Check(Vector3.Distance(claw.Golden.transform.position, tray) < 0.05f, "a won but uncounted cat goes back on the prize tray");
            jp.Hunt.Stop();
            Game.Passenger.Despawn();
            yield return Aim(claw.Golden.transform.position + Vector3.up * 0.12f);
            Check(hand.Focused == claw.Golden, "desktop aim focuses the golden cat on the tray (focused " + (hand.Focused != null ? hand.Focused.name : "nothing") + ")");
            hand.PressUse();
            yield return null;
            Check(hand.Held == claw.Golden, "E takes the golden cat into the hand");
            yield return new WaitForSeconds(3f);
            var cam = Game.Rig.Cam.transform;
            float dist = Vector3.Distance(claw.Golden.transform.position, cam.position);
            Check(hand.Held == claw.Golden, "the cat stays in the hand through the seal verify (" + dist.ToString("0.00") + " m from the eyes; the arm lowers it while the passport is up)");
            Check(Game.Campaign.IsVerified(CountryId.Japan), "taking the cat verifies Japan (desktop)");
            yield return Shot("d01_japan_cat_in_hand");
        }

        /// <summary>The Taj Mahal puzzle with desktop controls: aim at a piece and E, then aim at its place (it snaps in).</summary>
        IEnumerator DeskTajFlow()
        {
            Note("--- Desktop Taj Mahal puzzle: aim, E, aim at the place ---");
            var desk = Game.Rig as DesktopRig;
            if (desk == null) yield break;
            yield return Enter(CellId.Agra);
            var ag = (AgraCell)Game.Cells.Active;
            ag.Hunt.Stop();
            Game.Passenger.Despawn();
            var pz = ag.Puzzle;
            var hand = desk.MainHand;
            var feet = pz.transform.TransformPoint(new Vector3(0f, 0f, -0.85f));
            IEnumerator Aim(Vector3 at)
            {
                var eye = feet + Vector3.up * DesktopRig.EyeHeight;
                var d = at - eye;
                Game.Rig.PlaceAt(feet, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
                desk.SetPitch(-Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg);
                yield return null; yield return null; yield return null;
            }
            Check(pz.Placed == 0, "the Taj Mahal pieces start beside the board");
            yield return Aim(pz.BoardPosition);
            yield return Shot("d02_agra_puzzle_start");
            for (int i = 0; i < TajJigsaw.Count; i++)
            {
                var piece = pz.Piece(i);
                yield return Aim(piece.transform.position);
                Check(hand.Focused == piece, "desktop aim focuses piece " + i);
                hand.PressUse();
                yield return null;
                Check(hand.Held == piece, "E picks up piece " + i);
                if (i == 0)
                {
                    yield return Aim(pz.Slot(4).transform.position);
                    Check(hand.Focused != pz.Slot(4), "the wrong place does not take the piece");
                    yield return Shot("d03_agra_piece_in_hand");
                }
                yield return Aim(pz.Slot(i).transform.position);
                yield return new WaitForSeconds(0.5f);
                Check(pz.Slot(i).Current == piece, "aimed at its own place, piece " + i + " snaps in");
            }
            yield return WaitUntil(() => Game.Campaign.IsVerified(CountryId.India), 6f, "the finished picture verifies India (desktop)");
            yield return Aim(pz.BoardPosition);
            yield return Shot("d04_agra_puzzle_done");
        }

        /// <summary>
        /// The Saudi maze has one clear way through, and a player can actually walk it: the desktop rig is steered from cell
        /// to cell along the solution with its own movement (TestMove), out through the exit to the portal; then the stamp
        /// must wait close by, in the ring.
        /// </summary>
        IEnumerator MazeWalkFlow()
        {
            Note("--- Saudi maze: walk the way out ---");
            var desk = Game.Rig as DesktopRig;
            if (desk == null) yield break;
            yield return Enter(CellId.Riyadh);
            var ry = (RiyadhCell)Game.Cells.Active;
            ry.Hunt.Stop();
            Game.Passenger.Despawn();
            var m = ry.Maze;
            int blocked = 0;
            for (int i = 0; i + 1 < m.Solution.Count; i++)
            {
                var a = ry.transform.TransformPoint(m.CellCenter(m.Solution[i]) + Vector3.up);
                var b = ry.transform.TransformPoint(m.CellCenter(m.Solution[i + 1]) + Vector3.up);
                if (Physics.SphereCast(a, 0.3f, (b - a).normalized, out _, (b - a).magnitude, 1 << Layers.Default, QueryTriggerInteraction.Ignore)) blocked++;
            }
            Check(m.Solution.Count >= 2 && blocked == 0, "the maze's one way out is open all along (" + m.Solution.Count + " cells, " + blocked + " blocked)");
            // A top-down picture of the maze for review.
            var camGo = new GameObject("MazeShotCam");
            var cam = camGo.AddComponent<Camera>();
            cam.depth = 100;
            cam.fieldOfView = 55f;
            var mid = ry.transform.TransformPoint(m.Origin + new Vector3(SandMaze.W * SandMaze.Size / 2f, 0f, SandMaze.H * SandMaze.Size / 2f + 3f));
            camGo.transform.position = mid + Vector3.up * 26f;
            camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            yield return new WaitForSeconds(0.4f);
            yield return Shot("d05_riyadh_maze_top");
            Destroy(camGo);
            // Walk it with the rig's own movement.
            var waypoints = new System.Collections.Generic.List<Vector3>();
            foreach (var c in m.Solution) waypoints.Add(ry.transform.TransformPoint(m.CellCenter(c)));
            waypoints.Add(ry.transform.TransformPoint(m.CellCenter(m.Exit) + new Vector3(0f, 0f, SandMaze.Size)));
            waypoints.Add(ry.transform.TransformPoint(m.PortalLocal + new Vector3(0f, 0f, -2.5f)));
            int reached = 0;
            foreach (var w in waypoints)
            {
                float t = 0f;
                while (t < 6f)
                {
                    var to = w - Game.Rig.Feet; to.y = 0f;
                    if (to.magnitude < 0.45f) break;
                    desk.SetLook(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 0f);
                    desk.TestMove = new Vector2(0f, 1f);
                    t += Time.deltaTime;
                    yield return null;
                    if (Game.Campaign.IsVerified(CountryId.SaudiArabia)) break;
                }
                desk.TestMove = Vector2.zero;
                var left = w - Game.Rig.Feet; left.y = 0f;
                if (left.magnitude < 0.6f || Game.Campaign.IsVerified(CountryId.SaudiArabia)) reached++;
                else
                {
                    Note("stuck walking to waypoint " + reached + " (" + left.magnitude.ToString("0.0") + " m short): locks " + Game.InputLocks + ", menu " + Game.InMenu +
                         ", paused " + Game.Paused + ", busy " + Game.Story.Busy + ", speed " + desk.MoveSpeed.ToString("0.0"));
                    break;
                }
                if (Game.Campaign.IsVerified(CountryId.SaudiArabia)) break;
            }
            desk.TestMove = null;
            yield return WaitUntil(() => Game.Campaign.IsVerified(CountryId.SaudiArabia), 4f, "walking the maze's way out reaches the portal and verifies Saudi Arabia");
            yield return WaitUntil(() => Game.Story.WaitingStamp != null, 8f, "the stamp waits after the maze");
            if (Game.Story.WaitingStamp != null)
            {
                var d = Game.Story.WaitingStamp.transform.position - Game.Rig.Feet; d.y = 0f;
                Check(d.magnitude < 9f, "the stamp waits close by, in the portal (" + d.magnitude.ToString("0.0") + " m)");
            }
        }

        /// <summary>After a country's game the stamp waits a few metres from the player, not out at the landmark.</summary>
        IEnumerator NearStampFlow()
        {
            Note("--- The stamp waits close by ---");
            yield return Enter(CellId.Agra);
            var ag = (AgraCell)Game.Cells.Active;
            ag.Hunt.Stop();
            Game.Passenger.Despawn();
            Game.Rig.PlaceAt(ag.Puzzle.transform.TransformPoint(new Vector3(0f, 0f, -0.9f)), ag.Puzzle.transform.eulerAngles.y + 180f);
            yield return null;
            Game.Story.VerifyCountry(CountryId.India, null);
            yield return WaitUntil(() => Game.Story.WaitingStamp != null, 8f, "India's stamp comes out");
            if (Game.Story.WaitingStamp == null) yield break;
            var d = Game.Story.WaitingStamp.transform.position - Game.Rig.Feet; d.y = 0f;
            Check(d.magnitude > 2.5f && d.magnitude < 5f, "the stamp waits a few metres away (" + d.magnitude.ToString("0.0") + " m), not out at the Taj Mahal");
            yield return Shot("d06_agra_stamp_close");
        }

        /// <summary>Moves a scripted VR hand so its grab point sits at <paramref name="world"/>, pointing its aim forward.</summary>
        IEnumerator TouchWith(VRRig vr, HandSide side, Vector3 world)
        {
            var hand = vr.MainHand;
            var grip = vr.GripOf(side);
            grip.position += world - hand.HoldPoint.position;
            var aim = vr.AimOf(side);
            if (aim != null && Game.Rig.Head != null)
            {
                aim.position = hand.HoldPoint.position - (world - Game.Rig.Head.position).normalized * 0.05f;
                var fwd = world - Game.Rig.Head.position;
                if (fwd.sqrMagnitude > 0.001f) aim.rotation = Quaternion.LookRotation(fwd.normalized);
            }
            yield return null;
            yield return null;
        }

        /// <summary>Touches a control with the VR hand and pulls the trigger.</summary>
        IEnumerator Press(VRRig vr, HandSide side, Interactable it)
        {
            yield return TouchWith(vr, side, it.transform.position + it.transform.up * 0.03f);
            if (vr.MainHand.Focused == it) vr.MainHand.PressUse();
            else Note("VR hand did not focus " + it.name + " (focused " + (vr.MainHand.Focused != null ? vr.MainHand.Focused.name : "nothing") + ")");
            yield return new WaitForSeconds(0.45f);
        }

        /// <summary>Poses the VR camera (scripted, --vr-preview) for a screenshot.</summary>
        static void VRLook(Vector3 eye, Vector3 target)
        {
            var cam = Game.Rig != null ? Game.Rig.Cam : null;
            if (cam == null) return;
            cam.transform.position = eye;
            cam.transform.rotation = Quaternion.LookRotation(target - eye);
        }
    }
}

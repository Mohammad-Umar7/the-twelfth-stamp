using System.Collections;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// --autotest=arcade_b: New York's impostor vote and Rio's ring toss, played through the real controls. Desktop: the
    /// view is aimed at each control and E pressed (Interactor.PressUse, as DeskClawFlow does). VR (--mode=vr --vr-preview):
    /// the scripted hand's near-grab sphere on each control and the trigger or grip, the aim ray for the ring throws.
    /// Covers a locked vote, a wrong wire, a wrong vote (the figure rushes), a missed ring and the wins. Also the two
    /// countries-flow blocks (CountriesUsa, CountriesBrazil). Not part of "all".
    /// </summary>
    public partial class AutoTest
    {
        IEnumerator ArcadeBFlow()
        {
            Note("--- Arcade B: New York impostor vote and Rio ring toss (" + Game.Mode + ") ---");
            if (Game.Rig is VRRig vr)
            {
                yield return ArcBNewYorkVR(vr);
                yield return ArcBRioVR(vr);
            }
            else if (Game.Rig is DesktopRig desk)
            {
                yield return ArcBNewYorkDesk(desk);
                yield return ArcBRioDesk(desk);
            }
            else Check(false, "a desktop or VR rig is in use");
        }

        // ------------------------------------------------------------------ New York, desktop
        IEnumerator ArcBNewYorkDesk(DesktopRig desk)
        {
            yield return Enter(CellId.NewYork);
            var ny = (NewYorkCell)Game.Cells.Active;
            var s = ny.Station;
            var enc = (NewYorkEncounter)ny.Encounter;
            var hand = desk.MainHand;
            yield return ArcBCrewChecks(ny);
            Vector3 L(Vector3 local) => s.transform.TransformPoint(local);

            // The arcade from the arrival and from the walk, then a vote before the tasks: locked.
            Guide.I?.DismissBriefing();
            yield return ArcBAimDesk(desk, ny.Arrival.position, s.Crew[1].transform.position + Vector3.up * 1.4f);
            yield return new WaitForSeconds(0.3f);
            yield return Shot("ab00_ny_from_arrival");
            yield return ArcBAimDesk(desk, L(new Vector3(2.0f, 0f, 0.4f)), s.Crew[1].transform.position + Vector3.up * 0.8f);
            yield return Shot("ab01_ny_arcade");
            yield return ArcBAimDesk(desk, L(new Vector3(ImpostorGame.CrewX[0], 0f, ImpostorGame.VoteZ - 0.85f)), s.VotePosition(0) + Vector3.up * 0.02f);
            Check(hand.Focused == s.Votes[0], "desktop aim focuses a VOTE button (focused " + ArcBName(hand.Focused) + ")");
            hand.PressUse();
            yield return null;
            Check(s.LockedPresses == 1 && !s.Ejected && s.WrongVotes == 0, "voting is locked until both tasks are done");

            // Task 1: a wrong wire first (it starts again), then the order on the little screen.
            var wireFeet = L(ImpostorGame.WiringAt + new Vector3(0f, 0f, -0.8f));
            int wrong = s.WireOrder[1];
            yield return ArcBAimDesk(desk, wireFeet, s.WireButtons[wrong].transform.position + Vector3.up * 0.02f);
            Check(hand.Focused == s.WireButtons[wrong], "desktop aim focuses a wire button");
            hand.PressUse();
            yield return null;
            Check(s.WireStep == 0 && s.WrongWires == 1 && !s.WiringDone, "a wire pressed out of order starts the wiring again");
            int focused = 0;
            for (int k = 0; k < 3; k++)
            {
                var b = s.WireButtons[s.WireOrder[k]];
                yield return ArcBAimDesk(desk, wireFeet, b.transform.position + Vector3.up * 0.02f);
                if (hand.Focused == b) focused++;
                hand.PressUse();
                yield return new WaitForSeconds(0.2f);
                if (k == 1) yield return Shot("ab02_ny_wiring");
            }
            Check(focused == 3, "desktop aim focuses each wire button in turn (" + focused + "/3)");
            Check(s.WiringDone, "the three wires pressed in the screen's order fix the wiring");

            // Task 2: take the keycard, put it in the reader.
            var cardFeet = L(ImpostorGame.CardAt + new Vector3(0f, 0f, -0.8f));
            yield return ArcBAimDesk(desk, cardFeet, s.KeycardPosition + Vector3.up * 0.01f);
            Check(hand.Focused == s.Keycard, "desktop aim focuses the crew keycard (focused " + ArcBName(hand.Focused) + ")");
            hand.PressUse();
            yield return null;
            Check(hand.Held == s.Keycard, "E takes the keycard into the hand");
            yield return ArcBAimDesk(desk, cardFeet, s.ReaderPosition);
            Check(hand.Focused == s.Reader, "with the keycard in hand the reader is focused (focused " + ArcBName(hand.Focused) + ")");
            hand.PressUse();
            yield return WaitUntil(() => s.CardDone, 2f, "E puts the keycard in the reader: it swipes and is accepted");
            Check(s.TasksDone && s.ScreenText.Contains("IMPOSTOR SPOTTED"), "both tasks done: the security screen shows the clue");
            yield return ArcBAimDesk(desk, L(new Vector3(2.2f, 0f, 2.3f)), s.ScreenPosition);
            yield return Shot("ab03_ny_security_screen");

            // The noisemaker is still in the booth and still switches on and off.
            Check(ny.Noisemaker != null && ny.Noisemaker.OnActivate != null, "the noisemaker stays in the booth with its on/off switch");
            if (ny.Noisemaker != null && ny.Noisemaker.OnActivate != null)
            {
                ny.Noisemaker.OnActivate(ny.Noisemaker, hand);
                bool on = enc.NoiseOn;
                ny.Noisemaker.OnActivate(ny.Noisemaker, hand);
                Check(on && !enc.NoiseOn, "the noisemaker's toggle switches it on and off");
            }

            // A wrong vote: the crewmate was innocent, the figure rushes, and the vote can be cast again.
            int innocent = (s.Impostor + 1) % ImpostorGame.CrewCount;
            yield return ArcBAimDesk(desk, L(new Vector3(ImpostorGame.CrewX[innocent], 0f, ImpostorGame.VoteZ - 0.85f)), s.VotePosition(innocent) + Vector3.up * 0.02f);
            Check(hand.Focused == s.Votes[innocent], "desktop aim focuses the innocent crewmate's VOTE button");
            hand.PressUse();
            yield return ArcBWrongVote(ny, "desktop");

            // The right vote: ejected, the seal verified, the noisemaker granted.
            int imp = s.Impostor;
            yield return ArcBAimDesk(desk, L(new Vector3(ImpostorGame.CrewX[imp], 0f, ImpostorGame.VoteZ - 0.85f)), s.VotePosition(imp) + Vector3.up * 0.02f);
            Check(hand.Focused == s.Votes[imp], "desktop aim focuses the impostor's VOTE button");
            hand.PressUse();
            yield return ArcBRightVote(ny, desk, "desktop");
        }

        // ------------------------------------------------------------------ New York, VR
        IEnumerator ArcBNewYorkVR(VRRig vr)
        {
            var side = Game.Input.DominantHand == HandSide.Left ? HandSide.Left : HandSide.Right;
            var hand = vr.MainHand;
            yield return Enter(CellId.NewYork);
            var ny = (NewYorkCell)Game.Cells.Active;
            var s = ny.Station;
            yield return ArcBCrewChecks(ny);
            Vector3 L(Vector3 local) => s.transform.TransformPoint(local);
            float yaw = s.transform.eulerAngles.y;

            // A vote before the tasks: locked.
            var voteFeet = L(new Vector3(ImpostorGame.CrewX[0], 0f, ImpostorGame.VoteZ - 0.6f));
            Game.Rig.PlaceAt(voteFeet, yaw);
            yield return new WaitForSeconds(0.3f);
            VRLook(voteFeet + Vector3.up * 1.6f, s.Crew[1].transform.position + Vector3.up * 0.6f);
            yield return TouchWith(vr, side, s.Votes[0].transform.position + Vector3.up * 0.03f);
            Check(hand.Focused == s.Votes[0] && hand.FocusIsNear, "VR hand on a VOTE button focuses it (focused " + ArcBName(hand.Focused) + ")");
            yield return Shot("vrb01_ny_hand_on_vote");
            yield return Press(vr, side, s.Votes[0]);
            Check(s.LockedPresses == 1 && !s.Ejected, "VR: voting is locked until both tasks are done");

            // Task 1 with the trigger: one wrong wire, then the order.
            var wireFeet = L(ImpostorGame.WiringAt + new Vector3(0f, 0f, -0.6f));
            Game.Rig.PlaceAt(wireFeet, yaw);
            yield return new WaitForSeconds(0.3f);
            VRLook(wireFeet + Vector3.up * 1.6f, s.WiringPosition);
            int touched = 0;
            foreach (var b in s.WireButtons)
            {
                yield return TouchWith(vr, side, b.transform.position + Vector3.up * 0.03f);
                if (hand.Focused == b) touched++;
            }
            Check(touched == 3, "VR hand touching each wire button focuses that button, not a neighbour (" + touched + "/3)");
            yield return Press(vr, side, s.WireButtons[s.WireOrder[2]]);
            Check(s.WireStep == 0 && s.WrongWires == 1, "VR: a wire out of order starts the wiring again");
            for (int k = 0; k < 3; k++) yield return Press(vr, side, s.WireButtons[s.WireOrder[k]]);
            Check(s.WiringDone, "VR trigger presses in the screen's order fix the wiring");
            yield return Shot("vrb02_ny_wiring_fixed");

            // Task 2 with the grip: take the keycard, let go of it over the reader.
            var cardFeet = L(ImpostorGame.CardAt + new Vector3(0f, 0f, -0.6f));
            Game.Rig.PlaceAt(cardFeet, yaw);
            yield return new WaitForSeconds(0.3f);
            VRLook(cardFeet + Vector3.up * 1.6f, s.CardConsolePosition);
            yield return TouchWith(vr, side, s.KeycardPosition + Vector3.up * 0.02f);
            Check(hand.Focused == s.Keycard && hand.FocusIsNear, "VR hand reaches the keycard (focused " + ArcBName(hand.Focused) + ")");
            hand.PressGrab();
            Check(hand.Held == s.Keycard, "VR grip takes the keycard");
            yield return TouchWith(vr, side, s.ReaderPosition);
            yield return Shot("vrb03_ny_card_at_reader");
            hand.ReleaseGrab();
            yield return WaitUntil(() => s.CardDone, 2f, "letting go of the keycard over the reader swipes it (VR)");
            Check(s.TasksDone && s.ScreenText.Contains("IMPOSTOR SPOTTED"), "VR: both tasks done, the security screen shows the clue");
            var screenEye = L(new Vector3(2.2f, 1.6f, 2.3f));
            VRLook(screenEye, s.ScreenPosition);
            yield return Shot("vrb04_ny_security_screen");

            // A wrong vote, then the right one, with the trigger.
            int innocent = (s.Impostor + 2) % ImpostorGame.CrewCount;
            var innFeet = L(new Vector3(ImpostorGame.CrewX[innocent], 0f, ImpostorGame.VoteZ - 0.6f));
            Game.Rig.PlaceAt(innFeet, yaw);
            yield return new WaitForSeconds(0.3f);
            VRLook(innFeet + Vector3.up * 1.6f, s.Crew[innocent].transform.position + Vector3.up * 0.6f);
            yield return Press(vr, side, s.Votes[innocent]);
            yield return ArcBWrongVote(ny, "VR");
            int imp = s.Impostor;
            var impFeet = L(new Vector3(ImpostorGame.CrewX[imp], 0f, ImpostorGame.VoteZ - 0.6f));
            Game.Rig.PlaceAt(impFeet, yaw);
            yield return new WaitForSeconds(0.3f);
            VRLook(impFeet + Vector3.up * 1.6f, s.Crew[imp].transform.position + Vector3.up * 0.8f);
            yield return Press(vr, side, s.Votes[imp]);
            yield return ArcBRightVote(ny, null, "VR");
        }

        /// <summary>The figure hunts the hall; every crewmate has one oddity and the clue's belongs to the impostor alone.</summary>
        IEnumerator ArcBCrewChecks(NewYorkCell ny)
        {
            var s = ny.Station;
            Guide.I?.DismissBriefing();
            Check(s != null && s.Crew.Length == ImpostorGame.CrewCount && !s.TasksDone && !s.Ejected, "New York: three crewmates on their pads, the tasks not done");
            if (s == null) yield break;
            ny.Hunt.Begin(0.3f);
            yield return WaitUntil(() => Game.Passenger != null && Game.Passenger.Visible, 4f, "the figure hunts the station hall");
            Check(Game.Passenger.Visible && !Game.Passenger.AllowChase, "it stalks (rushes only now and then) while the game is played");
            ny.Hunt.Stop();
            Game.Passenger.Despawn();
            int match = 0, mask = 0;
            for (int i = 0; i < ImpostorGame.CrewCount; i++)
            {
                if (s.Crew[i].Oddity == s.Clue) match++;
                mask |= 1 << (int)s.Crew[i].Oddity;
            }
            Check(match == 1 && s.Crew[s.Impostor].Oddity == s.Clue && mask == 7,
                "each crewmate has its own oddity and only the impostor (" + ImpostorGame.Names[s.Impostor] + ") has the clue's: " + s.Clue);
            Check(s.Crew[s.Impostor].Backpack.activeSelf == (s.Clue != ImpostorGame.Oddity.NoBackpack) &&
                  s.Crew[s.Impostor].CrackedVisor.activeSelf == (s.Clue == ImpostorGame.Oddity.CrackedVisor) &&
                  s.Crew[s.Impostor].Slime.activeSelf == (s.Clue == ImpostorGame.Oddity.VentSlime), "the impostor's model shows the clue");
        }

        IEnumerator ArcBWrongVote(NewYorkCell ny, string mode)
        {
            var s = ny.Station;
            yield return WaitUntil(() => s.WrongVotes == 1, 3f, "a wrong vote is counted (" + mode + ")");
            Check(!s.Ejected && !Game.Campaign.IsVerified(CountryId.USA), "the innocent crewmate stays and the seal is not verified (" + mode + ")");
            yield return WaitUntil(() => Game.Passenger != null && Game.Passenger.Visible && Game.Passenger.Chasing, 4f, "a wrong vote sets the figure rushing at the player (" + mode + ")");
            yield return Shot(mode == "VR" ? "vrb05_ny_wrong_vote" : "ab04_ny_wrong_vote");
            ny.Hunt.Stop();
            Game.Passenger.Despawn();
            yield return WaitUntil(() => !s.Busy, 4f, "after a wrong vote the vote can be cast again (" + mode + ")");
        }

        IEnumerator ArcBRightVote(NewYorkCell ny, DesktopRig desk, string mode)
        {
            var s = ny.Station;
            yield return WaitUntil(() => s.Ejected, 2.5f, "voting the impostor ejects it (" + mode + ")");
            yield return new WaitForSeconds(0.9f);
            if (desk != null) yield return ArcBAimDesk(desk, s.transform.TransformPoint(new Vector3(2.2f, 0f, 2.3f)), s.Crew[s.Impostor].Visual.position);
            else VRLook(s.transform.TransformPoint(new Vector3(2.2f, 1.6f, 2.3f)), s.Crew[s.Impostor].Visual.position);
            yield return Shot(mode == "VR" ? "vrb06_ny_ejected" : "ab05_ny_ejected");
            yield return WaitUntil(() => Game.Campaign.IsVerified(CountryId.USA), 8f, "the ejection verifies the USA (" + mode + ")");
            Check(Game.Campaign.HasResource(ResourceId.Noisemaker), "the USA seal grants the noisemaker (" + mode + ")");
            Check(s.Crew[s.Impostor].Gone && !s.Crew[s.Impostor].Visual.gameObject.activeSelf, "the impostor is gone from its pad (" + mode + ")");
        }

        // ------------------------------------------------------------------ Rio, desktop
        IEnumerator ArcBRioDesk(DesktopRig desk)
        {
            yield return Enter(CellId.Rio);
            var rio = (RioCell)Game.Cells.Active;
            var toss = rio.Toss;
            var hand = desk.MainHand;
            yield return ArcBRioChecks(rio);
            var feet = toss.PlayerSpot;
            yield return ArcBAimDesk(desk, feet, toss.PegTop(1));
            Check(hand.Focused == toss.ThrowZone, "desktop aim over the counter focuses 'Throw a ring' (focused " + ArcBName(hand.Focused) + ")");
            yield return Shot("ab06_rio_stall");
            // A deliberate miss at the far end of the near rail.
            yield return ArcBThrowDesk(desk, toss, feet, 0, true);
            yield return ArcBWaitThrow(toss);
            Check(toss.Misses == 1 && toss.Score == 0, "a ring that lands away from the bottles misses (and vanishes)");
            int guard = 0;
            bool shot = false;
            while (toss.Score < RingToss.Target && guard++ < 16)
            {
                yield return ArcBThrowDesk(desk, toss, feet, guard % RingToss.Rows, false);
                if (!shot) { yield return new WaitForSeconds(0.25f); yield return Shot("ab07_rio_ring_in_flight"); shot = true; }
                yield return ArcBWaitThrow(toss);
            }
            Check(toss.Score >= RingToss.Target, "five ringers through the real aim and E (" + toss.Score + " ringers from " + toss.Thrown + " throws)");
            int onPegs = 0;
            for (int i = 0; i < RingToss.Rows; i++) onPegs += toss.RingsOn(i);
            Check(onPegs == toss.Score, "every ringer stays on its bottle (" + onPegs + ")");
            yield return WaitUntil(() => Game.Campaign.IsVerified(CountryId.Brazil), 6f, "five ringers verify Brazil (desktop)");
            yield return ArcBAimDesk(desk, feet, toss.TargetsPosition);
            yield return Shot("ab08_rio_won");
        }

        // ------------------------------------------------------------------ Rio, VR
        IEnumerator ArcBRioVR(VRRig vr)
        {
            var side = Game.Input.DominantHand == HandSide.Left ? HandSide.Left : HandSide.Right;
            var hand = vr.MainHand;
            yield return Enter(CellId.Rio);
            var rio = (RioCell)Game.Cells.Active;
            var toss = rio.Toss;
            yield return ArcBRioChecks(rio);
            Game.Rig.PlaceAt(toss.PlayerSpot, toss.transform.eulerAngles.y);
            yield return new WaitForSeconds(0.3f);
            VRLook(toss.PlayerSpot + Vector3.up * 1.6f, toss.TargetsPosition);
            int guard = 0, focused = 0, throws = 0;
            bool missDone = false, shot = false;
            while (toss.Score < RingToss.Target && guard++ < 18)
            {
                bool miss = !missDone;
                int row = guard % RingToss.Rows;
                // The hand out in front, a little low and to the side, the aim ray pointed through the stall's front.
                var head = Game.Rig.Head;
                var fwd = toss.transform.forward;
                var handAt = head.position + fwd * 0.4f - Vector3.up * 0.3f + toss.transform.right * 0.15f;
                yield return TouchWith(vr, side, handAt);
                var aim = vr.AimOf(side);
                aim.position = hand.HoldPoint.position;
                var land = ArcBLead(toss, row, hand.HoldPoint.position, miss);
                aim.rotation = Quaternion.LookRotation(land - aim.position);
                yield return null;
                yield return null;
                if (hand.Focused == toss.ThrowZone && !hand.FocusIsNear) focused++;
                // Lead again from now (two frames went by), then the trigger.
                land = ArcBLead(toss, row, hand.HoldPoint.position, miss);
                aim.rotation = Quaternion.LookRotation(land - aim.position);
                throws++;
                hand.PressUse();
                if (!shot) { yield return new WaitForSeconds(0.25f); yield return Shot("vrb07_rio_ring_from_hand"); shot = true; }
                yield return ArcBWaitThrow(toss);
                if (miss)
                {
                    missDone = true;
                    Check(toss.Misses == 1 && toss.Score == 0, "VR: a ring thrown away from the bottles misses");
                }
            }
            Check(focused == throws, "the VR aim ray over the counter focuses 'Throw a ring' for every throw (" + focused + "/" + throws + ")");
            Check(toss.Score >= RingToss.Target, "five ringers thrown from the VR hand with the trigger (" + toss.Score + " ringers from " + toss.Thrown + " throws)");
            yield return WaitUntil(() => Game.Campaign.IsVerified(CountryId.Brazil), 6f, "five ringers verify Brazil (VR)");
            VRLook(toss.PlayerSpot + Vector3.up * 1.6f, toss.TargetsPosition);
            yield return Shot("vrb08_rio_won");
        }

        IEnumerator ArcBRioChecks(RioCell rio)
        {
            var toss = rio.Toss;
            Guide.I?.DismissBriefing();
            Check(toss != null && toss.Score == 0 && toss.ThrowZone != null && toss.ThrowZone.Usable, "Rio: the ring toss stall is set up, no ringers yet");
            if (toss == null) yield break;
            rio.Hunt.Begin(0.3f);
            yield return WaitUntil(() => Game.Passenger != null && Game.Passenger.Visible, 4f, "the figure hunts the carnival avenue");
            Check(Game.Passenger.Visible && !Game.Passenger.AllowChase, "it stalks (rushes only now and then) while the stall is played");
            rio.Hunt.Stop();
            Game.Passenger.Despawn();
            var p0 = toss.PegTop(1);
            yield return new WaitForSeconds(0.4f);
            Check((toss.PegTop(1) - p0).magnitude > 0.05f, "the bottles slide on their rails");
        }

        // ------------------------------------------------------------------ countries flow blocks
        /// <summary>The countries flow's New York block: a locked vote, the two tasks, a wrong vote, the impostor voted out.</summary>
        IEnumerator CountriesUsa()
        {
            var hand = Game.Rig.MainHand;
            yield return Enter(CellId.NewYork);
            var ny = (NewYorkCell)Game.Cells.Active;
            var s = ny.Station;
            yield return Shot("30_newyork");
            ny.Hunt.Begin(0.2f);
            yield return WaitUntil(() => Game.Passenger.Visible, 4f, "the figure comes into the station hall");
            yield return PassengerCloseup("30b_newyork_passenger_close", 2.6f);
            ny.Hunt.Stop();
            Game.Passenger.Despawn();
            Game.Rig.PlaceAt(ny.Arrival.position, ny.Arrival.eulerAngles.y);
            yield return null;
            s.Votes[0].Use(hand);
            Check(s.LockedPresses == 1 && !s.Ejected, "voting is locked until both station tasks are done");
            for (int k = 0; k < 3; k++) { s.WireButtons[s.WireOrder[k]].Use(hand); yield return null; }
            Check(s.WiringDone, "the wires pressed in the screen's order fix the wiring");
            hand.Grab(s.Keycard);
            hand.PlaceInto(s.Reader);
            yield return WaitUntil(() => s.CardDone, 2f, "the keycard in the reader is accepted");
            Check(s.TasksDone && s.ScreenText.Contains("IMPOSTOR SPOTTED"), "the security screen shows the clue");
            var stand = s.transform.TransformPoint(new Vector3(2.2f, 0f, 2.3f));
            var look = s.ScreenPosition - (stand + Vector3.up * 1.6f);
            Game.Rig.PlaceAt(stand, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg);
            if (Game.Rig is DesktopRig d) d.SetPitch(-Mathf.Atan2(look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg);
            yield return new WaitForSeconds(0.3f);
            yield return Shot("31_newyork_screen");
            s.Votes[(s.Impostor + 1) % ImpostorGame.CrewCount].Use(hand);
            yield return WaitUntil(() => s.WrongVotes == 1 && Game.Passenger.Visible && Game.Passenger.Chasing, 5f, "a wrong vote: the crewmate was innocent and the figure rushes");
            ny.Hunt.Stop();
            Game.Passenger.Despawn();
            yield return WaitUntil(() => !s.Busy, 4f, "the vote can be cast again");
            s.Votes[s.Impostor].Use(hand);
            yield return WaitUntil(() => Game.Campaign.IsVerified(CountryId.USA), 8f, "voting the impostor out ejects it and verifies the USA");
            Check(Game.Campaign.HasResource(ResourceId.Noisemaker), "noisemaker becomes a carried resource");
        }

        /// <summary>The countries flow's Brazil block: five ringers at the ring toss stall.</summary>
        IEnumerator CountriesBrazil()
        {
            yield return Enter(CellId.Rio);
            var rio = (RioCell)Game.Cells.Active;
            var toss = rio.Toss;
            // Game-logic check: the hunt is switched off so the throws can't be interrupted.
            rio.Hunt.Stop();
            Game.Passenger?.Despawn();
            Game.Rig.PlaceAt(toss.PlayerSpot, toss.transform.eulerAngles.y);
            yield return new WaitForSeconds(0.5f);
            yield return Shot("38_rio");
            int guard = 0;
            while (toss.Score < RingToss.Target && guard++ < 16)
            {
                var head = Game.Rig.Head.position;
                var start = head + toss.transform.forward * 0.35f - Vector3.up * 0.2f;
                var land = ArcBLead(toss, guard % RingToss.Rows, start, false);
                toss.ThrowAlong(head, land - head, start);
                yield return ArcBWaitThrow(toss);
            }
            yield return WaitUntil(() => Game.Campaign.IsVerified(CountryId.Brazil), 6f, "Brazil verified by five ringers at the ring toss");
        }

        // ------------------------------------------------------------------ helpers
        /// <summary>Stands the desktop player at <paramref name="feet"/> looking at <paramref name="at"/>.</summary>
        IEnumerator ArcBAimDesk(DesktopRig desk, Vector3 feet, Vector3 at)
        {
            var eye = feet + Vector3.up * DesktopRig.EyeHeight;
            var d = at - eye;
            Game.Rig.PlaceAt(feet, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
            desk.SetPitch(-Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg);
            yield return null; yield return null; yield return null;
        }

        /// <summary>Turns the desktop view to <paramref name="at"/> at once (no frame), from where the eyes are now.</summary>
        static void ArcBLookDesk(DesktopRig desk, Vector3 at)
        {
            var d = at - desk.Head.position;
            desk.SetLook(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg);
        }

        /// <summary>One throw with the desktop controls: the view aimed ahead of bottle <paramref name="row"/> (or well away
        /// from it for a deliberate miss), then E.</summary>
        IEnumerator ArcBThrowDesk(DesktopRig desk, RingToss toss, Vector3 feet, int row, bool miss)
        {
            var hand = desk.MainHand;
            yield return ArcBAimDesk(desk, feet, toss.PegTop(row));
            for (int i = 0; i < 2; i++) ArcBLookDesk(desk, ArcBLead(toss, row, RingToss.ThrowStart(hand), miss));
            if (hand.Focused == toss.ThrowZone) hand.PressUse();
            else Note("desktop throw: 'Throw a ring' not focused (focused " + ArcBName(hand.Focused) + ")");
        }

        /// <summary>Where to aim so a ring thrown from <paramref name="start"/> meets bottle <paramref name="row"/> as it
        /// falls (or, for a miss, the far end of that rail from where the bottle will be).</summary>
        static Vector3 ArcBLead(RingToss toss, int row, Vector3 start, bool miss)
        {
            float ahead = toss.FlightTime(start, toss.PegTop(row));
            for (int i = 0; i < 3; i++) ahead = toss.FlightTime(start, toss.PredictPegTop(row, ahead + 0.02f));
            var land = toss.PredictPegTop(row, ahead + 0.02f);
            if (!miss) return land;
            var local = toss.transform.InverseTransformPoint(land);
            local.x = local.x > 0f ? -1.25f : 1.25f;
            return toss.transform.TransformPoint(local);
        }

        IEnumerator ArcBWaitThrow(RingToss toss)
        {
            yield return null;
            for (float t = 0f; t < 3.5f && toss.InFlight > 0; t += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(0.35f);
        }

        static string ArcBName(Interactable it) => it != null ? it.name : "nothing";
    }
}

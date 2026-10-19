using System.Collections;
using System.Linq;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// --autotest=arcade_d: the UAE whack-a-ghost and the Berlin ghost shooting gallery, played through the real controls.
    /// Desktop: the view is turned onto each ghost / the target and E pressed (Interactor.PressUse). VR (--mode=vr
    /// --vr-preview): the scripted hand touches each ghost and pulls the trigger (every other one by the aim ray from a step
    /// back), takes the sun torch with the grip and fires along the controller's aim. Berlin then goes on to the VOID stamp.
    /// The "story" and "finale" tests call in here for their UAE and Berlin games. Not part of "all".
    /// </summary>
    public partial class AutoTest
    {
        IEnumerator ArcadeDFlow()
        {
            bool vr = Game.Rig is VRRig;
            Note("--- Arcade D: UAE whack-a-ghost, Berlin shooting gallery (" + (vr ? "VR hands" : "desktop aim and E") + ") ---");
            yield return ArcadeUae();
            yield return ArcadeBerlin();
        }

        string ArcTag => Game.Rig is VRRig ? "v" : "d";
        static string ArcName(Component c) => c != null ? c.name : "nothing";
        static float ArcYaw(Vector3 from, Vector3 to) { var d = to - from; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }
        static float ArcFlat(Vector3 a, Vector3 b) { var d = a - b; d.y = 0f; return d.magnitude; }
        HandSide ArcSide => Game.Input.DominantHand == HandSide.Left ? HandSide.Left : HandSide.Right;

        /// <summary>Desktop: stands at <paramref name="feet"/> and turns the view onto <paramref name="at"/>.</summary>
        IEnumerator DeskAim(Vector3 feet, Vector3 at)
        {
            var desk = Game.Rig as DesktopRig;
            var eye = feet + Vector3.up * DesktopRig.EyeHeight;
            var d = at - eye;
            Game.Rig.PlaceAt(feet, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
            if (desk != null) desk.SetPitch(-Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg);
            yield return null; yield return null; yield return null;
        }

        // ------------------------------------------------------------------ UAE: whack-a-ghost
        IEnumerator ArcadeUae()
        {
            yield return Enter(CellId.ApartmentCorrupt, c =>
            {
                c.verified.Remove(CountryDatabase.Get(CountryId.UAE).StableId); // play the game, not the shortcut
                c.flags.Remove(StoryFlags.TutorialDone);                        // and the way-out tutorial after it
            });
            var apt = (ApartmentCell)Game.Cells.Active;
            var enc = apt.Encounter as UaeEncounter;
            var g = apt.Ghosts;
            Check(g != null && g.Running && !Game.Campaign.IsVerified(CountryId.UAE), "whack-a-ghost is on in the Margin apartment");
            if (g == null || enc == null) yield break;
            Check(g.Spots.Count >= 6, "ghosts hide in " + g.Spots.Count + " places in the furniture (" + string.Join(", ", g.Spots.Select(s => s.Where)) + ")");
            Check(!apt.Trace.Revealed && (Tutorial.I == null || !Tutorial.I.Active), "no old genuine-note trace and no tutorial checklist during the game");
            Check(enc.Objective.Contains("0 / " + GhostWhack.HitsToWin), "the objective counts the ghosts stamped (0 / " + GhostWhack.HitsToWin + ")");
            float earlyUp = g.UpSecondsNow, earlyGap = g.IntervalNow;
            // One left alone sinks back into its hiding place, and does not count.
            yield return WaitUntil(() => g.FirstUp != null && g.FirstUp.State == GhostSpot.Phase.Up, 8f, "a ghost pops out of the furniture");
            var first = g.FirstUp;
            if (first != null)
            {
                yield return LookAtGhost(first);
                Check(Game.Rig.MainHand.Focused == first && first.GetPrompt(Game.Rig.MainHand) == "Stamp it!", "the ghost out of the " + first.Where.ToLowerInvariant() + " can be stamped (\"Stamp it!\")");
                yield return Shot("ad_" + ArcTag + "01_uae_ghost_up");
                yield return WaitUntil(() => first.State == GhostSpot.Phase.Down, 4f, "left alone, it sinks back into the " + first.Where.ToLowerInvariant());
                Check(g.Hits == 0 && g.Escaped >= 1, "one that gets away does not count (" + g.Escaped + " got away)");
            }
            yield return WhackGhosts(g, GhostWhack.HitsToWin, "ad_" + ArcTag + "02_uae_ghost_burst");
            Check(g.UpSecondsNow < earlyUp && g.IntervalNow < earlyGap, "they pop faster as the count rises (up " + earlyUp.ToString("0.0") + " -> " + g.UpSecondsNow.ToString("0.0") + " s, gap " + earlyGap.ToString("0.0") + " -> " + g.IntervalNow.ToString("0.0") + " s)");
            yield return WaitUntil(() => Game.Campaign.IsVerified(CountryId.UAE), 6f, "ten stamped ghosts verify the UAE seal");
            Check(!g.Running, "no more ghosts once the ten are stamped");
            yield return WaitUntil(() => Game.Passenger.Visible, 14f, "the escape threat steps out of the bathroom");
            yield return WaitUntil(() => enc.Escaping && Tutorial.I != null && Tutorial.I.Active, 8f, "the way-out tutorial starts");
            Check(Tutorial.I != null && Tutorial.I.Current == TutorialStep.OpenPassport, "its first step is opening the passport (then a tear, widen, step through)");
            Check(enc.Objective.Contains("doorway"), "the objective now says: open a tear, widen it to a doorway, step through");
            yield return new WaitForSeconds(0.5f);
            yield return Shot("ad_" + ArcTag + "03_uae_escape");
        }

        /// <summary>The "story" test's UAE: whack-a-ghost with the real controls until the homecoming seal is verified.</summary>
        IEnumerator StoryWhack(ApartmentCell apt)
        {
            var g = apt.Ghosts;
            Check(g != null && g.Running, "ghosts pop out of the furniture in the Margin copy (whack-a-ghost)");
            if (g == null) yield break;
            yield return WhackGhosts(g, GhostWhack.HitsToWin, "26_whack_a_ghost");
            yield return WaitUntil(() => Game.Campaign.IsVerified(CountryId.UAE), 6f, "homecoming seal verified by ten stamped ghosts");
        }

        IEnumerator LookAtGhost(GhostSpot s)
        {
            if (Game.Rig is VRRig vr)
            {
                Game.Rig.PlaceAt(s.StandWorld, ArcYaw(s.StandWorld, s.AimPoint));
                yield return TouchWith(vr, ArcSide, s.AimPoint);
                VRLook(s.StandWorld + Vector3.up * 1.6f, s.AimPoint);
            }
            else yield return DeskAim(s.StandWorld, s.AimPoint);
        }

        /// <summary>Stamps ghosts as they come up, from each one's stand spot: desktop aim and E, or the VR hand (touch, or the
        /// aim ray from a step back) and the trigger.</summary>
        IEnumerator WhackGhosts(GhostWhack g, int target, string burstShot)
        {
            var vr = Game.Rig as VRRig;
            var side = ArcSide;
            var hand = Game.Rig.MainHand;
            int tries = 0, focused = 0, byRay = 0;
            bool shot = false;
            float t0 = Time.time;
            while (g.Hits < target && g.Running && Time.time - t0 < 150f)
            {
                var s = g.FirstUp;
                if (s == null || s.State != GhostSpot.Phase.Up) { yield return null; continue; }
                tries++;
                int before = g.Hits;
                bool ray = false;
                if (vr == null) yield return DeskAim(s.StandWorld, s.AimPoint);
                else
                {
                    Game.Rig.PlaceAt(s.StandWorld, ArcYaw(s.StandWorld, s.AimPoint));
                    yield return null;
                    ray = tries % 2 == 0;
                    if (ray)
                    {
                        // From a step back: the hand by the body, its aim ray on the ghost.
                        yield return TouchWith(vr, side, Game.Rig.Head.position + Game.Rig.Forward * 0.3f + Vector3.down * 0.4f);
                        var aim = vr.AimOf(side);
                        aim.position = hand.HoldPoint.position;
                        aim.rotation = Quaternion.LookRotation(s.AimPoint - aim.position);
                        yield return null; yield return null;
                    }
                    else yield return TouchWith(vr, side, s.AimPoint);
                }
                if (hand.Focused == s)
                {
                    focused++;
                    if (ray) byRay++;
                    hand.PressUse();
                }
                else Note((vr != null ? "VR hand (" + (ray ? "ray" : "touch") + ")" : "desktop aim") + " missed the " + s.Where + " ghost: focused " + ArcName(hand.Focused));
                yield return null;
                if (!shot && g.Hits == before + 1)
                {
                    shot = true;
                    yield return new WaitForSeconds(0.12f); // the sheet popping, the red stamp on it
                    if (vr != null) VRLook(s.StandWorld + Vector3.up * 1.6f, s.AimPoint);
                    yield return Shot(burstShot);
                }
                for (float w = 0f; w < 2.5f && s.Hittable; w += Time.deltaTime) yield return null;
            }
            Check(g.Hits >= target, (vr != null ? "the VR hand" : "desktop aim and E") + " stamps " + g.Hits + " ghosts (" + focused + " / " + tries + " focused" + (vr != null ? ", " + byRay + " by the aim ray" : "") + ")");
            Check(tries > 0 && focused == tries, "every ghost the player went for was in reach of the " + (vr != null ? "VR hand" : "desktop view") + " (" + focused + " / " + tries + ")");
        }

        // ------------------------------------------------------------------ Berlin: the ghost shooting gallery
        IEnumerator ArcadeBerlin()
        {
            yield return Enter(CellId.Berlin, c =>
            {
                foreach (var d in CountryDatabase.All) if (d.Id != CountryId.Germany) c.Verify(d.Id);
                c.SetFlag(StoryFlags.FirstTearDone);
                c.SetFlag(StoryFlags.CounterfeitRevealed);
                c.SetFlag(StoryFlags.OpticianNotesRead);
            });
            var b = (BerlinCell)Game.Cells.Active;
            var enc = (BerlinEncounter)b.Encounter;
            var gal = b.Gallery;
            Check(gal != null && !gal.TorchOut && !gal.Running, "no sun torch and no gallery before the seals are validated");
            if (gal == null) yield break;
            var vr = Game.Rig as VRRig;
            var hand = Game.Rig.MainHand;
            var stand = b.transform.TransformPoint(new Vector3(0f, 0f, 0.2f));
            if (vr == null)
            {
                yield return DeskAim(stand, b.Plinth.transform.position + Vector3.up * 0.25f);
                Check(hand.Focused == b.Plinth, "desktop aim focuses the registration plinth (focused " + ArcName(hand.Focused) + ")");
                hand.PressUse();
            }
            else
            {
                Game.Rig.PlaceAt(stand, 0f);
                yield return null;
                yield return Press(vr, ArcSide, b.Plinth);
            }
            yield return WaitUntil(() => Game.Campaign.AllElevenVerified, 5f, "eleven of eleven seals verified at the plinth");
            yield return PlayGallery(b, "ad_" + ArcTag + "1", true);
            yield return VoidWithControls(b);
        }

        /// <summary>
        /// The gallery with the real controls: take the sun torch, one shot wide (nothing happens but the beam), one appearance
        /// left to walk toward the player, then five hits until it kneels. Used by arcade_d and the "finale" test.
        /// </summary>
        IEnumerator PlayGallery(BerlinCell b, string shots, bool extraChecks)
        {
            var enc = (BerlinEncounter)b.Encounter;
            var gal = b.Gallery;
            var vr = Game.Rig as VRRig;
            var side = ArcSide;
            var hand = Game.Rig.MainHand;
            var stand = b.transform.TransformPoint(new Vector3(0f, 0f, 0.2f));
            yield return WaitUntil(() => gal.TorchOut, 12f, "a torch of Egypt's sunlight rises on the plinth");
            Check(Game.Passenger.Visible && !gal.Running, "it waits by the blank wall until the torch is taken");
            if (vr == null)
            {
                yield return DeskAim(stand, gal.Torch.transform.position);
                Check(hand.Focused == gal.Torch, "desktop aim focuses the sun torch (focused " + ArcName(hand.Focused) + ")");
                hand.PressUse();
            }
            else
            {
                Game.Rig.PlaceAt(stand, 0f);
                yield return null;
                yield return TouchWith(vr, side, gal.Torch.transform.position + Vector3.up * 0.03f);
                Check(hand.Focused == gal.Torch && hand.FocusIsNear, "the VR hand reaches the sun torch (focused " + ArcName(hand.Focused) + ")");
                hand.PressGrab();
            }
            yield return null;
            Check(hand.Held == gal.Torch, (vr != null ? "the VR grip" : "E") + " takes the sun torch");
            if (hand.Held != gal.Torch) hand.Grab(gal.Torch);
            if (vr != null) yield return TouchWith(vr, side, Game.Rig.Head.position + Game.Rig.Forward * 0.35f + Vector3.down * 0.35f);
            yield return WaitUntil(() => gal.Running, 3f, "taking the torch starts the shooting gallery");
            yield return WaitUntil(() => gal.TargetUp, 8f, "it flickers in across the hall with a glowing target on its chest");
            var pc = Game.Passenger;
            float apart = ArcFlat(pc.transform.position, Game.Rig.Feet);
            Check(pc.Threatening && apart >= 4.4f, "it appears well away from the player (" + apart.ToString("0.0") + " m) and is dangerous while it is out");

            IEnumerator FireAt(Vector3 at)
            {
                if (vr == null)
                {
                    yield return DeskAim(Game.Rig.Feet, at);
                    hand.PressUse();
                }
                else
                {
                    var aim = vr.AimOf(side);
                    aim.position = hand.HoldPoint.position;
                    aim.rotation = Quaternion.LookRotation(at - aim.position);
                    hand.PressUse();
                }
                yield return null;
            }

            void Frame()
            {
                if (vr == null || !gal.TargetUp) return;
                var head = Game.Rig.Head.position;
                VRLook(head, gal.TargetPoint);
            }

            if (extraChecks)
            {
                // A miss does nothing but draw the beam.
                yield return new WaitForSeconds(0.4f);
                int before = gal.Shots;
                var head = Game.Rig.Head.position;
                var wide = head + Quaternion.Euler(0f, 30f, 0f) * (gal.TargetPoint - head);
                yield return FireAt(wide);
                Check(gal.Shots == before + 1 && gal.Hits == 0 && gal.BeamShowing, "a shot wide of the target draws a beam of light and does nothing else");
                Frame();
                yield return Shot(shots + "1_gallery_miss_beam");
                yield return new WaitForSeconds(0.25f);
            }
            bool walked = !extraChecks;
            for (int i = 0; i < GhostGallery.HitsToWin; i++)
            {
                yield return WaitUntil(() => gal.TargetUp, 9f, "the target is up for hit " + (i + 1));
                if (!gal.TargetUp) break;
                if (!walked && i == 1)
                {
                    // Left alone, it holds still a moment and then comes for you, slowly (never a run).
                    walked = true;
                    float d0 = ArcFlat(pc.transform.position, Game.Rig.Feet);
                    yield return WaitUntil(() => gal.State == GhostGallery.Phase.Walking, 4f, "it holds still a moment, then walks toward the player");
                    yield return new WaitForSeconds(1.3f);
                    float d1 = ArcFlat(pc.transform.position, Game.Rig.Feet);
                    Check(d1 < d0 - 0.3f && !pc.Chasing && pc.State == PassengerState.Stalk, "between appearances it walks slowly toward the player (" + d0.ToString("0.0") + " -> " + d1.ToString("0.0") + " m, no run)");
                    if (!gal.TargetUp) { i--; continue; }
                }
                if (i > 0 || !extraChecks) yield return new WaitForSeconds(0.4f); // let it flicker in fully first
                int hits = gal.Hits;
                yield return FireAt(gal.TargetPoint);
                yield return WaitUntil(() => gal.Hits == hits + 1, 1f, "hit " + (hits + 1) + " / " + GhostGallery.HitsToWin + " (" + (vr != null ? "VR trigger" : "E") + ")");
                if (i == 0)
                {
                    Check(gal.BeamShowing, "the hit draws a beam from the torch to its chest");
                    Frame();
                    yield return Shot(shots + "2_gallery_hit_beam");
                }
                if (i == 2 && extraChecks)
                {
                    Check(enc.Objective.Contains("strength") || enc.Objective.Contains("Strength"), "the objective shows its strength as pips");
                }
            }
            yield return WaitUntil(() => enc.Exposed, 6f, "five hits: it staggers and kneels (the shooting gallery is won)");
            yield return new WaitForSeconds(0.6f);
            Check(!pc.Threatening && pc.Body.Pose == Pose.Crouch, "worn down, it kneels before the false doors and cannot catch you");
            Check(hand.Held != gal.Torch && !gal.TorchOut, "the spent torch leaves the hand (it is free for the VOID stamp)");
            Check(Game.Campaign.HasFlag(BerlinEncounter.WeakenedFlag) && gal.Shots >= gal.Hits, "the win is kept for a retry (" + gal.Shots + " shots, " + gal.Hits + " hits)");
            if (vr != null) VRLook(Game.Rig.Head.position, pc.ChestPosition);
            else yield return DeskAim(Game.Rig.Feet, pc.ChestPosition);
            yield return Shot(shots + "3_gallery_kneeling");
        }

        /// <summary>Takes the VOID stamp off the side table and uses it, with the real controls.</summary>
        IEnumerator VoidWithControls(BerlinCell b)
        {
            var vr = Game.Rig as VRRig;
            var side = ArcSide;
            var hand = Game.Rig.MainHand;
            var vs = b.VoidStamp;
            var near = new Vector3(vs.transform.position.x, b.transform.position.y, vs.transform.position.z + 1.1f);
            if (vr == null)
            {
                yield return DeskAim(near, vs.transform.position + Vector3.up * 0.06f);
                Check(hand.Focused == vs, "desktop aim focuses the VOID stamp (focused " + ArcName(hand.Focused) + ")");
                hand.PressUse();
                yield return null;
                Check(hand.Held == vs, "E takes the VOID stamp");
                hand.PressUse();
            }
            else
            {
                Game.Rig.PlaceAt(near, 180f);
                yield return null;
                yield return TouchWith(vr, side, vs.transform.position + Vector3.up * 0.05f);
                Check(hand.Focused == vs && hand.FocusIsNear, "the VR hand reaches the VOID stamp (focused " + ArcName(hand.Focused) + ")");
                hand.PressGrab();
                yield return null;
                Check(hand.Held == vs, "the VR grip takes the VOID stamp");
                hand.PressUse();
            }
            yield return WaitUntil(() => Game.Campaign.HasFlag(StoryFlags.ForgedCancelled), 3f, "VOID cancels the forged HOME seal");
            Check(b.HomeSeam.Revealed && b.DecoyDoors.All(d => !d.gameObject.activeSelf), "the real way home shows on the blank wall; the false doors are gone");
            yield return WaitUntil(() => Game.Passenger.Visible && Game.Passenger.Threatening && Game.Passenger.State == PassengerState.Stalk, 3f, "it rises from its knees and comes for you, slowly: the escape");
            Check(!Game.Passenger.AllowChase, "it walks, it does not run");
            yield return new WaitForSeconds(0.8f);
            yield return Shot("ad_" + ArcTag + "4_berlin_void");
            if (hand.Held == vs) hand.ForceRelease(true);
            Game.Passport?.Close();
        }
    }
}

using System.Collections;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// --autotest=arcade_a: Stop the Clock (London) and Memory Match (Morocco) played through the real controls. On the
    /// desktop rig the view's centre ray is aimed at each control and E pressed (Interactor.PressUse); with --mode=vr
    /// --vr-preview the VR hand's near-grab sphere goes on the control and the trigger / grip is pulled. Covers a missed
    /// press on the clock (the count goes back to nought, the figure rushes), a wrong pair of cards, the drawer, the
    /// calibration insert, the optician's notes and both seals. The London and Marrakech blocks of CountriesFlow and the
    /// Morocco block of LifecycleFlow live here too.
    /// </summary>
    public partial class AutoTest
    {
        IEnumerator ArcadeAFlow()
        {
            var vr = Game.Rig as VRRig;
            Note("--- Arcade A: Stop the Clock and Memory Match (" + (vr != null ? "VR rig, scripted hand" : "desktop aim and E") + ") ---");
            yield return ArcadeLondon(vr);
            yield return ArcadeMorocco(vr);
        }

        Interactor ArcadeHand(VRRig vr) => vr != null ? vr.MainHand : Game.Rig.MainHand;
        static HandSide ArcadeSide => Game.Input != null && Game.Input.DominantHand == HandSide.Left ? HandSide.Left : HandSide.Right;
        static string FocusName(Interactor hand) => hand != null && hand.Focused != null ? hand.Focused.name : "nothing";
        string Via(VRRig vr) => vr != null ? "VR hand" : "desktop aim";

        /// <summary>The VR hand on a point (near touch), or the desktop view's centre ray on it from <paramref name="feet"/>.</summary>
        IEnumerator ArcadePoint(VRRig vr, Vector3 feet, Vector3 at)
        {
            if (vr != null) { yield return TouchWith(vr, ArcadeSide, at); yield break; }
            var eye = feet + Vector3.up * DesktopRig.EyeHeight;
            var d = at - eye;
            Game.Rig.PlaceAt(feet, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
            if (Game.Rig is DesktopRig desk) desk.SetPitch(-Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg);
            yield return null; yield return null; yield return null;
        }

        // ---------------------------------------------------------------------------------------------- London
        IEnumerator ArcadeLondon(VRRig vr)
        {
            yield return Enter(CellId.London);
            var lon = (LondonCell)Game.Cells.Active;
            var clock = lon.Clock;
            var hand = ArcadeHand(vr);
            Check(clock != null && !clock.Won && clock.Hits == 0 && clock.Button != null && clock.Button.Usable, "London: Big Ben's clock face stands on the promenade, nothing hit yet, the STOP button live");
            if (clock == null) yield break;
            Check(lon.Trace != null && !lon.Trace.Revealed, "London: the old trace stays hidden (stopping the clock verifies the seal)");
            float a0 = clock.NeedleAngle;
            yield return new WaitForSeconds(0.3f);
            Check(Mathf.Abs(Mathf.DeltaAngle(a0, clock.NeedleAngle)) > 5f, "the needle spins round the dial");
            var arrive = lon.Arrival;
            var toClock = clock.FaceCenter - arrive.position; toClock.y = 0f;
            Check(Vector3.Angle(arrive.forward, toClock) < 20f && toClock.magnitude < 14f, "the clock stands straight ahead of the arrival, in view (" + toClock.magnitude.ToString("0.0") + " m)");
            Guide.I?.DismissBriefing();
            var back = clock.transform.TransformPoint(new Vector3(0.6f, 0f, -4.6f));
            Game.Rig.PlaceAt(back, clock.transform.eulerAngles.y);
            if (Game.Rig is DesktopRig deskView) deskView.SetPitch(-6f);
            if (vr != null) VRLook(back + Vector3.up * 1.6f, clock.FaceCenter + Vector3.down * 0.5f);
            yield return new WaitForSeconds(0.3f);
            yield return Shot(vr != null ? "a00v_london_overview" : "a00_london_overview");

            var feet = clock.transform.TransformPoint(new Vector3(0f, 0f, -1.55f));
            Game.Rig.PlaceAt(feet, clock.transform.eulerAngles.y);
            yield return new WaitForSeconds(0.2f);
            // The figure in the clock coat hunts the promenade while the clock is played.
            lon.Hunt.Begin(0.3f);
            yield return WaitUntil(() => Game.Passenger != null && Game.Passenger.Visible, 4f, "the figure in the clock coat comes while the clock is played");
            yield return new WaitForSeconds(0.3f);
            Check(Game.Passenger.State == PassengerState.Stalk && !Game.Passenger.AllowChase, "it stalks toward the clock (it rushes only now and then; state " + Game.Passenger.State + ")");
            var press = clock.ButtonPosition + clock.Button.transform.up * 0.03f;
            yield return ArcadePoint(vr, feet, press);
            Check(hand.Focused == clock.Button && (vr == null || hand.FocusIsNear), Via(vr) + " on the red STOP button focuses it (focused " + FocusName(hand) + ")");
            if (vr != null) VRLook(feet + Vector3.up * 1.6f, clock.FaceCenter + Vector3.down * 0.4f);
            yield return Shot(vr != null ? "a01v_london_clock" : "a01_london_clock");

            // A miss: the buzzer, the count stays at nought, and the figure rushes.
            float w0 = clock.ZoneWidth, s0 = clock.Speed;
            yield return WaitUntil(() => clock.NeedleFarFromZone, 6f, "the needle is well away from the green");
            int misses = clock.Misses;
            hand.PressUse();
            yield return null;
            Check(clock.Misses == misses + 1 && clock.Hits == 0, "a press off the green is a miss: 0 / 3");
            Check(Game.Passenger.Visible && Game.Passenger.Chasing, "the buzzer gives you away: the figure rushes at you");
            yield return Shot(vr != null ? "a02v_london_miss_rush" : "a02_london_miss_rush");
            lon.Hunt.Stop();
            Game.Passenger.Despawn();

            // A hit, then a miss: back to nought.
            yield return WaitUntil(() => clock.NeedleNearZoneCentre && !clock.Frozen, 8f, "the needle comes into the green");
            hand.PressUse();
            yield return null;
            Check(clock.Hits == 1 && clock.Frozen, "a press in the green is a hit (1 / 3): a lamp lights and the needle stops for a moment");
            yield return WaitUntil(() => !clock.Frozen, 2f, "the needle runs on after the hit");
            Check(clock.ZoneWidth < w0 && clock.Speed > s0, "after a hit the green shrinks (" + w0.ToString("0") + " -> " + clock.ZoneWidth.ToString("0") + " degrees) and the needle speeds up (" + s0.ToString("0") + " -> " + clock.Speed.ToString("0") + " deg/s)");
            yield return WaitUntil(() => clock.NeedleFarFromZone, 6f, "the needle is away from the green again");
            hand.PressUse();
            yield return null;
            Check(clock.Hits == 0 && Mathf.Approximately(clock.ZoneWidth, w0), "a miss after a hit puts the count back to 0 and the green back to full size");
            Check(lon.Hunt.Running, "a miss with the figure away calls it in");
            lon.Hunt.Stop();
            Game.Passenger.Despawn();

            // Three in a row. On the desktop the second is pressed with the view on the clock face itself (E anywhere on
            // the dial), so the needle stays in sight; the others on the red button.
            for (int i = 0; i < StopTheClock.HitsToWin; i++)
            {
                Interactable want = clock.Button;
                if (vr == null && i == 1)
                {
                    want = clock.Face;
                    yield return ArcadePoint(vr, feet, clock.FaceCenter + Vector3.down * 0.25f);
                    Check(hand.Focused == clock.Face, "desktop aim on the clock face focuses it, so E works while watching the needle (focused " + FocusName(hand) + ")");
                }
                else if (vr == null && i == 2) yield return ArcadePoint(vr, feet, press);
                yield return WaitUntil(() => clock.NeedleNearZoneCentre && !clock.Frozen, 8f, "needle in the green for hit " + (i + 1));
                if (hand.Focused != want) Note("hand lost its target (focused " + FocusName(hand) + ")");
                hand.PressUse();
                yield return null;
                Check(clock.Hits == i + 1, "hit " + (i + 1) + " / " + StopTheClock.HitsToWin + " (" + Via(vr) + " on the " + (want == clock.Face ? "clock face" : "STOP button") + ")");
                if (i == 1) yield return Shot(vr != null ? "a03v_london_two_hits" : "a03_london_two_hits");
            }
            Check(clock.Won && !clock.Button.Usable, "three hits in a row stop the clock");
            yield return WaitUntil(() => Game.Campaign.IsVerified(CountryId.UnitedKingdom), 6f, "stopping the clock verifies the United Kingdom");
            yield return WaitUntil(() => Game.Story.WaitingStamp != null, 8f, "the UK stamp is put out to run for");
            yield return new WaitForSeconds(1.5f);
            Check(Mathf.Abs(Mathf.DeltaAngle(clock.NeedleAngle, 0f)) < 1f, "the needle comes to rest at midnight");
            if (vr != null) VRLook(feet + Vector3.up * 1.6f, clock.FaceCenter);
            yield return Shot(vr != null ? "a04v_london_stopped" : "a04_london_stopped");
            // Back in London after the win: the clock stays stopped and nothing hunts it.
            lon.Encounter.OnEnter(false);
            Check(clock.Won && !clock.Button.Usable && !clock.Face.Usable && !lon.Hunt.Running, "back in London after the win: the clock stays stopped at midnight and the hunt stays off");
        }

        // ---------------------------------------------------------------------------------------------- Morocco
        IEnumerator ArcadeMorocco(VRRig vr)
        {
            yield return Enter(CellId.ShopCorrupt);
            var shop = (ShopCell)Game.Cells.Active;
            var enc = (MoroccoEncounter)shop.Encounter;
            var mm = enc.Memory;
            var hand = ArcadeHand(vr);
            Guide.I?.DismissBriefing();
            Check(mm != null && mm.Count == MemoryMatch.Pairs * 2 && mm.MatchedPairs == 0, "Marrakech: eight cards face down on the counter");
            if (mm == null) yield break;
            int down = 0;
            var perKind = new int[MemoryMatch.Pairs];
            for (int i = 0; i < mm.Count; i++)
            {
                if (!mm.IsFaceUp(i) && mm.CardAt(i).Usable) down++;
                perKind[mm.KindAt(i)]++;
            }
            Check(down == mm.Count, "all eight lie face down and can be flipped (" + down + ")");
            Check(System.Array.TrueForAll(perKind, n => n == 2), "four pairs: glasses, camera, passport, stamp");
            Check(Game.Passenger.Visible && Game.Passenger.Threatening && Game.Passenger.State == PassengerState.Observe, "the optician watches from the aisles");

            var feet = shop.transform.TransformPoint(new Vector3(0.5f, 0f, -0.95f));
            Game.Rig.PlaceAt(feet, shop.transform.eulerAngles.y);
            yield return new WaitForSeconds(0.3f);
            Vector3 CardPoint(int i) => mm.CardAt(i).transform.position + Vector3.up * 0.012f;
            int focused = 0;
            for (int i = 0; i < mm.Count; i++)
            {
                yield return ArcadePoint(vr, feet, CardPoint(i));
                if (hand.Focused == mm.CardAt(i)) focused++;
                else Note("card " + i + ": focused " + FocusName(hand));
            }
            Check(focused == mm.Count, Via(vr) + " on each card focuses that card (" + focused + "/" + mm.Count + ")");
            if (vr != null) VRLook(feet + Vector3.up * 1.6f, mm.BoardPosition);
            yield return Shot(vr != null ? "a05v_morocco_cards" : "a05_morocco_cards");

            // A wrong pair: both turn over, then back.
            int a = 0, b = 1;
            while (b < mm.Count && mm.KindAt(b) == mm.KindAt(a)) b++;
            yield return ArcadePoint(vr, feet, CardPoint(a));
            hand.PressUse();
            yield return WaitUntil(() => mm.IsFaceUp(a) && !mm.IsTurning(a), 2f, "the first card turns over (" + MemoryMatch.KindNames[mm.KindAt(a)] + ")");
            var st = Game.Passenger.State;
            Check(st == PassengerState.Stalk || st == PassengerState.Chase || st == PassengerState.Repelled || st == PassengerState.Retreat || st == PassengerState.Investigate, "turning the first card sets the optician stalking (" + st + ")");
            yield return ArcadePoint(vr, feet, CardPoint(b));
            hand.PressUse();
            yield return new WaitForSeconds(0.45f);
            Check(mm.IsFaceUp(a) && mm.IsFaceUp(b), "two different pictures face up (" + MemoryMatch.KindNames[mm.KindAt(a)] + ", " + MemoryMatch.KindNames[mm.KindAt(b)] + ")");
            if (vr != null) VRLook(feet + Vector3.up * 1.6f, mm.BoardPosition);
            yield return Shot(vr != null ? "a06v_morocco_wrong_pair" : "a06_morocco_wrong_pair");
            yield return WaitUntil(() => !mm.IsFaceUp(a) && !mm.IsFaceUp(b) && !mm.Resolving, 3f, "a wrong pair turns back face down after a moment");
            Check(mm.MatchedPairs == 0 && mm.Mismatches == 1, "no pair counted for a wrong guess");
            // Puzzle-logic from here: the drain is switched off so ~40 s of notes can't fail the run.
            Game.Passenger.Threatening = false;
            Note("(Marrakech: Passenger drain disabled after the threat checks)");

            // Every pair, through the controls.
            for (int k = 0; k < MemoryMatch.Pairs; k++)
            {
                int p = -1, q = -1;
                for (int i = 0; i < mm.Count; i++) if (mm.KindAt(i) == k) { if (p < 0) p = i; else q = i; }
                yield return ArcadePoint(vr, feet, CardPoint(p));
                hand.PressUse();
                yield return new WaitForSeconds(0.45f);
                yield return ArcadePoint(vr, feet, CardPoint(q));
                hand.PressUse();
                yield return WaitUntil(() => mm.IsMatched(p) && mm.IsMatched(q) && !mm.Resolving, 3f, "pair of " + MemoryMatch.KindNames[k] + " found: both stay face up and glow");
            }
            Check(mm.Solved && mm.MatchedPairs == MemoryMatch.Pairs, "all four pairs matched");
            if (vr != null) VRLook(feet + Vector3.up * 1.6f, mm.BoardPosition);
            yield return Shot(vr != null ? "a07v_morocco_matched" : "a07_morocco_matched");
            yield return WaitUntil(() => enc.DrawerOpen && enc.Insert != null && enc.CaseSocket != null, 4f, "all four pairs: the drawer slides open with the calibration insert, and the glasses case is set out");
            if (enc.Insert == null || enc.CaseSocket == null) yield break;

            // The insert into the glasses case.
            yield return ArcadePoint(vr, feet, enc.Insert.transform.position + Vector3.up * 0.01f);
            Check(hand.Focused == enc.Insert, Via(vr) + " on the calibration insert focuses it (focused " + FocusName(hand) + ")");
            if (vr != null) hand.PressGrab(); else hand.PressUse();
            yield return null;
            Check(hand.Held == enc.Insert, (vr != null ? "grip" : "E") + " takes the calibration insert");
            if (hand.Held != enc.Insert) hand.Grab(enc.Insert);
            yield return ArcadePoint(vr, feet, enc.CaseSocket.transform.position);
            if (vr != null) hand.ReleaseGrab();
            else
            {
                Check(hand.Focused == enc.CaseSocket, "desktop aim on the glasses case focuses its socket (focused " + FocusName(hand) + ")");
                hand.PressUse();
            }
            yield return null;
            if (enc.CaseSocket.Current != enc.Insert && hand.Held == enc.Insert) hand.PlaceInto(enc.CaseSocket);
            yield return WaitUntil(() => Game.Campaign.HasFlag(StoryFlags.OpticianNotesRead), 3f, "the insert fits the case (" + (vr != null ? "let go over it" : "E") + "); hidden notes appear");
            yield return WaitUntil(() => shop.Trace.Revealed, 90f, "the optician's notes read in full; the trace appears on the counter");

            // The trace, then the seal and the insert.
            var tfeet = shop.transform.TransformPoint(new Vector3(-0.15f, 0f, -0.95f));
            Game.Rig.PlaceAt(tfeet, shop.transform.eulerAngles.y);
            yield return null;
            yield return ArcadePoint(vr, tfeet, shop.Trace.transform.position + new Vector3(0.04f, 0.01f, 0.02f));
            Check(hand.Focused == shop.Trace, Via(vr) + " on the trace focuses it (focused " + FocusName(hand) + ")");
            if (hand.Focused == shop.Trace) hand.PressUse(); else shop.Trace.Use(hand);
            yield return WaitUntil(() => Game.Campaign.IsVerified(CountryId.Morocco), 6f, "pressing the passport to the trace verifies Morocco");
            Check(Game.Campaign.HasResource(ResourceId.CalibrationInsert), "the calibration insert is recovered (needed in Berlin)");
            if (vr != null) VRLook(tfeet + Vector3.up * 1.6f, mm.BoardPosition);
            yield return Shot(vr != null ? "a08v_morocco_verified" : "a08_morocco_verified");
        }

        // ---------------------------------------------------------------------------------------------- shared flows
        /// <summary>CountriesFlow's London block: three hits in the green stop the clock and verify the seal.</summary>
        IEnumerator CountriesLondon(Interactor hand)
        {
            yield return Enter(CellId.London);
            var lon = (LondonCell)Game.Cells.Active;
            var clock = lon.Clock;
            Check(clock != null && !clock.Won && lon.Hunt.Running, "London: the clock face spins on the promenade and the figure in the clock coat is on its way");
            if (clock == null) yield break;
            lon.Hunt.Stop();
            Game.Passenger.Despawn();
            Game.Rig.PlaceAt(clock.transform.TransformPoint(new Vector3(0f, 0f, -1.55f)), clock.transform.eulerAngles.y);
            yield return new WaitForSeconds(0.3f);
            yield return Shot("39_london");
            for (int i = 0; i < StopTheClock.HitsToWin; i++)
            {
                yield return WaitUntil(() => clock.NeedleNearZoneCentre && !clock.Frozen, 8f, "needle in the green zone (" + (i + 1) + ")");
                clock.Button.Use(hand);
                yield return null;
            }
            Check(clock.Won, "three hits in a row stop the clock");
            yield return WaitUntil(() => Game.Campaign.IsVerified(CountryId.UnitedKingdom), 6f, "stopping the clock verifies the United Kingdom");
            yield return new WaitForSeconds(1.2f);
            yield return Shot("40_london_clock_stopped");
        }

        /// <summary>CountriesFlow's Marrakech block: all four pairs, the insert into the case, the notes, the trace.</summary>
        IEnumerator CountriesMorocco(Interactor hand)
        {
            yield return Enter(CellId.ShopCorrupt);
            var shop = (ShopCell)Game.Cells.Active;
            var enc = (MoroccoEncounter)shop.Encounter;
            // Puzzle-logic check only: the observer's drain is switched off so ~40 s of notes can't fail the run.
            if (Game.Passenger != null) Game.Passenger.Threatening = false;
            Note("(Marrakech: Passenger drain disabled for this puzzle-logic check)");
            Game.Rig.PlaceAt(shop.CounterTop.position + new Vector3(0.5f, -1.04f, -1.0f), 0f);
            yield return new WaitForSeconds(0.5f);
            yield return Shot("41_marrakech_margin");
            var mm = enc.Memory;
            Check(mm != null && mm.MatchedPairs == 0, "Marrakech: the memory cards are dealt face down on the counter");
            if (mm == null) yield break;
            for (int k = 0; k < MemoryMatch.Pairs; k++)
            {
                int p = -1, q = -1;
                for (int i = 0; i < mm.Count; i++) if (mm.KindAt(i) == k) { if (p < 0) p = i; else q = i; }
                mm.CardAt(p).Use(hand);
                yield return new WaitForSeconds(0.4f);
                mm.CardAt(q).Use(hand);
                yield return WaitUntil(() => mm.IsMatched(p) && mm.IsMatched(q) && !mm.Resolving, 3f, "pair of " + MemoryMatch.KindNames[k] + " matched");
            }
            yield return WaitUntil(() => enc.Insert != null, 4f, "four pairs matched; the drawer gives up the calibration insert");
            Check(enc.CaseSocket != null, "glasses case set out on the counter");
            if (enc.Insert == null || enc.CaseSocket == null) yield break;
            hand.Grab(enc.Insert);
            hand.PlaceInto(enc.CaseSocket);
            yield return WaitUntil(() => Game.Campaign.HasFlag(StoryFlags.OpticianNotesRead), 3f, "insert fitted; hidden notes appear");
            yield return new WaitForSeconds(2f);
            yield return Shot("42_marrakech_notes");
            yield return WaitUntil(() => shop.Trace.Revealed, 90f, "optician's notes read in full; trace revealed");
            yield return UseTrace(shop.Trace, CountryId.Morocco);
            Check(Game.Campaign.HasResource(ResourceId.CalibrationInsert), "calibration insert recovered");
        }

        /// <summary>LifecycleFlow's Morocco block: the threat ON, the first card turned, and the lamplight keeps it back.</summary>
        IEnumerator LifecycleMorocco(Interactor hand)
        {
            Note("--- Morocco with the threat ON (the counter lamp keeps it back) ---");
            yield return Enter(CellId.ShopCorrupt);
            var shop = (ShopCell)Game.Cells.Active;
            var enc = (MoroccoEncounter)shop.Encounter;
            Game.Rig.PlaceAt(shop.CounterTop.position + new Vector3(0.5f, -1.04f, -1.0f), 0f);
            yield return new WaitForSeconds(0.6f);
            Check(Game.Passenger.Visible && Game.Passenger.Threatening, "Morocco: the observer is present and threatening");
            if (enc.Memory != null)
            {
                // A wrong pair as well, so it comes toward the counter.
                int b = 1;
                while (b < enc.Memory.Count && enc.Memory.KindAt(b) == enc.Memory.KindAt(0)) b++;
                enc.Memory.CardAt(0).Use(hand);
                yield return new WaitForSeconds(0.4f);
                enc.Memory.CardAt(b).Use(hand);
            }
            yield return null;
            var st = Game.Passenger.State;
            Check(st == PassengerState.Stalk || st == PassengerState.Chase || st == PassengerState.Repelled || st == PassengerState.Retreat || st == PassengerState.Investigate, "turning a card sets it stalking (" + st + ")");
            float survive = 0f;
            while (survive < 14f && !Game.Story.Failing) { survive += Time.deltaTime; yield return null; }
            Check(!Game.Story.Failing, "standing in the counter lamp's light, the player is not caught (14 s with the threat on)");
            Note("Morocco light check: ambient " + RenderSettings.ambientSkyColor + ", lamp " + (shop.Lamp != null ? shop.Lamp.intensity.ToString("0.00") : "none") +
                 ", fade " + Fader.I.Alpha.ToString("0.00") + ", brightness " + Game.Settings.brightness.ToString("0.00"));
            yield return Shot("L2_morocco_threat_on");
        }
    }
}

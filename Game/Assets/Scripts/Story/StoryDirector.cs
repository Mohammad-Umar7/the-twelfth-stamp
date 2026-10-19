using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Campaign flow: menu → shop prologue → homecoming → corrupted apartment → countries → Berlin finale → real home
    /// → bathroom/epilogue → results → credits → menu. Also the judges' demo. Owns vitality, checkpoints, failure
    /// and retry, early glasses removal, recall and travel between cells.
    /// </summary>
    public partial class StoryDirector : MonoBehaviour
    {
        public static StoryDirector I { get; private set; }

        public bool TearsAllowed = true;
        public bool ThreatAllowed = true;
        public bool ShowVitality = true;
        public float ContaminationScale = 1f;
        public bool InCampaign { get; private set; }
        public bool Busy { get; private set; }

        readonly List<Grabbable> m_Essentials = new List<Grabbable>();
        float m_DrainFeedback;
        bool m_Failing;

        public static StoryDirector Create(Transform parent)
        {
            var go = new GameObject("Story");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<StoryDirector>();
            return I;
        }

        void Update()
        {
            if (InCampaign && !Game.Paused && Game.Campaign != null) Game.Campaign.playTimeSeconds += Time.deltaTime;
            // Gentle recovery when nothing is draining (breathing room).
            if (InCampaign && !Game.Paused && m_DrainFeedback <= 0.01f && Game.Campaign != null && Game.Campaign.vitality < 1f && Game.Campaign.vitality > 0f)
                Game.Campaign.vitality = Mathf.Min(1f, Game.Campaign.vitality + Time.deltaTime * 0.012f);
            PostFx.I?.SetDrain(Game.Campaign != null ? Mathf.Max(1f - Game.Campaign.vitality, m_DrainFeedback * 0.5f) : 0f);
        }

        // ------------------------------------------------------------------ entry points
        public void StartNewGame()
        {
            Levels.Current = null;
            Game.IsDemo = false;
            SaveSystem.WritesSuspended = Game.TestWritesBlocked;
            Game.Campaign = CampaignState.NewGame();
            Game.SaveNow();
            StartCoroutine(RunCampaign());
        }

        public void ContinueGame()
        {
            var c = SaveSystem.LoadCampaign();
            if (c == null) { Hud.I?.ShowToast("No valid save found.", 2.5f); return; }
            Levels.Current = null;
            Game.IsDemo = false;
            Game.Campaign = c;
            StartCoroutine(RunCampaign());
        }

        /// <summary>Developer/test start in a given cell (no save writes).</summary>
        public void StartAt(CellId cell, System.Action<CampaignState> configure = null)
        {
            Levels.Current = null;
            Game.IsDemo = false;
            SaveSystem.WritesSuspended = true;
            var c = CampaignState.NewGame();
            foreach (var f in new[] { StoryFlags.PrologueDone, StoryFlags.ShopPurchased, StoryFlags.HomeArrived, StoryFlags.Unpacked, StoryFlags.GlassesWorn, StoryFlags.SeamRevealed, StoryFlags.TutorialDone }) c.SetFlag(f);
            c.Verify(CountryId.UAE);
            c.CheckpointCell = cell;
            configure?.Invoke(c);
            Game.Campaign = c;
            StartCoroutine(RunCampaign());
        }

        public void StartDemo()
        {
            Levels.Current = null;
            Game.IsDemo = true;
            SaveSystem.WritesSuspended = true; // the demo never touches the campaign save
            var c = CampaignState.NewGame();
            foreach (var f in new[] { StoryFlags.PrologueDone, StoryFlags.ShopPurchased, StoryFlags.HomeArrived, StoryFlags.Unpacked, StoryFlags.GlassesWorn, StoryFlags.SeamRevealed, StoryFlags.TutorialDone, StoryFlags.FirstTearDone }) c.SetFlag(f);
            c.Verify(CountryId.UAE);
            c.Verify(CountryId.Egypt); // gives Egyptian sunlight
            c.CheckpointCell = CellId.Paris;
            Game.Campaign = c;
            StartCoroutine(DemoRoutine());
        }

        /// <summary>Chapters that switch these off (prologue, homecoming, ending) must never leak into the next run.</summary>
        void ResetChapterGates()
        {
            TearsAllowed = true; ThreatAllowed = true; ShowVitality = true;
            if (Game.Passport != null) Game.Passport.Available = true; // the opening puts it away; never leave it locked
        }

        IEnumerator RunCampaign()
        {
            InCampaign = true;
            ResetChapterGates();
            MenuSystem.I?.HideAll();
            var c = Game.Campaign;
            // The story opens at home; the Marrakech shop is a flashback inside the homecoming (PrologueChapter).
            if (!c.HasFlag(StoryFlags.GlassesWorn) && CellManager.Factory.ContainsKey(CellId.ApartmentNormal))
            {
                yield return Homecoming.Run();
                if (!InCampaign) yield break;
            }
            c.SetFlag(StoryFlags.GlassesWorn);
            var cell = c.CheckpointCell;
            if (cell == CellId.ShopNormal || cell == CellId.ApartmentNormal || cell == CellId.Menu || !CellManager.Factory.ContainsKey(cell))
                cell = CellManager.Factory.ContainsKey(CellId.ApartmentCorrupt) ? CellId.ApartmentCorrupt : CellId.Egypt;
            if (!c.HasFlag(StoryFlags.SeamRevealed) && cell != CellId.ApartmentCorrupt)
            {
                c.SetFlag(StoryFlags.SeamRevealed);
                c.SetFlag(StoryFlags.TutorialDone);
            }
            // The homecoming hands over to the Margin copy in place; don't re-enter (no fade, no teleport).
            if (Game.Cells.Active != null && Game.Cells.Active.Id == cell && Game.Cells.Active.Encounter != null && Game.Cells.Active.Encounter.Entered)
                yield break;
            yield return EnterCell(cell, false);
        }

        public void ReturnToMenu()
        {
            StopAllCoroutines();
            InCampaign = false;
            Busy = false;
            m_Failing = false;
            Game.InputLocks = 0;
            Game.Paused = false;
            Game.Passenger?.Despawn();
            Game.Tears?.ForceReset();
            Narration.I?.StopAll();
            Hud.I?.HideTitleCard(); // a country's title card must not linger over the welcome screen
            Game.Passport?.Close();
            ClearStamp();
            Game.SaveNow();
            Game.IsDemo = false;
            Levels.Current = null;
            ResetChapterGates();
            SaveSystem.WritesSuspended = Game.TestWritesBlocked;
            StartCoroutine(MenuSystem.I.ShowMainMenuRoutine());
        }

        // ------------------------------------------------------------------ travel
        public void TravelThroughTear(CountryDef dest)
        {
            if (Busy || m_Failing) return; // no trip while a catch plays out: its Retry card must stay in front of the player
            if (dest == CountryDatabase.Home) { StartCoroutine(EndingChapter.Run()); return; }
            // Stepping through a doorway tear is a trip into another world, not a cut (playtest: "more dramatic"): a short
            // dimension shift with the doorway still open in front of the player until the swap.
            AudioDirector.I?.Play("tear_open_door", null, 0.6f);
            StartCoroutine(ShiftInto(dest.Cell, 1.8f));
        }

        public IEnumerator EnterCell(CellId id, bool viaTear, string titleOverride = null)
        {
            Busy = true;
            Game.InputLocks++;
            ClearStamp();
            if (viaTear) AudioDirector.I?.Play("tear_open_door", null, 0.6f);
            yield return Fader.I.FadeOut(viaTear ? 0.25f : 0.45f);
            Game.Passenger?.Despawn();
            Narration.I?.ClearTutorial();
            foreach (var h in Game.Rig.Hands) if (h.Held != null && !h.Held.Essential) h.ForceRelease(true);
            var cell = Game.Cells.Activate(id);
            if (cell == null) { Game.InputLocks--; Busy = false; yield return Fader.I.FadeIn(0.3f); yield break; }
            Game.Rig.PlaceAt(cell.Arrival.position, cell.Arrival.eulerAngles.y);
            var country = CountryDatabase.ByCell(id);
            bool first = country != null && !Game.Campaign.IsVisited(country.Id);
            if (country != null) Game.Campaign.MarkVisited(country.Id);
            Game.SaveCheckpoint(id);
            Game.Passport?.Refresh();
            UpdateObjective();
            yield return null;
            cell.RefreshProbe();
            yield return Fader.I.FadeIn(0.55f);
            if (country != null && first)
                Hud.I?.ShowTitleCard(country.DisplayName.ToUpperInvariant(), titleOverride ?? country.EncounterTitle, 4f);
            Game.InputLocks = Mathf.Max(0, Game.InputLocks - 1);
            Busy = false;
            if (cell.Encounter != null)
            {
                cell.Encounter.Entered = true;
                cell.Encounter.OnEnter(first);
            }
        }

        public void UpdateObjective()
        {
            var c = Game.Campaign;
            var cell = Game.Cells?.Active;
            var here = cell != null ? CountryDatabase.ByCell(cell.Id) : null;
            string text;
            if (here != null && !c.IsVerified(here.Id)) text = cell.Encounter != null ? cell.Encounter.Objective : here.Objective;
            else if (cell != null && cell.Id == CellId.VictoryGround && cell.Encounter != null) text = cell.Encounter.Objective;
            else
            {
                var next = c.NextRecommended();
                text = next == null ? "Find the real way home." : "Open your passport (" + Game.Input.Label(Intent.Passport) + "), choose " + next.DisplayName + ", and tear a way there.";
            }
            Hud.I?.SetObjective(text, false);
        }

        // ------------------------------------------------------------------ verification
        public void VerifyCountry(CountryId id, TraceMark trace)
        {
            StartCoroutine(VerifyRoutine(id));
        }

        IEnumerator VerifyRoutine(CountryId id)
        {
            var c = Game.Campaign;
            bool newly = c.Verify(id);
            RestoreVitality(0.3f);
            Narration.I?.ClearTutorial();
            yield return Game.Passport.PlayVerify(id);
            var def = CountryDatabase.Get(id);
            if (newly)
            {
                Hud.I?.ShowToast("Seal verified: " + def.DisplayName + "   (" + c.TargetVerifiedCount + " / " + c.TargetCount + ")", 3.5f);
                Game.Cells.Active?.Encounter?.SendMessage("OnVerified", SendMessageOptions.DontRequireReceiver);
            }
            Game.SaveNow();
            if (!c.IsLevelRun && !c.HasFlag(StoryFlags.CounterfeitRevealed) && c.ShouldRevealCounterfeit)
                yield return CounterfeitReveal();
            UpdateObjective();
            if (Game.IsDemo && id == CountryId.France) yield return DemoEnd();
            else if (newly && InCampaign && OnLevelSealVerified()) { }
            else if (newly && InCampaign) OfferStamp(id);
        }

        // ------------------------------------------------------------------ stamps: verify, run, shift
        StampPortal m_Stamp;

        /// <summary>
        /// After a seal is verified the country's stamp appears away from the player, and a few seconds later the
        /// Passenger comes running: reach the stamp and the dimension shift carries you on to the next country. (Playtest:
        /// "while I am running I have to collect the stamps, and when I collect one I enter another country".) The
        /// passport and tears still work as before; the stamp is the fast way on. Not in the demo, the UAE tutorial or
        /// Berlin (the finale).
        /// </summary>
        void OfferStamp(CountryId id)
        {
            var c = Game.Campaign;
            var cell = Game.Cells.Active;
            if (cell == null || id == CountryId.UAE || id == CountryId.Germany) return;
            var next = c.NextRecommended();
            if (next == null || next.Id == CountryId.UAE) return;
            ClearStamp();
            var target = next.Cell;
            m_Stamp = StampPortal.Spawn(cell.transform, StampSpot(cell), id, s => { if (!Busy) StartCoroutine(StampShift(target)); });
            var here = CountryDatabase.Get(id);
            Hud.I?.ShowToast("Your " + here.DisplayName + " stamp is waiting. Reach it before it reaches you.", 4f);
            Hud.I?.SetObjective("Run to your " + here.DisplayName + " stamp to go on to " + next.DisplayName + " (or open your passport and tear a way).", false);
            if (ThreatAllowed && !Levels.CurrentIsHarmless) StartCoroutine(HuntAfter(cell, 3.5f));
        }

        /// <summary>
        /// Where the stamp waits: of the arrival point and the seams, the spot that is far enough from the player to make them
        /// run and as far as possible from the Passenger, so it chases them towards the stamp instead of standing on it.
        /// </summary>
        static Vector3 StampSpot(Cell cell)
        {
            // Close by (playtest, 18 Oct: the stamp out at the landmark was "running for no reason"): a few metres in front
            // of the player, in the open. A cell's own stamp spot is kept when it is already that close (the Saudi maze's
            // portal ring, a finish portal).
            bool spot = Landmarks.TryStampSpot(cell, out var landmark);
            var fromFeet = landmark - Game.Rig.Feet; fromFeet.y = 0f;
            if (spot && fromFeet.magnitude < 9f) return landmark;
            if (TryNearStampSpot(out var near)) return near;
            if (spot) return landmark;
            var feet = Game.Rig.Feet;
            var pc = Game.Passenger;
            bool hunter = pc != null && pc.Visible && pc.transform.parent == cell.transform;
            float Flat(Vector3 a, Vector3 b) { var d = a - b; d.y = 0f; return d.magnitude; }
            float Score(Vector3 p) => Mathf.Min(Flat(p, feet), 14f) + (hunter ? Mathf.Min(Flat(p, pc.transform.position), 20f) * 0.7f : 0f) - (Flat(p, feet) < 4f ? 30f : 0f);
            var best = cell.Arrival.position;
            float bestScore = Score(best);
            foreach (var s in cell.Seams)
            {
                if (s == null) continue;
                var p = s.FloorPoint + s.Forward * 1.2f;
                float sc = Score(p);
                if (sc > bestScore) { best = p; bestScore = sc; }
            }
            return best + Vector3.up * 1.5f;
        }

        /// <summary>
        /// A spot about 3.5 m from the player on open, level ground with nothing in the way, tried straight ahead first and
        /// then round to the sides and behind; the stamp hangs there at chest height.
        /// </summary>
        static bool TryNearStampSpot(out Vector3 spot)
        {
            spot = default;
            var rig = Game.Rig;
            if (rig == null) return false;
            var feet = rig.Feet;
            var fwd = rig.Forward; fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
            int mask = (1 << Layers.Default) | (1 << Layers.Interactable) | (1 << Layers.Floor);
            foreach (float dist in new[] { 3.5f, 3.2f })
                foreach (float ang in new[] { 0f, 30f, -30f, 60f, -60f, 100f, -100f, 140f, -140f, 180f })
                {
                    var dir = Quaternion.Euler(0f, ang, 0f) * fwd;
                    if (Physics.SphereCast(feet + Vector3.up * 1.1f, 0.3f, dir, out _, dist + 0.5f, mask, QueryTriggerInteraction.Ignore)) continue; // a wall or a prop in the way
                    var p = feet + dir * dist;
                    if (!Physics.Raycast(p + Vector3.up * 1.4f, Vector3.down, out var hit, 3.5f, mask, QueryTriggerInteraction.Ignore)) continue; // nothing to stand on
                    if (Mathf.Abs(hit.point.y - feet.y) > 0.5f) continue; // a drop or a step up
                    spot = hit.point + Vector3.up * 1.45f;
                    return true;
                }
            return false;
        }

        /// <summary>The Passenger comes for the player once the stamp is out: the one already here breaks into a run, or
        /// it returns from behind them.</summary>
        IEnumerator HuntAfter(Cell cell, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (Game.Cells.Active != cell || m_Stamp == null || m_Stamp.IsCollected || !ThreatAllowed || Busy || m_Failing) yield break;
            var pc = Game.Passenger;
            if (pc == null) yield break;
            if (!pc.Visible)
            {
                // It comes from the side away from the stamp, so the run is towards the stamp.
                var rig = Game.Rig;
                var away = rig.Feet - m_Stamp.transform.position; away.y = 0f;
                away = away.sqrMagnitude > 0.01f ? away.normalized : -rig.Forward;
                var from = cell.transform.InverseTransformPoint(rig.Feet + away * 13f);
                from.y = cell.transform.InverseTransformPoint(rig.Feet).y;
                pc.RetreatPoint = cell.transform.TransformPoint(from);
                pc.RetreatSeconds = 6f;
                pc.Spawn(cell, from, rig.Head.eulerAngles.y, PassengerState.Stalk);
                pc.Threatening = true;
                Hud.I?.ShowCaption("running footsteps, behind you", 3f);
            }
            pc.Threatening = true;
            pc.StartChase();
        }

        IEnumerator StampShift(CellId next) => ShiftInto(next, 3f);

        /// <summary>Into the next place through the dimension shift (a collected stamp, or stepping through a doorway tear).
        /// Nothing can catch the player during the build-up; the old place, its tear and its Passenger go at the swap.</summary>
        IEnumerator ShiftInto(CellId next, float buildUp)
        {
            if (m_Failing) yield break;
            Busy = true;
            if (Game.Passenger != null) Game.Passenger.Threatening = false;
            Game.Passport?.Close();
            Narration.I?.ClearTutorial();
            Cell cell = null;
            yield return DimensionShift.Run(() =>
            {
                Game.Passenger?.Despawn();
                Game.Tears?.ForceReset();
                foreach (var h in Game.Rig.Hands) if (h.Held != null && !h.Held.Essential) h.ForceRelease(true);
                ClearStamp();
                cell = Game.Cells.Activate(next);
                if (cell != null) Game.Rig.PlaceAt(cell.Arrival.position, cell.Arrival.eulerAngles.y);
            }, buildUp, next); // the cracks show the destination's light
            if (cell == null) { Busy = false; yield break; }
            var country = CountryDatabase.ByCell(next);
            bool first = country != null && !Game.Campaign.IsVisited(country.Id);
            if (country != null) Game.Campaign.MarkVisited(country.Id);
            Game.SaveCheckpoint(next);
            Game.Passport?.Refresh();
            UpdateObjective();
            cell.RefreshProbe();
            if (country != null && first) Hud.I?.ShowTitleCard(country.DisplayName.ToUpperInvariant(), country.EncounterTitle, 4f);
            Busy = false;
            if (cell.Encounter != null)
            {
                cell.Encounter.Entered = true;
                cell.Encounter.OnEnter(first);
            }
        }

        void ClearStamp()
        {
            if (m_Stamp != null) Destroy(m_Stamp.gameObject);
            m_Stamp = null;
        }

        /// <summary>The stamp waiting in the current country, if any (tests).</summary>
        public StampPortal WaitingStamp => m_Stamp;

        IEnumerator CounterfeitReveal()
        {
            AudioDirector.I?.SetMusic("mus_reveal", 0.5f, 0.9f, false);
            yield return Game.Passport.PlayCounterfeitReveal();
            Narration.I?.Say("disc_01", null, false, null, true);
            yield return new WaitForSeconds(3.5f);
            Game.SaveNow();
        }

        // ------------------------------------------------------------------ vitality / failure
        public void DrainVitality(float amount)
        {
            if (!InCampaign && !Game.IsDemo) return;
            if (Levels.CurrentIsHarmless) return;
            var c = Game.Campaign;
            if (c == null || m_Failing || Busy || amount <= 0f) return;
            c.vitality = Mathf.Max(0f, c.vitality - amount);
            if (c.vitality <= 0f) StartCoroutine(FailRoutine(false));
        }

        /// <summary>
        /// Sudden death: the Passenger reached the player (or its hand closed through a tear). No slow drain: the screen
        /// cuts to red and the Retry card comes up. Warnings come before this (its approach, the breath, the lunge).
        /// </summary>
        public void Kill(string cause)
        {
            if (!InCampaign && !Game.IsDemo) return;
            var c = Game.Campaign;
            if (c == null || m_Failing) return;
            // Nothing catches the player mid-travel (a cell swap or the dimension shift's build-up). The two used to race when
            // Paris's hand breached as the player stepped through a doorway tear: the shift carried them on while the Retry
            // card stayed behind, leaving a dead screen with no card (playtest, 18 Oct).
            if (Busy) { Debug.Log("[TS-Story] Catch ignored mid-travel (" + cause + ")"); return; }
            if (Levels.CurrentIsHarmless) { OnHarmlessCatch(); return; }
            // Three hearts (playtest, 18 Oct: hearts at the top right, and the enemy must be able to kill you): a catch costs
            // one and throws it off you; the catch on your last heart is the end. A moment's grace after a hit.
            if (Time.time - m_LastHurt < HurtGraceSeconds) return;
            c.vitality = Mathf.Max(0f, c.vitality - 1f / Hearts - 0.002f);
            if (c.vitality > 0.01f)
            {
                m_LastHurt = Time.time;
                Debug.Log("[TS-Story] Hit by the Passenger (" + cause + "), hearts " + HeartsLeft);
                StartCoroutine(HurtRoutine());
                return;
            }
            c.vitality = 0f;
            Debug.Log("[TS-Story] Caught by the Passenger (" + cause + ")");
            StartCoroutine(FailRoutine(true));
        }

        public const int Hearts = 3;
        const float HurtGraceSeconds = 2.5f;
        float m_LastHurt = -99f;

        /// <summary>Whole hearts left (a part-drained heart counts).</summary>
        public static int HeartsLeft => Game.Campaign == null ? Hearts : Mathf.Clamp(Mathf.CeilToInt(Game.Campaign.vitality * Hearts - 0.01f), 0, Hearts);

        /// <summary>A hit that is not the last heart: a red flash and a sting, and it recoils from the player for a moment.</summary>
        IEnumerator HurtRoutine()
        {
            var pc = Game.Passenger;
            if (pc != null && pc.Visible && Game.Rig != null)
            {
                var away = pc.transform.position - Game.Rig.Feet; away.y = 0f;
                away = away.sqrMagnitude > 0.01f ? away.normalized : -Game.Rig.Forward;
                pc.RetreatPoint = pc.transform.position + away * 9f;
                pc.RetreatSeconds = 5f;
                pc.SetState(PassengerState.Retreat);
            }
            if (Game.Rig != null) AudioDirector.I?.Play("pass_sting", Game.Rig.Head.position + Game.Rig.Head.forward * 0.6f, 1f, 1.1f, Bus.SFX, true);
            AudioDirector.Haptic(HandSide.Left, 0.9f, 0.25f);
            AudioDirector.Haptic(HandSide.Right, 0.9f, 0.25f);
            int left = HeartsLeft;
            Hud.I?.ShowToast(left == 1 ? "It got you! One heart left. Run!" : "It got you! " + left + " hearts left. Run!", 3f);
            Fader.I.SetColor(new Color(0.55f, 0f, 0.02f));
            Fader.I.FadeTo(0.55f, 0.06f);
            yield return new WaitForSeconds(0.12f);
            Fader.I.FadeTo(0f, 0.6f);
            yield return new WaitForSeconds(0.65f);
            if (!m_Failing) Fader.I.SetColor(Color.black);
        }

        public bool Failing => m_Failing;

        public void RestoreVitality(float amount)
        {
            if (Game.Campaign == null) return;
            Game.Campaign.vitality = Mathf.Min(1f, Game.Campaign.vitality + amount);
        }

        public void SetDrainFeedback(float f) => m_DrainFeedback = Mathf.Clamp01(f);

        IEnumerator FailRoutine(bool sudden)
        {
            m_Failing = true;
            var c = Game.Campaign;
            c.failures++;
            Game.InputLocks++;
            if (Game.Passenger != null) Game.Passenger.Threatening = false;
            if (sudden && Game.Rig != null) AudioDirector.I?.Play("pass_sting", Game.Rig.Head.position + Game.Rig.Head.forward * 0.6f, 1f, 0.9f, Bus.SFX, true);
            Narration.I?.Say("fail_01", null, false, null, true);
            Fader.I.SetColor(new Color(0.08f, 0.02f, 0.02f));
            yield return Fader.I.FadeOut(sudden ? 0.35f : 0.9f);
            Game.Tears?.ForceReset();
            Game.Passenger?.Despawn();
            Game.InputLocks = Mathf.Max(0, Game.InputLocks - 1);
            m_Failing = false;
            // Lift the fade so the Retry card (a 3D panel behind the fade overlay) is visible; the drained grade keeps
            // the scene dead-looking behind it.
            Fader.I.FadeTo(0.25f, 0.4f);
            MenuSystem.I?.ShowFailure();
        }

        /// <summary>Retry from the last checkpoint: verified seals and resources stay, the encounter resets.</summary>
        public void RetryCheckpoint()
        {
            var c = Game.Campaign;
            c.vitality = 1f;
            Fader.I.SetColor(Color.black);
            MenuSystem.I?.HideAll();
            Game.Cells.Active?.Encounter?.ResetLocal();
            StartCoroutine(EnterCell(c.CheckpointCell, false));
        }

        // ------------------------------------------------------------------ glasses (early removal)
        public bool CanRemoveGlassesEarly => InCampaign && Levels.Current == null && Game.Campaign != null && Game.Campaign.HasFlag(StoryFlags.GlassesWorn) && !Game.Campaign.HasFlag(StoryFlags.Escaped) && !Busy;

        public void RequestEarlyGlassesRemoval()
        {
            if (!CanRemoveGlassesEarly) return;
            StartCoroutine(RejectionRoutine());
        }

        IEnumerator RejectionRoutine()
        {
            var c = Game.Campaign;
            c.forcedReturns++;
            Game.InputLocks++;
            Narration.I?.Say("tut_return", null, true, null, true, true);
            AudioDirector.I?.Play("reflection_sting", null, 0.8f);
            PostFx.I?.Flash(0.8f);
            yield return Fader.I.FadeOut(0.8f);
            Game.Tears?.ForceReset();
            Game.Passenger?.Despawn();
            Game.Cells.Active?.Encounter?.ResetLocal();
            Game.InputLocks = Mathf.Max(0, Game.InputLocks - 1);
            var target = CellManager.Factory.ContainsKey(CellId.ApartmentCorrupt) ? CellId.ApartmentCorrupt : c.CheckpointCell;
            yield return EnterCell(target, false);
            Narration.I?.Say("reject_01", null, false, null, false);
        }

        // ------------------------------------------------------------------ essentials / recall
        public void RegisterEssential(Grabbable g)
        {
            if (g == null || m_Essentials.Contains(g)) return;
            m_Essentials.Add(g);
            // Recall brings back the essential item held most recently (not the one registered last, which late in the
            // story could be an item from another country), so every grab moves it to the end of the list.
            g.Grabbed += (item, who) => { if (m_Essentials.Remove(item)) m_Essentials.Add(item); };
        }

        public void RecallEssential(Interactor hand)
        {
            Grabbable best = null;
            for (int i = m_Essentials.Count - 1; i >= 0; i--)
            {
                var g = m_Essentials[i];
                if (g == null) { m_Essentials.RemoveAt(i); continue; }
                if (g.Holder != null) continue;
                if (!g.gameObject.activeInHierarchy && g.transform.root != Game.Root) continue;
                best = g;
                break;
            }
            if (best == null) { Hud.I?.ShowToast("Nothing to recall.", 1.5f); return; }
            best.gameObject.SetActive(true);
            hand.Grab(best);
            AudioDirector.I?.Play("pp_seam_shimmer", hand.HoldPoint.position, 0.5f);
            Hud.I?.ShowToast("Recalled: " + best.ItemId.Replace('_', ' '), 1.5f);
        }

        // ------------------------------------------------------------------ passport hooks
        public void OnPassportOpened()
        {
            Tutorial.I?.Notify(TutorialStep.OpenPassport);
        }

        public void OnDestinationSelected(CountryId id)
        {
            Tutorial.I?.Notify(TutorialStep.ChooseDestination);
        }

        // ------------------------------------------------------------------ judges' demo
        IEnumerator DemoRoutine()
        {
            InCampaign = true;
            ResetChapterGates();
            MenuSystem.I?.HideAll();
            DemoTimer.Begin();
            yield return EnterCell(CellId.Paris, false, "Judges' demo");
            Hud.I?.SetObjective("Open your passport (" + Game.Input.Label(Intent.Passport) + "), choose Egypt, and open a tear at the seam on the festival poster board beside the desk.", false);
            Hud.I?.ShowToast("Open your passport: " + Game.Input.Label(Intent.Passport), 4f);
        }

        IEnumerator DemoEnd()
        {
            yield return new WaitForSeconds(2.5f);
            float seconds = DemoTimer.Elapsed;
            // End card: fade to black, the logo and the tagline, then the results card back in the scene.
            Game.InputLocks++;
            yield return Fader.I.FadeOut(0.8f);
            Game.Passenger?.Despawn();
            if (Branding.I != null) yield return Branding.I.EndCard(Branding.Tagline, 3.4f);
            yield return Fader.I.FadeIn(0.6f);
            Game.InputLocks = Mathf.Max(0, Game.InputLocks - 1);
            MenuSystem.I?.ShowDemoResults(seconds);
        }
    }

    /// <summary>Simple real-time measurement of the demo run (no faked numbers).</summary>
    public static class DemoTimer
    {
        static float s_Start;
        public static void Begin() => s_Start = Time.realtimeSinceStartup;
        public static float Elapsed => Time.realtimeSinceStartup - s_Start;
    }
}

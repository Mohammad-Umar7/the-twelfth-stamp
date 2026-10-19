using System.Collections;
using System.Linq;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Level runs from the welcome screen (Levels.cs): Easy and Medium play a fixed set of countries and end at the
    /// Victory Ground; Free Roam opens every country with nothing hunting. Hard is the story itself (StartNewGame /
    /// ContinueGame). Level runs never write the story save.
    /// </summary>
    public partial class StoryDirector
    {
        float m_LastHarmlessCatch = -99f;

        public void StartLevel(LevelId id)
        {
            var def = Levels.Get(id);
            if (def.IsStory) { StartNewGame(); return; }
            StopAllCoroutines();
            Levels.Current = def;
            Game.IsDemo = false;
            SaveSystem.WritesSuspended = true; // a level run is self-contained: the story save is never touched
            var c = CampaignState.NewGame();
            bool story = !Levels.SkipStoryIntro && CellManager.Factory.ContainsKey(CellId.ApartmentNormal);
            if (!story)
                foreach (var f in new[] { StoryFlags.PrologueDone, StoryFlags.ShopPurchased, StoryFlags.HomeArrived, StoryFlags.Unpacked, StoryFlags.GlassesWorn, StoryFlags.SeamRevealed, StoryFlags.TutorialDone, StoryFlags.FirstTearDone })
                    c.SetFlag(f);
            c.levelCountries = def.Countries.Select(x => CountryDatabase.Get(x).StableId).ToList();
            var first = CountryDatabase.Get(def.Start ?? def.Countries[0]);
            if (!story) c.CheckpointCell = first.Cell;
            Game.Campaign = c;
            // With the story: the homecoming in Abu Dhabi and the glasses first (the same opening as the full story); the
            // apartment's tear can only open on this level's countries (CampaignState.IsDestinationAvailable).
            StartCoroutine(story ? RunCampaign() : LevelIntro(def, first));
        }

        IEnumerator LevelIntro(LevelDef def, CountryDef first)
        {
            InCampaign = true;
            ResetChapterGates();
            if (def.Id == LevelId.FreeRoam) ThreatAllowed = false;
            MenuSystem.I?.HideAll();
            Busy = true;
            Game.InputLocks++;
            AudioDirector.I?.Play("shift_whoosh", null, 0.7f);
            Hud.I?.ShowTitleCard(def.Label.ToUpperInvariant(), def.Blurb, 3.2f);
            yield return new WaitForSeconds(1.6f);
            Game.InputLocks = Mathf.Max(0, Game.InputLocks - 1);
            Busy = false;
            // Out of the welcome room through the same dimension shift the stamps use: the cracks show the first country's light.
            yield return ShiftInto(first.Cell, 2.2f);
            if (def.Harmless)
                Hud.I?.ShowToast(def.Id == LevelId.FreeRoam
                    ? "Free Roam: every country is open in your passport (" + Game.Input.Label(Intent.Passport) + "). Nothing can catch you."
                    : "Easy: the Passenger is here, but it cannot catch you.", 5f);
        }

        /// <summary>A seal was newly verified in a level run. True when the level is finished and its ending has started.</summary>
        bool OnLevelSealVerified()
        {
            var c = Game.Campaign;
            var def = Levels.Current;
            if (def == null || !c.LevelComplete) return false;
            if (def.EndsInVictoryGround)
            {
                StartCoroutine(LevelCompleteRoutine(def));
                return true;
            }
            Levels.MarkCompleted(def.Id);
            Hud.I?.ShowToast("Every seal verified. Free Roam complete: keep wandering as long as you like.", 5f);
            return false;
        }

        IEnumerator LevelCompleteRoutine(LevelDef def)
        {
            Levels.MarkCompleted(def.Id);
            ThreatAllowed = false;
            if (Game.Passenger != null) Game.Passenger.Threatening = false;
            yield return new WaitForSeconds(1.8f); // let the seal's ink settle first
            Hud.I?.ShowTitleCard("LEVEL COMPLETE", def.Label + "  ·  " + def.Countries.Length + " seals verified", 3f);
            AudioDirector.I?.Play("seal_verify", null, 0.9f, 1.25f);
            PostFx.I?.Flash(0.6f);
            yield return new WaitForSeconds(1.4f);
            yield return ShiftInto(CellId.VictoryGround, 2.6f);
        }

        /// <summary>Easy and Free Roam: the Passenger reached the player. It is thrown back instead of catching them.</summary>
        void OnHarmlessCatch()
        {
            var pc = Game.Passenger;
            if (pc != null) { pc.Threatening = false; pc.Despawn(); }
            if (Time.time - m_LastHarmlessCatch < 6f) return;
            m_LastHarmlessCatch = Time.time;
            PostFx.I?.Flash(0.5f);
            AudioDirector.I?.Play("pass_recoil", Game.Rig != null ? Game.Rig.Head.position + Game.Rig.Head.forward : (Vector3?)null, 0.7f);
            Hud.I?.ShowToast("It cannot hold you on this level. Keep going.", 3f);
        }

        /// <summary>The Victory Ground's card: what the run took, and where to go next.</summary>
        public void ShowLevelResults()
        {
            var def = Levels.Current;
            if (def == null) return;
            var c = Game.Campaign;
            int m = Mathf.FloorToInt(c.playTimeSeconds / 60f), s = Mathf.FloorToInt(c.playTimeSeconds % 60f);
            var vg = Game.Cells.Active != null ? Game.Cells.Active.Encounter as VictoryEncounter : null;
            string summary = "Seals verified  " + c.TargetVerifiedCount + " / " + c.TargetCount +
                             "\nTime  " + m + ":" + s.ToString("00") +
                             "\nCaught  " + c.failures +
                             (vg != null ? "\nMini puzzles  " + vg.Solved + " / " + VictoryEncounter.PuzzleCount : "");
            MenuSystem.I?.ShowLevelComplete(def, summary);
        }
    }
}

using System.Collections;
using UnityEngine;

namespace TwelfthStamp
{
    public partial class AutoTest
    {
        /// <summary>
        /// The level list (--autotest=levels): the four colour-coded tiles on the welcome card, Easy played through to the
        /// Victory Ground with its three mini puzzles and grand finale, the results card leading on to Medium, Free Roam
        /// opening every country, and Hard leading to the story's own New / Continue card.
        /// </summary>
        IEnumerator LevelsFlow()
        {
            Note("--- Levels: welcome tiles ---");
            Levels.SkipStoryIntro = true; // the story opening (homecoming, glasses) is covered by the story test; these check the runs
            if (Game.Cells.Active == null || Game.Cells.Active.Id != CellId.Menu || !Branding.I.WelcomeShowing)
            {
                Game.Story.ReturnToMenu();
                yield return WaitUntil(() => Game.Cells.Active != null && Game.Cells.Active.Id == CellId.Menu && Branding.I.WelcomeShowing, 12f, "start from the welcome screen");
            }
            yield return new WaitForSecondsRealtime(1f);
            foreach (var def in Levels.All)
            {
                var tile = Find<LevelTile>("Button_" + def.Label);
                Check(tile != null && tile.Interactive && tile.Def == def, "the welcome card shows the " + def.Label + " tile");
            }
            Check(Levels.Get(LevelId.Easy).Accent == Levels.Green && Levels.Get(LevelId.Hard).Accent == Levels.Red, "easy is green and hard is red");
            Check(Levels.Get(LevelId.Easy).Countries.Length == 3 && Levels.Get(LevelId.Medium).Countries.Length == 6 && Levels.Get(LevelId.Hard).Countries.Length == 11, "3 / 6 / 11 countries");
            yield return Shot("60_levels_menu");

            Note("--- Level 1: Easy ---");
            yield return Press(Levels.Get(LevelId.Easy).Label);
            Check(MenuSystem.I.CurrentPage == Levels.Get(LevelId.Easy).Label, "the Easy tile opens its level card");
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("61_level_easy_card");
            yield return Press("Play");
            yield return WaitUntil(() => Game.Cells.Active != null && Game.Cells.Active.Id == CellId.Japan && !Game.Story.Busy && Game.Cells.Active.Encounter.Entered, 25f, "Easy begins in Japan through the dimension shift");
            var c = Game.Campaign;
            Check(Levels.Current != null && Levels.Current.Id == LevelId.Easy && SaveSystem.WritesSuspended, "a level run never writes the story save");
            Check(c.IsDestinationAvailable(CountryId.India) && c.IsDestinationAvailable(CountryId.SaudiArabia) && !c.IsDestinationAvailable(CountryId.Egypt), "the passport offers only Easy's countries");
            Check(c.NextRecommended() != null && c.NextRecommended().Id == CountryId.Japan, "the objective follows the level's order");
            yield return new WaitForSeconds(1.5f);
            yield return Shot("62_easy_japan");
            Game.Story.Kill("autotest");
            yield return null;
            Check(!Game.Story.Failing && c.vitality > 0f && !MenuSystem.I.IsOpen && StoryDirector.HeartsLeft == StoryDirector.Hearts - 1, "on Easy a catch costs one heart, not the run");
            Game.Story.VerifyCountry(CountryId.Japan, null);
            yield return WaitUntil(() => c.IsVerified(CountryId.Japan), 6f, "Japan verified");
            Check(c.TargetCount == 3 && c.TargetVerifiedCount == 1, "progress counts 1 / 3");
            yield return new WaitForSeconds(3f);
            Game.Story.VerifyCountry(CountryId.India, null);
            yield return WaitUntil(() => c.IsVerified(CountryId.India), 6f, "India verified");
            yield return new WaitForSeconds(3f);
            Game.Story.VerifyCountry(CountryId.SaudiArabia, null);
            yield return WaitUntil(() => Game.Cells.Active != null && Game.Cells.Active.Id == CellId.VictoryGround && !Game.Story.Busy, 30f, "the third seal carries the player to the Victory Ground");
            Check(Levels.IsCompleted(LevelId.Easy), "Easy is marked complete");

            Note("--- Victory Ground ---");
            var vg = Game.Cells.Active.Encounter as VictoryEncounter;
            Check(vg != null && vg.Solved == 0 && !c.IsDestinationAvailable(CountryId.Egypt), "three unsolved mini puzzles wait");
            yield return new WaitForSeconds(2.2f);
            yield return Shot("63_victory_arrival");
            yield return new WaitForSeconds(2.5f);
            yield return Shot("64_victory_fireworks");
            if (vg == null) yield break;
            var hand = Game.Rig.MainHand;

            // Bell Echo: listen, then ring the sequence back (one wrong ring first).
            LookAt(vg.Bells[1].Hinge.position + Vector3.down * 1f, 3.6f);
            vg.ListenButton.Use(hand);
            yield return WaitUntil(() => vg.BellsListening, 8f, "the bells play their sequence, then wait for the player");
            yield return Shot("65_victory_bells");
            int wrong = (vg.BellSequence[0] + 1) % 4;
            vg.Bells[wrong].Use.Use(hand);
            Check(!vg.BellsListening && !vg.BellsSolved, "a wrong bell resets the echo");
            yield return WaitUntil(() => vg.BellsListening, 10f, "the bells replay after a wrong ring");
            foreach (var i in vg.BellSequence) { vg.Bells[i].Use.Use(hand); yield return new WaitForSeconds(0.25f); }
            Check(vg.BellsSolved && vg.Solved == 1, "Bell Echo solved");

            // Lantern Row: the first and fourth lanterns light all five.
            LookAt(vg.Lanterns[2].Root.position + Vector3.up * 1.6f, 3.6f);
            vg.Lanterns[0].Use.Use(hand);
            Check(vg.LanternOn(0) && vg.LanternOn(1) && !vg.LanternOn(2), "a lantern flips itself and its neighbour");
            vg.Lanterns[3].Use.Use(hand);
            yield return new WaitForSeconds(0.8f);
            Check(vg.LanternsSolved && vg.Solved == 2, "Lantern Row solved");
            yield return Shot("66_victory_lanterns");

            // Compass Dials: turn each until its arrow points up.
            LookAt(vg.Dials[1].Spinner.position, 3.4f);
            for (int i = 0; i < vg.Dials.Count; i++)
                for (int k = 0; k < 4 && vg.DialSteps(i) != 0; k++) { vg.Dials[i].Use.Use(hand); yield return new WaitForSeconds(0.15f); }
            Check(vg.DialsSolved && vg.Solved == 3, "Compass Dials solved");
            yield return new WaitForSeconds(1f);
            yield return Shot("67_victory_dials");
            Game.Rig.PlaceAt(Game.Cells.Active.Arrival.position, 0f);
            yield return new WaitForSeconds(4f);
            yield return Shot("68_victory_finale");
            yield return WaitUntil(() => vg.FinaleDone && MenuSystem.I.IsOpen && MenuSystem.I.CurrentPage == "Level complete", 25f, "the grand finale ends on the results card");
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("69_victory_results");

            Note("--- Level 2: Medium (from the results card) ---");
            yield return Press("Next: " + Levels.Get(LevelId.Medium).Label);
            yield return WaitUntil(() => Game.Cells.Active != null && Game.Cells.Active.Id == CellId.Egypt && !Game.Story.Busy, 25f, "Next starts Medium in Egypt");
            c = Game.Campaign;
            Check(Levels.Current.Id == LevelId.Medium && !Levels.CurrentIsHarmless && c.TargetCount == 6, "Medium: six countries and a real threat");
            Check(c.IsDestinationAvailable(CountryId.France) && c.IsDestinationAvailable(CountryId.Morocco) && !c.IsDestinationAvailable(CountryId.Japan), "the passport offers only Medium's countries");
            Check(Game.Passport.Available && Game.Story.TearsAllowed, "the passport and tears work again after the Victory Ground");
            yield return new WaitForSeconds(1.5f);
            yield return Shot("70_medium_egypt");
            Game.Story.ReturnToMenu();
            yield return WaitUntil(() => Game.Cells.Active != null && Game.Cells.Active.Id == CellId.Menu && Branding.I.WelcomeShowing, 12f, "back to the welcome screen");
            Check(Levels.Current == null, "the menu clears the level");
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("71_levels_menu_easy_done");

            Note("--- Free Roam ---");
            yield return Press(Levels.Get(LevelId.FreeRoam).Label);
            yield return Press("Play");
            yield return WaitUntil(() => Game.Cells.Active != null && Game.Cells.Active.Id == CellId.Japan && !Game.Story.Busy, 25f, "Free Roam begins in Japan");
            c = Game.Campaign;
            bool all = true;
            foreach (var d in CountryDatabase.All) all &= c.IsDestinationAvailable(d.Id);
            Check(all && Levels.CurrentIsHarmless && !Game.Story.ThreatAllowed, "Free Roam opens all eleven countries with no threat");
            Game.Story.ReturnToMenu();
            yield return WaitUntil(() => Game.Cells.Active != null && Game.Cells.Active.Id == CellId.Menu && Branding.I.WelcomeShowing, 12f, "back to the welcome screen");

            Note("--- Level 3: Hard is the story ---");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Press(Levels.Get(LevelId.Hard).Label);
            Check(Find<Button3D>("Button_Continue") != null && (Find<Button3D>("Button_New Story") != null || Find<Button3D>("Button_Story Mode") != null), "Hard offers the story's New / Continue");
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("72_level_hard_card");
            yield return Press("Back");
            Check(MenuSystem.I.CurrentPage == "", "Back returns to the welcome card");

            Note("--- Every level opens with the story ---");
            Levels.SkipStoryIntro = false;
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Press(Levels.Get(LevelId.Easy).Label);
            yield return Press("Play");
            yield return WaitUntil(() => Game.Cells.Active != null && (Game.Cells.Active.Id == CellId.ApartmentNormal || Game.Cells.Active.Id == CellId.ShopNormal), 25f, "Easy opens with the homecoming in Abu Dhabi");
            Check(Levels.Current != null && Levels.Current.Id == LevelId.Easy && Game.Campaign.IsLevelRun && !Game.Campaign.HasFlag(StoryFlags.GlassesWorn), "the level run keeps its countries while the story plays (glasses not yet on)");
            Check(Game.Campaign.IsDestinationAvailable(CountryId.Japan) && !Game.Campaign.IsDestinationAvailable(CountryId.Egypt), "the apartment's tear can only open on the level's countries");
            yield return new WaitForSeconds(1.5f);
            yield return Shot("73_level_easy_homecoming");
            Game.Story.ReturnToMenu();
            yield return WaitUntil(() => Game.Cells.Active != null && Game.Cells.Active.Id == CellId.Menu && Branding.I.WelcomeShowing, 12f, "back to the welcome screen");
        }

        void LookAt(Vector3 target, float distance)
        {
            var flat = target - Game.Cells.Active.transform.position; flat.y = 0f;
            var dir = flat.sqrMagnitude > 0.01f ? -flat.normalized : Vector3.back;
            var stand = target + dir * distance; stand.y = Game.Cells.Active.transform.position.y;
            var look = target - stand; look.y = 0f;
            Game.Rig.PlaceAt(stand, Quaternion.LookRotation(look).eulerAngles.y);
        }
    }
}

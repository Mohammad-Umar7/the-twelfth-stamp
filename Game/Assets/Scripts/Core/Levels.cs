using System.Linq;
using UnityEngine;

namespace TwelfthStamp
{
    public enum LevelId { Easy = 1, Medium = 2, Hard = 3, FreeRoam = 4 }

    /// <summary>One entry on the welcome screen's level list.</summary>
    public sealed class LevelDef
    {
        public LevelId Id;
        public string Number;            // "Level 1"
        public string Difficulty;        // "Easy"
        public string Blurb;             // one line under the button
        public Color Accent;             // green easy, amber medium, red hard, blue free roam
        public CountryId[] Countries;    // in play order; Hard and Free Roam use all eleven
        public CountryId? Start;         // where the run begins, if not the first country
        /// <summary>The Passenger still walks these places but can never catch the player, and nothing drains.</summary>
        public bool Harmless;
        /// <summary>Finishing the level carries the player to the Victory Ground.</summary>
        public bool EndsInVictoryGround;
        /// <summary>Hard is the full story (its own ending and save); the other levels are self-contained runs.</summary>
        public bool IsStory;

        public string Label => Number + "  ·  " + Difficulty;
        public string Hex => "#" + ColorUtility.ToHtmlStringRGB(Accent);
        public string StableId => Id.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// The four ways in from the welcome screen. Easy: three of the best-looking places with one-step puzzles and a
    /// threat that cannot hurt. Medium: six places, a borrowed resource and real chases. Hard: the whole story, all
    /// eleven countries. Free Roam: every country open, nothing hunting. Easy and Medium end at the Victory Ground.
    /// </summary>
    public static class Levels
    {
        public static readonly Color Green = new Color(0.22f, 0.62f, 0.32f);
        public static readonly Color Amber = new Color(0.86f, 0.56f, 0.12f);
        public static readonly Color Red = new Color(0.76f, 0.16f, 0.13f);
        public static readonly Color Blue = new Color(0.2f, 0.46f, 0.78f);

        static readonly CountryId[] AllCountries = CountryDatabase.InRecommendedOrder.Select(c => c.Id).ToArray();

        static readonly LevelDef[] s_All =
        {
            // Lantern street, the Agra garden and the Riyadh festival: the prettiest places, each a single light-or-optics
            // puzzle with no resource to fetch first.
            new LevelDef { Id = LevelId.Easy, Number = "Level 1", Difficulty = "Easy", Accent = Green, EndsInVictoryGround = true,
                Countries = new[] { CountryId.Japan, CountryId.India, CountryId.SaudiArabia },
                Blurb = "Japan, India and Saudi Arabia. Gentle games, three hearts: don't let it catch you." },
            // Egypt's sunlight is carried on to Paris; New York, Rio and London are chases timed on sound; Marrakech closes it.
            new LevelDef { Id = LevelId.Medium, Number = "Level 2", Difficulty = "Medium", Accent = Amber, EndsInVictoryGround = true,
                Countries = new[] { CountryId.Egypt, CountryId.France, CountryId.USA, CountryId.Brazil, CountryId.UnitedKingdom, CountryId.Morocco },
                Blurb = "Six countries, a borrowed sunbeam and the Passenger on your heels." },
            new LevelDef { Id = LevelId.Hard, Number = "Level 3", Difficulty = "Hard", Accent = Red, IsStory = true,
                Countries = AllCountries,
                Blurb = "The full story: all eleven countries and one forged stamp." },
            new LevelDef { Id = LevelId.FreeRoam, Number = "Free Roam", Difficulty = "Explore", Accent = Blue, Harmless = true, Start = CountryId.Japan,
                Countries = AllCountries,
                Blurb = "Every country open. Wander, collect postcards, no threat." },
        };

        public static LevelDef[] All => s_All;

        public static LevelDef Get(LevelId id) => s_All.First(l => l.Id == id);

        /// <summary>The level being played, or null in the story (Hard), the demo and the menu.</summary>
        public static LevelDef Current;

        /// <summary>
        /// Every level opens with the story (playtest, 18 Oct: "no matter which level I enter I want the story mode to be
        /// there"): home in Abu Dhabi, the glasses, the seam waking in the apartment; the apartment's first tear then leads
        /// into the level's own countries. The levels test switches it off to check the runs themselves.
        /// </summary>
        public static bool SkipStoryIntro;

        public static bool CurrentIsHarmless => Current != null && Current.Harmless;

        // Completion is remembered with the settings (levels never touch the story save).
        public static bool IsCompleted(LevelId id) =>
            Game.Settings != null && ("," + Game.Settings.completedLevels + ",").Contains("," + Get(id).StableId + ",");

        public static void MarkCompleted(LevelId id)
        {
            if (Game.Settings == null || IsCompleted(id)) return;
            var s = Game.Settings;
            s.completedLevels = string.IsNullOrEmpty(s.completedLevels) ? Get(id).StableId : s.completedLevels + "," + Get(id).StableId;
            if (!Game.IsAutoTest) SaveSystem.SaveSettings(s);
        }

        /// <summary>The level after this one on the list (Easy → Medium → Hard), or null.</summary>
        public static LevelDef Next(LevelId id) => id == LevelId.Easy ? Get(LevelId.Medium) : id == LevelId.Medium ? Get(LevelId.Hard) : null;
    }
}

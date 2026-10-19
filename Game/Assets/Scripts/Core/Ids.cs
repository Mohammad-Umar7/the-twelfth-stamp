namespace TwelfthStamp
{
    /// <summary>The eleven genuine destination seals. Values are stable and used in saves via <see cref="CountryDef.StableId"/>.</summary>
    public enum CountryId
    {
        UAE = 0,
        Egypt = 1,
        France = 2,
        USA = 3,
        Japan = 4,
        SaudiArabia = 5,
        India = 6,
        Brazil = 7,
        UnitedKingdom = 8,
        Morocco = 9,
        Germany = 10
    }

    public enum GameMode { Desktop, VR }

    /// <summary>Playable 3D spaces. Countries map onto cells through <see cref="CountryDef.Cell"/>.</summary>
    public enum CellId
    {
        None,
        Menu,
        ShopNormal,
        ApartmentNormal,
        ApartmentCorrupt,
        Egypt,
        Paris,
        NewYork,
        Japan,
        Riyadh,
        Agra,
        Rio,
        London,
        ShopCorrupt,
        Berlin,
        Epilogue,
        VictoryGround
    }

    public enum ResourceId { Sunlight, Noisemaker, CalibrationInsert, VoidStamp }

    public enum TearSize { Closed, Slit, Window, Doorway }

    public enum HandSide { Desktop, Left, Right }

    /// <summary>Narrative flags stored in the campaign save (kept separate from visited/verified/resources).</summary>
    public static class StoryFlags
    {
        public const string PrologueDone = "prologue_done";
        public const string ShopPurchased = "shop_purchased";
        public const string HomeArrived = "home_arrived";
        public const string Unpacked = "unpacked";
        public const string GlassesWorn = "glasses_worn";
        public const string SeamRevealed = "seam_revealed";
        public const string TutorialDone = "tutorial_done";
        public const string FirstTearDone = "first_tear_done";
        public const string FirstSealDone = "first_seal_done";
        public const string CounterfeitRevealed = "counterfeit_revealed";
        public const string OpticianNotesRead = "optician_notes_read";
        public const string ForgedCancelled = "forged_cancelled";
        public const string FinaleStarted = "finale_started";
        public const string Escaped = "escaped";
        public const string GlassesBroken = "glasses_broken";
        public const string CampaignComplete = "campaign_complete";
    }
}

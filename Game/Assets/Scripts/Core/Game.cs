using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>Static access to the running game's services (deliberately simple: one scene, one of each).</summary>
    public static class Game
    {
        public static GameMode Mode = GameMode.Desktop;
        public static GameSettings Settings = new GameSettings();
        public static CampaignState Campaign = CampaignState.NewGame();
        public static bool IsDemo;
        public static bool IsAutoTest;
        /// <summary>Automated runs never write the player's save; with --save-dir they write only into that sandbox.</summary>
        public static bool TestWritesBlocked => IsAutoTest && string.IsNullOrEmpty(AppArgs.SaveDir);

        public static Transform Root;
        public static PlayerRig Rig;
        public static GameInput Input;
        public static CellManager Cells;
        public static TearController Tears;
        public static PassportController Passport;
        public static PassengerController Passenger;
        public static StoryDirector Story;

        static bool s_Paused;

        /// <summary>Gameplay pause: freezes the threat, drain and physics. UI and music keep running.</summary>
        public static bool Paused
        {
            get => s_Paused;
            set
            {
                if (s_Paused == value) return;
                s_Paused = value;
                Time.timeScale = value ? 0f : 1f;
                AudioListener.pause = value;
            }
        }

        /// <summary>Locks player locomotion/interaction during short scripted beats (never head tracking).</summary>
        public static int InputLocks;
        public static bool InputLocked => InputLocks > 0 || s_Paused;

        /// <summary>True while a menu is shown (pointer mode for both rigs).</summary>
        public static bool InMenu;

        public static void SaveCheckpoint(CellId cell)
        {
            if (Campaign == null) return;
            Campaign.CheckpointCell = cell;
            if (!IsDemo) SaveSystem.SaveCampaign(Campaign);
        }

        public static void SaveNow()
        {
            if (Campaign != null && !IsDemo) SaveSystem.SaveCampaign(Campaign);
        }

        public static void ApplySettings()
        {
            Settings.Sanitized();
            SaveSystem.SaveSettings(Settings);
            PostFx.I?.ApplySettings();
            Rig?.ApplySettings();
            Hud.I?.ApplySettings();
            GraphicsQuality.Apply(Settings);
        }
    }
}

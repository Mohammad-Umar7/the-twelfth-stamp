using System;
using System.IO;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Versioned JSON saves in Application.persistentDataPath, written atomically (temp file + replace, with a
    /// backup). Missing or corrupt files are reported and ignored rather than crashing. Demo mode suspends writes.
    /// </summary>
    public static class SaveSystem
    {
        public const string CampaignFile = "campaign_v1.json";
        public const string SettingsFile = "settings_v1.json";

        static string s_DirOverride;

        /// <summary>When true (judges' demo, automated tests of the build) nothing is written to disk.</summary>
        public static bool WritesSuspended;

        public static string LastError { get; private set; }

        public static string Directory => s_DirOverride ?? Application.persistentDataPath;

        public static void OverrideDirectory(string dir) => s_DirOverride = dir;

        public static string PathOf(string file) => Path.Combine(Directory, file);

        // ------------------------------------------------------------ campaign
        /// <summary>A finished story is not offered as "Continue"; the menu starts a fresh one instead.</summary>
        public static bool HasValidCampaign()
        {
            var c = LoadCampaign();
            return c != null && !c.HasFlag(StoryFlags.CampaignComplete);
        }

        public static CampaignState LoadCampaign()
        {
            var s = TryLoad<CampaignState>(CampaignFile);
            if (s != null && s.Sanitize()) return s;
            if (s != null) LastError = "Campaign save has an unsupported version";
            // Fall back to the backup if the primary is unreadable.
            var b = TryLoad<CampaignState>(CampaignFile + ".bak");
            if (b != null && b.Sanitize()) return b;
            return null;
        }

        public static bool SaveCampaign(CampaignState state)
        {
            if (state == null) return false;
            state.version = CampaignState.CurrentVersion;
            return WriteAtomic(CampaignFile, JsonUtility.ToJson(state, true));
        }

        public static void DeleteCampaign()
        {
            if (WritesSuspended) return;
            TryDelete(PathOf(CampaignFile));
            TryDelete(PathOf(CampaignFile + ".bak"));
        }

        // ------------------------------------------------------------ settings
        public static GameSettings LoadSettings()
        {
            var s = TryLoad<GameSettings>(SettingsFile) ?? TryLoad<GameSettings>(SettingsFile + ".bak");
            return (s ?? new GameSettings()).Sanitized();
        }

        public static bool SaveSettings(GameSettings settings)
        {
            if (settings == null) return false;
            return WriteAtomic(SettingsFile, JsonUtility.ToJson(settings.Sanitized(), true));
        }

        // ------------------------------------------------------------ internals
        static T TryLoad<T>(string file) where T : class
        {
            var path = PathOf(file);
            try
            {
                if (!File.Exists(path)) return null;
                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) { LastError = file + " is empty"; return null; }
                var obj = JsonUtility.FromJson<T>(json);
                if (obj == null) LastError = file + " could not be parsed";
                return obj;
            }
            catch (Exception e)
            {
                LastError = file + ": " + e.Message;
                Debug.LogWarning("[TS-Save] Could not read " + path + ": " + e.Message);
                return null;
            }
        }

        static bool WriteAtomic(string file, string json)
        {
            if (WritesSuspended) return false;
            var path = PathOf(file);
            var tmp = path + ".tmp";
            var bak = path + ".bak";
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                File.WriteAllText(tmp, json);
                if (File.Exists(path))
                {
                    File.Copy(path, bak, true);
                    File.Replace(tmp, path, null);
                }
                else
                {
                    File.Move(tmp, path);
                }
                return true;
            }
            catch (Exception e)
            {
                LastError = file + ": " + e.Message;
                Debug.LogWarning("[TS-Save] Could not write " + path + ": " + e.Message);
                TryDelete(tmp);
                return false;
            }
        }

        static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* ignored */ }
        }
    }
}

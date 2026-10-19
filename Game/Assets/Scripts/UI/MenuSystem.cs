using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// All menus as one diegetic 3D panel (works with mouse and VR lasers): main menu, new-game confirmation,
    /// controls, settings, credits, pause, failure/retry, demo results. Every button does something real.
    /// </summary>
    public class MenuSystem : MonoBehaviour
    {
        public static MenuSystem I { get; private set; }

        Panel3D m_Panel;
        string m_Page;
        readonly Stack<System.Action> m_Back = new Stack<System.Action>();
        bool m_PauseOpen;
        LevelId? m_OpenLevelOnMenu; // "Next: Level 3" from the Victory Ground opens that level's card on the welcome screen
        float m_PresenceLostTime;

        public bool IsOpen => m_Panel != null && m_Panel.gameObject.activeSelf;

        public static MenuSystem Create(Transform parent)
        {
            var go = new GameObject("Menus");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<MenuSystem>();
            I.m_Panel = Panel3D.Create(go.transform, "Panel");
            I.m_Panel.gameObject.SetActive(false);
            return I;
        }

        // ------------------------------------------------------------------ placement
        bool m_AtDesk;

        void Show(bool atMenuDesk)
        {
            m_Panel.gameObject.SetActive(true);
            Game.InMenu = true;
            Game.Rig?.SetPointerMode(true);
            m_AtDesk = atMenuDesk;
            Place(atMenuDesk);
        }

        void Place(bool atMenuDesk)
        {
            m_Panel.transform.localScale = Vector3.one;
            var rig = Game.Rig;
            if (rig == null) return;
            Vector3 pos;
            if (Game.Mode == GameMode.Desktop)
            {
                // On a monitor the card sits square to the camera in the middle of the view, so nothing is skewed or
                // cut off. (In VR it stays at the desk where you can turn to it.)
                pos = rig.Head.position + rig.Head.forward * 1.05f;
                m_Panel.transform.position = pos;
                m_Panel.transform.rotation = Quaternion.LookRotation(rig.Head.forward, rig.Head.up);
                return;
            }
            if (atMenuDesk && Game.Cells.Active != null && Game.Cells.Active.Id == CellId.Menu)
            {
                var anchor = Game.Cells.Active.transform.Find("MenuAnchor");
                pos = anchor != null ? anchor.position : rig.Head.position + rig.Forward * 1.2f;
            }
            else pos = rig.Head.position + rig.Forward * 1.15f + Vector3.down * 0.05f;
            m_Panel.transform.position = pos;
            var look = pos - rig.Head.position; look.y *= 0.5f;
            m_Panel.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }

        public void HideAll()
        {
            Branding.I?.HideWelcome();
            m_Panel.gameObject.SetActive(false);
            UiPointer.I?.ClearHover();
            Game.InMenu = false;
            Game.Rig?.SetPointerMode(false);
            m_Back.Clear();
            if (m_PauseOpen) { m_PauseOpen = false; Game.Paused = false; }
        }

        void Page(string title, System.Action build, bool icon = true)
        {
            // Leaving the welcome screen: put the card back where every other page sits.
            if (Branding.I != null && Branding.I.WelcomeShowing)
            {
                Branding.I.HideWelcome();
                Place(m_AtDesk);
            }
            m_Page = title;
            m_Panel.Clear(title, icon);
            build();
            m_Panel.FinishLayout();
        }

        /// <summary>The page currently shown (AutoTest reads it).</summary>
        public string CurrentPage => m_Page;

        void Push(System.Action current, System.Action next)
        {
            m_Back.Push(current);
            next();
        }

        void Back()
        {
            AudioDirector.I?.PlayUI("ui_back", 0.7f);
            if (m_Back.Count > 0) m_Back.Pop()();
            else if (m_PauseOpen) ResumeGame();
        }

        // ------------------------------------------------------------------ main menu
        public IEnumerator ShowMainMenuRoutine()
        {
            Game.InputLocks++;
            if (Fader.I.Alpha < 0.99f) yield return Fader.I.FadeOut(0.4f);
            Fader.I.SetColor(Color.black);
            Game.Cells.Activate(CellId.Menu);
            var cell = Game.Cells.Active;
            Game.Rig.PlaceAt(cell.Arrival.position, cell.Arrival.eulerAngles.y);
            AudioDirector.I?.SetMusic("mus_menu", 3f, 0.7f);
            Game.Campaign = SaveSystem.LoadCampaign() ?? CampaignState.NewGame();
            Show(true);
            MainPage();
            if (m_OpenLevelOnMenu.HasValue)
            {
                var def = Levels.Get(m_OpenLevelOnMenu.Value);
                m_OpenLevelOnMenu = null;
                Push(MainPage, () => LevelPage(def));
            }
            yield return null;
            cell.RefreshProbe();
            Game.InputLocks = Mathf.Max(0, Game.InputLocks - 1);
            yield return Fader.I.FadeIn(1.2f);
        }

        /// <summary>
        /// The rig was rebuilt for the other mode (a browser VR session started or ended, GameBootstrap.SwitchPresentation).
        /// Cards and the welcome art are laid out per mode, so the open menu is shown again for the new rig: the welcome
        /// screen and the pause menu from their first page, any other card where it is.
        /// </summary>
        public IEnumerator ReopenForRig()
        {
            if (!IsOpen) yield break;
            bool atWelcome = Game.Cells.Active != null && Game.Cells.Active.Id == CellId.Menu && (StoryDirector.I == null || !StoryDirector.I.InCampaign);
            if (m_PauseOpen) { HideAll(); OpenPause(); }
            else if (atWelcome) { HideAll(); Show(true); MainPage(); }
            else Show(m_AtDesk);
            yield return null;
        }

        void MainPage()
        {
            m_Back.Clear();
            // The welcome screen: the logo above the card is the title (Branding.ShowWelcome), so the card has none.
            Page("", () =>
            {
                m_Panel.Text("<i>" + Branding.Tagline + "</i>", 0.0032f, TMPro.TextAlignmentOptions.Center, 0f, new Color(0.3f, 0.27f, 0.27f), true);
                m_Panel.Space(0.01f);
                m_Panel.Text("<b>CHOOSE A LEVEL</b>", 0.0024f, TMPro.TextAlignmentOptions.Center, 0f, new Color(0.5f, 0.09f, 0.08f));
                m_Panel.LevelGrid(Levels.All, d => Levels.IsCompleted(d.Id), d => Push(MainPage, () => LevelPage(d)));
                m_Panel.Space(0.008f);
                // Only the levels are offered (18 Oct: the judges' demo button is gone; --demo still boots it for testing).
                m_Panel.Button("Controls", () => Push(MainPage, ControlsPage));
                m_Panel.Button("Settings", () => Push(MainPage, SettingsPage));
                m_Panel.Button("Credits", () => Push(MainPage, CreditsPage));
                m_Panel.Button("Quit", Quit);
                m_Panel.Text("<size=80%>" + (Game.Mode == GameMode.VR ? (Application.platform == RuntimePlatform.WebGLPlayer ? "Browser VR" : "Quest 3 (PC VR)") : "Desktop") + " mode · v" + Application.version + "</size>", 0.0026f, TMPro.TextAlignmentOptions.Center, 0f, new Color(0.4f, 0.38f, 0.38f));
            }, false);
            Branding.I?.ShowWelcome(m_Panel.transform, Panel3D.CardWidth, m_Panel.CardHeight);
            m_Panel.AnimateIn();
        }

        /// <summary>A level's card: what it holds, then Play (Hard: New Story / Continue, the story's own save).</summary>
        void LevelPage(LevelDef def)
        {
            Page(def.Label, () =>
            {
                m_Panel.Text("<color=" + def.Hex + "><b>" + def.Difficulty.ToUpperInvariant() + "</b></color>", 0.0034f);
                m_Panel.Text(def.Blurb, 0.0028f, TMPro.TextAlignmentOptions.Center, 0.02f);
                if (def.Id != LevelId.FreeRoam && def.Countries.Length < CountryDatabase.CountryCount)
                    m_Panel.Text("<size=90%>" + string.Join("   ·   ", System.Array.ConvertAll(def.Countries, c => CountryDatabase.Get(c).DisplayName)) + "</size>", 0.0026f, TMPro.TextAlignmentOptions.Center, 0.02f, new Color(0.3f, 0.27f, 0.27f));
                if (def.EndsInVictoryGround)
                    m_Panel.Text("<i><size=90%>Finish it to reach the Victory Ground.</size></i>", 0.0026f, TMPro.TextAlignmentOptions.Center, 0f, new Color(0.3f, 0.27f, 0.27f));
                if (def.IsStory)
                {
                    bool hasSave = SaveSystem.HasValidCampaign();
                    m_Panel.Button(hasSave ? "New Story" : "Story Mode", () =>
                    {
                        if (hasSave) Push(() => LevelPage(def), ConfirmNewGame);
                        else { HideAll(); StoryDirector.I.StartNewGame(); }
                    });
                    m_Panel.Button("Continue", () => { HideAll(); StoryDirector.I.ContinueGame(); }, hasSave);
                }
                else m_Panel.Button("Play", () => { HideAll(); StoryDirector.I.StartLevel(def.Id); });
                m_Panel.Button("Back", Back);
            });
            m_Panel.AnimateIn(0.035f);
        }

        /// <summary>The Victory Ground's results: next level, replay, stay, or the menu.</summary>
        public void ShowLevelComplete(LevelDef def, string summary)
        {
            Show(false);
            m_Back.Clear();
            Page("Level complete", () =>
            {
                m_Panel.Text("<color=" + def.Hex + "><b>" + def.Label.ToUpperInvariant() + "</b></color>", 0.0034f);
                m_Panel.Text(summary, 0.003f, TMPro.TextAlignmentOptions.Center, 0.1f);
                var next = Levels.Next(def.Id);
                if (next != null)
                    m_Panel.Button("Next: " + next.Label, () =>
                    {
                        if (next.IsStory) { HideAll(); m_OpenLevelOnMenu = next.Id; StoryDirector.I.ReturnToMenu(); return; }
                        HideAll(); StoryDirector.I.StartLevel(next.Id);
                    });
                m_Panel.Button("Play again", () => { HideAll(); StoryDirector.I.StartLevel(def.Id); });
                m_Panel.Button("Stay on the Victory Ground", HideAll);
                m_Panel.Button("Main menu", () => { HideAll(); StoryDirector.I.ReturnToMenu(); });
            });
            m_Panel.AnimateIn(0.05f, 0.1f);
        }

        void ConfirmNewGame()
        {
            Page("Start a new story?", () =>
            {
                m_Panel.Text("Your saved progress will be overwritten.", 0.0032f);
                m_Panel.Button("Start new story", () => { HideAll(); SaveSystem.DeleteCampaign(); StoryDirector.I.StartNewGame(); });
                m_Panel.Button("Cancel", Back);
            });
        }

        // ------------------------------------------------------------------ controls (generated from real bindings)
        void ControlsPage()
        {
            Page("Controls", () =>
            {
                var input = Game.Input;
                string Row(string what, Intent i) => "<b>" + what + "</b>   " + input.Label(i);
                var lines = new List<string>
                {
                    Row("Move", Game.Mode == GameMode.VR ? Intent.Teleport : Intent.Move),
                    Row(Game.Mode == GameMode.VR ? "Turn" : "Look", Game.Mode == GameMode.VR ? Intent.Turn : Intent.Look),
                    Row("Grab / pick up", Intent.Grab),
                    Row("Use / interact", Intent.Interact),
                    Row("Passport (summon / put away)", Intent.Passport),
                    Row("Choose destination", Intent.Destination),
                    Row("Open / widen a tear", Intent.Tear),
                    Row("Seal a tear", Intent.Seal),
                    Row("Recall essential item", Intent.Recall),
                    Row("Pause", Intent.Pause),
                    Row("Remove the glasses (returns you to the apartment)", Intent.RemoveGlasses),
                };
                lines.Insert(1, Row("Run", Intent.Sprint));
                if (Game.Mode == GameMode.Desktop)
                {
                    lines.Add(Row("Objectives and steps", Intent.Objectives));
                    lines.Add(Row("Map", Intent.Map));
                }
                m_Panel.Text(string.Join("\n", lines), 0.0027f, TMPro.TextAlignmentOptions.Left, 0.36f);
                if (Game.Mode == GameMode.VR)
                    m_Panel.Text("<i>Raise a controller to your face to see its button labels. The current step and the minimap stay in view.</i>", 0.0026f);
                m_Panel.Button("Back", Back);
            });
        }

        // ------------------------------------------------------------------ settings
        void SettingsPage()
        {
            Page("Settings", () =>
            {
                m_Panel.Button("Audio & subtitles", () => Push(SettingsPage, AudioPage));
                m_Panel.Button("Display & effects", () => Push(SettingsPage, DisplayPage));
                m_Panel.Button(Game.Mode == GameMode.VR ? "VR comfort & controls" : "Mouse & controls", () => Push(SettingsPage, ComfortPage));
                m_Panel.Button("Horror intensity", () => Push(SettingsPage, IntensityPage));
                m_Panel.Button("Back", Back);
            });
        }

        void AudioPage()
        {
            var s = Game.Settings;
            Page("Audio & subtitles", () =>
            {
                m_Panel.Slider("Master", s.master, v => { s.master = v; Save(); });
                m_Panel.Slider("Music", s.music, v => { s.music = v; Save(); });
                m_Panel.Slider("Ambience", s.ambience, v => { s.ambience = v; Save(); });
                m_Panel.Slider("Effects", s.sfx, v => { s.sfx = v; Save(); });
                m_Panel.Slider("Voice", s.voice, v => { s.voice = v; Save(); });
                m_Panel.Slider("Interface", s.ui, v => { s.ui = v; Save(); });
                m_Panel.Toggle("Reduced dynamic range", s.reducedDynamicRange, v => { s.reducedDynamicRange = v; Save(); });
                m_Panel.Toggle("Subtitles", s.subtitles, v => { s.subtitles = v; Save(); });
                m_Panel.Choice("Subtitle size", new[] { "Small", "Medium", "Large" }, s.subtitleSize, i => { s.subtitleSize = i; Save(); });
                m_Panel.Toggle("Subtitle background", s.subtitleBackground, v => { s.subtitleBackground = v; Save(); });
                m_Panel.Button("Test voice", () => Narration.I?.Say("pro_01", null, false, null, true));
                m_Panel.Button("Back", Back);
            });
        }

        void DisplayPage()
        {
            var s = Game.Settings;
            Page("Display & effects", () =>
            {
                m_Panel.Text("Brightness: adjust until the inner square is barely visible.", 0.0028f);
                m_Panel.Slider("Brightness", (s.brightness + 1f) / 2f, v => { s.brightness = v * 2f - 1f; Save(); }, v => (v * 2f - 1f).ToString("+0.00;-0.00;0"));
                m_Panel.Choice("Graphics", new[] { "Low", "Medium", "High", "Ultra" }, s.graphicsPreset, i => { s.graphicsPreset = i; Save(); });
                if (Game.Mode == GameMode.Desktop)
                {
                    m_Panel.Toggle("Fullscreen", s.fullscreen, v => { s.fullscreen = v; Save(); });
                    var res = Screen.resolutions;
                    var names = new List<string> { "Native" };
                    int start = Mathf.Max(0, res.Length - 6);
                    for (int i = start; i < res.Length; i++) names.Add(res[i].width + "×" + res[i].height);
                    int current = s.resolutionIndex < 0 ? 0 : Mathf.Clamp(s.resolutionIndex - start + 1, 0, names.Count - 1);
                    m_Panel.Choice("Resolution", names.ToArray(), current, i => { s.resolutionIndex = i == 0 ? -1 : start + i - 1; Save(); });
                }
                m_Panel.Toggle("Reduced effects (no grain / lens overlay)", s.reducedEffects, v => { s.reducedEffects = v; Save(); });
                m_Panel.Toggle("Vitality bar", s.showVitalityBar, v => { s.showVitalityBar = v; Save(); });
                m_Panel.Button("Back", Back);
            });
        }

        void ComfortPage()
        {
            var s = Game.Settings;
            Page(Game.Mode == GameMode.VR ? "VR comfort & controls" : "Mouse & controls", () =>
            {
                if (Game.Mode == GameMode.Desktop)
                {
                    m_Panel.Slider("Mouse sensitivity", Mathf.InverseLerp(0.2f, 3f, s.mouseSensitivity), v => { s.mouseSensitivity = Mathf.Lerp(0.2f, 3f, v); Save(); }, v => Mathf.Lerp(0.2f, 3f, v).ToString("0.0") + "×");
                    m_Panel.Toggle("Invert vertical look", s.invertY, v => { s.invertY = v; Save(); });
                }
                m_Panel.Choice("Movement", new[] { "Smooth", "Teleport" }, s.smoothLocomotion ? 0 : 1, i => { s.smoothLocomotion = i == 0; Save(); });
                m_Panel.Choice("Turning", new[] { "Snap", "Smooth (opt-in)" }, s.smoothTurn ? 1 : 0, i => { s.smoothTurn = i == 1; Save(); });
                m_Panel.Choice("Snap angle", new[] { "30°", "45°", "60°" }, s.snapAngle <= 30 ? 0 : s.snapAngle <= 45 ? 1 : 2, i => { s.snapAngle = i == 0 ? 30 : i == 1 ? 45 : 60; Save(); });
                m_Panel.Toggle("Left-handed (passport in right hand)", s.leftHanded, v => { s.leftHanded = v; Save(); });
                m_Panel.Toggle("Seated play", s.seated, v => { s.seated = v; Save(); });
                m_Panel.Slider("Seated height boost", s.seatedHeight / 0.8f, v => { s.seatedHeight = v * 0.8f; Save(); }, v => (v * 80f).ToString("0") + " cm");
                m_Panel.Slider("Haptics", s.haptics, v => { s.haptics = v; Save(); AudioDirector.Haptic(Game.Input.DominantHand, 0.5f, 0.08f); });
                m_Panel.Button("Back", Back);
            });
        }

        void IntensityPage()
        {
            var s = Game.Settings;
            Page("Horror intensity", () =>
            {
                m_Panel.Text("Gentle slows the drain and softens scare peaks. The story is identical.", 0.0028f, TMPro.TextAlignmentOptions.Center, 0.02f);
                m_Panel.Choice("Scare intensity", new[] { "Gentle", "Standard" }, s.scareIntensity, i => { s.scareIntensity = i; Save(); });
                m_Panel.Button("Back", Back);
            });
        }

        static void Save() => Game.ApplySettings();

        // ------------------------------------------------------------------ credits
        void CreditsPage()
        {
            Page("Credits", () =>
            {
                m_Panel.Text(CreditsText.Body, 0.0024f, TMPro.TextAlignmentOptions.Left, 0.42f);
                m_Panel.Button("Back", Back);
            });
        }

        // ------------------------------------------------------------------ pause
        void Update()
        {
            var input = Game.Input;
            if (input == null) return;
            if (Game.Mode == GameMode.Desktop && input.Pause.WasPressedThisFrame())
            {
                if (m_PauseOpen) Back();
                else if (IsOpen && m_Back.Count > 0) Back();
                else TogglePause();
            }
            // Headset removed (user presence) or controllers lost: pause automatically where the runtime reports it.
            if (Game.Mode == GameMode.VR && StoryDirector.I != null && StoryDirector.I.InCampaign && !m_PauseOpen)
            {
                var hmd = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
                bool present = true;
                if (hmd.isValid && hmd.TryGetFeatureValue(UnityEngine.XR.CommonUsages.userPresence, out bool p)) present = p;
                m_PresenceLostTime = present ? 0f : m_PresenceLostTime + Time.unscaledDeltaTime;
                if (m_PresenceLostTime > 0.5f) OpenPause();
            }
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus && Game.Mode == GameMode.Desktop && StoryDirector.I != null && StoryDirector.I.InCampaign && !m_PauseOpen && !Game.IsAutoTest) OpenPause();
        }

        public void TogglePause()
        {
            if (m_PauseOpen) { ResumeGame(); return; }
            if (StoryDirector.I == null || !StoryDirector.I.InCampaign || IsOpen) return;
            OpenPause();
        }

        void OpenPause()
        {
            m_PauseOpen = true;
            Game.Paused = true;
            Show(false);
            PausePage();
        }

        void PausePage()
        {
            m_Back.Clear();
            Page("Paused", () =>
            {
                m_Panel.Button("Resume", ResumeGame);
                m_Panel.Button("Controls", () => Push(PausePage, ControlsPage));
                m_Panel.Button("Settings", () => Push(PausePage, SettingsPage));
                if (StoryDirector.I.CanRemoveGlassesEarly && !Game.IsDemo)
                    m_Panel.Button("Remove the glasses (hold)", () => { ResumeGame(); StoryDirector.I.RequestEarlyGlassesRemoval(); }, true, 2f);
                m_Panel.Button(Game.IsDemo ? "Restart demo" : "Restart from checkpoint", () =>
                {
                    ResumeGame();
                    if (Game.IsDemo) { StoryDirector.I.StopAllCoroutines(); StoryDirector.I.StartDemo(); }
                    else StoryDirector.I.RetryCheckpoint();
                });
                if (!Game.IsDemo && StoryDirector.I.InCampaign)
                    m_Panel.Button(RestartLabel, RestartLevel, true, 1.5f);
                m_Panel.Button("Main menu", () => { ResumeGame(); StoryDirector.I.ReturnToMenu(); });
                m_Panel.Button("Quit", Quit);
                m_Panel.Text("<size=80%>Progress is saved at each checkpoint and verified seal.</size>", 0.0026f);
            });
        }

        public void ResumeGame()
        {
            HideAll();
            m_PauseOpen = false;
            Game.Paused = false;
            AudioDirector.I?.PlayUI("ui_back", 0.6f);
        }

        // ------------------------------------------------------------------ failure / results
        public void ShowFailure()
        {
            Hud.I?.HideTitleCard();
            Show(false);
            m_Back.Clear();
            Page("The Margin takes hold", () =>
            {
                m_Panel.Text("Your verified seals are safe. The encounter will reset.", 0.003f);
                m_Panel.Button("Retry from checkpoint", () => { HideAll(); StartCoroutine(RetryRoutine()); });
                if (!Game.IsDemo) m_Panel.Button(RestartLabel, RestartLevel, true, 1.5f);
                m_Panel.Button("Main menu", () => { HideAll(); StoryDirector.I.ReturnToMenu(); });
            });
        }

        // ------------------------------------------------------------------ restart the whole level
        /// <summary>"Restart level" in a level run (from its very start, story opening included); in the full story it
        /// starts the story again. Held to confirm: it throws away the run.</summary>
        static string RestartLabel => Levels.Current != null ? "Restart level (hold)" : "Restart the story (hold)";

        void RestartLevel()
        {
            var def = Levels.Current;
            StartCoroutine(RestartLevelRoutine(def));
        }

        /// <summary>Leaves through the welcome room (the same clean slate as Main menu), then starts the level again.</summary>
        IEnumerator RestartLevelRoutine(LevelDef def)
        {
            HideAll();
            m_PauseOpen = false;
            Game.Paused = false;
            StoryDirector.I.ReturnToMenu();
            for (float t = 0f; t < 20f && !(Game.Cells.Active != null && Game.Cells.Active.Id == CellId.Menu && Branding.I != null && Branding.I.WelcomeShowing); t += Time.unscaledDeltaTime)
                yield return null;
            yield return new WaitForSecondsRealtime(0.2f);
            HideAll();
            if (def == null || def.IsStory) StoryDirector.I.StartNewGame();
            else StoryDirector.I.StartLevel(def.Id);
        }

        IEnumerator RetryRoutine()
        {
            Fader.I.SetColor(Color.black);
            StoryDirector.I.RetryCheckpoint();
            yield break;
        }

        public void ShowDemoResults(float seconds)
        {
            Show(false);
            m_Back.Clear();
            int m = Mathf.FloorToInt(seconds / 60f), sec = Mathf.FloorToInt(seconds % 60f);
            var c = Game.Campaign;
            Page("Demo complete", () =>
            {
                m_Panel.Text("Time: " + m + ":" + sec.ToString("00") + (Game.IsAutoTest ? " (automated run)" : "") + "   ·   Vitality: " + Mathf.RoundToInt(c.vitality * 100) + "%   ·   Tears opened: " + c.tearsOpened, 0.0032f);
                m_Panel.Text("<i>The full story: a shop in Marrakech, an apartment in Abu Dhabi, eleven countries and one forged stamp.</i>", 0.0027f, TMPro.TextAlignmentOptions.Center, 0.02f);
                m_Panel.Button("Reset demo", () => { HideAll(); StoryDirector.I.StartDemo(); });
                m_Panel.Button("Main menu", () => { HideAll(); StoryDirector.I.ReturnToMenu(); });
            });
        }

        public void ShowResults(string summary, System.Action onContinue)
        {
            Show(false);
            m_Back.Clear();
            Page("Home", () =>
            {
                m_Panel.Text(summary, 0.003f, TMPro.TextAlignmentOptions.Center, 0.12f);
                m_Panel.Button("Credits", () => { HideAll(); onContinue(); });
            });
        }

        public void ShowCreditsThenMenu()
        {
            Show(false);
            m_Back.Clear();
            Page("Credits", () =>
            {
                m_Panel.Text(CreditsText.Body, 0.0024f, TMPro.TextAlignmentOptions.Left, 0.42f);
                m_Panel.Button("Main menu", () => { HideAll(); StoryDirector.I.ReturnToMenu(); });
            });
        }

        /// <summary>Shown on the desktop window when --mode=vr could not start XR. choice: 0 retry, 1 desktop, 2 quit.</summary>
        public void ShowVrFailure(string reason, System.Action<int> choice)
        {
            Show(false);
            m_Back.Clear();
            Page("VR could not start", () =>
            {
                m_Panel.Text(reason ?? "The headset runtime is not available.", 0.003f, TMPro.TextAlignmentOptions.Center, 0.08f);
                m_Panel.Text("<size=85%>Checklist: Meta Horizon Link open on this PC · Quest 3 connected (cable or Air Link) · Link prompt accepted in the headset · Meta Horizon Link set as the OpenXR runtime.</size>", 0.0026f, TMPro.TextAlignmentOptions.Center, 0.06f);
                m_Panel.Button("Retry VR", () => choice(0));
                m_Panel.Button("Play in Desktop mode instead", () => choice(1));
                m_Panel.Button("Quit", () => choice(2));
            });
        }

        /// <summary>First boot: comfort and audio setup before the welcome menu.</summary>
        public void ShowFirstRun()
        {
            var s = Game.Settings;
            Show(true);
            m_Back.Clear();
            Page("Before you begin", () =>
            {
                m_Panel.Text("Adjust brightness until the small square on the desk lamp's card is just visible.", 0.0028f);
                m_Panel.Slider("Brightness", (s.brightness + 1f) / 2f, v => { s.brightness = v * 2f - 1f; Save(); }, v => (v * 2f - 1f).ToString("+0.00;-0.00;0"));
                m_Panel.Slider("Master volume", s.master, v => { s.master = v; Save(); });
                m_Panel.Button("Play a test sound", () => AudioDirector.I?.Play("seal_verify", null, 0.9f));
                m_Panel.Toggle("Subtitles", s.subtitles, v => { s.subtitles = v; Save(); });
                if (Game.Mode == GameMode.VR)
                {
                    m_Panel.Choice("Movement", new[] { "Smooth", "Teleport" }, s.smoothLocomotion ? 0 : 1, i => { s.smoothLocomotion = i == 0; Save(); });
                    m_Panel.Toggle("Seated play", s.seated, v => { s.seated = v; Save(); });
                    m_Panel.Toggle("Left-handed", s.leftHanded, v => { s.leftHanded = v; Save(); });
                }
                m_Panel.Choice("Scare intensity", new[] { "Gentle", "Standard" }, s.scareIntensity, i => { s.scareIntensity = i; Save(); });
                m_Panel.Button("Continue", () => { s.comfortSetupDone = true; Save(); MainPage(); });
            });
        }

        public static void Quit()
        {
            if (Game.Story != null && Game.Story.InCampaign) Game.SaveNow(); // never save the demo's or a blank campaign from the menu
            SaveSystem.SaveSettings(Game.Settings);
            XRBoot.Shutdown();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

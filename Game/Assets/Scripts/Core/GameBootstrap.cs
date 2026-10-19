using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Entry point. The scene is empty; everything is created here. Parses OUR arguments (--mode=vr|desktop),
    /// starts XR only for VR mode, shows Retry / Desktop Mode / Quit if VR cannot start, then the welcome menu.
    /// In the browser build the game starts in desktop mode and follows the page's WebXR session instead (see Update).
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        GameObject m_Presentation;
        /// <summary>The welcome menu (or the requested start) is up: from here a browser VR session may swap the rig.</summary>
        public bool Started { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<GameBootstrap>() != null) return;
            var go = new GameObject("TwelfthStamp");
            DontDestroyOnLoad(go);
            go.AddComponent<GameBootstrap>();
        }

        IEnumerator Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // The headset's browser takes focus away from the page while presenting: keep reading the XR devices.
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
#endif
            AppArgs.Parse();
            Game.IsAutoTest = AppArgs.AutoTest != null;
            if (!string.IsNullOrEmpty(AppArgs.SaveDir)) SaveSystem.OverrideDirectory(AppArgs.SaveDir);
            SaveSystem.WritesSuspended = Game.TestWritesBlocked;
            Game.Root = transform;
            Game.Settings = SaveSystem.LoadSettings();
            if (!Game.Settings.comfortSetupDone && SystemInfo.graphicsMemorySize >= 8000)
                Game.Settings.graphicsPreset = (AppArgs.Mode ?? GameMode.Desktop) == GameMode.VR ? 2 : 3;
            var requested = AppArgs.Mode ?? GameMode.Desktop;
            Debug.Log($"[TS-Boot] {Application.productName} {Application.version} | args: {string.Join(" ", Environment.GetCommandLineArgs())}");
            Debug.Log($"[TS-Boot] requested mode = {requested}" + (AppArgs.ModeArgumentPresent ? "" : " (no --mode argument: defaulting to desktop)") +
                      (AppArgs.InvalidModeValue != null ? $" (ignored invalid --mode value '{AppArgs.InvalidModeValue}')" : ""));

            AudioDirector.Create(transform);
            Narration.Create(transform);
            Game.Input = new GameInput();
            Game.Cells = CellManager.Create(transform);
            Game.Tears = TearController.Create(transform);
            PostFx.Create(transform);
            Game.Story = StoryDirector.Create(transform);
            Tutorial.Create(transform);
            MenuSystem.Create(transform);
            Branding.Create(transform);
            UiPointer.Create(transform);
            Game.Passenger = PassengerController.Create(transform);
            Physics.IgnoreLayerCollision(Layers.PlayerBody, Layers.Pocket, true);
            Physics.IgnoreLayerCollision(Layers.Passenger, Layers.PlayerBody, true);
            Physics.IgnoreLayerCollision(Layers.Interactable, Layers.PlayerBody, true);

            var mode = requested;
            // Capture only: VR mode without OpenXR (no headset), for scripted in-engine footage of the VR rig. Never used by the
            // shortcuts, and the footage must not be presented as headset capture.
            bool vrPreview = requested == GameMode.VR && AppArgs.VrPreview && Game.IsAutoTest;
            if (vrPreview) Debug.Log("[TS-Boot] VR preview: VR rig without OpenXR (capture only, no headset)");
            if (requested == GameMode.VR && !vrPreview)
            {
                while (true)
                {
                    Game.Mode = GameMode.VR;
                    yield return XRBoot.StartXR();
                    if (XRBoot.Running) break;
                    // Explain on the desktop window and let the player decide. Never claim VR is working.
                    int choice = -1;
                    BuildPresentation(GameMode.Desktop);
                    Game.Cells.Activate(CellId.Menu);
                    Game.Rig.PlaceAt(Game.Cells.Active.Arrival.position, Game.Cells.Active.Arrival.eulerAngles.y);
                    Fader.I.FadeTo(0f, 0.5f);
                    MenuSystem.I.ShowVrFailure(XRBoot.LastError, c => choice = c);
                    while (choice < 0) yield return null;
                    MenuSystem.I.HideAll();
                    if (choice == 2) { MenuSystem.Quit(); yield break; }
                    if (choice == 1) { mode = GameMode.Desktop; Debug.Log("[TS-Boot] VR unavailable; player chose Desktop mode"); break; }
                    TearDownPresentation();
                }
            }

            Game.Mode = mode;
            if (m_Presentation == null || Game.Rig == null || (mode == GameMode.VR) != (Game.Rig is VRRig))
            {
                TearDownPresentation();
                BuildPresentation(mode);
            }
            Debug.Log("[TS-Boot] running in " + mode + " mode" + (mode == GameMode.VR ? (vrPreview ? " (preview, no OpenXR)" : " via " + XRBoot.RuntimeName) : ""));
            if (vrPreview && Game.Rig is VRRig previewRig) previewRig.UseScriptedPose();

            if (Game.IsAutoTest)
            {
                gameObject.AddComponent<AutoTest>().Begin(AppArgs.AutoTest, AppArgs.ReportDir); // plays and checks the intro itself
                yield break;
            }
            // The branded intro after the initial load (skippable; Art/Branding/BRANDING.md).
            if (!AppArgs.NoIntro) yield return Branding.I.Intro();
            if (AppArgs.Demo) { Fader.I.FadeTo(1f, 0f); Game.Story.StartDemo(); Started = true; yield break; }
            if (!string.IsNullOrEmpty(AppArgs.StartCell) && Enum.TryParse(AppArgs.StartCell, true, out CellId start))
            {
                Game.Story.StartAt(start);
                Started = true;
                yield break;
            }
            yield return MenuSystem.I.ShowMainMenuRoutine();
            if (!Game.Settings.comfortSetupDone) MenuSystem.I.ShowFirstRun();
            Started = true;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // ------------------------------------------------------------------ browser VR (WebXR Export)
        // The page's VR button starts an immersive WebXR session, which creates and runs an XR display subsystem; ending the
        // session (or taking the headset off) destroys it. The game follows it with the matching rig. A session that starts
        // during the intro is picked up as soon as the welcome menu is up.
        readonly List<UnityEngine.XR.XRDisplaySubsystem> m_Displays = new List<UnityEngine.XR.XRDisplaySubsystem>();

        void Update()
        {
            if (!Started || Game.IsAutoTest || m_Presentation == null) return;
            SubsystemManager.GetSubsystems(m_Displays);
            bool presenting = false;
            foreach (var d in m_Displays) if (d.running) presenting = true;
            var want = presenting ? GameMode.VR : GameMode.Desktop;
            if (want != Game.Mode) SwitchPresentation(want);
        }
#endif

        /// <summary>
        /// Rebuilds the rig, HUD, fader and passport for <paramref name="mode"/> where the player stands, keeping the
        /// passport's selection, and reopens any open menu for the new rig. Story, cells and tears are untouched.
        /// </summary>
        void SwitchPresentation(GameMode mode)
        {
            var rig = Game.Rig;
            bool placed = rig != null;
            var feet = placed ? rig.Feet : Vector3.zero;
            float yaw = placed ? Quaternion.LookRotation(rig.Forward, Vector3.up).eulerAngles.y : 0f;
            var passport = Game.Passport;
            bool passportAvailable = passport == null || passport.Available;
            CountryId? selected = passport != null ? passport.Selected : null;
            Debug.Log("[TS-Boot] Switching to " + mode + " mode" + (mode == GameMode.VR ? " (browser VR session started)" : " (browser VR session ended)"));

            // Deactivate first so only one rig, one camera and one AudioListener are live this frame.
            if (m_Presentation != null) m_Presentation.SetActive(false);
            TearDownPresentation();
            BuildPresentation(mode);
            if (placed) Game.Rig.PlaceAt(feet, yaw);
            Game.Passport.Available = passportAvailable;
            Game.Passport.RestoreSelection(selected);
            StartCoroutine(AfterSwitch());
        }

        IEnumerator AfterSwitch()
        {
            yield return null; // the old presentation is gone at the end of the frame
            bool menu = MenuSystem.I != null && MenuSystem.I.IsOpen;
            if (menu) yield return MenuSystem.I.ReopenForRig();
            if (Fader.I != null && Fader.I.Alpha > 0.99f) Fader.I.FadeTo(0f, 0.5f);
        }

        void BuildPresentation(GameMode mode)
        {
            Game.Mode = mode;
            Game.Input.Enable(mode);
            m_Presentation = new GameObject("Presentation");
            m_Presentation.transform.SetParent(transform, false);
            Hud.Create(m_Presentation.transform);
            Game.Rig = mode == GameMode.VR ? (PlayerRig)VRRig.Create(m_Presentation.transform) : DesktopRig.Create(m_Presentation.transform);
            Fader.Create(m_Presentation.transform, Game.Rig.Cam);
            Fader.I.FadeTo(1f, 0f);
            Game.Passport = PassportController.Create(m_Presentation.transform);
            GraphicsQuality.Apply(Game.Settings);
        }

        void TearDownPresentation()
        {
            if (m_Presentation == null) return;
            Destroy(m_Presentation);
            m_Presentation = null;
            Game.Rig = null;
            Game.Passport = null;
        }

        void OnApplicationQuit()
        {
            // Only a story in progress is saved here (returning to the menu saves it already). At the menu Game.Campaign can
            // hold the demo's throwaway run or a blank one, which must never overwrite the player's story.
            if (Game.Story != null && Game.Story.InCampaign) Game.SaveNow();
            SaveSystem.SaveSettings(Game.Settings);
#if !UNITY_WEBGL || UNITY_EDITOR
            XRBoot.Shutdown(); // OpenXR only; the browser owns the WebXR session
#endif
        }
    }
}

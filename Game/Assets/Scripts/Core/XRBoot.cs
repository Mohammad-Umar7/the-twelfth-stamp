using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace TwelfthStamp
{
    /// <summary>
    /// Starts OpenXR only for --mode=vr (Initialize-on-Startup is disabled in the project), verifies that a display
    /// is actually running, and reports a clear reason on failure. Never silently falls back.
    /// </summary>
    public static class XRBoot
    {
        public static bool Running { get; private set; }
        public static string LastError { get; private set; }
        public static string RuntimeName { get; private set; }

        public static IEnumerator StartXR()
        {
            LastError = null;
            Running = false;
            var general = XRGeneralSettings.Instance;
            if (general == null || general.Manager == null)
            {
                LastError = "XR settings are missing from this build.";
                yield break;
            }
            var mgr = general.Manager;
            if (mgr.activeLoader != null) { mgr.StopSubsystems(); mgr.DeinitializeLoader(); }
            Debug.Log("[TS-XR] Initialising OpenXR loader...");
            yield return mgr.InitializeLoader();
            if (mgr.activeLoader == null)
            {
                LastError = "The OpenXR runtime did not start. Open Meta Horizon Link on this PC, connect the Quest 3 with Link " +
                            "(cable or Air Link) and accept the prompt in the headset, then choose Retry.";
                Debug.LogWarning("[TS-XR] " + LastError);
                yield break;
            }
            mgr.StartSubsystems();
            // Wait for a running display subsystem (the headset is actually presenting).
            var displays = new List<XRDisplaySubsystem>();
            float t = 0f;
            bool running = false;
            while (t < 6f)
            {
                SubsystemManager.GetSubsystems(displays);
                foreach (var d in displays) if (d.running) running = true;
                if (running) break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!running)
            {
                LastError = "OpenXR started but no headset display is running. Put on the headset, make sure Link is active, then choose Retry.";
                mgr.StopSubsystems();
                mgr.DeinitializeLoader();
                yield break;
            }
            var inputs = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(inputs);
            foreach (var i in inputs) i.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
            XRSettings.gameViewRenderMode = GameViewRenderMode.LeftEye;
            RuntimeName = mgr.activeLoader.name;
            Running = true;
            Debug.Log("[TS-XR] XR running via " + RuntimeName);
        }

        public static void Shutdown()
        {
            var mgr = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (mgr == null || mgr.activeLoader == null) { Running = false; return; }
            mgr.StopSubsystems();
            mgr.DeinitializeLoader();
            Running = false;
            Debug.Log("[TS-XR] XR shut down");
        }
    }
}

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;

namespace TwelfthStamp.EditorTools
{
    /// <summary>
    /// Idempotent setup for the WebXR browser build (De-Panther WebXR Export, com.de-panther.webxr + webxr-interactions in
    /// Packages/manifest.json). The Web target gets the WebXR loader with Initialize-on-Startup, the WebXR settings, the
    /// project's WebXR page template, WebGL2 only and no threads. The Windows target (OpenXR, no auto start) is untouched.
    /// The WebXR types are looked up by name, so this compiles (and reports) when the packages are missing.
    /// </summary>
    public static class WebXRSetup
    {
        const string LoaderType = "WebXR.WebXRLoader, WebXR";
        const string SettingsType = "WebXR.WebXRSettings, WebXR";
        const string SettingsKey = "WebXR.Settings";
        const string SettingsPath = "Assets/XR/Settings/WebXRSettings.asset";
        const string LoaderPath = "Assets/XR/Loaders/WebXRLoader.asset";
        public const string Template = "TwelfthStampWebXR";

        [MenuItem("Twelfth Stamp/Run WebXR Setup")]
        public static bool Apply()
        {
            var loaderType = Type.GetType(LoaderType);
            var settingsType = Type.GetType(SettingsType);
            if (loaderType == null || settingsType == null)
            {
                Debug.LogError("[TS-WebXR] ERROR: WebXR Export is not installed (add com.de-panther.webxr and com.de-panther.webxr-interactions to Packages/manifest.json)");
                return false;
            }
            if (!Directory.Exists(Path.Combine("Assets", "WebGLTemplates", Template)))
            {
                Debug.LogError("[TS-WebXR] ERROR: missing page template Assets/WebGLTemplates/" + Template);
                return false;
            }

            SetupLoader(loaderType);
            SetupSettings(settingsType);

            PlayerSettings.WebGL.template = "PROJECT:" + Template;
            PlayerSettings.WebGL.threadsSupport = false; // pthreads break the WebXR polyfill loader
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 }); // WebGL2 (no WebGPU for WebXR)
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;

            AssetDatabase.SaveAssets();
            Debug.Log("[TS-WebXR] Web target: WebXR loader (init on start), template " + Template + ", WebGL2 only, no threads");
            return true;
        }

        static void SetupLoader(Type loaderType)
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
            if (perTarget == null)
            {
                var guids = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
                if (guids.Length > 0) perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            if (perTarget == null)
            {
                Debug.LogError("[TS-WebXR] ERROR: XRGeneralSettingsPerBuildTarget not found (run Project Setup first)");
                return;
            }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, perTarget, true);
            if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.WebGL))
                perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.WebGL);

            var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.WebGL);
            // WebXRManager only enables itself when the loader is already active, so the Web target starts XR on load. The
            // headset only presents after the player presses the page's VR button.
            general.InitManagerOnStart = true;
            EditorUtility.SetDirty(general);

            var manager = general.Manager;
            if (!manager.activeLoaders.Any(l => l != null && l.GetType() == loaderType))
            {
                bool ok = XRPackageMetadataStore.AssignLoader(manager, loaderType.FullName, BuildTargetGroup.WebGL);
                if (!ok || !manager.activeLoaders.Any(l => l != null && l.GetType() == loaderType))
                {
                    var loader = AssetDatabase.LoadAssetAtPath(LoaderPath, loaderType) as XRLoader;
                    if (loader == null)
                    {
                        EnsureFolder(Path.GetDirectoryName(LoaderPath).Replace('\\', '/'));
                        loader = (XRLoader)ScriptableObject.CreateInstance(loaderType);
                        AssetDatabase.CreateAsset(loader, LoaderPath);
                    }
                    ok = manager.TryAddLoader(loader);
                }
                Debug.Log("[TS-WebXR] Assign WebXR loader to the Web target: " + ok);
            }
            EditorUtility.SetDirty(manager);
            AssetDatabase.SaveAssets();
        }

        static void SetupSettings(Type settingsType)
        {
            EditorBuildSettings.TryGetConfigObject(SettingsKey, out UnityEngine.Object existing);
            var s = existing != null && existing.GetType() == settingsType ? (ScriptableObject)existing : AssetDatabase.LoadAssetAtPath(SettingsPath, settingsType) as ScriptableObject;
            if (s == null)
            {
                EnsureFolder(Path.GetDirectoryName(SettingsPath).Replace('\\', '/'));
                s = ScriptableObject.CreateInstance(settingsType);
                AssetDatabase.CreateAsset(s, SettingsPath);
            }
            // Controllers only: the game has no hand-tracking input, so do not ask for it (the controllers would vanish).
            Set(s, "VROptionalFeatures", 0);
            Set(s, "VRRequiredReferenceSpace", 2); // local-floor
            // Multi-pass stereo in a browser: render a little under the recommended resolution to keep the frame rate.
            Set(s, "UseFramebufferScaleFactor", true);
            Set(s, "UseNativeResolution", false);
            Set(s, "FramebufferScaleFactor", 0.8f);
            Set(s, "AutoLoadWebXRManager", true);
            Set(s, "AutoLoadWebXRInputSystem", true); // registers the controllers and headset as Input System XR devices
            Set(s, "DisableXRDisplaySubsystem", false);
            EditorUtility.SetDirty(s);
            EditorBuildSettings.AddConfigObject(SettingsKey, s, true);
        }

        static void Set(object target, string field, object value)
        {
            var f = target.GetType().GetField(field);
            if (f == null) { Debug.LogWarning("[TS-WebXR] WebXRSettings has no field " + field); return; }
            f.SetValue(target, f.FieldType.IsEnum ? Enum.ToObject(f.FieldType, value) : Convert.ChangeType(value, f.FieldType));
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

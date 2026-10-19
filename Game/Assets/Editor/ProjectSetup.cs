using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace TwelfthStamp.EditorTools
{
    /// <summary>
    /// Idempotent, headless project configuration. Run with:
    /// Unity.exe -batchmode -quit -projectPath Game -executeMethod TwelfthStamp.EditorTools.ProjectSetup.Run
    /// </summary>
    public static class ProjectSetup
    {
        public const string SettingsDir = "Assets/Settings";
        public const string MaterialsDir = "Assets/Resources/Materials";
        public const string FontsResDir = "Assets/Resources/Fonts";
        public const string ScenePath = "Assets/Scenes/Main.unity";

        static readonly string[] Layers =
        {
            // index 6..12
            "Pocket", "PlayerBody", "Interactable", "Passenger", "Floor", "UI3D", "Seam"
        };

        [MenuItem("Twelfth Stamp/Run Project Setup")]
        public static void Run()
        {
            Log("Project setup starting");
            EnsureFolder(SettingsDir);
            EnsureFolder(MaterialsDir);
            EnsureFolder(FontsResDir);
            EnsureFolder("Assets/Scenes");

            SetupLayers();
            var urp = SetupUrp();
            SetupGraphicsAndQuality(urp);
            SetupPlayer();
            SetupXR();
            ImportTmpEssentials();
            SetupFonts();
            SetupMaterials();
            SetupVolumeProfile();
            SetupScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log("Project setup finished");
        }

        static void Log(string msg) => Debug.Log("[TS-Setup] " + msg);

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        // ------------------------------------------------------------------ layers
        static void SetupLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 0; i < Layers.Length; i++)
            {
                var p = layers.GetArrayElementAtIndex(6 + i);
                if (p.stringValue != Layers[i]) p.stringValue = Layers[i];
            }
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            Log("Layers set: " + string.Join(", ", Layers));
        }

        // ------------------------------------------------------------------ URP
        static UniversalRenderPipelineAsset SetupUrp()
        {
            const string assetPath = SettingsDir + "/TS_URP.asset";
            const string rendererPath = SettingsDir + "/TS_URP_Renderer.asset";

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                AssetDatabase.CreateAsset(renderer, rendererPath);
                TryReloadResources(renderer);
            }

            renderer.renderingMode = RenderingMode.Forward;
            renderer.depthPrimingMode = DepthPrimingMode.Disabled;
            if (renderer.postProcessData == null)
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            EditorUtility.SetDirty(renderer);

            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (urp == null)
            {
                urp = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(urp, assetPath);
            }

            var so = new SerializedObject(urp);
            SetBool(so, "m_RequireDepthTexture", false);
            SetBool(so, "m_RequireOpaqueTexture", false);
            SetBool(so, "m_SupportsHDR", true);
            SetInt(so, "m_MSAA", 4);
            SetFloat(so, "m_RenderScale", 1f);
            SetBool(so, "m_MainLightShadowsSupported", true);
            SetInt(so, "m_MainLightShadowmapResolution", 2048);
            SetInt(so, "m_AdditionalLightsRenderingMode", 1); // per pixel
            SetInt(so, "m_AdditionalLightsPerObjectLimit", 8);
            SetBool(so, "m_AdditionalLightShadowsSupported", true);
            SetInt(so, "m_AdditionalLightsShadowmapResolution", 2048);
            SetFloat(so, "m_ShadowDistance", 32f);
            SetInt(so, "m_ShadowCascadeCount", 2);
            SetBool(so, "m_SoftShadowsSupported", true);
            SetBool(so, "m_UseSRPBatcher", true);
            SetBool(so, "m_EnableLODCrossFade", false);
            SetInt(so, "m_ColorGradingMode", 1); // HDR grading
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);
            EnsureSsao(renderer);
            Log("URP asset ready: " + assetPath);
            return urp;
        }

        /// <summary>Screen-space ambient occlusion (toggled per graphics preset at runtime).</summary>
        static void EnsureSsao(UniversalRendererData renderer)
        {
            if (renderer.rendererFeatures.Exists(f => f is ScreenSpaceAmbientOcclusion)) return;
            var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            ssao.name = "SSAO";
            AssetDatabase.AddObjectToAsset(ssao, renderer);
            AssetDatabase.SaveAssets();
            var fso = new SerializedObject(ssao);
            SetFloat(fso, "m_Settings.Intensity", 1.1f);
            SetFloat(fso, "m_Settings.Radius", 0.3f);
            SetFloat(fso, "m_Settings.DirectLightingStrength", 0.2f);
            SetBool(fso, "m_Settings.Downsample", false);
            SetBool(fso, "m_Settings.AfterOpaque", false);
            fso.ApplyModifiedPropertiesWithoutUndo();
            var rso = new SerializedObject(renderer);
            var list = rso.FindProperty("m_RendererFeatures");
            var map = rso.FindProperty("m_RendererFeatureMap");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = ssao;
            if (map != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out string _, out long localId))
            {
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            }
            rso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
            Log("SSAO renderer feature added");
        }

        static void TryReloadResources(ScriptableObject target)
        {
            try
            {
                var t = Type.GetType("UnityEngine.Rendering.ResourceReloader, Unity.RenderPipelines.Core.Runtime");
                var m = t?.GetMethod("ReloadAllNullIn", BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(object), typeof(string) }, null);
                m?.Invoke(null, new object[] { target, "Packages/com.unity.render-pipelines.universal" });
            }
            catch (Exception e)
            {
                Log("ResourceReloader not used: " + e.Message);
            }
        }

        static void SetBool(SerializedObject so, string name, bool v)
        {
            var p = so.FindProperty(name);
            if (p == null) { Log("missing property " + name); return; }
            p.boolValue = v;
        }

        static void SetInt(SerializedObject so, string name, int v)
        {
            var p = so.FindProperty(name);
            if (p == null) { Log("missing property " + name); return; }
            if (p.propertyType == SerializedPropertyType.Enum) p.intValue = v; else p.intValue = v;
        }

        static void SetFloat(SerializedObject so, string name, float v)
        {
            var p = so.FindProperty(name);
            if (p == null) { Log("missing property " + name); return; }
            p.floatValue = v;
        }

        static void SetupGraphicsAndQuality(UniversalRenderPipelineAsset urp)
        {
            GraphicsSettings.defaultRenderPipeline = urp;
            var names = QualitySettings.names;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
                QualitySettings.vSyncCount = 1;
                QualitySettings.antiAliasing = 0;
            }
            QualitySettings.SetQualityLevel(Mathf.Clamp(current, 0, names.Length - 1), false);

            // Keep fog variants: all fog is configured at runtime, so automatic stripping (based on scenes) would remove it.
            var gs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            SetInt(gs, "m_FogStripping", 1);
            SetBool(gs, "m_FogKeepLinear", true);
            SetBool(gs, "m_FogKeepExp", true);
            SetBool(gs, "m_FogKeepExp2", true);
            gs.ApplyModifiedPropertiesWithoutUndo();
            Log("Graphics + quality configured for URP (" + names.Length + " quality levels)");
        }

        // ------------------------------------------------------------------ player
        static void SetupPlayer()
        {
            PlayerSettings.companyName = "Awaiz Ahmed";
            PlayerSettings.productName = "The Twelfth Stamp";
            PlayerSettings.bundleVersion = "0.9.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

            // Input System only (no legacy Input Manager).
            var ps = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var aih = ps.FindProperty("activeInputHandler");
            if (aih != null) { aih.intValue = 1; ps.ApplyModifiedPropertiesWithoutUndo(); }
            else Log("activeInputHandler property not found");
            Log("Player settings applied");
        }

        // ------------------------------------------------------------------ XR (OpenXR, no auto start)
        static void SetupXR()
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
            if (perTarget == null)
            {
                var guids = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
                if (guids.Length > 0)
                    perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            if (perTarget == null)
            {
                EnsureFolder("Assets/XR");
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                AssetDatabase.SaveAssets();
            }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, perTarget, true);

            if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Standalone))
                perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Standalone);

            var general = perTarget.SettingsForBuildTarget(BuildTargetGroup.Standalone);
            general.InitManagerOnStart = false; // our bootstrap starts XR only for --mode=vr
            EditorUtility.SetDirty(general);

            var manager = general.Manager;
            bool hasOpenXR = manager.activeLoaders.Any(l => l != null && l.GetType().Name == "OpenXRLoader");
            if (!hasOpenXR)
            {
                bool ok = XRPackageMetadataStore.AssignLoader(manager, "UnityEngine.XR.OpenXR.OpenXRLoader", BuildTargetGroup.Standalone);
                Log("Assign OpenXR loader: " + ok);
            }
            EditorUtility.SetDirty(manager);
            AssetDatabase.SaveAssets();

            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Standalone);
            var oxr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            if (oxr == null)
            {
                Log("ERROR: OpenXR settings for Standalone not found");
                return;
            }
            oxr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            int enabled = 0;
            foreach (var f in oxr.GetFeatures<OpenXRInteractionFeature>())
            {
                bool want = f is OculusTouchControllerProfile || f is MetaQuestTouchPlusControllerProfile;
                if (f.enabled != want) f.enabled = want;
                if (want) enabled++;
            }
            EditorUtility.SetDirty(oxr);
            AssetDatabase.SaveAssets();
            Log($"OpenXR configured: loader assigned, InitManagerOnStart=false, interaction profiles enabled={enabled}, renderMode={oxr.renderMode}");
        }

        // ------------------------------------------------------------------ TextMeshPro + fonts
        static void ImportTmpEssentials()
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0)
            {
                Log("TMP essentials already present");
                return;
            }
            var pkg = Path.GetFullPath("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage");
            if (!File.Exists(pkg))
            {
                Log("ERROR: TMP Essential Resources package not found at " + pkg);
                return;
            }
            AssetDatabase.ImportPackage(pkg, false);
            AssetDatabase.Refresh();
            Log("Imported TMP Essential Resources");
        }

        static void SetupFonts()
        {
            // TMP Settings only exist after the essentials package import has completed (next editor session).
            if (Resources.Load<TMP_Settings>("TMP Settings") == null)
            {
                Log("TMP Settings not loaded yet; font assets will be created on the next setup run");
                return;
            }
            try
            {
                MakeFontAsset("Assets/Fonts/Inter-Variable.ttf", FontsResDir + "/Inter SDF.asset");
                MakeFontAsset("Assets/Fonts/EBGaramond-Variable.ttf", FontsResDir + "/EBGaramond SDF.asset");
            }
            catch (Exception e)
            {
                Log("ERROR creating font assets: " + e);
            }
        }

        static void MakeFontAsset(string ttfPath, string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null) return;
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null)
            {
                Log("ERROR: font not found " + ttfPath);
                return;
            }
            var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (fa == null)
            {
                Log("ERROR: CreateFontAsset failed for " + ttfPath);
                return;
            }
            fa.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(fa, assetPath);
            if (fa.atlasTextures != null && fa.atlasTextures.Length > 0 && fa.atlasTextures[0] != null)
            {
                fa.atlasTextures[0].name = fa.name + " Atlas";
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            }
            if (fa.material != null)
            {
                fa.material.name = fa.name + " Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
            }
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            Log("Font asset created: " + assetPath);
        }

        // ------------------------------------------------------------------ material templates
        static void SetupMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlitUrp = Shader.Find("Universal Render Pipeline/Unlit");
            if (lit == null) { Log("ERROR: URP Lit shader not found"); return; }

            MakeLit("Lit_Opaque", lit, false, false, false);
            MakeLit("Lit_Normal", lit, true, false, false);
            MakeLit("Lit_Emissive", lit, false, true, false);
            MakeLit("Lit_NormalEmissive", lit, true, true, false);
            MakeLit("Lit_Transparent", lit, false, false, true);
            MakeLit("Lit_TransparentEmissive", lit, false, true, true);
            MakeLitCutout("Lit_Cutout", lit);
            if (unlitUrp != null) MakeSimple("URP_Unlit", unlitUrp, null);

            MakeSimple("TS_TearMark", Shader.Find("TwelfthStamp/TearMark"), null);
            MakeSimple("TS_TearClear", Shader.Find("TwelfthStamp/TearClear"), null);
            MakeSimple("TS_PocketLit", Shader.Find("TwelfthStamp/PocketLit"), null);
            MakeSimple("TS_SkyDome", Shader.Find("TwelfthStamp/SkyDome"), null);
            MakeSimple("TS_ParticleAlpha", Shader.Find("TwelfthStamp/Particle"), m =>
            {
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            });
            MakeSimple("TS_ParticleAdd", Shader.Find("TwelfthStamp/Particle"), m =>
            {
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.One);
            });
            MakeSimple("TS_Beam", Shader.Find("TwelfthStamp/Beam"), null);
            MakeSimple("TS_Rim", Shader.Find("TwelfthStamp/Rim"), null);
            MakeSimple("TS_PlanarMirror", Shader.Find("TwelfthStamp/PlanarMirror"), null);
            MakeSimple("TS_Unlit", Shader.Find("TwelfthStamp/Unlit"), null);
            MakeSimple("TS_UnlitTransparent", Shader.Find("TwelfthStamp/Unlit"), m =>
            {
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0);
                m.renderQueue = 3000;
            });
            MakeSimple("TS_Fade", Shader.Find("TwelfthStamp/Unlit"), m =>
            {
                m.SetColor("_Color", new Color(0, 0, 0, 0));
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0);
                m.SetFloat("_ZTest", (float)CompareFunction.Always);
                m.SetFloat("_Cull", (float)CullMode.Off);
                m.renderQueue = 4000;
            });
            Log("Material templates ready");
        }

        static void MakeLit(string name, Shader lit, bool normal, bool emissive, bool transparent)
        {
            var path = MaterialsDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool create = m == null;
            if (create) m = new Material(lit) { name = name };
            m.SetFloat("_Smoothness", 0.4f);
            m.SetFloat("_Metallic", 0f);
            if (normal) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else m.DisableKeyword("_EMISSION");

            if (transparent)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            if (create) AssetDatabase.CreateAsset(m, path);
            EditorUtility.SetDirty(m);
        }

        static void MakeLitCutout(string name, Shader lit)
        {
            var path = MaterialsDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool create = m == null;
            if (create) m = new Material(lit) { name = name };
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0f);
            m.renderQueue = (int)RenderQueue.AlphaTest;
            m.SetOverrideTag("RenderType", "TransparentCutout");
            if (create) AssetDatabase.CreateAsset(m, path);
            EditorUtility.SetDirty(m);
        }

        static void MakeSimple(string name, Shader shader, Action<Material> configure)
        {
            if (shader == null) { Log("ERROR: shader missing for " + name); return; }
            var path = MaterialsDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool create = m == null;
            if (create) m = new Material(shader) { name = name };
            configure?.Invoke(m);
            if (create) AssetDatabase.CreateAsset(m, path);
            EditorUtility.SetDirty(m);
        }

        // ------------------------------------------------------------------ post-processing profile (keeps variants)
        static void SetupVolumeProfile()
        {
            const string path = "Assets/Resources/TS_PostProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null) return;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            AddComponent<Tonemapping>(profile, t => { t.mode.overrideState = true; t.mode.value = TonemappingMode.ACES; });
            AddComponent<Bloom>(profile, b => { b.intensity.overrideState = true; b.intensity.value = 0.5f; b.threshold.overrideState = true; b.threshold.value = 1.1f; });
            AddComponent<ColorAdjustments>(profile, c => { c.postExposure.overrideState = true; c.saturation.overrideState = true; c.contrast.overrideState = true; c.colorFilter.overrideState = true; });
            AddComponent<Vignette>(profile, v => { v.intensity.overrideState = true; v.intensity.value = 0.22f; v.smoothness.overrideState = true; v.smoothness.value = 0.45f; });
            AddComponent<FilmGrain>(profile, g => { g.intensity.overrideState = true; g.intensity.value = 0.12f; });
            AddComponent<ChromaticAberration>(profile, ca => { ca.intensity.overrideState = true; ca.intensity.value = 0f; });
            AddComponent<LiftGammaGain>(profile, l => { });
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Log("Post-processing profile created");
        }

        static void AddComponent<T>(VolumeProfile profile, Action<T> configure) where T : VolumeComponent
        {
            var c = profile.Add<T>(true);
            c.name = typeof(T).Name;
            configure(c);
            AssetDatabase.AddObjectToAsset(c, profile);
        }

        // ------------------------------------------------------------------ scene
        static void SetupScene()
        {
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var marker = new GameObject("TwelfthStamp (world is built at runtime by GameBootstrap)");
                EditorSceneManager.SaveScene(scene, ScenePath);
                Log("Scene created: " + ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }

    /// <summary>Import rules for generated textures and audio.</summary>
    public class TwelfthStampImportRules : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith("Assets/Branding/Icons/"))
            {
                // Application icons: exact pixels for Unity's icon writer (no compression, no mips, no resizing).
                var ii = (TextureImporter)assetImporter;
                ii.textureType = TextureImporterType.Default;
                ii.sRGBTexture = true;
                ii.alphaIsTransparency = true;
                ii.mipmapEnabled = false;
                ii.npotScale = TextureImporterNPOTScale.None;
                ii.isReadable = true;
                ii.textureCompression = TextureImporterCompression.Uncompressed;
                ii.maxTextureSize = 2048;
                ii.wrapMode = TextureWrapMode.Clamp;
                return;
            }
            if (!assetPath.StartsWith("Assets/Resources/")) return;
            var ti = (TextureImporter)assetImporter;
            var file = Path.GetFileNameWithoutExtension(assetPath);
            if (file.Contains("smudge")) ti.sRGBTexture = false;
            if (file.EndsWith("_n"))
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.sRGBTexture = false;
            }
            else if (assetPath.Contains("/Art/"))
            {
                ti.textureType = TextureImporterType.Default;
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.mipmapEnabled = true;
            }
            if (assetPath.Contains("/Textures/")) ti.wrapMode = TextureWrapMode.Repeat;
            ti.anisoLevel = 4;
            ti.maxTextureSize = assetPath.Contains("/Art/") ? 2048 : 1024;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            if (assetPath.Contains("/Art/Branding/")) ti.npotScale = TextureImporterNPOTScale.None; // never resized to a power of two
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Resources/Audio/")) return;
            var ai = (AudioImporter)assetImporter;
            var s = ai.defaultSampleSettings;
            if (assetPath.Contains("/Ambience/") || assetPath.Contains("/Music/"))
            {
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.7f;
            }
            else if (assetPath.Contains("/Voice/"))
            {
                s.loadType = AudioClipLoadType.CompressedInMemory;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.8f;
                ai.forceToMono = false; // files are mono and -18 LUFS matched; Unity's normalize would undo that
            }
            else
            {
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.ADPCM;
            }
            ai.defaultSampleSettings = s;
            ai.loadInBackground = assetPath.Contains("/Voice/") || assetPath.Contains("/Ambience/") || assetPath.Contains("/Music/");
        }
    }

    /// <summary>
    /// Blender models (Resources/Models): records on each mesh how many metres one UV unit spans (area-weighted median over
    /// its triangles, so a few odd faces don't skew it). Models.Spawn uses it to keep tiling textures at the game's scale.
    /// </summary>
    public class TwelfthStampModelRules : AssetPostprocessor
    {
        public override uint GetVersion() => 1;

        void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith("Assets/Resources/Models/")) return;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                float metres = mf.sharedMesh != null ? MetresPerUv(mf.sharedMesh) : 0f;
                if (metres > 0f) mf.gameObject.AddComponent<UvDensity>().MetresPerUv = metres;
            }
        }

        static float MetresPerUv(Mesh mesh)
        {
            var v = mesh.vertices;
            var uv = mesh.uv;
            var t = mesh.triangles;
            if (uv == null || uv.Length != v.Length) return 0f;
            var tris = new List<(float ratio, float area)>();
            float total = 0f;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                Vector2 ua = uv[t[i]], ub = uv[t[i + 1]], uc = uv[t[i + 2]];
                float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                float uvArea = Mathf.Abs((ub.x - ua.x) * (uc.y - ua.y) - (uc.x - ua.x) * (ub.y - ua.y)) * 0.5f;
                if (area < 1e-12f || uvArea < 1e-12f) continue;
                tris.Add((Mathf.Sqrt(area / uvArea), area));
                total += area;
            }
            if (tris.Count == 0) return 0f;
            tris.Sort((x, y) => x.ratio.CompareTo(y.ratio));
            float acc = 0f;
            foreach (var x in tris)
            {
                acc += x.area;
                if (acc >= total * 0.5f) return x.ratio;
            }
            return tris[tris.Count - 1].ratio;
        }
    }
}

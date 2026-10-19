using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TwelfthStamp
{
    /// <summary>Global post-processing: per-cell grading + brightness setting + contamination/drain feedback.</summary>
    public class PostFx : MonoBehaviour
    {
        public static PostFx I { get; private set; }

        Volume m_Volume;
        VolumeProfile m_Profile;
        ColorAdjustments m_Color;
        Bloom m_Bloom;
        Vignette m_Vignette;
        FilmGrain m_Grain;
        ChromaticAberration m_Chroma;
        Tonemapping m_Tone;

        LightingPreset m_Preset = new LightingPreset();
        float m_Contamination;   // 0..1 from the open tear
        float m_Drain;           // 0..1 vitality loss feedback
        float m_Flash;           // brief white/sun flash
        float m_Shift;           // 0..1 dimension shift (DimensionShift): tinted grade, contrast, vignette, aberration
        float m_ShiftTint;       // 0..1 how much of the shift's tint reaches the colour filter (it gives way to the cracks' light)
        float m_Inhale;          // 0..1 the shift's held breath before the slam: one slow darkening

        public static PostFx Create(Transform parent)
        {
            var go = new GameObject("PostFx");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<PostFx>();
            I.Build();
            return I;
        }

        void Build()
        {
            m_Volume = gameObject.AddComponent<Volume>();
            m_Volume.isGlobal = true;
            m_Volume.priority = 10;
            var source = Resources.Load<VolumeProfile>("TS_PostProfile");
            m_Profile = source != null ? Instantiate(source) : ScriptableObject.CreateInstance<VolumeProfile>();
            m_Volume.profile = m_Profile;
            m_Color = Get<ColorAdjustments>();
            m_Bloom = Get<Bloom>();
            // Clamp: a single razor-sharp specular glint (glossy glass/metal under a point light) must not bloom into
            // a screen-sized white blob.
            m_Bloom.threshold.overrideState = true; m_Bloom.threshold.value = 1f;
            m_Bloom.clamp.overrideState = true; m_Bloom.clamp.value = 8f;
            m_Bloom.scatter.overrideState = true; m_Bloom.scatter.value = 0.65f;
            m_Vignette = Get<Vignette>();
            m_Grain = Get<FilmGrain>();
            m_Chroma = Get<ChromaticAberration>();
            m_Tone = Get<Tonemapping>();
            m_Tone.mode.overrideState = true;
            m_Tone.mode.value = TonemappingMode.ACES;
            ApplySettings();
        }

        T Get<T>() where T : VolumeComponent
        {
            if (!m_Profile.TryGet<T>(out var c)) c = m_Profile.Add<T>(true);
            return c;
        }

        public void SetPreset(LightingPreset p)
        {
            m_Preset = p ?? new LightingPreset();
            ApplySettings();
        }

        public void SetContamination(float c) => m_Contamination = Mathf.Clamp01(c);
        public void SetDrain(float d) => m_Drain = Mathf.Clamp01(d);
        public void Flash(float amount) => m_Flash = Mathf.Max(m_Flash, amount);
        /// <summary>Dimension-shift grade, 0..1 (set every frame by DimensionShift; it never flashes).</summary>
        public void SetShift(float s) => SetShift(s, s);

        /// <summary>As SetShift, with the colour filter at its own level, so the light through the cracks keeps its colour.</summary>
        public void SetShift(float s, float tint)
        {
            m_Shift = Mathf.Clamp01(s);
            m_ShiftTint = Mathf.Clamp01(tint);
        }

        Color m_ShiftLight = new Color(0.85f, 0.9f, 1f);

        /// <summary>As SetShift, tinted with the destination's light (no red: playtest, 18 Oct).</summary>
        public void SetShift(float s, float tint, Color light)
        {
            SetShift(s, tint);
            m_ShiftLight = light;
        }

        /// <summary>The shift's inhale, 0..1: the picture darkens once, slowly (exposure, and a touch of vignette on desktop).</summary>
        public void SetInhale(float i) => m_Inhale = Mathf.Clamp01(i);

        public void ApplySettings()
        {
            if (m_Color == null) return;
            var s = Game.Settings ?? new GameSettings();
            m_Color.postExposure.overrideState = true;
            m_Color.saturation.overrideState = true;
            m_Color.contrast.overrideState = true;
            m_Color.colorFilter.overrideState = true;
            m_Grain.active = !s.reducedEffects;
            m_Chroma.active = !s.reducedEffects;
        }

        const float SpookyExposure = 0.12f, SpookySaturation = 14f, SpookyContrast = 9f, SpookyVignette = 0.1f;

        void Update()
        {
            if (m_Color == null) return;
            var s = Game.Settings ?? new GameSettings();
            m_Flash = Mathf.MoveTowards(m_Flash, 0f, Time.unscaledDeltaTime * 1.5f);
            // The spooky grade over every place (playtest, 18 Oct: "make everything look scary and spooky"): a little darker,
            // colder and less saturated, harder contrast, a heavier vignette and film grain. Hearts running low push it further.
            float exposure = m_Preset.PostExposure - SpookyExposure + s.brightness * 0.75f + m_Flash * 0.8f;
            m_Color.postExposure.value = exposure;
            m_Color.saturation.value = m_Preset.Saturation - SpookySaturation - m_Drain * 45f - m_Contamination * 15f;
            m_Color.contrast.value = m_Preset.Contrast + SpookyContrast;
            var cold = new Color(m_Preset.ColorFilter.r * 0.9f, m_Preset.ColorFilter.g * 0.97f, m_Preset.ColorFilter.b * 1.0f);
            m_Color.colorFilter.value = Color.Lerp(cold, new Color(0.75f, 0.85f, 0.85f), m_Drain * 0.4f);
            if (m_Grain != null && m_Grain.active)
            {
                m_Grain.intensity.overrideState = true;
                m_Grain.intensity.value = Game.Mode == GameMode.VR ? 0.12f : 0.32f + m_Drain * 0.3f;
                m_Grain.response.overrideState = true;
                m_Grain.response.value = 0.75f;
            }
            m_Bloom.intensity.overrideState = true;
            m_Bloom.intensity.value = m_Preset.Bloom + m_Flash * 1.5f;
            m_Vignette.intensity.overrideState = true;
            m_Vignette.intensity.value = Mathf.Clamp01(m_Preset.Vignette + SpookyVignette + m_Drain * 0.25f + m_Contamination * 0.15f);
            m_Vignette.color.overrideState = true;
            m_Vignette.color.value = Color.Lerp(Color.Lerp(Color.black, new Color(0.12f, 0f, 0.01f), m_Drain), new Color(0.05f, 0.12f, 0.11f), m_Contamination);
            if (m_Chroma.active)
            {
                m_Chroma.intensity.overrideState = true;
                m_Chroma.intensity.value = Game.Mode == GameMode.VR ? 0f : m_Contamination * 0.35f;
            }
            if (m_Shift > 0f)
            {
                m_Color.saturation.value -= m_Shift * 30f;
                m_Color.contrast.value += m_Shift * 22f;
                m_Color.colorFilter.value = Color.Lerp(m_Color.colorFilter.value, Color.Lerp(Color.white, m_ShiftLight, 0.6f), m_ShiftTint * 0.7f);
                m_Vignette.intensity.value = Mathf.Clamp01(m_Vignette.intensity.value + m_Shift * 0.3f);
                m_Vignette.color.value = Color.Lerp(m_Vignette.color.value, new Color(m_ShiftLight.r * 0.12f, m_ShiftLight.g * 0.12f, m_ShiftLight.b * 0.12f), m_Shift);
                if (m_Chroma.active && Game.Mode != GameMode.VR) m_Chroma.intensity.value = Mathf.Max(m_Chroma.intensity.value, m_Shift * 0.85f);
            }
            if (m_Inhale > 0f)
            {
                m_Color.postExposure.value -= m_Inhale * 1.3f;
                if (Game.Mode != GameMode.VR) m_Vignette.intensity.value = Mathf.Clamp01(m_Vignette.intensity.value + m_Inhale * 0.12f);
            }
        }
    }

    /// <summary>Graphics presets. Never disables puzzle-critical lights, clues or enemy warnings.</summary>
    public static class GraphicsQuality
    {
        public static void Apply(GameSettings s)
        {
            var urp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            bool vr = Game.Mode == GameMode.VR;
            int p = Mathf.Clamp(s.graphicsPreset, 0, 3);
            if (urp != null)
            {
                // Low / Medium / High / Ultra. Puzzle lights, clues and warnings are never removed by any preset.
                urp.shadowDistance = new[] { 14f, 22f, 32f, 45f }[p];
                urp.shadowCascadeCount = new[] { 1, 2, 2, 4 }[p];
                urp.mainLightShadowmapResolution = new[] { 1024, 2048, 2048, 4096 }[p];
                urp.additionalLightsShadowmapResolution = new[] { 512, 1024, 2048, 4096 }[p];
                urp.msaaSampleCount = vr ? (p == 0 ? 2 : 4) : new[] { 2, 4, 4, 8 }[p];
                if (vr)
                {
                    urp.renderScale = 1f;
                    UnityEngine.XR.XRSettings.eyeTextureResolutionScale = new[] { 0.85f, 1f, 1.15f, 1.3f }[p];
                }
                else urp.renderScale = new[] { 0.8f, 1f, 1f, 1.25f }[p];
#if UNITY_WEBGL && !UNITY_EDITOR
                // Browser build: WebGL2 can't run the FSR upscaler (shader target 4.5; URP skips only that pass), and
                // supersampling or 8x MSAA costs more than a browser tab can spare.
                urp.upscalingFilter = UpscalingFilterSelection.Linear;
                urp.renderScale = Mathf.Min(urp.renderScale, 1f);
                urp.msaaSampleCount = Mathf.Min(urp.msaaSampleCount, 4);
#endif
#if UNITY_WEBGL
                // Browser VR (WebXR): MSAA blacks out the Quest browser's view and HDR costs a mobile GPU too much; the
                // WebXR settings' framebuffer scale sets the resolution. Desktop play in the browser keeps the preset.
                urp.supportsHDR = !vr;
                if (vr) urp.msaaSampleCount = 1;
#endif
            }
            // Ambient occlusion: desktop High/Ultra, VR Ultra only (it is costly in stereo); never in browser VR.
            bool ao = vr ? p >= 3 && Application.platform != RuntimePlatform.WebGLPlayer : p >= 2;
            foreach (var f in Resources.FindObjectsOfTypeAll<ScreenSpaceAmbientOcclusion>()) f.SetActive(ao);
            // The browser owns the page size and fullscreen (which it only grants on a click), so the web build never sets them.
            if (Game.Mode == GameMode.Desktop && !Game.IsAutoTest && Application.platform != RuntimePlatform.WebGLPlayer)
            {
                QualitySettings.vSyncCount = 1;
                var mode = s.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                var res = Screen.resolutions;
                if (s.resolutionIndex >= 0 && s.resolutionIndex < res.Length)
                    Screen.SetResolution(res[s.resolutionIndex].width, res[s.resolutionIndex].height, mode);
                else if (Screen.fullScreenMode != mode)
                {
                    if (s.fullscreen) Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, mode);
                    else Screen.SetResolution(1600, 900, mode);
                }
            }
            else QualitySettings.vSyncCount = 0;
        }
    }
}

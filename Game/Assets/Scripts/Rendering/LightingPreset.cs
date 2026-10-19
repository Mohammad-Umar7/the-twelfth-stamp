using UnityEngine;
using UnityEngine.Rendering;

namespace TwelfthStamp
{
    /// <summary>Per-cell lighting, sky, fog and grading. Applied globally when a cell becomes the active world, and
    /// baked into PocketLit materials when the cell is seen through a tear.</summary>
    public class LightingPreset
    {
        public Color SunColor = new Color(1f, 0.95f, 0.88f);
        public float SunIntensity = 1.2f;
        public Vector3 SunEuler = new Vector3(45f, 30f, 0f);
        public float ShadowStrength = 0.85f;

        public Color AmbientSky = new Color(0.45f, 0.5f, 0.58f);
        public Color AmbientEquator = new Color(0.35f, 0.34f, 0.32f);
        public Color AmbientGround = new Color(0.2f, 0.18f, 0.16f);
        public float AmbientIntensity = 1f;

        public Color FogColor = new Color(0.6f, 0.62f, 0.66f);
        public float FogDensity = 0.02f;

        public Color SkyZenith = new Color(0.22f, 0.32f, 0.5f);
        public Color SkyHorizon = new Color(0.7f, 0.68f, 0.64f);
        public Color SkyGround = new Color(0.22f, 0.2f, 0.18f);
        public Color SunDiscColor = new Color(1f, 0.9f, 0.7f, 1f);
        public float SunSize = 0.004f;
        public Color CloudColor = new Color(1, 1, 1, 0f);
        public float Stars = 0f;
        public float SkyExposure = 1f;

        public float PostExposure = 0f;
        public float Saturation = 0f;
        public float Contrast = 8f;
        public Color ColorFilter = Color.white;
        public float Bloom = 0.5f;
        public float Vignette = 0.22f;

        /// <summary>Background colour behind everything (camera clear) — matches the fog at the horizon.</summary>
        public Color ClearColor => FogColor;

        public Vector3 DirectionToSun => -(Quaternion.Euler(SunEuler) * Vector3.forward);

        public void ApplyGlobal(Light sun)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky * AmbientIntensity;
            RenderSettings.ambientEquatorColor = AmbientEquator * AmbientIntensity;
            RenderSettings.ambientGroundColor = AmbientGround * AmbientIntensity;
            RenderSettings.fog = FogDensity > 0f;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            // A touch more fog everywhere, and a colder, dimmer fill: the spooky grade (see PostFx).
            RenderSettings.fogColor = Color.Lerp(FogColor, new Color(0.1f, 0.12f, 0.14f), 0.2f);
            RenderSettings.fogDensity = FogDensity * 1.3f;
            RenderSettings.skybox = null;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(SunEuler);
                sun.color = SunColor;
                sun.intensity = SunIntensity;
                sun.shadowStrength = ShadowStrength;
                RenderSettings.sun = sun;
            }
            DynamicGI.UpdateEnvironment();
        }

        public void ConfigureSky(Material sky)
        {
            sky.SetColor("_ZenithColor", SkyZenith);
            sky.SetColor("_HorizonColor", SkyHorizon);
            sky.SetColor("_GroundColor", SkyGround);
            sky.SetVector("_SunDir", DirectionToSun);
            sky.SetColor("_SunColor", SunDiscColor);
            sky.SetFloat("_SunSize", SunSize);
            sky.SetColor("_CloudColor", CloudColor);
            sky.SetFloat("_StarAmount", Stars);
            sky.SetFloat("_Exposure", SkyExposure);
        }

        public void ConfigurePocket(Material m)
        {
            m.SetVector("_SunDir", DirectionToSun);
            m.SetColor("_SunColor", SunColor * SunIntensity * 0.85f);
            m.SetColor("_SkyAmb", AmbientSky * AmbientIntensity * 1.1f);
            m.SetColor("_GroundAmb", AmbientGround * AmbientIntensity * 1.1f);
            m.SetColor("_FogColor", FogColor);
            m.SetFloat("_FogDensity", Mathf.Sqrt(Mathf.Max(0f, FogDensity)) * 0.9f);
        }

        public LightingPreset Clone() => (LightingPreset)MemberwiseClone();
    }
}

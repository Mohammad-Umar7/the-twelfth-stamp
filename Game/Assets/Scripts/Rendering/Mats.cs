using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TwelfthStamp
{
    /// <summary>
    /// Material factory. Clones template materials from Resources/Materials (so the needed shader variants are in
    /// the build) and caches instances by description. Also produces "pocket" variants of world materials for
    /// cells seen through a tear.
    /// </summary>
    public static class Mats
    {
        static readonly Dictionary<string, Material> s_Templates = new Dictionary<string, Material>();
        static readonly Dictionary<string, Material> s_Cache = new Dictionary<string, Material>();
        static readonly Dictionary<(Material, LightingPreset), Material> s_Pocket = new Dictionary<(Material, LightingPreset), Material>();

        public const float StencilAlways = 8f, StencilEqual = 3f, StencilNotEqual = 6f;
        public const int PocketQueue = 2460, PocketSkyQueue = 2470, ActiveSkyQueue = 2440;

        public static Material Template(string name)
        {
            if (s_Templates.TryGetValue(name, out var m) && m != null) return m;
            m = Resources.Load<Material>("Materials/" + name);
            if (m == null)
            {
                Debug.LogError("[TS-Mats] Missing material template " + name + " (run ProjectSetup)");
                var fallback = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Hidden/InternalErrorShader");
                m = new Material(fallback);
            }
            s_Templates[name] = m;
            return m;
        }

        static string Key(params object[] parts) => string.Join("|", parts);

        // ------------------------------------------------------------------ lit
        public static Material Lit(Color color, float smoothness = 0.35f, float metallic = 0f, string texture = null,
            bool useNormal = true, Color? emission = null, float normalStrength = 1f)
        {
            var key = Key("lit", color, smoothness, metallic, texture, useNormal, emission, normalStrength);
            if (s_Cache.TryGetValue(key, out var cached)) return cached;

            Texture2D albedo = texture != null ? Tex.Albedo(texture) : null;
            Texture2D normal = texture != null && useNormal ? Tex.Normal(texture) : null;
            bool emissive = emission.HasValue;
            string templ = normal != null ? (emissive ? "Lit_NormalEmissive" : "Lit_Normal") : (emissive ? "Lit_Emissive" : "Lit_Opaque");
            var m = new Material(Template(templ)) { name = "Lit_" + (texture ?? ColorUtility.ToHtmlStringRGB(color)) };
            m.SetColor("_BaseColor", color);
            if (albedo != null) m.SetTexture("_BaseMap", albedo);
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", normalStrength); }
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (emissive) m.SetColor("_EmissionColor", emission.Value);
            s_Cache[key] = m;
            return m;
        }

        public static Material Emissive(Color baseColor, Color emission, float smoothness = 0.3f)
            => Lit(baseColor, smoothness, 0f, null, false, emission);

        public static Material Glass(Color tint, float smoothness = 0.95f, Color? emission = null)
        {
            var key = Key("glass", tint, smoothness, emission);
            if (s_Cache.TryGetValue(key, out var cached)) return cached;
            var m = new Material(Template(emission.HasValue ? "Lit_TransparentEmissive" : "Lit_Transparent")) { name = "Glass" };
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            if (emission.HasValue) m.SetColor("_EmissionColor", emission.Value);
            s_Cache[key] = m;
            return m;
        }

        public static Material Cutout(Texture2D tex, Color color)
        {
            var key = Key("cutout", tex != null ? tex.name : "null", color);
            if (s_Cache.TryGetValue(key, out var cached)) return cached;
            var m = new Material(Template("Lit_Cutout")) { name = "Cutout" };
            m.SetColor("_BaseColor", color);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            s_Cache[key] = m;
            return m;
        }

        /// <summary>Art texture (stamp, page, postcard) on a lit surface.</summary>
        public static Material Printed(Texture2D tex, Color tint, float smoothness = 0.25f, bool cutout = false)
        {
            if (cutout) return Cutout(tex, tint);
            var key = Key("printed", tex != null ? tex.name : "null", tint, smoothness);
            if (s_Cache.TryGetValue(key, out var cached)) return cached;
            var m = new Material(Template("Lit_Opaque")) { name = "Printed_" + (tex != null ? tex.name : "none") };
            m.SetColor("_BaseColor", tint);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Smoothness", smoothness);
            s_Cache[key] = m;
            return m;
        }

        // ------------------------------------------------------------------ unlit / fx
        public static Material Unlit(Color color, Texture tex = null, bool transparent = false, float intensity = 1f)
        {
            var key = Key("unlit", color, tex != null ? tex.name : "null", transparent, intensity);
            if (s_Cache.TryGetValue(key, out var cached)) return cached;
            var m = new Material(Template(transparent ? "TS_UnlitTransparent" : "TS_Unlit")) { name = "Unlit" };
            m.SetColor("_Color", color);
            m.SetTexture("_MainTex", tex != null ? tex : Tex.White);
            m.SetFloat("_Intensity", intensity);
            s_Cache[key] = m;
            return m;
        }

        /// <summary>New (uncached) unlit instance for things that animate their colour.</summary>
        public static Material UnlitInstance(Color color, Texture tex = null, bool transparent = true, float intensity = 1f)
        {
            var m = new Material(Template(transparent ? "TS_UnlitTransparent" : "TS_Unlit")) { name = "UnlitInstance" };
            m.SetColor("_Color", color);
            m.SetTexture("_MainTex", tex != null ? tex : Tex.White);
            m.SetFloat("_Intensity", intensity);
            return m;
        }

        public static Material Particle(Texture tex, bool additive, Color tint, float intensity = 1f, float stencilComp = StencilAlways)
        {
            var key = Key("particle", tex != null ? tex.name : "null", additive, tint, intensity, stencilComp);
            if (s_Cache.TryGetValue(key, out var cached)) return cached;
            var m = new Material(Template(additive ? "TS_ParticleAdd" : "TS_ParticleAlpha")) { name = "Particle" };
            m.SetTexture("_MainTex", tex != null ? tex : Tex.SoftDot);
            m.SetColor("_TintColor", tint);
            m.SetFloat("_Intensity", intensity);
            m.SetFloat("_StencilComp", stencilComp);
            s_Cache[key] = m;
            return m;
        }

        /// <summary>Additive fresnel edge light (TwelfthStamp/Rim) for the Passenger's glass; drawn on a copy of the mesh.</summary>
        public static Material Rim(Color color, float power, float intensity)
        {
            var key = Key("rim", color, power, intensity);
            if (s_Cache.TryGetValue(key, out var cached)) return cached;
            var m = new Material(Template("TS_Rim")) { name = "Rim" };
            m.SetColor("_RimColor", color);
            m.SetFloat("_RimPower", power);
            m.SetFloat("_RimIntensity", intensity);
            s_Cache[key] = m;
            return m;
        }

        public static Material BeamInstance(Color color, float intensity)
        {
            var m = new Material(Template("TS_Beam")) { name = "Beam" };
            m.SetColor("_Color", color);
            m.SetFloat("_Intensity", intensity);
            return m;
        }

        public static Material SkyInstance(LightingPreset p)
        {
            var m = new Material(Template("TS_SkyDome")) { name = "Sky" };
            p.ConfigureSky(m);
            m.SetFloat("_StencilComp", StencilAlways);
            m.renderQueue = ActiveSkyQueue;
            return m;
        }

        /// <summary>Distant backdrop silhouettes: self-lit (PocketLit, stencil Always) so landmarks read clearly in fog.</summary>
        public static Material Backdrop(Color color, LightingPreset p, Color? emission = null)
        {
            var key = Key("backdrop", color, p.GetHashCode(), emission);
            if (s_Cache.TryGetValue(key, out var cached)) return cached;
            var m = new Material(Template("TS_PocketLit")) { name = "Backdrop" };
            m.SetColor("_BaseColor", color);
            m.SetTexture("_BaseMap", Tex.White);
            m.SetColor("_EmissionColor", emission ?? Color.black);
            p.ConfigurePocket(m);
            m.SetFloat("_StencilComp", StencilAlways);
            m.SetFloat("_UsePocketClip", 0f);
            m.renderQueue = 2000;
            s_Cache[key] = m;
            return m;
        }

        static Material s_PocketHidden;

        /// <summary>Draws nothing (stencil Never): stands in for one submesh with no pocket variant, such as a model's glass.</summary>
        public static Material PocketHidden
        {
            get
            {
                if (s_PocketHidden != null) return s_PocketHidden;
                s_PocketHidden = new Material(Template("TS_PocketLit")) { name = "PocketHidden" };
                s_PocketHidden.SetFloat("_StencilComp", (float)CompareFunction.Never);
                s_PocketHidden.renderQueue = PocketQueue;
                return s_PocketHidden;
            }
        }

        public static Material TearMark()
        {
            var m = new Material(Template("TS_TearMark")) { name = "TearMark" };
            m.SetTexture("_MainTex", Tex.TearMask);
            m.SetFloat("_Cutoff", 0.5f);
            m.renderQueue = 2450;
            return m;
        }

        public static Material TearClear(Color fill)
        {
            var m = new Material(Template("TS_TearClear")) { name = "TearClear" };
            m.SetColor("_FillColor", fill);
            m.renderQueue = 2451;
            return m;
        }

        // ------------------------------------------------------------------ pocket variants
        /// <summary>
        /// Returns the material to use when this world material is seen through a tear, or null to hide the renderer.
        /// </summary>
        public static Material PocketVariant(Material src, LightingPreset preset)
        {
            if (src == null) return null;
            if (s_Pocket.TryGetValue((src, preset), out var cached)) return cached;
            Material result = null;
            var shaderName = src.shader != null ? src.shader.name : "";

            if (shaderName == "Universal Render Pipeline/Lit" || shaderName == "Universal Render Pipeline/Unlit")
            {
                bool transparent = src.renderQueue >= 2900 || src.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT");
                if (!transparent)
                {
                    result = new Material(Template("TS_PocketLit")) { name = src.name + "_Pocket" };
                    var tex = src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : null;
                    result.SetTexture("_BaseMap", tex != null ? tex : Tex.White);
                    if (src.HasProperty("_BaseMap"))
                    {
                        result.SetTextureScale("_BaseMap", src.GetTextureScale("_BaseMap"));
                        result.SetTextureOffset("_BaseMap", src.GetTextureOffset("_BaseMap"));
                    }
                    result.SetColor("_BaseColor", src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : Color.white);
                    result.SetColor("_EmissionColor", src.IsKeywordEnabled("_EMISSION") && src.HasProperty("_EmissionColor") ? src.GetColor("_EmissionColor") : Color.black);
                    preset.ConfigurePocket(result);
                    result.SetFloat("_StencilComp", StencilEqual);
                    result.SetFloat("_UsePocketClip", 1f);
                    result.renderQueue = PocketQueue;
                }
            }
            else if (shaderName == "TwelfthStamp/PocketLit")
            {
                result = new Material(src) { name = src.name + "_Pocket" };
                preset.ConfigurePocket(result);
                result.SetFloat("_StencilComp", StencilEqual);
                result.SetFloat("_UsePocketClip", 1f);
                result.renderQueue = PocketQueue;
            }
            else if (shaderName == "TwelfthStamp/SkyDome")
            {
                result = new Material(src) { name = src.name + "_Pocket" };
                result.SetFloat("_StencilComp", StencilEqual);
                result.renderQueue = PocketSkyQueue;
            }
            else if (shaderName == "TwelfthStamp/Particle" || shaderName == "TwelfthStamp/Beam" || shaderName == "TwelfthStamp/Unlit" || shaderName == "TwelfthStamp/Rim")
            {
                result = new Material(src) { name = src.name + "_Pocket" };
                result.SetFloat("_StencilComp", StencilEqual);
                if (result.HasProperty("_UsePocketClip")) result.SetFloat("_UsePocketClip", 1f);
                if (result.renderQueue < 2452) result.renderQueue = PocketQueue + 2;
            }
            // Anything else (TextMeshPro, transparent lit, tear materials) is hidden in pocket view.
            s_Pocket[(src, preset)] = result;
            return result;
        }
    }
}

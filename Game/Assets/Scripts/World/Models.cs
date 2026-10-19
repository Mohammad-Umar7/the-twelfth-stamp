using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TwelfthStamp
{
    /// <summary>
    /// Blender-modelled visuals (Resources/Models, documented in Art/Models/MODELS.md). Every caller keeps its procedural
    /// mesh as the fallback: Spawn returns null when the file is missing. Only the visuals change: gameplay colliders,
    /// sockets and interactables stay on the procedural objects. The models face Unity +Z while most procedural props
    /// face -Z, hence the default 180 degree turn.
    /// </summary>
    public static class Models
    {
        static readonly Dictionary<string, Material> s_Defaults = new Dictionary<string, Material>();
        static readonly Dictionary<(Material, float), Material> s_Tiled = new Dictionary<(Material, float), Material>();
        static readonly Dictionary<Material, Material> s_NoShadow = new Dictionary<Material, Material>();
        static readonly Dictionary<string, Material> s_Baked = new Dictionary<string, Material>();

        public static GameObject Spawn(string name, Transform parent, System.Func<string, Material> slot, Vector3 position = default, float yaw = 180f, float scale = 1f)
            => Spawn(name, parent, slot, position, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);

        /// <summary>
        /// Instantiates the model and swaps its TS_* material slots for the game's own materials (slot returns null for
        /// "use the default"). Imported colliders are removed. Glass and lens slots cast no shadow; renderers made only of
        /// glass or glow cast none at all. Tiling textures are scaled to at least one tile per metre (UvDensity).
        /// </summary>
        public static GameObject Spawn(string name, Transform parent, System.Func<string, Material> slot, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var prefab = Resources.Load<GameObject>("Models/" + name);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.name = "Model_" + name;
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var density = r.GetComponent<UvDensity>();
                var s = r.transform.lossyScale;
                float tiling = density != null ? density.MetresPerUv * (Mathf.Abs(s.x) + Mathf.Abs(s.y) + Mathf.Abs(s.z)) / 3f : 1f;
                var mats = r.sharedMaterials;
                bool onlyLight = mats.Length > 0;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) { onlyLight = false; continue; }
                    string id = SlotOf(mats[i].name);
                    bool clear = id == "TS_Glass" || id == "TS_Lens";
                    var m = Tiled(slot?.Invoke(mats[i].name) ?? Default(id) ?? mats[i], tiling);
                    mats[i] = clear ? NoShadow(m) : m;
                    if (!clear && id != "TS_EmissiveWarm") onlyLight = false;
                }
                r.sharedMaterials = mats;
                if (onlyLight) r.shadowCastingMode = ShadowCastingMode.Off;
            }
            return go;
        }

        /// <summary>A slot map: exact TS_* names first, then the shared defaults.</summary>
        public static System.Func<string, Material> Slots(params (string slot, Material mat)[] map) => name =>
        {
            var id = SlotOf(name);
            foreach (var (s, m) in map) if (s == id) return m;
            return null;
        };

        /// <summary>"TS_FabricDark" from an imported material name (exact, so TS_Fabric never matches TS_FabricDark).</summary>
        public static string SlotOf(string materialName)
        {
            int i = materialName.IndexOf("TS_", System.StringComparison.Ordinal);
            if (i < 0) return materialName;
            int j = i + 3;
            while (j < materialName.Length && char.IsLetter(materialName[j])) j++;
            return materialName.Substring(i, j - i);
        }

        public static Transform Child(GameObject model, string name)
        {
            if (model == null) return null;
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        /// <summary>Drops a procedural piece's mesh but keeps its collider: the model is the visual, the collider stays gameplay.</summary>
        public static void StripVisual(GameObject go)
        {
            var r = go.GetComponent<MeshRenderer>();
            if (r != null) Object.DestroyImmediate(r);
            var f = go.GetComponent<MeshFilter>();
            if (f != null) Object.DestroyImmediate(f);
        }

        /// <summary>
        /// A lit material on a character's own baked albedo and normal map in Resources/Art (laid out on its UVs, so never
        /// tiled). <paramref name="art"/> is the albedo's path under Art, e.g. "Saeed/saeed_skin"; the normal map is the
        /// same name with "_n".
        /// </summary>
        public static Material Baked(string art, float smoothness, float metallic = 0f)
        {
            if (s_Baked.TryGetValue(art, out var cached) && cached != null) return cached;
            var albedo = Tex.Art(art);
            var normal = Tex.Art(art + "_n");
            var m = new Material(Mats.Template(normal != null ? "Lit_Normal" : "Lit_Opaque")) { name = "Baked_" + System.IO.Path.GetFileName(art) };
            m.SetColor("_BaseColor", Color.white);
            if (albedo != null) m.SetTexture("_BaseMap", albedo);
            if (normal != null) m.SetTexture("_BumpMap", normal);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            s_Baked[art] = m;
            return m;
        }

        /// <summary>Saeed's six slots (Saeed.fbx) on his baked textures in Resources/Art/Saeed.</summary>
        public static System.Func<string, Material> SaeedSlots() => name =>
        {
            switch (SlotOf(name))
            {
                case "TS_Skin": return Baked("Saeed/saeed_skin", 0.42f);
                case "TS_Kandura": return Baked("Saeed/saeed_kandura", 0.15f);
                case "TS_Hair": return Baked("Saeed/saeed_hair", 0.22f);
                case "TS_Leather": return Baked("Saeed/saeed_leather", 0.4f);
                case "TS_Suede": return Baked("Saeed/saeed_suede", 0.08f);
                case "TS_Eyes": return Baked("Saeed/saeed_eyes", 0.85f);
            }
            return null;
        };

        /// <summary>The game's materials for each slot when the caller has no scene-specific one.</summary>
        public static Material Default(string slot)
        {
            if (s_Defaults.TryGetValue(slot, out var m)) return m;
            switch (slot)
            {
                case "TS_Wood": m = Mats.Lit(new Color(0.45f, 0.31f, 0.2f), 0.45f, 0f, "wood_dark"); break;
                case "TS_WoodDark": m = Mats.Lit(new Color(0.24f, 0.16f, 0.1f), 0.45f, 0f, "wood_dark"); break;
                case "TS_Fabric": m = Mats.Lit(new Color(0.62f, 0.55f, 0.46f), 0.15f, 0f, "fabric_linen"); break;
                case "TS_FabricDark": m = Mats.Lit(new Color(0.2f, 0.2f, 0.23f), 0.15f, 0f, "fabric_linen"); break;
                case "TS_Leather": m = Mats.Lit(new Color(0.3f, 0.2f, 0.13f), 0.35f, 0f, "leather_navy"); break;
                case "TS_LeatherNavy": m = Mats.Lit(new Color(0.08f, 0.1f, 0.2f), 0.35f, 0f, "leather_navy"); break;
                case "TS_Metal": m = Mats.Lit(new Color(0.7f, 0.7f, 0.72f), 0.7f, 0.9f, "metal_brushed"); break;
                case "TS_MetalDark": m = Mats.Lit(new Color(0.12f, 0.12f, 0.13f), 0.55f, 0.8f); break;
                case "TS_Chrome": m = Mats.Lit(new Color(0.8f, 0.81f, 0.83f), 0.9f, 1f); break;
                case "TS_Brass": m = Mats.Lit(new Color(0.75f, 0.58f, 0.3f), 0.65f, 0.9f, "metal_brushed"); break;
                case "TS_Gold": m = Mats.Lit(new Color(0.85f, 0.66f, 0.3f), 0.75f, 1f); break;
                case "TS_Glass": m = Mats.Glass(new Color(0.55f, 0.62f, 0.65f, 0.25f)); break;
                case "TS_Stone": m = Mats.Lit(new Color(0.7f, 0.68f, 0.64f), 0.4f, 0f, "tile_terrazzo"); break;
                case "TS_Marble": m = Mats.Lit(new Color(0.92f, 0.9f, 0.88f), 0.6f, 0f, "marble_white"); break;
                case "TS_Ceramic": m = Mats.Lit(new Color(0.93f, 0.93f, 0.91f), 0.85f); break;
                case "TS_Plastic": m = Mats.Lit(new Color(0.06f, 0.06f, 0.065f), 0.5f); break;
                case "TS_PlasticWhite": m = Mats.Lit(new Color(0.88f, 0.88f, 0.86f), 0.55f); break;
                case "TS_Rubber": m = Mats.Lit(new Color(0.04f, 0.04f, 0.045f), 0.2f); break;
                case "TS_Paper": m = Mats.Lit(new Color(0.92f, 0.89f, 0.8f), 0.15f, 0f, "paper_ivory"); break;
                case "TS_PaperEdge": m = Mats.Lit(new Color(0.8f, 0.76f, 0.66f), 0.15f); break;
                case "TS_EmissiveWarm": m = Mats.Emissive(new Color(1f, 0.85f, 0.6f), new Color(1.6f, 1.1f, 0.55f)); break;
                default: return null;
            }
            s_Defaults[slot] = m;
            return m;
        }

        /// <summary>
        /// Smart-projected UVs pack a whole mesh into 0..1, so a 4 m kitchen would stretch a texture meant to repeat every
        /// metre. Big meshes get a copy tiled to about one repeat per metre; small props keep the denser default.
        /// </summary>
        static Material Tiled(Material m, float metresPerUv)
        {
            if (metresPerUv <= 1.05f || m == null || !m.HasProperty("_BaseMap")) return m;
            var tex = m.GetTexture("_BaseMap");
            if (tex == null || !(tex.name.EndsWith("_a") || tex.name.StartsWith("TS_Fallback_"))) return m; // art and plain colours stay
            float q = Mathf.Round(metresPerUv * 10f) / 10f;
            if (s_Tiled.TryGetValue((m, q), out var t)) return t;
            t = new Material(m) { name = m.name + "_x" + q.ToString("0.0") };
            t.SetTextureScale("_BaseMap", m.GetTextureScale("_BaseMap") * q);
            s_Tiled[(m, q)] = t;
            return t;
        }

        /// <summary>A copy that skips the shadow pass (glass and lenses cast no shadow, even inside a mixed mesh).</summary>
        static Material NoShadow(Material m)
        {
            if (s_NoShadow.TryGetValue(m, out var n)) return n;
            n = new Material(m) { name = m.name + "_NoShadow" };
            n.SetShaderPassEnabled("ShadowCaster", false);
            s_NoShadow[m] = n;
            return n;
        }
    }
}

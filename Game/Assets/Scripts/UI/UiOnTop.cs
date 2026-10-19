using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace TwelfthStamp
{
    /// <summary>
    /// Menus, cards and the VR HUD always draw in front of the world: nothing in a cell (a claw machine, a wall, a
    /// table) can cut through them when the player stands close to it (playtest: the Paused card sank into the claw
    /// machine). Registered roots are swept a few times a second, so rows and labels added later are covered too; every
    /// renderer and graphic under them stops depth-testing against the scene and moves to a queue above the world but
    /// below the fade (TS_Fade, 4000), keeping the cards' own draw order (sortingOrder).
    /// </summary>
    public class UiOnTop : MonoBehaviour
    {
        public const int BaseQueue = 3600;
        const float Always = (float)CompareFunction.Always;
        static readonly List<Transform> s_Roots = new List<Transform>();
        static readonly int ZTestId = Shader.PropertyToID("_ZTest");
        static readonly int GuiZTestId = Shader.PropertyToID("unity_GUIZTestMode");
        static UiOnTop s_I;
        static Material s_UiAlways;
        float m_Next;

        public static void Register(Transform root)
        {
            if (root == null) return;
            if (!s_Roots.Contains(root)) s_Roots.Add(root);
            if (s_I == null)
            {
                var go = new GameObject("UiOnTop");
                DontDestroyOnLoad(go);
                s_I = go.AddComponent<UiOnTop>();
            }
            Apply(root);
        }

        void LateUpdate()
        {
            if (Time.unscaledTime < m_Next) return;
            m_Next = Time.unscaledTime + 0.2f;
            for (int i = s_Roots.Count - 1; i >= 0; i--)
            {
                if (s_Roots[i] == null) { s_Roots.RemoveAt(i); continue; }
                Apply(s_Roots[i]);
            }
        }

        public static void Apply(Transform root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                var tmp = r.GetComponent<TMP_Text>();
                if (tmp != null)
                {
                    var fm = tmp.fontSharedMaterial;
                    if (fm == null || (fm.HasProperty(GuiZTestId) && fm.GetFloat(GuiZTestId) == Always && fm.renderQueue >= BaseQueue)) continue;
                    var inst = tmp.fontMaterial; // TMP's own per-text instance; the shared font material (world signs) is untouched
                    inst.SetFloat(GuiZTestId, Always);
                    inst.renderQueue = BaseQueue + 20 + Mathf.Clamp(r.sortingOrder, 0, 50);
                    continue;
                }
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || !m.HasProperty(ZTestId)) continue;
                    if (m.GetFloat(ZTestId) == Always && m.renderQueue >= BaseQueue) continue;
                    // Instances made for one element (Mats.UnlitInstance, the cards' hover colours) are edited in place, so
                    // the code holding them keeps working; cached shared materials get a copy of their own.
                    if (m.name != "UnlitInstance" && !m.name.EndsWith("(UiOnTop)")) { m = new Material(m) { name = m.name + " (UiOnTop)" }; mats[i] = m; changed = true; }
                    m.SetFloat(ZTestId, Always);
                    m.renderQueue = BaseQueue + Mathf.Clamp(r.sortingOrder, 0, 50);
                }
                if (changed) r.sharedMaterials = mats;
            }
            // uGUI (the VR HUD's world-space canvas).
            foreach (var g in root.GetComponentsInChildren<Graphic>(true))
            {
                if (g is TMP_Text t)
                {
                    var fm = t.fontSharedMaterial;
                    if (fm == null || (fm.HasProperty(GuiZTestId) && fm.GetFloat(GuiZTestId) == Always)) continue;
                    t.fontMaterial.SetFloat(GuiZTestId, Always);
                    continue;
                }
                if (g.material != null && g.material != g.defaultMaterial) continue; // a custom material: leave it
                if (s_UiAlways == null)
                {
                    s_UiAlways = new Material(g.defaultMaterial) { name = "UI (UiOnTop)" };
                    s_UiAlways.SetFloat(GuiZTestId, Always);
                }
                g.material = s_UiAlways;
            }
        }
    }
}

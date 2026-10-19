using TMPro;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// One coloured level tile on the welcome card: the level's numeral, its difficulty and how many countries it
    /// holds, a tick once finished. Hovering lifts and brightens it; a slow sheen keeps the card alive while it waits.
    /// </summary>
    public class LevelTile : Button3D
    {
        public LevelDef Def;
        Material m_Fill, m_Sheen;
        Transform m_Body, m_SheenT;
        Color m_Color;
        float m_Lift, m_Phase, m_Width;

        public static LevelTile Create(Transform parent, LevelDef def, bool completed, Vector3 localPos, float w, float h)
        {
            var go = new GameObject("Button_" + def.Label);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.layer = Layers.UI3D;
            var body = new GameObject("Body").transform;
            body.SetParent(go.transform, false);

            var t = go.AddComponent<LevelTile>();
            t.Def = def;
            t.m_Body = body;
            t.m_Width = w;
            t.m_Color = def.Accent;
            t.m_Phase = (int)def.Id * 1.7f;

            // Soft drop shadow, the coloured face, a darker band at the bottom and a moving sheen.
            Quad("Shadow", body, w + 0.008f, h + 0.008f, Mats.Unlit(new Color(0f, 0f, 0f, 0.22f), Tex.SoftDot, true), new Vector3(0.003f, -0.004f, 0.0012f), 1);
            t.m_Fill = Mats.UnlitInstance(def.Accent, null, true, 1f);
            Quad("Face", body, w, h, t.m_Fill, new Vector3(0, 0, 0.001f), 2);
            var band = Color.Lerp(def.Accent, Color.black, 0.28f);
            Quad("Band", body, w, h * 0.3f, Mats.Unlit(new Color(band.r, band.g, band.b, 0.9f), null, true), new Vector3(0, -h * 0.35f, 0.0008f), 3);
            t.m_Sheen = Mats.UnlitInstance(new Color(1f, 1f, 1f, 0.16f), Tex.SoftDot, true, 1f);
            var sheen = Quad("Sheen", body, w * 0.35f, h * 1.15f, t.m_Sheen, new Vector3(0, 0, 0.0006f), 3);
            sheen.transform.localRotation = Quaternion.Euler(0, 0, -18f);
            t.m_SheenT = sheen.transform;

            string numeral = def.Id == LevelId.FreeRoam ? "∞" : ((int)def.Id).ToString();
            var num = Text(body, numeral, 0.075f, Color.white, TextAlignmentOptions.Center, true, w * 0.3f, new Vector3(-w * 0.33f, h * 0.08f, -0.001f));
            num.fontStyle = FontStyles.Bold;
            var title = Text(body, def.Difficulty.ToUpperInvariant(), 0.03f, Color.white, TextAlignmentOptions.Left, false, w * 0.62f, new Vector3(w * 0.14f, h * 0.22f, -0.001f));
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 3f;
            string sub = def.Id == LevelId.FreeRoam ? "All 11 · no threat" : def.Countries.Length + (def.Countries.Length == 1 ? " country" : " countries");
            Text(body, sub, 0.017f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Left, false, w * 0.62f, new Vector3(w * 0.14f, -h * 0.07f, -0.001f));
            Text(body, def.Number.ToUpperInvariant(), 0.015f, new Color(1f, 1f, 1f, 0.9f), TextAlignmentOptions.Center, false, w, new Vector3(0, -h * 0.35f, -0.001f)).characterSpacing = 4f;
            if (completed)
                Text(body, "✓", 0.03f, Color.white, TextAlignmentOptions.Right, false, w * 0.3f, new Vector3(w * 0.33f, h * 0.3f, -0.001f));

            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(w, h, 0.01f);
            t.Label = title;
            t.Init(t.m_Fill, def.Accent, Color.Lerp(def.Accent, Color.white, 0.18f), Color.white, Color.white);
            return t;
        }

        static GameObject Quad(string name, Transform parent, float w, float h, Material m, Vector3 pos, int order)
        {
            var q = MeshFactory.Quad(name, parent, w, h, m, pos);
            q.layer = Layers.UI3D;
            foreach (var r in q.GetComponentsInChildren<Renderer>(true)) r.sortingOrder = order;
            return q;
        }

        static TextMeshPro Text(Transform parent, string s, float size, Color c, TextAlignmentOptions a, bool serif, float w, Vector3 pos)
        {
            var t = TextFactory.World(parent, s, size, c, a, serif, w);
            t.transform.localPosition = pos;
            t.sortingOrder = 5;
            t.gameObject.layer = Layers.UI3D;
            return t;
        }

        public override void SetHover(bool on)
        {
            base.SetHover(on);
            if (on && Interactive) AudioDirector.Haptic(HandSide.Right, 0.08f, 0.02f);
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            m_Lift = Mathf.MoveTowards(m_Lift, Hovered && Interactive ? 1f : 0f, dt * 6f);
            float e = m_Lift * m_Lift * (3f - 2f * m_Lift);
            m_Body.localScale = Vector3.one * (1f + 0.045f * e);
            m_Body.localPosition = new Vector3(0f, 0.004f * e, -0.004f * e);
            // A sheen sweeps across every few seconds (faster while hovered).
            float speed = Hovered ? 0.9f : 0.22f;
            float p = Mathf.Repeat(Time.unscaledTime * speed + m_Phase, 1.6f) - 0.3f;
            if (m_SheenT != null) m_SheenT.localPosition = new Vector3(Mathf.Lerp(-m_Width * 0.6f, m_Width * 0.6f, p), 0f, 0.0006f);
            if (m_Sheen != null) m_Sheen.SetColor("_Color", new Color(1f, 1f, 1f, (p > 0f && p < 1f ? Mathf.Sin(p * Mathf.PI) : 0f) * (0.12f + 0.12f * e)));
        }
    }

    /// <summary>Pops a menu row into place: rises from just below, overshoots a touch and settles (unscaled time, so it
    /// also plays on the pause and results cards).</summary>
    public class UiReveal : MonoBehaviour
    {
        Vector3 m_Target;
        float m_Start, m_Delay;
        bool m_Running;
        const float Duration = 0.42f;

        public void Begin(float delay)
        {
            m_Target = transform.localPosition;
            m_Delay = delay;
            m_Start = Time.unscaledTime;
            m_Running = true;
            Apply(0f);
        }

        void Update()
        {
            if (!m_Running) return;
            float t = (Time.unscaledTime - m_Start - m_Delay) / Duration;
            if (t >= 1f) { Apply(1f); m_Running = false; return; }
            Apply(Mathf.Clamp01(t));
        }

        void Apply(float t)
        {
            // Ease-out-back: a small overshoot reads as a pop rather than a slide.
            const float c1 = 1.5f, c3 = c1 + 1f;
            float e = t <= 0f ? 0f : 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
            transform.localPosition = m_Target + new Vector3(0f, -0.03f * (1f - e), 0f);
            transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.001f, 1f, e);
        }
    }
}

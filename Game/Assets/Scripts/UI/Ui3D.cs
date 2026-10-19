using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>Base for diegetic 3D UI elements (UI3D layer). Works with the desktop cursor and VR lasers.</summary>
    public abstract class Ui3DElement : MonoBehaviour
    {
        public bool Interactive = true;
        public bool Hovered { get; private set; }
        public virtual void SetHover(bool on) { Hovered = on; }
        public virtual void Press(Vector3 hit, HandSide hand) { }
        public virtual void Drag(Vector3 hit, HandSide hand) { }
        public virtual void Release(Vector3 hit, HandSide hand) { }
    }

    public class Button3D : Ui3DElement
    {
        public Action OnClick;
        public TextMeshPro Label;
        public TextMeshPro Value;          // optional right-aligned value (toggles, choices)
        public GameObject Marker;          // small pointer shown while hovered
        public float HoldSeconds;          // > 0: hold-to-confirm (e.g. Remove the glasses)
        Material m_Bg;
        Color m_Base, m_Hover, m_LabelBase, m_LabelHover;
        float m_Held;
        bool m_Pressing;

        public void Init(Material bg, Color baseColor, Color hover) => Init(bg, baseColor, hover, Color.clear, Color.clear);

        public void Init(Material bg, Color baseColor, Color hover, Color labelBase, Color labelHover)
        {
            m_Bg = bg; m_Base = baseColor; m_Hover = hover;
            m_LabelBase = labelBase; m_LabelHover = labelHover;
            m_Bg.SetColor("_Color", m_Base);
            if (Marker != null) Marker.SetActive(false);
        }

        public override void SetHover(bool on)
        {
            if (on && !Hovered && Interactive) AudioDirector.I?.PlayUI("ui_hover", 0.5f);
            base.SetHover(on);
            bool lit = on && Interactive;
            if (m_Bg != null) m_Bg.SetColor("_Color", lit ? m_Hover : m_Base);
            if (Label != null && m_LabelBase.a > 0f) Label.color = lit ? m_LabelHover : m_LabelBase;
            if (Marker != null) Marker.SetActive(lit);
            if (!on) { m_Pressing = false; m_Held = 0f; }
        }

        public override void Press(Vector3 hit, HandSide hand)
        {
            if (!Interactive) return;
            if (HoldSeconds > 0f) { m_Pressing = true; m_Held = 0f; return; }
            Click(hand);
        }

        public override void Release(Vector3 hit, HandSide hand) { m_Pressing = false; m_Held = 0f; }

        void Update()
        {
            if (!m_Pressing || HoldSeconds <= 0f) return;
            m_Held += Time.unscaledDeltaTime;
            if (m_Bg != null) m_Bg.SetColor("_Color", Color.Lerp(m_Hover, new Color(0.55f, 0.1f, 0.08f, 0.55f), m_Held / HoldSeconds));
            if (m_Held >= HoldSeconds) { m_Pressing = false; m_Held = 0f; Click(HandSide.Desktop); }
        }

        void Click(HandSide hand)
        {
            AudioDirector.I?.PlayUI("ui_click", 0.8f);
            AudioDirector.Haptic(hand, 0.25f, 0.04f);
            OnClick?.Invoke();
        }
    }

    public class Slider3D : Ui3DElement
    {
        public float Value;
        public Action<float> OnChanged;
        public Transform Track, Knob;
        public TextMeshPro ValueText;
        public Func<float, string> Format = v => Mathf.RoundToInt(v * 100) + "%";
        public float Width = 0.36f;
        float m_LastTick;

        public void SetValue(float v, bool notify)
        {
            Value = Mathf.Clamp01(v);
            if (Knob != null) Knob.localPosition = new Vector3(-Width / 2 + Width * Value, 0, -0.004f);
            if (ValueText != null) ValueText.text = Format(Value);
            if (notify) OnChanged?.Invoke(Value);
        }

        public override void Press(Vector3 hit, HandSide hand) => Drag(hit, hand);

        public override void Drag(Vector3 hit, HandSide hand)
        {
            if (!Interactive) return;
            var local = Track.InverseTransformPoint(hit);
            float v = Mathf.Clamp01((local.x + Width / 2) / Width);
            if (Mathf.Abs(v - m_LastTick) > 0.05f)
            {
                m_LastTick = v;
                AudioDirector.I?.PlayUI("ui_slider_tick", 0.4f);
                AudioDirector.Haptic(hand, 0.1f, 0.02f);
            }
            SetValue(v, true);
        }
    }

    /// <summary>Raycasts rig pointers against the UI3D layer and dispatches hover / press / drag / release.</summary>
    public class UiPointer : MonoBehaviour
    {
        public static UiPointer I { get; private set; }
        readonly List<PointerState> m_Pointers = new List<PointerState>();
        readonly Dictionary<HandSide, Ui3DElement> m_Hover = new Dictionary<HandSide, Ui3DElement>();
        readonly Dictionary<HandSide, Ui3DElement> m_Pressed = new Dictionary<HandSide, Ui3DElement>();
        Transform m_Dot;

        public static UiPointer Create(Transform parent)
        {
            var go = new GameObject("UiPointer");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<UiPointer>();
            var dot = MeshFactory.Sphere("PointerDot", go.transform, Vector3.zero, 0.006f, Mats.Unlit(new Color(0.85f, 1f, 0.92f), null, false, 1.5f));
            dot.layer = Layers.IgnoreRaycast;
            I.m_Dot = dot.transform;
            dot.SetActive(false);
            return I;
        }

        void Update()
        {
            var rig = Game.Rig;
            if (rig == null) return;
            m_Pointers.Clear();
            rig.GetPointers(m_Pointers);
            bool any = false;
            foreach (var p in m_Pointers)
            {
                Ui3DElement hit = null;
                Vector3 point = p.Ray.origin + p.Ray.direction * 2f;
                if (Physics.Raycast(p.Ray, out var h, 12f, Layers.UIMask, QueryTriggerInteraction.Collide))
                {
                    hit = h.collider.GetComponentInParent<Ui3DElement>();
                    point = h.point;
                    if (hit != null && Game.Mode == GameMode.VR && p.Hand != HandSide.Desktop) { m_Dot.position = point; any = true; }
                }
                m_Hover.TryGetValue(p.Hand, out var prev);
                if (prev != hit)
                {
                    if (prev != null) prev.SetHover(false);
                    if (hit != null) hit.SetHover(true);
                    m_Hover[p.Hand] = hit;
                }
                if (p.Down && hit != null && hit.Interactive) { m_Pressed[p.Hand] = hit; hit.Press(point, p.Hand); }
                if (p.Held && m_Pressed.TryGetValue(p.Hand, out var dragging) && dragging != null)
                {
                    // Keep dragging along the element's plane even if the ray slides off.
                    var plane = new Plane(dragging.transform.forward, dragging.transform.position);
                    if (plane.Raycast(p.Ray, out float enter)) dragging.Drag(p.Ray.GetPoint(enter), p.Hand);
                }
                if (p.Up && m_Pressed.TryGetValue(p.Hand, out var released) && released != null)
                {
                    released.Release(point, p.Hand);
                    m_Pressed[p.Hand] = null;
                }
            }
            m_Dot.gameObject.SetActive(any);
        }

        public void ClearHover()
        {
            foreach (var kv in m_Hover) if (kv.Value != null) kv.Value.SetHover(false);
            m_Hover.Clear();
            m_Pressed.Clear();
        }
    }

    /// <summary>A paper card panel with a title and rows of controls, built in world space.</summary>
    public class Panel3D : MonoBehaviour
    {
        public const float Width = 0.62f;
        public float RowHeight = 0.058f;
        float m_Cursor;
        Transform m_Content;
        TextMeshPro m_Title;
        GameObject m_Bg, m_Rule, m_Icon;
        readonly List<GameObject> m_Rows = new List<GameObject>();

        /// <summary>Card height in metres after the last layout (the card is centred on the panel's origin).</summary>
        public float CardHeight { get; private set; } = 0.9f;
        public const float CardWidth = Width + 0.08f;

        static readonly Color Ink = new Color(0.16f, 0.14f, 0.15f);
        static readonly Color InkFaint = new Color(0.16f, 0.14f, 0.15f, 0.38f);
        static readonly Color Red = new Color(0.5f, 0.09f, 0.08f);
        static readonly Color Paper = new Color(0.93f, 0.9f, 0.82f, 0.97f);
        // Explicit draw order so transparent pieces never sort behind the card (distance sorting flips them).
        const int OrderCard = 0, OrderWash = 1, OrderLine = 2, OrderText = 4;

        static void Order(GameObject go, int order)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.sortingOrder = order;
        }

        public static Panel3D Create(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = Layers.UI3D;
            var p = go.AddComponent<Panel3D>();
            p.Build();
            UiOnTop.Register(go.transform); // cards always draw in front of the world (UI/UiOnTop.cs)
            return p;
        }

        void Build()
        {
            var tex = Tex.Art("ui_panel_paper");
            m_Bg = MeshFactory.Quad("Card", transform, Width + 0.08f, 0.9f, Mats.UnlitInstance(tex != null ? Color.white : Paper, tex, true, 1f), new Vector3(0, 0, 0.004f));
            m_Bg.layer = Layers.UI3D;
            Order(m_Bg, OrderCard);
            m_Content = new GameObject("Content").transform;
            m_Content.SetParent(transform, false);
            m_Title = TextFactory.World(m_Content, "", 0.04f, Red, TextAlignmentOptions.Center, true, Width);
            m_Title.characterSpacing = 4f;
            m_Title.transform.localRotation = Quaternion.identity;
            m_Title.sortingOrder = OrderText;
            // A short ink rule under the title.
            m_Rule = MeshFactory.Quad("TitleRule", m_Title.transform, 0.16f, 0.0016f, Mats.Unlit(new Color(0.5f, 0.09f, 0.08f, 0.55f), null, true), new Vector3(0, -0.034f, 0));
            m_Rule.layer = Layers.UI3D;
            Order(m_Rule, OrderLine);
            // Small key-art mark above the title on every card except the welcome screen (Art/Branding/BRANDING.md).
            var thumb = Branding.Thumb;
            if (thumb != null)
            {
                m_Icon = MeshFactory.Quad("HeaderIcon", m_Content, 0.05f, 0.05f, Mats.Unlit(Color.white, thumb, true), Vector3.zero);
                m_Icon.layer = Layers.UI3D;
                Order(m_Icon, OrderText);
                m_Icon.SetActive(false);
            }
        }

        /// <summary>Starts a page. An empty title hides the title row (the welcome screen shows the logo instead);
        /// <paramref name="icon"/> puts the small key-art mark above the title.</summary>
        public void Clear(string title, bool icon = true)
        {
            foreach (var r in m_Rows) Destroy(r);
            m_Rows.Clear();
            bool hasTitle = !string.IsNullOrEmpty(title);
            m_Title.text = title ?? "";
            m_Title.gameObject.SetActive(hasTitle);
            m_Cursor = 0f;
            bool showIcon = icon && m_Icon != null && hasTitle;
            if (m_Icon != null)
            {
                m_Icon.SetActive(showIcon);
                m_Icon.transform.localPosition = new Vector3(0f, 0.012f, -0.001f);
            }
            if (showIcon) m_Cursor -= 0.062f;
            m_Title.transform.localPosition = new Vector3(0, m_Cursor, 0);
            m_Cursor -= hasTitle ? 0.07f : 0.01f;
        }

        public void FinishLayout()
        {
            // Content runs from the title centre (0) down to the cursor. The torn paper edge eats about 3 cm, so pad
            // the top and bottom generously and centre the whole card on the panel origin.
            const float top = 0.09f, bottom = 0.045f;
            float h = -m_Cursor + top + bottom;
            m_Content.localPosition = new Vector3(0, h / 2 - top, 0);
            m_Bg.transform.localScale = new Vector3(1f, h / 0.9f, 1f);
            CardHeight = h;
        }

        GameObject Row(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(m_Content, false);
            go.transform.localPosition = new Vector3(0, m_Cursor, 0);
            go.layer = Layers.UI3D;
            m_Rows.Add(go);
            m_Cursor -= RowHeight;
            return go;
        }

        public TextMeshPro Text(string text, float size = 0.0034f, TextAlignmentOptions align = TextAlignmentOptions.Center, float extraHeight = 0f, Color? color = null, bool serif = false)
        {
            var row = Row("Text");
            m_Cursor -= extraHeight;
            var t = TextFactory.World(row.transform, text, size * 6.5f, color ?? Ink, align, serif, Width - 0.06f);
            t.transform.localPosition = new Vector3(0, -extraHeight / 2, 0);
            t.rectTransform.sizeDelta = new Vector2(Width - 0.06f, RowHeight + extraHeight);
            t.sortingOrder = OrderText;
            return t;
        }

        public Button3D Button(string label, Action onClick, bool enabled = true, float holdSeconds = 0f)
        {
            var row = Row("Button_" + label);
            // Ink on paper: no dark bars. Hover lays a soft red wash, reddens the label and shows a pointer.
            var bgMat = Mats.UnlitInstance(new Color(0.5f, 0.09f, 0.08f, 0f), null, true, 1f);
            var bg = MeshFactory.Quad("Bg", row.transform, Width - 0.05f, RowHeight - 0.008f, bgMat, new Vector3(0, 0, 0.001f));
            bg.layer = Layers.UI3D;
            Order(bg, OrderWash);
            var line = MeshFactory.Quad("Hairline", row.transform, Width - 0.09f, 0.0012f, Mats.Unlit(new Color(0.16f, 0.14f, 0.15f, 0.14f), null, true), new Vector3(0, -RowHeight / 2 + 0.002f, 0.0008f));
            line.layer = Layers.UI3D;
            Order(line, OrderLine);
            var col = row.AddComponent<BoxCollider>();
            col.size = new Vector3(Width - 0.05f, RowHeight - 0.006f, 0.01f);
            var b = row.AddComponent<Button3D>();
            var labelColor = enabled ? Ink : InkFaint;
            b.Label = TextFactory.World(row.transform, label, 0.024f, labelColor, TextAlignmentOptions.Center, false, Width - 0.1f);
            b.Label.transform.localPosition = new Vector3(0, 0, -0.002f);
            b.Label.sortingOrder = OrderText;
            var marker = TextFactory.World(row.transform, "▶", 0.012f, Red, TextAlignmentOptions.Center, false, 0.03f);
            marker.transform.localPosition = new Vector3(-Width / 2 + 0.05f, 0, -0.002f);
            marker.sortingOrder = OrderText;
            b.Marker = marker.gameObject;
            b.Init(bgMat, new Color(0.5f, 0.09f, 0.08f, 0f), new Color(0.5f, 0.09f, 0.08f, 0.1f), labelColor, enabled ? Red : labelColor);
            b.Interactive = enabled;
            b.OnClick = onClick;
            b.HoldSeconds = holdSeconds;
            return b;
        }

        public Slider3D Slider(string label, float value, Action<float> onChanged, Func<float, string> format = null)
        {
            var row = Row("Slider_" + label);
            var t = TextFactory.World(row.transform, label, 0.018f, Ink, TextAlignmentOptions.Left, false, 0.2f);
            t.transform.localPosition = new Vector3(-Width / 2 + 0.12f, 0, 0);
            t.sortingOrder = OrderText;
            var track = new GameObject("Track").transform;
            track.SetParent(row.transform, false);
            track.localPosition = new Vector3(0.07f, 0, 0);
            var s = row.AddComponent<Slider3D>();
            s.Width = 0.3f;
            var lineGo = MeshFactory.Quad("Line", track, s.Width, 0.003f, Mats.Unlit(new Color(0.16f, 0.14f, 0.15f, 0.45f), null, true), Vector3.zero);
            lineGo.layer = Layers.UI3D;
            Order(lineGo, OrderLine);
            var knob = MeshFactory.Quad("Knob", track, 0.014f, 0.03f, Mats.Unlit(new Color(0.5f, 0.09f, 0.08f), null, false), Vector3.zero);
            knob.layer = Layers.UI3D;
            Order(knob, OrderText);
            var col = row.AddComponent<BoxCollider>();
            col.center = new Vector3(0.07f, 0, 0);
            col.size = new Vector3(s.Width + 0.04f, RowHeight - 0.01f, 0.01f);
            s.Track = track;
            s.Knob = knob.transform;
            s.ValueText = TextFactory.World(row.transform, "", 0.017f, Red, TextAlignmentOptions.Right, false, 0.1f);
            s.ValueText.transform.localPosition = new Vector3(Width / 2 - 0.05f, 0, 0);
            s.ValueText.sortingOrder = OrderText;
            if (format != null) s.Format = format;
            s.SetValue(value, false);
            s.OnChanged = onChanged;
            return s;
        }

        public Button3D Toggle(string label, bool value, Action<bool> onChanged)
        {
            Button3D b = null;
            bool v = value;
            b = Button(label, () =>
            {
                v = !v;
                b.Value.text = v ? "On" : "Off";
                onChanged(v);
            });
            AddValue(b, v ? "On" : "Off");
            return b;
        }

        public Button3D Choice(string label, string[] options, int index, Action<int> onChanged)
        {
            Button3D b = null;
            int i = Mathf.Clamp(index, 0, options.Length - 1);
            b = Button(label, () =>
            {
                i = (i + 1) % options.Length;
                b.Value.text = options[i] + "  ›";
                onChanged(i);
            });
            AddValue(b, options[i] + "  ›");
            return b;
        }

        void AddValue(Button3D b, string value)
        {
            b.Label.alignment = TextAlignmentOptions.Left;
            b.Label.rectTransform.sizeDelta = new Vector2(Width - 0.2f, b.Label.rectTransform.sizeDelta.y);
            b.Label.transform.localPosition = new Vector3(-0.05f, 0, -0.002f);
            b.Value = TextFactory.World(b.transform, value, 0.019f, Red, TextAlignmentOptions.Right, false, 0.24f);
            b.Value.transform.localPosition = new Vector3(Width / 2 - 0.16f, 0, -0.002f);
            b.Value.sortingOrder = OrderText;
        }

        public void Space(float h = 0.02f) => m_Cursor -= h;

        /// <summary>
        /// The level list as a grid of coloured tiles, two to a row (green easy, amber medium, red hard, blue free roam).
        /// Each tile is a button named "Button_" + its label, like every other button.
        /// </summary>
        public List<LevelTile> LevelGrid(IList<LevelDef> levels, Func<LevelDef, bool> completed, Action<LevelDef> onClick)
        {
            const float tileH = 0.105f, gap = 0.012f;
            float tileW = (Width - 0.05f - gap) * 0.5f;
            var tiles = new List<LevelTile>();
            for (int i = 0; i < levels.Count; i += 2)
            {
                var row = Row("LevelRow");
                m_Cursor -= tileH + gap - RowHeight;
                for (int k = 0; k < 2 && i + k < levels.Count; k++)
                {
                    var def = levels[i + k];
                    var x = (k == 0 ? -1f : 1f) * (tileW + gap) * 0.5f;
                    var tile = LevelTile.Create(row.transform, def, completed(def), new Vector3(x, -(tileH + gap - RowHeight) * 0.5f, 0f), tileW, tileH);
                    tile.OnClick = () => onClick(def);
                    tiles.Add(tile);
                }
            }
            return tiles;
        }

        /// <summary>Rows rise into place one after another (welcome screen, level cards).</summary>
        public void AnimateIn(float stagger = 0.045f, float startDelay = 0.05f)
        {
            for (int i = 0; i < m_Rows.Count; i++)
            {
                if (m_Rows[i] == null) continue;
                var r = m_Rows[i].GetComponent<UiReveal>() ?? m_Rows[i].AddComponent<UiReveal>();
                r.Begin(startDelay + i * stagger);
            }
        }
    }
}

using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TwelfthStamp
{
    /// <summary>
    /// Heads-up information. Desktop: screen overlay. VR: the same canvas in world space, lazily following the head
    /// at a comfortable distance (never rigidly head-locked).
    /// Layout rules: nothing is drawn over the held passport (lower left) or on the reticle. An interaction prompt sits
    /// beside the reticle; passport and tear controls live in one card on the right; subtitles and toasts size to
    /// their text; the title card moves aside for panels that share the screen with it (KeepTitleClearOf). Every key
    /// shown is read from the live input bindings.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        public static Hud I { get; private set; }

        public static readonly Color Ivory = new Color(0.96f, 0.94f, 0.89f);
        public static readonly Color Muted = new Color(0.70f, 0.70f, 0.68f);
        public static readonly Color Amber = new Color(0.93f, 0.81f, 0.60f);
        public static readonly Color Teal = new Color(0.52f, 0.90f, 0.80f);
        public static readonly Color Danger = new Color(0.93f, 0.40f, 0.33f);
        static readonly Color PanelColor = new Color(0.018f, 0.022f, 0.03f, 0.72f);
        const string AmberHex = "#EDCF99";

        Canvas m_Canvas;
        RectTransform m_Root;
        Transform m_VrAnchor;
        bool m_Vr;
        Sprite m_Dot, m_Ring, m_Round, m_Edge;
        readonly Texture2D[] m_ContamTex = new Texture2D[4];

        Image m_Crosshair, m_Contam;

        class KeyRow
        {
            public RectTransform Root;
            public Image Chip, Edge;
            public TextMeshProUGUI Key, Label;
        }

        // Interaction prompt.
        Image m_PromptPanel;
        KeyRow m_PromptRow;
        string m_PromptSig;

        // Passport / tear card.
        Image m_Card;
        TextMeshProUGUI m_CardHeader, m_CardTitle, m_CardStatus, m_CardHint, m_ContamText;
        RectTransform m_Gauge, m_ContamRow;
        Image m_GaugeFill, m_GaugeSeal, m_ContamFill, m_CardRule;
        readonly List<KeyRow> m_CardRows = new List<KeyRow>();
        string m_CardSig;

        // Generic hold progress (for holds that are not a tear, e.g. taking the glasses off).
        Image m_HoldPanel, m_HoldFill;
        TextMeshProUGUI m_HoldLabel;

        // Subtitles, toast, objective, title card, vitality.
        Image m_SubtitlePanel;
        TextMeshProUGUI m_Subtitle;
        float m_SubtitleUntil;
        Image m_ToastPanel;
        TextMeshProUGUI m_Toast;
        float m_ToastUntil;
        Image m_ObjectivePanel;
        TextMeshProUGUI m_ObjHeader, m_Objective;
        bool m_ObjectivesOpen;
        string m_ObjectiveText = "";
        RectTransform m_TitleRoot;
        TextMeshProUGUI m_Title, m_TitleSub;
        Image m_TitleRule;
        float m_TitleStart, m_TitleUntil, m_TitleWidth;
        Vector2 m_TitleHome;
        readonly List<RectTransform> m_TitleClear = new List<RectTransform>();
        readonly Vector3[] m_Corners = new Vector3[4];
        RectTransform m_VitalityPanel;
        CanvasGroup m_VitalityGroup;
        Image m_VitalityFill;
        readonly Image[] m_Hearts = new Image[StoryDirector.Hearts];
        static Sprite s_Heart;
        float m_LastVitality = 1f, m_VitalityShowUntil;

        // Fed every frame by TearController while a tear is open (or by other holds).
        float m_TearAperture, m_TearSeal, m_TearShowUntil;
        string m_TearLabel;

        public static Hud Create(Transform parent)
        {
            var go = new GameObject("Hud");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<Hud>();
            I.Build();
            return I;
        }

        // ------------------------------------------------------------------ key caps (from live bindings)
        /// <summary>Short key-cap text for an intent in the current mode: "LMB", "E", "WHEEL", "X", "TRIGGER".</summary>
        public static string KeyCap(Intent intent, out bool hold)
        {
            hold = false;
            var input = Game.Input;
            if (input == null) return "?";
            string full = input.Label(intent) ?? "?";
            if (full.StartsWith("Hold ")) { hold = true; full = full.Substring(5); }
            if (Game.Mode == GameMode.VR)
            {
                switch (intent)
                {
                    case Intent.Grab: return "GRIP";
                    case Intent.Interact: case Intent.Skip: return "TRIGGER";
                    case Intent.Tear: hold = true; return "TRIGGER";
                    case Intent.Seal: hold = true; return GameInput.XRControl(input.SealButton).Label.ToUpperInvariant();
                    case Intent.Passport: return GameInput.XRControl(input.PassportButton).Label.ToUpperInvariant();
                    case Intent.Destination: return GameInput.XRControl(input.DestinationButton).Label.ToUpperInvariant();
                    case Intent.Recall: return GameInput.XRControl(input.RecallButton).Label.ToUpperInvariant();
                    case Intent.Pause: return "MENU";
                    case Intent.Move: case Intent.Teleport: case Intent.Turn: return "STICK";
                    case Intent.Objectives: return "PASSPORT";
                }
            }
            else if (intent == Intent.Move || intent == Intent.Teleport) return "WASD";
            return Short(full);
        }

        public static string KeyCap(Intent intent) => KeyCap(intent, out _);

        static string Short(string label)
        {
            foreach (var sep in new[] { " or ", " / " })
            {
                int i = label.IndexOf(sep, System.StringComparison.Ordinal);
                if (i > 0) label = label.Substring(0, i);
            }
            switch (label)
            {
                case "Left Mouse": return "LMB";
                case "Right Mouse": return "RMB";
                case "Mouse Wheel": return "WHEEL";
                case "Mouse": return "MOUSE";
            }
            return label.ToUpperInvariant();
        }

        /// <summary>Inline key tag for rich text lists (objectives, tutorial).</summary>
        public static string KeyTag(Intent intent)
        {
            string k = KeyCap(intent, out bool hold);
            return "<color=" + AmberHex + "><b>" + k + "</b></color>" + (hold ? " <color=#9A9A96>hold</color>" : "");
        }

        // ------------------------------------------------------------------ construction
        static Sprite SpriteOf(Texture2D t) => Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));

        /// <summary>Anti-aliased rounded rectangle (filled, or a ring of the given width) for 9-slicing.</summary>
        static Sprite RoundedSprite(int size, int radius, float ring)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "HudRounded" };
            var px = new Color32[size * size];
            float half = size / 2f, inner = half - radius;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - inner, 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - inner, 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - radius; // < 0 inside
                    float a = Mathf.Clamp01(0.5f - d);
                    if (ring > 0f) a *= Mathf.Clamp01(d + ring + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            float b = radius + 1;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        void Build()
        {
            m_Vr = Game.Mode == GameMode.VR;
            m_Dot = SpriteOf(Tex.SoftDot);
            m_Ring = SpriteOf(Tex.Ring);
            m_Round = RoundedSprite(64, 18, 0f);
            m_Edge = RoundedSprite(64, 18, 1.5f);
            for (int i = 0; i < 4; i++) m_ContamTex[i] = Tex.Art("lens_contam_" + (i + 1));

            var canvasGo = new GameObject("HudCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            m_Canvas = canvasGo.GetComponent<Canvas>();
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            if (m_Vr)
            {
                m_Canvas.renderMode = RenderMode.WorldSpace;
                m_VrAnchor = new GameObject("HudAnchor").transform;
                m_VrAnchor.SetParent(transform, false);
                canvasGo.transform.SetParent(m_VrAnchor, false);
                var rt = canvasGo.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(1400, 900);
                rt.localScale = Vector3.one * 0.00085f;
                rt.localPosition = new Vector3(0, -0.18f, 1.45f);
                UiOnTop.Register(canvasGo.transform); // the VR HUD never sinks into walls or props (UI/UiOnTop.cs)
            }
            else
            {
                m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                m_Canvas.sortingOrder = 100;
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }
            m_Root = canvasGo.GetComponent<RectTransform>();

            // Lens contamination (desktop only; VR uses vignette + passport page staining instead).
            m_Contam = MakeImage("Contamination", m_Root, Color.clear);
            Stretch(m_Contam.rectTransform);
            m_Contam.preserveAspect = false;
            m_Contam.gameObject.SetActive(!m_Vr);

            // Reticle (desktop).
            m_Crosshair = MakeImage("Crosshair", m_Root, new Color(1f, 1f, 1f, 0.75f));
            Pin(m_Crosshair.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6, 6));
            m_Crosshair.sprite = m_Dot;
            m_Crosshair.gameObject.SetActive(!m_Vr);

            // Interaction prompt: beside the reticle (desktop) or below the view centre (VR).
            m_PromptPanel = Rounded("Prompt", m_Root, PanelColor, 1.6f);
            if (m_Vr) Pin(m_PromptPanel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -170), Vector2.zero);
            else Pin(m_PromptPanel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26, 0), Vector2.zero);
            m_PromptRow = MakeKeyRow(m_PromptPanel.transform, 16, 21);
            m_PromptPanel.gameObject.SetActive(false);

            // Passport / tear card (right).
            m_Card = Rounded("Card", m_Root, PanelColor, 1f);
            Pin(m_Card.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), m_Vr ? new Vector2(-30, 10) : new Vector2(-48, 40), new Vector2(390, 200));
            var cardEdge = MakeImage("Edge", m_Card.transform, new Color(1f, 1f, 1f, 0.07f));
            cardEdge.sprite = m_Edge; cardEdge.type = Image.Type.Sliced; Stretch(cardEdge.rectTransform);
            m_CardHeader = Label(m_Card.transform, 14, Amber, TextAlignmentOptions.TopLeft);
            m_CardHeader.fontStyle = FontStyles.Bold; m_CardHeader.characterSpacing = 9f;
            m_CardTitle = Label(m_Card.transform, 36, Ivory, TextAlignmentOptions.TopLeft, true);
            m_CardStatus = Label(m_Card.transform, 18, Muted, TextAlignmentOptions.TopLeft);
            m_Gauge = Holder("Gauge", m_Card.transform);
            var gBg = Rounded("Bg", m_Gauge, new Color(1f, 1f, 1f, 0.1f), 5f);
            Pin(gBg.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -16), new Vector2(346, 8));
            m_GaugeFill = Rounded("Fill", gBg.transform, Teal, 5f);
            Stretch(m_GaugeFill.rectTransform);
            m_GaugeFill.type = Image.Type.Filled; m_GaugeFill.fillMethod = Image.FillMethod.Horizontal; m_GaugeFill.sprite = SpriteOf(Tex.White);
            m_GaugeSeal = MakeImage("Seal", m_Gauge, Danger);
            Pin(m_GaugeSeal.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -27), new Vector2(346, 3));
            m_GaugeSeal.type = Image.Type.Filled; m_GaugeSeal.fillMethod = Image.FillMethod.Horizontal; m_GaugeSeal.sprite = SpriteOf(Tex.White);
            var names = new[] { "slit", "window", "door" };
            var marks = new[] { TearController.SlitThreshold, TearController.WindowThreshold, TearController.DoorThreshold };
            for (int i = 0; i < 3; i++)
            {
                var tick = MakeImage("Tick", m_Gauge, new Color(1f, 1f, 1f, 0.55f));
                Pin(tick.rectTransform, new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(346 * marks[i], -12), new Vector2(1.5f, 16));
                var tl = Label(m_Gauge, 12, Muted, TextAlignmentOptions.Top);
                tl.text = names[i];
                Pin(tl.rectTransform, new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(346 * marks[i], 2), new Vector2(70, 14));
            }
            m_ContamRow = Holder("Contamination", m_Card.transform);
            m_ContamText = Label(m_ContamRow, 15, Muted, TextAlignmentOptions.TopLeft);
            Pin(m_ContamText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(346, 18));
            var cBg = Rounded("Bg", m_ContamRow, new Color(1f, 1f, 1f, 0.1f), 6f);
            Pin(cBg.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -21), new Vector2(346, 4));
            m_ContamFill = MakeImage("Fill", cBg.transform, Teal);
            Stretch(m_ContamFill.rectTransform);
            m_ContamFill.type = Image.Type.Filled; m_ContamFill.fillMethod = Image.FillMethod.Horizontal; m_ContamFill.sprite = SpriteOf(Tex.White);
            m_CardHint = Label(m_Card.transform, 18, Muted, TextAlignmentOptions.TopLeft);
            m_CardHint.fontStyle = FontStyles.Italic;
            m_CardRule = MakeImage("Rule", m_Card.transform, new Color(1f, 1f, 1f, 0.1f));
            for (int i = 0; i < 5; i++) m_CardRows.Add(MakeKeyRow(m_Card.transform, 15, 20));
            m_Card.gameObject.SetActive(false);

            // Generic hold progress (bottom centre, above subtitles).
            m_HoldPanel = Rounded("Hold", m_Root, PanelColor, 1.4f);
            Pin(m_HoldPanel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, m_Vr ? 260 : 220), new Vector2(420, 66));
            m_HoldLabel = Label(m_HoldPanel.transform, 20, Ivory, TextAlignmentOptions.Center);
            Pin(m_HoldLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(380, 26));
            var hBg = Rounded("Bg", m_HoldPanel.transform, new Color(1f, 1f, 1f, 0.12f), 5f);
            Pin(hBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 14), new Vector2(360, 6));
            m_HoldFill = MakeImage("Fill", hBg.transform, Amber);
            Stretch(m_HoldFill.rectTransform);
            m_HoldFill.type = Image.Type.Filled; m_HoldFill.fillMethod = Image.FillMethod.Horizontal; m_HoldFill.sprite = SpriteOf(Tex.White);
            m_HoldPanel.gameObject.SetActive(false);

            // Subtitles (bottom centre, sized to the line).
            m_SubtitlePanel = Rounded("Subtitle", m_Root, new Color(0f, 0f, 0f, 0.62f), 1.4f);
            Pin(m_SubtitlePanel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, m_Vr ? 90 : 52), new Vector2(600, 60));
            m_Subtitle = Label(m_SubtitlePanel.transform, 31, Ivory, TextAlignmentOptions.Center);
            Pin(m_Subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 40));
            m_SubtitlePanel.gameObject.SetActive(false);

            // Toast (top centre).
            m_ToastPanel = Rounded("Toast", m_Root, new Color(0.018f, 0.022f, 0.03f, 0.6f), 1.4f);
            Pin(m_ToastPanel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, m_Vr ? -150 : -54), new Vector2(400, 50));
            m_Toast = Label(m_ToastPanel.transform, 26, new Color(0.97f, 0.92f, 0.82f), TextAlignmentOptions.Center, true);
            Pin(m_Toast.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380, 36));
            m_ToastPanel.gameObject.SetActive(false);

            // Objective (top left, toggled).
            m_ObjectivePanel = Rounded("Objective", m_Root, PanelColor, 1f);
            Pin(m_ObjectivePanel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), m_Vr ? new Vector2(120, -120) : new Vector2(40, -40), new Vector2(480, 120));
            var oEdge = MakeImage("Edge", m_ObjectivePanel.transform, new Color(1f, 1f, 1f, 0.07f));
            oEdge.sprite = m_Edge; oEdge.type = Image.Type.Sliced; Stretch(oEdge.rectTransform);
            m_ObjHeader = Label(m_ObjectivePanel.transform, 14, Amber, TextAlignmentOptions.TopLeft);
            m_ObjHeader.fontStyle = FontStyles.Bold; m_ObjHeader.characterSpacing = 9f; m_ObjHeader.text = "OBJECTIVE";
            m_Objective = Label(m_ObjectivePanel.transform, 21, Ivory, TextAlignmentOptions.TopLeft);
            m_Objective.lineSpacing = 6f;
            m_ObjectivePanel.gameObject.SetActive(false);

            // Title card.
            m_TitleRoot = Holder("TitleCard", m_Root);
            Pin(m_TitleRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, m_Vr ? 120 : 170), new Vector2(1400, 200));
            m_TitleHome = m_TitleRoot.anchoredPosition;
            m_Title = Label(m_TitleRoot, 74, Ivory, TextAlignmentOptions.Center, true);
            m_Title.characterSpacing = 6f;
            Pin(m_Title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1400, 96));
            m_TitleRule = MakeImage("Rule", m_TitleRoot, new Color(Amber.r, Amber.g, Amber.b, 0.85f));
            Pin(m_TitleRule.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -26), new Vector2(240, 1.5f));
            m_TitleSub = Label(m_TitleRoot, 25, Muted, TextAlignmentOptions.Center);
            m_TitleSub.fontStyle = FontStyles.Italic;
            Pin(m_TitleSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -56), new Vector2(1200, 36));
            m_TitleRoot.gameObject.SetActive(false);
            KeepTitleClearOf(m_ObjectivePanel.rectTransform);   // Tab, on a narrow screen
            KeepTitleClearOf(m_Card.rectTransform);

            // Health: three hearts at the top right, under the minimap (playtest, 18 Oct: "put the health bar as heart
            // emojis on top right"). A catch by the Passenger costs a heart; the maze's heat and other drains empty them
            // bit by bit (each heart fills from the bottom).
            m_VitalityPanel = Holder("Hearts", m_Root);
            Pin(m_VitalityPanel, new Vector2(1f, 1f), new Vector2(1f, 1f), m_Vr ? new Vector2(-150, -110) : new Vector2(-44, -36) /* top-right corner: the minimap is gone */, new Vector2(StoryDirector.Hearts * 58f, 52));
            m_VitalityGroup = m_VitalityPanel.gameObject.AddComponent<CanvasGroup>();
            m_VitalityGroup.alpha = 0f;
            if (s_Heart == null) s_Heart = SpriteOf(HeartTexture());
            for (int i = 0; i < StoryDirector.Hearts; i++)
            {
                var slot = MakeImage("HeartSlot" + i, m_VitalityPanel, new Color(0.12f, 0.02f, 0.03f, 0.75f));
                slot.sprite = s_Heart;
                Pin(slot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * 58f, 0f), new Vector2(60, 60));
                var fill = MakeImage("Heart" + i, slot.transform, Color.white); // the heart texture carries its own colours
                fill.sprite = s_Heart;
                Stretch(fill.rectTransform);
                fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Vertical; fill.fillOrigin = 0;
                m_Hearts[i] = fill;
            }
            m_VitalityFill = m_Hearts[0];

            ApplySettings();
        }

        // ------------------------------------------------------------------ UI helpers
        static Image MakeImage(string name, Transform parent, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        Image Rounded(string name, Transform parent, Color c, float cornerScale)
        {
            var img = MakeImage(name, parent, c);
            img.sprite = m_Round;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = cornerScale; // larger = tighter corners
            return img;
        }

        static RectTransform Holder(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static TextMeshProUGUI Label(Transform parent, float size, Color c, TextAlignmentOptions align, bool serif = false)
        {
            var t = TextFactory.Ui(parent, "", size, c, align, serif);
            t.raycastTarget = false;
            return t;
        }

        static void Pin(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = pivot;
            rt.anchoredPosition = pos;
            if (size != Vector2.zero) rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        /// <summary>Places a text block at the top-left of its parent and returns its height.</summary>
        static float Block(TextMeshProUGUI t, string text, float x, float y, float width)
        {
            t.text = text;
            bool on = !string.IsNullOrEmpty(text);
            t.gameObject.SetActive(on);
            if (!on) return 0f;
            float h = t.GetPreferredValues(text, width, 0f).y;
            Pin(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(width, h));
            return h;
        }

        KeyRow MakeKeyRow(Transform parent, float keySize, float labelSize)
        {
            var r = new KeyRow { Root = Holder("KeyRow", parent) };
            r.Chip = Rounded("Key", r.Root, new Color(Amber.r, Amber.g, Amber.b, 0.13f), 2.6f);
            r.Edge = MakeImage("Edge", r.Chip.transform, new Color(Amber.r, Amber.g, Amber.b, 0.8f));
            r.Edge.sprite = m_Edge; r.Edge.type = Image.Type.Sliced; r.Edge.pixelsPerUnitMultiplier = 2.6f;
            Stretch(r.Edge.rectTransform);
            r.Key = Label(r.Chip.transform, keySize, Amber, TextAlignmentOptions.Center);
            r.Key.fontStyle = FontStyles.Bold; r.Key.characterSpacing = 3f;
            r.Key.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(r.Key.rectTransform);
            r.Label = Label(r.Root, labelSize, Ivory, TextAlignmentOptions.MidlineLeft);
            return r;
        }

        /// <summary>Lays out [KEY] label inside a row anchored top-left at (x, y); returns the used size.</summary>
        static Vector2 LayoutKeyRow(KeyRow r, string key, string label, float x, float y, float maxWidth)
        {
            r.Root.gameObject.SetActive(true);
            r.Key.text = key;
            float chipH = Mathf.Round(r.Key.fontSize * 1.85f);
            float chipW = Mathf.Max(r.Key.GetPreferredValues(key).x + 22f, chipH + 4f);
            float labelMax = Mathf.Max(40f, maxWidth - chipW - 12f);
            var lp = r.Label.GetPreferredValues(label, labelMax, 0f);
            float lw = Mathf.Min(lp.x, labelMax) + 2f;
            float lh = r.Label.GetPreferredValues(label, lw, 0f).y;
            float h = Mathf.Max(chipH, lh);
            r.Label.text = label;
            Pin(r.Root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(chipW + 12f + lw, h));
            Pin(r.Chip.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(chipW, chipH));
            Pin(r.Label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(chipW + 12f, 0f), new Vector2(lw, lh));
            return new Vector2(chipW + 12f + lw, h);
        }

        // ------------------------------------------------------------------ settings
        public void ApplySettings()
        {
            var s = Game.Settings ?? new GameSettings();
            if (m_Subtitle == null) return;
            m_Subtitle.fontSize = s.subtitleSize == 0 ? 26 : s.subtitleSize == 1 ? 31 : 40;
            m_SubtitlePanel.color = new Color(0f, 0f, 0f, s.subtitleBackground ? 0.62f : 0f);
            m_Subtitle.outlineWidth = s.subtitleBackground ? 0f : 0.22f;
            m_Subtitle.outlineColor = new Color32(0, 0, 0, 230);
        }

        // ------------------------------------------------------------------ API
        public void ShowSubtitle(string speaker, string text, float seconds, bool tutorial)
        {
            if (Game.Settings != null && !Game.Settings.subtitles && !tutorial) return;
            string body = tutorial ? "<i>" + text + "</i>" : text;
            SetSubtitle(string.IsNullOrEmpty(speaker) ? body : "<color=" + AmberHex + ">" + speaker + ":</color>  " + body, seconds);
        }

        public void ShowCaption(string text, float seconds = 3f)
        {
            if (Game.Settings != null && !Game.Settings.subtitles) return;
            SetSubtitle("<color=#B9C9C4>[" + text + "]</color>", seconds);
        }

        void SetSubtitle(string rich, float seconds)
        {
            float maxW = m_Vr ? 1040f : 1180f;
            float w = Mathf.Min(m_Subtitle.GetPreferredValues(rich, maxW, 0f).x, maxW) + 2f;
            float h = m_Subtitle.GetPreferredValues(rich, w, 0f).y;
            m_Subtitle.text = rich;
            m_Subtitle.rectTransform.sizeDelta = new Vector2(w, h);
            m_SubtitlePanel.rectTransform.sizeDelta = new Vector2(w + 56f, h + 26f);
            m_SubtitlePanel.gameObject.SetActive(true);
            m_SubtitleUntil = Time.unscaledTime + seconds;
        }

        public void ClearSubtitle()
        {
            m_SubtitlePanel.gameObject.SetActive(false);
            m_SubtitleUntil = 0f;
        }

        public void ShowToast(string text, float seconds = 2.5f)
        {
            if (string.IsNullOrEmpty(text)) return;
            float maxW = 1000f;
            float w = Mathf.Min(m_Toast.GetPreferredValues(text, maxW, 0f).x, maxW) + 2f;
            float h = m_Toast.GetPreferredValues(text, w, 0f).y;
            m_Toast.text = text;
            m_Toast.rectTransform.sizeDelta = new Vector2(w, h);
            m_ToastPanel.rectTransform.sizeDelta = new Vector2(w + 52f, h + 22f);
            m_ToastPanel.gameObject.SetActive(true);
            m_ToastUntil = Time.unscaledTime + seconds;
        }

        public void SetObjective(string text, bool flash = true)
        {
            m_ObjectiveText = text ?? "";
            if (flash && !string.IsNullOrEmpty(text)) ShowToast(m_Vr ? "Objective updated" : "Objective updated  ·  " + KeyCap(Intent.Objectives), 2f);
            RefreshObjective();
        }

        public void ToggleObjectives()
        {
            m_ObjectivesOpen = !m_ObjectivesOpen;
            RefreshObjective();
        }

        void RefreshObjective()
        {
            var c = Game.Campaign;
            var sb = new StringBuilder(m_ObjectiveText);
            if (c != null) sb.Append("\n<size=82%><color=#A8A8A4>Seals verified  ").Append(c.TargetVerifiedCount).Append(" / ").Append(c.TargetCount).Append("</color></size>");
            if (!m_Vr) sb.Append("\n<size=72%><color=#8E8E8A>").Append(KeyCap(Intent.Objectives)).Append("  hide</color></size>");
            const float W = 480f, P = 22f;
            float y = -P;
            y -= Block(m_ObjHeader, "OBJECTIVE", P, y, W - 2 * P) + 8f;
            y -= Block(m_Objective, sb.ToString(), P, y, W - 2 * P);
            m_ObjectivePanel.rectTransform.sizeDelta = new Vector2(W, -y + P);
            m_ObjectivePanel.gameObject.SetActive(m_ObjectivesOpen && !string.IsNullOrEmpty(m_ObjectiveText));
        }

        public void ShowTitleCard(string title, string sub, float seconds = 4f)
        {
            m_Title.text = title;
            m_TitleSub.text = sub ?? "";
            m_TitleRule.gameObject.SetActive(!string.IsNullOrEmpty(sub));
            m_TitleRoot.gameObject.SetActive(true);
            m_TitleStart = Time.unscaledTime;
            m_TitleUntil = Time.unscaledTime + seconds;
            m_TitleWidth = Mathf.Max(m_Title.GetPreferredValues(m_Title.text).x, m_TitleSub.GetPreferredValues(m_TitleSub.text).x, m_TitleRule.rectTransform.rect.width);
            PlaceTitle(true);
        }

        public void HideTitleCard()
        {
            if (m_TitleRoot != null) m_TitleRoot.gameObject.SetActive(false);
        }

        public bool TitleShowing => m_TitleRoot != null && m_TitleRoot.gameObject.activeSelf;

        /// <summary>Keeps the title card clear of a panel that can be on screen with it (the guide's arrival briefing, the minimap).</summary>
        public void KeepTitleClearOf(RectTransform panel)
        {
            if (panel != null && !m_TitleClear.Contains(panel)) m_TitleClear.Add(panel);
        }

        /// <summary>The objectives panel (Tab) while it is open, else null.</summary>
        public RectTransform ObjectivesPanel => m_ObjectivePanel != null && m_ObjectivePanel.gameObject.activeSelf ? m_ObjectivePanel.rectTransform : null;

        /// <summary>The passport or tear card on the right while it is showing, else null.</summary>
        public RectTransform PassportCard => m_Card != null && m_Card.gameObject.activeSelf ? m_Card.rectTransform : null;

        /// <summary>Where a panel on this HUD's canvas is, in canvas units from the canvas centre.</summary>
        public Rect CanvasRect(RectTransform rt)
        {
            rt.GetWorldCorners(m_Corners);
            Vector2 min = m_Root.InverseTransformPoint(m_Corners[0]), max = m_Root.InverseTransformPoint(m_Corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>
        /// The title card stays centred unless a panel on screen with it would cover it: the guide's arrival briefing or the
        /// objectives panel on the left, the passport card or the minimap's label on the right. Then it slides along its line
        /// just clear of them, and shrinks only if the space between them is too narrow. In canvas units, so it holds at any
        /// aspect ratio.
        /// </summary>
        void PlaceTitle(bool snap)
        {
            const float Gap = 40f, Margin = 40f;
            float lo = -m_Root.rect.width / 2f + Margin, hi = m_Root.rect.width / 2f - Margin;
            var tr = m_Title.rectTransform;
            var sr = m_TitleSub.rectTransform;
            float top = m_TitleHome.y + tr.anchoredPosition.y + tr.rect.height / 2f;
            float bottom = m_TitleHome.y + sr.anchoredPosition.y - sr.rect.height / 2f;
            foreach (var p in m_TitleClear)
            {
                if (p == null || !p.gameObject.activeInHierarchy) continue;
                var r = CanvasRect(p);
                if (r.yMin > top || r.yMax < bottom) continue;   // above or below the card's lines
                if (r.center.x < 0f) lo = Mathf.Max(lo, r.xMax + Gap);
                else hi = Mathf.Min(hi, r.xMin - Gap);
            }
            float scale = Mathf.Clamp((hi - lo) / Mathf.Max(1f, m_TitleWidth), 0.55f, 1f);
            float half = m_TitleWidth * scale / 2f;
            float x = lo + half > hi - half ? (lo + hi) / 2f : Mathf.Clamp(0f, lo + half, hi - half);
            float k = snap ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime * 10f);
            m_TitleRoot.anchoredPosition = Vector2.Lerp(m_TitleRoot.anchoredPosition, new Vector2(x, m_TitleHome.y), k);
            m_TitleRoot.localScale = Vector3.one * Mathf.Lerp(m_TitleRoot.localScale.x, scale, k);
        }

        /// <summary>Called every frame while a tear is open (aperture 0..1, seal 0..1), or for any other timed hold.</summary>
        public void ShowTear(float aperture, float seal, string label)
        {
            m_TearAperture = aperture;
            m_TearSeal = seal;
            m_TearLabel = label;
            m_TearShowUntil = Time.unscaledTime + 0.25f;
        }

        // ------------------------------------------------------------------ per frame
        void LateUpdate()
        {
            if (AppArgs.CleanHud && m_Canvas != null && m_Canvas.enabled) m_Canvas.enabled = false; // trailer capture: a clean picture
            float t = Time.unscaledTime;
            if (m_SubtitlePanel.gameObject.activeSelf && t > m_SubtitleUntil) m_SubtitlePanel.gameObject.SetActive(false);
            if (m_ToastPanel.gameObject.activeSelf)
            {
                float a = Mathf.Clamp01((m_ToastUntil - t) * 2.5f);
                m_Toast.alpha = a;
                m_ToastPanel.color = new Color(PanelColor.r, PanelColor.g, PanelColor.b, 0.6f * a);
                if (a <= 0f) m_ToastPanel.gameObject.SetActive(false);
            }
            if (m_TitleRoot.gameObject.activeSelf)
            {
                float a = Mathf.Min(Mathf.Clamp01((t - m_TitleStart) * 1.6f), Mathf.Clamp01((m_TitleUntil - t) * 1.2f));
                m_Title.alpha = a; m_TitleSub.alpha = a;
                m_TitleRule.color = new Color(Amber.r, Amber.g, Amber.b, 0.85f * a);
                if (t > m_TitleUntil) m_TitleRoot.gameObject.SetActive(false);
                else PlaceTitle(t - m_TitleStart < 0.2f);   // the briefing appears in the same frame or the next: no visible slide
            }

            var rig = Game.Rig;
            bool playHud = rig != null && !Game.InMenu && !Game.Paused;
            UpdatePrompt(rig, playHud);
            UpdateCard(playHud, t);
            UpdateReticle(rig, playHud);
            UpdateVitality(playHud, t);
            UpdateContamination();
            if (m_VrAnchor != null && rig != null) FollowHead(rig.Head);
        }

        void UpdatePrompt(PlayerRig rig, bool playHud)
        {
            string key = null, text = null;
            if (playHud)
            {
                var hand = rig.MainHand;
                foreach (var h in rig.Hands) if (h.Focused != null) { hand = h; break; }
                var p = hand != null ? hand.CurrentPrompt() : null;
                if (!string.IsNullOrEmpty(p))
                {
                    text = p;
                    if (m_Vr) key = hand.Focused is Grabbable && hand.FocusIsNear ? "GRIP" : "TRIGGER";
                    else key = hand.Held != null && hand.Focused == null ? KeyCap(Intent.Tear) : KeyCap(Intent.Interact);
                }
            }
            if (text == null)
            {
                if (m_PromptPanel.gameObject.activeSelf) m_PromptPanel.gameObject.SetActive(false);
                m_PromptSig = null;
                return;
            }
            string sig = key + "|" + text;
            if (sig != m_PromptSig)
            {
                m_PromptSig = sig;
                var size = LayoutKeyRow(m_PromptRow, key, text, 12f, -9f, 560f);
                m_PromptPanel.rectTransform.sizeDelta = new Vector2(size.x + 26f, size.y + 18f);
            }
            if (!m_PromptPanel.gameObject.activeSelf) m_PromptPanel.gameObject.SetActive(true);
        }

        void UpdateReticle(PlayerRig rig, bool playHud)
        {
            if (m_Vr) return;
            bool show = playHud;
            if (m_Crosshair.gameObject.activeSelf != show) m_Crosshair.gameObject.SetActive(show);
            if (!show) return;
            bool focus = rig.MainHand != null && rig.MainHand.Focused != null;
            m_Crosshair.sprite = focus ? m_Ring : m_Dot;
            m_Crosshair.color = focus ? new Color(Amber.r, Amber.g, Amber.b, 0.9f) : new Color(1f, 1f, 1f, 0.7f);
            m_Crosshair.rectTransform.sizeDelta = focus ? new Vector2(26, 26) : new Vector2(6, 6);
        }

        void UpdateCard(bool playHud, float now)
        {
            var tc = Game.Tears;
            var pp = Game.Passport;
            var c = Game.Campaign;
            var tear = tc != null ? tc.Active : null;
            bool tearOn = tear != null && !tear.Closing && now <= m_TearShowUntil;
            bool passportOn = pp != null && pp.IsOpen;

            // A timed hold that is not a tear (e.g. taking the glasses off) gets the small centre bar instead.
            bool hold = !tearOn && now <= m_TearShowUntil && !string.IsNullOrEmpty(m_TearLabel);
            if (m_HoldPanel.gameObject.activeSelf != hold) m_HoldPanel.gameObject.SetActive(hold);
            if (hold) { m_HoldLabel.text = m_TearLabel; m_HoldFill.fillAmount = m_TearAperture; }

            bool show = playHud && c != null && (tearOn || passportOn);
            if (m_Card.gameObject.activeSelf != show) m_Card.gameObject.SetActive(show);
            if (!show) { m_CardSig = null; return; }

            string header, title, status, hint;
            bool hintDanger = false;
            var rows = new List<(string key, string label)>(5);
            if (tearOn)
            {
                header = "TEAR OPEN";
                title = tear.Destination == CountryDatabase.Home ? "Home" : tear.Destination.DisplayName;
                string size = tear.Size == TearSize.Doorway ? "Doorway" : tear.Size == TearSize.Window ? "Window" : "Slit";
                status = size + (tear.Beam != null ? "  ·  sunlight flowing" : "");
                float contam = tc.Contamination;
                hint = contam > 0.66f ? "It is pushing through. Seal it." : tear.Size == TearSize.Doorway ? "Wide enough to step through." : null;
                hintDanger = contam > 0.66f;
                if (tear.Size != TearSize.Doorway) rows.Add((KeyCap(Intent.Tear), "Hold to widen"));
                rows.Add((KeyCap(Intent.Seal), "Hold to seal"));
                if (tear.Size == TearSize.Doorway) rows.Add((m_Vr ? "STICK" : "W", "Walk into it to step through"));
            }
            else
            {
                header = "PASSPORT";
                var dest = pp.SelectedDef;
                bool home = tc != null && tc.HasTarget && tc.Target != null && tc.Target.HomeSeam;
                if (home)
                {
                    title = "Home";
                    status = c.CanOpenGenuineHome ? "The true return" : "Forged authorisation still holds";
                    hint = c.CanOpenGenuineHome ? null : "The wall resists.";
                    if (c.CanOpenGenuineHome) rows.Add((KeyCap(Intent.Tear), "Hold to open the way home"));
                }
                else
                {
                    title = dest != null ? dest.DisplayName : "No destination";
                    status = dest == null ? "" : c.IsVerified(dest.Id) ? "Seal verified" : "Seal not verified yet";
                    if (dest != null && dest.Id == CountryId.Egypt && c.ResourceFlowsFrom(CountryId.Egypt, ResourceId.Sunlight)) status += "  ·  brings sunlight";
                    string reason = null;
                    bool can = dest != null && tc != null && tc.CanOpenTo(dest, out reason);
                    hint = dest == null ? "Choose a destination." : !can ? reason : !tc.HasTarget ? "Aim at a stitched seam." : null;
                    rows.Add((KeyCap(Intent.Destination), "Change destination"));
                    if (can && tc.HasTarget) rows.Add((KeyCap(Intent.Tear), "Hold to open a tear"));
                }
                rows.Add((KeyCap(Intent.Passport), "Close passport"));
            }

            var sb = new StringBuilder(header).Append('|').Append(title).Append('|').Append(status).Append('|').Append(hint).Append('|').Append(hintDanger);
            foreach (var r in rows) sb.Append('|').Append(r.key).Append(':').Append(r.label);
            string sig = sb.ToString();
            if (sig != m_CardSig)
            {
                m_CardSig = sig;
                const float W = 390f, P = 22f, IW = W - 2 * P;
                float y = -P;
                y -= Block(m_CardHeader, header, P, y, IW) + 4f;
                y -= Block(m_CardTitle, title, P, y, IW);
                float sh = Block(m_CardStatus, status, P, y, IW);
                y -= sh + (sh > 0f ? 12f : 6f);
                m_Gauge.gameObject.SetActive(tearOn);
                m_ContamRow.gameObject.SetActive(tearOn);
                if (tearOn)
                {
                    Pin(m_Gauge, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(P, y), new Vector2(IW, 34));
                    y -= 40f;
                    Pin(m_ContamRow, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(P, y), new Vector2(IW, 28));
                    y -= 34f;
                }
                m_CardHint.color = hintDanger ? Danger : Muted;
                float hh = Block(m_CardHint, hint, P, y, IW);
                if (hh > 0f) y -= hh + 10f;
                Pin(m_CardRule.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(P, y), new Vector2(IW, 1));
                y -= 13f;
                for (int i = 0; i < m_CardRows.Count; i++)
                {
                    if (i >= rows.Count) { m_CardRows[i].Root.gameObject.SetActive(false); continue; }
                    var size = LayoutKeyRow(m_CardRows[i], rows[i].key, rows[i].label, P, y, IW);
                    y -= size.y + 8f;
                }
                m_Card.rectTransform.sizeDelta = new Vector2(W, -y + P - 8f);
            }
            if (tearOn)
            {
                m_GaugeFill.fillAmount = m_TearAperture;
                m_GaugeSeal.fillAmount = m_TearSeal;
                float contam = tc.Contamination;
                m_ContamFill.fillAmount = contam;
                m_ContamFill.color = Color.Lerp(Teal, Danger, Mathf.InverseLerp(0.4f, 0.9f, contam));
                m_ContamText.text = "Contamination  " + Mathf.RoundToInt(contam * 100f) + "%";
            }
        }

        void UpdateVitality(bool playHud, float now)
        {
            var c = Game.Campaign;
            bool allowed = playHud && c != null && (Game.Settings?.showVitalityBar ?? true) && Game.Story != null && Game.Story.ShowVitality;
            float v = c != null ? c.vitality : 1f;
            if (v < m_LastVitality - 0.05f) m_VitalityShowUntil = now + 0.6f; // a lost heart: the hearts jolt
            m_LastVitality = v;
            m_VitalityGroup.alpha = Mathf.MoveTowards(m_VitalityGroup.alpha, allowed ? 1f : 0f, Time.unscaledDeltaTime * 3f);
            float hearts = v * StoryDirector.Hearts;
            for (int i = 0; i < m_Hearts.Length; i++)
            {
                var h = m_Hearts[i];
                if (h == null) continue;
                h.fillAmount = Mathf.Clamp01(hearts - i);
                // The last heart beats; a fresh hit shakes them.
                bool last = hearts <= i + 1f && hearts > i;
                float beat = last && hearts < 1.01f ? 1f + 0.12f * Mathf.Max(0f, Mathf.Sin(now * 7f)) : 1f;
                float jolt = now < m_VitalityShowUntil ? 1f + 0.25f * (m_VitalityShowUntil - now) : 1f;
                h.transform.parent.localScale = Vector3.one * beat * jolt;
            }
        }

        /// <summary>
        /// A glossy red heart (the user's reference, 18 Oct): red from bright at the top to deep at the point, a bright rim,
        /// a white highlight on the upper-left lobe and a soft red glow round it. Drawn in full colour, so the filled hearts
        /// show it as is (white tint) and the empty slots darken it.
        /// </summary>
        static Texture2D HeartTexture()
        {
            const int n = 96;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Heart", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fx = (x + 0.5f) / n * 3.2f - 1.6f, fy = (y + 0.5f) / n * 3.2f - 1.5f;
                    float a = fx * fx + fy * fy - 1f;
                    float f = a * a * a - fx * fx * fy * fy * fy; // <= 0 inside
                    float inside = Mathf.Clamp01(0.5f - f * 18f);
                    float glow = f > 0f ? Mathf.Exp(-f * 9f) * 0.55f : 0f;
                    float v = Mathf.InverseLerp(-1.1f, 1.3f, fy);
                    var c = Color.Lerp(new Color(0.72f, 0.0f, 0.03f), new Color(1f, 0.16f, 0.16f), v);
                    float rim = f > -0.12f && f <= 0f ? (1f - (-f / 0.12f)) * 0.35f : 0f;
                    float hx = (fx + 0.48f) / 0.3f, hy = (fy - 0.62f) / 0.19f;
                    float spec = Mathf.Clamp01(1f - (hx * hx + hy * hy)) * 0.85f;
                    c = Color.Lerp(c, Color.white, Mathf.Clamp01(spec + rim));
                    var outCol = new Color(0.95f, 0.05f, 0.05f);
                    var col = Color.Lerp(outCol, c, inside);
                    float alpha = Mathf.Max(inside, glow);
                    px[y * n + x] = new Color32((byte)(col.r * 255f), (byte)(col.g * 255f), (byte)(col.b * 255f), (byte)(Mathf.Clamp01(alpha) * 255f));
                }
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        void UpdateContamination()
        {
            if (m_Contam == null || !m_Contam.gameObject.activeSelf) return;
            float c = Game.Tears != null ? Game.Tears.Contamination : 0f;
            float d = Game.Campaign != null ? 1f - Game.Campaign.vitality : 0f;
            float level = Mathf.Max(c, d * 0.8f);
            if (Game.Settings != null && Game.Settings.reducedEffects) level = 0f;
            int idx = Mathf.Clamp(Mathf.FloorToInt(level * 4f), 0, 3);
            var tex = m_ContamTex[idx];
            if (tex != null && (m_Contam.sprite == null || m_Contam.sprite.texture != tex)) m_Contam.sprite = SpriteOf(tex);
            m_Contam.color = new Color(1, 1, 1, tex != null ? Mathf.Clamp01(level * 1.2f) * 0.85f : 0f);
        }

        void FollowHead(Transform head)
        {
            m_VrAnchor.position = head.position;
            var fwd = head.forward;
            var target = Quaternion.LookRotation(fwd, Vector3.up);
            float angle = Quaternion.Angle(m_VrAnchor.rotation, target);
            float speed = angle > 25f ? 4f : angle > 8f ? 1.6f : 0.4f;
            m_VrAnchor.rotation = Quaternion.Slerp(m_VrAnchor.rotation, target, Time.unscaledDeltaTime * speed);
        }
    }
}

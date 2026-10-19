using TMPro;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>TextMeshPro helpers (3D text and UI text) with the project's fonts.</summary>
    public static class TextFactory
    {
        static TMP_FontAsset s_Sans, s_Serif;

        public static TMP_FontAsset Sans
        {
            get
            {
                if (s_Sans == null) s_Sans = Resources.Load<TMP_FontAsset>("Fonts/Inter SDF");
                if (s_Sans == null) s_Sans = TMP_Settings.defaultFontAsset;
                return s_Sans;
            }
        }

        public static TMP_FontAsset Serif
        {
            get
            {
                if (s_Serif == null) s_Serif = Resources.Load<TMP_FontAsset>("Fonts/EBGaramond SDF");
                if (s_Serif == null) s_Serif = Sans;
                return s_Serif;
            }
        }

        /// <summary>World-space text. size is in metres of cap height (approx).</summary>
        public static TextMeshPro World(Transform parent, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, bool serif = false, float width = 1f)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshPro>();
            t.font = serif ? Serif : Sans;
            t.text = text;
            t.fontSize = size * 10f;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.rectTransform.sizeDelta = new Vector2(width, size * 4f);
            t.enableAutoSizing = false;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return t;
        }

        /// <summary>UI text inside a Canvas.</summary>
        public static TextMeshProUGUI Ui(Transform parent, string text, float fontSize, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, bool serif = false)
        {
            var go = new GameObject("UIText", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = serif ? Serif : Sans;
            t.text = text;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            return t;
        }
    }
}

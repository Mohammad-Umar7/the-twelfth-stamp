using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TwelfthStamp
{
    /// <summary>
    /// Screen fades that work in both modes: a camera-attached inner sphere (renders in both eyes, ZTest Always) plus
    /// a screen-space overlay for the desktop window. Fades never move the camera.
    /// </summary>
    public class Fader : MonoBehaviour
    {
        public static Fader I { get; private set; }

        Material m_Mat;
        GameObject m_Sphere;
        Image m_Overlay;
        RawImage m_Mark;          // small key-art mark while the screen stays black (desktop)
        float m_BlackFor;
        float m_Alpha;
        float m_Target;
        float m_Speed = 2f;
        bool m_Blocker;
        Color m_Color = Color.black;

        public float Alpha => m_Alpha;
        public bool IsBusy => !Mathf.Approximately(m_Alpha, m_Target);

        public static Fader Create(Transform parent, Camera cam)
        {
            var go = new GameObject("Fader");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<Fader>();
            I.Build(cam);
            return I;
        }

        void Build(Camera cam)
        {
            m_Mat = new Material(Mats.Template("TS_Fade"));
            var b = new MeshBuilder();
            b.AddSphere(Vector3.zero, 0.25f, 16, 10);
            m_Sphere = MeshFactory.Make("FadeSphere", b.ToMesh("FadeSphere"), m_Mat, cam.transform);
            m_Sphere.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_Sphere.SetActive(false);

            var canvasGo = new GameObject("FadeCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            var img = new GameObject("Fade", typeof(RectTransform), typeof(Image));
            img.transform.SetParent(canvasGo.transform, false);
            var rt = img.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            m_Overlay = img.GetComponent<Image>();
            m_Overlay.raycastTarget = false;
            var thumb = Branding.Thumb;
            if (thumb != null)
            {
                m_Mark = new GameObject("LoadingMark", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                m_Mark.transform.SetParent(canvasGo.transform, false);
                m_Mark.texture = thumb;
                m_Mark.raycastTarget = false;
                var mrt = m_Mark.rectTransform;
                mrt.anchorMin = mrt.anchorMax = mrt.pivot = new Vector2(1f, 0f);
                mrt.anchoredPosition = new Vector2(-36f, 36f);
                mrt.sizeDelta = new Vector2(64f, 64f);
                m_Mark.color = new Color(1f, 1f, 1f, 0f);
            }
            Apply();
        }

        public void SetColor(Color c) { m_Color = c; Apply(); }

        public void FadeTo(float alpha, float seconds)
        {
            m_Target = Mathf.Clamp01(alpha);
            m_Speed = seconds <= 0f ? 1000f : Mathf.Abs(m_Target - m_Alpha) / seconds;
            if (seconds <= 0f) { m_Alpha = m_Target; Apply(); }
        }

        public IEnumerator FadeOut(float seconds = 0.5f)
        {
            FadeTo(1f, seconds);
            while (m_Alpha < 0.999f) yield return null;
        }

        public IEnumerator FadeIn(float seconds = 0.6f)
        {
            FadeTo(0f, seconds);
            while (m_Alpha > 0.001f) yield return null;
        }

        /// <summary>Quick dip used by teleport.</summary>
        public void Flash(float outSeconds, float inSeconds) => StartCoroutine(FlashRoutine(outSeconds, inSeconds));

        IEnumerator FlashRoutine(float o, float i)
        {
            float prev = m_Target;
            FadeTo(0.85f, o);
            yield return new WaitForSecondsRealtime(o);
            FadeTo(prev, i);
        }

        /// <summary>VR head inside geometry: darken strongly until the player steps back.</summary>
        public void SetBlocker(bool on)
        {
            m_Blocker = on;
            Apply();
            if (on) Hud.I?.ShowToast("Step back", 1.2f);
        }

        void Update()
        {
            if (!Mathf.Approximately(m_Alpha, m_Target))
            {
                m_Alpha = Mathf.MoveTowards(m_Alpha, m_Target, m_Speed * Time.unscaledDeltaTime);
                Apply();
            }
            if (m_Mark != null)
            {
                // Only for a real wait (black for a moment and staying black), never for quick dips or fades.
                // Only for a loading wait on black (never on the red death fade, menus or quick dips).
                bool black = m_Alpha > 0.99f && m_Target > 0.99f && Game.Mode == GameMode.Desktop && m_Color.maxColorComponent < 0.02f && !Game.InMenu;
                m_BlackFor = black ? m_BlackFor + Time.unscaledDeltaTime : 0f;
                float a = m_BlackFor > 0.35f ? Mathf.Clamp01((m_BlackFor - 0.35f) * 3f) * (0.6f + 0.25f * Mathf.Sin(Time.unscaledTime * 2.4f)) : 0f;
                var c = m_Mark.color;
                c.a = black ? a : Mathf.MoveTowards(c.a, 0f, Time.unscaledDeltaTime * 6f);
                m_Mark.color = c;
            }
        }

        void Apply()
        {
            float a = Mathf.Max(m_Alpha, m_Blocker ? 0.92f : 0f);
            var c = new Color(m_Color.r, m_Color.g, m_Color.b, a);
            if (m_Mat != null) m_Mat.SetColor("_Color", c);
            if (m_Sphere != null) m_Sphere.SetActive(a > 0.001f && Game.Mode == GameMode.VR);
            if (m_Overlay != null) { m_Overlay.color = c; m_Overlay.enabled = a > 0.001f; }
        }
    }
}

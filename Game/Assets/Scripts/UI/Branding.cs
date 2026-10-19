using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TwelfthStamp
{
    /// <summary>
    /// The approved key art and logo (Art/Branding/BRANDING.md): a short skippable intro (the full cover, then the logo),
    /// the welcome screen (the cover's picture area on the left, the logo above the menu card on the right), and the
    /// textures used by menu headers, end cards and the loading mark. Desktop draws the art as screen overlays, so the
    /// colours are exactly the supplied files'; VR uses world-locked panels at a comfortable distance, never head-locked.
    /// A missing texture is an error in the log, never a silent swap for another picture.
    /// </summary>
    public class Branding : MonoBehaviour
    {
        public static Branding I { get; private set; }

        public const string Tagline = "You travelled alone. Something came back with you.";
        static Texture2D s_Cover, s_Thumb, s_Logo, s_Menu;
        public static Texture2D Cover => s_Cover != null ? s_Cover : (s_Cover = Load("keyart_cover"));
        public static Texture2D Thumb => s_Thumb != null ? s_Thumb : (s_Thumb = Load("keyart_thumb"));
        public static Texture2D Logo => s_Logo != null ? s_Logo : (s_Logo = Load("keyart_logo"));
        public static Texture2D MenuArt => s_Menu != null ? s_Menu : (s_Menu = Load("keyart_menu"));
        /// <summary>True when every branding texture shipped with the build (checked by AutoTest).</summary>
        public static bool AllPresent => Cover != null && Thumb != null && Logo != null && MenuArt != null;
        static Texture2D s_TitleLogo;
        /// <summary>The bloodied metallic title logo, shown on the intro and the welcome screen only.</summary>
        public static Texture2D TitleLogo => s_TitleLogo != null ? s_TitleLogo : (s_TitleLogo = Load("keyart_titlelogo"));
        public static float TitleLogoAspect => TitleLogo != null ? TitleLogo.width / (float)TitleLogo.height : 3f;
        public static float LogoAspect => Logo != null ? Logo.width / (float)Logo.height : 3f;
        public static float MenuArtAspect => MenuArt != null ? MenuArt.width / (float)MenuArt.height : 1.37f;

        static Texture2D Load(string name)
        {
            var t = Tex.Art("Branding/" + name);
            if (t == null) Debug.LogError("[TS-Brand] Missing branding texture Resources/Art/Branding/" + name + " (run Tools/branding/make_branding.py)");
            return t;
        }

        // Desktop overlays.
        Canvas m_IntroCanvas, m_WelcomeCanvas;
        RawImage m_IntroCover, m_IntroLogo, m_WelcomeArt, m_WelcomeLogo;
        TextMeshProUGUI m_IntroTagline, m_IntroHint;

        // Welcome screen state.
        Transform m_Card;
        float m_CardW, m_CardH;              // card size in metres before any fitting scale
        GameObject m_VrArt, m_VrLogo;

        public bool IntroShowing { get; private set; }
        public bool WelcomeShowing => m_Card != null;

        public static Branding Create(Transform parent)
        {
            var go = new GameObject("Branding");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<Branding>();
            I.Build();
            return I;
        }

        void Build()
        {
            m_IntroCanvas = MakeCanvas("BrandIntroCanvas", 1100);
            var back = new GameObject("Back", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            back.transform.SetParent(m_IntroCanvas.transform, false);
            back.color = Color.black;
            back.raycastTarget = false;
            Stretch(back.rectTransform);
            m_IntroCover = MakeRaw(m_IntroCanvas.transform, "Cover", Cover);
            m_IntroCover.gameObject.SetActive(false); // the intro and welcome screen show the text logo only
            m_IntroLogo = MakeRaw(m_IntroCanvas.transform, "Logo", Logo);
            m_IntroTagline = TextFactory.Ui(m_IntroCanvas.transform, Tagline, 30f, new Color(0.78f, 0.75f, 0.7f, 0f), TextAlignmentOptions.Center, true);
            m_IntroTagline.fontStyle = FontStyles.Italic;
            m_IntroHint = TextFactory.Ui(m_IntroCanvas.transform, "Press any key to skip", 18f, new Color(0.55f, 0.53f, 0.5f, 0f), TextAlignmentOptions.Center);
            foreach (var rt in new[] { m_IntroTagline.rectTransform, m_IntroHint.rectTransform })
            {
                rt.anchorMin = rt.anchorMax = Vector2.zero; // Place() works in canvas pixels from the bottom-left
                rt.pivot = new Vector2(0.5f, 0.5f);
            }
            m_IntroCanvas.gameObject.SetActive(false);

            m_WelcomeCanvas = MakeCanvas("BrandWelcomeCanvas", 900); // below the fade overlay (1000), above the HUD
            m_WelcomeArt = MakeRaw(m_WelcomeCanvas.transform, "MenuArt", MenuArt);
            m_WelcomeArt.gameObject.SetActive(false);
            m_WelcomeLogo = MakeRaw(m_WelcomeCanvas.transform, "Logo", TitleLogo);
            m_WelcomeCanvas.gameObject.SetActive(false);
        }

        Canvas MakeCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(Canvas));
            go.transform.SetParent(transform, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            return c;
        }

        static RawImage MakeRaw(Transform parent, string name, Texture tex)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            img.transform.SetParent(parent, false);
            img.texture = tex;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            return img;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>Pixel rectangle (centre, size) on the overlay canvas; overlay canvases use screen pixels.</summary>
        static void Place(Graphic g, float cx, float cy, float w, float h)
        {
            var rt = g.rectTransform;
            rt.anchoredPosition = new Vector2(cx, cy);
            rt.sizeDelta = new Vector2(w, h);
        }

        // ------------------------------------------------------------------ intro
        /// <summary>
        /// Shown once after the initial load, before the menu or the demo: the full cover on black, then the logo with the
        /// tagline. About four seconds in all; any key, click or controller button skips after a short guard.
        /// </summary>
        public IEnumerator Intro()
        {
            if (TitleLogo == null) yield break;
            IntroShowing = true;
            m_IntroLogo.texture = TitleLogo;
            bool vr = Game.Mode == GameMode.VR;
            Debug.Log("[TS-Brand] Intro shown (" + (vr ? "VR panels" : "desktop overlay") + ")");

            // VR: world-locked panels 2.4 m ahead, drawn over the black fade (nothing else is visible yet).
            Material coverMat = null, logoMat = null;
            TextMeshPro vrTag = null;
            GameObject vrRoot = null;
            bool postWas = false;
            var camData = vr && Game.Rig != null ? Game.Rig.Cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() : null;
            if (vr && Game.Rig != null)
            {
                vrRoot = VrCardRoot(2.4f);
                logoMat = OverlayMat(TitleLogo);
                MeshFactory.Quad("IntroLogo", vrRoot.transform, 1.5f, 1.5f / TitleLogoAspect, logoMat, new Vector3(0f, 0.12f, 0f));
                vrTag = VrText(vrRoot.transform, Tagline, new Vector3(0f, -0.3f, 0f));
                if (camData != null) { postWas = camData.renderPostProcessing; camData.renderPostProcessing = false; } // exact colours
            }
            else m_IntroCanvas.gameObject.SetActive(true);

            // Timeline (seconds): the text logo in 0-0.6, hold to 2.6, out by 3.1. No cover picture.
            const float logoIn = 0.6f, logoHold = 2.6f, outEnd = 3.1f;
            float t = 0f;
            bool skipped = false;
            while (t < outEnd)
            {
                t += Time.unscaledDeltaTime;
                if (t > 0.35f && !skipped && AnySkipPressed())
                {
                    skipped = true;
                    t = Mathf.Max(t, outEnd - 0.25f); // quick fade from wherever it is
                }
                float coverA = 0f;
                float logoA = t < logoIn ? t / logoIn : t < logoHold ? 1f : Mathf.Clamp01(1f - (t - logoHold) / (outEnd - logoHold));
                if (skipped) { float k = Mathf.Clamp01((outEnd - t) / 0.25f); coverA = Mathf.Min(coverA, k); logoA = Mathf.Min(logoA, k); }
                if (vr)
                {
                    if (coverMat != null) coverMat.SetColor("_Color", new Color(1f, 1f, 1f, coverA));
                    if (logoMat != null) logoMat.SetColor("_Color", new Color(1f, 1f, 1f, logoA));
                    if (vrTag != null) vrTag.alpha = logoA * 0.9f;
                }
                else LayoutIntro(coverA, logoA, t);
                yield return null;
            }
            if (vr)
            {
                if (camData != null) camData.renderPostProcessing = postWas;
                if (vrRoot != null) Destroy(vrRoot);
                if (coverMat != null) Destroy(coverMat);
                if (logoMat != null) Destroy(logoMat);
            }
            m_IntroCanvas.gameObject.SetActive(false);
            IntroShowing = false;
            Debug.Log("[TS-Brand] Intro done" + (skipped ? " (skipped)" : ""));
        }

        void LayoutIntro(float coverA, float logoA, float t)
        {
            float W = Screen.width, H = Screen.height;
            // Square cover, never stretched: 74% of the window height (or width on a very tall window).
            float side = Mathf.Min(H * 0.74f, W * 0.74f);
            Place(m_IntroCover, W * 0.5f, H * 0.52f, side, side);
            m_IntroCover.color = new Color(1f, 1f, 1f, coverA);
            float lw = Mathf.Min(W * 0.64f, H * 1.75f);
            float aspect = m_IntroLogo.texture != null ? m_IntroLogo.texture.width / (float)m_IntroLogo.texture.height : LogoAspect;
            Place(m_IntroLogo, W * 0.5f, H * 0.56f, lw, lw / aspect);
            m_IntroLogo.color = new Color(1f, 1f, 1f, logoA);
            m_IntroTagline.fontSize = Mathf.Round(H * 0.03f);
            Place(m_IntroTagline, W * 0.5f, H * 0.56f - lw / aspect * 0.5f - H * 0.06f, W * 0.8f, H * 0.06f);
            m_IntroTagline.alpha = logoA * 0.9f;
            m_IntroHint.fontSize = Mathf.Round(H * 0.018f);
            Place(m_IntroHint, W * 0.5f, H * 0.04f, W * 0.6f, H * 0.03f);
            m_IntroHint.alpha = Mathf.Clamp01((t - 1.2f) * 1.5f) * Mathf.Max(coverA, logoA) * 0.7f;
        }

        /// <summary>VR: an anchor 'distance' metres ahead at eye height, facing the player, fixed in the world.</summary>
        static GameObject VrCardRoot(float distance)
        {
            var root = new GameObject("BrandCardVR");
            var head = Game.Rig.Head;
            var fwd = Game.Rig.Forward;
            root.transform.SetPositionAndRotation(head.position + fwd * distance, Quaternion.LookRotation(fwd, Vector3.up));
            return root;
        }

        /// <summary>Unlit, transparent, drawn after the VR fade sphere (queue 4001, ZTest Always) so it shows over black.</summary>
        static Material OverlayMat(Texture tex)
        {
            var m = Mats.UnlitInstance(new Color(1f, 1f, 1f, 0f), tex, true, 1f);
            m.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            m.SetFloat("_ZWrite", 0f);
            m.renderQueue = 4001;
            return m;
        }

        static TextMeshPro VrText(Transform parent, string text, Vector3 local)
        {
            var t = TextFactory.World(parent, text, 0.035f, new Color(0.78f, 0.75f, 0.7f, 0f), TextAlignmentOptions.Center, true, 1.8f);
            t.transform.localPosition = local;
            t.fontStyle = FontStyles.Italic;
            var r = t.GetComponent<MeshRenderer>();
            if (r != null)
            {
                var m = new Material(r.sharedMaterial);
                m.renderQueue = 4002;
                if (m.HasProperty("unity_GUIZTestMode")) m.SetFloat("unity_GUIZTestMode", (float)UnityEngine.Rendering.CompareFunction.Always);
                r.sharedMaterial = m;
            }
            return t;
        }

        /// <summary>
        /// End card over black: the logo and one line under it. Used when the judges' demo ends and before the story's
        /// results. The caller fades to black first; the card fades in and out by itself.
        /// </summary>
        public IEnumerator EndCard(string line, float seconds = 3.2f)
        {
            if (Logo == null) yield break;
            bool vr = Game.Mode == GameMode.VR;
            Material logoMat = null;
            TextMeshPro vrLine = null;
            GameObject vrRoot = null;
            if (vr && Game.Rig != null)
            {
                vrRoot = VrCardRoot(2.4f);
                logoMat = OverlayMat(Logo);
                MeshFactory.Quad("EndLogo", vrRoot.transform, 1.5f, 1.5f / LogoAspect, logoMat, new Vector3(0f, 0.1f, 0f));
                vrLine = VrText(vrRoot.transform, line, new Vector3(0f, -0.32f, 0f));
            }
            else
            {
                m_IntroCanvas.gameObject.SetActive(true);
                m_IntroLogo.texture = Logo; // end cards keep the original logo
                m_IntroCover.color = new Color(1f, 1f, 1f, 0f);
            }
            string saved = m_IntroTagline.text;
            m_IntroTagline.text = line;
            Debug.Log("[TS-Brand] End card: " + line);
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float a = Mathf.Min(Mathf.Clamp01(t / 0.6f), Mathf.Clamp01((seconds - t) / 0.6f));
                if (vr) { if (logoMat != null) logoMat.SetColor("_Color", new Color(1f, 1f, 1f, a)); if (vrLine != null) vrLine.alpha = a * 0.9f; }
                else { LayoutIntro(0f, a, 0f); m_IntroHint.alpha = 0f; }
                yield return null;
            }
            m_IntroTagline.text = saved;
            if (vrRoot != null) Destroy(vrRoot);
            if (logoMat != null) Destroy(logoMat);
            m_IntroCanvas.gameObject.SetActive(false);
        }

        static bool AnySkipPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)) return true;
            var input = Game.Input;
            if (input != null && Game.Mode == GameMode.VR)
                foreach (var a in new[] { input.LTrigger, input.RTrigger, input.A, input.B, input.X, input.Y })
                    if (a != null && a.WasPressedThisFrame()) return true;
            return false;
        }

        // ------------------------------------------------------------------ welcome screen
        /// <summary>
        /// Lays out the welcome screen around the main-menu card (cardW x cardH metres, centred on its transform).
        /// Desktop: the cover's picture area on the left and the logo top right, as overlays; the card is moved (and if
        /// necessary scaled) to sit under the logo, square to the camera. VR: the same pieces as panels beside the card.
        /// </summary>
        public void ShowWelcome(Transform card, float cardW, float cardH)
        {
            if (card == null || TitleLogo == null) return;
            m_Card = card;
            m_CardW = cardW;
            m_CardH = cardH;
            if (Game.Mode == GameMode.VR)
            {
                if (m_VrLogo == null)
                {
                    m_VrLogo = MeshFactory.Quad("WelcomeLogo", card, 1f, 1f, Mats.UnlitInstance(Color.white, TitleLogo, true, 1.08f), Vector3.zero);
                    m_VrLogo.layer = Layers.UI3D;
                }
                float logoW = cardW * 1.35f;
                SetLocal(m_VrLogo, card, new Vector3(0f, cardH * 0.5f + 0.03f + logoW / TitleLogoAspect * 0.5f, 0.003f), new Vector3(logoW, logoW / TitleLogoAspect, 1f));
                m_VrLogo.SetActive(true);
            }
            else
            {
                m_WelcomeCanvas.gameObject.SetActive(true);
                LayoutWelcomeDesktop();
            }
        }

        static void SetLocal(GameObject go, Transform parent, Vector3 pos, Vector3 scale)
        {
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;
        }

        public void HideWelcome()
        {
            if (m_Card != null) m_Card.localScale = Vector3.one;
            m_Card = null;
            if (m_WelcomeCanvas != null) m_WelcomeCanvas.gameObject.SetActive(false);
            if (m_VrArt != null) m_VrArt.SetActive(false);
            if (m_VrLogo != null) m_VrLogo.SetActive(false);
        }

        void LateUpdate()
        {
            if (m_Card == null) return;
            if (!m_Card.gameObject.activeInHierarchy) { HideWelcome(); return; }
            if (Game.Mode == GameMode.Desktop) LayoutWelcomeDesktop();
        }

        void LayoutWelcomeDesktop()
        {
            var rig = Game.Rig;
            if (rig == null || rig.Cam == null) return;
            var cam = rig.Cam;
            float W = Screen.width, H = Screen.height;
            // One centred column: the text logo at the top, the card under it (no cover picture).
            float colLeft = W * 0.2f, colRight = W * 0.8f;
            float colW = colRight - colLeft, colCx = (colLeft + colRight) * 0.5f;
            float logoW = Mathf.Min(colW * 0.96f, H * 0.62f * TitleLogoAspect * 0.5f);
            float logoH = logoW / TitleLogoAspect;
            float logoTop = H * 0.92f;
            Place(m_WelcomeLogo, colCx, logoTop - logoH * 0.5f, logoW, logoH);
            float areaTop = logoTop - logoH - H * 0.025f, areaBottom = H * 0.05f;

            // Card: square to the camera at 1.05 m, centred in the remaining area; scaled down only if it would not fit.
            const float dist = 0.9f; // a little closer than other pages: the welcome card reads at a glance
            float worldPerPx = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / H;
            float fit = Mathf.Min(1f, (areaTop - areaBottom) / (m_CardH / worldPerPx), colW / (m_CardW / worldPerPx));
            var ray = cam.ScreenPointToRay(new Vector3(colCx, (areaTop + areaBottom) * 0.5f, 0f));
            float along = dist / Mathf.Max(0.2f, Vector3.Dot(ray.direction, cam.transform.forward));
            m_Card.position = ray.GetPoint(along);
            m_Card.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
            m_Card.localScale = Vector3.one * fit;
        }
    }
}
